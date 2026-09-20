using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Inventory;
using FogHarbor.Items;
using FogHarbor.Quest;
using FogHarbor.Save;

namespace FogHarbor.UI
{
    /// <summary>
    /// 任务追踪浮窗：常驻屏幕右上的「进行中任务 + 目标进度」。
    /// 只显示 Accepted（进行中）与 Completed（已达成待交付）的任务；没有可追踪任务时整块隐藏。
    /// 刷新全部走事件（任务状态变化 / 背包变化 / 读档完成），不轮询。
    /// 配色与对话面板同源：深色石板底 + 暗金描边，扁平纯色。
    /// </summary>
    public class QuestTrackerPanel : MonoBehaviour
    {
        // UI 引用：用 Prefab 时来自序列化数据（可在 Inspector 上换），纯代码构建时由 BuildUI() 赋值
        [SerializeField] private RectTransform trackRoot;
        [SerializeField] private TextMeshProUGUI contentText;

        private QuestSystem questSystem;
        private InventorySystem inventory;
        private SaveSystem saveSystem;

        private readonly List<string> trackedIds = new();

        // ─── 布局 ───
        private const float TRACK_W = 340f;   // 浮窗宽
        private const float MARGIN  = 24f;    // 距屏幕右边 / 上边
        private const float FRAME_W = 2f;     // 外圈金边粗细
        private const float PAD     = 14f;    // 文字四周内边距
        private const int FONT_HDR   = 15;
        private const int FONT_TITLE = 19;
        private const int FONT_OBJ   = 16;

        // ─── 配色（深色石板 + 暗金；扁平纯色）───
        private static readonly Color GOLD     = Hex("#C8A96A");          // 暗金描边
        private static readonly Color PANEL_BG = Hex("#1E2732", 0.82f);   // 面板底（略透，能看见场景）
        private const string RICH_TITLE = "#D9A441";   // 任务名
        private const string RICH_BODY  = "#E8E2D6";   // 正文（米白）
        private const string RICH_MUTED = "#8A94A3";   // 次要文字
        private const string RICH_DONE  = "#62B36B";   // 已达成（绿）

        private static Color Hex(string hex, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                Debug.LogWarning($"[QuestTrackerPanel] 颜色解析失败：{hex}");
                return Color.magenta;
            }
            c.a = alpha;
            return c;
        }

        // ─── 初始化 ───

        /// <summary>注入系统引用并订阅事件（重复调用会先退订，避免重复刷新）。</summary>
        public void Initialize(QuestSystem qs, InventorySystem inv, SaveSystem save)
        {
            Unsubscribe();

            questSystem = qs;
            inventory = inv;
            saveSystem = save;

            if (questSystem != null) questSystem.OnQuestStateChanged += OnQuestStateChanged;
            if (inventory != null) inventory.OnInventoryChanged += Refresh;
            if (saveSystem != null) saveSystem.OnLoadComplete += Refresh;

            Refresh(); // 订阅后立即刷新初值（读档可能早于本面板创建）
        }

        private void Unsubscribe()
        {
            if (questSystem != null) questSystem.OnQuestStateChanged -= OnQuestStateChanged;
            if (inventory != null) inventory.OnInventoryChanged -= Refresh;
            if (saveSystem != null) saveSystem.OnLoadComplete -= Refresh;
        }

        private void OnDestroy() => Unsubscribe();

        /// <summary>任务状态变化（接取 / 完成 / 领奖）→ 重新组装列表。</summary>
        private void OnQuestStateChanged(string questId, QuestState state) => Refresh();

        // ─── 刷新 ───

        /// <summary>按当前任务状态与背包数量重建浮窗内容；没有可追踪任务时隐藏整块。</summary>
        public void Refresh()
        {
            if (contentText == null || trackRoot == null) return;

            string rich = BuildContent();
            if (string.IsNullOrEmpty(rich))
            {
                trackRoot.gameObject.SetActive(false);
                return;
            }

            trackRoot.gameObject.SetActive(true);
            ApplyLayout(rich);
        }

