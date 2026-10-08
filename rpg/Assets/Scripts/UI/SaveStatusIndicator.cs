using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Save;

namespace FogHarbor.UI
{
    /// <summary>Save feedback uses its own channel, so pickup/combat toasts cannot replace failures.</summary>
    public class SaveStatusIndicator : MonoBehaviour
    {
        private SaveSystem saves;
        private TMP_Text label;
        private CanvasGroup group;
        private float hideAt;
        public string DisplayedMessage => label != null ? label.text : "";
        public bool IsVisible => group != null && group.alpha > 0;
        public void Initialize(SaveSystem system)
        {
            saves = system;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1,1);
            rect.pivot = new Vector2(1,1);
            rect.anchoredPosition = new Vector2(-28,-58);
            rect.sizeDelta = new Vector2(620,64);
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(0.045f,0.08f,0.1f,0.92f);
            image.raycastTarget = false;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.blocksRaycasts = false;
            label = UIHelper.CreateText("SaveResult",transform,"",24,Color.white,TextAlignmentOptions.MidlineRight);
            UIHelper.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16,6);
            label.rectTransform.offsetMax = new Vector2(-16,-6);
            label.enableWordWrapping = false;
            if (saves != null) {
                saves.OnSaveResult += OnResult;
                if (Time.unscaledTime - saves.LastSaveTime < 3f && !string.IsNullOrEmpty(saves.LastSaveMessage))
                    OnResult(saves.LastSaveSucceeded,saves.LastSaveMessage);
            }
        }
        private void OnResult(bool success,string message)
        {
            // A later success may clear a failure, but repeated successes should remain quiet.
            label.text = message;
            label.color = success ? new Color(0.7f,0.86f,0.75f) : new Color(1f,0.57f,0.44f);
            group.alpha = 1;
            hideAt = Time.unscaledTime + (success ? 3f : 10f);
        }
        private void Update()
        {
            if (group != null && Time.unscaledTime >= hideAt)
                group.alpha = 0;
        }
        private void OnDestroy() { if (saves != null) saves.OnSaveResult -= OnResult; }
    }
}