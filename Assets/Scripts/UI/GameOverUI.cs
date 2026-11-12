using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public GameObject gameOverPanel;
    public TMP_Text rankingText;

    public string mainMenuSceneName = "LobbyScene";
    bool styled = false;

    void Start()
    {
        gameOverPanel.SetActive(false);
    }

    public void ShowRanking(string ranking)
    {
        gameOverPanel.SetActive(true);
        rankingText.text = ranking;
        ApplyRoyalGameOverStyling();
    }

    public void AppendMessage(string message)
    {
        if (rankingText != null)
            rankingText.text += message;
    }

    void ApplyRoyalGameOverStyling()
    {
        if (styled || gameOverPanel == null)
            return;
        styled = true;

        // Full-screen dark dim overlay that covers HUD and board
        RectTransform panelRt = gameOverPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.SetAsLastSibling();
        }

        Image panelImg = gameOverPanel.GetComponent<Image>() ?? gameOverPanel.AddComponent<Image>();
        panelImg.sprite = null;
        panelImg.color = new Color(0.03f, 0.02f, 0.015f, 0.90f);
        panelImg.raycastTarget = true;

        // Hide raw "GAME OVER" text to eliminate ugly overlap
        Transform legacyTitle = gameOverPanel.transform.Find("GameOverTitle");
        if (legacyTitle != null)
            legacyTitle.gameObject.SetActive(false);

        // Check if card exists
        Transform existingCard = gameOverPanel.transform.Find("RoyalGameOverCard");
        if (existingCard != null)
            return;

        // Create Center Royal Lacquer Card
        GameObject cardObj = new GameObject("RoyalGameOverCard", typeof(RectTransform));
        cardObj.layer = gameOverPanel.layer;
        cardObj.transform.SetParent(gameOverPanel.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(640f, 440f);

        RoyalUiThemeKit.ApplyLacquerPanel(cardObj, RoyalUiThemeKit.LacquerEbony, RoyalUiThemeKit.BrassAntique, 2.2f, true);

        // Header Plaque Ribbon
        GameObject hdrObj = new GameObject("GameOverHeaderPlaque", typeof(RectTransform), typeof(Image), typeof(Outline));
        hdrObj.layer = gameOverPanel.layer;
        hdrObj.transform.SetParent(cardRt, false);
        RectTransform hdrRt = hdrObj.GetComponent<RectTransform>();
        hdrRt.anchorMin = new Vector2(0.5f, 1f);
        hdrRt.anchorMax = new Vector2(0.5f, 1f);
        hdrRt.pivot = new Vector2(0.5f, 1f);
        hdrRt.anchoredPosition = new Vector2(0f, -16f);
        hdrRt.sizeDelta = new Vector2(460f, 46f);
        Image hdrImg = hdrObj.GetComponent<Image>();
        hdrImg.sprite = RoyalUiThemeKit.GetRibbonHeaderSprite();
        hdrImg.type = Image.Type.Sliced;
        hdrImg.color = RoyalUiThemeKit.CrimsonVelvet;
        Outline hdrOut = hdrObj.GetComponent<Outline>();
        hdrOut.effectColor = RoyalUiThemeKit.BrassBright;
        hdrOut.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject titleObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.layer = gameOverPanel.layer;
        titleObj.transform.SetParent(hdrRt, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) titleTmp.font = TMP_Settings.defaultFontAsset;
        titleTmp.text = "KẾT QUẢ TRẬN ĐẤU";
        titleTmp.fontSize = 20f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = RoyalUiThemeKit.IvoryText;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;

        // Content recessed slot
        GameObject slotObj = new GameObject("RankingSlot", typeof(RectTransform), typeof(Image));
        slotObj.layer = gameOverPanel.layer;
        slotObj.transform.SetParent(cardRt, false);
        RectTransform slotRt = slotObj.GetComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0.5f, 0.5f);
        slotRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotRt.pivot = new Vector2(0.5f, 0.5f);
        slotRt.anchoredPosition = new Vector2(0f, 8f);
        slotRt.sizeDelta = new Vector2(570f, 250f);
        Image slotImg = slotObj.GetComponent<Image>();
        slotImg.sprite = RoyalUiThemeKit.GetRecessedSlotSprite();
        slotImg.type = Image.Type.Sliced;
        slotImg.color = RoyalUiThemeKit.LacquerInset;

        // Reparent rankingText into slotRt
        if (rankingText != null)
        {
            rankingText.transform.SetParent(slotRt, false);
            RectTransform rRt = rankingText.rectTransform;
            rRt.anchorMin = Vector2.zero;
            rRt.anchorMax = Vector2.one;
            rRt.offsetMin = new Vector2(24f, 16f);
            rRt.offsetMax = new Vector2(-24f, -16f);
            rankingText.alignment = TextAlignmentOptions.TopLeft;
            rankingText.color = RoyalUiThemeKit.IvoryText;
            rankingText.fontSize = 17f;
            rankingText.lineSpacing = 1.35f;
            rankingText.textWrappingMode = TextWrappingModes.Normal;
        }

        // Reparent and style RestartButton and MainMenuButton
        Transform restartTr = gameOverPanel.transform.Find("RestartButton");
        if (restartTr != null)
        {
            restartTr.SetParent(cardRt, false);
            RectTransform rBtnRt = restartTr.GetComponent<RectTransform>();
            rBtnRt.anchorMin = new Vector2(0.5f, 0f);
            rBtnRt.anchorMax = new Vector2(0.5f, 0f);
            rBtnRt.pivot = new Vector2(0.5f, 0f);
            rBtnRt.anchoredPosition = new Vector2(-125f, 18f);
            rBtnRt.sizeDelta = new Vector2(210f, 46f);

            RoyalUiThemeKit.ApplyArcadeButton(restartTr.gameObject, RoyalUiThemeKit.ArcadeGoldCta, RoyalUiThemeKit.BrassBright, 1.8f);
            if (restartTr.gameObject.GetComponent<RoyalButtonHoverEffect>() == null)
                restartTr.gameObject.AddComponent<RoyalButtonHoverEffect>();

            TMP_Text rTxt = restartTr.GetComponentInChildren<TMP_Text>(true);
            if (rTxt != null)
            {
                rTxt.text = "CHƠI LẠI";
                rTxt.fontStyle = FontStyles.Bold;
                rTxt.fontSize = 16f;
                rTxt.color = RoyalUiThemeKit.DarkGoldInk;
                rTxt.alignment = TextAlignmentOptions.Center;
            }
        }

        Transform menuTr = gameOverPanel.transform.Find("MainMenuButton");
        if (menuTr != null)
        {
            menuTr.SetParent(cardRt, false);
            RectTransform mBtnRt = menuTr.GetComponent<RectTransform>();
            mBtnRt.anchorMin = new Vector2(0.5f, 0f);
            mBtnRt.anchorMax = new Vector2(0.5f, 0f);
            mBtnRt.pivot = new Vector2(0.5f, 0f);
            mBtnRt.anchoredPosition = new Vector2(125f, 18f);
            mBtnRt.sizeDelta = new Vector2(210f, 46f);

            RoyalUiThemeKit.ApplyArcadeButton(menuTr.gameObject, RoyalUiThemeKit.NavyRibbon, RoyalUiThemeKit.BrassAntique, 1.6f);
            if (menuTr.gameObject.GetComponent<RoyalButtonHoverEffect>() == null)
                menuTr.gameObject.AddComponent<RoyalButtonHoverEffect>();

            TMP_Text mTxt = menuTr.GetComponentInChildren<TMP_Text>(true);
            if (mTxt != null)
            {
                mTxt.text = "VỀ PHÒNG CHỜ";
                mTxt.fontStyle = FontStyles.Bold;
                mTxt.fontSize = 16f;
                mTxt.color = RoyalUiThemeKit.IvoryText;
                mTxt.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    public void RestartGame()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                SceneManager.GetActiveScene().name,
                LoadSceneMode.Single
            );
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToMainMenu()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                mainMenuSceneName,
                LoadSceneMode.Single
            );
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
