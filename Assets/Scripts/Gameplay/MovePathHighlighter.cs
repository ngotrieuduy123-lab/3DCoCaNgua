using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MovePathHighlighter : MonoBehaviour
{
    public static MovePathHighlighter Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject holder = new GameObject("MovePathHighlighter");
                instance = holder.AddComponent<MovePathHighlighter>();
            }

            return instance;
        }
    }

    class Marker
    {
        public Transform transform;
        public Renderer renderer;
        public Material material;
        public Vector3 baseScale;
        public bool isTarget;
        public int playerIndex;
    }

    static MovePathHighlighter instance;

    readonly List<GameObject> markerObjects = new List<GameObject>();
    readonly List<Marker> markers = new List<Marker>();
    readonly HashSet<string> markerKeys = new HashSet<string>();
    Coroutine blinkRoutine;

    [Header("Marker Placement")]
    [SerializeField] float markerYOffset = 0.075f;
    [SerializeField] float markerRightOffset = 0.04f;

    [Header("Ring Size")]
    [SerializeField] int ringSegments = 56;
    [SerializeField] float stepRingRadius = 0.038f;
    [SerializeField] float targetRingRadius = 0.043f;
    [SerializeField] float stepRingWidth = 0.005f;
    [SerializeField] float targetRingWidth = 0.006f;

    [Header("Blink")]
    [SerializeField] float blinkSpeed = 5f;
    [SerializeField] float stepMinAlpha = 0.35f;
    [SerializeField] float stepMaxAlpha = 0.78f;
    [SerializeField] float targetMinAlpha = 0.5f;
    [SerializeField] float targetMaxAlpha = 0.95f;
    [SerializeField] float stepPulseScale = 1.05f;
    [SerializeField] float targetPulseScale = 1.08f;

    [Header("Colors")]
    [SerializeField] Color targetColor = new Color(1f, 0.88f, 0.08f, 1f);
    [SerializeField] Color blueStepColor = new Color(0.1f, 0.65f, 1f, 1f);
    [SerializeField] Color redStepColor = new Color(1f, 0.12f, 0.12f, 1f);
    [SerializeField] Color greenStepColor = new Color(0.12f, 0.85f, 0.22f, 1f);
    [SerializeField] Color yellowStepColor = new Color(1f, 0.9f, 0.12f, 1f);

    public static void TryClear()
    {
        if (instance == null)
            return;

        instance.Clear();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void Clear()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        foreach (GameObject markerObject in markerObjects)
        {
            if (markerObject != null)
                Destroy(markerObject);
        }

        markerObjects.Clear();
        markers.Clear();
        markerKeys.Clear();
    }

    public void ShowMovePreview(
        PieceController piece,
        int step,
        bool canSpawn,
        bool canClimbHome)
    {
        if (piece == null || piece.board == null)
            return;

        List<Vector3> positions = BuildPreviewPositions(piece, step, canSpawn, canClimbHome);

        for (int i = 0; i < positions.Count; i++)
        {
            bool isTarget = i == positions.Count - 1;
            CreateMarker(positions[i], isTarget, piece.playerIndex);
        }

        if (markers.Count > 0 && blinkRoutine == null)
            blinkRoutine = StartCoroutine(BlinkMarkers());
    }

    List<Vector3> BuildPreviewPositions(
        PieceController piece,
        int step,
        bool canSpawn,
        bool canClimbHome)
    {
        List<Vector3> positions = new List<Vector3>();
        BoardManager board = piece.board;

        if (piece.isInStable)
        {
            if (canSpawn &&
                piece.startIndex >= 0 &&
                piece.startIndex < board.pathPoints.Length &&
                board.pathPoints[piece.startIndex] != null)
            {
                positions.Add(board.pathPoints[piece.startIndex].position);
            }

            return positions;
        }

        Transform[] homePath = board.GetHomePath(piece.playerIndex);

        if (piece.isInHomePath)
        {
            int targetHomeIndex = piece.homeIndex + 1;

            if (canClimbHome &&
                homePath != null &&
                targetHomeIndex >= 0 &&
                targetHomeIndex < homePath.Length &&
                homePath[targetHomeIndex] != null)
            {
                positions.Add(homePath[targetHomeIndex].position);
            }

            return positions;
        }

        if (step <= 0 || board.pathPoints == null || board.pathPoints.Length == 0)
            return positions;

        for (int i = 1; i <= step; i++)
        {
            bool willEnterHome = piece.stepsMoved + i > piece.maxLoopSteps;

            if (!willEnterHome)
            {
                int pathIndex = (piece.currentIndex + i) % board.pathPoints.Length;

                if (pathIndex >= 0 && board.pathPoints[pathIndex] != null)
                    positions.Add(board.pathPoints[pathIndex].position);
            }
            else
            {
                int stepsToHome = piece.maxLoopSteps - piece.stepsMoved;
                int homeIndex = i - stepsToHome - 1;

                if (homePath != null &&
                    homeIndex >= 0 &&
                    homeIndex < homePath.Length &&
                    homePath[homeIndex] != null)
                {
                    positions.Add(homePath[homeIndex].position);
                }
            }
        }

        return positions;
    }

    void CreateMarker(Vector3 boardPosition, bool isTarget, int playerIndex)
    {
        string markerKey = GetMarkerKey(boardPosition, isTarget);

        if (markerKeys.Contains(markerKey))
            return;

        markerKeys.Add(markerKey);

        GameObject markerObject = new GameObject(isTarget ? "MoveTargetRing" : "MoveStepRing");
        markerObject.name = isTarget ? "MoveTargetMarker" : "MoveStepMarker";
        SetIgnoreRaycastLayer(markerObject);
        markerObject.transform.SetParent(transform, true);
        markerObject.transform.position =
            boardPosition +
            Vector3.up * markerYOffset +
            Vector3.right * markerRightOffset;
        markerObject.transform.rotation = Quaternion.identity;

        LineRenderer ringRenderer = markerObject.AddComponent<LineRenderer>();
        ConfigureRingRenderer(ringRenderer, isTarget);

        Material markerMaterial = CreateMarkerMaterial(isTarget, playerIndex, 0.82f);

        if (markerMaterial == null)
        {
            Destroy(markerObject);
            return;
        }

        ringRenderer.sharedMaterial = markerMaterial;
        ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;

        markerObjects.Add(markerObject);
        markers.Add(new Marker
        {
            transform = markerObject.transform,
            renderer = ringRenderer,
            material = markerMaterial,
            baseScale = markerObject.transform.localScale,
            isTarget = isTarget,
            playerIndex = playerIndex
        });
    }

    void ConfigureRingRenderer(LineRenderer ringRenderer, bool isTarget)
    {
        if (ringRenderer == null)
            return;

        float radius = Mathf.Max(0.005f, isTarget ? targetRingRadius : stepRingRadius);

        ringRenderer.useWorldSpace = false;
        ringRenderer.loop = true;
        ringRenderer.positionCount = Mathf.Max(12, ringSegments);
        ringRenderer.widthMultiplier = Mathf.Max(0.001f, isTarget ? targetRingWidth : stepRingWidth);
        ringRenderer.numCornerVertices = 4;
        ringRenderer.numCapVertices = 4;
        ringRenderer.textureMode = LineTextureMode.Stretch;
        ringRenderer.alignment = LineAlignment.View;

        for (int i = 0; i < ringRenderer.positionCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / ringRenderer.positionCount;
            Vector3 point = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            ringRenderer.SetPosition(i, point);
        }
    }

    void SetIgnoreRaycastLayer(GameObject target)
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer >= 0 && target != null)
            target.layer = ignoreRaycastLayer;
    }

    string GetMarkerKey(Vector3 position, bool isTarget)
    {
        int x = Mathf.RoundToInt(position.x * 100f);
        int y = Mathf.RoundToInt(position.y * 100f);
        int z = Mathf.RoundToInt(position.z * 100f);

        return (isTarget ? "T" : "S") + x + "_" + y + "_" + z;
    }

    Material CreateMarkerMaterial(bool isTarget, int playerIndex, float alpha)
    {
        Shader shader = FindMarkerShader();

        if (shader == null)
        {
            Debug.LogWarning("Could not create move marker material.");
            return null;
        }

        Material material = new Material(shader);
        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, GetMarkerColor(isTarget, playerIndex, alpha));
        return material;
    }

    Shader FindMarkerShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) return shader;

        shader = Shader.Find("Unlit/Color");
        if (shader != null) return shader;

        shader = Shader.Find("Sprites/Default");
        if (shader != null) return shader;

        shader = Shader.Find("UI/Default");
        if (shader != null) return shader;

        shader = Shader.Find("Hidden/Internal-Colored");
        if (shader != null) return shader;

        return Shader.Find("Standard");
    }

    Color GetMarkerColor(bool isTarget, int playerIndex, float alpha)
    {
        Color color;

        if (isTarget)
        {
            color = targetColor;
            color.a = alpha;
            return color;
        }

        if (playerIndex == 0) color = blueStepColor;
        else if (playerIndex == 1) color = redStepColor;
        else if (playerIndex == 2) color = greenStepColor;
        else if (playerIndex == 3) color = yellowStepColor;
        else color = Color.white;

        color.a = alpha;
        return color;
    }

    void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    IEnumerator BlinkMarkers()
    {
        float blinkTime = 0f;

        while (markers.Count > 0)
        {
            blinkTime += Time.deltaTime * blinkSpeed;
            float pulse = (Mathf.Sin(blinkTime) + 1f) * 0.5f;

            foreach (Marker marker in markers)
            {
                if (marker == null || marker.transform == null || marker.renderer == null)
                    continue;

                float alpha = marker.isTarget
                    ? Mathf.Lerp(targetMinAlpha, targetMaxAlpha, pulse)
                    : Mathf.Lerp(stepMinAlpha, stepMaxAlpha, pulse);
                float scale = marker.isTarget
                    ? Mathf.Lerp(1f, targetPulseScale, pulse)
                    : Mathf.Lerp(1f, stepPulseScale, pulse);

                marker.transform.localScale = marker.baseScale * scale;

                SetMaterialColor(
                    marker.material,
                    GetMarkerColor(marker.isTarget, marker.playerIndex, alpha)
                );
            }

            yield return null;
        }
    }
}
