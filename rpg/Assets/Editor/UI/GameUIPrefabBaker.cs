using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FogHarbor.UI;

namespace FogHarbor.EditorTools
{
    /// <summary>
    /// 把「运行时用代码生成的整套 UI」烘焙成预制体，方便在编辑期直接看与改。
    /// 产物：Assets/Resources/UI/GameUI.prefab（Canvas + HUD / 任务追踪 / 背包 / 任务 / 对话 五个面板）。
    /// 外观的唯一来源仍是各面板脚本里的 BuildUI()：改完代码执行一次本菜单重新烘焙即可。
    /// 运行时的取用顺序见 UIManager.ResolveCanvas()（场景实例 → 本预制体 → 代码兜底）。
    /// </summary>
    public static class GameUIPrefabBaker
    {
        private const string PrefabDir = "Assets/Resources/UI";
        private const string PrefabPath = PrefabDir + "/GameUI.prefab";
        private const string BootScenePath = "Assets/Scenes/Boot.scene";
        private const string InstanceName = "GameUI";

        // 血条美术资源（Bloodlines UI 的 Rectangle 血条；包内滑条按 empty/full 同名版本成对使用，这里取 v4 一套）
        private const string HpBarEmptyPath =
            "Assets/ThirdParty/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle/Progress_Bar_Rectangle_empty_v4.png";
        private const string HpBarFullPath =
            "Assets/ThirdParty/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle/Progress_Bar_Rectangle_full_v4.png";
        private const string HpBarEmptySpriteName = "Progress_Bar=Rectangle_empty_v4_0";
        private const string HpBarFullSpriteName = "Progress_Bar=Rectangle_full_v4_0";

        [MenuItem("Tools/雾港小镇/烘焙 UI 预制体")]
        public static void BakeGameUIPrefabMenu()
        {
            if (File.Exists(PrefabPath) &&
                !EditorUtility.DisplayDialog("烘焙 UI 预制体",
                    "已存在 GameUI.prefab。重新烘焙会用面板脚本里的布局覆盖它——" +
                    "预制体上手工改过的素材、位置、尺寸都会丢失。要继续吗？",
                    "覆盖烘焙", "取消"))
                return;

            BakeGameUIPrefab();
        }

        /// <summary>无提示直接烘焙（供脚本调用；菜单入口请用 BakeGameUIPrefabMenu）。</summary>
        public static void BakeGameUIPrefab()
        {
            // 1) 用与运行时完全相同的 Canvas 配置，建一个临时根对象
            var canvas = UIManager.CreateCanvasObject(null);
            canvas.gameObject.name = InstanceName;

            // 2) 五个面板：AddComponent 后显式构建一次层级
            //    （面板里的 UI 引用是 [SerializeField]，会随预制体一起序列化保存）
            var hud = BuildPanel<HUDPanel>("HUD", canvas.transform);
            hud.gameObject.SetActive(true);
            // 血条贴图在这里写进预制体：重新烘焙不会丢（之前是手工在预制体上换的，一烘焙就被覆盖）
            hud.ApplyHpBarSprites(
                LoadSubSprite(HpBarEmptyPath, HpBarEmptySpriteName),
                LoadSubSprite(HpBarFullPath, HpBarFullSpriteName));
            BuildPanel<QuestTrackerPanel>("QuestTrackerPanel", canvas.transform).gameObject.SetActive(true);
            BuildPanel<InventoryPanel>("InventoryPanel", canvas.transform).gameObject.SetActive(false);
            BuildPanel<QuestPanel>("QuestPanel", canvas.transform).gameObject.SetActive(false);
            BuildPanel<DialoguePanel>("DialoguePanel", canvas.transform).gameObject.SetActive(false);

            // 3) 保存（覆盖同名预制体）
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GameUIPrefabBaker] 已生成 {PrefabPath}（HUD 常驻，其余三个面板默认隐藏，可在层级里勾选查看）");
            EditorGUIUtility.PingObject(prefab);
            Selection.activeObject = prefab;
        }

        [MenuItem("Tools/雾港小镇/把 UI 实例放进 Boot 场景")]
        public static void PlaceInstanceIntoBootScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("UI 预制体", "还没烘焙过，请先执行「烘焙 UI 预制体」。", "好");
                return;
            }

            var previousScenePath = EditorSceneManager.GetActiveScene().path;
            if (EditorSceneManager.GetActiveScene().isDirty
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!File.Exists(BootScenePath))
            {
                Debug.LogError($"[GameUIPrefabBaker] 找不到 {BootScenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            GameObject appRoot = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "AppRoot") { appRoot = root; break; }

            if (appRoot == null)
            {
                Debug.LogError("[GameUIPrefabBaker] Boot 场景里没找到 AppRoot");
                return;
            }

            // 清理旧实例（按名字找直接子物体，避免重复挂载）
            for (int i = appRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = appRoot.transform.GetChild(i);
                if (child.name == InstanceName) Object.DestroyImmediate(child.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, appRoot.transform);
            instance.name = InstanceName;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameUIPrefabBaker] 已在 {BootScenePath} 的 AppRoot 下放置 {InstanceName} 实例并保存场景");

            // 回到原来打开的场景
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != BootScenePath)
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        /// <summary>取贴图里的指定子精灵（spriteMode=Multiple 的图集必须按名字取，不能直接取主贴图）。</summary>
        private static Sprite LoadSubSprite(string texturePath, string spriteName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(texturePath))
            {
                if (obj is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            Debug.LogWarning($"[GameUIPrefabBaker] 没找到子精灵 {spriteName}（{texturePath}），血条会退回纯色块");
            return null;
        }

        private static T BuildPanel<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UIHelper.Stretch(go.GetComponent<RectTransform>());

            var panel = go.AddComponent<T>();

            // Awake 在编辑期不保证被调用，这里显式让面板构建一次层级（EnsureUI 是幂等的）
            switch (panel)
            {
                case HUDPanel hud: hud.EnsureUI(); break;
                case QuestTrackerPanel tracker: tracker.EnsureUI(); break;
                case InventoryPanel inventory: inventory.EnsureUI(); break;
                case QuestPanel quest: quest.EnsureUI(); break;
                case DialoguePanel dialogue: dialogue.EnsureUI(); break;
            }
            return panel;
        }
    }
}
