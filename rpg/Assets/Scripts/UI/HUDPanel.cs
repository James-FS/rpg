using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Quest;
using FogHarbor.Player;

namespace FogHarbor.UI
{
    /// <summary>
    /// HUD 面板：HP 条、金币、交互提示。常驻屏幕。
    /// §6.1 差距 #3：HP/金币改由事件驱动刷新（OnPlayerHpChanged / OnGoldChanged），不再每帧轮询。
    /// </summary>
    public class HUDPanel : MonoBehaviour
    {
        private Image hpBarFill;
        private TextMeshProUGUI hpText;
        private TextMeshProUGUI goldText;
        private TextMeshProUGUI promptText;
        private TextMeshProUGUI toastText;
        private Coroutine toastCoroutine;

        private PlayerController player;
        private RewardService rewardService;
        private QuestSystem questSystem;

        private static readonly Color HP_COLOR = new Color(0.7f, 0.15f, 0.15f, 1f);
        private static readonly Color HP_BG = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        private static readonly Color GOLD_COLOR = new Color(1f, 0.84f, 0f, 1f);

        public void Initialize(PlayerController player, RewardService rewardService, QuestSystem qs)
        {
            SetPlayer(player);
            SetRewardService(rewardService);
            SetQuestSystem(qs);
        }

        public void SetQuestSystem(QuestSystem qs)
        {
            if (questSystem != null)
                questSystem.OnQuestStateChanged -= OnQuestStateChanged;
            questSystem = qs;
            if (questSystem != null)
                questSystem.OnQuestStateChanged += OnQuestStateChanged;
        }

        /// <summary>任务状态变化：进入 Completed 时给玩家即时提示（拾取/掉落推进后）。</summary>
        private void OnQuestStateChanged(string questId, QuestState state)
        {
            if (state != QuestState.Completed || questSystem == null) return;
            var data = questSystem.GetQuest(questId);
            string title = data != null ? data.Title : questId;
            ShowToast($"任务「{title}」目标已完成！");
        }

        public void SetPlayer(PlayerController pc)
        {
            if (player != null)
                player.OnPlayerHpChanged -= UpdateHp;
            player = pc;
            if (player != null)
            {
                player.OnPlayerHpChanged += UpdateHp;
                UpdateHp(player.CurrentHp, player.MaxHp); // 订阅后立即刷新初值
            }
        }

        public void SetRewardService(RewardService rs)
        {
            if (rewardService != null)
                rewardService.OnGoldChanged -= UpdateGold;
            rewardService = rs;
            if (rewardService != null)
            {
                rewardService.OnGoldChanged += UpdateGold;
                UpdateGold(rewardService.Gold); // 订阅后立即刷新初值
            }
        }

        private void OnDestroy()
        {
            if (player != null)
                player.OnPlayerHpChanged -= UpdateHp;
            if (rewardService != null)
                rewardService.OnGoldChanged -= UpdateGold;
            if (questSystem != null)
                questSystem.OnQuestStateChanged -= OnQuestStateChanged;
        }

        public void UpdateHp(int current, int max)
        {
            if (hpText != null)
                hpText.text = $"HP {current}/{max}";
            if (hpBarFill != null)
                hpBarFill.fillAmount = max > 0 ? (float)current / max : 0f;
        }

        public void UpdateGold(int gold)
        {
            if (goldText != null)
                goldText.text = $"金币 {gold}";
        }

        public void SetPrompt(string text)
        {
            if (promptText != null)
            {
                promptText.text = text ?? "";
                promptText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        /// <summary>短暂显示一条提示（如"获得月光药草 x1"），2 秒后自动消失。</summary>
        public void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            toastText.gameObject.SetActive(true);

            if (toastCoroutine != null)
                StopCoroutine(toastCoroutine);
            toastCoroutine = StartCoroutine(HideToastAfterDelay(2f));
        }

        private System.Collections.IEnumerator HideToastAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            toastText.gameObject.SetActive(false);
            toastCoroutine = null;
        }

