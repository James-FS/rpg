using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Items;
using FogHarbor.Inventory;
using FogHarbor.Quest;

namespace FogHarbor.UI
{
    /// <summary>
    /// 任务面板（Q 呼出）：左侧任务清单 + 右侧任务详情（描述 / 目标进度 / 奖励）。
    /// 视觉沿用对话面板那套：深色石板底 + 暗金描边 + 扁平纯色（无纹理 / 渐变）。
    /// 面板根是全屏拉伸的容器，所有内容都放在固定尺寸的 Panel 子物体里，元素一律相对 Panel 定位
    /// （早前版本直接锚在根节点上，标题会跑到屏幕顶部、列表横跨整屏）。
    /// 只读展示：接取 / 完成 / 领奖一律走 GameSession，本面板不推进任何状态。
    /// </summary>
    public class QuestPanel : MonoBehaviour
    {
        private QuestSystem questSystem;
        private InventorySystem inventory;

        // UI 引用：用 Prefab 时来自序列化数据（可在 Inspector 上换），纯代码构建时由 BuildUI() 赋值
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Transform listContent;
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailMetaText;
        [SerializeField] private TextMeshProUGUI detailDescText;
        [SerializeField] private TextMeshProUGUI detailObjectiveText;
        [SerializeField] private TextMeshProUGUI detailRewardText;

        private string selectedQuestId;

        // ─── 布局（与背包面板同规格，保证两块面板观感一致）───
        private const float PANEL_W  = 960f;   // 面板宽
        private const float PANEL_H  = 580f;   // 面板高
        private const float FRAME_W  = 2f;     // 外圈金边粗细
        private const float HEADER_H = 64f;    // 头部栏高
        private const float RULE_H   = 2f;     // 头部下沿金线粗细
        private const float PAD      = 20f;    // 面板内边距
        private const float GAP      = 20f;    // 区块间距
        private const float STRIP_H  = 56f;    // 底部条高
        private const float LIST_W   = 470f;   // 左列：任务清单宽
        private const float ROW_H    = 52f;    // 任务行高
        private const float ROW_GAP  = 4f;     // 任务行间距

        // ─── 字号 ───
        private const float FONT_TITLE = 26f;
        private const float FONT_HINT  = 16f;
        private const float FONT_ROW   = 18f;
        private const float FONT_TAG   = 15f;
        private const float FONT_NAME  = 22f;
        private const float FONT_STAT  = 17f;
        private const float FONT_SMALL = 15f;

        // ─── 配色（深色石板 + 暗金；与对话面板同源）───
        private static readonly Color GOLD      = Hex("#C8A96A");           // 暗金描边
        private static readonly Color PANEL_BG  = Hex("#1E2732", 0.90f);    // 面板底（略透）
        private static readonly Color HEADER_BG = Hex("#26313F", 0.94f);    // 头部栏
        private static readonly Color BOX_BG    = Hex("#161D26", 0.45f);    // 列表 / 详情底
        private static readonly Color STRIP_BG  = Hex("#161D26", 0.55f);    // 底部条底
        private static readonly Color ROW_BG    = Hex("#2A3644", 0.55f);    // 任务行（常态）
        private static readonly Color ROW_SEL   = Hex("#3C4E68", 0.90f);    // 任务行（选中）
        private static readonly Color BODY      = Hex("#E8E2D6");           // 正文
        private static readonly Color MUTED     = Hex("#8A94A3");           // 次要文字

        private const string RICH_GOLD = "#C8A96A";

        private static Color Hex(string hex, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                Debug.LogWarning($"[QuestPanel] 颜色解析失败：{hex}");
                return Color.magenta;
            }
            c.a = alpha;
            return c;
        }

