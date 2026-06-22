using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiceRollPresentation : MonoBehaviour
{
    const float ShakeDuration = 1.25f;
    const float SettleDuration = 0.28f;
    const float ResultHoldDuration = 0.55f;

    static DiceRollPresentation instance;
    static Sprite roundedSquareSprite;
    static Sprite circleSprite;

    CanvasGroup canvasGroup;
    RectTransform diceOne;
    RectTransform diceTwo;
    Image[] diceOnePips;
    Image[] diceTwoPips;
    TMP_Text titleText;
    TMP_Text resultText;
    Coroutine activeRoll;

    readonly Vector2 diceOneHome = new Vector2(-145f, 20f);
    readonly Vector2 diceTwoHome = new Vector2(145f, 20f);

    public static DiceRollPresentation EnsureCreated()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<DiceRollPresentation>();
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Dice Roll Presentation");
        instance = root.AddComponent<DiceRollPresentation>();
        instance.BuildUI();
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
        if (canvasGroup == null)
            BuildUI();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public IEnumerator PlayRoll(int firstValue, int secondValue)
    {
        firstValue = Mathf.Clamp(firstValue, 1, 6);
        secondValue = Mathf.Clamp(secondValue, 1, 6);

        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        titleText.text = "ĐANG LẮC XÚC XẮC";
        resultText.text = string.Empty;

        SetFace(diceOnePips, 1);
        SetFace(diceTwoPips, 6);
        ResetDiceTransforms();

        yield return FadeCanvas(0f, 1f, 0.14f);

        float elapsed = 0f;
        int frame = -1;
        while (elapsed < ShakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / ShakeDuration);
            float energy = Mathf.Lerp(1f, 0.55f, progress);
            float angle = elapsed * 18f;

            diceOne.anchoredPosition = diceOneHome + new Vector2(
                Mathf.Sin(angle * 1.13f) * 34f,
                Mathf.Cos(angle * 1.47f) * 25f) * energy;
            diceTwo.anchoredPosition = diceTwoHome + new Vector2(
                Mathf.Cos(angle * 1.31f) * 32f,
                Mathf.Sin(angle * 1.61f) * 27f) * energy;

            diceOne.localEulerAngles = new Vector3(
                Mathf.Sin(angle * 0.71f) * 20f,
                Mathf.Cos(angle * 0.83f) * 20f,
                elapsed * 290f);
            diceTwo.localEulerAngles = new Vector3(
                Mathf.Cos(angle * 0.77f) * 20f,
                Mathf.Sin(angle * 0.91f) * 20f,
                -elapsed * 315f);

            float pulse = 1f + Mathf.Sin(elapsed * 24f) * 0.055f * energy;
            diceOne.localScale = Vector3.one * pulse;
            diceTwo.localScale = Vector3.one * (2f - pulse);

            int currentFrame = Mathf.FloorToInt(elapsed / 0.085f);
            if (currentFrame != frame)
            {
                frame = currentFrame;
                SetFace(diceOnePips, currentFrame % 6 + 1);
                SetFace(diceTwoPips, (currentFrame * 5 + 2) % 6 + 1);
            }

            yield return null;
        }

        SetFace(diceOnePips, firstValue);
        SetFace(diceTwoPips, secondValue);

        Vector2 firstStart = diceOne.anchoredPosition;
        Vector2 secondStart = diceTwo.anchoredPosition;
        Quaternion firstRotation = diceOne.localRotation;
        Quaternion secondRotation = diceTwo.localRotation;
        Vector3 firstScale = diceOne.localScale;
        Vector3 secondScale = diceTwo.localScale;

        elapsed = 0f;
        while (elapsed < SettleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SettleDuration));
            diceOne.anchoredPosition = Vector2.Lerp(firstStart, diceOneHome, t);
            diceTwo.anchoredPosition = Vector2.Lerp(secondStart, diceTwoHome, t);
            diceOne.localRotation = Quaternion.Slerp(firstRotation, Quaternion.identity, t);
            diceTwo.localRotation = Quaternion.Slerp(secondRotation, Quaternion.identity, t);
            diceOne.localScale = Vector3.Lerp(firstScale, Vector3.one, t);
            diceTwo.localScale = Vector3.Lerp(secondScale, Vector3.one, t);
            yield return null;
        }

        ResetDiceTransforms();
        titleText.text = "KẾT QUẢ";
        resultText.text = firstValue + "  +  " + secondValue + "  =  " + (firstValue + secondValue);
        yield return new WaitForSecondsRealtime(ResultHoldDuration);
        yield return FadeCanvas(1f, 0f, 0.18f);

        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void PreviewRoll(int firstValue, int secondValue)
    {
        gameObject.SetActive(true);
        if (activeRoll != null)
            StopCoroutine(activeRoll);
        activeRoll = StartCoroutine(PreviewRoutine(firstValue, secondValue));
    }

    IEnumerator PreviewRoutine(int firstValue, int secondValue)
    {
        yield return PlayRoll(firstValue, secondValue);
        activeRoll = null;
    }

    IEnumerator FadeCanvas(float from, float to, float duration)
    {
        float elapsed = 0f;
        canvasGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    void BuildUI()
    {
        if (canvasGroup != null)
            return;

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Image backdrop = CreateImage("Backdrop", transform, new Color(0.025f, 0.035f, 0.06f, 0.88f));
        Stretch(backdrop.rectTransform);

        Image glow = CreateImage("Center Glow", transform, new Color(0.10f, 0.42f, 0.58f, 0.23f));
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = new Vector2(900f, 620f);
        glow.sprite = GetRoundedSquareSprite();
        glow.type = Image.Type.Sliced;

        titleText = CreateText("Title", transform, 55f, FontStyles.Bold);
        // Keep the heading fully inside the presentation panel instead of
        // straddling its upper edge at narrower Game View resolutions.
        SetRect(titleText.rectTransform, new Vector2(0f, 258f), new Vector2(1000f, 90f));
        titleText.color = new Color(0.94f, 0.97f, 1f, 1f);

        resultText = CreateText("Result", transform, 49f, FontStyles.Bold);
        SetRect(resultText.rectTransform, new Vector2(0f, -250f), new Vector2(900f, 90f));
        resultText.color = new Color(1f, 0.80f, 0.20f, 1f);

        diceOne = CreateDie("Large Dice 1", diceOneHome, out diceOnePips);
        diceTwo = CreateDie("Large Dice 2", diceTwoHome, out diceTwoPips);

        gameObject.SetActive(false);
    }

    RectTransform CreateDie(string objectName, Vector2 position, out Image[] pips)
    {
        Image die = CreateImage(objectName, transform, new Color(0.97f, 0.95f, 0.88f, 1f));
        die.sprite = GetRoundedSquareSprite();
        die.type = Image.Type.Sliced;
        SetRect(die.rectTransform, position, new Vector2(230f, 230f));

        Shadow shadow = die.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
        shadow.effectDistance = new Vector2(12f, -16f);

        Outline outline = die.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.68f, 0.73f, 0.78f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Vector2[] positions =
        {
            new Vector2(-66f, 66f), new Vector2(66f, 66f),
            new Vector2(-66f, 0f), Vector2.zero, new Vector2(66f, 0f),
            new Vector2(-66f, -66f), new Vector2(66f, -66f)
        };

        pips = new Image[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Image pip = CreateImage("Pip " + i, die.transform, new Color(0.035f, 0.045f, 0.06f, 1f));
            pip.sprite = GetCircleSprite();
            SetRect(pip.rectTransform, positions[i], new Vector2(39f, 39f));
            pips[i] = pip;
        }

        return die.rectTransform;
    }

    static void SetFace(Image[] pips, int value)
    {
        bool[] visible = new bool[7];
        switch (value)
        {
            case 1: visible[3] = true; break;
            case 2: visible[0] = visible[6] = true; break;
            case 3: visible[0] = visible[3] = visible[6] = true; break;
            case 4: visible[0] = visible[1] = visible[5] = visible[6] = true; break;
            case 5: visible[0] = visible[1] = visible[3] = visible[5] = visible[6] = true; break;
            case 6: visible[0] = visible[1] = visible[2] = visible[4] = visible[5] = visible[6] = true; break;
        }

        for (int i = 0; i < pips.Length; i++)
            pips[i].enabled = visible[i];
    }

    void ResetDiceTransforms()
    {
        diceOne.anchoredPosition = diceOneHome;
        diceTwo.anchoredPosition = diceTwoHome;
        diceOne.localRotation = Quaternion.identity;
        diceTwo.localRotation = Quaternion.identity;
        diceOne.localScale = Vector3.one;
        diceTwo.localScale = Vector3.one;
    }

    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static TMP_Text CreateText(string objectName, Transform parent, float fontSize, FontStyles style)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
            circleSprite = CreateProceduralSprite(64, 32f);
        return circleSprite;
    }

    static Sprite GetRoundedSquareSprite()
    {
        if (roundedSquareSprite == null)
            roundedSquareSprite = CreateProceduralSprite(64, 13f);
        return roundedSquareSprite;
    }

    static Sprite CreateProceduralSprite(int size, float radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Dice UI Shape";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;
        float half = center;
        float inner = Mathf.Max(0f, half - radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - center) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - center) - inner, 0f);
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
