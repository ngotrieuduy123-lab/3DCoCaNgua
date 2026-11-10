using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class AnimeKickCutscene : MonoBehaviour
{
    private static AnimeKickCutscene instance;

    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private RectTransform leftBanner;
    private RectTransform rightBanner;
    private Image leftBannerBg;
    private Image rightBannerBg;
    private Image leftPortrait;
    private Image rightPortrait;
    private TMP_Text leftNameText;
    private TMP_Text rightNameText;
    private RectTransform centerBadge;
    private TMP_Text centerClashText;
    private TMP_Text debugHintText;

    private RectTransform pipFrame;
    private RawImage pipRawImage;
    private Camera actionCamera;
    private RenderTexture actionRenderTex;

    private bool isPlayingCutscene = false;
    private bool isSlowMoActive = false;
    private const float SlowMoTimeScale = 0.30f;

    public bool IsPlayingCutscene => isPlayingCutscene;
    public bool IsSlowMoActive => isSlowMoActive;

    public static AnimeKickCutscene EnsureCreated()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<AnimeKickCutscene>();
        if (instance != null)
            return instance;

        GameObject go = new GameObject("AnimeKickCutsceneManager");
        instance = go.AddComponent<AnimeKickCutscene>();
        instance.BuildOverlayUI();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (overlayCanvas == null)
            BuildOverlayUI();
    }

    void OnDestroy()
    {
        if (isSlowMoActive)
            Time.timeScale = 1f;

        if (actionRenderTex != null)
        {
            actionRenderTex.Release();
            Destroy(actionRenderTex);
        }
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        // Phím L: Bật / Tắt chế độ Slow-Motion (0.3x) để soi kỹ hoạt cảnh
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            ToggleSlowMo();
        }

        // Phím K: Test ngay pha Đá Quân Anime bất kỳ lúc nào trong Play Mode
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            TriggerDebugKickTest();
        }
    }

    public void ToggleSlowMo()
    {
        isSlowMoActive = !isSlowMoActive;
        Time.timeScale = isSlowMoActive ? SlowMoTimeScale : 1f;
        UpdateDebugHintLabel();

        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null)
        {
            string msg = isSlowMoActive
                ? "SLOW-MO 0.3x: ĐÃ BẬT (Ấn L để về 1.0x | Ấn K để Test Đá)"
                : "TỐC ĐỘ 1.0x: BÌNH THƯỜNG (Ấn L bật Slow-Mo | Ấn K để Test Đá)";
            ui.SetMessage(msg);
        }

        Debug.Log($"[AnimeKickCutscene] Slow-Mo = {isSlowMoActive} (Time.timeScale = {Time.timeScale:F2})");
    }

    public void TriggerDebugKickTest()
    {
        if (isPlayingCutscene)
        {
            Debug.Log("[AnimeKickCutscene] Cutscene đang chạy, vui lòng đợi kết thúc...");
            return;
        }

        BoardManager bm = FindFirstObjectByType<BoardManager>();
        if (bm == null || bm.allPieces == null || bm.pathPoints == null || bm.pathPoints.Length < 10)
            return;

        int activeTurn = 0;
        if (TurnManager.Instance != null)
            activeTurn = TurnManager.Instance.currentPlayerIndex;

        int enemyPlayer = (activeTurn + 1) % 2;

        PieceController attacker = null;
        PieceController victim = null;

        foreach (PieceController p in bm.allPieces)
        {
            if (p == null) continue;
            if (p.playerIndex == activeTurn && attacker == null && !p.isMoving)
                attacker = p;
            else if (p.playerIndex == enemyPlayer && victim == null && !p.isMoving)
                victim = p;
        }

        if (attacker == null || victim == null)
            return;

        // Tìm 1 ô trống trên đường đua chính để dàn cảnh đá 2 bước chân thực
        int startTile = (!attacker.isInStable && !attacker.isInHomePath && attacker.currentIndex >= 0)
            ? attacker.currentIndex
            : attacker.startIndex;

        int targetTile = (startTile + 2) % bm.pathPoints.Length;
        if (bm.HasOwnPieceAt(attacker.playerIndex, targetTile))
        {
            startTile = (startTile + 4) % bm.pathPoints.Length;
            targetTile = (startTile + 2) % bm.pathPoints.Length;
        }

        // Đặt quân địch đứng chờ sẵn ở cách 2 ô
        victim.isInStable = false;
        victim.isInHomePath = false;
        victim.hasSpawnProtection = false;
        victim.currentIndex = targetTile;
        victim.stepsMoved = 16;
        victim.transform.position = bm.pathPoints[targetTile].position;
        victim.UpdateStatusBadgeRing();

        // Đặt quân tấn công ở cách 2 ô phía sau và cho chạy 2 bước tới đá thật!
        attacker.isInStable = false;
        attacker.isInHomePath = false;
        attacker.hasSpawnProtection = false;
        attacker.currentIndex = startTile;
        attacker.stepsMoved = 14;
        attacker.transform.position = bm.pathPoints[startTile].position;
        attacker.UpdateStatusBadgeRing();

        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null)
        {
            string modeTag = isSlowMoActive ? " [SLOW-MO 0.3x]" : "";
            ui.SetMessage($"[TEST KICK - Phím K]{modeTag}: {(PlayerColor)attacker.playerIndex} lao tới đá văng {(PlayerColor)victim.playerIndex}!");
        }

        attacker.MoveByStep(2);
    }

    public IEnumerator PlayClashRoutine(
        PieceController attacker,
        PieceController victim,
        int kickPoints,
        Vector3 victimStablePos)
    {
        if (attacker == null || victim == null)
            yield break;

        isPlayingCutscene = true;
        BuildOverlayUI();
        UpdateDebugHintLabel();

        int atkIdx = attacker.playerIndex;
        int vicIdx = victim.playerIndex;
        Vector3 clashPos = victim.transform.position;

        // Báo cho người ngồi quanh chiếu cờ giậm chân / giật mình
        if (VietnameseFloorAtmosphere.Instance != null)
            VietnameseFloorAtmosphere.Instance.TriggerKickReaction(atkIdx, vicIdx);

        ConfigureBannerVisuals(atkIdx, vicIdx, kickPoints);
        PositionActionCamera(clashPos, attacker.transform.position);

        overlayCanvas.gameObject.SetActive(true);
        actionCamera.gameObject.SetActive(true);
        overlayGroup.alpha = 1f;

        // Vị trí khởi đầu của 2 dải băng VS Cut-in
        leftBanner.anchoredPosition = new Vector2(-950f, 165f);
        rightBanner.anchoredPosition = new Vector2(950f, 100f);
        centerBadge.localScale = Vector3.zero;
        pipFrame.localScale = new Vector3(1f, 0.05f, 1f);

        Vector3 atkStartPos = attacker.transform.position;
        Quaternion atkStartRot = attacker.transform.rotation;
        Vector3 vicStartPos = victim.transform.position;
        Quaternion vicStartRot = victim.transform.rotation;
        Vector3 vicOrigScale = victim.transform.localScale;

        // Hướng đối mặt giữa 2 quân cờ
        Vector3 clashDir = (vicStartPos - atkStartPos);
        clashDir.y = 0f;
        if (clashDir.sqrMagnitude < 0.0001f)
            clashDir = Vector3.forward;
        clashDir.Normalize();

        Vector3 faceOffAtkPos = clashPos - clashDir * 0.11f;

        // =========================================================================
        // PHASE 1 (0.42s): Banner VS trượt chéo vào + Mở khung 3D Action Cam + Gườm nhau
        // =========================================================================
        float p1Duration = 0.42f;
        float elapsed = 0f;
        while (elapsed < p1Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / p1Duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            leftBanner.anchoredPosition = Vector2.Lerp(new Vector2(-950f, 165f), new Vector2(-220f, 165f), ease);
            rightBanner.anchoredPosition = Vector2.Lerp(new Vector2(950f, 100f), new Vector2(220f, 100f), ease);
            centerBadge.localScale = Vector3.one * Mathf.Lerp(0f, 1.22f, ease);
            pipFrame.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1f, ease), 1f);

            // Quân đá lùi nhẹ lấy đà trước mặt quân địch
            attacker.transform.position = Vector3.Lerp(atkStartPos, faceOffAtkPos, ease);

            // Quân bị đá run lắc nhẹ vì sắp trúng đòn
            float tremble = Mathf.Sin(t * Mathf.PI * 10f) * 0.012f;
            victim.transform.position = vicStartPos + new Vector3(tremble, 0f, -tremble);

            yield return null;
        }

        centerBadge.localScale = Vector3.one;

        // =========================================================================
        // PHASE 2 (0.55s): Tụ lực & Nhảy vọt lên không trung + Xoay 360 độ (Aerial Sommersault)
        // =========================================================================
        float p2Duration = 0.55f;
        elapsed = 0f;
        Vector3 jumpApexPos = clashPos + Vector3.up * 0.36f;
        while (elapsed < p2Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / p2Duration);
            float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

            // Quỹ đạo bật nhảy cao lên đỉnh đầu đối thủ và xoay 360 độ
            attacker.transform.position = Vector3.Lerp(faceOffAtkPos, jumpApexPos, easeOut);
            attacker.transform.rotation = Quaternion.Euler(t * 360f, t * 360f, 0f) * atkStartRot;

            // Action Cam lia nhẹ theo độ cao cú nhảy
            actionCamera.transform.LookAt(Vector3.Lerp(clashPos, attacker.transform.position, 0.55f) + Vector3.up * 0.06f);

            // Huy hiệu VS nhịp nhẹ
            float badgePulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * 0.08f;
            centerBadge.localScale = Vector3.one * badgePulse;

            yield return null;
        }

        // =========================================================================
        // PHASE 3 (0.30s): Giáng đòn Meteor Slam xuống ô cờ + Hit-Stop bẹp dí đối thủ!
        // =========================================================================
        float slamDuration = 0.16f;
        elapsed = 0f;
        while (elapsed < slamDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slamDuration);
            attacker.transform.position = Vector3.Lerp(jumpApexPos, clashPos, t * t * t);
            attacker.transform.rotation = atkStartRot;
            yield return null;
        }
        attacker.transform.position = clashPos;

        // Tạo 2 vòng sóng xung kích Shockwave Ring lan tỏa
        StartCoroutine(PlayImpactShockwaveRoutine(clashPos, GetFactionColor(atkIdx), 0.30f, 0.55f));
        StartCoroutine(PlayImpactShockwaveRoutine(clashPos + Vector3.up * 0.03f, new Color(1f, 0.90f, 0.35f, 1f), 0.20f, 0.40f));

        // Hit-Stop (0.16s): Quân địch bị dập dẹp xuống sàn trước khi văng đi!
        victim.transform.localScale = new Vector3(vicOrigScale.x * 1.45f, vicOrigScale.y * 0.42f, vicOrigScale.z * 1.45f);
        centerBadge.localScale = Vector3.one * 1.32f;
        yield return new WaitForSeconds(0.16f);
        victim.transform.localScale = vicOrigScale;
        centerBadge.localScale = Vector3.one;

        // =========================================================================
        // PHASE 4 (1.15s): Quân bị đá bay Parabol cao vút + Xoay 1080° về thẳng Chuồng!
        // =========================================================================
        Camera mainCam = Camera.main;
        Vector3 mainCamOrigPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
        Vector3 actionCamBasePos = actionCamera.transform.position;

        float p4Duration = 1.15f;
        elapsed = 0f;
        while (elapsed < p4Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / p4Duration);

            // Rung lắc Camera mạnh trong 0.36s đầu sau va chạm
            if (elapsed < 0.36f)
            {
                float shakeAmp = (1f - elapsed / 0.36f) * 0.055f;
                Vector3 shakeOffset = Random.insideUnitSphere * shakeAmp;
                if (mainCam != null)
                    mainCam.transform.position = mainCamOrigPos + shakeOffset;
                actionCamera.transform.position = actionCamBasePos + shakeOffset * 0.65f;
            }
            else if (mainCam != null)
            {
                mainCam.transform.position = mainCamOrigPos;
            }

            // Quân thắng cuộc nhún nhảy ăn mừng tại ô cờ
            float victoryHop = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * 0.08f * (1f - t);
            attacker.transform.position = clashPos + Vector3.up * victoryHop;

            // Quân bị đá bay Parabol cao vút + xoay 1080 độ về thẳng chuồng
            float heightArc = Mathf.Sin(t * Mathf.PI) * 0.68f;
            Vector3 horizontalPos = Vector3.Lerp(vicStartPos, victimStablePos, t);
            victim.transform.position = horizontalPos + Vector3.up * heightArc;
            victim.transform.rotation = Quaternion.Euler(t * 720f, t * 1080f, t * 540f) * vicStartRot;

            // Action Cam lia theo hướng bay của nạn nhân
            Vector3 lookTarget = Vector3.Lerp(clashPos, victim.transform.position, 0.45f) + Vector3.up * 0.10f;
            actionCamera.transform.LookAt(lookTarget);

            // Mờ dần giao diện Cutscene ở 25% cuối
            if (t > 0.75f)
            {
                overlayGroup.alpha = Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);
            }

            yield return null;
        }

        if (mainCam != null)
            mainCam.transform.position = mainCamOrigPos;

        attacker.transform.position = clashPos;
        attacker.transform.rotation = atkStartRot;
        victim.transform.position = victimStablePos;
        victim.transform.rotation = vicStartRot;
        victim.transform.localScale = vicOrigScale;

        overlayGroup.alpha = 0f;
        actionCamera.gameObject.SetActive(false);
        overlayCanvas.gameObject.SetActive(false);
        isPlayingCutscene = false;
    }

    void PositionActionCamera(Vector3 clashPos, Vector3 attackerPos)
    {
        Vector3 dir = (clashPos - attackerPos);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        dir.Normalize();

        // Đặt Action Cam ở góc 3/4 cận cảnh thấp sát mặt bàn cờ để thấy rõ cú nhảy và văng
        Vector3 sideDir = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 camOffset = (-dir * 0.34f) + (sideDir * 0.40f) + (Vector3.up * 0.25f);
        actionCamera.transform.position = clashPos + camOffset;
        actionCamera.transform.LookAt(clashPos + Vector3.up * 0.12f);
    }

    void ConfigureBannerVisuals(int atkIdx, int vicIdx, int kickPoints)
    {
        Color atkColor = GetFactionColor(atkIdx);
        Color vicColor = GetFactionColor(vicIdx);

        leftBannerBg.color = new Color(atkColor.r * 0.85f, atkColor.g * 0.85f, atkColor.b * 0.85f, 0.96f);
        rightBannerBg.color = new Color(vicColor.r * 0.85f, vicColor.g * 0.85f, vicColor.b * 0.85f, 0.96f);

        string atkName = GetPlayerName(atkIdx);
        string vicName = GetPlayerName(vicIdx);

        leftNameText.text = $"<b>{atkName}</b>\n<size=78%><color=#FFE57F>TẤN CÔNG!</color></size>";
        rightNameText.text = $"<b>{vicName}</b>\n<size=78%><color=#FF9E9E>BỊ HẤT VĂNG!</color></size>";

        centerClashText.text = kickPoints > 0
            ? $"ĐÁ VĂNG!\n<size=75%>+{kickPoints}đ</size>"
            : "ĐẨY LÙI!";

        Sprite atkSprite = GetEquippedSkinPreview(atkIdx);
        Sprite vicSprite = GetEquippedSkinPreview(vicIdx);

        if (leftPortrait != null)
        {
            leftPortrait.sprite = atkSprite != null ? atkSprite : GameplayUI.GetCirclePipSprite();
            leftPortrait.color = atkSprite != null ? Color.white : atkColor;
        }

        if (rightPortrait != null)
        {
            rightPortrait.sprite = vicSprite != null ? vicSprite : GameplayUI.GetCirclePipSprite();
            rightPortrait.color = vicSprite != null ? Color.white : vicColor;
        }
    }

    void UpdateDebugHintLabel()
    {
        if (debugHintText == null)
            return;

        string slowMoStatus = isSlowMoActive
            ? "<color=#4DFF88>SLOW-MO: BẬT</color>"
            : "<color=#FFD966>1.0x</color>";
        debugHintText.text = $"[K] Đá Anime | [L] {slowMoStatus} | [B] Truy Nã | [H] Bốc Cháy | [N] Tắt SK";
    }

    Sprite GetEquippedSkinPreview(int playerIndex)
    {
        string skinId = PlayerPrefs.GetString("PlayerSkin_" + playerIndex, SkinCatalog.DefaultSkinId);
        SkinCatalog catalog = SkinCatalog.Load();
        if (catalog == null)
            return null;

        SkinDefinition def = catalog.Get(skinId);
        return def != null ? def.previewImage : null;
    }

    static string GetPlayerName(int playerIndex)
    {
        string name = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");
        if (string.IsNullOrWhiteSpace(name))
            name = ((PlayerColor)playerIndex).ToString();
        return name;
    }

    static Color GetFactionColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.18f, 0.45f, 0.92f, 1f);
            case 1: return new Color(0.88f, 0.20f, 0.20f, 1f);
            case 2: return new Color(0.16f, 0.68f, 0.34f, 1f);
            case 3: return new Color(0.86f, 0.62f, 0.12f, 1f);
            default: return Color.white;
        }
    }

    IEnumerator PlayImpactShockwaveRoutine(Vector3 center, Color color, float maxRadius, float duration)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default");
        if (shader == null)
            yield break;

        GameObject ringGo = new GameObject("AnimeImpactShockwave");
        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreLayer >= 0)
            ringGo.layer = ignoreLayer;

        ringGo.transform.position = center + Vector3.up * 0.02f;

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        LineRenderer lr = ringGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 32;
        lr.widthMultiplier = 0.026f;
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(0.03f, maxRadius, 1f - Mathf.Pow(1f - t, 2f));
            lr.widthMultiplier = Mathf.Lerp(0.028f, 0.002f, t);

            for (int i = 0; i < 32; i++)
            {
                float angle = (Mathf.PI * 2f * i) / 32f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            yield return null;
        }

        Destroy(mat);
        Destroy(ringGo);
    }

    void BuildOverlayUI()
    {
        if (overlayCanvas != null)
            return;

        // 1. Create Action Camera & RenderTexture
        actionRenderTex = new RenderTexture(560, 340, 16, RenderTextureFormat.ARGB32);
        actionRenderTex.Create();

        GameObject camObj = new GameObject("AnimeClashActionCamera");
        camObj.transform.SetParent(transform, false);
        actionCamera = camObj.AddComponent<Camera>();
        actionCamera.fieldOfView = 36f;
        actionCamera.nearClipPlane = 0.05f;
        actionCamera.farClipPlane = 20f;
        actionCamera.targetTexture = actionRenderTex;
        int ignoreRaycastMask = ~(1 << LayerMask.NameToLayer("UI"));
        actionCamera.cullingMask = ignoreRaycastMask;
        camObj.SetActive(false);

        // 2. Create Screen-Space Overlay Canvas
        GameObject canvasObj = new GameObject("AnimeClashOverlayCanvas");
        canvasObj.transform.SetParent(transform, false);

        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 4800;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        overlayGroup = canvasObj.AddComponent<CanvasGroup>();
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;

        Sprite panelSprite = GameplayUI.GetRoundedPanelSprite();

        // Left Attacker Slash Banner
        leftBanner = CreateBannerStrip(
            canvasObj.transform,
            "LeftAttackerBanner",
            new Vector2(-220f, 165f),
            new Vector2(560f, 96f),
            panelSprite,
            true,
            out leftBannerBg,
            out leftPortrait,
            out leftNameText
        );

        // Right Victim Slash Banner
        rightBanner = CreateBannerStrip(
            canvasObj.transform,
            "RightVictimBanner",
            new Vector2(220f, 100f),
            new Vector2(560f, 96f),
            panelSprite,
            false,
            out rightBannerBg,
            out rightPortrait,
            out rightNameText
        );

        // Center VS Clash Badge
        GameObject badgeObj = new GameObject("CenterClashBadge");
        badgeObj.transform.SetParent(canvasObj.transform, false);
        centerBadge = badgeObj.AddComponent<RectTransform>();
        centerBadge.anchorMin = new Vector2(0.5f, 0.5f);
        centerBadge.anchorMax = new Vector2(0.5f, 0.5f);
        centerBadge.pivot = new Vector2(0.5f, 0.5f);
        centerBadge.anchoredPosition = new Vector2(0f, 132f);
        centerBadge.sizeDelta = new Vector2(204f, 90f);

        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.sprite = panelSprite;
        badgeBg.type = Image.Type.Sliced;
        badgeBg.color = new Color(0.14f, 0.09f, 0.04f, 0.98f);

        Outline badgeOutline = badgeObj.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(1f, 0.84f, 0.25f, 1f);
        badgeOutline.effectDistance = new Vector2(3f, -3f);

        centerClashText = CreateOverlayText(
            centerBadge,
            "CenterClashLabel",
            Vector2.zero,
            new Vector2(204f, 90f),
            24f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.86f, 0.24f, 1f)
        );

        // Picture-in-Picture 3D Action Cam Window below the VS Banner
        GameObject pipObj = new GameObject("ActionCamPiPFrame");
        pipObj.transform.SetParent(canvasObj.transform, false);
        pipFrame = pipObj.AddComponent<RectTransform>();
        pipFrame.anchorMin = new Vector2(0.5f, 0.5f);
        pipFrame.anchorMax = new Vector2(0.5f, 0.5f);
        pipFrame.pivot = new Vector2(0.5f, 0.5f);
        pipFrame.anchoredPosition = new Vector2(0f, -95f);
        pipFrame.sizeDelta = new Vector2(520f, 310f);

        Image pipBg = pipObj.AddComponent<Image>();
        pipBg.sprite = panelSprite;
        pipBg.type = Image.Type.Sliced;
        pipBg.color = new Color(0.10f, 0.08f, 0.06f, 0.98f);

        Outline pipOutline = pipObj.AddComponent<Outline>();
        pipOutline.effectColor = new Color(0.92f, 0.72f, 0.26f, 1f);
        pipOutline.effectDistance = new Vector2(3.5f, -3.5f);

        GameObject rawObj = new GameObject("ActionCamView");
        rawObj.transform.SetParent(pipFrame, false);
        RectTransform rawRt = rawObj.AddComponent<RectTransform>();
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        rawRt.offsetMin = new Vector2(8f, 8f);
        rawRt.offsetMax = new Vector2(-8f, -8f);

        pipRawImage = rawObj.AddComponent<RawImage>();
        pipRawImage.texture = actionRenderTex;

        // Nhãn nhắc phím tắt K / L dưới đáy khung Action Cam
        debugHintText = CreateOverlayText(
            pipFrame,
            "CutsceneShortcutHint",
            new Vector2(0f, -136f),
            new Vector2(500f, 28f),
            15f,
            TextAlignmentOptions.Center,
            new Color(0.95f, 0.92f, 0.82f, 0.95f)
        );
        UpdateDebugHintLabel();

        canvasObj.SetActive(false);
    }

    RectTransform CreateBannerStrip(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        Sprite sprite,
        bool isLeft,
        out Image bgImg,
        out Image portraitImg,
        out TMP_Text nameLabel)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        bgImg = go.AddComponent<Image>();
        bgImg.sprite = sprite;
        bgImg.type = Image.Type.Sliced;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.35f, 1f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        GameObject portObj = new GameObject("SkinPortrait");
        portObj.transform.SetParent(rt, false);
        RectTransform portRt = portObj.AddComponent<RectTransform>();
        portRt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0.5f);
        portRt.anchorMax = new Vector2(isLeft ? 0f : 1f, 0.5f);
        portRt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
        portRt.anchoredPosition = new Vector2(isLeft ? 14f : -14f, 0f);
        portRt.sizeDelta = new Vector2(74f, 74f);

        portraitImg = portObj.AddComponent<Image>();
        portraitImg.preserveAspect = true;

        Vector2 textPos = isLeft ? new Vector2(102f, 0f) : new Vector2(-102f, 0f);
        nameLabel = CreateOverlayText(
            rt,
            "PlayerClashName",
            textPos,
            new Vector2(340f, 78f),
            22f,
            isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
            Color.white
        );

        return rt;
    }

    static TMP_Text CreateOverlayText(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions align,
        Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null && ui.turnText != null && ui.turnText.font != null)
            tmp.font = ui.turnText.font;

        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