        /// <summary>状态配色：可接取（灰）/ 进行中（金）/ 待交付（绿）/ 已领奖（暗蓝）。</summary>
        private static Color StateColor(QuestState state) => state switch
        {
            QuestState.Available => Hex("#8A94A3"),
            QuestState.Accepted => Hex("#D9A441"),
            QuestState.Completed => Hex("#62B36B"),
            QuestState.Rewarded => Hex("#4F6B8F"),
            _ => MUTED
        };

        private static string StateRichColor(QuestState state) => state switch
        {
            QuestState.Available => "#8A94A3",
            QuestState.Accepted => "#D9A441",
            QuestState.Completed => "#62B36B",
            QuestState.Rewarded => "#4F6B8F",
            _ => "#8A94A3"
        };

        private string StateToString(QuestState state) => state switch
        {
            QuestState.Available => "可接取",
            QuestState.Accepted => "进行中",
            QuestState.Completed => "待交付",   // 目标已达成，等回去交任务领奖（与右上追踪浮窗、底部统计同一说法）
            QuestState.Rewarded => "已领奖",
            _ => state.ToString()
        };

        // ─── 初始化 ───

        public void Initialize(QuestSystem qs, InventorySystem inv = null)
        {
            Unsubscribe();

            questSystem = qs;
            inventory = inv;

            if (questSystem != null) questSystem.OnQuestStateChanged += OnQuestStateChanged;
            Refresh();
        }

        private void Unsubscribe()
        {
            if (questSystem != null) questSystem.OnQuestStateChanged -= OnQuestStateChanged;
        }

        private void OnDestroy() => Unsubscribe();

        private void OnQuestStateChanged(string questId, QuestState state) => Refresh();

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

            // 选中的任务没了（理论上不会）就清空选中
            if (!string.IsNullOrEmpty(selectedQuestId) && questSystem.GetQuest(selectedQuestId) == null)
                selectedQuestId = null;

