using UnityEngine;
using UnityEngine.SceneManagement;

namespace FogHarbor.Bootstrap
{
    /// <summary>
    /// 游戏启动入口，挂载在 Boot 场景的 AppRoot 上。
    /// 职责：DontDestroyOnLoad 保护 AppRoot，加载首个游戏场景。
    /// 后续系统（QuestSystem、InventorySystem 等）将作为 AppRoot 子物体挂载。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [Header("启动场景")]
        [SerializeField] private string firstSceneName = "Town";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Boot 场景启动后自动加载第一个游戏场景
            if (SceneManager.GetActiveScene().name == "Boot")
            {
                SceneManager.LoadScene(firstSceneName);
            }
        }
    }
}
