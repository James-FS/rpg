using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FogHarbor.Quest;
using FogHarbor.Player;

namespace FogHarbor.UI
{
    /// <summary>
    /// HUD 面板：HP 条 + 交互提示 + toast。常驻屏幕。
    /// §6.1 差距 #3：HP 由事件驱动刷新（OnPlayerHpChanged），不再每帧轮询。
    /// 金币不在此显示（数值仍由 RewardService 维护，只是不上 HUD）。
    /// </summary>
    public class HUDPanel : MonoBehaviour
    {
        // UI 引用：用 Prefab 时来自序列化数据（可在 Inspector 上换），纯代码构建时由 BuildUI() 赋值
        [SerializeField] private Image hpBarBg;
        [SerializeField] private Image hpBarFill;
        [SerializeField] private TextMeshProUGUI hpLabelText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TextMeshProUGUI toastText;

        // 血条美术资源（Bloodlines UI 的 Rectangle 血条；由烘焙器写入预制体，为空时退回纯色块）
        [SerializeField] private Sprite hpBarBgSprite;
        [SerializeField] private Sprite hpBarFillSprite;
        private Coroutine toastCoroutine;

        // ─── 血条行布局：左「HP」标签 + 右侧血条（数值压在条内）───
        private const float HP_LABEL_W = 40f;   // 「HP」标签宽
        private const float BAR_GAP    = 6f;    // 标签与血条的间距
        private const float BAR_H      = 26f;   // 血条高
        private const float BAR_W      = 266f;  // 血条宽
        private const float EDGE_MARGIN = 20f;  // 距屏幕左上角
        private const int   FONT_LABEL = 22;
        private const int   FONT_VALUE = 20;

        private PlayerController player;
        private QuestSystem questSystem;

        private static readonly Color HP_COLOR = new Color(0.7f, 0.15f, 0.15f, 1f);
        private static readonly Color HP_BG = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        public void Initialize(PlayerController player, QuestSystem qs)
        {
            SetPlayer(player);
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

        private void OnDestroy()
        {
            if (player != null)
                player.OnPlayerHpChanged -= UpdateHp;
            if (questSystem != null)
                questSystem.OnQuestStateChanged -= OnQuestStateChanged;
        }

        public void UpdateHp(int current, int max)
        {
            if (hpText != null)
                hpText.text = $"{current}/{max}";   // 「HP」由左侧标签负责，条内只放数值
            if (hpBarFill != null)
                hpBarFill.fillAmount = max > 0 ? (float)current / max : 0f;
        }

        public void SetPrompt(string text)
        {
            if (promptText != null)
            {
                promptText.text = text ?? "";
                promptText.gameObject.SetActive(!string.IsNullOrEmpty(text));
                SetOutline(promptText);   // 首帧显示时才会创建 TMP 材质，所以在这里补描边
            }
        }

        /// <summary>短暂显示一条提示（如"获得月光药草 x1"），2 秒后自动消失。</summary>
        public void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            toastText.gameObject.SetActive(true);
            SetOutline(toastText);        // 首帧显示时才会创建 TMP 材质，所以在这里补描边

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
            EnsureUI();
        }

        private void Start()
        {
            // 放到 Start：TMP 的共享材质要到 Awake 之后才就绪，早了会在 SetOutlineThickness 里报空引用
            ApplyTextOutline();
            ApplyBarSprites();
        }

        /// <summary>
        /// 给血条套上美术资源（烘焙 UI 预制体时调用一次即写进预制体）。
        /// 传 null 则保持 BuildUI() 里的纯色块，删掉贴图也不会崩。
        /// </summary>
        public void ApplyHpBarSprites(Sprite background, Sprite fill)
        {
            hpBarBgSprite = background;
            hpBarFillSprite = fill;
            ApplyBarSprites();
        }