            RebuildList();
            UpdateDetail();
            UpdateSummary();
        }

        private void RebuildList()
        {
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            var ids = SortedQuestIds();
            if (ids.Count == 0)
            {
                CreateEmptyRow();
                return;
            }

            foreach (var questId in ids)
                CreateQuestRow(questId);
        }

        /// <summary>排序：主线优先 → 进行中 / 待交付 → 可接取 → 已领奖，同级按 ID 稳定排序。</summary>
        private List<string> SortedQuestIds()
        {
            var ids = new List<string>(questSystem.GetAllQuestIds());
            ids.Sort((a, b) =>
            {
                int mainA = IsMain(a) ? 0 : 1;
                int mainB = IsMain(b) ? 0 : 1;
                if (mainA != mainB) return mainA - mainB;

                int priA = StatePriority(questSystem.GetState(a));
                int priB = StatePriority(questSystem.GetState(b));
                if (priA != priB) return priA - priB;

                return string.CompareOrdinal(a, b);
            });
            return ids;
        }

        private static int StatePriority(QuestState state) => state switch
        {
            QuestState.Accepted => 0,
            QuestState.Completed => 1,
            QuestState.Available => 2,
            QuestState.Rewarded => 3,
            _ => 4
        };

        private bool IsMain(string questId)
        {
            var def = questSystem.GetQuest(questId);
            return def != null && def.IsMainQuest;
        }

        private void CreateQuestRow(string questId)
        {
            var data = questSystem.GetQuest(questId);
            string title = data != null ? data.Title : questId;
            var state = questSystem.GetState(questId);

            var row = CreateRowObject($"Row_{questId}");
            var bg = row.GetComponent<Image>();
            bg.color = questId == selectedQuestId ? ROW_SEL : ROW_BG;

            // 左侧状态色条
            var stateBar = UIHelper.CreateImage("StateBar", row.transform, StateColor(state));
            var barRT = stateBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.pivot = new Vector2(0, 0.5f);
            barRT.anchoredPosition = Vector2.zero;
            barRT.sizeDelta = new Vector2(4, 0);
            stateBar.raycastTarget = false;

            // 任务名（主线加"主线"前缀）
            string nameRich = IsMain(questId) ? $"<color={RICH_GOLD}>主线</color>  {title}" : title;
            var nameText = UIHelper.CreateText("Name", row.transform, nameRich, (int)FONT_ROW,
                questId == selectedQuestId ? Color.white : BODY, TextAlignmentOptions.Left);
            var nameRT = nameText.rectTransform;
            nameRT.anchorMin = new Vector2(0, 0);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.offsetMin = new Vector2(16, 0);
            nameRT.offsetMax = new Vector2(-120, 0);

            // 右侧状态标签
            var stateText = UIHelper.CreateText("State", row.transform, StateToString(state), (int)FONT_TAG,
                StateColor(state), TextAlignmentOptions.Right);
            var stateRT = stateText.rectTransform;
            stateRT.anchorMin = new Vector2(1, 0);
            stateRT.anchorMax = new Vector2(1, 1);
            stateRT.pivot = new Vector2(1, 0.5f);
            stateRT.anchoredPosition = new Vector2(-14, 0);
            stateRT.sizeDelta = new Vector2(110, 0);

            var btn = row.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectQuest(questId));
        }

        /// <summary>没有任何任务定义时的占位行。</summary>
        private void CreateEmptyRow()
        {
            var row = CreateRowObject("Empty");
            row.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var text = UIHelper.CreateText("Text", row.transform, "暂无可接任务", (int)FONT_SMALL,
                MUTED, TextAlignmentOptions.Center);
            UIHelper.Stretch(text.rectTransform);
        }

        private RectTransform CreateRowObject(string name)
        {
            var row = UIHelper.CreateObject(name, listContent);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(1, 1);
            rowRT.pivot = new Vector2(0.5f, 1);
            rowRT.sizeDelta = new Vector2(0, ROW_H);
            row.AddComponent<Image>();
            return rowRT;
        }

        private void SelectQuest(string questId)
        {
            selectedQuestId = questId;
            Refresh();
        }

        private void UpdateDetail()
        {
            if (detailNameText == null) return;

            if (string.IsNullOrEmpty(selectedQuestId))
            {
                SetDetail("未选择任务", "点击左侧任务查看详情", "", "", "");
                return;
            }

            var data = questSystem.GetQuest(selectedQuestId);
            if (data == null)
            {
                SetDetail(selectedQuestId, "", "", "", "");
                return;
            }

            var state = questSystem.GetState(selectedQuestId);
            string kind = data.IsMainQuest ? "主线任务" : "支线任务";
            SetDetail(
                data.Title,
                $"{kind}    <color={StateRichColor(state)}>{StateToString(state)}</color>",
                data.Description,
                BuildObjectiveLine(data, state),
                BuildRewardLine(data));
        }

        private void SetDetail(string name, string meta, string desc, string objective, string reward)
        {
            detailNameText.text = name ?? "";
            detailMetaText.text = meta ?? "";
            detailDescText.text = desc ?? "";
            detailObjectiveText.text = objective ?? "";
            detailRewardText.text = reward ?? "";
        }

        /// <summary>目标行：有目标物品时带上当前进度（进行中才有意义，其余只显示需要数量）。</summary>
        private string BuildObjectiveLine(QuestData def, QuestState state)
        {
            if (string.IsNullOrEmpty(def.TargetItemId) || def.TargetCount <= 0)
                return $"<color={RICH_GOLD}>目标</color>  无特定目标";

            string itemName = ItemName(def.TargetItemId);
            if (state == QuestState.Accepted && inventory != null)
            {
                int have = Mathf.Min(inventory.GetCount(def.TargetItemId), def.TargetCount);
                string color = have >= def.TargetCount ? "#62B36B" : "#E8E2D6";
                return $"<color={RICH_GOLD}>目标</color>  {itemName}  <color={color}>{have}/{def.TargetCount}</color>";
            }

            return $"<color={RICH_GOLD}>目标</color>  {itemName}  x{def.TargetCount}";
        }

        private string BuildRewardLine(QuestData def)
        {
            string reward = $"{def.RewardGold} 金币";
            if (!string.IsNullOrEmpty(def.RewardItemId) && def.RewardItemCount > 0)
                reward += $" · {ItemName(def.RewardItemId)} x{def.RewardItemCount}";
            return $"<color={RICH_GOLD}>奖励</color>  {reward}";
        }

        private static string ItemName(string itemId)
        {
            var data = ItemDatabase.GetById(itemId);
            return data != null ? data.DisplayName : itemId;
        }

        /// <summary>底部条：各状态任务数量一览。</summary>
        private void UpdateSummary()
        {
            if (summaryText == null) return;

            int accepted = 0, completed = 0, available = 0, rewarded = 0;
            foreach (var questId in questSystem.GetAllQuestIds())
            {
                switch (questSystem.GetState(questId))
                {
                    case QuestState.Accepted: accepted++; break;
                    case QuestState.Completed: completed++; break;
                    case QuestState.Available: available++; break;
                    case QuestState.Rewarded: rewarded++; break;
                }
            }

            summaryText.text =
                $"<color=#D9A441>进行中</color> {accepted}    " +
                $"<color=#62B36B>待交付</color> {completed}    " +
                $"<color=#8A94A3>可接取</color> {available}    " +
                $"<color=#4F6B8F>已领奖</color> {rewarded}";
        }

        // ─── UI 构建 ───

        private void Awake() => EnsureUI();

        /// <summary>
        /// 确保 UI 层级与引用就绪：空对象（无预制体时）用代码构建一次；
        /// 已有层级（预制体实例）只校验引用、绝不重建，避免出现两套 UI。
        /// 编辑器烘焙工具也调用本方法在编辑模式生成层级。
        /// </summary>
        public void EnsureUI()
        {
            if (transform.childCount == 0)
            {
                BuildUI();
                return;
            }

            if (panelRoot == null || listContent == null || summaryText == null || detailNameText == null
                || detailMetaText == null || detailDescText == null
                || detailObjectiveText == null || detailRewardText == null)
                Debug.LogWarning("[QuestPanel] 预制体里有未绑定的 UI 引用，请检查任务面板的 Inspector 绑定。", this);
        }

        private void BuildUI()
        {
            // ── 面板框：960×580 居中（外圈金边 + 深蓝灰底）──
            var panel = UIHelper.CreateObject("Panel", transform);
            panelRoot = panel.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
            panelRoot.anchoredPosition = Vector2.zero;
            panelRoot.sizeDelta = new Vector2(PANEL_W, PANEL_H);

            foreach (var (edgeName, min, max, pivot, pos, size) in new[]
            {
                ("FrameTop",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, FRAME_W)),
                ("FrameBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, FRAME_W)),
                ("FrameLeft",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(FRAME_W, 0)),
                ("FrameRight",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(FRAME_W, 0)),
            })
            {
                var edge = UIHelper.CreateImage(edgeName, panelRoot, GOLD);
                var rt = edge.GetComponent<RectTransform>();
                rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
                rt.anchoredPosition = pos; rt.sizeDelta = size;
                edge.raycastTarget = false;
            }

            var bg = UIHelper.CreateImage("BG", panelRoot, PANEL_BG);
            var bgRT = bg.GetComponent<RectTransform>();
            UIHelper.Stretch(bgRT);
            bgRT.offsetMin = new Vector2(FRAME_W, FRAME_W);
            bgRT.offsetMax = new Vector2(-FRAME_W, -FRAME_W);
            bg.raycastTarget = false;

            float innerW = PANEL_W - FRAME_W * 2f;   // 956
            float innerH = PANEL_H - FRAME_W * 2f;   // 576
            float detailX = PAD + LIST_W + GAP;                       // 510
            float detailW = innerW - PAD - detailX;                   // 426
            float bodyY = HEADER_H + GAP;                             // 84
            float bodyH = innerH - bodyY - STRIP_H - GAP;             // 416

            BuildHeader(bgRT);
            BuildList(bgRT, PAD, bodyY, LIST_W, bodyH);
            BuildDetail(bgRT, detailX, bodyY, detailW, bodyH);
            BuildStrip(bgRT, innerW);
        }

        private void BuildHeader(RectTransform parent)
        {
            var header = UIHelper.CreateImage("Header", parent, HEADER_BG);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = Vector2.zero;
            headerRT.sizeDelta = new Vector2(0, HEADER_H);
            header.raycastTarget = false;

            var title = UIHelper.CreateText("Title", header.transform, "任务", (int)FONT_TITLE, BODY, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var titleRT = title.rectTransform;
            titleRT.anchorMin = new Vector2(0, 0.5f);
            titleRT.anchorMax = new Vector2(0, 0.5f);
            titleRT.pivot = new Vector2(0, 0.5f);
            titleRT.anchoredPosition = new Vector2(PAD, 0);
            titleRT.sizeDelta = new Vector2(300, 34);

            var hint = UIHelper.CreateText("Hint", header.transform, "按 Q 关闭", (int)FONT_HINT, MUTED, TextAlignmentOptions.Right);
            var hintRT = hint.rectTransform;
            hintRT.anchorMin = new Vector2(1, 0.5f);
            hintRT.anchorMax = new Vector2(1, 0.5f);
            hintRT.pivot = new Vector2(1, 0.5f);
            hintRT.anchoredPosition = new Vector2(-PAD, 0);
            hintRT.sizeDelta = new Vector2(160, 24);

            var rule = UIHelper.CreateImage("HeaderRule", parent, GOLD);
            var ruleRT = rule.GetComponent<RectTransform>();
            ruleRT.anchorMin = new Vector2(0, 1);
            ruleRT.anchorMax = new Vector2(1, 1);
            ruleRT.pivot = new Vector2(0.5f, 1);
            ruleRT.anchoredPosition = new Vector2(0, -HEADER_H);
            ruleRT.sizeDelta = new Vector2(0, RULE_H);
            rule.raycastTarget = false;
        }

        /// <summary>左列：任务清单（ScrollRect + RectMask2D 裁剪 + 自适应内容高度）。</summary>
        private void BuildList(RectTransform parent, float x, float topY, float w, float h)
        {
            var list = UIHelper.CreateObject("QuestList", parent);
            var listRT = list.GetComponent<RectTransform>();
            listRT.anchorMin = new Vector2(0, 1);
            listRT.anchorMax = new Vector2(0, 1);
            listRT.pivot = new Vector2(0, 1);
            listRT.anchoredPosition = new Vector2(x, -topY);
            listRT.sizeDelta = new Vector2(w, h);

            var listBg = list.AddComponent<Image>();
            listBg.color = BOX_BG;
            listBg.raycastTarget = false;

            var scroll = list.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;

            var viewport = UIHelper.CreateObject("Viewport", list.transform);
            var vpRT = viewport.GetComponent<RectTransform>();
            UIHelper.Stretch(vpRT);
            vpRT.offsetMin = new Vector2(8, 8);
            vpRT.offsetMax = new Vector2(-8, -8);
            // 用 RectMask2D 而非 Mask：运行时创建的透明 Mask 在团结引擎有 stencil 渲染兼容问题（子内容不渲染）
            viewport.AddComponent<RectMask2D>();

            var content = UIHelper.CreateObject("Content", viewport.transform);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 0);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = ROW_GAP;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRT;
            scroll.viewport = vpRT;
            listContent = content.transform;
        }

        /// <summary>右列：任务详情（标题 / 类型与状态 / 描述 / 目标 / 奖励）。</summary>
        private void BuildDetail(RectTransform parent, float x, float topY, float w, float h)
        {
            var box = UIHelper.CreateObject("Detail", parent);
            var boxRT = box.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0, 1);
            boxRT.anchorMax = new Vector2(0, 1);
            boxRT.pivot = new Vector2(0, 1);
            boxRT.anchoredPosition = new Vector2(x, -topY);
            boxRT.sizeDelta = new Vector2(w, h);

            var boxBg = box.AddComponent<Image>();
            boxBg.color = BOX_BG;
            boxBg.raycastTarget = false;

            detailNameText = UIHelper.CreateText("Name", box.transform, "未选择任务", (int)FONT_NAME, BODY, TextAlignmentOptions.Left);
            SetTopLeft(detailNameText.rectTransform, 16, -14, w - 32, 30);

            detailMetaText = UIHelper.CreateText("Meta", box.transform, "点击左侧任务查看详情", (int)FONT_SMALL, MUTED, TextAlignmentOptions.Left);
            SetTopLeft(detailMetaText.rectTransform, 16, -48, w - 32, 24);

            AddThinRule(box.transform, w, -80);

            detailDescText = UIHelper.CreateText("Desc", box.transform, "", (int)FONT_SMALL, BODY, TextAlignmentOptions.TopLeft);
            SetTopLeft(detailDescText.rectTransform, 16, -92, w - 32, 104);

            AddThinRule(box.transform, w, -204);

            detailObjectiveText = UIHelper.CreateText("Objective", box.transform, "", (int)FONT_STAT, BODY, TextAlignmentOptions.Left);
            SetTopLeft(detailObjectiveText.rectTransform, 16, -216, w - 32, 26);

            detailRewardText = UIHelper.CreateText("Reward", box.transform, "", (int)FONT_STAT, BODY, TextAlignmentOptions.Left);
            SetTopLeft(detailRewardText.rectTransform, 16, -248, w - 32, 26);
        }

        /// <summary>底部条：各状态任务数量一览。</summary>
        private void BuildStrip(RectTransform parent, float innerW)
        {
            var strip = UIHelper.CreateImage("Strip", parent, STRIP_BG);
            var stripRT = strip.GetComponent<RectTransform>();
            stripRT.anchorMin = new Vector2(0, 0);
            stripRT.anchorMax = new Vector2(1, 0);
            stripRT.pivot = new Vector2(0.5f, 0);
            stripRT.anchoredPosition = Vector2.zero;
            stripRT.sizeDelta = new Vector2(0, STRIP_H);
            strip.raycastTarget = false;

            var rule = UIHelper.CreateImage("StripRule", strip.transform, GOLD);
            var ruleRT = rule.GetComponent<RectTransform>();
            ruleRT.anchorMin = new Vector2(0, 1);
            ruleRT.anchorMax = new Vector2(1, 1);
            ruleRT.pivot = new Vector2(0.5f, 1);
            ruleRT.anchoredPosition = Vector2.zero;
            ruleRT.sizeDelta = new Vector2(0, RULE_H);
            rule.raycastTarget = false;

            summaryText = UIHelper.CreateText("Summary", strip.transform, "", (int)FONT_SMALL, BODY, TextAlignmentOptions.Left);
            var summaryRT = summaryText.rectTransform;
            summaryRT.anchorMin = new Vector2(0, 0.5f);
            summaryRT.anchorMax = new Vector2(0, 0.5f);
            summaryRT.pivot = new Vector2(0, 0.5f);
            summaryRT.anchoredPosition = new Vector2(PAD, 0);
            summaryRT.sizeDelta = new Vector2(innerW - PAD * 2, 24);
        }

        // ─── 小工具 ───

        private static void SetTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void AddThinRule(Transform parent, float width, float y)
        {
            var rule = UIHelper.CreateImage("Rule", parent, Hex("#C8A96A", 0.35f));
            SetTopLeft(rule.GetComponent<RectTransform>(), 16, y, width - 32, 1);
            rule.raycastTarget = false;
        }
    }
}
