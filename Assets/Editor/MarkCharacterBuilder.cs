using System;
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
    private static bool installing;
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
        if (installing) return;
        installing = true;
        try { InstallCore(force); }
        finally { installing = false; }
    }

    private static void InstallCore(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;

        GameObject mark = scene.GetRootGameObjects().FirstOrDefault(g =>
            g.name == "Mark" && g.GetComponent<PlayerMovement>() != null);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (mark == null || model == null) return;

        bool removedDuplicate = false;
        foreach (Transform child in mark.transform.Cast<Transform>().ToArray())
        {
            if (child.name != "Mark" || !PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                continue;
            UnityEngine.Object.DestroyImmediate(child.gameObject);
            removedDuplicate = true;
        }
        if (removedDuplicate)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Transform existing = mark.transform.Find("MarkVisual");
        if (!force && existing != null && existing.GetComponentInChildren<Animator>() != null)
            return;

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();
        AnimationClip idle = FindClip(clips, "Idle");
        AnimationClip walk = FindClip(clips, "Walk");
        AnimatorController controller = idle != null && walk != null
            ? BuildController(idle, walk) : null;

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
        Debug.Log(controller == null
            ? "MGB: Rigged Mark installed with textured materials and procedural walking."
            : "MGB: Rigged Mark installed with Idle/Walk animation and URP materials.");
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
                    AssetDatabase.CreateAsset(material, path);
                }
                Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                    source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                string lower = source.name.ToLowerInvariant();
                if (lower == "hair") ColorUtility.TryParseHtmlString("#48352A", out color);
                if (lower.Contains("uniform shirt")) ColorUtility.TryParseHtmlString("#292929", out color);
                if (lower.Contains("buttons")) ColorUtility.TryParseHtmlString("#111111", out color);
                if (lower.Contains("jeans")) ColorUtility.TryParseHtmlString("#55585B", out color);
                material.SetColor("_BaseColor", color);
                Texture texture = GetTexture(source.name, false);
                if (texture == null && lower != "hair") texture = source.mainTexture;
                if (lower.Contains("uniform shirt") || lower.Contains("jeans")) texture = null;
                material.SetTexture("_BaseMap", texture);
                Texture normal = GetTexture(source.name, true);
                if (lower.Contains("uniform shirt") || lower.Contains("jeans")) normal = null;
                material.SetTexture("_BumpMap", normal);
                if (normal != null) material.EnableKeyword("_NORMALMAP");
                else material.DisableKeyword("_NORMALMAP");
                bool transparent = lower.Contains("skin details") ||
                    lower.Contains("lash") || lower.Contains("brow");
                material.SetFloat("_AlphaClip", 0f);
                material.SetFloat("_Surface", transparent ? 1f : 0f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", transparent ? 0f : 1f);
                material.SetFloat("_Cull", lower == "hair" ? 0f : 2f);
                material.SetFloat("_Smoothness", lower == "hair" ? 0.18f : 0.35f);
                material.DisableKeyword("_ALPHATEST_ON");
                if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                material.renderQueue = transparent
                    ? (int)UnityEngine.Rendering.RenderQueue.Transparent
                    : (int)UnityEngine.Rendering.RenderQueue.Geometry;
                EditorUtility.SetDirty(material);
                assigned[i] = material;
            }
            renderer.sharedMaterials = assigned;
            EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
    }

    private static Texture GetTexture(string materialName, bool normal)
    {
        string lower = materialName.ToLowerInvariant();
        string file = null;
        if (lower.Contains("skin details")) file = normal ? null : "look_15k5c1j.png";
        else if (lower.Contains("skin")) file = normal ? null : "skin_young_caucasian_male2.jpg";
        else if (lower.Contains("eyebrow")) file = normal ? null : "eyebrows_eyebrow001.png";
        else if (lower.Contains("eyelash")) file = normal ? null : "eyelashes_eyelashes01.png";
        else if (lower.Contains("eyes")) file = normal ? null : "eyes_grey.png";
        else if (lower.Contains("teeth")) file = normal ? null : "teeth_teeth_base.png";
        else if (lower.Contains("tongue")) file = normal ? null : "tongue_tongue01.jpg";
        else if (lower.Contains("jeans legs")) file = normal ? "gen_14zfe7w_n.png" : "gen_14zfe7w.png";
        else if (lower.Contains("baggy jeans")) file = normal ? "gen_ye6bh6_n.png" : "gen_ye6bh6.png";
        else if (lower.Contains("uniform shirt")) file = normal ? "gen_rlv3fi_n.png" : "gen_rlv3fi.png";
        else if (lower.Contains("sneaker leather")) file = normal ? "gen_1ilymrh_n.png" : "gen_1ilymrh.png";
        return file == null ? null : AssetDatabase.LoadAssetAtPath<Texture>(
            "Assets/MGB/Model/Textures/" + file);
    }
}
