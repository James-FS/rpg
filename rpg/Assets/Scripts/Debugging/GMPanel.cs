#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIBot.Core.Context;
using AIBot.Unity;
using FogHarbor.Dialogue;
using FogHarbor.Items;
using FogHarbor.Session;
using FogHarbor.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FogHarbor.Debugging
{
    public sealed class GMPanel : MonoBehaviour
    {
        private static readonly Color Background = new Color32(27, 29, 32, 255);
        private static readonly Color Surface = new Color32(48, 51, 55, 255);
        private static readonly Color Accent = new Color32(48, 115, 98, 255);
        private static readonly Color TextColor = new Color32(234, 237, 239, 255);
        private GMCommandService commands;
        private GMDiagnostics diagnostics;
        private RectTransform frame;
        private ScrollRect contentScroll;
        private Transform body;
        private TextMeshProUGUI status, slotLabel;
        private GameObject confirmation;
        private readonly List<Action> refreshers = new();
        private readonly List<Button> writeButtons = new();
        private int tab;
        private string selectedQuest, selectedItem, search = "", itemType = "全部";
        private string quantity = "1", goldQuantity = "100";
        private NpcAgent selectedNpc;
        private float nextRefresh;
        private bool running;
        public bool IsOpen => gameObject.activeSelf;
        public bool HasConfirmation => confirmation != null;

        public void Initialize(GMCommandService service, GMDiagnostics logs)
        {
            commands = service;
            diagnostics = logs;
            Build();
            Hide();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            commands.RefreshAgents();
            diagnostics.Refresh();
            BuildTab();
            RefreshValues();
        }

        public void Hide()
        {
            DismissConfirmation();
            gameObject.SetActive(false);
        }
        public void DismissConfirmation()
        {
            if (confirmation != null) Destroy(confirmation);
            confirmation = null;
        }
        private void Update()
        {
            Fit();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.3f;
            RefreshValues();
        }

        private void Fit()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null) frame.sizeDelta = new Vector2(Mathf.Min(1020, parent.rect.width - 32), Mathf.Min(760, parent.rect.height - 32));
        }

        private void Build()
        {
            UIHelper.Stretch(GetComponent<RectTransform>());
            var backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(0, 0, 0, 0.65f);
            var panel = UIHelper.CreateImage("Window", transform, Background);
            frame = panel.rectTransform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1020, 760);
            var header = UIHelper.CreateObject("Header", frame);
            Area(header.GetComponent<RectTransform>(), 12, 12, 12, 58);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10;
            headerLayout.childForceExpandWidth = false;
            Label(header.transform, "GM", 24, 80);
            slotLabel = Label(header.transform, "", 18);
            Button(header.transform, "×", Hide, false, 44);

            var nav = UIHelper.CreateObject("Tabs", frame);
            var navRt = nav.GetComponent<RectTransform>();
            navRt.anchorMin = new Vector2(0, 0);
            navRt.anchorMax = new Vector2(0, 1);
            navRt.offsetMin = new Vector2(12, 90);
            navRt.offsetMax = new Vector2(134, -78);
            var navLayout = nav.AddComponent<VerticalLayoutGroup>();
            navLayout.spacing = 8;
            navLayout.childForceExpandHeight = false;
            string[] tabs = { "任务", "玩家与背包", "NPC", "测试存档" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                Button(nav.transform, tabs[i], () => { tab = index; BuildTab(); });
            }

            var scrollGo = UIHelper.CreateObject("ContentScroll", frame);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            UIHelper.Stretch(scrollRt);
            scrollRt.offsetMin = new Vector2(154, 90);
            scrollRt.offsetMax = new Vector2(-18, -78);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            contentScroll = scroll;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = UIHelper.CreateObject("Viewport", scrollGo.transform);
            var viewportRt = UIHelper.Stretch(viewport.GetComponent<RectTransform>());
            viewportRt.offsetMax = new Vector2(-18, 0);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            var content = UIHelper.CreateObject("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(4, 4, 4, 12);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            var barImage = UIHelper.CreateImage("Scrollbar", scrollGo.transform, Surface);
            var barRt = barImage.rectTransform;
            barRt.anchorMin = new Vector2(1, 0);
            barRt.anchorMax = Vector2.one;
            barRt.offsetMin = new Vector2(-12, 0);
            barRt.offsetMax = Vector2.zero;
            var bar = barImage.gameObject.AddComponent<Scrollbar>();
            var handle = UIHelper.CreateImage("Handle", barImage.transform, Accent);
            bar.handleRect = UIHelper.Stretch(handle.rectTransform);
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            body = content.transform;

            status = UIHelper.CreateText("Result", frame, "", 18, TextColor);
            status.richText = false;
            var footer = status.rectTransform;
            footer.anchorMin = new Vector2(0, 0);
            footer.anchorMax = new Vector2(1, 0);
            footer.offsetMin = new Vector2(18, 12);
            footer.offsetMax = new Vector2(-18, 76);
            status.overflowMode = TextOverflowModes.Ellipsis;
            Fit();
        }

        private static void Area(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private Transform Row(Transform parent = null)
        {
            var go = UIHelper.CreateObject("Row", parent ?? body);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            go.AddComponent<LayoutElement>().minHeight = 42;
            return go.transform;
        }
        private TextMeshProUGUI Label(Transform parent, string value, int size = 18, float width = -1)
        {
            var text = UIHelper.CreateText("Label", parent, value, size, TextColor);
            text.richText = false;
            text.enableWordWrapping = true;
            var le = text.gameObject.AddComponent<LayoutElement>();
            if (width > 0) { le.preferredWidth = width; le.flexibleWidth = 0; }
            else le.flexibleWidth = 1;
            le.minHeight = 30;
            return text;
        }
        private Button Button(Transform parent, string label, Action action, bool writing = false, float width = -1)
        {
            var button = UIHelper.CreateButton(label, parent, label, 18, writing ? Accent : Surface, TextColor);
            var le = button.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 42;
            if (width > 0) le.preferredWidth = width;
            else le.flexibleWidth = 1;
            button.onClick.AddListener(() => { if (!running) action(); });
            if (writing) writeButtons.Add(button);
            return button;
        }

        private TMP_InputField Input(Transform parent, string value, Action<string> changed, bool numeric)
        {
            var image = UIHelper.CreateImage("Input", parent, Surface);
            var le = image.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 42;
            le.flexibleWidth = 1;
            var input = image.gameObject.AddComponent<TMP_InputField>();
            var text = UIHelper.CreateText("Text", image.transform, "", 18, TextColor);
            UIHelper.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(10, 4);
            text.rectTransform.offsetMax = new Vector2(-10, -4);
            text.richText = false;
            input.textViewport = text.rectTransform;
            input.textComponent = text;
            input.contentType = numeric ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
            input.characterLimit = numeric ? 4 : 80;
            input.text = value;
            input.onValueChanged.AddListener(v => changed(v));
            return input;
        }

        private void BuildTab()
        {
            refreshers.Clear();
            writeButtons.Clear();
            foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            switch (tab)
            {
                case 0: BuildQuests(); break;
                case 1: BuildInventory(); break;
                case 2: BuildNpcs(); break;
                case 3: BuildSaves(); break;
            }
            RefreshValues();
            Canvas.ForceUpdateCanvases();
            contentScroll.verticalNormalizedPosition = 1;
        }

        private void BuildQuests()
        {
            Label(body, "任务", 22);
            var ids = commands.Quests.GetAllQuestIds().OrderBy(id => id).ToArray();
            if (!ids.Contains(selectedQuest)) selectedQuest = ids.FirstOrDefault();
            foreach (var id in ids)
            {
                var def = commands.Quests.GetQuest(id);
                var button = Button(body, "", () => { selectedQuest = id; BuildTab(); });
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                refreshers.Add(() => text.text = (id == selectedQuest ? "• " : "") + def.Title + "  [" + id + "]  " + commands.Quests.GetState(id));
            }
            if (selectedQuest == null) return;
            var quest = commands.Quests.GetQuest(selectedQuest);
            var details = Label(body, "");
            refreshers.Add(() => details.text = quest.Description + "\n归属 NPC：未配置\n目标：" + quest.TargetItemId
                + "  " + commands.Inventory.GetCount(quest.TargetItemId ?? "") + "/" + quest.TargetCount
                + "\n奖励：" + quest.RewardGold + " 金币  " + quest.RewardItemId + " ×" + quest.RewardItemCount);
            var row = Row();
            Button(row, "接取", () => Run(() => commands.QuestCommand(selectedQuest, "accept")), true);
            Button(row, "补齐物品", () => Run(() => commands.QuestCommand(selectedQuest, "fill")), true);
            Button(row, "准备交付", () => Run(() => commands.QuestCommand(selectedQuest, "prepare")), true);
            row = Row();
            Button(row, "结算交付", () => Confirm("直接执行游戏结算并发奖，不会向 AI 发起对话。", () => Run(() => commands.QuestCommand(selectedQuest, "deliver"))), true);
            Button(row, "重置状态", () => Confirm("仅将任务设为 Available，不扣回已经领取的金币或物品。完整回退请恢复检查点。", () => Run(() => commands.QuestCommand(selectedQuest, "reset"))), true);
        }

        private void BuildInventory()
        {
            var player = Label(body, "", 20);
            refreshers.Add(() => player.text = "金币 " + commands.Wallet.Gold + "    HP "
                + (commands.Player == null ? "无玩家" : commands.Player.CurrentHp + "/" + commands.Player.MaxHp));
            var row = Row();
            Button(row, "回血", () => Run(commands.Heal), true);
            Input(row, goldQuantity, v => goldQuantity = v, true);
            Button(row, "增加金币", () => Run(() => commands.AddGold(Number(goldQuantity))), true);
            row = Row();
            Input(row, search, v => search = v, false);
            Button(row, "搜索", BuildTab, false, 90);
            var types = new List<string> { "全部" };
            types.AddRange(Enum.GetNames(typeof(ItemType)));
            row = Row();
            foreach (var type in types)
                Button(row, type == "全部" ? type : TypeName(type), () => { itemType = type; BuildTab(); });
            var items = ItemDatabase.GetAll().Where(i => (itemType == "全部" || i.Type.ToString() == itemType)
                && (i.ItemId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || i.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(i => i.ItemId).ToArray();
            if (!items.Any(i => i.ItemId == selectedItem)) selectedItem = items.FirstOrDefault()?.ItemId;
            foreach (var item in items)
            {
                var button = Button(body, "", () => { selectedItem = item.ItemId; BuildTab(); });
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                refreshers.Add(() => text.text = (item.ItemId == selectedItem ? "• " : "") + item.DisplayName + "  [" + item.ItemId + "]  ×" + commands.Inventory.GetCount(item.ItemId));
            }
            if (selectedItem == null) return;
            Label(body, ItemDatabase.GetById(selectedItem).Description);
            row = Row();
            Label(row, "数量", 18, 70);
            Input(row, quantity, v => quantity = v, true);
            Button(row, "添加", () => Run(() => commands.ChangeItem(selectedItem, Number(quantity), false)), true);
            Button(row, "移除", () => Run(() => commands.ChangeItem(selectedItem, Number(quantity), true)), true);
        }

        private static string TypeName(string type) => type switch
        {
            "Weapon" => "武器", "Armor" => "护甲", "Consumable" => "消耗品", "QuestItem" => "任务品", _ => "材料"
        };
        private static int Number(string value) => int.TryParse(value, out var number) ? number : 0;

        private void BuildNpcs()
        {
            Button(Row(), "刷新 NPC 列表", () => { commands.RefreshAgents(); diagnostics.Refresh(); BuildTab(); });
            var agents = commands.Agents.OrderBy(a => a.name).ToArray();
            if (selectedNpc == null || !agents.Contains(selectedNpc)) selectedNpc = agents.FirstOrDefault();
            foreach (var agent in agents)
                Button(body, commands.NpcName(agent) + "  [" + (agent.connectionProfile != null ? agent.connectionProfile.npcId : agent.npcId) + "]",
                    () => { selectedNpc = agent; BuildTab(); });
            if (selectedNpc == null) { Label(body, "当前场景没有 NPC"); return; }
            var selected = selectedNpc;
            var details = Label(body, "");
            refreshers.Add(() =>
            {
                if (selected == null) { details.text = "NPC 已离开当前场景"; return; }
                var profile = selected.connectionProfile;
                var gesture = selected.GetComponent<NpcDialogueAnimator>();
                var animator = gesture != null ? gesture.Animator : null;
                string npcId = profile != null ? profile.npcId : selected.npcId;
                details.text = "Game: " + (profile != null ? profile.gameId : selected.gameId) + "    NPC: " + npcId
                    + "\n好感 " + commands.Session.GetFavor(npcId) + "    请求 " + (selected.IsBusy ? "执行中" : "空闲")
                    + "\nAnimator: " + (animator != null ? animator.name : "缺失") + "    动作监听: " + (gesture != null ? "已绑定" : "缺失")
                    + "\nAvatar: " + (animator != null && animator.avatar != null ? animator.avatar.name : "缺失")
                    + "\nServer: " + (profile != null ? SafeUrl(profile.serverBaseUrl) : "未配置")
                    + "    超时: " + (profile != null ? profile.timeoutMs + "ms" : "未配置")
                    + "\nPlayer: " + (profile != null ? profile.playerId : selected.playerId)
                    + "\nSession: " + (profile != null ? profile.sessionId : selected.sessionId);
            });
            var row = Row();
            Button(row, "打开对话", () =>
            {
                if (!commands.Testing) { SetResult(QuestResult.Fail("请先开始测试，以隔离 AI 记忆和任务自动保存")); return; }
                if (selected == null || !selected.isActiveAndEnabled) { SetResult(QuestResult.Fail("NPC 未启用，或不支持测试会话隔离")); return; }
                var interactable = selected != null ? selected.GetComponent<NpcInteractable>() : null;
                if (interactable == null) { SetResult(QuestResult.Fail("NPC 缺少交互组件")); return; }
                Hide();
                interactable.Interact();
            }, true);
            Button(row, "检测连接", () => RunAsync(async () => selected == null ? QuestResult.Fail("NPC 已销毁")
                : await selected.CheckServerAsync() ? QuestResult.Ok("连接正常") : QuestResult.Fail("连接未就绪，查看下方记录")));
            row = Row();
            foreach (string action in new[] { "idle", "wave", "nod", "bow" })
                Button(row, action switch { "idle" => "待机", "wave" => "招手", "nod" => "点头", _ => "鞠躬" }, () => Run(() => selected != null && selected.GetComponent<NpcDialogueAnimator>()?.PlayAction(action) == true
                    ? QuestResult.Ok("播放 " + action) : QuestResult.Fail("动作组件或 Animator 状态缺失")));
            Label(body, "实际上下文", 20);
            var context = Label(body, "");
            refreshers.Add(() => context.text = selected != null && selected.gameContextProvider is IGameContext provider
                ? provider.SnapshotJson : "尚未绑定游戏上下文（首次打开对话时绑定）");
            Label(body, "最近调用与错误", 20);
            var logs = Label(body, "");
            refreshers.Add(() => logs.text = string.Join("\n\n", diagnostics.Entries.Where(e => e.AgentId == 0 || (selected != null && e.AgentId == selected.GetInstanceID()))
                .Reverse().Take(20).Select(e => e.Text)));
            Button(Row(), "清空记录", () => { diagnostics.Clear(); RefreshValues(); });
        }

        private static string SafeUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return "无效 URL";
            return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.GetLeftPart(UriPartial.Authority);
        }

        private void BuildSaves()
        {
            Label(body, "测试存档", 22);
            Label(body, "复制当前进度进行测试，不会自动重置任务。测试存档与 AI 会话独立，结束后恢复原进度。重测任务请在任务页点击“重置状态”。");
            var state = Label(body, "");
            refreshers.Add(() => state.text = "当前槽：" + commands.Saves.CurrentSlot + "\n测试身份：" + (commands.TestIdentity ?? "未启用"));
            Button(Row(), "复制当前进度并开始测试", () => RunAsync(() => commands.SwitchTestAsync(true)));
            var row = Row();
            Button(row, "建立检查点", () => Confirm("覆盖当前测试检查点？", () => Run(commands.SaveCheckpoint)), true);
            Button(row, "恢复检查点", () => Confirm("取消当前 NPC 请求，恢复完整检查点并换用新的测试 AI 身份？", () => RunAsync(commands.RestoreCheckpointAsync)));
            Button(row, "保存测试", () => Run(commands.SaveTest), true);
            Button(Row(), "结束测试", () => Confirm("放弃测试进度，恢复进入测试前的状态和正式 AI 会话？", () => RunAsync(() => commands.SwitchTestAsync(false))));
        }

        private void RefreshValues()
        {
            if (slotLabel == null) return;
            slotLabel.text = commands.Testing ? "测试槽" : "正式槽 · 只读";
            foreach (var refresh in refreshers) refresh();
            foreach (var button in writeButtons) if (button != null) button.interactable = commands.CanWrite && !running;
        }
        private void SetResult(QuestResult result)
        {
            status.color = result.Success ? new Color32(122, 215, 173, 255) : new Color32(245, 169, 139, 255);
            status.text = result.Message;
            RefreshValues();
        }
        private void Run(Func<QuestResult> action)
        {
            if (running || commands.Transitioning) return;
            try { SetResult(action()); }
            catch (Exception ex) { SetResult(QuestResult.Fail(ex.Message)); }
        }
        private async void RunAsync(Func<Task<QuestResult>> action)
        {
            if (running || commands.Transitioning) return;
            running = true;
            RefreshValues();
            try
            {
                var result = await action();
                if (this != null) SetResult(result);
            }
            catch (Exception ex) { if (this != null) SetResult(QuestResult.Fail(ex.Message)); }
            finally { running = false; if (this != null) RefreshValues(); }
        }
        private void Confirm(string message, Action action)
        {
            if (running || commands.Transitioning) return;
            DismissConfirmation();
            var overlay = UIHelper.CreateImage("Confirmation", transform, new Color(0, 0, 0, 0.8f));
            confirmation = overlay.gameObject;
            UIHelper.Stretch(overlay.rectTransform);
            var window = UIHelper.CreateImage("Dialog", overlay.transform, Background);
            window.rectTransform.anchorMin = new Vector2(0.15f, 0.3f);
            window.rectTransform.anchorMax = new Vector2(0.85f, 0.7f);
            window.rectTransform.offsetMin = window.rectTransform.offsetMax = Vector2.zero;
            var layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 16;
            layout.childForceExpandHeight = false;
            Label(window.transform, message, 20);
            var row = Row(window.transform);
            Button(row, "取消", DismissConfirmation);
            Button(row, "确认", () => { DismissConfirmation(); action(); });
        }
    }
}
#endif
