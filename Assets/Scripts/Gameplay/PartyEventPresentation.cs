using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PartyEventPresentation : MonoBehaviour
{
    public static PartyEventPresentation Instance { get; private set; }

    private BoardManager board;
    private PartyEventType lastObservedEvent = PartyEventType.None;
    private int lastObservedBountyTarget = -1;
    private int lastObservedRound = -1;

    // Audio for Event Stings & Ambient Loop
    private AudioSource stingAudioSource;
    private AudioSource ambientLoopSource;
    private AudioClip bountyStingClip;
    private AudioClip homeRushStingClip;
    private AudioClip fireLoopClip;

    // Shared procedural textures/sprites/materials/meshes
    private Texture2D softParticleTex;
    private Texture2D flameTongueTex;
    private Texture2D sparkStarTex;
    private Texture2D verticalBeamTex;
    private Material flameParticleMat;
    private Material sparkParticleMat;
    private Material bountyDarkBackingMat;
    private Material bountyRingMat;
    private Material bountyInnerMat;
    private Material bountyBeamMat;
    private Material bountySpotDiscMat;
    private Material bountyDiamondMat;
    private Material rushRingMat;
    private Material tmpOverlayMat;
    private readonly Material[] rushStepMats = new Material[6];
    private Mesh spotlightConeMesh;
    private Mesh spotlightDiscMesh;
    private Mesh targetDiamondMesh;
    private Sprite crosshairSprite;
    private Sprite flameSprite;

    // 1. UI Overlay: Dramatic Splash Banner + Screen Edge Vignette
    private Canvas eventCanvas;
    private Image screenFlashOverlay;
    private Image screenEdgeFrame;
    private CanvasGroup splashBannerGroup;
    private RectTransform splashBannerRect;
    private Image splashBannerBg;
    private Outline splashBannerOutline;
    private Image leftHazardBar;
    private Image rightHazardBar;
    private Image leftEmblemImage;
    private Image rightEmblemImage;
    private TMP_Text splashTagText;
    private TMP_Text splashTitleText;
    private TMP_Text splashSubtitleText;
    private TMP_Text splashRuleText;
    private Image activeEventCornerPillBg;
    private TMP_Text activeEventCornerPillText;
    private Coroutine splashRoutine;

    // 2. 3D Pooled Visuals for BOUNTY HUNT (Truy Nã)
    private class BountyPieceRig
    {
        public PieceController piece;
        public GameObject root;
        public LineRenderer darkBackingRing;
        public LineRenderer outerRing;
        public LineRenderer innerCrosshair;
        public Transform crosshairTicksGroup;
        public Transform spotlightBeam;
        public Transform spotlightDisc;
        public Light spotLight;
        public Transform diamondMarker;
        public ParticleSystem sparksPs;
        public TextMeshPro floatingTag;
    }
    private readonly List<BountyPieceRig> bountyRigs = new List<BountyPieceRig>();

    // 3. 3D Pooled Visuals for HOME RUSH (Bàn Cờ Bốc Cháy)
    private class RushRingEntry
    {
        public LineRenderer ring;
        public int stepIndex;
    }
    private GameObject homeRushRoot;
    private readonly List<ParticleSystem> homeRushParticleSystems = new List<ParticleSystem>();
    private readonly List<RushRingEntry> homeRushRings = new List<RushRingEntry>();
    private readonly List<Light> homeRushFireLights = new List<Light>();
    private readonly List<Transform> homeRushFloatingLabels = new List<Transform>();

    private static readonly Color CrimsonAlert = new Color(1.00f, 0.18f, 0.22f, 1f);
    private static readonly Color GoldBright = new Color(1.00f, 0.86f, 0.28f, 1f);
    private static readonly Color ColorBlazeOrange = new Color(1.00f, 0.44f, 0.08f, 1f);

    public static PartyEventPresentation EnsureCreated(BoardManager targetBoard = null)
    {
        if (Instance != null)
        {
            if (targetBoard != null && Instance.board == null)
                Instance.Initialize(targetBoard);
            return Instance;
        }

        PartyEventPresentation existing = FindFirstObjectByType<PartyEventPresentation>();
        if (existing != null)
        {
            Instance = existing;
            if (targetBoard != null)
                existing.Initialize(targetBoard);
            return existing;
        }

        GameObject go = new GameObject("PartyEventPresentationManager");
        Instance = go.AddComponent<PartyEventPresentation>();
        if (targetBoard == null)
            targetBoard = FindFirstObjectByType<BoardManager>();
        if (targetBoard != null)
            Instance.Initialize(targetBoard);

        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureResources();
        BuildOverlayCanvas();
    }

    public void Initialize(BoardManager targetBoard)
    {
        board = targetBoard;
        EnsureResources();
        BuildOverlayCanvas();
        BuildBountyPieceRigs();
        BuildHomeRushBlazingBoardRig();
        SyncVisualsImmediate(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DestroySafe(softParticleTex);
        DestroySafe(flameTongueTex);
        DestroySafe(sparkStarTex);
        DestroySafe(verticalBeamTex);
        DestroySafe(flameParticleMat);
        DestroySafe(sparkParticleMat);
        DestroySafe(bountyDarkBackingMat);
        DestroySafe(bountyRingMat);
        DestroySafe(bountyInnerMat);
        DestroySafe(bountyBeamMat);
        DestroySafe(bountySpotDiscMat);
        DestroySafe(bountyDiamondMat);
        DestroySafe(rushRingMat);
        DestroySafe(tmpOverlayMat);
        for (int i = 0; i < rushStepMats.Length; i++)
            DestroySafe(rushStepMats[i]);
        DestroySafe(spotlightConeMesh);
        DestroySafe(spotlightDiscMesh);
        DestroySafe(targetDiamondMesh);
    }

    private static void DestroySafe(UnityEngine.Object obj)
    {
        if (obj != null)
            Destroy(obj);
    }

    private void Update()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<BoardManager>();
            if (board != null)
                Initialize(board);
            else
                return;
        }

        HandleDemoKeyboardShortcuts();
        CheckForEventStateTransition();
        AnimateActiveEventVisuals();
    }

    // =========================================================================
    // DEMO KEYBOARD SHORTCUTS (B = Truy Nã, H = Bàn Cờ Bốc Cháy, N = Tắt)
    // =========================================================================
    private void HandleDemoKeyboardShortcuts()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            TriggerDemoBountyHunt();
        }
        else if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            TriggerDemoHomeRush();
        }
        else if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            TriggerDemoClearEvent();
        }
    }

    public void TriggerDemoBountyHunt()
    {
        if (board == null)
            return;

        int playerCount = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
        if (NetworkTurnManager.Instance != null && NetworkTurnManager.Instance.IsSpawned)
            playerCount = Mathf.Clamp(NetworkTurnManager.Instance.playerCount.Value, 2, 4);
        else if (TurnManager.Instance != null)
            playerCount = Mathf.Clamp(TurnManager.Instance.playerCount, 2, 4);

        // Pick next target on repeated presses so demo presenter can cycle targets if desired
        int nextTarget = board.bountyTargetPlayer;
        if (board.activeEvent == PartyEventType.BountyHunt && nextTarget >= 0)
        {
            nextTarget = (nextTarget + 1) % playerCount;
        }
        else
        {
            int bestIdx = -1;
            int bestScore = -1;
            for (int i = 0; i < playerCount; i++)
            {
                if (board.IsPlayerParticipating(i) && board.playerScores[i] > bestScore)
                {
                    bestScore = board.playerScores[i];
                    bestIdx = i;
                }
            }
            nextTarget = bestIdx >= 0 ? bestIdx : 0;
        }

        board.activeEvent = PartyEventType.BountyHunt;
        board.upcomingEvent = PartyEventType.None;
        board.bountyTargetPlayer = nextTarget;
        board.bountyTargetWasKickedThisRound = false;
        for (int i = 0; i < 4; i++)
            board.bountyClaimedThisRound[i] = false;

        board.UpdateEventBannerText(playerCount);

        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null)
        {
            ui.RefreshPartyClashHud(board);
            ui.SetMessage($"LỆNH TRUY NÃ KÍCH HOẠT: Nhắm vào {(PlayerColor)nextTarget}!");
        }

        if (NetworkDiceManager.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkDiceManager.Instance.SyncPartyClashBoardStateServer();
        }

        // Force splash replay even if already in BountyHunt
        lastObservedEvent = PartyEventType.None;
        CheckForEventStateTransition();
    }

    public void TriggerDemoHomeRush()
    {
        if (board == null)
            return;

        int playerCount = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
        if (NetworkTurnManager.Instance != null && NetworkTurnManager.Instance.IsSpawned)
            playerCount = Mathf.Clamp(NetworkTurnManager.Instance.playerCount.Value, 2, 4);
        else if (TurnManager.Instance != null)
            playerCount = Mathf.Clamp(TurnManager.Instance.playerCount, 2, 4);

        board.activeEvent = PartyEventType.HomeRush;
        board.upcomingEvent = PartyEventType.None;
        board.bountyTargetPlayer = -1;
        for (int i = 0; i < 4; i++)
            board.homeRushUsedThisRound[i] = false;

        board.UpdateEventBannerText(playerCount);

        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null)
        {
            ui.RefreshPartyClashHud(board);
            ui.SetMessage("NƯỚC RÚT VỀ ĐÍCH: Bàn cờ rực lửa, tăng tốc về chuồng!");
        }

        if (NetworkDiceManager.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkDiceManager.Instance.SyncPartyClashBoardStateServer();
        }

        lastObservedEvent = PartyEventType.None;
        CheckForEventStateTransition();
    }

    public void TriggerDemoClearEvent()
    {
        if (board == null)
            return;

        int playerCount = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
        board.activeEvent = PartyEventType.None;
        board.upcomingEvent = PartyEventType.None;
        board.bountyTargetPlayer = -1;
        board.UpdateEventBannerText(playerCount);

        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null)
        {
            ui.RefreshPartyClashHud(board);
            ui.SetMessage("Sự kiện kết thúc — Vòng đấu trở lại bình thường.");
        }

        if (NetworkDiceManager.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkDiceManager.Instance.SyncPartyClashBoardStateServer();
        }

        CheckForEventStateTransition();
    }

    // =========================================================================
    // STATE TRANSITION & SPLASH BANNER TRIGGER
    // =========================================================================
    private void CheckForEventStateTransition()
    {
        if (board == null)
            return;

        PartyEventType currentEvt = board.activeEvent;
        int currentTarget = board.bountyTargetPlayer;
        int currentRound = board.currentRound;

        if (currentEvt == lastObservedEvent &&
            currentTarget == lastObservedBountyTarget &&
            currentRound == lastObservedRound)
        {
            return;
        }

        bool eventChangedOrStarted = currentEvt != lastObservedEvent ||
                                    (currentEvt != PartyEventType.None && currentRound != lastObservedRound) ||
                                    (currentEvt == PartyEventType.BountyHunt && currentTarget != lastObservedBountyTarget);

        lastObservedEvent = currentEvt;
        lastObservedBountyTarget = currentTarget;
        lastObservedRound = currentRound;

        SyncVisualsImmediate(eventChangedOrStarted);
    }

    private void SyncVisualsImmediate(bool playSplash)
    {
        if (board == null)
            return;

        PartyEventType evt = board.activeEvent;

        // 1. Toggle Home Rush Blazing Board 3D VFX
        if (homeRushRoot != null)
        {
            bool rushActive = evt == PartyEventType.HomeRush;
            homeRushRoot.SetActive(rushActive);
            foreach (ParticleSystem ps in homeRushParticleSystems)
            {
                if (ps == null) continue;
                if (rushActive)
                {
                    ps.Clear();
                    ps.Play();
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        // 2. Toggle ambient fire loop sound
        if (ambientLoopSource != null)
        {
            if (evt == PartyEventType.HomeRush && fireLoopClip != null)
            {
                if (!ambientLoopSource.isPlaying)
                {
                    ambientLoopSource.clip = fireLoopClip;
                    ambientLoopSource.volume = 0.24f;
                    ambientLoopSource.Play();
                }
            }
            else if (ambientLoopSource.isPlaying)
            {
                ambientLoopSource.Stop();
            }
        }

        // 3. Update persistent top-center status pill & screen edge frame
        UpdatePersistentOverlayState(evt, board.bountyTargetPlayer);

        // 4. Play dramatic 2.3s Splash Banner on event activation
        if (playSplash && evt != PartyEventType.None)
        {
            if (splashRoutine != null)
                StopCoroutine(splashRoutine);
            splashRoutine = StartCoroutine(PlaySplashAnnouncementRoutine(evt, board.bountyTargetPlayer));
        }
        else if (evt == PartyEventType.None)
        {
            if (splashRoutine != null)
            {
                StopCoroutine(splashRoutine);
                splashRoutine = null;
            }
            if (splashBannerGroup != null)
                splashBannerGroup.alpha = 0f;
            if (screenFlashOverlay != null)
                screenFlashOverlay.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    private void UpdatePersistentOverlayState(PartyEventType evt, int targetPlayer)
    {
        // Keep activeEventCornerPill permanently hidden to guarantee 100% board visibility!
        if (activeEventCornerPillBg != null)
            activeEventCornerPillBg.gameObject.SetActive(false);

        if (evt == PartyEventType.BountyHunt && targetPlayer >= 0)
        {
            if (screenEdgeFrame != null)
            {
                screenEdgeFrame.gameObject.SetActive(true);
                screenEdgeFrame.color = new Color(0.95f, 0.15f, 0.18f, 0.22f);
            }
        }
        else if (evt == PartyEventType.HomeRush)
        {
            if (screenEdgeFrame != null)
            {
                screenEdgeFrame.gameObject.SetActive(true);
                screenEdgeFrame.color = new Color(1f, 0.42f, 0.06f, 0.28f);
            }
        }
        else
        {
            if (screenEdgeFrame != null)
                screenEdgeFrame.gameObject.SetActive(false);
        }
    }

    private IEnumerator PlaySplashAnnouncementRoutine(PartyEventType evt, int targetPlayer)
    {
        if (splashBannerGroup == null || splashBannerRect == null)
            yield break;

        bool isBounty = evt == PartyEventType.BountyHunt;

        // Configure Banner Visuals & Text
        if (isBounty)
        {
            int safeTarget = Mathf.Clamp(targetPlayer, 0, 3);
            string pName = GetPlayerDisplayName(safeTarget);
            string cName = GetVietnameseColorName(safeTarget);
            string hexCol = ColorUtility.ToHtmlStringRGB(GetFactionColor(safeTarget));

            splashBannerBg.color = new Color(0.16f, 0.04f, 0.05f, 0.97f);
            splashBannerOutline.effectColor = CrimsonAlert;
            leftHazardBar.color = CrimsonAlert;
            rightHazardBar.color = GoldBright;

            leftEmblemImage.sprite = crosshairSprite;
            leftEmblemImage.color = CrimsonAlert;
            rightEmblemImage.sprite = crosshairSprite;
            rightEmblemImage.color = GoldBright;

            splashTagText.text = "BIẾN CỐ TOÀN BÀN • LỆNH TRUY NÃ ĐẶC BIỆT";
            splashTagText.color = GoldBright;

            splashTitleText.text = "TRUY NÃ KỲ THỦ DẪN ĐẦU!";
            splashTitleText.color = new Color(1f, 0.95f, 0.88f, 1f);

            splashSubtitleText.text = $"MỤC TIÊU: <color=#{hexCol}><b>{pName.ToUpper()} ({cName})</b></color>";
            splashRuleText.text = "Đá hạ quân mục tiêu: +2 Điểm Thưởng  |  Mục tiêu sống sót hết vòng: +2 Điểm";

            if (stingAudioSource != null && bountyStingClip != null)
                stingAudioSource.PlayOneShot(bountyStingClip, 0.92f);
        }
        else
        {
            splashBannerBg.color = new Color(0.20f, 0.07f, 0.02f, 0.97f);
            splashBannerOutline.effectColor = GoldBright;
            leftHazardBar.color = ColorBlazeOrange;
            rightHazardBar.color = GoldBright;

            leftEmblemImage.sprite = flameSprite;
            leftEmblemImage.color = GoldBright;
            rightEmblemImage.sprite = flameSprite;
            rightEmblemImage.color = ColorBlazeOrange;

            splashTagText.text = "BIẾN CỐ TOÀN BÀN • BÀN CỜ BỐC CHÁY";
            splashTagText.color = GoldBright;

            splashTitleText.text = "NƯỚC RÚT VỀ ĐÍCH RỰC LỬA!";
            splashTitleText.color = new Color(1f, 0.95f, 0.82f, 1f);

            splashSubtitleText.text = "<color=#FFD95A><b>ĐƯỜNG VỀ ĐÍCH & CỔNG CHECKPOINT ĐÃ RỰC CHÁY!</b></color>";
            splashRuleText.text = "Về Đích Đầu Tiên Trong Vòng: +2 Điểm  |  Vượt Cổng Checkpoint (Bước 28): Nhân Đôi Điểm (x2)";

            if (stingAudioSource != null && homeRushStingClip != null)
                stingAudioSource.PlayOneShot(homeRushStingClip, 0.95f);
        }

        // Phase 1 (0 -> 0.32s): Gentle non-glaring screen flash + OutBack Scale-in
        Color flashColor = isBounty
            ? new Color(1f, 0.16f, 0.20f, 0.22f)
            : new Color(1f, 0.44f, 0.08f, 0.24f);

        float inDuration = 0.34f;
        float elapsed = 0f;
        splashBannerGroup.alpha = 1f;

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inDuration);

            // OutBack easing
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float scale = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            splashBannerRect.localScale = new Vector3(scale, scale, 1f);

            if (screenFlashOverlay != null)
            {
                float flashAlpha = Mathf.Lerp(flashColor.a, 0f, Mathf.Clamp01(elapsed / 0.22f));
                screenFlashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            }

            yield return null;
        }

        splashBannerRect.localScale = Vector3.one;
        if (screenFlashOverlay != null)
            screenFlashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        // Phase 2 (0.34s -> 2.15s): Hold with lively emblem animation & subtle pulse
        float holdDuration = 1.85f;
        elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = 1f + Mathf.Sin(elapsed * 8f) * 0.022f;
            splashBannerRect.localScale = new Vector3(pulse, pulse, 1f);

            if (isBounty)
            {
                float rot = elapsed * 140f;
                leftEmblemImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
                rightEmblemImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -rot);
            }
            else
            {
                float flameScale = 1f + Mathf.Sin(elapsed * 16f) * 0.14f;
                leftEmblemImage.rectTransform.localScale = new Vector3(1f, flameScale, 1f);
                rightEmblemImage.rectTransform.localScale = new Vector3(1f, flameScale, 1f);
                leftEmblemImage.rectTransform.localRotation = Quaternion.identity;
                rightEmblemImage.rectTransform.localRotation = Quaternion.identity;
            }

            yield return null;
        }

        // Phase 3 (2.15s -> 2.40s): Smooth scale & fade out
        float outDuration = 0.24f;
        elapsed = 0f;
        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            splashBannerGroup.alpha = 1f - t;
            float s = Mathf.Lerp(1f, 0.86f, t);
            splashBannerRect.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        splashBannerGroup.alpha = 0f;
        splashRoutine = null;
    }

    // =========================================================================
    // LIVE FRAME ANIMATION FOR 3D RETICLES & BLAZING BOARD
    // =========================================================================
    private void AnimateActiveEventVisuals()
    {
        if (board == null)
            return;

        float t = Time.time;
        PartyEventType evt = board.activeEvent;
        Camera mainCam = Camera.main;

        // 1. Animate Bounty Hunt 3D Target Reticles & Spotlight Pillar on hunted player's pieces
        bool bountyActive = evt == PartyEventType.BountyHunt && board.bountyTargetPlayer >= 0;
        for (int i = 0; i < bountyRigs.Count; i++)
        {
            BountyPieceRig rig = bountyRigs[i];
            if (rig == null || rig.piece == null || rig.root == null)
                continue;

            bool shouldShow = bountyActive &&
                              rig.piece.gameObject.activeInHierarchy &&
                              rig.piece.playerIndex == board.bountyTargetPlayer &&
                              !rig.piece.isFinished;

            if (rig.root.activeSelf != shouldShow)
            {
                rig.root.SetActive(shouldShow);
                if (rig.sparksPs != null)
                {
                    if (shouldShow) rig.sparksPs.Play();
                    else rig.sparksPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (!shouldShow)
                continue;

            Vector3 pPos = rig.piece.transform.position;
            rig.root.transform.position = new Vector3(pPos.x, pPos.y + 0.008f, pPos.z);

            float pulse = 1f + Mathf.Sin(t * 5.5f + i * 0.7f) * 0.08f;
            if (rig.darkBackingRing != null)
            {
                rig.darkBackingRing.transform.localScale = Vector3.one * pulse;
            }
            if (rig.outerRing != null)
            {
                rig.outerRing.transform.localRotation = Quaternion.Euler(0f, t * 95f, 0f);
                rig.outerRing.transform.localScale = Vector3.one * pulse;
            }
            if (rig.crosshairTicksGroup != null)
            {
                rig.crosshairTicksGroup.localRotation = Quaternion.Euler(0f, t * 95f, 0f);
                rig.crosshairTicksGroup.localScale = Vector3.one * pulse;
            }
            if (rig.innerCrosshair != null)
            {
                rig.innerCrosshair.transform.localRotation = Quaternion.Euler(0f, -t * 80f, 0f);
            }
            if (rig.spotlightBeam != null)
            {
                rig.spotlightBeam.localRotation = Quaternion.Euler(0f, t * 32f + i * 45f, 0f);
                float beamBreath = 1f + Mathf.Sin(t * 4.5f + i) * 0.045f;
                rig.spotlightBeam.localScale = new Vector3(beamBreath, 1f, beamBreath);
            }
            if (rig.spotlightDisc != null)
            {
                float discPulse = 1f + Mathf.Sin(t * 5.2f + i * 0.9f) * 0.08f;
                rig.spotlightDisc.localScale = Vector3.one * discPulse;
            }
            if (rig.spotLight != null)
            {
                float spotPulse = 0.5f + 0.5f * Mathf.Sin(t * 5.2f + i * 0.9f);
                rig.spotLight.intensity = Mathf.Lerp(1.90f, 2.55f, spotPulse);
            }
            if (rig.diamondMarker != null)
            {
                float diamondBob = 0.285f + Mathf.Sin(t * 4.8f + i) * 0.016f;
                rig.diamondMarker.localPosition = new Vector3(0f, diamondBob, 0f);
                rig.diamondMarker.localRotation = Quaternion.Euler(0f, t * 140f, 0f);
            }
            if (rig.floatingTag != null)
            {
                float bobY = 0.355f + Mathf.Sin(t * 4.8f + i) * 0.015f;
                rig.floatingTag.transform.position = new Vector3(pPos.x, pPos.y + bobY, pPos.z);
                if (mainCam != null)
                    rig.floatingTag.transform.rotation = mainCam.transform.rotation;
            }
        }

        // 2. Animate Home Rush Subtle Corner Firelights & Sequential Rush Wave
        if (evt == PartyEventType.HomeRush && homeRushRoot != null && homeRushRoot.activeSelf)
        {
            for (int i = 0; i < homeRushFireLights.Count; i++)
            {
                Light l = homeRushFireLights[i];
                if (l == null) continue;
                float noise = Mathf.PerlinNoise(t * 7.5f, i * 3.7f);
                l.intensity = Mathf.Lerp(0.55f, 1.05f, noise);
                l.color = Color.Lerp(new Color(1f, 0.40f, 0.08f, 1f), new Color(1f, 0.70f, 0.20f, 1f), noise);
            }

            // Sequential inward fire wave across the 6 home-path steps (step 0 -> step 5)
            for (int s = 0; s < rushStepMats.Length; s++)
            {
                Material stepMat = rushStepMats[s];
                if (stepMat == null) continue;

                float phase = Mathf.Sin(t * 4.2f - s * 0.85f) * 0.5f + 0.5f;
                float wave = Mathf.Pow(phase, 2.2f);
                Color waveCol = Color.Lerp(
                    new Color(0.88f, 0.24f, 0.04f, 0.72f),
                    new Color(1.00f, 0.82f, 0.24f, 0.98f),
                    wave
                );
                SetMatColor(stepMat, waveCol);
            }

            for (int i = 0; i < homeRushRings.Count; i++)
            {
                RushRingEntry entry = homeRushRings[i];
                if (entry == null || entry.ring == null) continue;

                float phase = Mathf.Sin(t * 4.2f - entry.stepIndex * 0.85f) * 0.5f + 0.5f;
                float wave = Mathf.Pow(phase, 2.2f);
                entry.ring.transform.localScale = Vector3.one * (1.0f + wave * 0.12f);
            }

            for (int i = 0; i < homeRushFloatingLabels.Count; i++)
            {
                Transform tr = homeRushFloatingLabels[i];
                if (tr == null) continue;
                float s = 1f + Mathf.Sin(t * 5.2f + i * 0.8f) * 0.06f;
                tr.localScale = Vector3.one * (0.18f * s);
                if (mainCam != null)
                    tr.rotation = mainCam.transform.rotation;
            }
        }

        // 3. Subtle Non-Glaring Screen Edge Vignette Pulse
        if (screenEdgeFrame != null && screenEdgeFrame.gameObject.activeSelf)
        {
            float pulseAlpha = 0.10f + (Mathf.Sin(Time.unscaledTime * 4.0f) + 1f) * 0.5f * 0.10f;
            Color baseCol = evt == PartyEventType.BountyHunt
                ? new Color(0.95f, 0.14f, 0.18f, pulseAlpha)
                : new Color(1.00f, 0.42f, 0.06f, pulseAlpha);
            screenEdgeFrame.color = baseCol;
        }
    }

    // =========================================================================
    // BUILD 3D BOUNTY HUNT TARGET RETICLES & SPOTLIGHT (POOLED PER PIECE)
    // =========================================================================
    private void BuildBountyPieceRigs()
    {
        foreach (BountyPieceRig old in bountyRigs)
        {
            if (old?.root != null)
                Destroy(old.root);
        }
        bountyRigs.Clear();

        if (board == null || board.allPieces == null)
            return;

        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        TMP_FontAsset sharedFont = GetSharedTmpFont();

        foreach (PieceController piece in board.allPieces)
        {
            if (piece == null)
                continue;

            GameObject rigRoot = new GameObject($"BountyRig_{piece.name}");
            if (ignoreLayer >= 0) rigRoot.layer = ignoreLayer;
            rigRoot.transform.SetParent(transform, false);
            rigRoot.transform.position = piece.transform.position;

            // 1) Dark Contrast Backing Ring (radius 0.095m) so reticle pops on ANY tile/red chuồng
            GameObject backingObj = new GameObject("DarkContrastRing");
            if (ignoreLayer >= 0) backingObj.layer = ignoreLayer;
            backingObj.transform.SetParent(rigRoot.transform, false);
            backingObj.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            LineRenderer backingLr = backingObj.AddComponent<LineRenderer>();
            ConfigureCircleLineRenderer(backingLr, 32, 0.095f, 0.017f, bountyDarkBackingMat);

            // 2) Outer Spinning Crimson Target Ring (radius 0.095m)
            GameObject outerObj = new GameObject("OuterTargetRing");
            if (ignoreLayer >= 0) outerObj.layer = ignoreLayer;
            outerObj.transform.SetParent(rigRoot.transform, false);
            outerObj.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            LineRenderer outerLr = outerObj.AddComponent<LineRenderer>();
            ConfigureCircleLineRenderer(outerLr, 32, 0.095f, 0.0085f, bountyRingMat);

            // 3) Inner Gold Crosshair Octagon Ring (radius 0.068m)
            GameObject innerObj = new GameObject("InnerCrosshairRing");
            if (ignoreLayer >= 0) innerObj.layer = ignoreLayer;
            innerObj.transform.SetParent(rigRoot.transform, false);
            innerObj.transform.localPosition = new Vector3(0f, 0.007f, 0f);
            LineRenderer innerLr = innerObj.AddComponent<LineRenderer>();
            ConfigureCircleLineRenderer(innerLr, 8, 0.068f, 0.0065f, bountyInnerMat);

            // 4) 4 Radial Tactical Scope Ticks around perimeter
            GameObject ticksGroup = new GameObject("CrosshairTicksGroup");
            if (ignoreLayer >= 0) ticksGroup.layer = ignoreLayer;
            ticksGroup.transform.SetParent(rigRoot.transform, false);
            ticksGroup.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            for (int k = 0; k < 4; k++)
            {
                float rad = k * Mathf.PI * 0.5f;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                GameObject tickObj = new GameObject($"ScopeTick_{k}");
                if (ignoreLayer >= 0) tickObj.layer = ignoreLayer;
                tickObj.transform.SetParent(ticksGroup.transform, false);
                LineRenderer tLr = tickObj.AddComponent<LineRenderer>();
                tLr.useWorldSpace = false;
                tLr.loop = false;
                tLr.positionCount = 2;
                tLr.widthMultiplier = 0.007f;
                tLr.sharedMaterial = bountyInnerMat;
                tLr.shadowCastingMode = ShadowCastingMode.Off;
                tLr.receiveShadows = false;
                tLr.SetPosition(0, dir * 0.064f);
                tLr.SetPosition(1, dir * 0.118f);
            }

            // 5) Translucent Inverted Spotlight Cone Pillar + Ground Spotlight Pool Disc
            GameObject beamObj = new GameObject("BountySpotlightBeam", typeof(MeshFilter), typeof(MeshRenderer));
            if (ignoreLayer >= 0) beamObj.layer = ignoreLayer;
            beamObj.transform.SetParent(rigRoot.transform, false);
            beamObj.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            beamObj.GetComponent<MeshFilter>().sharedMesh = spotlightConeMesh;
            MeshRenderer beamMr = beamObj.GetComponent<MeshRenderer>();
            beamMr.sharedMaterial = bountyBeamMat;
            beamMr.shadowCastingMode = ShadowCastingMode.Off;
            beamMr.receiveShadows = false;

            GameObject discObj = new GameObject("BountySpotlightDisc", typeof(MeshFilter), typeof(MeshRenderer));
            if (ignoreLayer >= 0) discObj.layer = ignoreLayer;
            discObj.transform.SetParent(rigRoot.transform, false);
            discObj.transform.localPosition = new Vector3(0f, 0.001f, 0f);
            discObj.GetComponent<MeshFilter>().sharedMesh = spotlightDiscMesh;
            MeshRenderer discMr = discObj.GetComponent<MeshRenderer>();
            discMr.sharedMaterial = bountySpotDiscMat;
            discMr.shadowCastingMode = ShadowCastingMode.Off;
            discMr.receiveShadows = false;

            // 6) Focused Overhead Warm Stage SpotLight illuminating only the hunted piece (<= 4 total per URP cap)
            GameObject spotObj = new GameObject("BountySpotLight");
            spotObj.transform.SetParent(rigRoot.transform, false);
            spotObj.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            spotObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light spotLight = spotObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.spotAngle = 32f;
            spotLight.innerSpotAngle = 16f;
            spotLight.range = 0.95f;
            spotLight.intensity = 2.15f;
            spotLight.color = new Color(1.00f, 0.78f, 0.48f, 1f);
            spotLight.shadows = LightShadows.None;

            // 7) Spinning 3D Target Diamond above piece head
            GameObject diamondObj = new GameObject("BountyDiamondMarker", typeof(MeshFilter), typeof(MeshRenderer));
            if (ignoreLayer >= 0) diamondObj.layer = ignoreLayer;
            diamondObj.transform.SetParent(rigRoot.transform, false);
            diamondObj.transform.localPosition = new Vector3(0f, 0.295f, 0f);
            diamondObj.transform.localScale = Vector3.one * 0.046f;
            diamondObj.GetComponent<MeshFilter>().sharedMesh = targetDiamondMesh;
            MeshRenderer diaMr = diamondObj.GetComponent<MeshRenderer>();
            diaMr.sharedMaterial = bountyDiamondMat;
            diaMr.shadowCastingMode = ShadowCastingMode.Off;
            diaMr.receiveShadows = false;

            // 8) Crisp Star-Ember Hunter Sparks ParticleSystem
            GameObject psObj = new GameObject("BountySparksPS");
            if (ignoreLayer >= 0) psObj.layer = ignoreLayer;
            psObj.transform.SetParent(rigRoot.transform, false);
            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
            ConfigureFireOrSparkEmitter(
                ps,
                isFlameWall: false,
                boxExtents: new Vector3(0.068f, 0.01f, 0.068f),
                rateOverTime: 14f,
                startSizeMin: 0.016f,
                startSizeMax: 0.030f,
                lifetimeMin: 0.40f,
                lifetimeMax: 0.72f,
                upSpeedMin: 0.22f,
                upSpeedMax: 0.40f,
                coreColor: new Color(1f, 0.92f, 0.42f, 0.98f),
                midColor: new Color(1f, 0.24f, 0.22f, 0.92f),
                tipColor: new Color(0.80f, 0.06f, 0.10f, 0f)
            );

            // 9) Compact Collision-Free Floating 3D "TRUY NÃ" Label above Diamond & Piece Head
            GameObject tagObj = new GameObject("WantedFloatingTag");
            if (ignoreLayer >= 0) tagObj.layer = ignoreLayer;
            tagObj.transform.SetParent(rigRoot.transform, false);
            tagObj.transform.localScale = Vector3.one * 0.17f;
            TextMeshPro tmp = tagObj.AddComponent<TextMeshPro>();
            if (sharedFont != null) tmp.font = sharedFont;
            if (tmpOverlayMat != null) tmp.fontSharedMaterial = tmpOverlayMat;
            tmp.text = "TRUY NÃ";
            tmp.fontSize = 1.85f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = new Color(1f, 0.90f, 0.36f, 1f);

            MeshRenderer mr = tagObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sortingOrder = 25;
            }

            rigRoot.SetActive(false);

            bountyRigs.Add(new BountyPieceRig
            {
                piece = piece,
                root = rigRoot,
                darkBackingRing = backingLr,
                outerRing = outerLr,
                innerCrosshair = innerLr,
                crosshairTicksGroup = ticksGroup.transform,
                spotlightBeam = beamObj.transform,
                spotlightDisc = discObj.transform,
                spotLight = spotLight,
                diamondMarker = diamondObj.transform,
                sparksPs = ps,
                floatingTag = tmp
            });
        }
    }

    // =========================================================================
    // BUILD 3D HOME RUSH "BLAZING BOARD" (BÀN CỜ BỐC CHÁY) RIG
    // =========================================================================
    private void BuildHomeRushBlazingBoardRig()
    {
        if (homeRushRoot != null)
            Destroy(homeRushRoot);

        homeRushParticleSystems.Clear();
        homeRushRings.Clear();
        homeRushFireLights.Clear();
        homeRushFloatingLabels.Clear();

        if (board == null || board.pathPoints == null || board.pathPoints.Length == 0)
            return;

        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        homeRushRoot = new GameObject("HomeRushBlazingBoardRoot");
        if (ignoreLayer >= 0) homeRushRoot.layer = ignoreLayer;
        homeRushRoot.transform.SetParent(transform, false);

        // Compute playing surface bounds from pathPoints
        Bounds tileBounds = new Bounds(board.pathPoints[0].position, Vector3.zero);
        for (int i = 0; i < board.pathPoints.Length; i++)
        {
            if (board.pathPoints[i] != null)
                tileBounds.Encapsulate(board.pathPoints[i].position);
        }

        // Align perimeter fire walls right onto the wooden rim of "ludo_board" if present
        Bounds rimBounds = tileBounds;
        bool foundBoardRim = false;
        GameObject ludoBoardObj = GameObject.Find("ludo_board");
        if (ludoBoardObj != null)
        {
            Renderer[] rends = ludoBoardObj.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                rimBounds = rends[0].bounds;
                for (int r = 1; r < rends.Length; r++)
                    rimBounds.Encapsulate(rends[r].bounds);
                foundBoardRim = true;
            }
        }

        Vector3 center = foundBoardRim ? rimBounds.center : tileBounds.center;
        float halfX = foundBoardRim ? Mathf.Max(0.5f, rimBounds.extents.x - 0.045f) : (tileBounds.extents.x + 0.14f);
        float halfZ = foundBoardRim ? Mathf.Max(0.5f, rimBounds.extents.z - 0.045f) : (tileBounds.extents.z + 0.14f);
        float rimTopY = foundBoardRim ? (rimBounds.max.y - 0.015f) : (tileBounds.min.y + 0.04f);
        float tileSurfaceY = tileBounds.min.y + 0.016f;

        // 1. 4 Rim Fire Walls (sculpted dancing flame tongues sitting cleanly along wooden rim)
        Vector3[] edgePositions =
        {
            new Vector3(center.x, rimTopY, center.z + halfZ), // North rim
            new Vector3(center.x, rimTopY, center.z - halfZ), // South rim
            new Vector3(center.x + halfX, rimTopY, center.z), // East rim
            new Vector3(center.x - halfX, rimTopY, center.z)  // West rim
        };
        Vector3[] edgeBoxExtents =
        {
            new Vector3(halfX * 0.94f, 0.012f, 0.028f),
            new Vector3(halfX * 0.94f, 0.012f, 0.028f),
            new Vector3(0.028f, 0.012f, halfZ * 0.94f),
            new Vector3(0.028f, 0.012f, halfZ * 0.94f)
        };

        for (int e = 0; e < 4; e++)
        {
            // Main Flame Tongue Wall (continuous dancing orange-amber fire curtain hugging rim without glaring bloom)
            GameObject edgeFlameObj = new GameObject($"BoardPerimeterFlame_{e}");
            if (ignoreLayer >= 0) edgeFlameObj.layer = ignoreLayer;
            edgeFlameObj.transform.SetParent(homeRushRoot.transform, false);
            edgeFlameObj.transform.position = edgePositions[e];

            ParticleSystem flamePs = edgeFlameObj.AddComponent<ParticleSystem>();
            ConfigureFireOrSparkEmitter(
                flamePs,
                isFlameWall: true,
                boxExtents: edgeBoxExtents[e],
                rateOverTime: 88f,
                startSizeMin: 0.085f,
                startSizeMax: 0.155f,
                lifetimeMin: 0.44f,
                lifetimeMax: 0.76f,
                upSpeedMin: 0.20f,
                upSpeedMax: 0.38f,
                coreColor: new Color(1.00f, 0.82f, 0.28f, 0.90f),
                midColor: new Color(1.00f, 0.34f, 0.04f, 0.85f),
                tipColor: new Color(0.55f, 0.05f, 0.01f, 0f)
            );
            homeRushParticleSystems.Add(flamePs);

            // Crisp Rising Star-Embers along rim
            GameObject edgeSparksObj = new GameObject($"BoardPerimeterEmbers_{e}");
            if (ignoreLayer >= 0) edgeSparksObj.layer = ignoreLayer;
            edgeSparksObj.transform.SetParent(homeRushRoot.transform, false);
            edgeSparksObj.transform.position = edgePositions[e];

            ParticleSystem emberPs = edgeSparksObj.AddComponent<ParticleSystem>();
            ConfigureFireOrSparkEmitter(
                emberPs,
                isFlameWall: false,
                boxExtents: edgeBoxExtents[e],
                rateOverTime: 24f,
                startSizeMin: 0.015f,
                startSizeMax: 0.028f,
                lifetimeMin: 0.55f,
                lifetimeMax: 1.05f,
                upSpeedMin: 0.32f,
                upSpeedMax: 0.65f,
                coreColor: new Color(1.00f, 0.92f, 0.45f, 0.96f),
                midColor: new Color(1.00f, 0.50f, 0.08f, 0.88f),
                tipColor: new Color(0.92f, 0.18f, 0.02f, 0f)
            );
            homeRushParticleSystems.Add(emberPs);
        }

        // 2. 4 Corner Braziers on the wooden rim corners
        Vector3[] cornerPositions =
        {
            new Vector3(center.x - halfX, rimTopY + 0.005f, center.z - halfZ),
            new Vector3(center.x - halfX, rimTopY + 0.005f, center.z + halfZ),
            new Vector3(center.x + halfX, rimTopY + 0.005f, center.z - halfZ),
            new Vector3(center.x + halfX, rimTopY + 0.005f, center.z + halfZ)
        };
        for (int c = 0; c < 4; c++)
        {
            GameObject cornerTorch = new GameObject($"CornerBonfire_{c}");
            if (ignoreLayer >= 0) cornerTorch.layer = ignoreLayer;
            cornerTorch.transform.SetParent(homeRushRoot.transform, false);
            cornerTorch.transform.position = cornerPositions[c];

            ParticleSystem cornerPs = cornerTorch.AddComponent<ParticleSystem>();
            ConfigureFireOrSparkEmitter(
                cornerPs,
                isFlameWall: true,
                boxExtents: new Vector3(0.048f, 0.012f, 0.048f),
                rateOverTime: 36f,
                startSizeMin: 0.115f,
                startSizeMax: 0.205f,
                lifetimeMin: 0.48f,
                lifetimeMax: 0.86f,
                upSpeedMin: 0.26f,
                upSpeedMax: 0.52f,
                coreColor: new Color(1.00f, 0.86f, 0.34f, 0.92f),
                midColor: new Color(1.00f, 0.36f, 0.05f, 0.86f),
                tipColor: new Color(0.62f, 0.07f, 0.02f, 0f)
            );
            homeRushParticleSystems.Add(cornerPs);
        }

        // 3. Sequential Running Fire Wave along all 4 Home Paths (Đường Về Đích)
        for (int p = 0; p < 4; p++)
        {
            Transform[] homePath = board.GetHomePath(p);
            if (homePath == null)
                continue;

            for (int h = 0; h < homePath.Length; h++)
            {
                if (homePath[h] == null)
                    continue;

                Vector3 tilePos = new Vector3(homePath[h].position.x, Mathf.Max(homePath[h].position.y + 0.008f, tileSurfaceY), homePath[h].position.z);
                int stepIdx = Mathf.Clamp(h, 0, rushStepMats.Length - 1);

                // Pulsing sequential wave ring framing each home tile clearly without masking printed numbers
                GameObject ringObj = new GameObject($"RushHomeRing_{p}_{h}");
                if (ignoreLayer >= 0) ringObj.layer = ignoreLayer;
                ringObj.transform.SetParent(homeRushRoot.transform, false);
                ringObj.transform.position = tilePos;
                LineRenderer lr = ringObj.AddComponent<LineRenderer>();
                ConfigureCircleLineRenderer(lr, 24, 0.048f, 0.0072f, rushStepMats[stepIdx]);
                homeRushRings.Add(new RushRingEntry { ring = lr, stepIndex = stepIdx });

                // Gentle flame wisps only on entrance (0) and deepest finish tile (5)
                if (h == 0 || h == homePath.Length - 1)
                {
                    GameObject tileFlame = new GameObject($"HomePathFlame_{p}_{h}");
                    if (ignoreLayer >= 0) tileFlame.layer = ignoreLayer;
                    tileFlame.transform.SetParent(homeRushRoot.transform, false);
                    tileFlame.transform.position = tilePos;

                    ParticleSystem hPs = tileFlame.AddComponent<ParticleSystem>();
                    ConfigureFireOrSparkEmitter(
                        hPs,
                        isFlameWall: true,
                        boxExtents: new Vector3(0.018f, 0.008f, 0.018f),
                        rateOverTime: h == homePath.Length - 1 ? 14f : 8f,
                        startSizeMin: 0.036f,
                        startSizeMax: 0.064f,
                        lifetimeMin: 0.28f,
                        lifetimeMax: 0.52f,
                        upSpeedMin: 0.14f,
                        upSpeedMax: 0.26f,
                        coreColor: new Color(1.00f, 0.82f, 0.30f, 0.76f),
                        midColor: new Color(1.00f, 0.42f, 0.06f, 0.70f),
                        tipColor: new Color(0.75f, 0.10f, 0.02f, 0f)
                    );
                    homeRushParticleSystems.Add(hPs);
                }
            }
        }

        // Center Goal Golden Ring + Trophy Sparks + SINGLE Clean Center Badge ("ĐÍCH +2đ")
        Vector3 goalCenterPos = new Vector3(tileBounds.center.x, tileSurfaceY + 0.006f, tileBounds.center.z);
        GameObject goalRingObj = new GameObject("CenterGoalRing");
        if (ignoreLayer >= 0) goalRingObj.layer = ignoreLayer;
        goalRingObj.transform.SetParent(homeRushRoot.transform, false);
        goalRingObj.transform.position = goalCenterPos;
        LineRenderer goalLr = goalRingObj.AddComponent<LineRenderer>();
        ConfigureCircleLineRenderer(goalLr, 32, 0.095f, 0.0075f, rushStepMats[5]);
        homeRushRings.Add(new RushRingEntry { ring = goalLr, stepIndex = 5 });

        GameObject centerTrophyFlame = new GameObject("CenterGoalTrophySparks");
        if (ignoreLayer >= 0) centerTrophyFlame.layer = ignoreLayer;
        centerTrophyFlame.transform.SetParent(homeRushRoot.transform, false);
        centerTrophyFlame.transform.position = goalCenterPos;
        ParticleSystem centerPs = centerTrophyFlame.AddComponent<ParticleSystem>();
        ConfigureFireOrSparkEmitter(
            centerPs,
            isFlameWall: false,
            boxExtents: new Vector3(0.060f, 0.01f, 0.060f),
            rateOverTime: 22f,
            startSizeMin: 0.016f,
            startSizeMax: 0.032f,
            lifetimeMin: 0.45f,
            lifetimeMax: 0.85f,
            upSpeedMin: 0.20f,
            upSpeedMax: 0.40f,
            coreColor: new Color(1.00f, 0.92f, 0.48f, 0.95f),
            midColor: new Color(1.00f, 0.52f, 0.10f, 0.85f),
            tipColor: new Color(0.90f, 0.18f, 0.02f, 0f)
        );
        homeRushParticleSystems.Add(centerPs);

        GameObject goalBadge = Create3DFlatOrBillboardLabel(
            homeRushRoot.transform,
            "CenterGoalBonusTag",
            goalCenterPos + new Vector3(0f, 0.075f, 0f),
            "ĐÍCH +2đ",
            new Color(1f, 0.92f, 0.42f, 1f),
            ignoreLayer
        );
        homeRushFloatingLabels.Add(goalBadge.transform);

        // 4. 4 Blazing Checkpoint Gates (indices 0, 14, 28, 42) -> Ring + Crisp Embers + compact "x2" badge
        int[] gateIndices = { 0, 14, 28, 42 };
        foreach (int gateIdx in gateIndices)
        {
            if (gateIdx < 0 || gateIdx >= board.pathPoints.Length || board.pathPoints[gateIdx] == null)
                continue;

            Vector3 gatePos = new Vector3(board.pathPoints[gateIdx].position.x, Mathf.Max(board.pathPoints[gateIdx].position.y + 0.008f, tileSurfaceY), board.pathPoints[gateIdx].position.z);

            GameObject chkRingObj = new GameObject($"CheckpointRing_{gateIdx}");
            if (ignoreLayer >= 0) chkRingObj.layer = ignoreLayer;
            chkRingObj.transform.SetParent(homeRushRoot.transform, false);
            chkRingObj.transform.position = gatePos;
            LineRenderer chkLr = chkRingObj.AddComponent<LineRenderer>();
            ConfigureCircleLineRenderer(chkLr, 24, 0.056f, 0.0068f, rushStepMats[5]);
            homeRushRings.Add(new RushRingEntry { ring = chkLr, stepIndex = 5 });

            GameObject gateFlame = new GameObject($"CheckpointSparks_{gateIdx}");
            if (ignoreLayer >= 0) gateFlame.layer = ignoreLayer;
            gateFlame.transform.SetParent(homeRushRoot.transform, false);
            gateFlame.transform.position = gatePos;

            ParticleSystem gPs = gateFlame.AddComponent<ParticleSystem>();
            ConfigureFireOrSparkEmitter(
                gPs,
                isFlameWall: false,
                boxExtents: new Vector3(0.036f, 0.008f, 0.036f),
                rateOverTime: 14f,
                startSizeMin: 0.015f,
                startSizeMax: 0.028f,
                lifetimeMin: 0.35f,
                lifetimeMax: 0.65f,
                upSpeedMin: 0.16f,
                upSpeedMax: 0.32f,
                coreColor: new Color(1.00f, 0.92f, 0.45f, 0.95f),
                midColor: new Color(1.00f, 0.46f, 0.08f, 0.88f),
                tipColor: new Color(0.85f, 0.14f, 0.02f, 0f)
            );
            homeRushParticleSystems.Add(gPs);

            GameObject chkLabel = Create3DFlatOrBillboardLabel(
                homeRushRoot.transform,
                $"CheckpointX2Tag_{gateIdx}",
                gatePos + new Vector3(0f, 0.075f, 0f),
                "x2",
                new Color(1f, 0.90f, 0.38f, 1f),
                ignoreLayer
            );
            homeRushFloatingLabels.Add(chkLabel.transform);
        }

        // 5. 4 Controlled Warm Corner Firelights (kissing wooden rim — NO center washout!)
        Vector3[] lightOffsets =
        {
            new Vector3(-halfX * 0.92f, 0.24f, -halfZ * 0.92f),
            new Vector3(halfX * 0.92f, 0.24f, -halfZ * 0.92f),
            new Vector3(-halfX * 0.92f, 0.24f, halfZ * 0.92f),
            new Vector3(halfX * 0.92f, 0.24f, halfZ * 0.92f)
        };
        for (int i = 0; i < lightOffsets.Length; i++)
        {
            GameObject lightObj = new GameObject($"HomeRushFireLight_{i}");
            lightObj.transform.SetParent(homeRushRoot.transform, false);
            lightObj.transform.position = new Vector3(center.x + lightOffsets[i].x, rimTopY + lightOffsets[i].y, center.z + lightOffsets[i].z);

            Light ptLight = lightObj.AddComponent<Light>();
            ptLight.type = LightType.Point;
            ptLight.range = 1.25f;
            ptLight.intensity = 0.80f;
            ptLight.color = new Color(1f, 0.46f, 0.12f, 1f);
            ptLight.shadows = LightShadows.None;
            homeRushFireLights.Add(ptLight);
        }

        homeRushRoot.SetActive(false);
    }

    private GameObject Create3DFlatOrBillboardLabel(Transform parent, string name, Vector3 worldPos, string text, Color color, int layer)
    {
        GameObject go = new GameObject(name);
        if (layer >= 0) go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        if (Camera.main != null)
            go.transform.rotation = Camera.main.transform.rotation;
        else
            go.transform.rotation = Quaternion.Euler(78f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.18f;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        TMP_FontAsset sharedFont = GetSharedTmpFont();
        if (sharedFont != null) tmp.font = sharedFont;
        if (tmpOverlayMat != null) tmp.fontSharedMaterial = tmpOverlayMat;
        tmp.text = text;
        tmp.fontSize = 2.0f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = color;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 22;
        }
        return go;
    }

    private void ConfigureCircleLineRenderer(LineRenderer lr, int segments, float radius, float width, Material mat)
    {
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = segments;
        lr.widthMultiplier = width;
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private void ConfigureFireOrSparkEmitter(
        ParticleSystem ps,
        bool isFlameWall,
        Vector3 boxExtents,
        float rateOverTime,
        float startSizeMin,
        float startSizeMax,
        float lifetimeMin,
        float lifetimeMax,
        float upSpeedMin,
        float upSpeedMax,
        Color coreColor,
        Color midColor,
        Color tipColor)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
        main.startRotation = isFlameWall
            ? new ParticleSystem.MinMaxCurve(-0.22f, 0.22f)
            : new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = isFlameWall ? 180 : 60;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rateOverTime;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxExtents * 2f;

        // Rule from vfx.md: set ALL THREE AXES in the exact same curve mode (TwoConstants) to avoid Unity curve mode errors!
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
        vel.y = new ParticleSystem.MinMaxCurve(upSpeedMin, upSpeedMax);
        vel.z = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);

        // Organic flame lick curl
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(isFlameWall ? 0.05f : 0.08f);
        noise.frequency = isFlameWall ? 2.2f : 1.6f;
        noise.scrollSpeed = isFlameWall ? 1.6f : 1.2f;
        noise.damping = true;
        noise.octaveCount = 2;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(coreColor, 0f),
                new GradientColorKey(midColor, 0.45f),
                new GradientColorKey(tipColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(coreColor.a, 0.16f),
                new GradientAlphaKey(midColor.a, 0.56f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.22f, 1.0f),
            new Keyframe(1f, 0.0f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = isFlameWall ? flameParticleMat : sparkParticleMat;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.sortingOrder = isFlameWall ? 18 : 21;
        }
    }

    // =========================================================================
    // UI OVERLAY CANVAS: SPLASH BANNER & PERSISTENT EVENT STRIP
    // =========================================================================
    private void BuildOverlayCanvas()
    {
        if (eventCanvas != null)
            return;

        GameObject canvasObj = new GameObject("PartyEventOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        eventCanvas = canvasObj.GetComponent<Canvas>();
        eventCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        eventCanvas.sortingOrder = 915;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        // 1) Screen-edge glowing vignette frame (non-blocking)
        GameObject edgeObj = new GameObject("ScreenEdgeEventVignette", typeof(RectTransform), typeof(Image));
        edgeObj.transform.SetParent(canvasObj.transform, false);
        RectTransform edgeRt = edgeObj.GetComponent<RectTransform>();
        edgeRt.anchorMin = Vector2.zero;
        edgeRt.anchorMax = Vector2.one;
        edgeRt.offsetMin = Vector2.zero;
        edgeRt.offsetMax = Vector2.zero;
        screenEdgeFrame = edgeObj.GetComponent<Image>();
        screenEdgeFrame.sprite = CreateEdgeVignetteSprite(128);
        screenEdgeFrame.type = Image.Type.Simple;
        screenEdgeFrame.raycastTarget = false;
        screenEdgeFrame.gameObject.SetActive(false);

        // 2) Fullscreen Flash Overlay on event start
        GameObject flashObj = new GameObject("ScreenFlashOverlay", typeof(RectTransform), typeof(Image));
        flashObj.transform.SetParent(canvasObj.transform, false);
        RectTransform flashRt = flashObj.GetComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.offsetMin = Vector2.zero;
        flashRt.offsetMax = Vector2.zero;
        screenFlashOverlay = flashObj.GetComponent<Image>();
        screenFlashOverlay.color = new Color(0f, 0f, 0f, 0f);
        screenFlashOverlay.raycastTarget = false;

        // 3) Persistent Active Event Pill just below Top-Center Royal Banner
        GameObject pillObj = new GameObject("ActiveEventPersistentPill", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
        pillObj.transform.SetParent(canvasObj.transform, false);
        RectTransform pillRt = pillObj.GetComponent<RectTransform>();
        pillRt.anchorMin = new Vector2(0.5f, 1f);
        pillRt.anchorMax = new Vector2(0.5f, 1f);
        pillRt.pivot = new Vector2(0.5f, 1f);
        pillRt.anchoredPosition = new Vector2(20f, -124f);
        pillRt.sizeDelta = new Vector2(640f, 30f);

        activeEventCornerPillBg = pillObj.GetComponent<Image>();
        activeEventCornerPillBg.color = new Color(0.58f, 0.08f, 0.12f, 0.96f);
        activeEventCornerPillBg.raycastTarget = false;

        Outline pillOut = pillObj.GetComponent<Outline>();
        pillOut.effectColor = GoldBright;
        pillOut.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow pillSh = pillObj.GetComponent<Shadow>();
        pillSh.effectColor = new Color(0f, 0f, 0f, 0.65f);
        pillSh.effectDistance = new Vector2(0f, -3f);

        activeEventCornerPillText = CreateUiLabel(pillObj.transform, "PillText", "", 13.5f, GoldBright, FontStyles.Bold);
        StretchFull(activeEventCornerPillText.rectTransform);
        pillObj.SetActive(false);

        // 4) Center-Screen Splash Announcement Banner (2.3s milestone popup)
        GameObject bannerObj = new GameObject("EventSplashBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline), typeof(Shadow));
        bannerObj.transform.SetParent(canvasObj.transform, false);

        splashBannerRect = bannerObj.GetComponent<RectTransform>();
        splashBannerRect.anchorMin = splashBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        splashBannerRect.pivot = new Vector2(0.5f, 0.5f);
        splashBannerRect.anchoredPosition = new Vector2(0f, 36f);
        splashBannerRect.sizeDelta = new Vector2(740f, 156f);

        splashBannerGroup = bannerObj.GetComponent<CanvasGroup>();
        splashBannerGroup.alpha = 0f;
        splashBannerGroup.blocksRaycasts = false;
        splashBannerGroup.interactable = false;

        splashBannerBg = bannerObj.GetComponent<Image>();
        splashBannerBg.sprite = GameplayUI.GetRoundedPanelSprite();
        splashBannerBg.type = Image.Type.Sliced;
        splashBannerBg.color = new Color(0.12f, 0.04f, 0.05f, 0.98f);
        splashBannerBg.raycastTarget = false;

        splashBannerOutline = bannerObj.GetComponent<Outline>();
        splashBannerOutline.effectColor = GoldBright;
        splashBannerOutline.effectDistance = new Vector2(2.5f, -2.5f);

        Shadow bannerShadow = bannerObj.GetComponent<Shadow>();
        bannerShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        bannerShadow.effectDistance = new Vector2(0f, -8f);

        // Top & Bottom Accent Bars on Splash Banner
        leftHazardBar = CreateAccentBar(bannerObj.transform, "TopAccentBar", true, CrimsonAlert);
        rightHazardBar = CreateAccentBar(bannerObj.transform, "BottomAccentBar", false, GoldBright);

        // Left & Right Procedural Emblem Icons
        leftEmblemImage = CreateEmblemIcon(bannerObj.transform, "LeftEmblem", new Vector2(-296f, 0f));
        rightEmblemImage = CreateEmblemIcon(bannerObj.transform, "RightEmblem", new Vector2(296f, 0f));

        // Text Rows inside Splash Banner
        splashTagText = CreateUiLabel(bannerObj.transform, "SplashTag", "BIẾN CỐ TOÀN BÀN", 13f, GoldBright, FontStyles.Bold);
        SetUiRect(splashTagText.rectTransform, new Vector2(0f, 48f), new Vector2(500f, 22f));

        splashTitleText = CreateUiLabel(bannerObj.transform, "SplashTitle", "LỆNH TRUY NÃ TOÀN BÀN!", 26f, Color.white, FontStyles.Bold);
        SetUiRect(splashTitleText.rectTransform, new Vector2(0f, 16f), new Vector2(520f, 36f));

        splashSubtitleText = CreateUiLabel(bannerObj.transform, "SplashSubtitle", "MỤC TIÊU: RED", 18f, GoldBright, FontStyles.Bold);
        SetUiRect(splashSubtitleText.rectTransform, new Vector2(0f, -18f), new Vector2(520f, 26f));

        splashRuleText = CreateUiLabel(bannerObj.transform, "SplashRule", "Đá hạ mục tiêu: +2 Điểm  |  Mục tiêu sống sót: +2 Điểm", 13.5f, new Color(0.95f, 0.90f, 0.82f, 1f), FontStyles.Normal);
        SetUiRect(splashRuleText.rectTransform, new Vector2(0f, -48f), new Vector2(580f, 22f));
    }

    private Image CreateAccentBar(Transform parent, string name, bool isTop, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, isTop ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, isTop ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 7f);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Image CreateEmblemIcon(Transform parent, string name, Vector2 anchoredPos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(82f, 82f);

        Image img = go.GetComponent<Image>();
        img.sprite = crosshairSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_FontAsset GetSharedTmpFont()
    {
        GameplayUI ui = FindFirstObjectByType<GameplayUI>();
        if (ui != null && ui.turnText != null && ui.turnText.font != null)
            return ui.turnText.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static TMP_Text CreateUiLabel(Transform parent, string name, string text, float fontSize, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset sharedFont = GetSharedTmpFont();
        if (sharedFont != null)
            tmp.font = sharedFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SetUiRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);
    }

    // =========================================================================
    // PROCEDURAL MATERIALS, SPRITES & AUDIO SYNTHESIS
    // =========================================================================
    private void EnsureResources()
    {
        if (stingAudioSource == null)
        {
            stingAudioSource = gameObject.AddComponent<AudioSource>();
            stingAudioSource.playOnAwake = false;
            stingAudioSource.spatialBlend = 0f;
        }

        if (ambientLoopSource == null)
        {
            ambientLoopSource = gameObject.AddComponent<AudioSource>();
            ambientLoopSource.playOnAwake = false;
            ambientLoopSource.loop = true;
            ambientLoopSource.spatialBlend = 0f;
        }

        if (bountyStingClip == null)
            bountyStingClip = CreateBountySirensAndGongClip();
        if (homeRushStingClip == null)
            homeRushStingClip = CreateHomeRushBlazeStingClip();
        if (fireLoopClip == null)
            fireLoopClip = CreateAmbientFireCrackleLoopClip();

        if (softParticleTex == null)
            softParticleTex = CreateSoftRadialParticleTexture(64);
        if (flameTongueTex == null)
            flameTongueTex = CreateFlameTongueParticleTexture(64);
        if (sparkStarTex == null)
            sparkStarTex = CreateSparkStarTexture(64);
        if (verticalBeamTex == null)
            verticalBeamTex = CreateVerticalSpotlightBeamTexture(64);

        if (spotlightConeMesh == null)
            spotlightConeMesh = CreateSpotlightConeMesh(24, 0.092f, 0.185f, 0.56f);
        if (spotlightDiscMesh == null)
            spotlightDiscMesh = CreateSpotlightDiscMesh(0.115f);
        if (targetDiamondMesh == null)
            targetDiamondMesh = CreateTargetDiamondMesh(0.55f, 0.90f);

        // URP-safe transparent Sprites/Default shader (guarantees clean alpha without opaque traps or HDR bloom blow-out)
        Shader spriteShader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Unlit/Transparent");

        if (flameParticleMat == null && spriteShader != null)
        {
            flameParticleMat = new Material(spriteShader);
            flameParticleMat.mainTexture = flameTongueTex;
        }

        if (sparkParticleMat == null && spriteShader != null)
        {
            sparkParticleMat = new Material(spriteShader);
            sparkParticleMat.mainTexture = sparkStarTex;
        }

        if (bountyBeamMat == null && spriteShader != null)
        {
            bountyBeamMat = new Material(spriteShader);
            bountyBeamMat.mainTexture = verticalBeamTex;
            bountyBeamMat.renderQueue = 3100;
            SetMatColor(bountyBeamMat, new Color(1.00f, 0.42f, 0.24f, 0.40f));
        }

        if (bountySpotDiscMat == null && spriteShader != null)
        {
            bountySpotDiscMat = new Material(spriteShader);
            bountySpotDiscMat.mainTexture = softParticleTex;
            bountySpotDiscMat.renderQueue = 3095;
            SetMatColor(bountySpotDiscMat, new Color(1.00f, 0.72f, 0.26f, 0.36f));
        }

        if (bountyDarkBackingMat == null && spriteShader != null)
        {
            bountyDarkBackingMat = new Material(spriteShader);
            SetMatColor(bountyDarkBackingMat, new Color(0.10f, 0.02f, 0.03f, 0.92f));
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Color");

        if (bountyRingMat == null && unlitShader != null)
        {
            bountyRingMat = new Material(unlitShader);
            SetMatColor(bountyRingMat, CrimsonAlert);
        }

        if (bountyInnerMat == null && unlitShader != null)
        {
            bountyInnerMat = new Material(unlitShader);
            SetMatColor(bountyInnerMat, GoldBright);
        }

        if (bountyDiamondMat == null && unlitShader != null)
        {
            bountyDiamondMat = new Material(unlitShader);
            SetMatColor(bountyDiamondMat, GoldBright);
        }

        if (rushRingMat == null && unlitShader != null)
        {
            rushRingMat = new Material(unlitShader);
            SetMatColor(rushRingMat, ColorBlazeOrange);
        }

        for (int i = 0; i < rushStepMats.Length; i++)
        {
            if (rushStepMats[i] == null && spriteShader != null)
            {
                rushStepMats[i] = new Material(spriteShader);
                SetMatColor(rushStepMats[i], ColorBlazeOrange);
            }
        }

        if (crosshairSprite == null)
            crosshairSprite = CreateCrosshairEmblemSprite(96);

        if (flameSprite == null)
            flameSprite = CreateFlameEmblemSprite(96);

        if (tmpOverlayMat == null)
        {
            TMP_FontAsset sharedFont = GetSharedTmpFont();
            Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay")
                ?? Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            if (sharedFont != null && sharedFont.material != null && overlayShader != null)
            {
                tmpOverlayMat = new Material(sharedFont.material);
                tmpOverlayMat.shader = overlayShader;
                tmpOverlayMat.EnableKeyword("OUTLINE_ON");
                tmpOverlayMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.32f);
                tmpOverlayMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(22, 4, 4, 255));
                tmpOverlayMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.14f);
                tmpOverlayMat.EnableKeyword("UNDERLAY_ON");
                tmpOverlayMat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color32(0, 0, 0, 220));
                tmpOverlayMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
                tmpOverlayMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
                tmpOverlayMat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
                tmpOverlayMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.20f);
            }
        }
    }

    private static void SetMatColor(Material mat, Color col)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
    }

    private static Texture2D CreateSoftRadialParticleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float alpha = Mathf.Pow(1f - dist, 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateFlameTongueParticleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1); // 0 bottom .. 1 top tip
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half; // -1 .. 1
                if (ny < 0.04f || ny > 0.96f)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                    continue;
                }

                // Teardrop silhouette tapering cleanly to a dancing flame lick at top
                float t = Mathf.Clamp01((ny - 0.04f) / 0.92f);
                float halfWidth = Mathf.Sin(Mathf.Pow(t, 0.52f) * Mathf.PI) * 0.58f;
                float wave = Mathf.Sin(t * Mathf.PI * 2.2f) * 0.09f * t;
                float dist = Mathf.Abs(nx - wave);

                if (halfWidth <= 0.001f || dist >= halfWidth)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                    continue;
                }

                float edgeFactor = Mathf.Clamp01(1f - (dist / halfWidth));
                float alpha = Mathf.Pow(edgeFactor, 1.35f) * Mathf.Sin(t * Mathf.PI);
                // Subtle vertical intensity core fade
                float lum = Mathf.Lerp(0.88f, 1.0f, edgeFactor);
                tex.SetPixel(x, y, new Color(lum, lum, lum, Mathf.Clamp01(alpha * 1.15f)));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateSparkStarTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x - half) / half);
                float dy = Mathf.Abs((y - half) / half);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Exp(-r * r * 14f);
                float cross = Mathf.Exp(-dx * 16f) * Mathf.Exp(-dy * 3.5f)
                            + Mathf.Exp(-dy * 16f) * Mathf.Exp(-dx * 3.5f);
                float edgeMask = Mathf.Clamp01(1f - Mathf.SmoothStep(0.65f, 1.0f, r));
                float alpha = Mathf.Clamp01((core + cross * 0.65f) * edgeMask);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateVerticalSpotlightBeamTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1); // 0 base .. 1 top
            // Soft bottom ground rim (fade 0..0.06) then clean vertical decay to 0 at top
            float bottomFade = Mathf.SmoothStep(0.0f, 0.08f, ny);
            float topDecay = Mathf.Pow(1f - ny, 1.45f);
            float vertAlpha = bottomFade * topDecay;

            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)(size - 1);
                // Crisp rotating light shafts around the spotlight cylinder
                float shafts = 0.42f + 0.58f * (0.5f + 0.5f * Mathf.Sin(nx * Mathf.PI * 8f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(vertAlpha * shafts)));
            }
        }
        tex.Apply();
        return tex;
    }

    private static Mesh CreateSpotlightDiscMesh(float halfExtent)
    {
        Mesh mesh = new Mesh { name = "ProcSpotlightDisc" };
        mesh.vertices = new[]
        {
            new Vector3(-halfExtent, 0f, -halfExtent),
            new Vector3(-halfExtent, 0f,  halfExtent),
            new Vector3( halfExtent, 0f,  halfExtent),
            new Vector3( halfExtent, 0f, -halfExtent)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateSpotlightConeMesh(int segments, float bottomRadius, float topRadius, float height)
    {
        Mesh mesh = new Mesh { name = "ProcSpotlightCone" };
        int vertCount = (segments + 1) * 2;
        Vector3[] verts = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] tris = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            float angle = u * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            int bIdx = i * 2;
            int tIdx = bIdx + 1;

            verts[bIdx] = new Vector3(cos * bottomRadius, 0f, sin * bottomRadius);
            verts[tIdx] = new Vector3(cos * topRadius, height, sin * topRadius);

            uvs[bIdx] = new Vector2(u, 0f);
            uvs[tIdx] = new Vector2(u, 1f);

            if (i < segments)
            {
                int tri = i * 6;
                tris[tri + 0] = bIdx;
                tris[tri + 1] = tIdx;
                tris[tri + 2] = bIdx + 2;

                tris[tri + 3] = bIdx + 2;
                tris[tri + 4] = tIdx;
                tris[tri + 5] = tIdx + 2;
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateTargetDiamondMesh(float radius, float halfHeight)
    {
        Mesh mesh = new Mesh { name = "ProcTargetDiamond" };
        Vector3 top = new Vector3(0f, halfHeight, 0f);
        Vector3 bottom = new Vector3(0f, -halfHeight, 0f);
        Vector3[] ring =
        {
            new Vector3(radius, 0f, 0f),
            new Vector3(0f, 0f, radius),
            new Vector3(-radius, 0f, 0f),
            new Vector3(0f, 0f, -radius)
        };

        Vector3[] verts = new Vector3[24];
        int[] tris = new int[24];
        int v = 0;
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            // Upper pyramid face
            verts[v] = top; verts[v + 1] = ring[next]; verts[v + 2] = ring[i];
            tris[v] = v; tris[v + 1] = v + 1; tris[v + 2] = v + 2;
            v += 3;

            // Lower pyramid face
            verts[v] = bottom; verts[v + 1] = ring[i]; verts[v + 2] = ring[next];
            tris[v] = v; tris[v + 1] = v + 1; tris[v + 2] = v + 2;
            v += 3;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Sprite CreateEdgeVignetteSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x / (float)(size - 1)) * 2f - 1f);
                float ny = Mathf.Abs((y / (float)(size - 1)) * 2f - 1f);
                float edge = Mathf.Max(nx, ny);
                float alpha = Mathf.Clamp01((edge - 0.72f) / 0.28f);
                alpha = alpha * alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCrosshairEmblemSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                bool outerRing = r >= size * 0.34f && r <= size * 0.42f;
                bool innerRing = r >= size * 0.16f && r <= size * 0.21f;
                bool centerDot = r <= size * 0.06f;
                bool crossH = Mathf.Abs(dy) <= 3.2f && r >= size * 0.12f && r <= size * 0.47f;
                bool crossV = Mathf.Abs(dx) <= 3.2f && r >= size * 0.12f && r <= size * 0.47f;

                tex.SetPixel(x, y, (outerRing || innerRing || centerDot || crossH || crossV) ? Color.white : new Color(0, 0, 0, 0));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
    }

    private static Sprite CreateFlameEmblemSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float cx = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            float ny = y / (float)(size - 1); // 0 bottom .. 1 top
            for (int x = 0; x < size; x++)
            {
                float nx = (x - cx) / (size * 0.5f); // -1 .. 1
                // Teardrop flame silhouette with wave
                float wave = Mathf.Sin(ny * Mathf.PI * 2.5f) * 0.12f * ny;
                float halfWidth = Mathf.Sin(Mathf.Pow(ny, 0.65f) * Mathf.PI) * 0.68f;
                float dist = Mathf.Abs(nx - wave);
                bool inside = ny >= 0.08f && ny <= 0.94f && dist <= halfWidth;
                tex.SetPixel(x, y, inside ? Color.white : new Color(0, 0, 0, 0));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
    }

    private static AudioClip CreateBountySirensAndGongClip()
    {
        int sr = 22050;
        float dur = 1.35f;
        int samples = Mathf.CeilToInt(sr * dur);
        float[] data = new float[samples];

        float p1 = 0f;
        float p2 = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sr;
            // Dramatic western/arena wanted brass chord + alert sweep
            float sirenFreq = 440f + 180f * Mathf.Sin(t * Mathf.PI * 6f);
            float gongFreq = 146.83f; // D3 deep brass hit
            p1 += 2f * Mathf.PI * sirenFreq / sr;
            p2 += 2f * Mathf.PI * gongFreq / sr;

            float envGong = Mathf.Exp(-t * 2.8f) * Mathf.Clamp01(t * 120f);
            float envSiren = Mathf.Exp(-t * 1.8f) * Mathf.Clamp01(t * 40f);

            float gong = (Mathf.Sin(p2) + 0.5f * Mathf.Sin(p2 * 1.5f) + 0.3f * Mathf.Sin(p2 * 2f)) * envGong;
            float alert = Mathf.Sin(p1) * envSiren * 0.35f;
            data[i] = Mathf.Clamp((gong * 0.55f + alert) * 0.8f, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("BountySting_Proc", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateHomeRushBlazeStingClip()
    {
        int sr = 22050;
        float dur = 1.45f;
        int samples = Mathf.CeilToInt(sr * dur);
        float[] data = new float[samples];
        System.Random rng = new System.Random(42);

        float p1 = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sr;
            // Rising fanfare notes (C5 -> E5 -> G5 -> C6) + roaring fire whoosh noise
            float noteFreq = t < 0.16f ? 523.25f : (t < 0.32f ? 659.25f : (t < 0.48f ? 783.99f : 1046.50f));
            p1 += 2f * Mathf.PI * noteFreq / sr;

            float hornEnv = Mathf.Exp(-Mathf.Max(0f, t - 0.48f) * 2.5f) * Mathf.Clamp01(t * 80f);
            float horn = (Mathf.Sin(p1) + 0.4f * Mathf.Sin(p1 * 2f)) * hornEnv * 0.45f;

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float whooshEnv = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI) * Mathf.Exp(-t * 1.4f);
            float fireWhoosh = noise * whooshEnv * 0.38f;

            data[i] = Mathf.Clamp(horn + fireWhoosh, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create("HomeRushSting_Proc", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateAmbientFireCrackleLoopClip()
    {
        int sr = 22050;
        float dur = 2.0f;
        int samples = Mathf.CeilToInt(sr * dur);
        float[] data = new float[samples];
        System.Random rng = new System.Random(101);

        float lowFilter = 0f;
        for (int i = 0; i < samples; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lowFilter = lowFilter * 0.92f + white * 0.08f; // Warm low rumble
            float crackle = rng.NextDouble() > 0.996 ? (float)(rng.NextDouble() * 0.65) : 0f;
            data[i] = Mathf.Clamp(lowFilter * 0.45f + crackle * 0.25f, -0.8f, 0.8f);
        }

        AudioClip clip = AudioClip.Create("FireLoop_Proc", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static string GetPlayerDisplayName(int playerIndex)
    {
        string name = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");
        if (string.IsNullOrWhiteSpace(name))
            return ((PlayerColor)Mathf.Clamp(playerIndex, 0, 3)).ToString();
        return name;
    }

    private static string GetVietnameseColorName(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return "Xanh Dương";
            case 1: return "Đỏ";
            case 2: return "Xanh Lá";
            case 3: return "Vàng";
            default: return "";
        }
    }

    private static Color GetFactionColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.28f, 0.68f, 1.00f, 1f);
            case 1: return new Color(1.00f, 0.34f, 0.34f, 1f);
            case 2: return new Color(0.28f, 0.92f, 0.48f, 1f);
            case 3: return new Color(1.00f, 0.86f, 0.24f, 1f);
            default: return Color.white;
        }
    }
}
