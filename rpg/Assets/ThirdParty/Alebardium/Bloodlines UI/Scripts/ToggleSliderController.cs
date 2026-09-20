using UnityEngine;
using UnityEngine.UI;

namespace BloodlinesUI
{
    /// <summary>
    /// Component for controlling slider through toggle
    /// When toggle is off, sets slider to 0
    /// When toggle is on, returns slider to original position
    /// </summary>
    public class ToggleSliderController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Toggle toggle;
        [SerializeField] private Slider slider;
        
        [Header("Settings")]
        [SerializeField] private bool saveValueOnStart = true;
        
        private float originalSliderValue;
        private bool isInitialized = false;
        
        private void Start()
        {
            InitializeComponents();
            
            if (saveValueOnStart)
            {
                SaveOriginalValue();
            }
            
            SubscribeToToggleEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromToggleEvents();
        }
        
        /// <summary>
        /// Component initialization
        /// </summary>
        private void InitializeComponents()
        {
            if (toggle == null)
            {
                toggle = GetComponent<Toggle>();
            }
            
            if (slider == null)
            {
                slider = GetComponentInChildren<Slider>();
            }
            
            if (toggle == null)
            {
                Debug.LogError("[ToggleSliderController] Toggle component not found!");
                return;
            }
            
            if (slider == null)
            {
                Debug.LogError("[ToggleSliderController] Slider component not found!");
                return;
            }
            
            isInitialized = true;
        }
        
        /// <summary>
        /// Save original slider value
        /// </summary>
        public void SaveOriginalValue()
        {
            if (!isInitialized) return;
            
            originalSliderValue = slider.value;
            Debug.Log($"[ToggleSliderController] Original slider value saved: {originalSliderValue}");
        }
        
        /// <summary>
        /// Subscribe to toggle and slider events
        /// </summary>
        private void SubscribeToToggleEvents()
        {
            if (!isInitialized) return;
            
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }
        
        /// <summary>
        /// Unsubscribe from toggle and slider events
        /// </summary>
        private void UnsubscribeFromToggleEvents()
        {
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            }
            
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            }
        }
        
        /// <summary>
        /// Toggle value change handler
        /// </summary>
        /// <param name="isOn">New toggle state</param>
        private void OnToggleValueChanged(bool isOn)
        {
            if (!isInitialized) return;
            
            if (isOn)
            {
                RestoreSliderValue();
            }
            else
            {
                SetSliderToZero();
            }
        }
        
        /// <summary>
        /// Slider value change handler
        /// </summary>
        /// <param name="value">New slider value</param>
        private void OnSliderValueChanged(float value)
        {
            if (!isInitialized) return;
            
            // Update original value only if toggle is on
            if (toggle.isOn)
            {
                originalSliderValue = value;
                Debug.Log($"[ToggleSliderController] Original value updated to: {originalSliderValue}");
            }
        }
        
        /// <summary>
        /// Set slider to zero
        /// </summary>
        public void SetSliderToZero()
        {
            if (!isInitialized) return;
            
            slider.value = 0f;
            Debug.Log("[ToggleSliderController] Slider set to zero");
        }
        
        /// <summary>
        /// Restore original slider value
        /// </summary>
        public void RestoreSliderValue()
        {
            if (!isInitialized) return;
            
            slider.value = originalSliderValue;
            Debug.Log($"[ToggleSliderController] Slider restored to original value: {originalSliderValue}");
        }
        
        /// <summary>
        /// Set new original value
        /// </summary>
        /// <param name="value">New original value</param>
        public void SetOriginalValue(float value)
        {
            originalSliderValue = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            Debug.Log($"[ToggleSliderController] Original value set to: {originalSliderValue}");
        }
        
        /// <summary>
        /// Get current original value
        /// </summary>
        /// <returns>Original slider value</returns>
        public float GetOriginalValue()
        {
            return originalSliderValue;
        }
        
        /// <summary>
        /// Force update slider state based on toggle
        /// </summary>
        public void UpdateSliderState()
        {
            if (!isInitialized) return;
            
            if (toggle.isOn)
            {
                RestoreSliderValue();
            }
            else
            {
                SetSliderToZero();
            }
        }
    }
}