        private string BuildContent()
        {
            if (questSystem == null) return null;

            CollectTrackedIds();
            if (trackedIds.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append($"<size={FONT_HDR}><color={RICH_MUTED}>任务追踪</color></size>");

            foreach (var questId in trackedIds)
            {
                var def = questSystem.GetQuest(questId);
                if (def == null) continue;

                var state = questSystem.GetState(questId);
                string titleColor = state == QuestState.Completed ? RICH_DONE : RICH_TITLE;

                sb.Append("\n\n");
                sb.Append($"<size={FONT_TITLE}><color={titleColor}><b>{def.Title}</b></color></size>");
                sb.Append('\n');
                sb.Append(BuildObjective(def, state));
            }

            return sb.ToString();
        }

        /// <summary>目标行：有目标物品时显示 名称 已有/需要（集齐转绿），否则只给状态提示。</summary>
        private string BuildObjective(QuestData def, QuestState state)
        {
            bool hasTarget = !string.IsNullOrEmpty(def.TargetItemId) && def.TargetCount > 0;
            if (state == QuestState.Completed)
            {
                string done = hasTarget
                    ? $"· {ItemName(def.TargetItemId)} {Mathf.Min(CurrentCount(def.TargetItemId), def.TargetCount)}/{def.TargetCount} 已集齐，回去交付"
                    : "· 目标达成，回去交付";
                return $"<size={FONT_OBJ}><color={RICH_DONE}>{done}</color></size>";
            }

            if (!hasTarget)
                return $"<size={FONT_OBJ}><color={RICH_MUTED}>· 进行中</color></size>";

            int have = CurrentCount(def.TargetItemId);
            string color = have >= def.TargetCount ? RICH_DONE : RICH_BODY;
            return $"<size={FONT_OBJ}><color={color}>· {ItemName(def.TargetItemId)} {Mathf.Min(have, def.TargetCount)}/{def.TargetCount}</color></size>";
        }

        /// <summary>收集可追踪任务（进行中 / 待交付），主任务排最前，其余按 ID 稳定排序。</summary>
        private void CollectTrackedIds()
        {
            trackedIds.Clear();
            foreach (var questId in questSystem.GetAllQuestIds())
            {
                var state = questSystem.GetState(questId);
                if (state == QuestState.Accepted || state == QuestState.Completed)
                    trackedIds.Add(questId);
            }

            trackedIds.Sort((a, b) =>
            {
                int mainA = IsMain(a) ? 0 : 1;
                int mainB = IsMain(b) ? 0 : 1;
                return mainA != mainB ? mainA - mainB : string.CompareOrdinal(a, b);
            });
        }

        private bool IsMain(string questId)
        {
            var def = questSystem.GetQuest(questId);
            return def != null && def.IsMainQuest;
        }

        private int CurrentCount(string itemId)
        {
            return inventory != null ? inventory.GetCount(itemId) : 0;
        }

        private static string ItemName(string itemId)
        {
            var data = ItemDatabase.GetById(itemId);
            return data != null ? data.DisplayName : itemId;
        }

        /// <summary>按文本实际高度定浮窗尺寸（单 TMP 富文本，不用嵌套布局组，避免高度计算打架）。</summary>
        private void ApplyLayout(string rich)
        {
            float contentW = TRACK_W - (FRAME_W + PAD) * 2f;
            var rt = contentText.rectTransform;

            rt.sizeDelta = new Vector2(contentW, 0f);   // 先定宽，换行结果才准
            contentText.text = rich;
            contentText.ForceMeshUpdate();

            float textH = contentText.preferredHeight;
            rt.sizeDelta = new Vector2(contentW, textH);
            rt.anchoredPosition = new Vector2(FRAME_W + PAD, -(FRAME_W + PAD));

            float totalH = textH + (FRAME_W + PAD) * 2f;
            trackRoot.sizeDelta = new Vector2(TRACK_W, Mathf.Max(totalH, 48f));
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

                // 编辑期（烘焙预制体时）填一段示意内容，方便在层级里直接看与调
                if (!Application.isPlaying)
                    ApplyLayout(PreviewText());
                return;
            }

            if (trackRoot == null || contentText == null)
                Debug.LogWarning("[QuestTrackerPanel] 预制体里有未绑定的 UI 引用，任务追踪会显示不出来，请在 Inspector 上补齐。", this);
        }

        private static string PreviewText()
        {
            return $"<size={FONT_HDR}><color={RICH_MUTED}>任务追踪</color></size>\n\n" +
                   $"<size={FONT_TITLE}><color={RICH_TITLE}><b>寻找月光药草</b></color></size>\n" +
                   $"<size={FONT_OBJ}><color={RICH_BODY}>· 月光药草 0/1</color></size>";
        }

        private void BuildUI()
        {
            // ── 浮窗根：贴屏幕右上，高度随内容（Refresh 里算）──
            var track = UIHelper.CreateObject("Track", transform);
            trackRoot = track.GetComponent<RectTransform>();
            trackRoot.anchorMin = new Vector2(1, 1);
            trackRoot.anchorMax = new Vector2(1, 1);
            trackRoot.pivot = new Vector2(1, 1);
            trackRoot.anchoredPosition = new Vector2(-MARGIN, -MARGIN);
            trackRoot.sizeDelta = new Vector2(TRACK_W, 96f);

            // 金边（外层整块）+ 深色石板底（内缩 FRAME_W，露出边）
            var frame = UIHelper.CreateImage("Frame", track.transform, GOLD);
            UIHelper.Stretch(frame.GetComponent<RectTransform>());
            frame.raycastTarget = false;

            var bg = UIHelper.CreateImage("BG", track.transform, PANEL_BG);
            var bgRT = bg.GetComponent<RectTransform>();
            UIHelper.Stretch(bgRT);
            bgRT.offsetMin = new Vector2(FRAME_W, FRAME_W);
            bgRT.offsetMax = new Vector2(-FRAME_W, -FRAME_W);
            bg.raycastTarget = false;

            // 内容：单个 TMP 富文本（贴左上角，尺寸由 Refresh 按文本高度写回）
            contentText = UIHelper.CreateText("Content", bg.transform, "", FONT_OBJ,
                new Color(1f, 1f, 1f, 1f), TextAlignmentOptions.TopLeft);
            var contentRT = contentText.rectTransform;
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(0, 1);
            contentRT.pivot = new Vector2(0, 1);
            contentRT.anchoredPosition = new Vector2(FRAME_W + PAD, -(FRAME_W + PAD));
            contentRT.sizeDelta = new Vector2(TRACK_W - (FRAME_W + PAD) * 2f, 0f);
            contentText.enableWordWrapping = true;
            contentText.richText = true;
            contentText.lineSpacing = 4f;
        }
    }
}
