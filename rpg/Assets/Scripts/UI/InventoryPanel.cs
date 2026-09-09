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
    /// 背包面板：物品列表 + 详情 + 装备/使用/丢弃按钮。
    /// 按 B 呼出/关闭。监听 OnInventoryChanged 自动刷新。
    /// §6.1 差距 #1：按钮只发意图（走 UIManager 转发到 GameSession），不直接调系统改数据；
    /// 面板仍只读系统状态用于展示（事件驱动刷新）。
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        private InventorySystem inventory;
        private EquipmentSystem equipment;

        private Transform listContent;
        private TextMeshProUGUI detailText;
        private Button equipButton;
        private Button useButton;
        private Button dropButton;
        private TextMeshProUGUI equipInfoText;

        private string selectedItemId;

        private static readonly Color PANEL_BG = new Color(0, 0, 0, 0.85f);
        private static readonly Color ROW_NORMAL = new Color(0, 0, 0, 0);
        private static readonly Color ROW_SELECTED = new Color(0.2f, 0.35f, 0.6f, 0.6f);

        public void Initialize(InventorySystem inv, EquipmentSystem eq)
        {
            this.inventory = inv;
            this.equipment = eq;

            inventory.OnInventoryChanged += Refresh;
            equipment.OnEquipmentChanged += RefreshEquipInfo;
            RefreshEquipInfo();
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
            if (!gameObject.activeSelf) return;

            // 物品被用完/丢弃后清空选中
            if (!string.IsNullOrEmpty(selectedItemId) && inventory.GetCount(selectedItemId) <= 0)
                selectedItemId = null;

            // 清空列表
            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            // 创建行
            foreach (var pair in inventory.GetAll())
                CreateItemRow(pair.Key, pair.Value);

            // 更新详情
            UpdateDetail();

            // 更新装备信息
            RefreshEquipInfo();
        }

        private void CreateItemRow(string itemId, int count)
        {
            var data = ItemDatabase.GetById(itemId);
            string name = data != null ? data.DisplayName : itemId;
            string typeTag = data != null ? TypeToString(data.Type) : "";

            var row = new GameObject($"Row_{itemId}", typeof(RectTransform));
            row.transform.SetParent(listContent, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, 45);
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(1, 1);
            rowRT.pivot = new Vector2(0.5f, 1);

            var bg = row.AddComponent<Image>();
            bg.color = itemId == selectedItemId ? ROW_SELECTED : ROW_NORMAL;

            var tmp = UIHelper.CreateText("Text", row.transform,
                $"{name} x{count}  [{typeTag}]", 18, Color.white,
                TextAlignmentOptions.Left);
            var tmpRT = tmp.GetComponent<RectTransform>();
            tmpRT.anchorMin = new Vector2(0, 0);
            tmpRT.anchorMax = new Vector2(1, 1);
            tmpRT.offsetMin = new Vector2(15, 0);
            tmpRT.offsetMax = new Vector2(-15, 0);

            var btn = row.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectItem(itemId));
        }

        private void SelectItem(string itemId)
        {
            selectedItemId = itemId;
            UpdateDetail();
            UpdateButtons();
            Refresh();
        }

        private void UpdateDetail()
        {
            if (string.IsNullOrEmpty(selectedItemId))
            {
                detailText.text = "选择一个物品查看详情";
                return;
            }

            var data = ItemDatabase.GetById(selectedItemId);
            if (data == null)
            {
                detailText.text = selectedItemId;
                return;
            }

            int count = inventory.GetCount(selectedItemId);
            string stats = "";
            if (data.Attack > 0) stats += $"攻击 +{data.Attack}\n";
            if (data.Defense > 0) stats += $"防御 +{data.Defense}\n";
            if (data.HealAmount > 0) stats += $"恢复 +{data.HealAmount} HP\n";

            detailText.text =
                $"{data.DisplayName}\n" +
                $"类型: {TypeToString(data.Type)}\n" +
                $"数量: {count}\n" +
                $"\n{stats}" +
                $"\n{data.Description}";
        }

        private void UpdateButtons()
        {
            if (string.IsNullOrEmpty(selectedItemId))
            {
                equipButton.gameObject.SetActive(false);
                useButton.gameObject.SetActive(false);
                dropButton.gameObject.SetActive(false);
                return;
            }

            var data = ItemDatabase.GetById(selectedItemId);
            if (data == null)
            {
                equipButton.gameObject.SetActive(false);
                useButton.gameObject.SetActive(false);
                dropButton.gameObject.SetActive(false);
                return;
            }

            equipButton.gameObject.SetActive(
                data.Type == ItemType.Weapon || data.Type == ItemType.Armor);
            useButton.gameObject.SetActive(data.Type == ItemType.Consumable);
            dropButton.gameObject.SetActive(true);
        }

        private void RefreshEquipInfo()
        {
            if (equipInfoText == null) return;

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
                $"武器: {weaponDisplay} (ATK+{equipment.BonusAttack})\n" +
                $"护甲: {armorDisplay} (DEF+{equipment.BonusDefense})";
        }

        // ─── 按钮操作：只发意图，由 UIManager 转发到 GameSession（§6.1 差距 #1）───

        private void OnEquip()
        {
            if (string.IsNullOrEmpty(selectedItemId)) return;
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
            bgRT.sizeDelta = new Vector2(600, 500);

            // 标题
            var title = UIHelper.CreateText("Title", transform,
                "背包 (B)", 28, Color.white, TextAlignmentOptions.Center);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1);
            titleRT.anchorMax = new Vector2(0.5f, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.anchoredPosition = new Vector2(0, -15);
            titleRT.sizeDelta = new Vector2(500, 35);

            // 物品列表 (ScrollRect)
            var scrollObj = UIHelper.CreateObject("ItemScroll", transform);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 1);
            scrollRT.anchorMax = new Vector2(1, 0.4f);
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

            // 详情区域
            detailText = UIHelper.CreateText("Detail", transform,
                "选择一个物品查看详情", 18, new Color(0.9f, 0.9f, 0.9f, 1f),
                TextAlignmentOptions.Left);
            var detailRT = detailText.GetComponent<RectTransform>();
            detailRT.anchorMin = new Vector2(0, 0.25f);
            detailRT.anchorMax = new Vector2(0.45f, 0.03f);
            detailRT.offsetMin = new Vector2(20, 0);
            detailRT.offsetMax = new Vector2(0, 0);

            // 装备信息
            equipInfoText = UIHelper.CreateText("EquipInfo", transform,
                "武器: 无\n护甲: 无", 18, new Color(0.7f, 0.85f, 1f, 1f),
                TextAlignmentOptions.Left);
            var equipInfoRT = equipInfoText.GetComponent<RectTransform>();
            equipInfoRT.anchorMin = new Vector2(0.45f, 0.25f);
            equipInfoRT.anchorMax = new Vector2(1, 0.03f);
            equipInfoRT.offsetMin = new Vector2(10, 0);
            equipInfoRT.offsetMax = new Vector2(-20, 0);

            // 按钮
            equipButton = UIHelper.CreateButton("EquipBtn", transform,
                "装备", 18, new Color(0.2f, 0.3f, 0.2f, 1f), Color.white);
            var equipBtnRT = equipButton.GetComponent<RectTransform>();
            equipBtnRT.anchorMin = new Vector2(0.5f, 0);
            equipBtnRT.anchorMax = new Vector2(0.5f, 0);
            equipBtnRT.pivot = new Vector2(0.5f, 0);
            equipBtnRT.anchoredPosition = new Vector2(-110, 10);
            equipBtnRT.sizeDelta = new Vector2(90, 35);
            equipButton.onClick.AddListener(OnEquip);

            useButton = UIHelper.CreateButton("UseBtn", transform,
                "使用", 18, new Color(0.2f, 0.2f, 0.4f, 1f), Color.white);
            var useBtnRT = useButton.GetComponent<RectTransform>();
            useBtnRT.anchorMin = new Vector2(0.5f, 0);
            useBtnRT.anchorMax = new Vector2(0.5f, 0);
            useBtnRT.pivot = new Vector2(0.5f, 0);
            useBtnRT.anchoredPosition = new Vector2(0, 10);
            useBtnRT.sizeDelta = new Vector2(90, 35);
            useButton.onClick.AddListener(OnUse);

            dropButton = UIHelper.CreateButton("DropBtn", transform,
                "丢弃", 18, new Color(0.4f, 0.2f, 0.2f, 1f), Color.white);
            var dropBtnRT = dropButton.GetComponent<RectTransform>();
            dropBtnRT.anchorMin = new Vector2(0.5f, 0);
            dropBtnRT.anchorMax = new Vector2(0.5f, 0);
            dropBtnRT.pivot = new Vector2(0.5f, 0);
            dropBtnRT.anchoredPosition = new Vector2(110, 10);
            dropBtnRT.sizeDelta = new Vector2(90, 35);
            dropButton.onClick.AddListener(OnDrop);

            equipButton.gameObject.SetActive(false);
            useButton.gameObject.SetActive(false);
            dropButton.gameObject.SetActive(false);
        }
    }
}
