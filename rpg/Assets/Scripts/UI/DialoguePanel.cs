using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AIBot.Unity;
using AIBot.Core.Output;
using FogHarbor.Relationship;

namespace FogHarbor.UI
{
    /// <summary>
    /// 对话面板：漂亮外壳（头部栏/状态灯/输入框/发送按钮）+ 可靠的消息区。
    /// 消息区使用单个 TMP 富文本（玩家金色、NPC 浅蓝），可滚动，自动滚到底部。
    /// 流式回复实时追加。
    /// 头部栏显示当前 NPC 好感（§6.1 模板④：订阅 OnFavorChanged 实时刷新）。
    /// </summary>
    public class DialoguePanel : MonoBehaviour
    {
        // ─── UI 引用 ───
        private TextMeshProUGUI npcNameText;
        private TextMeshProUGUI favorText;
        private Image statusDot;
        private TextMeshProUGUI statusText;
        private ScrollRect scrollRect;
        private TextMeshProUGUI messageText;
        private TMP_InputField inputField;
        private Button sendButton;

        // ─── 对话状态 ───
        private NpcAgent agent;
        private RelationshipSystem relationshipSystem;
        private string currentNpcId;
        private readonly StringBuilder transcript = new StringBuilder();
        private readonly StringBuilder streaming = new StringBuilder();
        private float nextRefreshAt;
        private bool refreshQueued;

        // ─── 配色 ───
        private static readonly Color PANEL_BG      = new Color(0.05f, 0.06f, 0.09f, 0.95f);
        private static readonly Color HEADER_BG     = new Color(0.10f, 0.14f, 0.22f, 0.95f);
        private static readonly Color NPC_NAME_COL  = new Color(1f, 0.90f, 0.55f, 1f);
        private static readonly Color INPUT_BG      = new Color(0.12f, 0.13f, 0.16f, 1f);
        private static readonly Color SEND_BTN      = new Color(0.20f, 0.45f, 0.75f, 1f);
        private static readonly Color ST_READY      = new Color(0.35f, 0.85f, 0.40f, 1f);
        private static readonly Color ST_THINKING   = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color ST_ERROR      = new Color(1f, 0.40f, 0.40f, 1f);

        // 富文本颜色
        private const string PLAYER_COLOR = "#FFE8A3";
        private const string NPC_COLOR    = "#D4E8FF";
        private const string ERROR_COLOR  = "#FF7777";

        // ─── 面板控制 ───

        public void Show(string npcName, NpcAgent npcAgent)
        {
            agent = npcAgent;
            gameObject.SetActive(true);

            npcNameText.text = npcName ?? "NPC";
            ClearMessages();
            inputField.text = "";
            SetStatus("Ready", ST_READY);
            BindFavor();
            inputField.ActivateInputField();
        }

        public void ShowSkeleton(string npcName)
        {
            gameObject.SetActive(true);
            npcNameText.text = npcName ?? "NPC";
            ClearMessages();
            AppendLine("（对话系统未绑定 NpcAgent）", ERROR_COLOR);
            SetStatus("Ready", ST_READY);
        }

        public void Hide()
        {
            // 面板关闭不代表 NPC 请求应继续占用；取消后由 onCancelled 统一复位 UI。
            agent?.CancelRunning();
            UnbindFavor();
            gameObject.SetActive(false);
        }

        // ─── 好感度显示（§6.1 模板④：订阅 OnFavorChanged）───

        private void BindFavor()
        {
            UnbindFavor();
            relationshipSystem = relationshipSystem ?? FindObjectOfType<RelationshipSystem>();
            currentNpcId = ResolveNpcId();
            if (relationshipSystem == null || string.IsNullOrEmpty(currentNpcId))
            {
                SetFavorText(null);
                return;
            }
            relationshipSystem.OnFavorChanged += OnFavorChanged;
            SetFavorText(relationshipSystem.GetFavor(currentNpcId));
        }

        private void UnbindFavor()
        {
            if (relationshipSystem != null)
                relationshipSystem.OnFavorChanged -= OnFavorChanged;
            relationshipSystem = null;
            currentNpcId = null;
        }

        private string ResolveNpcId()
        {
            if (agent != null)
            {
                if (agent.connectionProfile != null && !string.IsNullOrEmpty(agent.connectionProfile.npcId))
                    return agent.connectionProfile.npcId;
                if (!string.IsNullOrEmpty(agent.npcId))
                    return agent.npcId;
            }
            return null;
        }

