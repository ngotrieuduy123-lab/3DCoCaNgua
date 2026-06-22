using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinShopUI : MonoBehaviour
{
    static readonly Color Backdrop = new Color(0.025f, 0.035f, 0.055f, 0.94f);
    static readonly Color Card = new Color(0.09f, 0.12f, 0.18f, 1f);
    static readonly Color Accent = new Color(0.18f, 0.55f, 0.95f, 1f);
    static readonly Color CoinGold = new Color(1f, 0.78f, 0.16f, 1f);

    GameObject panel;
    RectTransform listRoot;
    TMP_Text coinText;
    TMP_Text statusText;
    TMP_Text titleText;
    Sprite coinSprite;
    bool ownedOnly;

    public static void EnsureCreated(bool createLauncher = false)
    {
        if (FindFirstObjectByType<SkinShopUI>() != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
            return;

        GameObject root = new GameObject("SkinShopUI");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.AddComponent<SkinShopUI>().Build(createLauncher);
    }

    public static void OpenShop()
    {
        EnsureCreated(false);
        SkinShopUI shop = FindFirstObjectByType<SkinShopUI>();
        if (shop != null) shop.Open(false);
    }

    public static void OpenOwnedSkins()
    {
        EnsureCreated(false);
        SkinShopUI shop = FindFirstObjectByType<SkinShopUI>();
        if (shop != null) shop.Open(true);
    }

    void Build(bool createLauncher)
    {
        coinSprite = Resources.Load<Sprite>("UI/Icons/coin");

        if (createLauncher)
        {
            Button launcher = CreateButton(transform, "Skin Shop", new Vector2(0f, 1f), new Vector2(-26f, -26f), new Vector2(180f, 52f));
            RectTransform launcherRect = launcher.GetComponent<RectTransform>();
            launcherRect.anchorMin = Vector2.one;
            launcherRect.anchorMax = Vector2.one;
            launcherRect.pivot = Vector2.one;
            launcher.onClick.AddListener(() => Open(false));
        }

        panel = CreatePanel(transform, "SkinShopPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Backdrop);
        panel.SetActive(false);

        GameObject card = CreatePanel(panel.transform, "ShopCard", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero, Card);

        titleText = CreateText(card.transform, "SKIN SHOP", 34f, TextAlignmentOptions.Center);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        SetRect(titleText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -14f), new Vector2(-120f, 48f));

        CreateCoinIcon(card.transform, "PlayerCoinIcon", new Vector2(0f, 1f), new Vector2(24f, -68f), new Vector2(30f, 30f), new Vector2(0f, 1f));

        coinText = CreateText(card.transform, "0", 23f, TextAlignmentOptions.MidlineLeft);
        coinText.color = CoinGold;
        coinText.fontStyle = FontStyles.Bold;
        coinText.rectTransform.pivot = new Vector2(0f, 1f);
        SetRect(coinText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -68f), new Vector2(242f, 34f));

        Button close = CreateButton(card.transform, "X", Vector2.one, new Vector2(-24f, -22f), new Vector2(46f, 42f));
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = Vector2.one;
        closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        close.onClick.AddListener(() => panel.SetActive(false));

        GameObject list = CreatePanel(card.transform, "SkinListViewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.16f));
        RectTransform viewportRect = list.GetComponent<RectTransform>();
        viewportRect.offsetMin = new Vector2(24f, 64f);
        viewportRect.offsetMax = new Vector2(-24f, -110f);
        list.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content");
        content.transform.SetParent(list.transform, false);
        listRoot = content.AddComponent<RectTransform>();
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = Vector2.one;
        listRoot.pivot = new Vector2(0.5f, 1f);
        listRoot.anchoredPosition = Vector2.zero;
        listRoot.sizeDelta = Vector2.zero;

        ScrollRect scroll = list.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = listRoot;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        statusText = CreateText(card.transform, "", 20f, TextAlignmentOptions.Center);
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 28f), new Vector2(-60f, 42f));
    }

    void Open(bool showOwnedOnly)
    {
        ownedOnly = showOwnedOnly;
        panel.SetActive(true);
        statusText.text = "";
        titleText.text = ownedOnly ? "MY SKINS" : "SKIN SHOP";
        Refresh();
    }

    void Refresh()
    {
        foreach (Transform child in listRoot)
            Destroy(child.gameObject);

        PlayerData player = DatabaseManager.Instance != null ? DatabaseManager.Instance.CurrentPlayer : null;
        int coins = player != null ? player.Coins : PlayerPrefs.GetInt("Coins", 0);
        coinText.text = coins.ToString("N0");

        SkinCatalog catalog = SkinCatalog.Load();

        if (catalog == null)
        {
            statusText.text = "Skin catalog is missing.";
            return;
        }

        foreach (SkinDefinition skin in catalog.skins)
        {
            if (skin == null)
                continue;

            bool owned = player != null && DatabaseManager.Instance.CurrentPlayerOwnsSkin(skin.id);
            if (ownedOnly && !owned)
                continue;

            CreateSkinRow(skin, player, owned);
        }
    }

    void CreateSkinRow(SkinDefinition skin, PlayerData player, bool owned)
    {
        GameObject row = CreatePanel(listRoot, skin.id, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 128f), new Color(0.13f, 0.17f, 0.24f, 1f));
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 128f;

        GameObject previewObject = CreatePanel(row.transform, "Preview", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(104f, 104f), new Color(0.055f, 0.075f, 0.11f, 1f));
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.pivot = new Vector2(0f, 0.5f);
        Image preview = previewObject.GetComponent<Image>();
        preview.sprite = skin.previewImage;
        preview.preserveAspect = true;
        preview.color = skin.previewImage != null ? Color.white : new Color(0.055f, 0.075f, 0.11f, 1f);

        TMP_Text label = CreateText(row.transform, skin.displayName + "\n<size=18><color=#AEBBD0>" + skin.description + "</color></size>", 25f, TextAlignmentOptions.MidlineLeft);
        SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(130f, 10f), new Vector2(-322f, -20f));

        bool equipped = player != null && player.EquippedSkinId == skin.id;
        bool showPrice = !ownedOnly && !owned;
        string action = ownedOnly
            ? (equipped ? "Equipped" : "Equip")
            : (owned ? "Already have" : skin.price.ToString("N0"));
        bool canInteract = ownedOnly
            ? owned && !equipped && player != null
            : !owned && player != null;

        Button button = CreateButton(row.transform, action, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(176f, 54f));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        button.interactable = canInteract;
        button.onClick.AddListener(() => HandleSkin(skin, owned));

        if (showPrice)
            StyleCoinPriceButton(button);
    }

    async void HandleSkin(SkinDefinition skin, bool owned)
    {
        if (DatabaseManager.Instance == null)
            return;

        if (ownedOnly && !owned)
            return;

        if (!ownedOnly && owned)
            return;

        DatabaseManager.ShopResult result = ownedOnly
            ? await DatabaseManager.Instance.EquipSkin(skin.id)
            : await DatabaseManager.Instance.PurchaseSkin(skin.id);

        statusText.text = result.Message;
        Refresh();
        statusText.text = result.Message;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject target = new GameObject(name);
        target.transform.SetParent(parent, false);
        RectTransform rect = target.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = target.AddComponent<Image>();
        image.color = color;
        return target;
    }

    static Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject target = CreatePanel(parent, label.Replace(" ", "") + "Button", anchor, anchor, position, size, Accent);
        Button button = target.AddComponent<Button>();
        TMP_Text text = CreateText(target.transform, label, 21f, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    static TMP_Text CreateText(Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject target = new GameObject("Text");
        target.transform.SetParent(parent, false);
        TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    void StyleCoinPriceButton(Button button)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = CoinGold;
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.anchoredPosition = new Vector2(17f, 0f);
        }

        CreateCoinIcon(
            button.transform,
            "PriceCoinIcon",
            new Vector2(0.5f, 0.5f),
            new Vector2(-57f, 0f),
            new Vector2(27f, 27f),
            new Vector2(0.5f, 0.5f));
    }

    Image CreateCoinIcon(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        GameObject target = new GameObject(name);
        target.transform.SetParent(parent, false);
        RectTransform rect = target.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = target.AddComponent<Image>();
        image.sprite = coinSprite;
        image.color = CoinGold;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = coinSprite != null;
        return image;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
