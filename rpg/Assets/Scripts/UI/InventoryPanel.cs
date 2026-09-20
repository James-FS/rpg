using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Items;
using FogHarbor.Inventory;
using FogHarbor.Equipment;

namespace FogHarbor.UI
{
    /// <summary>
    /// 背包面板（B 呼出）：左侧物品列表 + 右侧物品详情 + 底部装备概要与操作按钮。
    /// 视觉沿用对话面板那套：深色石板底 + 暗金描边 + 扁平纯色（无纹理 / 渐变）。
    /// 面板根是全屏拉伸的容器，所有内容都放在固定尺寸的 Panel 子物体里，
    /// 元素一律相对 Panel 定位（早前版本直接锚在根节点上，导致标题跑到屏幕顶部、内容散在四角）。
    /// §6.1 差距 #1：按钮只发意图（走 UIManager 转发到 GameSession），不直接调系统改数据；
    /// 展示数据只读系统状态，事件驱动刷新（OnInventoryChanged / OnEquipmentChanged）。
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        private InventorySystem inventory;
        private EquipmentSystem equipment;

        // UI 引用：用 Prefab 时来自序列化数据（可在 Inspector 上换），纯代码构建时由 BuildUI() 赋值
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Transform listContent;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailMetaText;
        [SerializeField] private TextMeshProUGUI detailStatsText;
        [SerializeField] private TextMeshProUGUI detailDescText;
        [SerializeField] private TextMeshProUGUI equipInfoText;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button useButton;
        [SerializeField] private Button dropButton;

        private string selectedItemId;

        // ─── 布局（间距统一取 8 的倍数）───
        private const float PANEL_W  = 960f;   // 面板宽
        private const float PANEL_H  = 580f;   // 面板高
        private const float FRAME_W  = 2f;     // 外圈金边粗细
        private const float HEADER_H = 64f;    // 头部栏高
        private const float RULE_H   = 2f;     // 头部下沿金线粗细
        private const float PAD      = 20f;    // 面板内边距
        private const float GAP      = 20f;    // 区块间距
        private const float STRIP_H  = 56f;    // 底部条（装备概要 + 按钮）高
        private const float LIST_W   = 470f;   // 左列：物品列表宽
        private const float ROW_H    = 52f;    // 物品行高
        private const float ROW_GAP  = 4f;     // 物品行间距
        private const float BTN_W    = 96f;    // 操作按钮宽
        private const float BTN_H    = 40f;    // 操作按钮高
        private const float BTN_GAP  = 12f;    // 操作按钮间距

        // ─── 字号 ───
        private const float FONT_TITLE  = 26f;
        private const float FONT_HINT   = 16f;
        private const float FONT_ROW    = 18f;
        private const float FONT_NUM    = 16f;
        private const float FONT_NAME   = 22f;
        private const float FONT_STAT   = 17f;
        private const float FONT_SMALL  = 15f;
        private const float FONT_BTN    = 17f;

        // ─── 配色（深色石板 + 暗金；与对话面板同源）───
        private static readonly Color GOLD      = Hex("#C8A96A");           // 暗金描边
        private static readonly Color PANEL_BG  = Hex("#1E2732", 0.90f);    // 面板底（略透）
        private static readonly Color HEADER_BG = Hex("#26313F", 0.94f);    // 头部栏
        private static readonly Color LIST_BG   = Hex("#161D26", 0.45f);    // 列表 / 详情底
        private static readonly Color STRIP_BG  = Hex("#161D26", 0.55f);    // 底部条底
        private static readonly Color ROW_BG    = Hex("#2A3644", 0.55f);    // 物品行（常态）
        private static readonly Color ROW_SEL   = Hex("#3C4E68", 0.90f);    // 物品行（选中）
        private static readonly Color BODY      = Hex("#E8E2D6");           // 正文 / 物品名
        private static readonly Color MUTED     = Hex("#8A94A3");           // 次要文字
        private static readonly Color BTN_EQUIP = Hex("#3E5A78");           // 装备按钮底
        private static readonly Color BTN_USE   = Hex("#3F6B4A");           // 使用按钮底
        private static readonly Color BTN_DROP  = Hex("#6B3F3F");           // 丢弃按钮底

