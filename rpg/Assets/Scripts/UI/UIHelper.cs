using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FogHarbor.UI
{
    /// <summary>
    /// UI 元素创建工具：统一创建 Image / Text / Button，减少重复代码。
    /// </summary>
    public static class UIHelper
    {
        public static GameObject CreateObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = CreateObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent,
            string text, int fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var go = CreateObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button CreateButton(string name, Transform parent,
            string text, int fontSize, Color bgColor, Color textColor)
        {
            var go = CreateObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            btn.colors = colors;

            var tmp = CreateText("Text", go.transform, text, fontSize, textColor,
                TextAlignmentOptions.Center);
            Stretch(tmp.rectTransform);

            return btn;
        }
    }
}
