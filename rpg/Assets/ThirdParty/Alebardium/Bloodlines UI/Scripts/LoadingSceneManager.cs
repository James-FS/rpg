using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace BloodlinesUI
{
    /// <summary>
    /// Loading scene manager, controlling loading animation
    /// </summary>
    public class LoadingSceneManager : MonoBehaviour
    {
        [Header("Loading Components")]
        [SerializeField] private Slider loadingSlider;
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private GameObject[] objectsToHide;
        
        [Header("Animation Settings")]
        [SerializeField] private float loadingDuration = 3f;
        [SerializeField] private float hideDelay = 1f;
        
        [Header("Text Settings")]
        [SerializeField] private string loadingTextFormat = "{0}%";
        
        private Coroutine loadingCoroutine;
        private Coroutine hideCoroutine;
        
        private void Start()
        {
            StartLoading();
        }
        
        /// <summary>
        /// Starts loading animation
        /// </summary>
        public void StartLoading()
        {
            if (loadingCoroutine != null)
            {
                StopCoroutine(loadingCoroutine);
            }
            
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
            
            loadingCoroutine = StartCoroutine(LoadingAnimation());
            hideCoroutine = StartCoroutine(HideObjectsAfterDelay());
        }
        
        /// <summary>
        /// Stops loading animation
        /// </summary>
        public void StopLoading()
        {
            if (loadingCoroutine != null)
            {
                StopCoroutine(loadingCoroutine);
                loadingCoroutine = null;
            }
            
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }
        }
        
        /// <summary>
        /// Loading animation for slider and text
        /// </summary>
        private IEnumerator LoadingAnimation()
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < loadingDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / loadingDuration;
                
                UpdateSlider(progress);
                UpdateText(progress);
                
                yield return null;
            }
            
            UpdateSlider(1f);
            UpdateText(1f);
        }
        
        /// <summary>
        /// Hiding objects after specified delay
        /// </summary>
        private IEnumerator HideObjectsAfterDelay()
        {
            yield return new WaitForSeconds(loadingDuration + hideDelay);
            
            HideObjects();
        }
        
        /// <summary>
        /// Updates slider value
        /// </summary>
        private void UpdateSlider(float progress)
        {
            if (loadingSlider != null)
            {
                loadingSlider.value = progress;
            }
        }
        
        /// <summary>
        /// Updates loading text
        /// </summary>
        private void UpdateText(float progress)
        {
            if (loadingText != null)
            {
                int percentage = Mathf.RoundToInt(progress * 100);
                loadingText.text = string.Format(loadingTextFormat, percentage);
            }
        }
        
        /// <summary>
        /// Hides specified objects
        /// </summary>
        private void HideObjects()
        {
            if (objectsToHide != null)
            {
                foreach (GameObject obj in objectsToHide)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                    }
                }
            }
        }
        
        /// <summary>
        /// Shows specified objects
        /// </summary>
        public void ShowObjects()
        {
            if (objectsToHide != null)
            {
                foreach (GameObject obj in objectsToHide)
                {
                    if (obj != null)
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }
        
        /// <summary>
        /// Sets loading duration
        /// </summary>
        public void SetLoadingDuration(float duration)
        {
            loadingDuration = Mathf.Max(0.1f, duration);
        }
        
        /// <summary>
        /// Sets hide delay for objects
        /// </summary>
        public void SetHideDelay(float delay)
        {
            hideDelay = Mathf.Max(0f, delay);
        }
        
        private void OnDestroy()
        {
            StopLoading();
        }
    }
}
