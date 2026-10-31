using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Installs the rigged Mark FBX into the existing MGB scene.</summary>
[InitializeOnLoad]
public static class MarkCharacterBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ModelPath = "Assets/MGB/Model/Mark.fbx";
    private const string ControllerPath = "Assets/MGB/Model/Mark.controller";
    private const string MaterialFolder = "Assets/MGB/Model/Materials";

    static MarkCharacterBuilder()
    {
        EditorApplication.delayCall += InstallIfNeeded;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += InstallIfNeeded;
        EditorApplication.projectChanged += InstallIfNeeded;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += InstallIfNeeded;
        };
    }

    [MenuItem("MGB/Install Real Mark Character")]
    public static void Rebuild()
    {
        Install(true);
    }

    // Used for a reproducible import without relying on the editor window state.
    public static void BatchInstall()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Install(true);
    }

    private static void InstallIfNeeded()
    {
        Install(false);
    }

    private static void Install(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;

        GameObject mark = GameObject.Find("Mark");
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (mark == null || model == null) return;

        Transform existing = mark.transform.Find("MarkVisual");
        if (!force && existing != null && existing.GetComponentInChildren<Animator>() != null)
            return;

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();
        AnimationClip idle = FindClip(clips, "Idle");
        AnimationClip walk = FindClip(clips, "Walk");
        if (idle == null || walk == null)
        {
            Debug.LogError("MGB: Mark.fbx must contain Idle and Walk clips. Found: " +
                string.Join(", ", clips.Select(c => c.name)));
            return;
        }

        AnimatorController controller = BuildController(idle, walk);
        if (controller == null) return;

        // Keep the old visual until the real model and animations are ready.
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        instance.name = "MarkVisual";
        instance.transform.SetParent(mark.transform, false);
        instance.transform.localPosition = new Vector3(0f, -1f, 0f);
        instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        instance.transform.localScale = Vector3.one;

        ApplyUrpMaterials(instance);
        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        MarkWalkAnimator oldWalk = mark.GetComponent<MarkWalkAnimator>();
        if (oldWalk != null) UnityEngine.Object.DestroyImmediate(oldWalk);
        MarkAnimationDriver driver = mark.GetComponent<MarkAnimationDriver>();
        if (driver == null) driver = mark.AddComponent<MarkAnimationDriver>();
        driver.Configure(instance.transform, animator);

        MeshRenderer capsule = mark.GetComponent<MeshRenderer>();
        if (capsule != null) capsule.enabled = false;
        Camera camera = Camera.main;
        if (camera != null)
        {
            MarkCameraFollow follow = camera.GetComponent<MarkCameraFollow>();
            if (follow == null) follow = camera.gameObject.AddComponent<MarkCameraFollow>();
            follow.SetTarget(mark.transform);
        }

        EditorUtility.SetDirty(mark);
        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MGB: Rigged Mark installed with Idle/Walk animation and URP materials.");
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string name)
    {
        return clips.FirstOrDefault(c => c.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? clips.FirstOrDefault(c => c.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase));
    }

    private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null) return controller;

        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle");
        AnimatorState walkState = machine.AddState("Walk");
        idleState.motion = idle;
        walkState.motion = walk;
        machine.defaultState = idleState;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.15f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void ApplyUrpMaterials(GameObject instance)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("MGB: Universal Render Pipeline/Lit shader is missing.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/MGB/Model", "Materials");

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] assigned = renderer.sharedMaterials;
            for (int i = 0; i < assigned.Length; i++)
            {
                Material source = assigned[i];
                if (source == null) continue;
                string safe = string.Concat(source.name.Select(c =>
                    char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
                string path = MaterialFolder + "/" + safe + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = source.name };
                    Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                        source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    material.SetColor("_BaseColor", color);
                    Texture texture = source.mainTexture;
                    if (texture != null) material.SetTexture("_BaseMap", texture);
                    string lower = source.name.ToLowerInvariant();
                    if (lower.Contains("hair") || lower.Contains("lash") || lower.Contains("brow"))
                    {
                        material.SetFloat("_AlphaClip", 1f);
                        material.SetFloat("_Cutoff", 0.25f);
                        material.EnableKeyword("_ALPHATEST_ON");
                        material.SetFloat("_Cull", 0f);
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }
                    AssetDatabase.CreateAsset(material, path);
                }
                assigned[i] = material;
            }
            renderer.sharedMaterials = assigned;
            EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
    }
}
