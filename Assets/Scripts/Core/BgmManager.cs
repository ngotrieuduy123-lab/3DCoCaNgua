using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BgmManager : MonoBehaviour
{
    public static BgmManager Instance { get; private set; }

    private const string MutePrefKey = "CoCaNgua_BgmMuted";
    private const float DefaultVolume = 0.34f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private AudioClip menuLobbyClip;
    private AudioClip gameplayClip;
    private AudioClip uiClickClip;
    private AudioClip uiHoverClip;
    private bool isMuted;
    private Coroutine fadeRoutine;
    private float lastHoverSoundTime;
    private float lastClickSoundTime;

    private Canvas overlayCanvas;
    private Button muteButton;
    private TMP_Text muteButtonLabel;
    private Image muteButtonBg;
    private Image muteNoteIcon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureCreated();
    }

    public static BgmManager EnsureCreated()
    {
        if (Instance != null)
            return Instance;

        BgmManager existing = Object.FindFirstObjectByType<BgmManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("BgmManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<BgmManager>();
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
        DontDestroyOnLoad(gameObject);

        isMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;

        bgmSource = gameObject.GetComponent<AudioSource>();
        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();

        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = isMuted ? 0f : DefaultVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 0.85f;

        LoadAudioClips();
        BuildPersistentMusicHud();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        UpdateBgmForScene(SceneManager.GetActiveScene().name);
        AttachSfxToAllSceneButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LoadAudioClips()
    {
        menuLobbyClip = Resources.Load<AudioClip>("Audio/Bgm_MenuLobby");
        gameplayClip = Resources.Load<AudioClip>("Audio/Bgm_Gameplay");
        uiClickClip = Resources.Load<AudioClip>("Audio/Ui_ButtonClick");
        uiHoverClip = Resources.Load<AudioClip>("Audio/Ui_ButtonHover");

        if (menuLobbyClip == null)
            menuLobbyClip = CreateProceduralClip("Bgm_MenuLobby_Fallback", false);

        if (gameplayClip == null)
            gameplayClip = CreateProceduralClip("Bgm_Gameplay_Fallback", true);

        if (uiClickClip == null)
            uiClickClip = CreateProceduralUiClickClip();

        if (uiHoverClip == null)
            uiHoverClip = CreateProceduralUiHoverClip();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateBgmForScene(scene.name);
        UpdateHudPositionForScene(scene.name);
        AttachSfxToAllSceneButtons();
    }

    public static void PlayUiClick()
    {
        EnsureCreated().PlayUiClickInternal();
    }

    public static void PlayUiHover()
    {
        EnsureCreated().PlayUiHoverInternal();
    }

    private void PlayUiClickInternal()
    {
        if (sfxSource == null)
            return;

        if (Time.unscaledTime - lastClickSoundTime < 0.03f)
            return;

        lastClickSoundTime = Time.unscaledTime;

        if (uiClickClip == null)
            uiClickClip = CreateProceduralUiClickClip();

        sfxSource.pitch = Random.Range(0.97f, 1.04f);
        sfxSource.PlayOneShot(uiClickClip, 0.88f);
    }

    private void PlayUiHoverInternal()
    {
        if (sfxSource == null)
            return;

        if (Time.unscaledTime - lastHoverSoundTime < 0.055f)
            return;

        lastHoverSoundTime = Time.unscaledTime;

        if (uiHoverClip == null)
            uiHoverClip = CreateProceduralUiHoverClip();

        sfxSource.pitch = Random.Range(0.98f, 1.05f);
        sfxSource.PlayOneShot(uiHoverClip, 0.42f);
    }

    public void AttachSfxToAllSceneButtons()
    {
        Button[] allButtons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button btn in allButtons)
        {
            if (btn == null)
                continue;

            if (btn.GetComponent<RoyalButtonHoverEffect>() == null)
                btn.gameObject.AddComponent<RoyalButtonHoverEffect>();
        }
    }

    public void UpdateBgmForScene(string sceneName)
    {
        LoadAudioClips();

        bool isGameplay = !string.IsNullOrEmpty(sceneName) &&
                          sceneName.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) >= 0;

        AudioClip targetClip = isGameplay ? gameplayClip : menuLobbyClip;
        if (targetClip == null)
            return;

        if (bgmSource.clip == targetClip && bgmSource.isPlaying)
        {
            bgmSource.volume = isMuted ? 0f : DefaultVolume;
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(CrossfadeToClipRoutine(targetClip));
    }

    private IEnumerator CrossfadeToClipRoutine(AudioClip nextClip)
    {
        float targetVol = isMuted ? 0f : DefaultVolume;

        if (bgmSource.isPlaying && bgmSource.clip != null && bgmSource.volume > 0.01f)
        {
            float startVol = bgmSource.volume;
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, t / 0.28f);
                yield return null;
            }
        }

        bgmSource.clip = nextClip;
        bgmSource.loop = true;
        bgmSource.Play();

        float fadeIn = 0f;
        while (fadeIn < 0.40f)
        {
            fadeIn += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, targetVol, fadeIn / 0.40f);
            yield return null;
        }

        bgmSource.volume = targetVol;
        fadeRoutine = null;
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        PlayerPrefs.SetInt(MutePrefKey, isMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (bgmSource != null)
            bgmSource.volume = isMuted ? 0f : DefaultVolume;

        RefreshMuteHudVisuals();
    }

    private void BuildPersistentMusicHud()
    {
        if (overlayCanvas != null)
            return;

        GameObject canvasObj = new GameObject("BgmHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.transform.SetParent(transform, false);

        overlayCanvas = canvasObj.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 950;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject btnObj = new GameObject("MusicToggleButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
        btnObj.transform.SetParent(canvasObj.transform, false);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 0f);
        btnRect.anchorMax = new Vector2(0f, 0f);
        btnRect.pivot = new Vector2(0f, 0f);
        btnRect.anchoredPosition = new Vector2(16f, 14f);
        btnRect.sizeDelta = new Vector2(142f, 38f);

        muteButtonBg = btnObj.GetComponent<Image>();
        muteButtonBg.sprite = RoyalUiThemeKit.GetArcadeButtonSprite();
        muteButtonBg.type = Image.Type.Sliced;
        muteButtonBg.color = RoyalUiThemeKit.LacquerWarm;

        Outline outline = btnObj.GetComponent<Outline>();
        outline.effectColor = RoyalUiThemeKit.BrassAntique;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow shadow = btnObj.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(0f, -3.5f);

        muteButton = btnObj.GetComponent<Button>();
        muteButton.targetGraphic = muteButtonBg;
        muteButton.onClick.AddListener(ToggleMute);
        btnObj.AddComponent<RoyalButtonHoverEffect>();

        GameObject iconObj = new GameObject("MusicIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(11f, 1f);
        iconRect.sizeDelta = new Vector2(20f, 20f);
        muteNoteIcon = iconObj.GetComponent<Image>();
        muteNoteIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.MusicNote);
        muteNoteIcon.preserveAspect = true;
        muteNoteIcon.raycastTarget = false;

        GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(34f, 3f);
        txtRect.offsetMax = new Vector2(-10f, -1f);

        muteButtonLabel = txtObj.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            muteButtonLabel.font = TMP_Settings.defaultFontAsset;
        muteButtonLabel.fontSize = 13.5f;
        muteButtonLabel.fontStyle = FontStyles.Bold;
        muteButtonLabel.alignment = TextAlignmentOptions.Center;
        muteButtonLabel.raycastTarget = false;

        RefreshMuteHudVisuals();
        UpdateHudPositionForScene(SceneManager.GetActiveScene().name);
    }

    private void UpdateHudPositionForScene(string sceneName)
    {
        if (muteButton == null)
            return;

        RectTransform rect = muteButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(16f, 14f);
    }

    private void RefreshMuteHudVisuals()
    {
        if (muteButtonLabel == null || muteButtonBg == null)
            return;

        if (isMuted)
        {
            muteButtonLabel.text = "NHẠC: TẮT";
            muteButtonLabel.color = new Color(0.75f, 0.68f, 0.58f, 0.90f);
            muteButtonBg.color = RoyalUiThemeKit.LacquerInset;
            if (muteNoteIcon != null)
                muteNoteIcon.color = new Color(0.65f, 0.58f, 0.48f, 0.75f);
        }
        else
        {
            muteButtonLabel.text = "NHẠC: BẬT";
            muteButtonLabel.color = RoyalUiThemeKit.BrassBright;
            muteButtonBg.color = RoyalUiThemeKit.LacquerWarm;
            if (muteNoteIcon != null)
                muteNoteIcon.color = RoyalUiThemeKit.BrassBright;
        }
    }

    private static AudioClip CreateProceduralUiClickClip()
    {
        int sr = 22050;
        float dur = 0.09f;
        int samples = Mathf.CeilToInt(sr * dur);
        float[] data = new float[samples];
        float p1 = 0f;
        float p2 = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sr;
            float f1 = 380f + 920f * Mathf.Exp(-t * 85f);
            float f2 = 1174.66f;
            p1 += 2f * Mathf.PI * f1 / sr;
            p2 += 2f * Mathf.PI * f2 / sr;
            float e1 = Mathf.Exp(-t * 58f) * Mathf.Clamp01(t * 600f);
            float e2 = Mathf.Exp(-t * 36f) * Mathf.Clamp01(t * 300f);
            data[i] = (0.7f * Mathf.Sin(p1) * e1 + 0.35f * Mathf.Sin(p2) * e2) * 0.85f;
        }
        AudioClip clip = AudioClip.Create("UiClick_Proc", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateProceduralUiHoverClip()
    {
        int sr = 22050;
        float dur = 0.042f;
        int samples = Mathf.CeilToInt(sr * dur);
        float[] data = new float[samples];
        float p = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sr;
            float f = 640f + 380f * Mathf.Exp(-t * 120f);
            p += 2f * Mathf.PI * f / sr;
            float e = Mathf.Exp(-t * 95f) * Mathf.Clamp01(t * 500f);
            data[i] = Mathf.Sin(p) * e * 0.45f;
        }
        AudioClip clip = AudioClip.Create("UiHover_Proc", samples, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateProceduralClip(string clipName, bool energetic)
    {
        int sampleRate = 22050;
        float duration = energetic ? 8f : 10f;
        int totalSamples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[totalSamples];

        float[] notes = energetic
            ? new float[] { 220f, 261.63f, 329.63f, 392f, 329.63f, 261.63f, 293.66f, 349.23f }
            : new float[] { 174.61f, 220f, 261.63f, 329.63f, 293.66f, 246.94f, 196f, 261.63f };

        float noteLen = duration / notes.Length;
        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            int idx = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / noteLen));
            float localT = t - idx * noteLen;
            float env = Mathf.Exp(-localT * 2.8f) * Mathf.Clamp01(localT * 30f);
            float f = notes[idx];
            data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t)) * env * 0.25f;
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
