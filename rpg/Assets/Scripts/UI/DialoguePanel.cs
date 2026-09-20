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
    /// 对话面板：深色石板 + 暗金描边（扁平纯色，无纹理 / 花纹 / 渐变 / 发光）。
    /// 结构（1600×480，贴屏幕底部正中）：金边 → 深蓝灰底（略透，能看见场景）
    ///   → 通栏头部栏（头像框 / NPC 名 / 好感度 / 状态灯）→ 细金分隔线
    ///   → 消息区（单 TMP 富文本，可滚动）→ 底部输入行（输入框 + 发送按钮）。
    /// 消息区用单个 TMP 富文本（玩家名暗金、NPC 名与正文米白），流式回复实时追加。
    /// 头部栏显示当前 NPC 好感（§6.1 模板④：订阅 OnFavorChanged 实时刷新）。
    /// </summary>
    public class DialoguePanel : MonoBehaviour
    {
        // ─── UI 引用 ───
        // UI 引用：用 Prefab 时来自序列化数据（可在 Inspector 上换），纯代码构建时由 BuildUI() 赋值
        [SerializeField] private TextMeshProUGUI npcNameText;
        [SerializeField] private TextMeshProUGUI favorText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image statusDot;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton;

        // ─── 调试日志 ───
        [Header("调试")]
        [Tooltip("把与 AIBot 的每轮往来（玩家输入 / NPC 回复 / 工具调用 / 错误）打印到 Console")]
        [SerializeField] private bool logConversationToConsole = true;
        [Tooltip("连每轮发给 AIBot 的游戏上下文快照（JSON）一起打印")]
        [SerializeField] private bool logContextSnapshot;

        // ─── 对话状态 ───
        private NpcAgent agent;
        private RelationshipSystem relationshipSystem;
        private string currentNpcId;
        private readonly StringBuilder transcript = new StringBuilder();
        private readonly StringBuilder streaming = new StringBuilder();
        private int streamingLineStart = -1;   // 本轮流式行的起始下标（空回复时回退掉名字前缀）
        private float nextRefreshAt;
        private bool refreshQueued;
        private float executingHoldUntil;

        // ─── 布局（间距统一取 8 的倍数）───
        private const float PANEL_W       = 1600f;  // 面板宽
        private const float PANEL_H       = 480f;   // 面板高
        private const float PANEL_BOTTOM  = 24f;    // 面板距屏幕底边
        private const float FRAME_W       = 2f;     // 外圈金边粗细
        private const float HEADER_H      = 72f;    // 头部栏高（56 头像 + 上下各 8）
        private const float RULE_H        = 2f;     // 头部下沿金线粗细
        private const float PAD           = 16f;    // 面板内左右边距
        private const float GAP           = 16f;    // 元素间距
        private const float DOT_GAP       = 8f;     // 状态点与状态文字间距
        private const float AVATAR_SIZE   = 56f;    // 头像框边长
        private const float ROW_H         = 56f;    // 输入框 / 按钮高
        private const float ROW_BOTTOM    = 8f;     // 输入行距面板底边
        private const float INPUT_W       = 1200f;  // 输入框宽（面板宽的 3/4）
        private const float SEND_W        = 352f;   // 发送按钮宽
        private const float NAME_W        = 320f;   // NPC 名占位宽（其后为好感度留位）
        private const float FAVOR_W       = 160f;   // 好感度文字宽
        private const float STATUS_W      = 160f;   // 状态文字宽（贴右端）
        private const float DOT_SIZE      = 14f;    // 状态点直径
        private const float MSG_PAD       = 16f;    // 消息文本上下内边距（左右不再内缩，与输入框/头像同一条基准线）

        // ─── 字号（正文 20 / NPC 名 28 / 输入 20 / 状态 17）───
        private const float FONT_NAME     = 28f;
        private const float FONT_BODY     = 20f;
        private const float FONT_INPUT    = 20f;
        private const float FONT_STATUS   = 17f;
        private const float FONT_FAVOR    = 18f;

        // ─── 配色（深色石板 + 暗金；扁平纯色）───
        private static readonly Color GOLD       = Hex("#C8A96A");            // 暗金描边
        private static readonly Color PANEL_BG   = Hex("#1E2732", 0.90f);     // 面板底（略透）
        private static readonly Color HEADER_BG  = Hex("#26313F", 0.94f);     // 头部栏
        private static readonly Color MESSAGE_BG = Hex("#161D26", 0.45f);     // 消息区（更深一档的透明底）
        private static readonly Color AVATAR_BG  = Hex("#35414F");            // 头像占位色
        private static readonly Color BODY_TEXT  = Hex("#E8E2D6");            // 正文 / NPC 名
        private static readonly Color MUTED_TEXT = Hex("#8A94A3");            // 次要文字
        private static readonly Color INPUT_BG   = Hex("#161D26", 0.95f);     // 输入框底
        private static readonly Color INPUT_LINE = Hex("#C8A96A", 0.45f);     // 输入框细描边
        private static readonly Color PLACEHOLDER= Hex("#8A94A3", 0.75f);     // 输入提示
        private static readonly Color SEND_BG    = Hex("#3E5A78");            // 发送按钮底
        private static readonly Color ST_IDLE    = Hex("#62B36B");            // 状态：空闲（绿）
        private static readonly Color ST_THINK   = Hex("#C8A96A");            // 状态：思考中（金）
        private static readonly Color ST_RUN     = Hex("#4F8FCF");            // 状态：执行中（蓝）
        private static readonly Color ST_ERROR   = Hex("#C75B4E");            // 状态：出错（红）

        // 富文本颜色（消息区；与上面同源的十六进制字符串）
        private const string PLAYER_RICH = "#D9A441";   // 玩家名
        private const string NPC_RICH    = "#E8E2D6";   // NPC 名 / 正文
        private const string ERROR_RICH  = "#C75B4E";   // 错误行

        private static Color Hex(string hex, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                Debug.LogWarning($"[DialoguePanel] 颜色解析失败：{hex}");
                return Color.magenta;
            }
            c.a = alpha;
            return c;
        }

        // ─── 面板控制 ───

        public void Show(string npcName, NpcAgent npcAgent)
        {
            agent = npcAgent;
            gameObject.SetActive(true);

            npcNameText.text = npcName ?? "NPC";
            ClearMessages();
            inputField.text = "";
            SetStatus(StatusKind.Idle);
            BindFavor();
            inputField.ActivateInputField();

            LogConversation($"── 开始对话：{npcNameText.text}（npcId={ResolveNpcId() ?? "?"}）──");
        }

        public void ShowSkeleton(string npcName)
        {
            gameObject.SetActive(true);
            npcNameText.text = npcName ?? "NPC";
            ClearMessages();
            AppendLine("（对话系统未绑定 NpcAgent）", ERROR_RICH);
            SetStatus(StatusKind.Idle);
        }

        public void Hide()
        {
            // 面板关闭不代表 NPC 请求应继续占用；取消后由 onCancelled 统一复位 UI。
            agent?.CancelRunning();
            executingHoldUntil = 0f;
            UnbindFavor();
            gameObject.SetActive(false);

            LogConversation("── 结束对话 ──");
        }

        // ─── 好感度显示（§6.1 模板④：订阅 OnFavorChanged）───

        private void BindFavor()
        {
            UnbindFavor();
            relationshipSystem = relationshipSystem ?? FindObjectOfType<RelationshipSystem>();
            currentNpcId = ResolveNpcId();

            // 头像：优先用 NPC 资料里配的立绘，没配就保持纯色占位
            var profile = relationshipSystem != null && !string.IsNullOrEmpty(currentNpcId)
                ? relationshipSystem.GetProfile(currentNpcId)
                : null;
            ApplyPortrait(profile);

            if (relationshipSystem == null || string.IsNullOrEmpty(currentNpcId))
            {
                SetFavorText(null);
                return;
            }
            relationshipSystem.OnFavorChanged += OnFavorChanged;
            SetFavorText(relationshipSystem.GetFavor(currentNpcId));
        }

        /// <summary>把 NPC 资料里的立绘贴到头部头像框内（没配则保持纯色占位）。</summary>
        private void ApplyPortrait(NpcProfile profile)
        {
            if (avatarImage == null) return;

            Sprite portrait = profile != null ? profile.Portrait : null;
            if (portrait != null)
            {
                avatarImage.sprite = portrait;
                avatarImage.type = Image.Type.Simple;
                avatarImage.color = Color.white;
                avatarImage.preserveAspect = true;
            }
            else
            {
                avatarImage.sprite = null;
                avatarImage.color = AVATAR_BG;
            }
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
                agent.onToolExecuted.AddListener(OnToolExecuted);
                agent.onServerStatus.AddListener(OnServerStatus);
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
                agent.onToolExecuted.RemoveListener(OnToolExecuted);
                agent.onServerStatus.RemoveListener(OnServerStatus);
            }
            UnbindFavor();
        }

        // ─── 对话日志（Console）───

        private const string LogTag = "[AIBot对话]";

        /// <summary>把一轮往来的内容打到 Console（只读展示，不影响对话流程；可在 Inspector 关掉）。</summary>
        private void LogConversation(string line)
        {
            if (logConversationToConsole)
                Debug.Log($"{LogTag} {line}");
        }

        /// <summary>本轮发给 AIBot 的游戏上下文快照（GameContextProvider 提供）。</summary>
        private void LogContextIfEnabled()
        {
            if (!logConversationToConsole || !logContextSnapshot || agent == null) return;

            var provider = agent.gameContextProvider as FogHarbor.Dialogue.GameContextProvider;
            if (provider != null)
                Debug.Log($"{LogTag} [上下文 → AIBot] {provider.SnapshotJson}");
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
            LogConversation($"{npcNameText.text} → 你：{AssembledReplyText(reply)}{ReplyMeta(reply)}");
            FinishStreaming();
            ClearExecutingHold();
            SetStatus(StatusKind.Idle);
        }

        /// <summary>本轮 NPC 的完整回复文本（优先用流式累积结果，没有则用结构化回复的 say）。</summary>
        private string AssembledReplyText(StructuredReply reply)
        {
            if (streaming.Length > 0) return streaming.ToString();
            return reply != null ? reply.say : "";
        }

        /// <summary>结构化回复里的情绪/动作（有才附在日志后面，方便排查模型输出）。</summary>
        private static string ReplyMeta(StructuredReply reply)
        {
            if (reply == null) return "";
            string meta = "";
            if (!string.IsNullOrEmpty(reply.emotion)) meta += $"情绪={reply.emotion}";
            if (!string.IsNullOrEmpty(reply.action)) meta += (meta.Length > 0 ? " " : "") + $"动作={reply.action}";
            return meta.Length > 0 ? $"　（{meta}）" : "";
        }

        private void OnError(string message)
        {
            LogConversation($"出错：{message}");
            FinishStreaming();
            AppendLine($"（对话出错：{message}）", ERROR_RICH);
            ClearExecutingHold();
            SetStatus(StatusKind.Error);
        }

        private void OnFallback(string reason)
        {
            // 兜底回复本身仍然会通过 onReply 正常显示，面板上不插诊断行（避免玩家读到像报错的提示）；
            // 诊断只进 Console，开发期照样能查。
            LogConversation($"兜底回复（模型暂时不可用）：{reason}");
            ClearExecutingHold();
            SetStatus(StatusKind.Idle);
        }

        private void OnCancelled()
        {
            LogConversation("本轮已取消");
            FinishStreaming();
            ClearExecutingHold();
            SetStatus(StatusKind.Idle);
        }

        private void OnBusy()
        {
            SetStatus(StatusKind.Thinking);
        }

        private void OnServerStatus(string status)
        {
            LogConversation($"服务器：{status}");
        }

        /// <summary>工具已在本机执行（game 模式回传）。执行本身是瞬间的，这里保持一小段展示时间。</summary>
        private void OnToolExecuted(AgentToolExecutionEvent evt)
        {
            LogConversation($"工具 {evt.toolName} {evt.argumentsJson} → {(evt.success ? "成功" : "失败")}：{evt.result}");
            executingHoldUntil = Time.unscaledTime + 1.2f;
            SetStatus(StatusKind.Executing);
        }

        private void ClearExecutingHold()
        {
            executingHoldUntil = 0f;
        }

        // ─── 发送 ───

        private void OnSend()
        {
            if (agent == null)
            {
                SetStatus(StatusKind.Error);
                return;
            }
            string text = inputField.text.Trim();
            if (text.Length == 0) return;

            AppendLine(text, PLAYER_RICH, "你");
            inputField.text = "";

            LogConversation($"你 → {npcNameText.text}：{text}");
            LogContextIfEnabled();

            // 上一轮若还挂在半行上（正文没吐完 / 或空了），先收尾再开新行
            if (streaming.Length > 0 || streamingLineStart >= 0) FinishStreaming();
            // 开始流式：先写 NPC 名字前缀，正文随后续在同一行
            StartStreamingLine();
            RefreshMessage(true);

            ClearExecutingHold();
            SetStatus(StatusKind.Thinking);
            agent.Chat(text);
        }

        // ─── 消息管理 ───

        private void ClearMessages()
        {
            transcript.Length = 0;
            streaming.Length = 0;
            streamingLineStart = -1;
            RefreshMessage();
        }

        private void AppendLine(string content, string color, string speaker = null)
        {
            string name = speaker ?? npcNameText.text;
            transcript.AppendLine($"<color={color}><b>{name}：</b></color>{content}");
            RefreshMessage();
        }

        /// <summary>开始流式：名字前缀只写一段，不回车的，正文接着同一行流式追加。</summary>
        private void StartStreamingLine()
        {
            streamingLineStart = transcript.Length;
            transcript.Append($"<color={NPC_RICH}><b>{npcNameText.text}：</b></color>");
        }

        private void Update()
        {
            // “执行中”是瞬时事件，展示一小段后回到等待模型续跑
            if (executingHoldUntil > 0f && Time.unscaledTime >= executingHoldUntil)
            {
                executingHoldUntil = 0f;
                SetStatus(StatusKind.Thinking);
            }

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

        /// <summary>
        /// 结束本轮流式：把累积的正文提交进 transcript（名字与正文同一行），并换行收尾。
        /// 之前这里只清空缓冲、不提交，导致下一轮刷新时上一轮回复会从面板上消失，只剩「名字：」一行。
        /// </summary>
        private void FinishStreaming()
        {
            if (streaming.Length > 0)
            {
                transcript.Append(streaming);
                transcript.Append('\n');
            }
            else if (streamingLineStart >= 0 && streamingLineStart <= transcript.Length)
            {
                // 空回复（没吐任何字）：把这轮只写了名字的半行撤掉，不留孤零零的「名字：」
                transcript.Length = streamingLineStart;
            }

            streamingLineStart = -1;
            streaming.Length = 0;
            RefreshMessage();
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (scrollRect != null)
                Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        // ─── 状态灯（空闲 / 思考中 / 执行中 / 出错）───

        private enum StatusKind { Idle, Thinking, Executing, Error }

        private const string LABEL_IDLE      = "空闲";
        private const string LABEL_THINKING  = "思考中…";
        private const string LABEL_EXECUTING = "执行中";
        private const string LABEL_ERROR     = "出错";

        private void SetStatus(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Thinking:  ApplyStatus(LABEL_THINKING, ST_THINK);  break;
                case StatusKind.Executing: ApplyStatus(LABEL_EXECUTING, ST_RUN);   break;
                case StatusKind.Error:     ApplyStatus(LABEL_ERROR, ST_ERROR);     break;
                default:                   ApplyStatus(LABEL_IDLE, ST_IDLE);       break;
            }
        }

        private void ApplyStatus(string label, Color dotColor)
        {
            if (statusText != null) statusText.text = label;
            if (statusDot != null) statusDot.color = dotColor;
            LayoutStatusDot(label);
        }

        /// <summary>状态文字贴在面板右端，圆点按文字实际宽度紧贴在它左侧。</summary>
        private void LayoutStatusDot(string label)
        {
            if (statusDot == null || statusText == null) return;
            float width = Mathf.Min(statusText.GetPreferredValues(label ?? string.Empty).x, STATUS_W);
            statusDot.rectTransform.anchoredPosition = new Vector2(-(PAD + width + DOT_GAP), 0f);
        }

        // ─── UI 构建 ───

        private void Awake()
        {
            EnsureUI();
            ApplyGeneratedSprites();
            BindEvents();
        }

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

            if (npcNameText == null || statusDot == null || statusText == null || avatarImage == null
                || scrollRect == null || messageText == null || inputField == null || sendButton == null)
                Debug.LogWarning("[DialoguePanel] 预制体里有未绑定的 UI 引用，请检查对话面板的 Inspector 绑定。", this);
        }

        /// <summary>按钮/输入框事件绑定（运行时监听无法存进 Prefab，Prefab 与代码两条路径都走这里）。</summary>
        private void BindEvents()
        {
            sendButton.onClick.AddListener(OnSend);
            inputField.onSubmit.AddListener(_ => OnSend());
        }

        // 圆角按钮 / 圆点用的贴图由代码生成，预制体里存不了临时 Sprite 引用，只能在运行时补上。
        private static Sprite sendButtonSprite;
        private static Sprite statusDotSprite;

        private void ApplyGeneratedSprites()
        {
            if (!Application.isPlaying) return;

            if (sendButton != null && sendButton.image != null)
            {
                if (sendButtonSprite == null) sendButtonSprite = UIHelper.CreateRoundedSprite(40, 12);
                sendButton.image.sprite = sendButtonSprite;
                sendButton.image.type = Image.Type.Sliced;
            }
            if (statusDot != null)
            {
                if (statusDotSprite == null) statusDotSprite = UIHelper.CreateRoundedSprite(24, 12);
                statusDot.sprite = statusDotSprite;
                statusDot.type = Image.Type.Simple;
            }
        }

        private void BuildUI()
        {
            // ── 面板框：1600×480 贴屏幕底部正中（离底边 24），四边金色细条组成外框 ──
            // 根节点本身不能带底色：内层 BG 是半透明的，垫一层实色会把整块面板染成那个颜色。
            var border = UIHelper.CreateObject("Border", transform);
            var borderRT = border.GetComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(0.5f, 0f);
            borderRT.anchorMax = new Vector2(0.5f, 0f);
            borderRT.pivot = new Vector2(0.5f, 0f);
            borderRT.anchoredPosition = new Vector2(0f, PANEL_BOTTOM);
            borderRT.sizeDelta = new Vector2(PANEL_W, PANEL_H);

            var edgeTop = UIHelper.CreateImage("FrameTop", border.transform, GOLD);
            var edgeBot = UIHelper.CreateImage("FrameBottom", border.transform, GOLD);
            var edgeLeft = UIHelper.CreateImage("FrameLeft", border.transform, GOLD);
            var edgeRight = UIHelper.CreateImage("FrameRight", border.transform, GOLD);

            var etRT = edgeTop.GetComponent<RectTransform>();
            etRT.anchorMin = new Vector2(0, 1); etRT.anchorMax = new Vector2(1, 1); etRT.pivot = new Vector2(0.5f, 1);
            etRT.anchoredPosition = Vector2.zero; etRT.sizeDelta = new Vector2(0, FRAME_W);

            var ebRT = edgeBot.GetComponent<RectTransform>();
            ebRT.anchorMin = new Vector2(0, 0); ebRT.anchorMax = new Vector2(1, 0); ebRT.pivot = new Vector2(0.5f, 0);
            ebRT.anchoredPosition = Vector2.zero; ebRT.sizeDelta = new Vector2(0, FRAME_W);

            var elRT = edgeLeft.GetComponent<RectTransform>();
            elRT.anchorMin = new Vector2(0, 0); elRT.anchorMax = new Vector2(0, 1); elRT.pivot = new Vector2(0, 0.5f);
            elRT.anchoredPosition = Vector2.zero; elRT.sizeDelta = new Vector2(FRAME_W, 0);

            var erRT = edgeRight.GetComponent<RectTransform>();
            erRT.anchorMin = new Vector2(1, 0); erRT.anchorMax = new Vector2(1, 1); erRT.pivot = new Vector2(1, 0.5f);
            erRT.anchoredPosition = Vector2.zero; erRT.sizeDelta = new Vector2(FRAME_W, 0);

            foreach (var edge in new[] { edgeTop, edgeBot, edgeLeft, edgeRight })
                edge.raycastTarget = false;

            // ── 深蓝灰底（只隔这一层，所以还能透出场景） ──
            var bg = UIHelper.CreateImage("BG", border.transform, PANEL_BG);
            var bgRT = bg.GetComponent<RectTransform>();
            UIHelper.Stretch(bgRT);
            bgRT.offsetMin = new Vector2(FRAME_W, FRAME_W);
            bgRT.offsetMax = new Vector2(-FRAME_W, -FRAME_W);
            bg.raycastTarget = false;

            // ── 通栏头部栏 ──
            var header = UIHelper.CreateImage("Header", bg.transform, HEADER_BG);
            var headerRT = header.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = Vector2.zero;
            headerRT.sizeDelta = new Vector2(0, HEADER_H);
            header.raycastTarget = false;

            // 头部下沿细金线
            var headerRule = UIHelper.CreateImage("HeaderRule", bg.transform, GOLD);
            var ruleRT = headerRule.GetComponent<RectTransform>();
            ruleRT.anchorMin = new Vector2(0, 1);
            ruleRT.anchorMax = new Vector2(1, 1);
            ruleRT.pivot = new Vector2(0.5f, 1);
            ruleRT.anchoredPosition = new Vector2(0, -HEADER_H);
            ruleRT.sizeDelta = new Vector2(0, RULE_H);
            headerRule.raycastTarget = false;

            // 头像框 56×56：金框 + 纯色占位（以后换成头像图）
            var avatar = UIHelper.CreateImage("Avatar", header.transform, GOLD);
            var avatarRT = avatar.GetComponent<RectTransform>();
            avatarRT.anchorMin = new Vector2(0, 0.5f);
            avatarRT.anchorMax = new Vector2(0, 0.5f);
            avatarRT.pivot = new Vector2(0, 0.5f);
            avatarRT.anchoredPosition = new Vector2(PAD, 0);
            avatarRT.sizeDelta = new Vector2(AVATAR_SIZE, AVATAR_SIZE);
            avatar.raycastTarget = false;

            avatarImage = UIHelper.CreateImage("Fill", avatar.transform, AVATAR_BG);
            var avatarFillRT = avatarImage.GetComponent<RectTransform>();
            UIHelper.Stretch(avatarFillRT);
            avatarFillRT.offsetMin = new Vector2(FRAME_W, FRAME_W);
            avatarFillRT.offsetMax = new Vector2(-FRAME_W, -FRAME_W);
            avatarImage.raycastTarget = false;

            // NPC 名（米白加粗）
            npcNameText = UIHelper.CreateText("NpcName", header.transform, "NPC", (int)FONT_NAME, BODY_TEXT);
            npcNameText.fontStyle = FontStyles.Bold;
            var nameRT = npcNameText.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(0, 0.5f);
            nameRT.pivot = new Vector2(0, 0.5f);
            nameRT.anchoredPosition = new Vector2(PAD + AVATAR_SIZE + GAP, 0);
            nameRT.sizeDelta = new Vector2(NAME_W, 40);

            // 好感度（模板④：显示当前 NPC 好感；名字右侧留位）
            favorText = UIHelper.CreateText("Favor", header.transform, "", (int)FONT_FAVOR, MUTED_TEXT);
            var favorRT = favorText.GetComponent<RectTransform>();
            favorRT.anchorMin = new Vector2(0, 0.5f);
            favorRT.anchorMax = new Vector2(0, 0.5f);
            favorRT.pivot = new Vector2(0, 0.5f);
            favorRT.anchoredPosition = new Vector2(PAD + AVATAR_SIZE + GAP + NAME_W + GAP, 0);
            favorRT.sizeDelta = new Vector2(FAVOR_W, 28);
            favorText.gameObject.SetActive(false);

            // 状态文字（贴右端）+ 状态圆点（位置按文字宽度算，见 LayoutStatusDot）
            statusText = UIHelper.CreateText("Status", header.transform, LABEL_IDLE, (int)FONT_STATUS, MUTED_TEXT,
                TextAlignmentOptions.Right);
            var statusRT = statusText.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(1, 0.5f);
            statusRT.anchorMax = new Vector2(1, 0.5f);
            statusRT.pivot = new Vector2(1, 0.5f);
            statusRT.anchoredPosition = new Vector2(-PAD, 0);
            statusRT.sizeDelta = new Vector2(STATUS_W, 28);

            statusDot = UIHelper.CreateImage("StatusDot", header.transform, ST_IDLE);
            var dotRT = statusDot.GetComponent<RectTransform>();
            dotRT.anchorMin = new Vector2(1, 0.5f);
            dotRT.anchorMax = new Vector2(1, 0.5f);
            dotRT.pivot = new Vector2(1, 0.5f);
            dotRT.sizeDelta = new Vector2(DOT_SIZE, DOT_SIZE);
            statusDot.raycastTarget = false;
            LayoutStatusDot(LABEL_IDLE);

            // ── 消息区：深色透明底 + 单 TMP 富文本滚动 ──
            var scrollObj = UIHelper.CreateImage("MessageScroll", bg.transform, MESSAGE_BG);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.pivot = new Vector2(0.5f, 1);
            scrollRT.offsetMin = new Vector2(PAD, ROW_BOTTOM + ROW_H + GAP);  // 底部让开输入行
            scrollRT.offsetMax = new Vector2(-PAD, -(HEADER_H + RULE_H));     // 顶部让开头部栏与金线

            scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewport = UIHelper.CreateObject("Viewport", scrollObj.transform);
            var vpRT = viewport.GetComponent<RectTransform>();
            UIHelper.Stretch(vpRT);
            // 用 RectMask2D 而非 Mask：运行时创建的透明 Mask 在团结引擎有 stencil 渲染兼容问题
            viewport.AddComponent<RectMask2D>();

            messageText = UIHelper.CreateText("Message", viewport.transform, "", (int)FONT_BODY, BODY_TEXT,
                TextAlignmentOptions.TopLeft);
            messageText.enableWordWrapping = true;
            messageText.richText = true;
            messageText.raycastTarget = false;

            var msgRT = messageText.GetComponent<RectTransform>();
            msgRT.anchorMin = new Vector2(0, 1);
            msgRT.anchorMax = new Vector2(1, 1);
            msgRT.pivot = new Vector2(0.5f, 1);
            msgRT.anchoredPosition = new Vector2(0, -MSG_PAD);
            msgRT.sizeDelta = new Vector2(0, 0);

            // 文本高度随内容增长（ContentSizeFitter）
            var msgCsf = messageText.gameObject.AddComponent<ContentSizeFitter>();
            msgCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            msgCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ScrollRect 绑定（背景 Image 保持可接收滚轮/拖拽）
            scrollRect.content = msgRT;
            scrollRect.viewport = vpRT;

            // ── 底部输入行：输入框（深色底 + 细描边）+ 发送按钮（暗蓝圆角）──
            var inputBorder = UIHelper.CreateImage("Input", bg.transform, INPUT_LINE);
            var inputRT = inputBorder.GetComponent<RectTransform>();
            inputRT.anchorMin = Vector2.zero;
            inputRT.anchorMax = Vector2.zero;
            inputRT.pivot = Vector2.zero;
            inputRT.anchoredPosition = new Vector2(PAD, ROW_BOTTOM);
            inputRT.sizeDelta = new Vector2(INPUT_W, ROW_H);

            var inputBg = UIHelper.CreateImage("Bg", inputBorder.transform, INPUT_BG);
            var inBgRT = inputBg.GetComponent<RectTransform>();
            UIHelper.Stretch(inBgRT);
            inBgRT.offsetMin = new Vector2(1, 1);
            inBgRT.offsetMax = new Vector2(-1, -1);
            inputBg.raycastTarget = false;

            inputField = inputBorder.gameObject.AddComponent<TMP_InputField>();

            var inputTextObj = UIHelper.CreateObject("Text", inputBorder.transform);
            var inputTextRT = inputTextObj.GetComponent<RectTransform>();
            UIHelper.Stretch(inputTextRT);
            inputTextRT.offsetMin = new Vector2(PAD, 8);
            inputTextRT.offsetMax = new Vector2(-PAD, -8);
            inputField.textComponent = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputField.textComponent.fontSize = (int)FONT_INPUT;
            inputField.textComponent.color = BODY_TEXT;
            inputField.textComponent.alignment = TextAlignmentOptions.Left;

            var placeholderObj = UIHelper.CreateObject("Placeholder", inputBorder.transform);
            var placeholderRT = placeholderObj.GetComponent<RectTransform>();
            UIHelper.Stretch(placeholderRT);
            placeholderRT.offsetMin = new Vector2(PAD, 8);
            placeholderRT.offsetMax = new Vector2(-PAD, -8);
            inputField.placeholder = placeholderObj.AddComponent<TextMeshProUGUI>();
            (inputField.placeholder as TextMeshProUGUI).text = "对林洛说点什么...";
            (inputField.placeholder as TextMeshProUGUI).fontSize = (int)FONT_INPUT;
            (inputField.placeholder as TextMeshProUGUI).color = PLACEHOLDER;

            sendButton = UIHelper.CreateButton("SendBtn", bg.transform, "发送", (int)FONT_INPUT, SEND_BG, BODY_TEXT);
            var sendRT = sendButton.GetComponent<RectTransform>();
            sendRT.anchorMin = new Vector2(1, 0);
            sendRT.anchorMax = new Vector2(1, 0);
            sendRT.pivot = new Vector2(1, 0);
            sendRT.anchoredPosition = new Vector2(-PAD, ROW_BOTTOM);
            sendRT.sizeDelta = new Vector2(SEND_W, ROW_H);

            // 按钮反馈：悬停略亮、按下略暗（不改色相，保持扁平）
            var sendColors = sendButton.colors;
            sendColors.normalColor = Color.white;
            sendColors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            sendColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            sendColors.selectedColor = Color.white;
            sendButton.colors = sendColors;
        }
    }
}
