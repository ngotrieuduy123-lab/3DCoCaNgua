using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BoardFaceFixer
{
    private const string TexturePath = "Assets/Prefabs/2aOboQZxA67SjoSxmQhqbSeE9kQcsi9vAVYKbuhU.jpg";
    private const string MaterialPath = "Assets/Prefabs/BoardFace_Ludo.mat";

    [MenuItem("Tools/Co Ca Ngua/Fix Board Face Texture")]
    public static void ApplyBoardFaceTexture()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError($"Board face texture not found: {TexturePath}");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "BoardFace_Ludo" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);

        var plane = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "Plane" && t.parent != null && t.parent.name == "ludo_boardFull");

        if (plane == null)
            plane = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Plane");

        if (plane == null || !plane.TryGetComponent<MeshRenderer>(out var renderer))
        {
            Debug.LogError("Could not find the board Plane MeshRenderer in the open scene.");
            return;
        }

        var slots = renderer.sharedMaterials.Length > 0 ? renderer.sharedMaterials.Length : 1;
        renderer.sharedMaterials = Enumerable.Repeat(material, slots).ToArray();
        EditorUtility.SetDirty(renderer);
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Applied {MaterialPath} to {GetPath(plane)}. Save the scene when it looks correct.");
    }

    private static string GetPath(Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
