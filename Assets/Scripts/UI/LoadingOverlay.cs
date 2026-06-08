using TMPro;
using UnityEngine;

public class LoadingOverlay : MonoBehaviour
{
    public GameObject root;
    public TMP_Text messageText;

    void Awake()
    {
        if (root == null)
            root = gameObject;
    }

    public void Show(string message)
    {
        if (root != null)
            root.SetActive(true);

        if (messageText != null)
            messageText.text = message;
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }
}
