using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace BloodlinesUI
{
    /// <summary>
    /// Component for managing button text color states based on interaction
    /// </summary>
    public class ButtonTextColorChanger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public TextMeshProUGUI buttonText;

        [Header("Colors")]
        public Color defaultColor = Color.white;
        public Color highlightedColor = Color.gray;
        public Color pressedColor = Color.red;
        public Color disabledColor = Color.gray;

        private bool isPressed = false;
        private bool isHighlighted = false;
        private bool isDisabled = false;
        private Button button;

        void Start()
        {
            button = GetComponent<Button>();
            if (buttonText != null)
                buttonText.color = defaultColor;
        }

        void Update()
        {
            if (button != null)
            {
                bool newDisabledState = !button.interactable;
                if (newDisabledState != isDisabled)
                {
                    isDisabled = newDisabledState;
                    UpdateTextColor();
                }
            }
        }

        /// <summary>
        /// Handle pointer enter event
        /// </summary>
        /// <param name="eventData">Pointer event data</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDisabled) return;
            
            isHighlighted = true;
            if (!isPressed)
                buttonText.color = highlightedColor;
        }

        /// <summary>
        /// Handle pointer exit event
        /// </summary>
        /// <param name="eventData">Pointer event data</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDisabled) return;
            
            isHighlighted = false;
            if (!isPressed)
                buttonText.color = defaultColor;
        }

        /// <summary>
        /// Handle pointer down event
        /// </summary>
        /// <param name="eventData">Pointer event data</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (isDisabled) return;
            
            isPressed = true;
            buttonText.color = pressedColor;
        }

        /// <summary>
        /// Handle pointer up event
        /// </summary>
        /// <param name="eventData">Pointer event data</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDisabled) return;
            
            isPressed = false;
            buttonText.color = isHighlighted ? highlightedColor : defaultColor;
        }

        /// <summary>
        /// Update text color based on current state
        /// </summary>
        private void UpdateTextColor()
        {
            if (buttonText == null) return;

            if (isDisabled)
            {
                buttonText.color = disabledColor;
            }
            else
            {
                if (isPressed)
                    buttonText.color = pressedColor;
                else if (isHighlighted)
                    buttonText.color = highlightedColor;
                else
                    buttonText.color = defaultColor;
            }
        }

        /// <summary>
        /// Enables button
        /// </summary>
        public void EnableButton()
        {
            if (button != null)
            {
                button.interactable = true;
                isDisabled = false;
                UpdateTextColor();
            }
        }

        /// <summary>
        /// Disables button
        /// </summary>
        public void DisableButton()
        {
            if (button != null)
            {
                button.interactable = false;
                isDisabled = true;
                UpdateTextColor();
            }
        }
    }
}
