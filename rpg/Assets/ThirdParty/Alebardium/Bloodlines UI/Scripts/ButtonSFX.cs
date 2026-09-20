using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BloodlinesUI
{
    /// <summary>
    /// Component for playing button sound effects
    /// </summary>
    public class ButtonSFX : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        [Header("Sound Effects")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;
        
        [Header("Settings")]
        [SerializeField] private bool playHoverSound = true;
        [SerializeField] private bool playClickSound = true;
        
        private Button button;
        
        private void Start()
        {
            button = GetComponent<Button>();
        }
        
        /// <summary>
        /// Button hover event handler
        /// </summary>
        /// <param name="eventData">Event data</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (playHoverSound && hoverSound != null && button != null && button.interactable)
            {
                if (SoundManager.Instance != null && SoundManager.Instance.IsSoundEnabled())
                {
                    SoundManager.Instance.PlayHoverSound(hoverSound);
                }
            }
        }
        
        /// <summary>
        /// Button click event handler
        /// </summary>
        /// <param name="eventData">Event data</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (playClickSound && clickSound != null && button != null && button.interactable)
            {
                if (SoundManager.Instance != null && SoundManager.Instance.IsSoundEnabled())
                {
                    SoundManager.Instance.PlayClickSound(clickSound);
                }
            }
        }
        
        /// <summary>
        /// Set hover sound
        /// </summary>
        /// <param name="clip">Audio clip</param>
        public void SetHoverSound(AudioClip clip)
        {
            hoverSound = clip;
        }
        
        /// <summary>
        /// Set click sound
        /// </summary>
        /// <param name="clip">Audio clip</param>
        public void SetClickSound(AudioClip clip)
        {
            clickSound = clip;
        }
        
        /// <summary>
        /// Enable/disable hover sound
        /// </summary>
        /// <param name="enabled">Enable sound</param>
        public void SetHoverSoundEnabled(bool enabled)
        {
            playHoverSound = enabled;
        }
        
        /// <summary>
        /// Enable/disable click sound
        /// </summary>
        /// <param name="enabled">Enable sound</param>
        public void SetClickSoundEnabled(bool enabled)
        {
            playClickSound = enabled;
        }
        
        /// <summary>
        /// Force play hover sound
        /// </summary>
        public void PlayHoverSound()
        {
            if (playHoverSound && hoverSound != null)
            {
                if (SoundManager.Instance != null && SoundManager.Instance.IsSoundEnabled())
                {
                    SoundManager.Instance.PlayHoverSound(hoverSound);
                }
            }
        }
        
        /// <summary>
        /// Force play click sound
        /// </summary>
        public void PlayClickSound()
        {
            if (playClickSound && clickSound != null)
            {
                if (SoundManager.Instance != null && SoundManager.Instance.IsSoundEnabled())
                {
                    SoundManager.Instance.PlayClickSound(clickSound);
                }
            }
        }
    }
}
