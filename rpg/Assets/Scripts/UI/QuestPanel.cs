using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Items;
using FogHarbor.Quest;

namespace FogHarbor.UI
{
    /// <summary>
    /// 任务面板：任务列表 + 详情（描述/目标/奖励）。
    /// 按 Q 呼出/关闭。监听 OnQuestStateChanged 自动刷新。
    /// </summary>
    public class QuestPanel : MonoBehaviour
    {
        private QuestSystem questSystem;

        private Transform listContent;
        private TextMeshProUGUI detailText;

        private string selectedQuestId;

        private static readonly Color PANEL_BG = new Color(0, 0, 0, 0.85f);
        private static readonly Color ROW_SELECTED = new Color(0.2f, 0.35f, 0.6f, 0.6f);

        public void Initialize(QuestSystem qs)
        {
            this.questSystem = qs;
            questSystem.OnQuestStateChanged += (questId, state) => Refresh();
        }

        public void Show() { gameObject.SetActive(true); Refresh(); }
        public void Hide() { gameObject.SetActive(false); }
        public void Toggle()
        {
            if (gameObject.activeSelf) Hide();
            else Show();
        }

        // ─── 刷新 ───

        private void Refresh()
        {
            if (!gameObject.activeSelf || questSystem == null) return;

            // 清空
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            // 遍历所有任务（QuestSystem.GetAllQuestIds 公开接口，替代反射读取私有字段，§6.1 差距 #2）
            foreach (var questId in questSystem.GetAllQuestIds())
                CreateQuestRow(questId, questSystem.GetState(questId));

            UpdateDetail();
        }

        private void CreateQuestRow(string questId, QuestState state)
        {
            var data = questSystem.GetQuest(questId);
            string title = data != null ? data.Title : questId;
            string stateText = StateToString(state);

            var row = new GameObject($"Quest_{questId}", typeof(RectTransform));
            row.transform.SetParent(listContent, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, 45);
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(1, 1);
            rowRT.pivot = new Vector2(0.5f, 1);

            var bg = row.AddComponent<Image>();
            bg.color = questId == selectedQuestId ? ROW_SELECTED : new Color(0, 0, 0, 0);

            var tmp = UIHelper.CreateText("Text", row.transform,
                $"{title}  [{stateText}]", 18, Color.white,
                TextAlignmentOptions.Left);
            var tmpRT = tmp.GetComponent<RectTransform>();
            tmpRT.anchorMin = new Vector2(0, 0);
            tmpRT.anchorMax = new Vector2(1, 1);
            tmpRT.offsetMin = new Vector2(15, 0);
            tmpRT.offsetMax = new Vector2(-15, 0);

            var btn = row.AddComponent<Button>();
            btn.onClick.AddListener(() => { selectedQuestId = questId; Refresh(); });
        }

        private void UpdateDetail()
        {
            if (string.IsNullOrEmpty(selectedQuestId))
            {
                detailText.text = "选择一个任务查看详情";
                return;
            }

            var data = questSystem.GetQuest(selectedQuestId);
            if (data == null)
            {
                detailText.text = selectedQuestId;
                return;
            }

            var state = questSystem.GetState(selectedQuestId);
            string targetName = data.TargetItemId;
            var targetData = ItemDatabase.GetById(data.TargetItemId);
            if (targetData != null) targetName = targetData.DisplayName;

            string rewardName = data.RewardItemId;
            var rewardData = ItemDatabase.GetById(data.RewardItemId);
            if (rewardData != null) rewardName = rewardData.DisplayName;

            detailText.text =
                $"{data.Title}\n" +
                $"状态: {StateToString(state)}\n" +
                $"\n{data.Description}\n" +
                $"\n目标: {targetName} x{data.TargetCount}\n" +
                $"奖励: {data.RewardGold} 金币" +
                (!string.IsNullOrEmpty(data.RewardItemId) ? $", {rewardName} x{data.RewardItemCount}" : "");
        }

        private string StateToString(QuestState state) => state switch
        {
            QuestState.Available => "可接取",
            QuestState.Accepted => "进行中",
            QuestState.Completed => "已完成",
            QuestState.Rewarded => "已领奖",
            _ => state.ToString()
        };

        // ─── UI 构建 ───

        private void Awake() => BuildUI();

        private void BuildUI()
        {
            // 背景
            var bg = UIHelper.CreateImage("BG", transform, PANEL_BG);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = new Vector2(500, 450);

            // 标题
            var title = UIHelper.CreateText("Title", transform,
                "任务 (Q)", 28, Color.white, TextAlignmentOptions.Center);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1);
            titleRT.anchorMax = new Vector2(0.5f, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.anchoredPosition = new Vector2(0, -15);
            titleRT.sizeDelta = new Vector2(400, 35);

            // 任务列表
            var scrollObj = UIHelper.CreateObject("QuestScroll", transform);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0.4f);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.pivot = new Vector2(0.5f, 1);
            scrollRT.offsetMin = new Vector2(20, 0);
            scrollRT.offsetMax = new Vector2(-20, -60);

            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = UIHelper.CreateObject("Viewport", scrollObj.transform);
            var vpRT = viewport.GetComponent<RectTransform>();
            UIHelper.Stretch(vpRT);
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            viewport.AddComponent<Image>().color = Color.clear;

            var content = UIHelper.CreateObject("Content", viewport.transform);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRT;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            listContent = content.transform;

            // 详情
            detailText = UIHelper.CreateText("Detail", transform,
                "选择一个任务查看详情", 18, new Color(0.9f, 0.9f, 0.9f, 1f),
                TextAlignmentOptions.Left);
            var detailRT = detailText.GetComponent<RectTransform>();
            detailRT.anchorMin = new Vector2(0, 0.03f);
            detailRT.anchorMax = new Vector2(1, 0.38f);
            detailRT.offsetMin = new Vector2(20, 0);
            detailRT.offsetMax = new Vector2(-20, 0);
        }
    }
}