        private void OnFavorChanged(string npcId, int favor)
        {
            if (!string.IsNullOrEmpty(currentNpcId) && npcId == currentNpcId)
                SetFavorText(favor);
        }

        private void SetFavorText(int? favor)
        {
            if (favorText == null) return;
            favorText.text = favor.HasValue ? $"好感 {favor.Value}" : "";
            favorText.gameObject.SetActive(favor.HasValue);
        }

        // ─── 事件订阅 ───

        private void OnEnable()
        {
            if (agent != null)
            {
                agent.onToken.AddListener(OnToken);
                agent.onReply.AddListener(OnReply);
                agent.onError.AddListener(OnError);
                agent.onFallback.AddListener(OnFallback);
                agent.onCancelled.AddListener(OnCancelled);
                agent.onBusy.AddListener(OnBusy);
            }
        }

        private void OnDisable()
        {
            if (agent != null)
            {
                agent.onToken.RemoveListener(OnToken);
                agent.onReply.RemoveListener(OnReply);
                agent.onError.RemoveListener(OnError);
                agent.onFallback.RemoveListener(OnFallback);
                agent.onCancelled.RemoveListener(OnCancelled);
                agent.onBusy.RemoveListener(OnBusy);
            }
            UnbindFavor();
        }

        // ─── AIBot 事件 ───

        private void OnToken(string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;
            streaming.Append(delta);
            RefreshMessage(false);
        }

        private void OnReply(StructuredReply reply)
        {
            if (streaming.Length == 0 && reply != null && !string.IsNullOrEmpty(reply.say))
            {
                streaming.Append(reply.say);
            }
            FinishStreaming();
            SetStatus("Ready", ST_READY);
        }

        private void OnError(string message)
        {
            FinishStreaming();
            AppendLine($"（对话出错：{message}）", ERROR_COLOR);
            SetStatus("Error", ST_ERROR);
        }

        private void OnFallback(string reason)
        {
            // fallback 仍然会通过 onReply 正常显示；这里补充诊断，不把可用回复误判为终止错误。
            if (!string.IsNullOrEmpty(reason))
                AppendLine($"（模型暂时不可用，已使用兜底回复：{reason}）", ERROR_COLOR);
            SetStatus("Ready", ST_READY);
        }

        private void OnCancelled()
        {
            FinishStreaming();
            SetStatus("Ready", ST_READY);
        }

        private void OnBusy()
        {
            SetStatus("上一轮未结束...", ST_THINKING);
        }

        // ─── 发送 ───

        private void OnSend()
        {
            if (agent == null)
            {
                SetStatus("错误：NPC 未绑定", ST_ERROR);
                return;
            }
            string text = inputField.text.Trim();
            if (text.Length == 0) return;

            AppendLine(text, PLAYER_COLOR, "你");
            inputField.text = "";

            if (streaming.Length > 0) FinishStreaming();
            // 开始流式：先建一条空的 NPC 行
            streaming.Length = 0;
            StartStreamingLine();
            RefreshMessage(true);

            SetStatus("Thinking...", ST_THINKING);
            agent.Chat(text);
        }

        // ─── 消息管理 ───

        private void ClearMessages()
        {
            transcript.Length = 0;
            streaming.Length = 0;
            RefreshMessage();
        }

        private void AppendLine(string content, string color, string speaker = null)
        {
            string name = speaker ?? npcNameText.text;
            transcript.AppendLine($"<color={color}>{name}：</color>{content}");
            RefreshMessage();
        }

        private void StartStreamingLine()
        {
            transcript.AppendLine($"<color={NPC_COLOR}>{npcNameText.text}：</color>");
        }

        private void Update()
        {
            if (refreshQueued && Time.unscaledTime >= nextRefreshAt)
                RefreshMessage(true);
        }

        private void RefreshMessage(bool immediate = true)
        {
            if (messageText == null) return;
            if (!immediate && Time.unscaledTime < nextRefreshAt)
            {
                refreshQueued = true;
                return;
            }
            messageText.text = streaming.Length > 0
                ? transcript.ToString() + streaming
                : transcript.ToString();
            nextRefreshAt = Time.unscaledTime + 0.05f;
            refreshQueued = false;
            ScrollToBottom();
        }

