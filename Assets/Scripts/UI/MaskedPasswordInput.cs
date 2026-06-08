using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MaskedPasswordInput : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler
{
    public TMP_InputField input;
    public TMP_Text displayText;
    public char maskCharacter = '*';

    const float CaretBlinkInterval = 0.48f;
    const float CaretHeightPadding = 10f;

    string passwordValue = string.Empty;
    int caretIndex;
    bool isVisible;
    bool isFocused;
    bool caretBlinkOn = true;
    float caretBlinkTimer;
    bool isApplyingText;
    RectTransform caretRect;
    Image caretImage;

    public string Password => passwordValue;

    void Awake()
    {
        Configure();
    }

    void OnEnable()
    {
        Configure();
        SubscribeKeyboard();
        RefreshDisplay(true);
    }

    void OnDisable()
    {
        UnsubscribeKeyboard();
        isFocused = false;
        SetCaretVisible(false);
    }

    void OnDestroy()
    {
        UnsubscribeKeyboard();

        if (input != null)
            input.onValueChanged.RemoveListener(HandleExternalValueChanged);
    }

    void Update()
    {
        if (!isFocused)
            return;

        HandleNavigationKeys();
        UpdateCaretBlink();
    }

    public void Configure()
    {
        if (input == null)
            input = GetComponent<TMP_InputField>();

        if (input == null)
            return;

        passwordValue = input.text ?? string.Empty;
        caretIndex = Mathf.Clamp(caretIndex, 0, passwordValue.Length);

        input.contentType = TMP_InputField.ContentType.Standard;
        input.inputType = TMP_InputField.InputType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.readOnly = true;
        input.caretWidth = 0;
        input.customCaretColor = true;
        input.caretColor = new Color(0f, 0f, 0f, 0f);
        input.selectionColor = new Color(0.15f, 0.35f, 0.75f, 0.25f);

        if (input.textComponent != null)
            input.textComponent.color = new Color(0f, 0f, 0f, 0f);

        if (displayText != null)
            displayText.raycastTarget = false;

        EnsureCaret();

        input.onValueChanged.RemoveListener(HandleExternalValueChanged);
        input.onValueChanged.AddListener(HandleExternalValueChanged);

        ApplyTextToInput();
        RefreshDisplay(true);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        RefreshDisplay(true);
    }

    public void ToggleVisible()
    {
        SetVisible(!isVisible);
    }

    public void Clear()
    {
        passwordValue = string.Empty;
        caretIndex = 0;
        ApplyTextToInput();
        RefreshDisplay(true);
    }

    public void OnSelect(BaseEventData eventData)
    {
        isFocused = true;
        caretIndex = Mathf.Clamp(caretIndex, 0, passwordValue.Length);
        ResetCaretBlink();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isFocused = false;
        SetCaretVisible(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        caretIndex = passwordValue.Length;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);

        isFocused = true;
        ResetCaretBlink();
        RefreshDisplay(true);
    }

    void SubscribeKeyboard()
    {
        if (Keyboard.current != null)
        {
            Keyboard.current.onTextInput -= HandleTextInput;
            Keyboard.current.onTextInput += HandleTextInput;
        }
    }

    void UnsubscribeKeyboard()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= HandleTextInput;
    }

    void HandleTextInput(char character)
    {
        if (!isFocused || input == null || !input.interactable)
            return;

        if (char.IsControl(character))
            return;

        if (input.characterLimit > 0 && passwordValue.Length >= input.characterLimit)
            return;

        passwordValue = passwordValue.Insert(caretIndex, character.ToString());
        caretIndex++;
        ApplyTextToInput();
        RefreshDisplay(true);
        ResetCaretBlink();
    }

    void HandleNavigationKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || input == null || !input.interactable)
            return;

        bool changed = false;

        if (keyboard.backspaceKey.wasPressedThisFrame && caretIndex > 0)
        {
            passwordValue = passwordValue.Remove(caretIndex - 1, 1);
            caretIndex--;
            changed = true;
        }

        if (keyboard.deleteKey.wasPressedThisFrame && caretIndex < passwordValue.Length)
        {
            passwordValue = passwordValue.Remove(caretIndex, 1);
            changed = true;
        }

        if (keyboard.leftArrowKey.wasPressedThisFrame)
        {
            caretIndex = Mathf.Max(0, caretIndex - 1);
            changed = true;
        }

        if (keyboard.rightArrowKey.wasPressedThisFrame)
        {
            caretIndex = Mathf.Min(passwordValue.Length, caretIndex + 1);
            changed = true;
        }

        if (keyboard.homeKey.wasPressedThisFrame)
        {
            caretIndex = 0;
            changed = true;
        }

        if (keyboard.endKey.wasPressedThisFrame)
        {
            caretIndex = passwordValue.Length;
            changed = true;
        }

        if (changed)
        {
            ApplyTextToInput();
            RefreshDisplay(true);
            ResetCaretBlink();
        }
    }

    void HandleExternalValueChanged(string value)
    {
        if (isApplyingText)
            return;

        passwordValue = value ?? string.Empty;
        caretIndex = Mathf.Clamp(caretIndex, 0, passwordValue.Length);
        RefreshDisplay(true);
    }

    void ApplyTextToInput()
    {
        if (input == null)
            return;

        isApplyingText = true;
        input.SetTextWithoutNotify(passwordValue);
        input.caretPosition = caretIndex;
        input.stringPosition = caretIndex;
        input.ForceLabelUpdate();
        isApplyingText = false;
    }

    void RefreshDisplay(bool forceMesh)
    {
        if (displayText == null)
            return;

        displayText.text = isVisible ? passwordValue : new string(maskCharacter, passwordValue.Length);

        if (forceMesh)
            displayText.ForceMeshUpdate();

        UpdateCaretPosition();
    }

    void EnsureCaret()
    {
        if (displayText == null || caretRect != null)
            return;

        GameObject caretObject = new GameObject("PasswordCaret", typeof(RectTransform), typeof(Image));
        caretObject.transform.SetParent(displayText.transform.parent, false);

        caretRect = caretObject.GetComponent<RectTransform>();
        caretRect.anchorMin = new Vector2(0f, 0f);
        caretRect.anchorMax = new Vector2(0f, 1f);
        caretRect.pivot = new Vector2(0.5f, 0.5f);
        caretRect.sizeDelta = new Vector2(2f, -CaretHeightPadding);

        caretImage = caretObject.GetComponent<Image>();
        caretImage.color = new Color(0.08f, 0.1f, 0.13f, 1f);
        caretImage.raycastTarget = false;

        SetCaretVisible(false);
    }

    void UpdateCaretPosition()
    {
        if (displayText == null || caretRect == null)
            return;

        displayText.ForceMeshUpdate();

        float x = 0f;
        int visibleLength = displayText.textInfo.characterCount;
        int visibleCaretIndex = Mathf.Clamp(caretIndex, 0, visibleLength);

        if (visibleCaretIndex > 0 && visibleCaretIndex <= displayText.textInfo.characterInfo.Length)
        {
            TMP_CharacterInfo characterInfo = displayText.textInfo.characterInfo[visibleCaretIndex - 1];
            x = characterInfo.xAdvance;
        }

        caretRect.anchoredPosition = new Vector2(x + 1f, 0f);
        SetCaretVisible(isFocused && caretBlinkOn);
    }

    void UpdateCaretBlink()
    {
        caretBlinkTimer += Time.unscaledDeltaTime;
        if (caretBlinkTimer < CaretBlinkInterval)
            return;

        caretBlinkTimer = 0f;
        caretBlinkOn = !caretBlinkOn;
        SetCaretVisible(caretBlinkOn);
    }

    void ResetCaretBlink()
    {
        caretBlinkTimer = 0f;
        caretBlinkOn = true;
        SetCaretVisible(true);
        UpdateCaretPosition();
    }

    void SetCaretVisible(bool visible)
    {
        if (caretImage != null)
            caretImage.enabled = isFocused && visible;
    }
}