        // ─── UI 构建 ───

        private void Awake()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            // ── 左上角：HP 条 + 金币 ──

            var hpContainer = UIHelper.CreateObject("HPContainer", transform);
            var hpRT = hpContainer.GetComponent<RectTransform>();
            hpRT.anchorMin = new Vector2(0, 1);
            hpRT.anchorMax = new Vector2(0, 1);
            hpRT.pivot = new Vector2(0, 1);
            hpRT.anchoredPosition = new Vector2(20, -20);
            hpRT.sizeDelta = new Vector2(250, 60);

            // HP 条背景
            var hpBg = UIHelper.CreateImage("HPBarBg", hpContainer.transform, HP_BG);
            var hpBgRT = hpBg.GetComponent<RectTransform>();
            hpBgRT.anchorMin = new Vector2(0, 1);
            hpBgRT.anchorMax = new Vector2(1, 1);
            hpBgRT.pivot = new Vector2(0, 1);
            hpBgRT.anchoredPosition = new Vector2(0, -25);
            hpBgRT.sizeDelta = new Vector2(0, 20);

            // HP 条填充
            hpBarFill = UIHelper.CreateImage("HPBarFill", hpBg.transform, HP_COLOR);
            hpBarFill.type = Image.Type.Filled;
            hpBarFill.fillMethod = Image.FillMethod.Horizontal;
            var fillRT = hpBarFill.GetComponent<RectTransform>();
            UIHelper.Stretch(fillRT);

            // HP 文字
            hpText = UIHelper.CreateText("HPText", hpContainer.transform,
                "HP 100/100", 18, Color.white, TextAlignmentOptions.Left);
            var hpTextRT = hpText.GetComponent<RectTransform>();
            hpTextRT.anchorMin = new Vector2(0, 1);
            hpTextRT.anchorMax = new Vector2(1, 1);
            hpTextRT.pivot = new Vector2(0, 1);
            hpTextRT.anchoredPosition = new Vector2(0, 0);
            hpTextRT.sizeDelta = new Vector2(0, 22);

            // 金币
            goldText = UIHelper.CreateText("GoldText", hpContainer.transform,
                "金币 20", 20, GOLD_COLOR, TextAlignmentOptions.Left);
            var goldRT = goldText.GetComponent<RectTransform>();
            goldRT.anchorMin = new Vector2(0, 1);
            goldRT.anchorMax = new Vector2(0, 1);
            goldRT.pivot = new Vector2(0, 1);
            goldRT.anchoredPosition = new Vector2(0, -50);
            goldRT.sizeDelta = new Vector2(200, 25);

            // ── 底部中央：交互提示 ──

            promptText = UIHelper.CreateText("PromptText", transform,
                "", 24, Color.white, TextAlignmentOptions.Center);
            var promptRT = promptText.GetComponent<RectTransform>();
            promptRT.anchorMin = new Vector2(0.5f, 0);
            promptRT.anchorMax = new Vector2(0.5f, 0);
            promptRT.pivot = new Vector2(0.5f, 0);
            promptRT.anchoredPosition = new Vector2(0, 40);
            promptRT.sizeDelta = new Vector2(500, 40);
            promptText.gameObject.SetActive(false);

            // ── 屏幕中上方：拾取/系统提示 toast ──

            toastText = UIHelper.CreateText("Toast", transform,
                "", 22, new Color(1f, 0.92f, 0.5f, 1f), TextAlignmentOptions.Center);
            var toastRT = toastText.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 1);
            toastRT.anchorMax = new Vector2(0.5f, 1);
            toastRT.pivot = new Vector2(0.5f, 1);
            toastRT.anchoredPosition = new Vector2(0, -120);
            toastRT.sizeDelta = new Vector2(600, 40);
            toastText.gameObject.SetActive(false);
        }
    }
}
