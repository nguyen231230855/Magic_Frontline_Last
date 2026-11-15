using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EncounterPanel : MonoBehaviour
{
    RectTransform dim;
    RectTransform sheet;
    Image icon;
    TextMeshProUGUI title;
    TextMeshProUGUI body;
    bool held;

    void Awake()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        dim = Full("EncounterDim", new Color(0f, 0f, 0f, 0.5882353f));
        sheet = Box("EncounterSheet", new Vector2(480f, 320f), new Color(0.1f, 0.08f, 0.12f, 0.96f));
        icon = Box("Icon", new Vector2(96f, 96f), new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
        icon.transform.SetParent(sheet, false);
        icon.rectTransform.anchoredPosition = new Vector2(0f, 78f);
        title = Label("Title", font, 28f, new Vector2(0f, 10f), new Vector2(420f, 40f));
        body = Label("Body", font, 18f, new Vector2(0f, -70f), new Vector2(420f, 120f));
        body.alignment = TextAlignmentOptions.TopLeft;
        var close = Box("CloseBTN", new Vector2(160f, 42f), new Color(0.25f, 0.2f, 0.35f, 1f));
        close.SetParent(sheet, false);
        close.anchoredPosition = new Vector2(0f, -124f);
        var word = Label("Label", font, 20f, Vector2.zero, new Vector2(150f, 36f));
        word.transform.SetParent(close, false);
        word.text = "Đã rõ";
        word.raycastTarget = false;
        close.gameObject.AddComponent<Button>().onClick.AddListener(EncounterBook.Dismiss);
        dim.gameObject.SetActive(false);
        sheet.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        EncounterBook.Shown += Show;
        EncounterBook.Closed += Hide;
        if (EncounterBook.Showing.card != null)
            Show(EncounterBook.Showing);
    }

    void OnDisable()
    {
        EncounterBook.Shown -= Show;
        EncounterBook.Closed -= Hide;
    }

    void Show(EncounterBook.Page page)
    {
        var home = GetComponent<LevelHome>();
        if (home != null && home.ResultOpen)
            return;
        if (!held)
        {
            SpeedUpManager.PushPause();
            held = true;
        }
        icon.sprite = page.icon;
        icon.enabled = page.icon != null;
        title.text = page.card.displayName;
        body.text = "Mạnh: " + page.card.strength + "\nYếu: " + page.card.weakness;
        dim.gameObject.SetActive(true);
        sheet.gameObject.SetActive(true);
        dim.SetAsLastSibling();
        sheet.SetAsLastSibling();
    }

    void Hide()
    {
        if (dim != null)
            dim.gameObject.SetActive(false);
        if (sheet != null)
            sheet.gameObject.SetActive(false);
        if (!held)
            return;
        held = false;
        SpeedUpManager.PopPause();
    }

    RectTransform Full(string name, Color color)
    {
        var rect = Box(name, Vector2.zero, color);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    RectTransform Box(string name, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    TextMeshProUGUI Label(string name, TMP_FontAsset font, float size, Vector2 at, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(sheet != null ? sheet : transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = box;
        rect.anchoredPosition = at;
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
