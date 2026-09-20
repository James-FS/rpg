using UnityEngine;

namespace BloodlinesUI
{
    /// <summary>
    /// Manager for switching local scenes (GameObject)
    /// </summary>
    public class LocalSceneManager : MonoBehaviour
    {
        [Header("Scene Objects")]
        [SerializeField] private GameObject[] scenes;
        
        [Header("Loading Scene")]
        [SerializeField] private GameObject loadingScene;
        
        private int currentSceneIndex = -1;
        
        /// <summary>
        /// Loads scene by ID
        /// </summary>
        /// <param name="sceneId">Scene ID in array</param>
        public void LoadScene(int sceneId)
        {
            if (sceneId < 0 || sceneId >= scenes.Length)
            {
                return;
            }
            
            if (loadingScene != null)
            {
                loadingScene.SetActive(true);
            }
            
            if (currentSceneIndex >= 0 && currentSceneIndex < scenes.Length)
            {
                scenes[currentSceneIndex].SetActive(false);
            }
            
            scenes[sceneId].SetActive(true);
            currentSceneIndex = sceneId;
            
            if (loadingScene != null)
            {
                loadingScene.SetActive(false);
            }
        }
        
        /// <summary>
        /// Reloads current scene
        /// </summary>
        public void ReloadCurrentScene()
        {
            if (currentSceneIndex >= 0 && currentSceneIndex < scenes.Length)
            {
                LoadScene(currentSceneIndex);
            }
        }
        
        /// <summary>
        /// Gets current scene index
        /// </summary>
        public int GetCurrentSceneIndex()
        {
            return currentSceneIndex;
        }
        
        /// <summary>
        /// Gets number of available scenes
        /// </summary>
        public int GetScenesCount()
        {
            return scenes.Length;
        }
    }
}
