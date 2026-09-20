using UnityEngine;
using UnityEngine.UI;

namespace BloodlinesUI
{
    /// <summary>
    /// Sound manager for controlling audio settings
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        /// <summary>
        /// Resets static state before each Play session.
        /// Required because 'Fast Enter Play Mode' (default since Unity 6) does not
        /// reload the domain, so static fields keep their values between sessions.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [Header("Audio Components")]
        [SerializeField] private AudioSource audioSource;
        
        [Header("UI Controls")]
        [SerializeField] private Toggle soundToggle;
        [SerializeField] private Slider volumeSlider;
        
        [Header("Default Values")]
        [SerializeField] private float defaultVolume = 0.8f;
        [SerializeField] private bool defaultSoundEnabled = true;
        [SerializeField] private float defaultHoverScale = 0.5f;
        [SerializeField] private float defaultClickScale = 0.7f;
        
        private bool isSoundEnabled = true;
        private float currentVolume = 0.8f;
        private float hoverScale = 0.5f;
        private float clickScale = 0.7f;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad only works on root GameObjects. In the demo scene
                // this manager lives as a UI element under a Canvas, and detaching a UI
                // child from its Canvas would break its rendering. So only persist across
                // scenes when this object is actually a root object.
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            InitializeAudio();
            SetupUIControls();
            SetupAudioSource();
        }
        
        /// <summary>
        /// Initialize audio settings
        /// </summary>
        private void InitializeAudio()
        {
            isSoundEnabled = PlayerPrefs.GetInt("SoundEnabled", defaultSoundEnabled ? 1 : 0) == 1;
            currentVolume = PlayerPrefs.GetFloat("MasterVolume", defaultVolume);
            hoverScale = PlayerPrefs.GetFloat("HoverScale", defaultHoverScale);
            clickScale = PlayerPrefs.GetFloat("ClickScale", defaultClickScale);
            
            ApplyAudioSettings();
        }
        
        /// <summary>
        /// Setup UI control elements
        /// </summary>
        private void SetupUIControls()
        {
            if (soundToggle != null)
            {
                soundToggle.isOn = isSoundEnabled;
                soundToggle.onValueChanged.AddListener(OnSoundToggleChanged);
            }
            
            if (volumeSlider != null)
            {
                volumeSlider.value = currentVolume;
                volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
            }
        }
        
        /// <summary>
        /// Setup AudioSource
        /// </summary>
        private void SetupAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
        
        /// <summary>
        /// Sound toggle state change handler
        /// </summary>
        /// <param name="isEnabled">Is sound enabled</param>
        public void OnSoundToggleChanged(bool isEnabled)
        {
            isSoundEnabled = isEnabled;
            PlayerPrefs.SetInt("SoundEnabled", isEnabled ? 1 : 0);
            PlayerPrefs.Save();
            
            ApplyAudioSettings();
        }
        
        /// <summary>
        /// Volume slider value change handler
        /// </summary>
        /// <param name="volume">New volume value</param>
        public void OnVolumeSliderChanged(float volume)
        {
            currentVolume = volume;
            PlayerPrefs.SetFloat("MasterVolume", volume);
            PlayerPrefs.Save();
            
            ApplyAudioSettings();
        }
        
        /// <summary>
        /// Apply audio settings
        /// </summary>
        private void ApplyAudioSettings()
        {
            if (audioSource != null)
            {
                audioSource.volume = isSoundEnabled ? currentVolume : 0f;
            }
        }
        
        /// <summary>
        /// Play sound
        /// </summary>
        /// <param name="clip">Audio clip</param>
        /// <param name="volume">Volume (0-1)</param>
        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null && isSoundEnabled)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
        
        /// <summary>
        /// Play hover sound
        /// </summary>
        /// <param name="clip">Audio clip</param>
        public void PlayHoverSound(AudioClip clip)
        {
            if (audioSource != null && clip != null && isSoundEnabled)
            {
                audioSource.PlayOneShot(clip, currentVolume * hoverScale);
            }
        }
        
        /// <summary>
        /// Play click sound
        /// </summary>
        /// <param name="clip">Audio clip</param>
        public void PlayClickSound(AudioClip clip)
        {
            if (audioSource != null && clip != null && isSoundEnabled)
            {
                audioSource.PlayOneShot(clip, currentVolume * clickScale);
            }
        }
        
        /// <summary>
        /// Enable/disable sound programmatically
        /// </summary>
        /// <param name="enabled">Enable sound</param>
        public void SetSoundEnabled(bool enabled)
        {
            if (soundToggle != null)
            {
                soundToggle.isOn = enabled;
            }
            else
            {
                OnSoundToggleChanged(enabled);
            }
        }
        
        /// <summary>
        /// Set volume programmatically
        /// </summary>
        /// <param name="volume">Volume value (0-1)</param>
        public void SetVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            
            if (volumeSlider != null)
            {
                volumeSlider.value = volume;
            }
            else
            {
                OnVolumeSliderChanged(volume);
            }
        }
        
        /// <summary>
        /// Set hover volume scale
        /// </summary>
        /// <param name="scale">Scale factor (0-1)</param>
        public void SetHoverScale(float scale)
        {
            hoverScale = Mathf.Clamp01(scale);
            PlayerPrefs.SetFloat("HoverScale", hoverScale);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Set click volume scale
        /// </summary>
        /// <param name="scale">Scale factor (0-1)</param>
        public void SetClickScale(float scale)
        {
            clickScale = Mathf.Clamp01(scale);
            PlayerPrefs.SetFloat("ClickScale", clickScale);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Get current sound state
        /// </summary>
        /// <returns>Is sound enabled</returns>
        public bool IsSoundEnabled()
        {
            return isSoundEnabled;
        }
        
        /// <summary>
        /// Get current volume
        /// </summary>
        /// <returns>Current volume (0-1)</returns>
        public float GetCurrentVolume()
        {
            return currentVolume;
        }
        
        /// <summary>
        /// Get hover volume scale
        /// </summary>
        /// <returns>Hover scale (0-1)</returns>
        public float GetHoverScale()
        {
            return hoverScale;
        }
        
        /// <summary>
        /// Get click volume scale
        /// </summary>
        /// <returns>Click scale (0-1)</returns>
        public float GetClickScale()
        {
            return clickScale;
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (soundToggle != null)
            {
                soundToggle.onValueChanged.RemoveListener(OnSoundToggleChanged);
            }
            
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeSliderChanged);
            }
        }
    }
}
