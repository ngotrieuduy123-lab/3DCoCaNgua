using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayUI : MonoBehaviour
{
    public TMP_Text turnText;
    public TMP_Text diceText;
    public TMP_Text messageText;
    public TMP_Text winnerText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public TMP_Text timerText;
    int localPlayerIndex = -1;
    PlayerColor currentTurnColor;
    RectTransform hudPopup;
    RectTransform turnNamePopup;
    TMP_Text turnNameText;
    string currentTurnDisplayName = "";
    bool hudConfigured;

    static readonly Color PanelColor = new Color(0.055f, 0.06f, 0.07f, 0.86f);
    static readonly Color PrimaryTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    static readonly Color SecondaryTextColor = new Color(0.74f, 0.78f, 0.84f, 1f);

    void Awake()
    {
        ConfigureHudPopup();
    }

    void Start()
    {
        ConfigureHudPopup();
    }

    public void SetTurn(PlayerColor color)
    {
        ConfigureHudPopup();
        currentTurnColor = color;
        RenderTurnText();
        RenderTurnNameText();
    }

    public void SetTurnDisplayName(int playerIndex, string displayName)
    {
        ConfigureHudPopup();
        currentTurnDisplayName = GetSafeDisplayName(playerIndex, displayName);
        RenderTurnNameText();
    }

    public void SetLocalPlayer(int playerIndex)
    {
        ConfigureHudPopup();
        localPlayerIndex = playerIndex;
        RenderTurnText();
    }

    void RenderTurnText()
    {
        if (turnText == null)
            return;

        string turnColor = currentTurnColor.ToString();
        string turnHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor((int)currentTurnColor));
        string turnLine = "Turn: <color=#" + turnHex + ">" + turnColor + "</color>";

        if (localPlayerIndex >= 0)
        {
            string localName = ((PlayerColor)localPlayerIndex).ToString();
            string localHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(localPlayerIndex));
            turnText.text = "You: <color=#" + localHex + ">" + localName + "</color>\n" + turnLine;
        }
        else
        {
            turnText.text = turnLine;
        }

        turnText.color = PrimaryTextColor;
    }

    void RenderTurnNameText()
    {
        if (turnNameText == null)
            return;

        int turnIndex = (int)currentTurnColor;
        string displayName = GetSafeDisplayName(turnIndex, currentTurnDisplayName);
        string turnHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(turnIndex));

        turnNameText.text = "<color=#" + turnHex + ">" + displayName + "</color> Turn";
        turnNameText.color = PrimaryTextColor;
    }

    public void SetDice(int dice1, int dice2, int total)
    {
        ConfigureHudPopup();
        if (diceText != null)
            diceText.color = SecondaryTextColor;

        diceText.text = "Dice: " + dice1 + " + " + dice2 + " = " + total;
    }

    public void SetMessage(string message)
    {
        ConfigureHudPopup();
        if (messageText != null)
            messageText.color = SecondaryTextColor;

        messageText.text = message;
    }

    public void SetWinner(int playerIndex)
    {
        winnerText.text = "Player " + playerIndex + " finished";
    }

    public void SetGameOver(string message = "Game Over")
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = message;

        SetMessage(message);
    }

    public void ClearDice()
    {
        ConfigureHudPopup();
        diceText.text = "Dice: -";
    }
    public void SetTimer(int value)
    {
        if (timerText != null)
        {
            timerText.text = "Time: " + value;
        }
    }

    void ConfigureHudPopup()
    {
        if (hudConfigured || turnText == null || diceText == null || messageText == null)
            return;

        RectTransform parent = turnText.rectTransform.parent as RectTransform;

        if (parent == null)
            return;

        GameObject popupObject = new GameObject("TurnHudPopup");
        popupObject.layer = turnText.gameObject.layer;
        popupObject.transform.SetParent(parent, false);

        hudPopup = popupObject.AddComponent<RectTransform>();
        hudPopup.anchorMin = new Vector2(0f, 1f);
        hudPopup.anchorMax = new Vector2(0f, 1f);
        hudPopup.pivot = new Vector2(0f, 1f);
        hudPopup.anchoredPosition = new Vector2(14f, -14f);
        hudPopup.sizeDelta = new Vector2(390f, 150f);

        Image panelImage = popupObject.AddComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        int firstTextIndex = Mathf.Min(
            turnText.rectTransform.GetSiblingIndex(),
            Mathf.Min(diceText.rectTransform.GetSiblingIndex(), messageText.rectTransform.GetSiblingIndex())
        );
        hudPopup.SetSiblingIndex(firstTextIndex);

        StyleHudText(turnText, new Vector2(28f, -24f), new Vector2(340f, 54f), 24f, PrimaryTextColor);
        StyleHudText(diceText, new Vector2(28f, -82f), new Vector2(340f, 30f), 21f, SecondaryTextColor);
        StyleHudText(messageText, new Vector2(28f, -114f), new Vector2(340f, 36f), 20f, SecondaryTextColor);

        CreateTurnNamePopup(parent);

        hudConfigured = true;
    }

    void CreateTurnNamePopup(RectTransform parent)
    {
        GameObject popupObject = new GameObject("TurnDisplayNamePopup");
        popupObject.layer = turnText.gameObject.layer;
        popupObject.transform.SetParent(parent, false);

        turnNamePopup = popupObject.AddComponent<RectTransform>();
        turnNamePopup.anchorMin = new Vector2(0f, 1f);
        turnNamePopup.anchorMax = new Vector2(0f, 1f);
        turnNamePopup.pivot = new Vector2(0f, 1f);
        turnNamePopup.anchoredPosition = new Vector2(418f, -14f);
        turnNamePopup.sizeDelta = new Vector2(290f, 58f);

        Image panelImage = popupObject.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.06f, 0.07f, 0.78f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject("TurnDisplayNameText");
        textObject.layer = turnText.gameObject.layer;
        textObject.transform.SetParent(popupObject.transform, false);

        turnNameText = textObject.AddComponent<TextMeshProUGUI>();
        turnNameText.alignment = TextAlignmentOptions.MidlineLeft;
        StyleHudText(turnNameText, new Vector2(18f, -12f), new Vector2(252f, 36f), 22f, PrimaryTextColor);

        RenderTurnNameText();
    }

    void StyleHudText(TMP_Text text, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.fontSize = fontSize;
        text.color = color;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.46f, 0.65f, 1f);
            case 1: return new Color(1f, 0.45f, 0.45f);
            case 2: return new Color(0.45f, 0.82f, 0.56f);
            case 3: return new Color(1f, 0.82f, 0.35f);
            default: return PrimaryTextColor;
        }
    }

    string GetSafeDisplayName(int playerIndex, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = ((PlayerColor)playerIndex).ToString();

        displayName = displayName.Trim();

        if (displayName.Length > 18)
            displayName = displayName.Substring(0, 18);

        return displayName;
    }
}
