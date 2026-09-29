#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FogHarbor.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FogHarbor.Tests
{
    public class HUDPanelTests
    {
        [TestCase(44, 100, 0.44f)]
        [TestCase(0, 100, 0f)]
        [TestCase(100, 100, 1f)]
        public void ApplyingBarSpritesPreservesHealthFill(int hp, int maxHp, float expected)
        {
            var root = new GameObject("HUD Test", typeof(RectTransform));
            root.SetActive(false);
            var hud = root.AddComponent<HUDPanel>();
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(root.transform, false);
            var fill = fillObject.GetComponent<Image>();
            typeof(HUDPanel).GetField("hpBarFill", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hud, fill);
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                hud.UpdateHp(hp, maxHp);
                hud.ApplyHpBarSprites(null, sprite);
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(fill.fillAmount, Is.EqualTo(expected).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
