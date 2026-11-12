using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shared design tokens, procedural 9-slice beveled sprites, brass corner filigree,
/// and anti-aliased icons for the Royal Lacquer Arcade UI theme across all scenes.
/// </summary>
public static class RoyalUiThemeKit
{
    public enum IconKind
    {
        Crown = 0,
        Lotus = 1,
        DieQuestion = 2,
        FlameSword = 3,
        HelpMark = 4,
        MusicNote = 5,
        Crosshair = 6,
        Horse = 7,
        Dice = 8,
        Star = 9,
        Check = 10,
        DoorExit = 11,
        ShopStore = 12,
        SkinTunic = 13
    }

    // -------------------------------------------------------------------------
    // Semantic Color Tokens (Hoàng Gia Sơn Mài Lạc Việt × Arcade Nổi Khối 3D)
    // -------------------------------------------------------------------------
    public static readonly Color LacquerEbony = new Color(0.105f, 0.068f, 0.048f, 0.96f);
    public static readonly Color LacquerWarm = new Color(0.165f, 0.105f, 0.072f, 0.96f);
    public static readonly Color LacquerActive = new Color(0.245f, 0.158f, 0.082f, 0.98f);
    public static readonly Color LacquerInset = new Color(0.068f, 0.044f, 0.032f, 0.95f);
    public static readonly Color CrimsonVelvet = new Color(0.265f, 0.062f, 0.072f, 0.97f);
    public static readonly Color NavyRibbon = new Color(0.085f, 0.145f, 0.285f, 0.98f);

    public static readonly Color BrassAntique = new Color(0.86f, 0.65f, 0.24f, 1f);
    public static readonly Color BrassBright = new Color(1.00f, 0.87f, 0.42f, 1f);
    public static readonly Color BronzeMuted = new Color(0.46f, 0.34f, 0.17f, 0.85f);

    public static readonly Color ArcadeGoldCta = new Color(0.95f, 0.69f, 0.17f, 1f);
    public static readonly Color ArcadeCrimson = new Color(0.80f, 0.17f, 0.19f, 1f);
    public static readonly Color ArcadeJade = new Color(0.16f, 0.66f, 0.35f, 1f);
    public static readonly Color ArcadeAzure = new Color(0.16f, 0.52f, 0.88f, 1f);
    public static readonly Color ArcadeSlate = new Color(0.27f, 0.18f, 0.12f, 0.98f);
    public static readonly Color ArcadeDisabled = new Color(0.19f, 0.14f, 0.10f, 0.88f);

    public static readonly Color ParchmentFill = new Color(0.99f, 0.94f, 0.81f, 1f);
    public static readonly Color ParchmentInk = new Color(0.20f, 0.11f, 0.05f, 1f);
    public static readonly Color IvoryText = new Color(0.98f, 0.95f, 0.89f, 1f);
    public static readonly Color WarmMutedText = new Color(0.82f, 0.75f, 0.63f, 1f);
    public static readonly Color DarkGoldInk = new Color(0.15f, 0.08f, 0.02f, 1f);

    private static readonly Color[] FactionColors =
    {
        new Color(0.21f, 0.57f, 0.98f, 1f), // 0: Xanh Dương
        new Color(0.95f, 0.25f, 0.27f, 1f), // 1: Đỏ
        new Color(0.20f, 0.80f, 0.43f, 1f), // 2: Xanh Lá
        new Color(0.96f, 0.75f, 0.16f, 1f)  // 3: Vàng
    };

    private static Sprite lacquerPanelSprite;
    private static Sprite recessedSlotSprite;
    private static Sprite arcadeButtonSprite;
    private static Sprite ribbonHeaderSprite;
    private static Sprite parchmentScrollSprite;
    private static Sprite pillCapsuleSprite;
    private static Sprite circleMedallionSprite;
    private static Sprite cornerFiligreeSprite;

    private static readonly Dictionary<IconKind, Sprite> CachedIcons = new Dictionary<IconKind, Sprite>();

