using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BloodlinesUI
{
    /// <summary>
    /// Component for synchronizing text and slider
    /// Updates text in percentage when slider value changes
    /// </summary>
    public class SliderTextSynchronizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI textComponent;
        
        [Header("Settings")]
        [SerializeField] private bool updateOnStart = true;
        [SerializeField] private string textFormat = "{0}%";
        
        private float lastSliderValue;
        
        private void Start()
        {
            if (slider == null)
                slider = GetComponent<Slider>();
                
            if (updateOnStart)
                UpdateText();
                
            lastSliderValue = slider.value;
        }
        
        private void Update()
        {
            if (slider != null && textComponent != null)
            {
                if (Mathf.Abs(slider.value - lastSliderValue) > 0.001f)
                {
                    UpdateText();
                    lastSliderValue = slider.value;
                }
            }
        }
        
        /// <summary>
        /// Updates text according to the current slider value
        /// </summary>
        private void UpdateText()
        {
            if (slider == null || textComponent == null)
                return;
                
            float percentage = Mathf.Round(slider.value * 100f);
            textComponent.text = string.Format(textFormat, percentage);
        }
        
        /// <summary>
        /// Sets slider for synchronization
        /// </summary>
        /// <param name="newSlider">Slider for synchronization</param>
        public void SetSlider(Slider newSlider)
        {
            slider = newSlider;
            if (slider != null)
                lastSliderValue = slider.value;
        }
        
        /// <summary>
        /// Sets text component for synchronization
        /// </summary>
        /// <param name="newText">Text component</param>
        public void SetTextComponent(TextMeshProUGUI newText)
        {
            textComponent = newText;
        }
        
        /// <summary>
        /// Sets text format
        /// </summary>
        /// <param name="format">Text format (e.g.: "{0}%")</param>
        public void SetTextFormat(string format)
        {
            textFormat = format;
            UpdateText();
        }
        
        /// <summary>
        /// Forces text update
        /// </summary>
        public void ForceUpdateText()
        {
            UpdateText();
        }
    }
}