        /// <summary>把序列化的血条贴图应用到 Image 上（没配贴图就维持纯色块）。</summary>
        private void ApplyBarSprites()
        {
            if (hpBarBg != null && hpBarBgSprite != null)
            {
                hpBarBg.sprite = hpBarBgSprite;
                hpBarBg.type = Image.Type.Simple;   // 无九宫格边框，按原图拉伸（与 Bloodlines 自带滑条一致）
                hpBarBg.color = Color.white;
            }

            if (hpBarFill != null && hpBarFillSprite != null)
            {
                hpBarFill.sprite = hpBarFillSprite;
                hpBarFill.type = Image.Type.Filled;
                hpBarFill.fillMethod = Image.FillMethod.Horizontal;
                hpBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                hpBarFill.fillAmount = 1f;
                hpBarFill.color = Color.white;
            }
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

            if (hpBarBg == null || hpBarFill == null || hpLabelText == null || hpText == null
                || promptText == null || toastText == null)
                Debug.LogWarning("[HUDPanel] 预制体里有未绑定的 UI 引用，HUD 会显示不全，请在 Inspector 上补齐。", this);
        }

        /// <summary>
        /// 常显文字（HP/金币）加描边。常显的两个在 Awake 里材质已就绪，放 Start 更稳妥。
        /// </summary>
        private void ApplyTextOutline()
        {
            SetOutline(hpLabelText);
            SetOutline(hpText);
            SetOutline(toastText);
        }

        /// <summary>
        /// 给叠在场景上的文字加轻微描边（草地图案上也看得清）。
        /// 不走 TMP 的 outlineWidth 接口：对没激活过的文本它会走到未初始化的 CanvasRenderer 上抛空引用，
        /// 这里直接改实例材质的描边属性。
        /// </summary>
        private static void SetOutline(TextMeshProUGUI t)
        {
            if (t == null || t.fontSharedMaterial == null)
                return;

            var mat = t.fontMaterial;   // 按需创建实例材质
            if (mat == null || !mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                return;

            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.15f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.75f));
            t.SetMaterialDirty();
        }

        private void BuildUI()
        {
            // ── 左上角：血条行（左「HP」标签 + 右侧血条，数值压在条内）──

            float containerW = HP_LABEL_W + BAR_GAP + BAR_W;

            var hpContainer = UIHelper.CreateObject("HPContainer", transform);
            var hpRT = hpContainer.GetComponent<RectTransform>();
            hpRT.anchorMin = new Vector2(0, 1);
            hpRT.anchorMax = new Vector2(0, 1);
            hpRT.pivot = new Vector2(0, 1);
            hpRT.anchoredPosition = new Vector2(EDGE_MARGIN, -EDGE_MARGIN);
            hpRT.sizeDelta = new Vector2(containerW, BAR_H);

            // 「HP」标签（与血条同一行，垂直居中对齐）
            hpLabelText = UIHelper.CreateText("HPLabel", hpContainer.transform,
                "HP", FONT_LABEL, Color.white, TextAlignmentOptions.Left);
            var labelRT = hpLabelText.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 1);
            labelRT.anchorMax = new Vector2(0, 1);
            labelRT.pivot = new Vector2(0, 1);
            labelRT.anchoredPosition = new Vector2(0, 0);
            labelRT.sizeDelta = new Vector2(HP_LABEL_W, BAR_H);

            // 血条（贴在标签右侧）
            hpBarBg = UIHelper.CreateImage("HPBarBg", hpContainer.transform, HP_BG);
            var hpBgRT = hpBarBg.GetComponent<RectTransform>();
            hpBgRT.anchorMin = new Vector2(0, 1);
            hpBgRT.anchorMax = new Vector2(0, 1);
            hpBgRT.pivot = new Vector2(0, 1);
            hpBgRT.anchoredPosition = new Vector2(HP_LABEL_W + BAR_GAP, 0);
            hpBgRT.sizeDelta = new Vector2(BAR_W, BAR_H);

            // HP 条填充
            hpBarFill = UIHelper.CreateImage("HPBarFill", hpBarBg.transform, HP_COLOR);
            hpBarFill.type = Image.Type.Filled;
            hpBarFill.fillMethod = Image.FillMethod.Horizontal;
            hpBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var fillRT = hpBarFill.GetComponent<RectTransform>();
            UIHelper.Stretch(fillRT);

            // 数值：压在血条内居中
            hpText = UIHelper.CreateText("HPText", hpBarBg.transform,
                "100/100", FONT_VALUE, Color.white, TextAlignmentOptions.Center);
            UIHelper.Stretch(hpText.rectTransform);

            ApplyBarSprites();   // 已配贴图（预制体路径）则套上贴图，否则保持上面的纯色块

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