    public static Color GetFactionColor(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= FactionColors.Length)
            return IvoryText;
        return FactionColors[playerIndex];
    }

    public static Sprite GetLacquerPanelSprite()
    {
        if (lacquerPanelSprite == null)
            lacquerPanelSprite = BuildLacquerPanelSprite();
        return lacquerPanelSprite;
    }

    public static Sprite GetRecessedSlotSprite()
    {
        if (recessedSlotSprite == null)
            recessedSlotSprite = BuildRecessedSlotSprite();
        return recessedSlotSprite;
    }

    public static Sprite GetArcadeButtonSprite()
    {
        if (arcadeButtonSprite == null)
            arcadeButtonSprite = BuildArcadeButtonSprite();
        return arcadeButtonSprite;
    }

    public static Sprite GetRibbonHeaderSprite()
    {
        if (ribbonHeaderSprite == null)
            ribbonHeaderSprite = BuildRibbonHeaderSprite();
        return ribbonHeaderSprite;
    }

    public static Sprite GetParchmentScrollSprite()
    {
        if (parchmentScrollSprite == null)
            parchmentScrollSprite = BuildParchmentScrollSprite();
        return parchmentScrollSprite;
    }

    public static Sprite GetPillCapsuleSprite()
    {
        if (pillCapsuleSprite == null)
            pillCapsuleSprite = BuildPillCapsuleSprite();
        return pillCapsuleSprite;
    }

    public static Sprite GetCircleMedallionSprite()
    {
        if (circleMedallionSprite == null)
            circleMedallionSprite = BuildCircleMedallionSprite();
        return circleMedallionSprite;
    }

    public static Sprite GetCornerFiligreeSprite()
    {
        if (cornerFiligreeSprite == null)
            cornerFiligreeSprite = BuildCornerFiligreeSprite();
        return cornerFiligreeSprite;
    }

    public static Sprite GetIcon(IconKind kind)
    {
        if (CachedIcons.TryGetValue(kind, out Sprite existing) && existing != null)
            return existing;

        const int size = 96;
        Texture2D tex = CreateBlankTexture(size, size);
        DrawIcon(tex, size, kind);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        CachedIcons[kind] = sprite;
        return sprite;
    }

    /// <summary>
    /// Applies a 9-sliced Lacquer panel sprite with beveled lighting and optional antique brass corner filigree.
    /// </summary>
    public static void ApplyLacquerPanel(GameObject panel, Color fillTint, Color outlineColor, float outlineWidth = 1.6f, bool addCornerFiligree = true)
    {
        if (panel == null)
            return;

        Image img = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
        img.sprite = GetLacquerPanelSprite();
        img.type = Image.Type.Sliced;
        img.color = fillTint;

        Outline outline = panel.GetComponent<Outline>() ?? panel.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);

        Shadow shadow = panel.GetComponent<Shadow>() ?? panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(0f, -5f);

        if (addCornerFiligree && panel.transform is RectTransform rt)
            EnsureBrassCornerFiligree(rt, BrassAntique, 22f, 4f);
    }

    /// <summary>
    /// Applies a sunken lacquer slot sprite for inputs, status containers, and card recesses.
    /// </summary>
    public static void ApplyRecessedSlot(GameObject target, Color fillTint, Color borderColor, float borderWidth = 1.2f)
    {
        if (target == null)
            return;

        Image img = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        img.sprite = GetRecessedSlotSprite();
        img.type = Image.Type.Sliced;
        img.color = fillTint;

        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderWidth, -borderWidth);
    }

    /// <summary>
    /// Applies a tactile 3D arcade button sprite with specular highlight, chunky bottom lip, outline, and shadow.
    /// </summary>
    public static void ApplyArcadeButton(GameObject target, Color tint, Color borderColor, float borderWidth = 1.5f)
    {
        if (target == null)
            return;

        Image img = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        img.sprite = GetArcadeButtonSprite();
        img.type = Image.Type.Sliced;
        img.color = tint;

        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderWidth, -borderWidth);

        Shadow shadow = target.GetComponent<Shadow>() ?? target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
        shadow.effectDistance = new Vector2(0f, -3.5f);
    }

    /// <summary>
    /// Adds 4 antique brass corner filigree accents onto a panel RectTransform without blocking raycasts.
    /// </summary>
    public static void EnsureBrassCornerFiligree(RectTransform parent, Color tint, float size = 20f, float inset = 7f)
    {
        if (parent == null)
            return;

        Transform existing = parent.Find("BrassCornerFiligreeGroup");
        if (existing != null)
            Object.Destroy(existing.gameObject);

        GameObject group = new GameObject("BrassCornerFiligreeGroup", typeof(RectTransform));
        group.layer = parent.gameObject.layer;
        group.transform.SetParent(parent, false);

        RectTransform gRt = group.GetComponent<RectTransform>();
        gRt.anchorMin = Vector2.zero;
        gRt.anchorMax = Vector2.one;
        gRt.offsetMin = Vector2.zero;
        gRt.offsetMax = Vector2.zero;

        Sprite filigree = GetCornerFiligreeSprite();
        float halfShift = inset + size * 0.5f;
        CreateCornerAccent(gRt, "TopLeft", filigree, tint, new Vector2(0f, 1f), new Vector2(halfShift, -halfShift), new Vector2(size, size), Vector3.one);
        CreateCornerAccent(gRt, "TopRight", filigree, tint, new Vector2(1f, 1f), new Vector2(-halfShift, -halfShift), new Vector2(size, size), new Vector3(-1f, 1f, 1f));
        CreateCornerAccent(gRt, "BottomLeft", filigree, tint, new Vector2(0f, 0f), new Vector2(halfShift, halfShift), new Vector2(size, size), new Vector3(1f, -1f, 1f));
        CreateCornerAccent(gRt, "BottomRight", filigree, tint, new Vector2(1f, 0f), new Vector2(-halfShift, halfShift), new Vector2(size, size), new Vector3(-1f, -1f, 1f));
    }

    private static void CreateCornerAccent(
        RectTransform parent,
        string name,
        Sprite sprite,
        Color tint,
        Vector2 anchorEdge,
        Vector2 anchoredPos,
        Vector2 size,
        Vector3 scale)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorEdge;
        rt.anchorMax = anchorEdge;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = scale;

        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.color = tint;
        img.raycastTarget = false;
    }

    // -------------------------------------------------------------------------
    // Procedural 9-Slice Sprites
    // -------------------------------------------------------------------------
    private static Texture2D CreateBlankTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] clear = new Color32[width * height];
        tex.SetPixels32(clear);
        return tex;
    }

    private static Sprite BuildLacquerPanelSprite()
    {
        const int S = 96;
        const float radius = 16f;
        const int border = 26;
        Texture2D tex = CreateBlankTexture(S, S);

        for (int y = 0; y < S; y++)
        {
            float ny = y / (float)(S - 1);
            for (int x = 0; x < S; x++)
            {
                float nx = x / (float)(S - 1);
                float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, S, S, radius);
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (alpha <= 0.001f)
                    continue;

                // Subtle wood-lacquer radial/vertical shading (white-base tintable texture)
                float topGlow = Mathf.SmoothStep(0.35f, 1.0f, ny) * 0.16f;
                float edgeVignette = Mathf.Clamp01(Mathf.Abs(dist) / 14f);
                float shade = Mathf.Lerp(0.78f, 1.00f, edgeVignette) + topGlow;

                // Inner specular top rim & recessed bottom bevel
                if (dist > -2.6f && dist <= -0.6f)
                    shade += ny > 0.5f ? 0.24f : -0.14f;

                // Fine warm inner pinstripe at dist in [-5.5..-4.2]
                if (dist > -5.5f && dist < -4.1f)
                    shade = Mathf.Lerp(shade, 1.24f, 0.55f);

                // Subtle lacquer grain
                float grain = Mathf.Sin(nx * 28f + ny * 6f) * 0.015f;
                float v = Mathf.Clamp(shade + grain, 0.55f, 1.15f);
                tex.SetPixel(x, y, new Color(v, v * 0.99f, v * 0.97f, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildRecessedSlotSprite()
    {
        const int S = 64;
        const float radius = 11f;
        const int border = 16;
        Texture2D tex = CreateBlankTexture(S, S);

        for (int y = 0; y < S; y++)
        {
            float ny = y / (float)(S - 1);
            for (int x = 0; x < S; x++)
            {
                float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, S, S, radius);
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (alpha <= 0.001f)
                    continue;

                // Sunken interior: darker near top inner lip, slightly lighter at bottom catchlight
                float topShadow = Mathf.Clamp01((S - y - 4f) / 14f);
                float shade = Mathf.Lerp(0.72f, 0.98f, topShadow);
                if (dist > -2.2f && ny < 0.35f)
                    shade += 0.14f;

                tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildArcadeButtonSprite()
    {
        const int W = 96;
        const int H = 64;
        const float radius = 14f;
        const int border = 20;
        Texture2D tex = CreateBlankTexture(W, H);

        for (int y = 0; y < H; y++)
        {
            float ny = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, W, H, radius);
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (alpha <= 0.001f)
                    continue;

                // Chunky 3D lower bevel lip (bottom 16% darker) + glossy upper cap
                float lipMask = 1f - Mathf.SmoothStep(0.11f, 0.22f, ny);
                float bodyGrad = Mathf.Lerp(0.84f, 1.07f, Mathf.SmoothStep(0.16f, 0.95f, ny));

                // Specular gloss band near top half
                float glossBand = Mathf.Exp(-Mathf.Pow((ny - 0.78f) / 0.14f, 2f)) * 0.22f;

                // Sharp crisp rim highlight at top edge
                float topRim = (dist > -2.6f && ny > 0.50f) ? 0.25f : 0f;

                float shade = Mathf.Lerp(bodyGrad + glossBand + topRim, 0.50f, lipMask);
                shade = Mathf.Clamp(shade, 0.42f, 1.28f);

                tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildRibbonHeaderSprite()
    {
        const int W = 128;
        const int H = 56;
        const float radius = 18f;
        const int borderX = 28;
        const int borderY = 18;
        Texture2D tex = CreateBlankTexture(W, H);

        for (int y = 0; y < H; y++)
        {
            float ny = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, W, H, radius);
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (alpha <= 0.001f)
                    continue;

                float centerSheen = Mathf.Sin(ny * Mathf.PI) * 0.16f;
                float rim = (dist > -3.0f && dist <= -0.8f) ? 0.22f : 0f;
                float v = Mathf.Clamp(0.84f + centerSheen + rim, 0.65f, 1.2f);
                tex.SetPixel(x, y, new Color(v, v, v, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(borderX, borderY, borderX, borderY));
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildParchmentScrollSprite()
    {
        const int W = 128;
        const int H = 56;
        const float radius = 10f;
        const int borderX = 22;
        const int borderY = 14;
        Texture2D tex = CreateBlankTexture(W, H);

        for (int y = 0; y < H; y++)
        {
            float ny = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                float nx = x / (float)(W - 1);
                float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, W, H, radius);
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (alpha <= 0.001f)
                    continue;

                // Subtle rolled scroll end shading
                float sideCurl = Mathf.SmoothStep(0.0f, 0.12f, nx) * Mathf.SmoothStep(1.0f, 0.88f, nx);
                float edgeDark = Mathf.Lerp(0.84f, 1.0f, sideCurl);
                float vertCurl = Mathf.Lerp(0.92f, 1.03f, Mathf.Sin(ny * Mathf.PI));
                float v = edgeDark * vertCurl;
                if (dist > -2.5f)
                    v *= 0.86f;

                tex.SetPixel(x, y, new Color(v, v * 0.98f, v * 0.94f, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(borderX, borderY, borderX, borderY));
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildPillCapsuleSprite()
    {
        const int W = 64;
        const int H = 48;
        const float radius = 21f;
        const int border = 22;
        Texture2D tex = CreateBlankTexture(W, H);

        for (int y = 0; y < H; y++)
        {
            float ny = y / (float)(H - 1);
            for (int x = 0; x < W; x++)
            {
                float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, W, H, radius);
                float alpha = Mathf.Clamp01(0.5f - dist);
                if (alpha <= 0.001f)
                    continue;

                float bevel = Mathf.Lerp(0.82f, 1.08f, ny);
                float v = Mathf.Clamp(bevel, 0.72f, 1.15f);
                tex.SetPixel(x, y, new Color(v, v, v, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, 16, border, 16));
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildCircleMedallionSprite()
    {
        const int S = 96;
        Texture2D tex = CreateBlankTexture(S, S);
        float c = (S - 1) * 0.5f;
        float r = S * 0.45f;

        for (int y = 0; y < S; y++)
        {
            float ny = y / (float)(S - 1);
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) - r;
                float alpha = Mathf.Clamp01(0.5f - d);
                if (alpha <= 0.001f)
                    continue;

                float bevel = Mathf.Lerp(0.72f, 1.12f, ny);
                float ring = (d > -4.5f && d < -1.5f) ? 0.16f : 0f;
                float v = Mathf.Clamp(bevel + ring, 0.65f, 1.2f);
                tex.SetPixel(x, y, new Color(v, v, v, alpha));
            }
        }

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 96f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    private static Sprite BuildCornerFiligreeSprite()
    {
        const int S = 44;
        Texture2D tex = CreateBlankTexture(S, S);

        // Top-Left curved-elbow antique brass inlay hugging rounded panel bevel
        float arcCx = 13f;
        float arcCy = S - 14f;
        float arcR = 9f;
        for (int i = 0; i <= 18; i++)
        {
            float a = Mathf.Lerp(180f, 90f, i / 18f) * Mathf.Deg2Rad;
            DrawDisc(tex, arcCx + Mathf.Cos(a) * arcR, arcCy + Mathf.Sin(a) * arcR, 1.85f, Color.white);
        }
        DrawSegment(tex, arcCx, S - 5, S - 6, S - 5, 1.85f, Color.white);
        DrawSegment(tex, 4, arcCy, 4, 6, 1.85f, Color.white);
        DrawSegment(tex, 14, S - 10, S * 0.65f, S - 10, 1.0f, new Color(1f, 1f, 1f, 0.85f));
        DrawSegment(tex, 9, S - 15, 9, S * 0.35f, 1.0f, new Color(1f, 1f, 1f, 0.85f));
        DrawDisc(tex, 9.5f, S - 10.5f, 2.6f, Color.white);
        DrawDisc(tex, S - 6f, S - 5f, 1.7f, Color.white);
        DrawDisc(tex, 4f, 6f, 1.7f, Color.white);

        tex.Apply();
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 44f);
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    // -------------------------------------------------------------------------
    // Shared Party Clash Rules Popup (Reused in Lobby & GameScene)
    // -------------------------------------------------------------------------
    public static void TogglePartyClashRulesPopup(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        Transform existing = canvasRoot.Find("RoyalRulesOverlay");
        if (existing != null)
        {
            existing.gameObject.SetActive(!existing.gameObject.activeSelf);
            existing.SetAsLastSibling();
            return;
        }

        GameObject overlay = new GameObject("RoyalRulesOverlay", typeof(RectTransform), typeof(Image), typeof(RoyalModalEscCloser));
        overlay.layer = canvasRoot.gameObject.layer;
        overlay.transform.SetParent(canvasRoot, false);
        overlay.transform.SetAsLastSibling();

        RectTransform ovRt = overlay.GetComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = Vector2.zero;
        ovRt.offsetMax = Vector2.zero;

        Image dimBg = overlay.GetComponent<Image>();
        dimBg.color = new Color(0.03f, 0.02f, 0.015f, 0.78f);

        // Clicking backdrop dismisses modal cleanly
        Button dimBtn = overlay.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => overlay.SetActive(false));

        GameObject card = new GameObject("RulesCard", typeof(RectTransform));
        card.layer = canvasRoot.gameObject.layer;
        card.transform.SetParent(overlay.transform, false);

        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(680f, 430f);

        ApplyLacquerPanel(card, LacquerEbony, BrassAntique, 2.2f, true);

        // Header Ribbon
        GameObject headerObj = new GameObject("RulesHeaderPlaque", typeof(RectTransform), typeof(Image), typeof(Outline));
        headerObj.layer = canvasRoot.gameObject.layer;
        headerObj.transform.SetParent(cardRt, false);
        RectTransform hdrRt = headerObj.GetComponent<RectTransform>();
        hdrRt.anchorMin = new Vector2(0.5f, 1f);
        hdrRt.anchorMax = new Vector2(0.5f, 1f);
        hdrRt.pivot = new Vector2(0.5f, 1f);
        hdrRt.anchoredPosition = new Vector2(-10f, -18f);
        hdrRt.sizeDelta = new Vector2(496f, 44f);
        Image hdrImg = headerObj.GetComponent<Image>();
        hdrImg.sprite = GetRibbonHeaderSprite();
        hdrImg.type = Image.Type.Sliced;
        hdrImg.color = CrimsonVelvet;
        hdrImg.raycastTarget = false;
        Outline hdrOut = headerObj.GetComponent<Outline>();
        hdrOut.effectColor = BrassBright;
        hdrOut.effectDistance = new Vector2(1.4f, -1.4f);

        TMP_Text rTitle = CreateModalTmp(
            hdrRt,
            "RulesTitle",
            "LUẬT PARTY CLASH — CỜ CÁ NGỰA 3D",
            16.5f,
            BrassBright,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            new Vector2(12f, 0f),
            new Vector2(-12f, 0f)
        );
        rTitle.textWrappingMode = TextWrappingModes.NoWrap;
        rTitle.enableAutoSizing = true;
        rTitle.fontSizeMin = 12f;
        rTitle.fontSizeMax = 16.5f;

        // Close Button (top-right)
        GameObject closeObj = new GameObject("CloseRulesBtn", typeof(RectTransform), typeof(Button));
        closeObj.layer = canvasRoot.gameObject.layer;
        closeObj.transform.SetParent(cardRt, false);
        RectTransform cRt = closeObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(1f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(1f, 1f);
        cRt.anchoredPosition = new Vector2(-14f, -14f);
        cRt.sizeDelta = new Vector2(38f, 38f);
        ApplyArcadeButton(closeObj, ArcadeCrimson, BrassBright, 1.4f);
        Button closeBtn = closeObj.GetComponent<Button>();
        closeBtn.onClick.AddListener(() => overlay.SetActive(false));
        if (closeObj.GetComponent<RoyalButtonHoverEffect>() == null)
            closeObj.AddComponent<RoyalButtonHoverEffect>();

        CreateModalTmp(
            cRt,
            "X",
            "X",
            17f,
            IvoryText,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        // Inner recessed body
        GameObject bodyObj = new GameObject("RulesBodySlot", typeof(RectTransform));
        bodyObj.layer = canvasRoot.gameObject.layer;
        bodyObj.transform.SetParent(cardRt, false);
        RectTransform bRt = bodyObj.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.offsetMin = new Vector2(24f, 64f);
        bRt.offsetMax = new Vector2(-24f, -76f);
        ApplyRecessedSlot(bodyObj, LacquerInset, BronzeMuted, 1.2f);

        string rulesContent =
            "<b><color=#FFD95A>1. MỤC TIÊU & TÍNH ĐIỂM TRONG 10 PHÚT</color></b>\n" +
            "• <b>Qua bước 28 (Tinh Nhuệ):</b> +1 điểm & quân mở khóa nâng cấp chiến đấu.\n" +
            "• <b>Đá hạ quân địch:</b> +2 điểm thưởng và mở ngay ô <b>COMBO +3 bước</b> miễn phí.\n" +
            "• <b>Đưa ngựa về chuồng Đích:</b> +3 điểm thưởng (lên tới +5 điểm ở vòng Nước Rút).\n\n" +
            "<b><color=#FFD95A>2. CƠ CHẾ XÚC XẮC & Ô ĐẶC BIỆT</color></b>\n" +
            "• Gieo <b>2 xúc xắc</b> mỗi lượt; chọn <b>VIÊN 1</b> hoặc <b>VIÊN 2</b> để chia nhịp đi thông minh.\n" +
            "• <b>Ra quân khỏi chuồng:</b> cần số <b>1</b> hoặc <b>6</b>.\n" +
            "• <b>Ô Tăng Tốc (Vàng/Xanh trên đường):</b> đạp trúng được đẩy thêm +3 bước.\n\n" +
            "<b><color=#FFD95A>3. BIẾN CỐ TOÀN BÀN</color></b>\n" +
            "• <b>LỆNH TRUY NÃ:</b> Người dẫn đầu bị đánh dấu; ai đá hạ mục tiêu nhận +2đ (mục tiêu sống sót hết vòng cũng nhận +2đ).\n" +
            "• <b>NƯỚC RÚT BỐC CHÁY:</b> Toàn bàn bốc lửa, điểm về đích tăng mạnh & ô checkpoint nhân đôi điểm!";

        TMP_Text bodyTxt = CreateModalTmp(
            bRt,
            "RulesBodyText",
            rulesContent,
            13.8f,
            IvoryText,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            Vector2.zero,
            Vector2.one,
            new Vector2(18f, 16f),
            new Vector2(-18f, -16f)
        );
        bodyTxt.textWrappingMode = TextWrappingModes.Normal;
        bodyTxt.richText = true;

        // Bottom Confirm Button
        GameObject okObj = new GameObject("UnderstandBtn", typeof(RectTransform), typeof(Button));
        okObj.layer = canvasRoot.gameObject.layer;
        okObj.transform.SetParent(cardRt, false);
        RectTransform okRt = okObj.GetComponent<RectTransform>();
        okRt.anchorMin = new Vector2(0.5f, 0f);
        okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(0.5f, 0f);
        okRt.anchoredPosition = new Vector2(0f, 14f);
        okRt.sizeDelta = new Vector2(210f, 40f);
        ApplyArcadeButton(okObj, ArcadeGoldCta, BrassBright, 1.5f);
        Button okBtn = okObj.GetComponent<Button>();
        okBtn.onClick.AddListener(() => overlay.SetActive(false));
        if (okObj.GetComponent<RoyalButtonHoverEffect>() == null)
            okObj.AddComponent<RoyalButtonHoverEffect>();

        CreateModalTmp(
            okRt,
            "Label",
            "ĐÃ HIỂU (ESC)",
            15f,
            DarkGoldInk,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
    }

    private static TMP_Text CreateModalTmp(
        RectTransform parent,
        string name,
        string text,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions align,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    // -------------------------------------------------------------------------
    // Shared Exit Confirmation Modal (Popup xác nhận rời phòng / chịu thua)
    // -------------------------------------------------------------------------
    public static void ShowExitConfirmationPopup(Transform canvasRoot, System.Action onConfirmExit)
    {
        if (canvasRoot == null)
            return;

        Transform existing = canvasRoot.Find("RoyalExitConfirmOverlay");
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();
            return;
        }

        GameObject overlay = new GameObject("RoyalExitConfirmOverlay", typeof(RectTransform), typeof(Image), typeof(RoyalModalEscCloser));
        overlay.layer = canvasRoot.gameObject.layer;
        overlay.transform.SetParent(canvasRoot, false);
        overlay.transform.SetAsLastSibling();

        RectTransform ovRt = overlay.GetComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = Vector2.zero;
        ovRt.offsetMax = Vector2.zero;

        Image dimBg = overlay.GetComponent<Image>();
        dimBg.color = new Color(0.03f, 0.02f, 0.015f, 0.82f);

        Button dimBtn = overlay.AddComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => overlay.SetActive(false));

        // Dialog Card
        GameObject card = new GameObject("ExitConfirmCard", typeof(RectTransform));
        card.layer = canvasRoot.gameObject.layer;
        card.transform.SetParent(overlay.transform, false);

        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(530f, 300f);

        ApplyLacquerPanel(card, LacquerEbony, BrassAntique, 2.2f, true);

        // Header Ribbon Plaque
        GameObject headerObj = new GameObject("ExitHeaderPlaque", typeof(RectTransform), typeof(Image), typeof(Outline));
        headerObj.layer = canvasRoot.gameObject.layer;
        headerObj.transform.SetParent(cardRt, false);
        RectTransform hdrRt = headerObj.GetComponent<RectTransform>();
        hdrRt.anchorMin = new Vector2(0.5f, 1f);
        hdrRt.anchorMax = new Vector2(0.5f, 1f);
        hdrRt.pivot = new Vector2(0.5f, 1f);
        hdrRt.anchoredPosition = new Vector2(0f, -16f);
        hdrRt.sizeDelta = new Vector2(390f, 44f);
        Image hdrImg = headerObj.GetComponent<Image>();
        hdrImg.sprite = GetRibbonHeaderSprite();
        hdrImg.type = Image.Type.Sliced;
        hdrImg.color = CrimsonVelvet;
        hdrImg.raycastTarget = false;
        Outline hdrOut = headerObj.GetComponent<Outline>();
        hdrOut.effectColor = BrassBright;
        hdrOut.effectDistance = new Vector2(1.4f, -1.4f);

        TMP_Text titleTmp = CreateModalTmp(
            hdrRt,
            "ExitTitle",
            "XÁC NHẬN RỜI TRẬN ĐẤU",
            17f,
            IvoryText,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        titleTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // Content slot
        GameObject slotObj = new GameObject("ExitNoticeSlot", typeof(RectTransform), typeof(Image));
        slotObj.layer = canvasRoot.gameObject.layer;
        slotObj.transform.SetParent(cardRt, false);
        RectTransform slotRt = slotObj.GetComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0.5f, 0.5f);
        slotRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotRt.pivot = new Vector2(0.5f, 0.5f);
        slotRt.anchoredPosition = new Vector2(0f, 8f);
        slotRt.sizeDelta = new Vector2(470f, 134f);
        Image slotImg = slotObj.GetComponent<Image>();
        slotImg.sprite = GetRecessedSlotSprite();
        slotImg.type = Image.Type.Sliced;
        slotImg.color = LacquerInset;

        // Body message
        TMP_Text msgTmp = CreateModalTmp(
            slotRt,
            "ExitMessage",
            "<b>Bạn có chắc chắn muốn rời khỏi trận đấu?</b>\n\n<color=#FF7070>•</color> Nếu rời trận giữa chừng, bạn sẽ bị xử <color=#FF6666><b>BỎ CUỘC (XỬ THUA)</b></color> và không nhận xu thưởng.\n<color=#FFD566>•</color> Nếu phòng 2 người: đối thủ còn lại sẽ <b>thắng ngay lập tức</b>.\n<color=#70D6FF>•</color> Nếu phòng từ 3 người trở lên: trận đấu vẫn <b>tiếp tục bình thường</b> cho các kỳ thủ ở lại.",
            13.5f,
            IvoryText,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            Vector2.zero,
            Vector2.one,
            new Vector2(16f, 12f),
            new Vector2(-16f, -12f)
        );
        msgTmp.textWrappingMode = TextWrappingModes.Normal;

        // Bottom action buttons:
        // Button 1: Ở LẠI (Stay / Cancel)
        GameObject stayBtnObj = new GameObject("StayButton", typeof(RectTransform), typeof(Image), typeof(Button));
        stayBtnObj.layer = canvasRoot.gameObject.layer;
        stayBtnObj.transform.SetParent(cardRt, false);
        RectTransform stayRt = stayBtnObj.GetComponent<RectTransform>();
        stayRt.anchorMin = new Vector2(0.5f, 0f);
        stayRt.anchorMax = new Vector2(0.5f, 0f);
        stayRt.pivot = new Vector2(0.5f, 0f);
        stayRt.anchoredPosition = new Vector2(-118f, 16f);
        stayRt.sizeDelta = new Vector2(204f, 44f);

        ApplyArcadeButton(stayBtnObj, ArcadeGoldCta, BrassBright, 1.8f);
        if (stayBtnObj.GetComponent<RoyalButtonHoverEffect>() == null)
            stayBtnObj.AddComponent<RoyalButtonHoverEffect>();

        CreateModalTmp(
            stayRt,
            "StayText",
            "Ở LẠI CHIẾN TIẾP",
            15f,
            DarkGoldInk,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Button stayBtn = stayBtnObj.GetComponent<Button>();
        stayBtn.onClick.AddListener(() => overlay.SetActive(false));

        // Button 2: XÁC NHẬN RỜI (Confirm surrender/exit)
        GameObject leaveBtnObj = new GameObject("ConfirmLeaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
        leaveBtnObj.layer = canvasRoot.gameObject.layer;
        leaveBtnObj.transform.SetParent(cardRt, false);
        RectTransform leaveRt = leaveBtnObj.GetComponent<RectTransform>();
        leaveRt.anchorMin = new Vector2(0.5f, 0f);
        leaveRt.anchorMax = new Vector2(0.5f, 0f);
        leaveRt.pivot = new Vector2(0.5f, 0f);
        leaveRt.anchoredPosition = new Vector2(118f, 16f);
        leaveRt.sizeDelta = new Vector2(204f, 44f);

        ApplyArcadeButton(leaveBtnObj, ArcadeCrimson, BrassAntique, 1.6f);
        if (leaveBtnObj.GetComponent<RoyalButtonHoverEffect>() == null)
            leaveBtnObj.AddComponent<RoyalButtonHoverEffect>();

        CreateModalTmp(
            leaveRt,
            "LeaveText",
            "RỜI TRẬN (CHỊU THUA)",
            14f,
            IvoryText,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        Button leaveBtn = leaveBtnObj.GetComponent<Button>();
        leaveBtn.onClick.AddListener(() =>
        {
            overlay.SetActive(false);
            onConfirmExit?.Invoke();
        });
    }

    // -------------------------------------------------------------------------
    // Crisp Anti-Aliased Icons (96x96 RGBA32)
    // -------------------------------------------------------------------------
    private static void DrawIcon(Texture2D tex, int s, IconKind kind)
    {
        switch (kind)
        {
            case IconKind.Crown:
                DrawCrown(tex, s);
                break;
            case IconKind.Lotus:
                DrawLotus(tex, s);
                break;
            case IconKind.DieQuestion:
                DrawDieQuestion(tex, s);
                break;
            case IconKind.FlameSword:
                DrawFlameSword(tex, s);
                break;
            case IconKind.HelpMark:
                DrawQuestionMarkGlyph(tex, s, Color.white);
                break;
            case IconKind.MusicNote:
                DrawMusicNote(tex, s);
                break;
            case IconKind.Crosshair:
                DrawCrosshair(tex, s);
                break;
            case IconKind.Horse:
                DrawHorse(tex, s);
                break;
            case IconKind.Dice:
                DrawDicePipFive(tex, s);
                break;
            case IconKind.Star:
                DrawStar(tex, s);
                break;
            case IconKind.Check:
                DrawSegment(tex, 20, 46, 39, 27, 6.2f, Color.white);
                DrawSegment(tex, 39, 27, 76, 68, 6.2f, Color.white);
                break;
            case IconKind.DoorExit:
                DrawDoorExit(tex, s);
                break;
            case IconKind.ShopStore:
                DrawShopStore(tex, s);
                break;
            case IconKind.SkinTunic:
                DrawSkinTunic(tex, s);
                break;
        }
    }

    private static void DrawShopStore(Texture2D tex, int s)
    {
        // Awning roof
        DrawRoundedBox(tex, 14, 58, 82, 78, 5f, Color.white);
        // Canopy scallops
        DrawDisc(tex, 25, 58, 9f, Color.white);
        DrawDisc(tex, 48, 58, 9f, Color.white);
        DrawDisc(tex, 71, 58, 9f, Color.white);
        // Shop walls & counter
        DrawRoundedBox(tex, 20, 16, 76, 52, 4f, Color.white);
        // Carved door & window cutouts
        for (int y = 17; y <= 40; y++)
            for (int x = 28; x <= 44; x++)
                tex.SetPixel(x, y, Color.clear);
        for (int y = 27; y <= 42; y++)
            for (int x = 52; x <= 68; x++)
                tex.SetPixel(x, y, Color.clear);
    }

    private static void DrawSkinTunic(Texture2D tex, int s)
    {
        // T-shirt / warrior tunic silhouette in crisp white for clean tinting
        DrawRoundedBox(tex, 28, 16, 68, 76, 6f, Color.white);
        DrawSegment(tex, 30, 72, 12, 56, 8.5f, Color.white);
        DrawSegment(tex, 66, 72, 84, 56, 8.5f, Color.white);
        CarveDisc(tex, 48, 79, 10f, Color.clear);
    }

    private static void DrawCrown(Texture2D tex, int s)
    {
        DrawRoundedBox(tex, 16, 18, 80, 32, 4f, Color.white);
        DrawSegment(tex, 19, 30, 16, 67, 5.2f, Color.white);
        DrawSegment(tex, 16, 67, 34, 43, 4.8f, Color.white);
        DrawSegment(tex, 34, 43, 48, 76, 5.5f, Color.white);
        DrawSegment(tex, 48, 76, 62, 43, 4.8f, Color.white);
        DrawSegment(tex, 62, 43, 80, 67, 5.2f, Color.white);
        DrawSegment(tex, 80, 67, 77, 30, 5.2f, Color.white);
        DrawRoundedBox(tex, 20, 29, 76, 48, 3f, Color.white);
        DrawDisc(tex, 16, 70, 5.8f, Color.white);
        DrawDisc(tex, 48, 79, 6.4f, Color.white);
        DrawDisc(tex, 80, 70, 5.8f, Color.white);
    }

    private static void DrawLotus(Texture2D tex, int s)
    {
        // Stylized golden lotus silhouette (inspired by Mẫu 2 active turn crest)
        DrawEllipsePetal(tex, 48f, 52f, 11f, 27f, 0f, Color.white);
        DrawEllipsePetal(tex, 31f, 45f, 10f, 23f, 26f, new Color(1f, 1f, 1f, 0.96f));
        DrawEllipsePetal(tex, 65f, 45f, 10f, 23f, -26f, new Color(1f, 1f, 1f, 0.96f));
        DrawEllipsePetal(tex, 20f, 33f, 8.5f, 19f, 52f, new Color(1f, 1f, 1f, 0.88f));
        DrawEllipsePetal(tex, 76f, 33f, 8.5f, 19f, -52f, new Color(1f, 1f, 1f, 0.88f));
        DrawSegment(tex, 24f, 18f, 72f, 18f, 3.2f, Color.white);
    }

    private static void DrawDieQuestion(Texture2D tex, int s)
    {
        // 3D Ivory rounded die cube with recessed bottom-right edge and dark crisp '?' mark
        Color ivoryTop = new Color(0.99f, 0.97f, 0.92f, 1f);
        Color ivoryEdge = new Color(0.78f, 0.72f, 0.62f, 1f);
        DrawRoundedBox(tex, 14, 10, 84, 80, 14f, ivoryEdge);
        DrawRoundedBox(tex, 12, 16, 80, 84, 14f, ivoryTop);

        // Dark charcoal '?'
        Color ink = new Color(0.20f, 0.13f, 0.07f, 1f);
        DrawQuestionMarkGlyph(tex, s, ink, offsetX: -2f, offsetY: 2f, scale: 0.72f);
    }

    private static void DrawFlameSword(Texture2D tex, int s)
    {
        // Flame aura behind diagonal steel-gold sword (matches Mẫu 1 COMBO +3 emblem)
        Color flameOuter = new Color(0.98f, 0.34f, 0.10f, 1f);
        Color flameInner = new Color(1.00f, 0.78f, 0.22f, 1f);
        DrawEllipsePetal(tex, 48f, 46f, 24f, 34f, 10f, flameOuter);
        DrawEllipsePetal(tex, 62f, 58f, 12f, 22f, -20f, flameOuter);
        DrawEllipsePetal(tex, 36f, 52f, 13f, 24f, 24f, flameInner);
        DrawEllipsePetal(tex, 48f, 42f, 16f, 23f, 0f, flameInner);

        // Diagonal sword blade
        Color bladeLight = new Color(0.97f, 0.98f, 1.00f, 1f);
        Color goldGuard = new Color(0.98f, 0.78f, 0.22f, 1f);
        Color handleDark = new Color(0.32f, 0.14f, 0.06f, 1f);

        DrawSegment(tex, 28, 26, 74, 72, 6.2f, bladeLight);
        DrawDisc(tex, 75, 73, 4.2f, bladeLight);
        // Crossguard
        DrawSegment(tex, 25, 42, 42, 25, 4.6f, goldGuard);
        // Pommel & grip
        DrawSegment(tex, 18, 16, 30, 28, 4.0f, handleDark);
        DrawDisc(tex, 16, 14, 4.8f, goldGuard);
    }

    private static void DrawQuestionMarkGlyph(Texture2D tex, int s, Color color, float offsetX = 0f, float offsetY = 0f, float scale = 1f)
    {
        float cx = s * 0.5f + offsetX;
        float cy = s * 0.5f + offsetY;

        // Top arc of '?'
        float arcCx = cx;
        float arcCy = cy + 12f * scale;
        float arcR = 14f * scale;
        for (int i = 0; i <= 28; i++)
        {
            float a = Mathf.Lerp(175f, -35f, i / 28f) * Mathf.Deg2Rad;
            DrawDisc(tex, arcCx + Mathf.Cos(a) * arcR, arcCy + Mathf.Sin(a) * arcR, 4.2f * scale, color);
        }
        DrawSegment(tex, arcCx + Mathf.Cos(-35f * Mathf.Deg2Rad) * arcR, arcCy + Mathf.Sin(-35f * Mathf.Deg2Rad) * arcR, cx, cy - 2f * scale, 4.2f * scale, color);
        DrawSegment(tex, cx, cy - 2f * scale, cx, cy - 9f * scale, 4.2f * scale, color);
        DrawDisc(tex, cx, cy - 22f * scale, 4.8f * scale, color);
    }

    private static void DrawMusicNote(Texture2D tex, int s)
    {
        // Double beamed eighth note
        DrawSegment(tex, 32, 30, 32, 72, 4.2f, Color.white);
        DrawSegment(tex, 68, 24, 68, 66, 4.2f, Color.white);
        DrawSegment(tex, 30, 72, 70, 66, 5.8f, Color.white);
        DrawSegment(tex, 30, 60, 70, 54, 4.0f, Color.white);
        DrawEllipsePetal(tex, 25f, 27f, 11f, 8.5f, 18f, Color.white);
        DrawEllipsePetal(tex, 61f, 21f, 11f, 8.5f, 18f, Color.white);
    }

    private static void DrawCrosshair(Texture2D tex, int s)
    {
        float c = s * 0.5f;
        DrawRing(tex, c, c, 27f, 4.5f, Color.white);
        DrawRing(tex, c, c, 12f, 3.5f, Color.white);
        DrawSegment(tex, c, 8, c, 24, 3.8f, Color.white);
        DrawSegment(tex, c, 72, c, 88, 3.8f, Color.white);
        DrawSegment(tex, 8, c, 24, c, 3.8f, Color.white);
        DrawSegment(tex, 72, c, 88, c, 3.8f, Color.white);
    }

    private static void DrawHorse(Texture2D tex, int s)
    {
        DrawRoundedBox(tex, 22, 12, 74, 22, 3f, Color.white);
        DrawRoundedBox(tex, 28, 20, 68, 28, 3f, Color.white);
        DrawSegment(tex, 40, 26, 36, 58, 10.5f, Color.white);
        DrawSegment(tex, 54, 26, 50, 52, 9.0f, Color.white);
        DrawSegment(tex, 38, 58, 66, 54, 8.5f, Color.white);
        DrawDisc(tex, 68, 52, 6.2f, Color.white);
        DrawSegment(tex, 36, 64, 32, 82, 4.2f, Color.white);
        DrawSegment(tex, 44, 65, 42, 83, 4.0f, Color.white);
        DrawDisc(tex, 29, 55, 5.2f, Color.white);
        DrawDisc(tex, 29, 44, 5.2f, Color.white);
        DrawDisc(tex, 31, 34, 5.2f, Color.white);
    }

    private static void DrawDicePipFive(Texture2D tex, int s)
    {
        DrawRoundedBox(tex, 16, 16, 80, 80, 12f, Color.white);
        Color hole = new Color(0, 0, 0, 0);
        CarveDisc(tex, 32, 64, 6.2f, hole);
        CarveDisc(tex, 64, 64, 6.2f, hole);
        CarveDisc(tex, 48, 48, 6.6f, hole);
        CarveDisc(tex, 32, 32, 6.2f, hole);
        CarveDisc(tex, 64, 32, 6.2f, hole);
    }

    private static void DrawStar(Texture2D tex, int s)
    {
        Vector2 c = new Vector2(48f, 48f);
        for (int i = 0; i < 5; i++)
        {
            float a = (90f + i * 72f) * Mathf.Deg2Rad;
            Vector2 tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 32f;
            DrawSegment(tex, c.x, c.y, tip.x, tip.y, 6.2f, Color.white);
        }
        DrawDisc(tex, 48f, 48f, 14.5f, Color.white);
    }

    private static void DrawDoorExit(Texture2D tex, int s)
    {
        DrawSegment(tex, 20, 18, 20, 78, 4.5f, Color.white);
        DrawSegment(tex, 20, 78, 52, 78, 4.5f, Color.white);
        DrawSegment(tex, 20, 18, 52, 18, 4.5f, Color.white);
        DrawSegment(tex, 42, 48, 76, 48, 4.8f, Color.white);
        DrawSegment(tex, 62, 62, 78, 48, 4.8f, Color.white);
        DrawSegment(tex, 62, 34, 78, 48, 4.8f, Color.white);
    }

    // -------------------------------------------------------------------------
    // Anti-Aliased SDF Drawing Primitives
    // -------------------------------------------------------------------------
    private static float SignedDistRoundedRect(float px, float py, float w, float h, float r)
    {
        float hx = w * 0.5f;
        float hy = h * 0.5f;
        float qx = Mathf.Abs(px - hx) - (hx - r);
        float qy = Mathf.Abs(py - hy) - (hy - r);
        float outDist = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        float inDist = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outDist + inDist - r;
    }

    private static void DrawRoundedBox(Texture2D tex, int x0, int y0, int x1, int y1, float r, Color col)
    {
        float w = x1 - x0;
        float h = y1 - y0;
        for (int y = Mathf.Max(0, y0 - 2); y <= Mathf.Min(tex.height - 1, y1 + 2); y++)
        {
            for (int x = Mathf.Max(0, x0 - 2); x <= Mathf.Min(tex.width - 1, x1 + 2); x++)
            {
                float dist = SignedDistRoundedRect(x - x0 + 0.5f, y - y0 + 0.5f, w, h, r);
                float cov = Mathf.Clamp01(0.5f - dist);
                if (cov > 0.001f)
                    BlendPixel(tex, x, y, new Color(col.r, col.g, col.b, col.a * cov));
            }
        }
    }

    private static void DrawDisc(Texture2D tex, float cx, float cy, float r, Color col)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - 2));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + r + 2));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - 2));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + r + 2));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;
                float cov = Mathf.Clamp01(0.6f - d);
                if (cov > 0.001f)
                    BlendPixel(tex, x, y, new Color(col.r, col.g, col.b, col.a * cov));
            }
        }
    }

    private static void CarveDisc(Texture2D tex, float cx, float cy, float r, Color clear)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - 2));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + r + 2));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - 2));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + r + 2));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r;
                if (d <= 0f)
                    tex.SetPixel(x, y, clear);
                else if (d < 1.0f)
                {
                    Color prev = tex.GetPixel(x, y);
                    prev.a *= d;
                    tex.SetPixel(x, y, prev);
                }
            }
        }
    }

    private static void DrawRing(Texture2D tex, float cx, float cy, float r, float halfThickness, Color col)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - halfThickness - 2));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + r + halfThickness + 2));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - halfThickness - 2));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + r + halfThickness + 2));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r) - halfThickness;
                float cov = Mathf.Clamp01(0.6f - d);
                if (cov > 0.001f)
                    BlendPixel(tex, x, y, new Color(col.r, col.g, col.b, col.a * cov));
            }
        }
    }

    private static void DrawEllipsePetal(Texture2D tex, float cx, float cy, float rx, float ry, float rotDeg, Color col)
    {
        float rad = rotDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float bound = Mathf.Max(rx, ry) + 2f;

        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - bound));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + bound));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - bound));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + bound));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float lx = (dx * cos + dy * sin) / rx;
                float ly = (-dx * sin + dy * cos) / ry;
                float d = (Mathf.Sqrt(lx * lx + ly * ly) - 1f) * Mathf.Min(rx, ry);
                float cov = Mathf.Clamp01(0.6f - d);
                if (cov > 0.001f)
                    BlendPixel(tex, x, y, new Color(col.r, col.g, col.b, col.a * cov));
            }
        }
    }

    private static void DrawSegment(Texture2D tex, float x0, float y0, float x1, float y1, float r, Color col)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - r - 2));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + r + 2));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - r - 2));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + r + 2));

        Vector2 a = new Vector2(x0, y0);
        Vector2 b = new Vector2(x1, y1);
        Vector2 ab = b - a;
        float abSqr = Vector2.Dot(ab, ab);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x, y);
                float t = abSqr > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSqr) : 0f;
                Vector2 proj = a + ab * t;
                float d = Vector2.Distance(p, proj) - r;
                float cov = Mathf.Clamp01(0.6f - d);
                if (cov > 0.001f)
                    BlendPixel(tex, x, y, new Color(col.r, col.g, col.b, col.a * cov));
            }
        }
    }

    private static void BlendPixel(Texture2D tex, int x, int y, Color src)
    {
        Color dst = tex.GetPixel(x, y);
        float outA = src.a + dst.a * (1f - src.a);
        if (outA <= 0.0001f)
        {
            tex.SetPixel(x, y, Color.clear);
            return;
        }

        Color res = new Color(
            (src.r * src.a + dst.r * dst.a * (1f - src.a)) / outA,
            (src.g * src.a + dst.g * dst.a * (1f - src.a)) / outA,
            (src.b * src.a + dst.b * dst.a * (1f - src.a)) / outA,
            outA
        );
        tex.SetPixel(x, y, res);
    }

    /// <summary>
    /// Creates or refreshes the shared glossy round '?' Party Clash guide button cleanly.
    /// </summary>
    public static Button BuildHelpGuideButton(Transform canvasRoot, Vector2 anchorPivot, Vector2 anchoredPos, float size = 40f)
    {
        if (canvasRoot == null)
            return null;

        Transform existing = canvasRoot.Find("RoyalGuideHelpButton");
        if (existing != null)
            Object.Destroy(existing.gameObject);

        GameObject btnObj = new GameObject("RoyalGuideHelpButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
        btnObj.layer = canvasRoot.gameObject.layer;
        btnObj.transform.SetParent(canvasRoot, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorPivot;
        rt.anchorMax = anchorPivot;
        rt.pivot = anchorPivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        Image bg = btnObj.GetComponent<Image>();
        bg.sprite = GetCircleMedallionSprite();
        bg.type = Image.Type.Simple;
        bg.color = ArcadeAzure;

        Outline outline = btnObj.GetComponent<Outline>();
        outline.effectColor = BrassBright;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow shadow = btnObj.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
        shadow.effectDistance = new Vector2(0f, -3.5f);

        GameObject glyph = new GameObject("HelpGlyph", typeof(RectTransform), typeof(Image));
        glyph.layer = canvasRoot.gameObject.layer;
        glyph.transform.SetParent(rt, false);
        RectTransform gRt = glyph.GetComponent<RectTransform>();
        gRt.anchorMin = Vector2.zero;
        gRt.anchorMax = Vector2.one;
        gRt.offsetMin = new Vector2(size * 0.20f, size * 0.20f);
        gRt.offsetMax = new Vector2(-size * 0.20f, -size * 0.20f);

        Image gImg = glyph.GetComponent<Image>();
        gImg.sprite = GetIcon(IconKind.HelpMark);
        gImg.preserveAspect = true;
        gImg.color = IvoryText;
        gImg.raycastTarget = false;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => TogglePartyClashRulesPopup(canvasRoot));

        if (btnObj.GetComponent<RoyalButtonHoverEffect>() == null)
            btnObj.AddComponent<RoyalButtonHoverEffect>();

        return btn;
    }
}

/// <summary>
/// Closes an active modal overlay cleanly when the Escape key is pressed.
/// </summary>
public class RoyalModalEscCloser : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            gameObject.SetActive(false);
    }
}
