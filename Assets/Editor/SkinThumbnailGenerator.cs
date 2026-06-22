using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SkinThumbnailGenerator
{
    const string GundamModelPath = "Assets/Prefabs/Skins/Gundam/gundam_unicorn_walking.glb";
    const string GundamOutputPath = "Assets/UI/Generated/gundam_unicorn_preview.png";
    const string OdinModelPath = "Assets/Prefabs/Skins/Odin/odin_optimized.glb";
    const string OdinOutputPath = "Assets/UI/Generated/odin_preview.png";

    [MenuItem("Tools/3DCoCaNgua/Generate Gundam Shop Preview")]
    public static void GenerateGundamPreview()
    {
        GeneratePreview(
            GundamModelPath,
            GundamOutputPath,
            Quaternion.Euler(270f, 0f, 0f) * Quaternion.Euler(180f, 180f, 0f));
    }

    [MenuItem("Tools/3DCoCaNgua/Generate Odin Shop Preview")]
    public static void GenerateOdinPreview()
    {
        GeneratePreview(OdinModelPath, OdinOutputPath, Quaternion.Euler(0f, 180f, 0f));
    }

    static void GeneratePreview(string modelPath, string outputPath, Quaternion rotation)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null) throw new FileNotFoundException("Skin GLB was not found.", modelPath);

        GameObject model = Object.Instantiate(source);
        model.name = "SkinThumbnailModel";
        model.hideFlags = HideFlags.HideAndDontSave;
        model.transform.rotation = rotation;

        Bounds bounds = CalculateBounds(model);
        model.transform.position -= bounds.center;
        bounds = CalculateBounds(model);

        GameObject cameraObject = new GameObject("ThumbnailCamera", typeof(Camera));
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f, 1f);
        camera.fieldOfView = 30f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = bounds.size.magnitude * 8f;

        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float distance = bounds.extents.y / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) + bounds.extents.z + maxSize * 0.12f;
        camera.transform.position = new Vector3(maxSize * 0.18f, maxSize * 0.04f, -distance);
        camera.transform.LookAt(Vector3.zero, Vector3.up);

        GameObject keyObject = CreateLight("KeyLight", new Vector3(-maxSize, maxSize, -maxSize), 1.25f, new Color(1f, 0.94f, 0.86f));
        GameObject fillObject = CreateLight("FillLight", new Vector3(maxSize, maxSize * 0.35f, -maxSize * 0.25f), 0.8f, new Color(0.62f, 0.76f, 1f));
        GameObject rimObject = CreateLight("RimLight", new Vector3(0f, maxSize, maxSize), 1.1f, new Color(0.55f, 0.72f, 1f));

        RenderTexture renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        renderTexture.antiAliasing = 4;
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        File.WriteAllBytes(outputPath, image.EncodeToPNG());

        Object.DestroyImmediate(image);
        Object.DestroyImmediate(renderTexture);
        Object.DestroyImmediate(model);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(keyObject);
        Object.DestroyImmediate(fillObject);
        Object.DestroyImmediate(rimObject);

        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
    }

    static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
        bool initialized = false;

        foreach (Renderer renderer in renderers)
        {
            if (!initialized) { bounds = renderer.bounds; initialized = true; }
            else bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    static GameObject CreateLight(string name, Vector3 position, float intensity, Color color)
    {
        GameObject target = new GameObject(name, typeof(Light));
        target.hideFlags = HideFlags.HideAndDontSave;
        target.transform.position = position;
        target.transform.LookAt(Vector3.zero);
        Light light = target.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.Soft;
        return target;
    }
}
