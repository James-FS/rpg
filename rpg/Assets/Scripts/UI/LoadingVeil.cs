using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FogHarbor.UI
{
    /// <summary>
    /// 全屏黑幕加载层：进入游戏的场景切换链路（Title→Boot→Town/Forest）中，
    /// Boot 无相机、Town 为同步加载，会出现无遮罩黑屏；本层在标题淡出后接管画面，
    /// 等真正的游戏场景渲染数帧后自动淡出。DontDestroyOnLoad 跨场景存活。
    /// </summary>
    public class LoadingVeil : MonoBehaviour
    {
        private const float RevealDelayFrames = 3;
        private const float RevealFadeSeconds = 0.35f;
        private const float TimeoutSeconds = 20f;

        public static LoadingVeil Active { get; private set; }

        private CanvasGroup group;

        public static LoadingVeil Show(TMP_FontAsset chineseFont, string message = "正在进入雾港…")
        {
            if (Active != null) return Active;

            var root = new GameObject("LoadingVeil", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            var shadeRect = shade.GetComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.035f, 1f);

            var hintObject = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintObject.transform.SetParent(root.transform, false);
            var hint = hintObject.GetComponent<TextMeshProUGUI>();
            hint.font = chineseFont != null ? chineseFont : TMP_Settings.defaultFontAsset;
            hint.text = message;
            hint.fontSize = 22;
            hint.color = new Color(0.8f, 0.73f, 0.52f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.raycastTarget = false;
            var hintRect = hintObject.GetComponent<RectTransform>();
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.42f);
            hintRect.sizeDelta = new Vector2(500, 40);

            Active = root.AddComponent<LoadingVeil>();
            Active.group = root.GetComponent<CanvasGroup>();
            Active.group.alpha = 1f;
            Active.group.blocksRaycasts = true;
            return Active;
        }

        private IEnumerator Start()
        {
            float waited = 0;
            while (waited < TimeoutSeconds)
            {
                string scene = SceneManager.GetActiveScene().name;
                if (scene != TitleMenu.SceneName && scene != "Boot") break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (waited >= TimeoutSeconds) Debug.LogError("[LoadingVeil] 等待游戏场景超时，强制显示");
            for (int i = 0; i < RevealDelayFrames; i++) yield return null;
            float elapsed = 0;
            while (elapsed < RevealFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / RevealFadeSeconds);
                yield return null;
            }
            Active = null;
            Destroy(gameObject);
        }
    }
}
