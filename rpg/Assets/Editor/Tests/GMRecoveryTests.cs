#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using AIBot.Unity;
using FogHarbor.Debugging;
using FogHarbor.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FogHarbor.Tests
{
    public sealed class GMRecoveryTests
    {
        private GMCommandService gm;
        private NpcAgent npc;
        private AIBotConnectionProfile originalProfile;
        private AIBotConnectionProfile workingProfile;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            SceneManager.LoadScene("Town");
            yield return null;
            yield return null;
            gm = UnityEngine.Object.FindObjectOfType<GMCommandService>();
            Assert.That(gm, Is.Not.Null);
            npc = gm.Agents.First();
            originalProfile = npc.connectionProfile;
            workingProfile = UnityEngine.Object.Instantiate(originalProfile);
            workingProfile.hideFlags = HideFlags.DontSave;
            npc.connectionProfile = workingProfile;
            Assert.That(npc.ReloadConfig(), Is.True);
            yield return Wait(SwitchTest(true));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (workingProfile != null && originalProfile != null)
                workingProfile.gameId = originalProfile.gameId;
            if (gm != null && gm.Testing) yield return Wait(SwitchTest(false));
            if (npc != null)
            {
                npc.connectionProfile = originalProfile;
                npc.gameObject.SetActive(true);
                npc.ReloadConfig();
            }
            if (workingProfile != null) UnityEngine.Object.Destroy(workingProfile);
            yield return new ExitPlayMode();
        }

        private async Task SwitchTest(bool start)
        {
            var result = await gm.SwitchTestAsync(start);
            Assert.That(result.Success, Is.True, result.Message);
        }

        private static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (!task.IsCompleted)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Operation timed out");
                yield return null;
            }
            Assert.That(task.IsCanceled, Is.False);
            if (task.IsFaulted) Assert.Fail(task.Exception.ToString());
        }

        [UnityTest]
        public IEnumerator HiddenNpcRequestIsAwaitedBeforeRestoringFormalSession()
        {
            var chat = npc.ChatAsync("你好。");
            Assert.That(npc.IsBusy, Is.True);
            npc.gameObject.SetActive(false);
            Assert.That(gm.CanWrite, Is.False, "Hidden requests must block GM writes");
            yield return Wait(SwitchTest(false));
            yield return Wait(chat);
            Assert.That(npc.IsBusy, Is.False);
            Assert.That(npc.connectionProfile, Is.SameAs(workingProfile));
            Assert.That(npc.LastServerRequestId, Is.Null, "Restoration must rebuild the backend");
        }

        [UnityTest]
        public IEnumerator FailedProfileRestoreKeepsTestSlotAndProgress()
        {
            var runtimeProfile = npc.connectionProfile;
            Assert.That(gm.AddGold(7).Success, Is.True);
            int gold = gm.Wallet.Gold;
            workingProfile.gameId = "";
            LogAssert.Expect(LogType.Error, "[AIBot] Server Connection Profile 必须填写 gameId 和 npcId");
            yield return Wait(CheckFailure());
            Assert.That(gm.Testing, Is.True);
            Assert.That(gm.Wallet.Gold, Is.EqualTo(gold));
            Assert.That(npc.connectionProfile, Is.SameAs(runtimeProfile));
            Assert.That(gm.TestIdentity, Is.EqualTo(runtimeProfile.playerId));
            workingProfile.gameId = originalProfile.gameId;
        }

        private async Task CheckFailure()
        {
            var result = await gm.SwitchTestAsync(false);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("重载失败"));
        }

        [UnityTest]
        public IEnumerator GmHealCancelsQueuedRespawn()
        {
            var player = gm.Player;
            int sceneHandle = SceneManager.GetActiveScene().handle;
            player.TakeDamage(player.MaxHp + player.Defense);
            player.TakeDamage(1);
            Assert.That(player.CurrentHp, Is.Zero);
            Assert.That(gm.Heal().Success, Is.True);
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(gm.Player, Is.SameAs(player));
            Assert.That(player.CurrentHp, Is.EqualTo(player.MaxHp));
        }

        [UnityTest]
        public IEnumerator CheckpointRestoreCancelsQueuedRespawn()
        {
            var player = gm.Player;
            int sceneHandle = SceneManager.GetActiveScene().handle;
            player.SetHp(37);
            Assert.That(gm.SaveCheckpoint().Success, Is.True);
            player.TakeDamage(player.MaxHp + player.Defense);
            yield return Wait(RestoreCheckpoint());
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneHandle));
            Assert.That(gm.Player, Is.SameAs(player));
            Assert.That(player.CurrentHp, Is.EqualTo(37));
        }

        private async Task RestoreCheckpoint()
        {
            var result = await gm.RestoreCheckpointAsync();
            Assert.That(result.Success, Is.True, result.Message);
        }
    }
}
#endif