        // 已装备标记（富文本）
        private const string RICH_GOLD = "#C8A96A";

        private static Color Hex(string hex, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                Debug.LogWarning($"[InventoryPanel] 颜色解析失败：{hex}");
                return Color.magenta;
            }
            c.a = alpha;
            return c;
        }

        /// <summary>物品类型配色（列表行左侧色条 / 详情类型标签）。</summary>
        private static Color TypeColor(ItemType type) => type switch
        {
            ItemType.Weapon => Hex("#D9A441"),      // 武器（暗金）
            ItemType.Armor => Hex("#4F8FCF"),       // 护甲（蓝）
            ItemType.Consumable => Hex("#62B36B"),  // 消耗品（绿）
            ItemType.QuestItem => Hex("#B08FD9"),   // 任务物品（紫）
            ItemType.Material => Hex("#8A94A3"),    // 材料（灰）
            _ => MUTED
        };

        // ─── 初始化 ───

        public void Initialize(InventorySystem inv, EquipmentSystem eq)
        {
            this.inventory = inv;
            this.equipment = eq;

            if (inventory != null) inventory.OnInventoryChanged += Refresh;
            if (equipment != null) equipment.OnEquipmentChanged += Refresh;  // 装备变化也要刷新列表上的"已装备"标记
            Refresh();
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.OnInventoryChanged -= Refresh;
            if (equipment != null) equipment.OnEquipmentChanged -= Refresh;
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
            if (!gameObject.activeSelf || inventory == null) return;

            // 物品被用完/丢弃后清空选中（仍穿戴中的物品即使不在背包里也保留选中，便于卸下）
            if (!string.IsNullOrEmpty(selectedItemId) && inventory.GetCount(selectedItemId) <= 0 && !IsEquipped(selectedItemId))
                selectedItemId = null;

            RebuildList();
            UpdateDetail();
            UpdateButtons();
            RefreshEquipInfo();
        }

        private void RebuildList()
        {
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            int shown = 0;
            var listed = new HashSet<string>();
            foreach (var pair in inventory.GetAll())
            {
                CreateItemRow(pair.Key, pair.Value);
                listed.Add(pair.Key);
                shown++;
            }

            // 已装备但不在背包里的物品也要列出（选中后可点"卸下"脱下）
            if (equipment != null)
            {
                if (equipment.HasWeapon && !listed.Contains(equipment.WeaponId))
                {
                    CreateItemRow(equipment.WeaponId, 0);
                    listed.Add(equipment.WeaponId);
                    shown++;
                }
                if (equipment.HasArmor && !listed.Contains(equipment.ArmorId))
                {
                    CreateItemRow(equipment.ArmorId, 0);
                    listed.Add(equipment.ArmorId);
                    shown++;
                }
            }

            if (shown == 0)
                CreateEmptyRow();
        }

        private void CreateItemRow(string itemId, int count)
        {
            var data = ItemDatabase.GetById(itemId);
            string name = data != null ? data.DisplayName : itemId;
            bool isEquipped = IsEquipped(itemId);

            var row = CreateRowObject($"Row_{itemId}");
            var bg = row.GetComponent<Image>();
            bg.color = itemId == selectedItemId ? ROW_SEL : ROW_BG;

            // 左侧类型色条：一眼区分武器 / 护甲 / 消耗品 / 材料
            var typeBar = UIHelper.CreateImage("TypeBar", row.transform,
                data != null ? TypeColor(data.Type) : MUTED);
            var barRT = typeBar.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(0, 1);
            barRT.pivot = new Vector2(0, 0.5f);
            barRT.anchoredPosition = Vector2.zero;
            barRT.sizeDelta = new Vector2(4, 0);
            typeBar.raycastTarget = false;

            // 物品名
            var nameText = UIHelper.CreateText("Name", row.transform, name, (int)FONT_ROW,
                itemId == selectedItemId ? Color.white : BODY, TextAlignmentOptions.Left);
            var nameRT = nameText.rectTransform;
            nameRT.anchorMin = new Vector2(0, 0);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.offsetMin = new Vector2(16, 0);
            nameRT.offsetMax = new Vector2(-150, 0);

            // 右侧：已装备标记 + 数量
            string rightRich = isEquipped
                ? (count > 0 ? $"<color={RICH_GOLD}>已装备</color>    x{count}" : $"<color={RICH_GOLD}>已装备</color>")
                : $"x{count}";
            var rightText = UIHelper.CreateText("Right", row.transform, rightRich, (int)FONT_NUM,
                isEquipped ? GOLD : MUTED, TextAlignmentOptions.Right);
            var rightRT = rightText.rectTransform;
            rightRT.anchorMin = new Vector2(1, 0);
            rightRT.anchorMax = new Vector2(1, 1);
            rightRT.pivot = new Vector2(1, 0.5f);
            rightRT.anchoredPosition = new Vector2(-14, 0);
            rightRT.sizeDelta = new Vector2(140, 0);

            var btn = row.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectItem(itemId));
        }

        /// <summary>空背包占位行。</summary>
        private void CreateEmptyRow()
        {
            var row = CreateRowObject("Empty");
            var bg = row.GetComponent<Image>();
            bg.color = new Color(0, 0, 0, 0);

            var text = UIHelper.CreateText("Text", row.transform, "背包空空如也", (int)FONT_SMALL,
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

        private bool IsEquipped(string itemId)
        {
            if (equipment == null) return false;
            return (equipment.HasWeapon && equipment.WeaponId == itemId)
                || (equipment.HasArmor && equipment.ArmorId == itemId);
        }

        private void SelectItem(string itemId)
        {
            selectedItemId = itemId;
            Refresh();
        }

        private void UpdateDetail()
        {
            if (detailNameText == null) return;

            if (string.IsNullOrEmpty(selectedItemId))
            {
                detailNameText.text = "未选择物品";
                detailMetaText.text = "点击左侧物品查看详情";
                detailStatsText.text = "";
                detailDescText.text = "";
                return;
            }

            var data = ItemDatabase.GetById(selectedItemId);
            if (data == null)
            {
                detailNameText.text = selectedItemId;
                detailMetaText.text = "";
                detailStatsText.text = "";
                detailDescText.text = "";
                return;
            }

            detailNameText.text = data.DisplayName;
            int ownedCount = inventory.GetCount(selectedItemId);
            detailMetaText.text = $"{TypeToString(data.Type)}"
                + (ownedCount > 0 ? $"    数量 x{ownedCount}" : "")
                + (IsEquipped(selectedItemId) ? $"    <color={RICH_GOLD}>已装备</color>" : "");

            var stats = new System.Text.StringBuilder();
            if (data.Attack > 0) stats.Append($"<color={RICH_GOLD}>攻击</color>  +{data.Attack}\n");
            if (data.Defense > 0) stats.Append($"<color={RICH_GOLD}>防御</color>  +{data.Defense}\n");
            if (data.HealAmount > 0) stats.Append($"<color={RICH_GOLD}>恢复</color>  +{data.HealAmount} HP\n");
            detailStatsText.text = stats.ToString().TrimEnd('\n');

            detailDescText.text = data.Description;
        }

        private void UpdateButtons()
        {
            if (equipButton == null) return;

            var data = string.IsNullOrEmpty(selectedItemId) ? null : ItemDatabase.GetById(selectedItemId);

            bool equippable = data != null && (data.Type == ItemType.Weapon || data.Type == ItemType.Armor);
            equipButton.gameObject.SetActive(equippable);
            useButton.gameObject.SetActive(data != null && data.Type == ItemType.Consumable);
            dropButton.gameObject.SetActive(data != null && inventory.GetCount(selectedItemId) > 0);

            // 已装备的物品：按钮变为"卸下"（装备槽里的物品只能从这里脱下）
            if (equippable)
            {
                var label = equipButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = IsEquipped(selectedItemId) ? "卸下" : "装备";
            }
        }

        private void RefreshEquipInfo()
        {
            if (equipInfoText == null || equipment == null) return;

            string weaponDisplay = "无";
            if (equipment.HasWeapon)
            {
                var wData = ItemDatabase.GetById(equipment.WeaponId);
                weaponDisplay = wData != null ? wData.DisplayName : equipment.WeaponId;
            }

            string armorDisplay = "无";
            if (equipment.HasArmor)
            {
                var aData = ItemDatabase.GetById(equipment.ArmorId);
                armorDisplay = aData != null ? aData.DisplayName : equipment.ArmorId;
            }

            equipInfoText.text =
                $"<color={RICH_GOLD}>武器</color> {weaponDisplay}  <color={RICH_GOLD}>ATK+{equipment.BonusAttack}</color>" +
                $"      <color={RICH_GOLD}>护甲</color> {armorDisplay}  <color={RICH_GOLD}>DEF+{equipment.BonusDefense}</color>";
        }

        // ─── 按钮操作：只发意图，由 UIManager 转发到 GameSession（§6.1 差距 #1）───

        private void OnEquip()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;
            if (IsEquipped(selectedItemId))
                UIManager.Instance?.RequestUnequip(selectedItemId);
            else
                UIManager.Instance?.RequestEquip(selectedItemId);
        }

        private void OnUse()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;
            UIManager.Instance?.RequestUseItem(selectedItemId);
        }

        private void OnDrop()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;
            UIManager.Instance?.RequestDrop(selectedItemId);
        }

        private string TypeToString(ItemType type) => type switch
        {
            ItemType.Weapon => "武器",
            ItemType.Armor => "护甲",
            ItemType.Consumable => "消耗品",
            ItemType.QuestItem => "任务物品",
            ItemType.Material => "材料",
            _ => type.ToString()
        };

        // ─── UI 构建 ───

        private void Awake()
        {
            EnsureUI();
            BindEvents();
        }

        private void Start()
        {
            ApplyGeneratedSprites();
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

            if (panelRoot == null || listContent == null || detailNameText == null || detailMetaText == null
                || detailStatsText == null || detailDescText == null || equipInfoText == null
                || equipButton == null || useButton == null || dropButton == null)
                Debug.LogWarning("[InventoryPanel] 预制体里有未绑定的 UI 引用，请检查背包面板的 Inspector 绑定。", this);
        }

        /// <summary>按钮事件绑定（运行时监听无法存进 Prefab，所以 Prefab 与代码两条路径都要走这里）。</summary>
        private void BindEvents()
        {
            if (equipButton == null || useButton == null || dropButton == null) return;
            equipButton.onClick.AddListener(OnEquip);
            useButton.onClick.AddListener(OnUse);
            dropButton.onClick.AddListener(OnDrop);
        }

        // 圆角按钮底图由代码生成，预制体里存不了临时 Sprite 引用，只能在运行时补上。
        private static Sprite buttonSprite;

        private void ApplyGeneratedSprites()
        {
            if (!Application.isPlaying) return;
            if (buttonSprite == null) buttonSprite = UIHelper.CreateRoundedSprite(40, 10);

            foreach (var btn in new[] { equipButton, useButton, dropButton })
            {
                if (btn == null || btn.image == null) continue;
                btn.image.sprite = buttonSprite;
                btn.image.type = Image.Type.Sliced;
            }
        }

        private void BuildUI()
        {
            // ── 面板框：960×580 居中（外圈金边 + 深蓝灰底，只隔一层所以还能透出场景）──
            var panel = UIHelper.CreateObject("Panel", transform);
            panelRoot = panel.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
            panelRoot.anchoredPosition = Vector2.zero;
            panelRoot.sizeDelta = new Vector2(PANEL_W, PANEL_H);

            foreach (var (name, min, max, pivot, pos, size) in new[]
            {
                ("FrameTop",    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, FRAME_W)),
                ("FrameBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, FRAME_W)),
                ("FrameLeft",   new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(FRAME_W, 0)),
                ("FrameRight",  new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, new Vector2(FRAME_W, 0)),
            })
            {
                var edge = UIHelper.CreateImage(name, panelRoot, GOLD);
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

            var title = UIHelper.CreateText("Title", header.transform, "背包", (int)FONT_TITLE, BODY, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var titleRT = title.rectTransform;
            titleRT.anchorMin = new Vector2(0, 0.5f);
            titleRT.anchorMax = new Vector2(0, 0.5f);
            titleRT.pivot = new Vector2(0, 0.5f);
            titleRT.anchoredPosition = new Vector2(PAD, 0);
            titleRT.sizeDelta = new Vector2(300, 34);

            var hint = UIHelper.CreateText("Hint", header.transform, "按 B 关闭", (int)FONT_HINT, MUTED, TextAlignmentOptions.Right);
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

        /// <summary>左列：物品列表（ScrollRect + RectMask2D 裁剪 + 自适应内容高度）。</summary>
        private void BuildList(RectTransform parent, float x, float topY, float w, float h)
        {
            var list = UIHelper.CreateObject("ItemList", parent);
            var listRT = list.GetComponent<RectTransform>();
            listRT.anchorMin = new Vector2(0, 1);
            listRT.anchorMax = new Vector2(0, 1);
            listRT.pivot = new Vector2(0, 1);
            listRT.anchoredPosition = new Vector2(x, -topY);
            listRT.sizeDelta = new Vector2(w, h);

            var listBg = list.AddComponent<Image>();
            listBg.color = LIST_BG;
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

        /// <summary>右列：选中物品的详情（名称 / 类型数量 / 属性 / 描述）。</summary>
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
            boxBg.color = LIST_BG;
            boxBg.raycastTarget = false;

            detailNameText = UIHelper.CreateText("Name", box.transform, "未选择物品", (int)FONT_NAME, BODY, TextAlignmentOptions.Left);
            SetTopLeft(detailNameText.rectTransform, 16, -14, w - 32, 30);

            detailMetaText = UIHelper.CreateText("Meta", box.transform, "点击左侧物品查看详情", (int)FONT_SMALL, MUTED, TextAlignmentOptions.Left);
            SetTopLeft(detailMetaText.rectTransform, 16, -48, w - 32, 24);

            AddThinRule(box.transform, w, -80);

            detailStatsText = UIHelper.CreateText("Stats", box.transform, "", (int)FONT_STAT, BODY, TextAlignmentOptions.TopLeft);
            SetTopLeft(detailStatsText.rectTransform, 16, -92, w - 32, 100);

            AddThinRule(box.transform, w, -200);

            detailDescText = UIHelper.CreateText("Desc", box.transform, "", (int)FONT_SMALL, MUTED, TextAlignmentOptions.TopLeft);
            SetTopLeft(detailDescText.rectTransform, 16, -212, w - 32, h - 212 - 16);
        }

        /// <summary>底部条：左侧装备概要，右侧操作按钮（装备 / 使用 / 丢弃）。</summary>
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

            equipInfoText = UIHelper.CreateText("EquipInfo", strip.transform, "", (int)FONT_SMALL, BODY, TextAlignmentOptions.Left);
            var infoRT = equipInfoText.rectTransform;
            infoRT.anchorMin = new Vector2(0, 0.5f);
            infoRT.anchorMax = new Vector2(0, 0.5f);
            infoRT.pivot = new Vector2(0, 0.5f);
            infoRT.anchoredPosition = new Vector2(PAD, 0);
            infoRT.sizeDelta = new Vector2(innerW - PAD * 2 - (BTN_W + BTN_GAP) * 3 - 16, 24);

            equipButton = CreateActionButton("EquipBtn", strip.transform, "装备", BTN_EQUIP, 2);
            useButton = CreateActionButton("UseBtn", strip.transform, "使用", BTN_USE, 1);
            dropButton = CreateActionButton("DropBtn", strip.transform, "丢弃", BTN_DROP, 0);

            equipButton.gameObject.SetActive(false);
            useButton.gameObject.SetActive(false);
            dropButton.gameObject.SetActive(false);
        }

        private Button CreateActionButton(string name, Transform parent, string label, Color bgColor, int slotFromRight)
        {
            var btn = UIHelper.CreateButton(name, parent, label, (int)FONT_BTN, bgColor, BODY);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-(PAD + (BTN_W + BTN_GAP) * slotFromRight), 0);
            rt.sizeDelta = new Vector2(BTN_W, BTN_H);

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            return btn;
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
