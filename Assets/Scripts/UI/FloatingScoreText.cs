using TMPro;
using UnityEngine;

public class FloatingScoreText : MonoBehaviour
{
    private TextMeshPro label;
    private Color baseColor;
    private Vector3 startPos;
    private float baseScale = 0.11f;
    private float elapsed;
    private const float Duration = 1.35f;
    private const float FloatHeight = 0.28f;

    private static TMP_FontAsset cachedFont;

    public static void Spawn(Vector3 worldPosition, string text, Color color, float scaleMultiplier = 1f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        GameObject go = new GameObject("FloatingScoreText");
        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreLayer >= 0)
            go.layer = ignoreLayer;

        FloatingScoreText instance = go.AddComponent<FloatingScoreText>();
        instance.Initialize(worldPosition + new Vector3(0f, 0.34f, 0f), text, color, scaleMultiplier);
    }

    void Initialize(Vector3 pos, string text, Color color, float scaleMultiplier)
    {
        startPos = pos;
        transform.position = pos;
        baseColor = color;
        baseScale = 0.11f * Mathf.Clamp(scaleMultiplier, 0.6f, 2f);

        label = gameObject.AddComponent<TextMeshPro>();
        TMP_FontAsset font = GetSharedFont();
        if (font != null)
            label.font = font;

        label.text = text;
        label.fontSize = 1.5f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = color;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(12, 15, 22, 245);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 200;
        }

        FaceCameraImmediate();
        transform.localScale = Vector3.one * (baseScale * 0.4f);
    }

    static TMP_FontAsset GetSharedFont()
    {
        if (cachedFont != null)
            return cachedFont;

        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null && ui.turnText != null && ui.turnText.font != null)
        {
            cachedFont = ui.turnText.font;
            return cachedFont;
        }

        return TMP_Settings.defaultFontAsset;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Duration);

        // Smooth upward float with ease-out
        float verticalProgress = 1f - Mathf.Pow(1f - t, 2.2f);
        transform.position = startPos + new Vector3(0f, verticalProgress * FloatHeight, 0f);

        // Punch scale in first 20%, settle to 1.0
        float scaleFactor;
        if (t < 0.18f)
        {
            scaleFactor = Mathf.Lerp(0.45f, 1.22f, t / 0.18f);
        }
        else if (t < 0.32f)
        {
            scaleFactor = Mathf.Lerp(1.22f, 1.0f, (t - 0.18f) / 0.14f);
        }
        else
        {
            scaleFactor = 1.0f;
        }
        transform.localScale = Vector3.one * (baseScale * scaleFactor);

        // Fade out during last 35%
        if (label != null)
        {
            float alpha = t < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.65f) / 0.35f);
            Color c = baseColor;
            c.a = alpha;
            label.color = c;
        }

        FaceCameraImmediate();

        if (elapsed >= Duration)
            Destroy(gameObject);
    }

    void FaceCameraImmediate()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 dir = transform.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
    }
}
