using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Asset authoring and repeatable checks only. Does not modify gameplay scenes or PlayerPrefs.
public static class AryaSkinSetup
{
    const string Root = "Assets/Prefabs/Skins/Arya";
    const string DefinitionPath = "Assets/Prefabs/Skins/Definitions/Arya.asset";
    const string Source = Root + "/Source/";

    [MenuItem("Tools/3DCoCaNgua/Arya/Build Skin Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build skin assets outside Play Mode.");
        Directory.CreateDirectory(Root + "/Textures");
        Directory.CreateDirectory(Root + "/Animations");
        AssetDatabase.Refresh();

        var walkingImporter = (ModelImporter)AssetImporter.GetAtPath(Source + "Walking.fbx");
        if (!Directory.GetFiles(Root + "/Textures", "*.png").Any())
        {
            if (!walkingImporter.ExtractTextures(Root + "/Textures"))
                throw new InvalidOperationException("Walking.fbx contains no extractable texture.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        string texturePath = Directory.GetFiles(Root + "/Textures", "*.png").First().Replace('\\', '/');
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        textureImporter.textureType = TextureImporterType.Default;
        textureImporter.sRGBTexture = true;
        textureImporter.maxTextureSize = 1024;
        textureImporter.mipmapEnabled = true;
        textureImporter.textureCompression = TextureImporterCompression.Compressed;
        textureImporter.compressionQuality = 80;
        textureImporter.SaveAndReimport();

        foreach (string file in new[] { "Walking.fbx", "Left Turn.fbx", "Right Turn.fbx" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Source + file);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Arya_Material.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, Root + "/Arya_Material.mat");
        }
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(material);

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Walking.fbx"), preview);
            model.name = "AryaSkin";
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();

            var walk = Clip(Source + "Walking.fbx");
            var left = Clip(Source + "Left Turn.fbx");
            var right = Clip(Source + "Right Turn.fbx");
            walk.SampleAnimation(model, 0f);
            Transform hips = model.GetComponentsInChildren<Transform>().First(t => t.name.EndsWith("Hips"));
            Vector3 referencePosition = hips.position;
            AnimationClip walkClip = MakeInPlace(model, walk, referencePosition, false, "Arya_Walk");
            AnimationClip leftClip = MakeInPlace(model, left, referencePosition, true, "Arya_LeftTurn");
            AnimationClip rightClip = MakeInPlace(model, right, referencePosition, true, "Arya_RightTurn");

            walkClip.SampleAnimation(model, 0f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, Root + "/AryaSkin.prefab");
            Bounds nativeBounds = BakedBounds(model);
            float targetHeight = GundamHeight(preview);
            float scale = targetHeight / nativeBounds.size.y;
            if (scale <= 0f || !float.IsFinite(scale)) throw new InvalidOperationException("Invalid character dimensions.");

            var definition = AssetDatabase.LoadAssetAtPath<SkinDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SkinDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            definition.id = "arya";
            definition.displayName = "Arya";
            definition.description = "Arya with walking and left/right turn animations.";
            definition.price = 5000;
            definition.includedByDefault = false;
            definition.previewImage = null;
            definition.modelPrefab = prefab;
            definition.movementStyle = SkinMovementStyle.Walk;
            definition.walkAnimation = walkClip;
            definition.turnLeftAnimation = leftClip;
            definition.turnRightAnimation = rightClip;
            definition.turnPlaybackSpeed = 1.8f;
            definition.turnThresholdDegrees = 12f;
            definition.movementSpeed = 0.28f;
            definition.localScale = Vector3.one * scale;
            // The existing chess-piece root is -90 degrees on X. Model Y-up must become piece-local Z-up.
            definition.localEulerAngles = new Vector3(90f, 0f, 0f);
            definition.localPosition = new Vector3(0f, 0f, -nativeBounds.min.y * scale);
            EditorUtility.SetDirty(definition);

            SkinCatalog catalog = SkinCatalog.Load();
            catalog.skins.RemoveAll(s => s != null && s.id == "arya");
            catalog.skins.Add(definition);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("ARYA_BUILD_OK: source height=" + nativeBounds.size.y.ToString("F6") + ", skin scale=" + scale.ToString("F6") + ", target height=" + targetHeight.ToString("F6") + ". " + Validate());
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

    static AnimationClip MakeInPlace(GameObject model, AnimationClip source, Vector3 referencePosition, bool removeTurnYaw, string name)
    {
        AnimationClip output = Object.Instantiate(source);
        output.name = name;
        output.legacy = false;
        Transform hips = model.GetComponentsInChildren<Transform>().First(t => t.name.EndsWith("Hips"));
        string path = AnimationUtility.CalculateTransformPath(hips, model.transform);
        var positions = new[] { new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>() };
        var rotations = new[] { new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>() };
        source.SampleAnimation(model, 0f);
        Vector3 startForward = Vector3.ProjectOnPlane(hips.forward, Vector3.up).normalized;
        if (startForward.sqrMagnitude < 0.5f) throw new InvalidOperationException("Cannot determine the hips heading.");
        int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * source.frameRate));
        Quaternion previous = hips.localRotation;
        for (int frame = 0; frame <= frames; frame++)
        {
            float time = source.length * frame / frames;
            source.SampleAnimation(model, time);
            Vector3 position = hips.position;
            position.x = referencePosition.x;
            position.z = referencePosition.z;
            position = hips.parent.InverseTransformPoint(position);
            Quaternion rotation = hips.localRotation;
            if (removeTurnYaw)
            {
                // The game rotates SkinFacingPivot. Remove clip yaw to avoid turning twice then snapping back.
                float yaw = Vector3.SignedAngle(startForward, Vector3.ProjectOnPlane(hips.forward, Vector3.up), Vector3.up);
                rotation = Quaternion.Inverse(hips.parent.rotation) * Quaternion.AngleAxis(-yaw, Vector3.up) * hips.rotation;
            }
            if (Quaternion.Dot(previous, rotation) < 0f) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            previous = rotation;
            for (int axis = 0; axis < 3; axis++) positions[axis].Add(new Keyframe(time, position[axis]));
            for (int axis = 0; axis < 4; axis++) rotations[axis].Add(new Keyframe(time, rotation[axis]));
        }
        for (int axis = 0; axis < 3; axis++)
            AnimationUtility.SetEditorCurve(output, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[axis]), LinearCurve(positions[axis]));
        for (int axis = 0; axis < 4; axis++)
            AnimationUtility.SetEditorCurve(output, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]), LinearCurve(rotations[axis]));
        foreach (var binding in AnimationUtility.GetCurveBindings(output).Where(b => b.type == typeof(Transform) && b.path.Length == 0))
            AnimationUtility.SetEditorCurve(output, binding, null);
        output.EnsureQuaternionContinuity();
        AnimationUtility.SetAnimationEvents(output, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(output);
        settings.loopTime = !removeTurnYaw;
        AnimationUtility.SetAnimationClipSettings(output, settings);
        string destination = Root + "/Animations/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
        if (existing == null) { AssetDatabase.CreateAsset(output, destination); return output; }
        EditorUtility.CopySerialized(output, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(output);
        return existing;
    }

    static AnimationCurve LinearCurve(List<Keyframe> keys)
    {
        var curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < keys.Count; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    static float GundamHeight(Scene scene)
    {
        SkinDefinition gundam = SkinCatalog.Load().Get("gundam_unicorn");
        var frame = new GameObject("GundamMeasurementFrame");
        SceneManager.MoveGameObjectToScene(frame, scene);
        frame.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(gundam.modelPrefab, scene);
        model.transform.SetParent(frame.transform, false);
        model.transform.localPosition = gundam.localPosition;
        model.transform.localEulerAngles = gundam.localEulerAngles;
        model.transform.localScale = gundam.localScale;
        // Imported glTF skinning matrices are not evaluated by SampleAnimation in a preview scene.
        // Its authored renderer bounds give the same reference size used by the live renderer.
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        float height = bounds.size.y;
        Object.DestroyImmediate(frame);
        return height;
    }

    public static Bounds BakedBounds(GameObject model)
    {
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            // Compensate renderer scale before TransformPoint applies its world transform.
            renderer.BakeMesh(mesh, true);
            foreach (var vertex in mesh.vertices)
            {
                Vector3 point = renderer.transform.TransformPoint(vertex);
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
            Object.DestroyImmediate(mesh);
        }
        if (first) throw new InvalidOperationException("No skinned mesh found.");
        return bounds;
    }

    public static string Validate()
    {
        SkinDefinition skin = SkinCatalog.Load().Get("arya");
        if (skin == null || skin.modelPrefab == null || skin.walkAnimation == null || skin.turnLeftAnimation == null || skin.turnRightAnimation == null)
            throw new InvalidOperationException("Arya references are incomplete.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var frame = new GameObject("ActualPieceFrame");
            SceneManager.MoveGameObjectToScene(frame, preview);
            frame.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            frame.transform.localScale = Vector3.one * 16.5f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(skin.modelPrefab, preview);
            model.transform.SetParent(frame.transform, false);
            model.transform.localPosition = skin.localPosition;
            model.transform.localRotation = Quaternion.Euler(skin.localEulerAngles);
            model.transform.localScale = skin.localScale;
            var bones = model.GetComponentsInChildren<Transform>();
            Transform head = bones.First(b => b.name.EndsWith("Head"));
            Transform hips = bones.First(b => b.name.EndsWith("Hips"));
            Transform leftFoot = bones.First(b => b.name.EndsWith("LeftFoot"));
            Transform rightFoot = bones.First(b => b.name.EndsWith("RightFoot"));
            skin.walkAnimation.SampleAnimation(model, 0f);
            Bounds standingBounds = BakedBounds(model);
            float referenceHeight = GundamHeight(preview) * frame.transform.localScale.y;
            if (Mathf.Abs(standingBounds.min.y) > 0.001f ||
                Mathf.Abs(standingBounds.size.y - referenceHeight) > referenceHeight * 0.05f)
                throw new InvalidOperationException("Arya must touch the board and match the other skin's height.");
            float minUp = 1f, maxDrift = 0f, maxFootMotion = 0f;
            foreach (AnimationClip clip in new[] { skin.walkAnimation, skin.turnLeftAnimation, skin.turnRightAnimation })
            {
                clip.SampleAnimation(model, 0f);
                Vector3 rootStart = hips.position;
                Vector3 footStart = leftFoot.position;
                float clipFootMotion = 0f;
                for (int sample = 0; sample <= 30; sample++)
                {
                    clip.SampleAnimation(model, clip.length * sample / 30f);
                    float up = Vector3.Dot((head.position - (leftFoot.position + rightFoot.position) / 2f).normalized, Vector3.up);
                    minUp = Mathf.Min(minUp, up);
                    maxDrift = Mathf.Max(maxDrift, Vector3.ProjectOnPlane(hips.position - rootStart, Vector3.up).magnitude);
                    clipFootMotion = Mathf.Max(clipFootMotion, (leftFoot.position - footStart).magnitude);
                }
                if (clipFootMotion < 0.0001f)
                    throw new InvalidOperationException(clip.name + " does not animate Arya's feet.");
                maxFootMotion = Mathf.Max(maxFootMotion, clipFootMotion);
            }
            if (minUp < 0.9f || maxDrift > 0.00001f || maxFootMotion < 0.0001f)
                throw new InvalidOperationException("Arya pose validation failed: up=" + minUp + " drift=" + maxDrift + " motion=" + maxFootMotion);
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (renderer.sharedMaterials.Any(m => m == null || m.mainTexture == null || m.shader.name != "Universal Render Pipeline/Lit"))
                    throw new InvalidOperationException("Arya has an invalid material or texture.");
            var deps = AssetDatabase.GetDependencies(Root + "/AryaSkin.prefab", true);
            if (deps.Any(p => p.EndsWith("Left Turn.fbx") || p.EndsWith("Right Turn.fbx")))
                throw new InvalidOperationException("Turn FBX geometry should not be a prefab dependency.");
            return "ARYA_VALIDATED: 93 pose samples; minimum upright=" + minUp.ToString("F4") + "; horizontal root drift=" + maxDrift.ToString("F8") + "; foot motion=" + maxFootMotion.ToString("F6");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
