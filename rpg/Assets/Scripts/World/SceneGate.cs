using UnityEngine;
using UnityEngine.SceneManagement;
using FogHarbor.Player;

namespace FogHarbor.World
{
    /// <summary>
    /// 场景入口：玩家按 E 交互后加载目标场景。
    /// 挂载到场景中的门/入口物体上，设置 targetSceneName。
    /// </summary>
    public class SceneGate : MonoBehaviour, IInteractable
    {
        [Header("场景配置")]
        [SerializeField] private string targetSceneName;
        [SerializeField] private string promptText = "按 E 进入";

        public string GetPrompt() => promptText;

        public void Interact()
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                Debug.Log($"[SceneGate] 切换到场景: {targetSceneName}");
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("[SceneGate] 未设置 targetSceneName！");
            }
        }
    }
}
