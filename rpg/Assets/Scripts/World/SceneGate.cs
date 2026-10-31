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

        public string TargetSceneName => targetSceneName;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
                throw new System.InvalidOperationException($"SceneGate '{name}' requires targetSceneName.");
            if (!SceneManager.GetSceneByName(targetSceneName).IsValid()
                && !Application.CanStreamedLevelBeLoaded(targetSceneName))
                throw new System.InvalidOperationException($"SceneGate '{name}' targets a scene that is not in Build Settings: '{targetSceneName}'.");
        }

        public string GetPrompt() => promptText;

        public void Interact()
        {
            Debug.Log($"[SceneGate] 切换到场景: {targetSceneName}");
            var session = FogHarbor.Session.GameSession.Instance;
            if (!session.ChangeScene(targetSceneName) && !session.IsTransitioning)
                FogHarbor.UI.UIManager.Instance.ShowToast(session.Saves.LastSaveMessage);
        }
    }
}

