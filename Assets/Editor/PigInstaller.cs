using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PigInstaller
{
    private const string ModelPath = "Assets/MGB/Pig/Pig.fbx";
    private const string Folder = "Assets/MGB/Pig";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("MGB/Add Hryak to Field")]
    public static void AddPig()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("MGB: Open SampleScene before adding the pig.");
            return;
        }

        if (scene.GetRootGameObjects().Any(root => root.name == "Хряк"))
        {
            Debug.Log("MGB: Хряк is already in the scene.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (prefab == null || shader == null)
        {
            Debug.LogError("MGB: Pig FBX or URP Lit shader is missing.");
            return;
        }

        Material pink = GetMaterial("PigPink", shader, "#D995A0");
        Material dark = GetMaterial("PigDark", shader, "#5B382F");
        GameObject pig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        pig.name = "Хряк";
        pig.transform.position = Vector3.zero;
        pig.transform.rotation = Quaternion.Euler(0f, -30f, 0f);

        foreach (Renderer renderer in pig.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = slots[i] != null && slots[i].name.Contains("003") ? pink : dark;
            renderer.sharedMaterials = slots;
        }

        Renderer[] renderers = pig.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y > 0.001f)
                pig.transform.localScale *= 1.05f / bounds.size.y;

            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            pig.transform.position = new Vector3(2.2f, -bounds.min.y, 0.8f);
        }
        else
        {
            pig.transform.position = new Vector3(2.2f, 0f, 0.8f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = pig;
        Debug.Log("MGB: Added Хряк to the field.");
    }

    private static Material GetMaterial(string name, Shader shader, string hexColor)
    {
        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        ColorUtility.TryParseHtmlString(hexColor, out Color color);
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}
