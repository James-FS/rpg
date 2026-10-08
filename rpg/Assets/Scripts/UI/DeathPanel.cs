using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FogHarbor.UI
{
    /// <summary>GameUI death modal, using the shared slate and antique gold palette.</summary>
    public sealed class DeathPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Button reviveButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI buttonText;
        private Action requestRevive;

        public bool IsOpen => gameObject.activeSelf;

        private static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            color.a = alpha;
            return color;
        }

        private void Awake()
        {
            EnsureUI();
            reviveButton.onClick.AddListener(HandleRevive);
        }

        public void Initialize(Action revive)
        {
            EnsureUI();
            requestRevive = revive;
            // Also supports an initially inactive prefab whose Awake has not run yet.
            reviveButton.onClick.RemoveListener(HandleRevive);
            reviveButton.onClick.AddListener(HandleRevive);
        }

        private void HandleRevive()
        {
            if (!reviveButton.interactable) return;
            SetReady(false);
            buttonText.text = "正在返回小镇…";
            requestRevive?.Invoke();
        }

        public void Show(bool ready)
        {
            EnsureUI();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            SetReady(ready);
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetReady(bool ready)
        {
            reviveButton.interactable = ready;
            buttonText.text = ready ? "返回小镇 · 复活" : "请稍候…";
            statusText.text = ready ? "在小镇中恢复全部生命，重新出发。" : "生命的余烬尚未散尽…";
        }

        public void EnsureUI()
        {
            if (panelRoot != null) return;
            var gold = Hex("#C8A96A");
            var body = Hex("#E8E2D6");
            var backdrop = UIHelper.CreateImage("Backdrop", transform, Hex("#0B1017", 0.64f));
            UIHelper.Stretch(backdrop.rectTransform);

            var frame = UIHelper.CreateImage("Panel", transform, gold);
            panelRoot = frame.rectTransform;
            Place(panelRoot, 0, -20, 620, 330);
            var surface = UIHelper.CreateImage("Surface", panelRoot, Hex("#1E2732", 0.97f));
            UIHelper.Stretch(surface.rectTransform);
            surface.rectTransform.offsetMin = new Vector2(2, 2);
            surface.rectTransform.offsetMax = new Vector2(-2, -2);
            var header = UIHelper.CreateImage("Header", panelRoot, Hex("#26313F"));
            Place(header.rectTransform, 0, 124, 616, 78);
            header.raycastTarget = false;
            var rule = UIHelper.CreateImage("Rule", panelRoot, gold);
            Place(rule.rectTransform, 0, 84, 560, 2);
            rule.raycastTarget = false;

            Text("Title", "你已倒下", 34, gold, 0, 125, 540, 55);
            Text("Description", "雾港的灯火，仍在等待你的归来。", 22, body, 0, 35, 550, 44);
            statusText = Text("Status", "生命的余烬尚未散尽…", 18, Hex("#8A94A3"), 0, -10, 550, 42);

            var buttonFrame = UIHelper.CreateImage("ReviveFrame", panelRoot, gold);
            Place(buttonFrame.rectTransform, 0, -91, 310, 60);
            buttonFrame.raycastTarget = false;
            reviveButton = UIHelper.CreateButton("ReviveButton", buttonFrame.transform,
                "返回小镇 · 复活", 23, Color.white, body);
            UIHelper.Stretch(reviveButton.GetComponent<RectTransform>());
            reviveButton.GetComponent<RectTransform>().offsetMin = new Vector2(2, 2);
            reviveButton.GetComponent<RectTransform>().offsetMax = new Vector2(-2, -2);
            reviveButton.targetGraphic = reviveButton.GetComponent<Image>();
            var colors = reviveButton.colors;
            colors.normalColor = Hex("#3C4E68");
            colors.highlightedColor = Hex("#536782");
            colors.pressedColor = Hex("#26313F");
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Hex("#252D38");
            reviveButton.colors = colors;
            buttonText = reviveButton.GetComponentInChildren<TextMeshProUGUI>();
            SetReady(false);
        }

        private TextMeshProUGUI Text(string name, string value, int size, Color color,
            float x, float y, float width, float height)
        {
            var text = UIHelper.CreateText(name, panelRoot, value, size, color, TextAlignmentOptions.Center);
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