        private void FinishStreaming()
        {
            RefreshMessage();
            streaming.Length = 0;
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (scrollRect != null)
                Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private void SetStatus(string text, Color color)
        {
            if (statusText != null) statusText.text = text;
            if (statusDot != null) statusDot.color = color;
        }

        // ─── UI 构建 ───

        private void Awake() => BuildUI();

        private void BuildUI()
        {
            // ── 外层边框 ──
            var border = UIHelper.CreateImage("Border", transform, new Color(0.30f, 0.35f, 0.45f, 1f));
            var borderRT = border.GetComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(0.5f, 0);
            borderRT.anchorMax = new Vector2(0.5f, 0);
            borderRT.pivot = new Vector2(0.5f, 0);
            borderRT.anchoredPosition = new Vector2(0, 30);
            borderRT.sizeDelta = new Vector2(816, 376);

            // ── 主背景 ──
            var bg = UIHelper.CreateImage("BG", border.transform, PANEL_BG);
            var bgRT = bg.GetComponent<RectTransform>();
            UIHelper.Stretch(bgRT);
            bgRT.offsetMin = new Vector2(2, 2);
            bgRT.offsetMax = new Vector2(-2, -2);

            // ── 头部栏 ──
            var header = UIHelper.CreateImage("Header", bg.transform, HEADER_BG);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0, 1);
            headerRT.sizeDelta = new Vector2(0, 52);

            // 头像占位（色块）
            var avatar = UIHelper.CreateImage("Avatar", header.transform, new Color(0.28f, 0.55f, 0.35f, 1f));
            var avatarRT = avatar.GetComponent<RectTransform>();
            avatarRT.anchorMin = new Vector2(0, 0.5f);
            avatarRT.anchorMax = new Vector2(0, 0.5f);
            avatarRT.pivot = new Vector2(0, 0.5f);
            avatarRT.anchoredPosition = new Vector2(16, 0);
            avatarRT.sizeDelta = new Vector2(36, 36);

            // NPC 名
            npcNameText = UIHelper.CreateText("NpcName", header.transform,
                "NPC", 22, NPC_NAME_COL, TextAlignmentOptions.Left);
            var nameRT = npcNameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(0, 0.5f);
            nameRT.pivot = new Vector2(0, 0.5f);
            nameRT.anchoredPosition = new Vector2(62, 3);
            nameRT.sizeDelta = new Vector2(300, 30);

            // 好感度（模板④：显示当前 NPC 好感）
            favorText = UIHelper.CreateText("Favor", header.transform,
                "", 16, new Color(1f, 0.80f, 0.85f, 1f), TextAlignmentOptions.Left);
            var favorRT = favorText.GetComponent<RectTransform>();
            favorRT.anchorMin = new Vector2(0, 0.5f);
            favorRT.anchorMax = new Vector2(0, 0.5f);
            favorRT.pivot = new Vector2(0, 0.5f);
            favorRT.anchoredPosition = new Vector2(380, 0);
            favorRT.sizeDelta = new Vector2(120, 24);
            favorText.gameObject.SetActive(false);

            // 状态灯
            statusDot = UIHelper.CreateImage("StatusDot", header.transform, ST_READY);
            var dotRT = statusDot.GetComponent<RectTransform>();
            dotRT.anchorMin = new Vector2(1, 0.5f);
            dotRT.anchorMax = new Vector2(1, 0.5f);
            dotRT.pivot = new Vector2(1, 0.5f);
            dotRT.anchoredPosition = new Vector2(-16, 4);
            dotRT.sizeDelta = new Vector2(12, 12);

            // 状态文字
            statusText = UIHelper.CreateText("Status", header.transform,
                "Ready", 15, ST_READY, TextAlignmentOptions.Right);
            var statusRT = statusText.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(1, 0.5f);
            statusRT.anchorMax = new Vector2(1, 0.5f);
            statusRT.pivot = new Vector2(1, 0.5f);
            statusRT.anchoredPosition = new Vector2(-36, 0);
            statusRT.sizeDelta = new Vector2(150, 22);

            // ── 消息滚动区（单 TMP 文本） ──
            var scrollObj = UIHelper.CreateObject("MessageScroll", bg.transform);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0.16f);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.pivot = new Vector2(0.5f, 1);
            scrollRT.offsetMin = new Vector2(4, 0);
            scrollRT.offsetMax = new Vector2(-4, -58);

            scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 40f;

