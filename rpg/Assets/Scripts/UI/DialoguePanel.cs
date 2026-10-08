using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AIBot.Unity;
using AIBot.Core.Output;
using FogHarbor.Relationship;
using FogHarbor.Quest;
using FogHarbor.Inventory;
using FogHarbor.Session;
using FogHarbor.Dialogue;

namespace FogHarbor.UI
{
    /// <summary>Responsive dialogue with message cards, contextual topics and free input.</summary>
    public class DialoguePanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI npcNameText, roleText, favorText, statusText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton, closeButton;
        [SerializeField] private Button[] topicButtons;
        [Header("Debug")]
        [SerializeField] private bool logConversationToConsole = true;
        private NpcAgent agent, subscribedAgent;
        private RelationshipSystem relationships;
        private QuestSystem quests;
        private InventorySystem inventory;
        private GameSession session;
        private NpcDialogueGuide guide;
        private string npcId, lastAttempt;
        private bool waiting, failed, fallback, controlsEnabled;
        private readonly List<Message> messages = new List<Message>();
        private readonly StringBuilder stream = new StringBuilder();
        private Message streamingMessage;
        private float nextLayout;
        private float lastWidth;
        private bool layoutDirty;
        private DialogueTopic[] topics = Array.Empty<DialogueTopic>();
        private static readonly Color Ivory = new Color(0.91f,0.89f,0.83f);
        private static readonly Color Gold = new Color(0.76f,0.64f,0.41f);
        private static readonly Color Muted = new Color(0.58f,0.67f,0.68f);
        private sealed class Message { public RectTransform rect; public TMP_Text speaker, text; public bool player; }

