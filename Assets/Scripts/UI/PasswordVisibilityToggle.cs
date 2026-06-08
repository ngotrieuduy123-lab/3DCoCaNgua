using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PasswordVisibilityToggle : MonoBehaviour
{
    public MaskedPasswordInput targetInput;
    public TMP_Text label;

    bool isVisible;

    void Awake()
    {
        SetVisible(false);
    }

    public void Toggle()
    {
        SetVisible(!isVisible);
    }

    public void SetVisible(bool visible)
    {
        if (targetInput == null) return;

        isVisible = visible;
        targetInput.SetVisible(isVisible);

        if (label != null)
            label.text = isVisible ? "Hide" : "Show";
    }
}