            var scrollImg = scrollObj.AddComponent<Image>();
            scrollImg.color = Color.clear;
            scrollImg.raycastTarget = false;

            var viewport = UIHelper.CreateObject("Viewport", scrollObj.transform);
            var vpRT = viewport.GetComponent<RectTransform>();
            UIHelper.Stretch(vpRT);
            // 用 RectMask2D 而非 Mask：运行时创建的透明 Mask 在团结引擎有 stencil 渲染兼容问题
            viewport.AddComponent<RectMask2D>();

            // 消息文本（顶部对齐，全宽）
            messageText = UIHelper.CreateText("Message", viewport.transform,
                "", 18, Color.white, TextAlignmentOptions.TopLeft);
            messageText.enableWordWrapping = true;
            messageText.richText = true;
            messageText.raycastTarget = false;

            var msgRT = messageText.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0, 1);
            msgRT.anchorMax = new Vector2(1, 1);
            msgRT.pivot = new Vector2(0.5f, 1);
            msgRT.anchoredPosition = new Vector2(0, 0);
            msgRT.sizeDelta = new Vector2(0, 0);

            // 文本高度随内容增长（ContentSizeFitter）
            var msgCsf = messageText.gameObject.AddComponent<ContentSizeFitter>();
            msgCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ScrollRect 绑定
            scrollRect.content = msgRT;
            scrollRect.viewport = vpRT;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // ── 输入区 ──
            var inputObj = UIHelper.CreateObject("Input", bg.transform);
            var inputRT = inputObj.GetComponent<RectTransform>();
            inputRT.anchorMin = new Vector2(0, 0.10f);
            inputRT.anchorMax = new Vector2(0.78f, 0.10f);
            inputRT.pivot = new Vector2(0, 0.5f);
            inputRT.anchoredPosition = new Vector2(14, 0);
            inputRT.sizeDelta = new Vector2(-28, 38);

            var inputBorder = UIHelper.CreateImage("InputBorder", inputObj.transform,
                new Color(0.30f, 0.35f, 0.42f, 1f));
            var inBorderRT = inputBorder.GetComponent<RectTransform>();
            UIHelper.Stretch(inBorderRT);
            var inputBg = UIHelper.CreateImage("Bg", inputBorder.transform, INPUT_BG);
            var inBgRT = inputBg.GetComponent<RectTransform>();
            UIHelper.Stretch(inBgRT);
            inBgRT.offsetMin = new Vector2(1, 1);
            inBgRT.offsetMax = new Vector2(-1, -1);

            inputField = inputBorder.gameObject.AddComponent<TMP_InputField>();

            var inputTextObj = UIHelper.CreateObject("Text", inputBorder.transform);
            var inputTextRT = inputTextObj.GetComponent<RectTransform>();
            UIHelper.Stretch(inputTextRT);
            inputTextRT.offsetMin = new Vector2(12, 4);
            inputTextRT.offsetMax = new Vector2(-12, -4);
            inputField.textComponent = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputField.textComponent.fontSize = 18;
            inputField.textComponent.color = Color.white;

            var placeholderObj = UIHelper.CreateObject("Placeholder", inputBorder.transform);
            var placeholderRT = placeholderObj.GetComponent<RectTransform>();
            UIHelper.Stretch(placeholderRT);
            placeholderRT.offsetMin = new Vector2(12, 4);
            placeholderRT.offsetMax = new Vector2(-12, -4);
            inputField.placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
            (inputField.placeholder as TextMeshProUGUI).text = "对林洛说点什么...";
            (inputField.placeholder as TextMeshProUGUI).fontSize = 17;
            (inputField.placeholder as TextMeshProUGUI).color = new Color(0.55f, 0.58f, 0.62f, 1f);

            // 发送按钮
            sendButton = UIHelper.CreateButton("SendBtn", bg.transform,
                "发送", 18, SEND_BTN, Color.white);
            var sendRT = sendButton.GetComponent<RectTransform>();
            sendRT.anchorMin = new Vector2(0.82f, 0.10f);
            sendRT.anchorMax = new Vector2(0.98f, 0.10f);
            sendRT.pivot = new Vector2(0, 0.5f);
            sendRT.anchoredPosition = new Vector2(-14, 0);
            sendRT.sizeDelta = new Vector2(10, 38);
            sendButton.onClick.AddListener(OnSend);
            inputField.onSubmit.AddListener(_ => OnSend());
        }
    }
}