        private void Awake() { EnsureUI(); BindEvents(); }
        public void Show(string npcName, NpcAgent npcAgent)
        {
            EnsureUI();
            Unsubscribe();
            agent = npcAgent;
            npcId = agent != null ? (agent.connectionProfile != null ? agent.connectionProfile.npcId : agent.npcId) : null;
            if (string.IsNullOrEmpty(npcId) && agent != null) npcId = agent.npcId;
            npcNameText.text = npcName ?? "旅人";
            gameObject.SetActive(true);
            Subscribe();
            ClearMessages();
            waiting = failed = fallback = false;
            lastAttempt = null;
            inputField.text = "";
            ((TMP_Text)inputField.placeholder).text = "也可以直接和" + npcNameText.text + "聊聊……";
            guide = null;
            foreach (var candidate in Resources.LoadAll<NpcDialogueGuide>("DialogueGuides"))
                if (candidate.npcId == npcId) { guide = candidate; break; }
            roleText.text = guide != null ? guide.role : "雾港居民";
            var profile = relationships != null && !string.IsNullOrEmpty(npcId) ? relationships.GetProfile(npcId) : null;
            avatarImage.sprite = profile != null ? profile.Portrait : null;
            avatarImage.color = avatarImage.sprite != null ? Color.white : new Color(0.19f,0.27f,0.29f);
            favorText.gameObject.SetActive(profile != null);
            UpdateFavor();
            RefreshTopics();
            AddMessage(guide != null ? guide.Greeting(CurrentState) : "你好，想聊些什么？", false);
            SetStatus("选择一个话题，或自由聊天", Muted);
            UpdateControls();
            // GameCursor and UIManager own the modal cursor/input policy.
            Log("开始对话：" + npcNameText.text);
        }
        public void ShowSkeleton(string npcName)
        {
            Show(npcName, null);
            SetStatus("暂时无法联系这位居民", Muted);
        }
        public void Hide()
        {
            var previous = agent;
            Unsubscribe();
            previous?.CancelRunning();
            waiting = false;
            gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            Unsubscribe();
            agent?.CancelRunning();
            waiting = false;
        }
        private void Subscribe()
        {
            if (agent != null && subscribedAgent == null) {
                subscribedAgent = agent;
                agent.onToken.AddListener(OnToken); agent.onReply.AddListener(OnReply);
                agent.onError.AddListener(OnError); agent.onFallback.AddListener(OnFallback);
                agent.onCancelled.AddListener(OnCancelled); agent.onBusy.AddListener(OnBusy);
                agent.onToolExecuted.AddListener(OnToolExecuted);
            }
            quests = FindObjectOfType<QuestSystem>();
            inventory = FindObjectOfType<InventorySystem>();
            relationships = FindObjectOfType<RelationshipSystem>();
            session = FindObjectOfType<GameSession>();
            if (quests != null) { quests.OnQuestStateChanged -= QuestChanged; quests.OnQuestStateChanged += QuestChanged; }
            if (inventory != null) { inventory.OnInventoryChanged -= RefreshTopics; inventory.OnInventoryChanged += RefreshTopics; }
            if (relationships != null) { relationships.OnFavorChanged -= FavorChanged; relationships.OnFavorChanged += FavorChanged; }
        }
        private void Unsubscribe()
        {
            if (subscribedAgent != null) {
                subscribedAgent.onToken.RemoveListener(OnToken); subscribedAgent.onReply.RemoveListener(OnReply);
                subscribedAgent.onError.RemoveListener(OnError); subscribedAgent.onFallback.RemoveListener(OnFallback);
                subscribedAgent.onCancelled.RemoveListener(OnCancelled); subscribedAgent.onBusy.RemoveListener(OnBusy);
                subscribedAgent.onToolExecuted.RemoveListener(OnToolExecuted);
            }
            subscribedAgent = null;
            if (quests != null) quests.OnQuestStateChanged -= QuestChanged;
            if (inventory != null) inventory.OnInventoryChanged -= RefreshTopics;
            if (relationships != null) relationships.OnFavorChanged -= FavorChanged;
        }
        private QuestState CurrentState => guide != null && guide.quest != null && quests != null
            ? quests.GetState(guide.quest.QuestId) : QuestState.Available;
        private void QuestChanged(string id, QuestState state) { RefreshTopics(); }
        private void FavorChanged(string id, int favor) { if (id == npcId) UpdateFavor(); }
        private void UpdateFavor() { favorText.text = relationships != null && !string.IsNullOrEmpty(npcId) ? "好感 " + relationships.GetFavor(npcId) : ""; }
        private void RefreshTopics()
        {
            topics = guide != null ? guide.Topics(CurrentState) : new[] {
                new DialogueTopic { label="认识一下你",prompt="可以介绍一下你自己吗？" },
                new DialogueTopic { label="聊聊雾港",prompt="你觉得雾港小镇是个怎样的地方？" },
                new DialogueTopic { label="附近有什么？",prompt="附近有什么值得探索的地方？" }
            };
            if (topics == null) topics = Array.Empty<DialogueTopic>();
            // A pre-collected target can also be handed in; the system still validates the operation.
            if (guide != null && guide.quest != null && quests != null && quests.CanComplete(guide.quest.QuestId)) {
                topics = guide.Topics(QuestState.Completed) ?? Array.Empty<DialogueTopic>();
            }
            for (int i=0;i<topicButtons.Length;i++) {
                bool visible = i < topics.Length;
                topicButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                topicButtons[i].GetComponentInChildren<TMP_Text>().text = topics[i].label;
                topicButtons[i].image.color = topics[i].action == DialogueTopicAction.Speak
                    ? new Color(0.18f,0.27f,0.28f) : new Color(0.38f,0.30f,0.17f);
            }
            UpdateControls();
        }
        public void SelectTopic(int index)
        {
            if (index < 0 || index >= topics.Length || Busy || agent == null) return;
            var topic = topics[index];
            if (topic.action == DialogueTopicAction.Speak) { Send(topic.prompt, false); return; }
            if (guide == null || guide.quest == null || session == null) return;
            AddMessage(topic.prompt, true);
            QuestResult result = topic.action == DialogueTopicAction.AcceptQuest
                ? session.AcceptQuest(guide.quest.QuestId) : session.DeliverQuest(guide.quest.QuestId);
            if (result.Success) {
                AddMessage(topic.action == DialogueTopicAction.AcceptQuest ? guide.acceptedResponse : guide.completedResponse, false);
                SetStatus(topic.action == DialogueTopicAction.AcceptQuest ? "已接受委托" : "委托物品已交付，奖励已领取", Gold);
            } else SetStatus(result.Message, new Color(0.9f,0.55f,0.44f));
            RefreshTopics();
        }
        private bool Busy => waiting || (agent != null && agent.IsBusy);
        private void OnSend() { Send(inputField.text, true); }
        private void Send(string text, bool clearDraft)
        {
            if (Busy || agent == null || string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            lastAttempt = text;
            failed = fallback = false;
            stream.Clear();
            AddMessage(text, true);
            streamingMessage = AddMessage("", false);
            if (clearDraft) inputField.text = "";
            waiting = true;
            SetStatus(npcNameText.text + "正在回应……", Gold);
            UpdateControls();
            Log("你：" + text);
            try { agent.Chat(text); }
            catch (Exception ex) { OnError(ex.Message); }
        }
        private void OnToken(string delta)
        {
            if (!waiting || streamingMessage == null || string.IsNullOrEmpty(delta)) return;
            stream.Append(delta);
            streamingMessage.text.text = stream.ToString();
            layoutDirty = true;
        }
        private void OnReply(StructuredReply reply)
        {
            if (!waiting || streamingMessage == null) return;
            if (stream.Length == 0 && reply != null) stream.Append(reply.say);
            streamingMessage.text.text = stream.Length > 0 ? stream.ToString() : "……";
            Log(npcNameText.text + "：" + stream);
            streamingMessage = null;
            waiting = false;
            failed = fallback;
            if (failed && string.IsNullOrEmpty(inputField.text)) inputField.text = lastAttempt ?? "";
            SetStatus(failed ? "暂时无法回应，可重试" : "选择一个话题，或自由聊天", failed ? Gold : Muted);
            RefreshTopics(); Reflow(true);
        }
        private void OnError(string error)
        {
            if (!waiting) return;
            Log("对话失败：" + error);
            if (streamingMessage != null && stream.Length == 0) streamingMessage.text.text = "暂时无法回应，请稍后再试。";
            streamingMessage = null; waiting = false; failed = true;
            if (string.IsNullOrEmpty(inputField.text)) inputField.text = lastAttempt ?? "";
            SetStatus("未能收到回复，已保留输入，可重试", new Color(0.9f,0.55f,0.44f));
            UpdateControls(); Reflow(true);
        }
        private void OnFallback(string reason) { fallback = true; Log("兜底：" + reason); }
        private void OnCancelled()
        {
            if (streamingMessage != null && stream.Length == 0) streamingMessage.text.text = "这次交谈已取消。";
            streamingMessage = null; waiting = false;
            if (isActiveAndEnabled) { SetStatus("选择一个话题，或自由聊天", Muted); Reflow(true); UpdateControls(); }
        }
        private void OnBusy() { SetStatus(npcNameText.text + "正在回应……", Gold); UpdateControls(); }
        private void OnToolExecuted(AgentToolExecutionEvent evt) { RefreshTopics(); }
        private void SetStatus(string text, Color color) { statusText.text=text; statusText.color=color; }
        private void UpdateControls()
        {
            if (sendButton == null) return;
            bool enabled = agent != null && !Busy;
            controlsEnabled = enabled;
            sendButton.interactable = enabled;
            inputField.interactable = enabled;
            sendButton.GetComponentInChildren<TMP_Text>().text = failed ? "重试" : "发送";
            foreach (var button in topicButtons) {
                button.interactable = enabled;
                var group=button.GetComponent<CanvasGroup>();
                if(group!=null)group.alpha=enabled?1:0.5f;
            }
        }
        private void Update()
        {
            bool enabled = agent != null && !Busy;
            if (enabled != controlsEnabled) UpdateControls();
            if (content != null && (layoutDirty || Mathf.Abs(content.rect.width-lastWidth)>1f) && Time.unscaledTime>=nextLayout)
                Reflow(false);
        }
        private Message AddMessage(string text, bool player)
        {
            if (string.IsNullOrEmpty(text)) text = "";
            var image = UIHelper.CreateImage(player ? "PlayerMessage" : "NpcMessage", content,
                player ? new Color(0.21f,0.27f,0.28f) : new Color(0.13f,0.20f,0.23f));
            image.raycastTarget=false;
            var speaker = UIHelper.CreateText("Speaker",image.transform,player?"你":npcNameText.text,19,player?Gold:Muted);
            var body = UIHelper.CreateText("Body",image.transform,text,27,Ivory,TextAlignmentOptions.TopLeft);
            body.richText=false; body.enableWordWrapping=true; body.lineSpacing=7;
            var item=new Message {rect=image.rectTransform,speaker=speaker,text=body,player=player};
            messages.Add(item);
            if(messages.Count>80) { var first=messages[0];messages.RemoveAt(0);Destroy(first.rect.gameObject); }
            Reflow(true);return item;
        }
        private void Reflow(bool immediate)
        {
            if(content==null)return;
            Canvas.ForceUpdateCanvases();
            float width=content.rect.width;lastWidth=width;
            if(width<=10)return;
            bool follow=immediate || scrollRect.verticalNormalizedPosition<0.06f;
            float y=12;
            foreach(var message in messages) {
                float w=width*0.82f;
                float textHeight=Mathf.Max(36,message.text.GetPreferredValues(message.text.text,w-64,0).y);
                float h=textHeight+61;
                var rt=message.rect;rt.anchorMin=rt.anchorMax=new Vector2(message.player?1:0,1);
                rt.pivot=new Vector2(message.player?1:0,1);rt.anchoredPosition=new Vector2(message.player?-12:12,-y);
                rt.sizeDelta=new Vector2(w-24,h);
                AtTop(message.speaker.rectTransform,new Vector2(20,-12),new Vector2(w-64,25));
                AtTop(message.text.rectTransform,new Vector2(20,-43),new Vector2(w-64,textHeight+6));
                y+=h+12;
            }
            content.sizeDelta=new Vector2(0,Mathf.Max(y,scrollRect.viewport.rect.height));
            if(follow)scrollRect.verticalNormalizedPosition=0;
            layoutDirty=false;nextLayout=Time.unscaledTime+0.05f;
        }
        private void ClearMessages()
        {
            foreach(var message in messages) {message.rect.gameObject.SetActive(false);Destroy(message.rect.gameObject);}
            messages.Clear();stream.Clear();streamingMessage=null;Reflow(true);
        }
        private void Log(string text) { if(logConversationToConsole)Debug.Log("[AIBot对话] "+text); }
        public void EnsureUI()
        {
            if (scrollRect == null || content == null || topicButtons == null || topicButtons.Length != 3) BuildUI();
            // Keep TMP composition formatting and its text renderer in plain-text mode.
            // Otherwise IME composition can display literal <u> tags from TMP_InputField.
            if (inputField != null) inputField.richText = false;
        }
        private void BindEvents()
        {
            sendButton.onClick.AddListener(OnSend);inputField.onSubmit.AddListener(_=>OnSend());
            closeButton.onClick.AddListener(Hide);
            for(int i=0;i<topicButtons.Length;i++) {int index=i;topicButtons[i].onClick.AddListener(()=>SelectTopic(index));}
        }
        private static void AtTop(RectTransform rect,Vector2 position,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=position;rect.sizeDelta=size;
        }
        private static void AtBottom(RectTransform rect,Vector2 position,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;
            rect.anchoredPosition=position;rect.sizeDelta=size;
        }
        private void BuildUI()
        {
            // Remove legacy UI only when upgrading an old prefab instance. New prefab stores these references.
            for(int i=transform.childCount-1;i>=0;i--) {
                var old=transform.GetChild(i).gameObject;old.SetActive(false);
                if(Application.isPlaying)Destroy(old);else DestroyImmediate(old);
            }
            var panel=UIHelper.CreateImage("DialogueCard",transform,new Color(0.065f,0.11f,0.14f,0.98f));
            var rt=panel.rectTransform;rt.anchorMin=new Vector2(0.15f,0.025f);rt.anchorMax=new Vector2(0.85f,0.59f);rt.offsetMin=rt.offsetMax=Vector2.zero;
            var header=UIHelper.CreateImage("Header",panel.transform,new Color(0.10f,0.17f,0.20f));
            var hr=header.rectTransform;hr.anchorMin=new Vector2(0,1);hr.anchorMax=Vector2.one;hr.pivot=new Vector2(0.5f,1);hr.sizeDelta=new Vector2(0,86);hr.anchoredPosition=Vector2.zero;
            avatarImage=UIHelper.CreateImage("Portrait",header.transform,new Color(0.19f,0.27f,0.29f));
            AtTop(avatarImage.rectTransform,new Vector2(24,-12),new Vector2(62,62));avatarImage.preserveAspect=true;
            npcNameText=UIHelper.CreateText("NpcName",header.transform,"药师林洛",31,Ivory);
            AtTop(npcNameText.rectTransform,new Vector2(104,-10),new Vector2(440,43));
            roleText=UIHelper.CreateText("Role",header.transform,"雾港居民",19,Muted);
            AtTop(roleText.rectTransform,new Vector2(105,-51),new Vector2(400,26));
            favorText=UIHelper.CreateText("Favor",header.transform,"好感 5",21,Muted,TextAlignmentOptions.Right);
            var fr=favorText.rectTransform;fr.anchorMin=fr.anchorMax=new Vector2(1,0.5f);fr.pivot=new Vector2(1,0.5f);fr.anchoredPosition=new Vector2(-104,0);fr.sizeDelta=new Vector2(200,38);
            closeButton=UIHelper.CreateButton("Close",header.transform,"×",30,new Color(0.13f,0.21f,0.23f),Ivory);
            var cr=(RectTransform)closeButton.transform;cr.anchorMin=cr.anchorMax=new Vector2(1,0.5f);cr.pivot=new Vector2(1,0.5f);cr.anchoredPosition=new Vector2(-24,0);cr.sizeDelta=new Vector2(54,48);
            var rule=UIHelper.CreateImage("Rule",panel.transform,Gold);var rr=rule.rectTransform;rr.anchorMin=new Vector2(0,1);rr.anchorMax=Vector2.one;rr.pivot=new Vector2(0.5f,1);rr.anchoredPosition=new Vector2(0,-86);rr.sizeDelta=new Vector2(0,1);
            var scroll=UIHelper.CreateImage("Messages",panel.transform,new Color(0.07f,0.12f,0.15f));
            var sr=scroll.rectTransform;sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=new Vector2(24,186);sr.offsetMax=new Vector2(-24,-102);
            scrollRect=scroll.gameObject.AddComponent<ScrollRect>();scrollRect.horizontal=false;scrollRect.movementType=ScrollRect.MovementType.Clamped;scrollRect.scrollSensitivity=44;
            var viewport=UIHelper.CreateObject("Viewport",scroll.transform);UIHelper.Stretch((RectTransform)viewport.transform);viewport.AddComponent<RectMask2D>();
            content=(RectTransform)UIHelper.CreateObject("Content",viewport.transform).transform;
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(0.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            scrollRect.viewport=(RectTransform)viewport.transform;scrollRect.content=content;
            statusText=UIHelper.CreateText("Status",panel.transform,"选择一个话题，或自由聊天",19,Muted);
            var str=statusText.rectTransform;str.anchorMin=new Vector2(0,0);str.anchorMax=new Vector2(1,0);str.pivot=Vector2.zero;str.anchoredPosition=new Vector2(26,144);str.sizeDelta=new Vector2(-52,38);
            statusText.enableWordWrapping=false;statusText.overflowMode=TextOverflowModes.Ellipsis;
            var topicsRoot=UIHelper.CreateObject("Topics",panel.transform);var tr=(RectTransform)topicsRoot.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=new Vector2(1,0);tr.pivot=Vector2.zero;tr.anchoredPosition=new Vector2(24,94);tr.sizeDelta=new Vector2(-48,48);
            topicButtons=new Button[3];
            for(int i=0;i<3;i++) {
                var b=UIHelper.CreateButton("Topic"+i,topicsRoot.transform,"推荐话题",23,new Color(0.18f,0.27f,0.28f),Ivory);
                var br=(RectTransform)b.transform;br.anchorMin=new Vector2(i/3f,0);br.anchorMax=new Vector2((i+1)/3f,1);br.offsetMin=new Vector2(i==0?0:8,0);br.offsetMax=Vector2.zero;
                b.gameObject.AddComponent<CanvasGroup>();topicButtons[i]=b;
            }
            var input=UIHelper.CreateImage("Input",panel.transform,new Color(0.12f,0.19f,0.22f));var ir=input.rectTransform;ir.anchorMin=Vector2.zero;ir.anchorMax=new Vector2(1,0);ir.pivot=Vector2.zero;ir.anchoredPosition=new Vector2(24,26);ir.sizeDelta=new Vector2(-190,52);
            inputField=input.gameObject.AddComponent<TMP_InputField>();inputField.characterLimit=1000;
            var inputArea=UIHelper.CreateObject("TextArea",input.transform);var ia=(RectTransform)inputArea.transform;UIHelper.Stretch(ia);ia.offsetMin=new Vector2(16,6);ia.offsetMax=new Vector2(-16,-6);inputArea.AddComponent<RectMask2D>();
            inputField.textViewport=ia;
            var text=UIHelper.CreateText("Text",inputArea.transform,"",25,Ivory);UIHelper.Stretch(text.rectTransform);text.richText=false;
            var placeholder=UIHelper.CreateText("Placeholder",inputArea.transform,"也可以直接和林洛聊聊……",25,Muted);UIHelper.Stretch(placeholder.rectTransform);
            inputField.textComponent=text;inputField.richText=false;inputField.placeholder=placeholder;inputField.lineType=TMP_InputField.LineType.SingleLine;
            sendButton=UIHelper.CreateButton("Send",panel.transform,"发送",24,new Color(0.37f,0.30f,0.18f),Ivory);
            var se=(RectTransform)sendButton.transform;se.anchorMin=se.anchorMax=new Vector2(1,0);se.pivot=new Vector2(1,0);se.anchoredPosition=new Vector2(-24,26);se.sizeDelta=new Vector2(144,52);
            foreach(var button in GetComponentsInChildren<Button>(true)) {
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.18f,1.18f,1.12f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(0.8f,0.8f,0.8f);colors.disabledColor=new Color(0.55f,0.55f,0.55f);button.colors=colors;
            }
        }
    }
}

