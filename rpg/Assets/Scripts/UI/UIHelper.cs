using System.Collections.Generic;
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeFontAtlases()
        {
            var visited = new HashSet<TMP_FontAsset>();
            PrepareFontAtlas(TMP_Settings.defaultFontAsset, visited);
            if (TMP_Settings.fallbackFontAssets == null) return;
            foreach (var font in TMP_Settings.fallbackFontAssets)
                PrepareFontAtlas(font, visited);
        }

        public static void PrepareFontAtlases(TMP_FontAsset font)
        {
            PrepareFontAtlas(font, new HashSet<TMP_FontAsset>());
        }

        private static void PrepareFontAtlas(TMP_FontAsset font, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font)) return;

            var atlases = font.atlasTextures;
            if (atlases == null || atlases.Length == 0 || atlases[0] == null)
            {
                Debug.LogWarning($"[UIHelper] Font atlas missing: {font.name}", font);
            }
            else
            {
                // TMP 3.0.9 uses this lazy cache directly when expanding a persistent font's atlas.
                _ = font.atlasTexture;
            }

            if (font.fallbackFontAssetTable == null) return;
            foreach (var fallback in font.fallbackFontAssetTable)
                PrepareFontAtlas(fallback, visited);
        }

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
            PrepareFontAtlases(tmp.font);
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

        /// <summary>
        /// 生成圆角矩形 Sprite（白色 + 九宫格边框），用于没有贴图素材时的圆角块：
        /// 配 Image.type = Sliced 可任意拉伸而不变形；radius = size/2 时即圆形（做圆点用）。
        /// 贴图由代码生成、不落资产，因此只在运行时可用（预制体里保存不了临时 Sprite 引用）。
        /// </summary>
        public static Sprite CreateRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
            {
                name = $"Rounded_{size}_{radius}",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            const int samples = 4;  // 每像素 4×4 超采样，边缘平滑
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples;
                            float py = y + (sy + 0.5f) / samples;
                            if (InsideRoundedRect(px, py, size, radius)) inside++;
                        }
                    }
                    // 透明像素保持白色，避免双线性采样时边缘发黑
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(255f * inside / (samples * samples)));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var border = new Vector4(radius, radius, radius, radius);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool InsideRoundedRect(float px, float py, float size, float radius)
        {
            float cx = Mathf.Clamp(px, radius, size - radius);
            float cy = Mathf.Clamp(py, radius, size - radius);
            float dx = px - cx;
            float dy = py - cy;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
