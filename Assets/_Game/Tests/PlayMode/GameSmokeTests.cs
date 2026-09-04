using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Marchio.Tests
{
    public class GameSmokeTests
    {
        [UnityTest]
        public IEnumerator RunStartsSpawnsEnemiesAndPoolsRecycle()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Game.unity", LoadSceneMode.Single);
            yield return null;
            var gm = GameManager.I;
            Assert.IsNotNull(gm);
            Assert.AreEqual(GameMode.Menu, gm.Mode);

            gm.OnScreenTap();
            Assert.AreEqual(GameMode.Play, gm.Mode);
            Assert.AreEqual(1, gm.Run.Level);
            Assert.Greater(gm.Waves.WaveCount, 0, "level 1 should have configured waves");

            float t = 0f;
            while (t < 4f) { t += Time.deltaTime; yield return null; }

            Assert.Greater(gm.Enemies.Count, 0, "level 1 should have spawned chasers");
            Assert.Greater(gm.PlayerProjectiles.Active.Count + gm.EnemyProjectilePools.Sum(p => p.Active.Count), 0, "auto attack or enemies should have fired");

            var enemy = gm.Enemies.Find(e => !e.Dead);
            Assert.IsNotNull(enemy, "a live enemy should exist");
            var pos = enemy.Pos;
            float scoreBefore = gm.Run.LevelScore;
            enemy.Kill();
            yield return null;
            Assert.IsTrue(enemy.Dead);
            Assert.Greater(gm.Run.LevelScore, scoreBefore, "kill should add level score");
            float wait = 0f;
            while (gm.Enemies.Contains(enemy) && wait < 6f) { wait += Time.deltaTime; yield return null; }
            Assert.IsFalse(gm.Enemies.Contains(enemy), "dead enemy released back to pool after its death particles finish");
            Assert.IsFalse(enemy.gameObject.activeSelf);


            gm.Fail();
            Assert.AreEqual(GameMode.Fail, gm.Mode);
            Assert.AreEqual(gm.Preset.freeRevives, gm.Run.RevivesLeft);
            gm.Revive();
            Assert.AreEqual(GameMode.Play, gm.Mode);
            Assert.AreEqual(0, gm.Enemies.Count, "revive restarts the level with a clear field");
            gm.ToMenu();
            Assert.AreEqual(GameMode.Menu, gm.Mode);
        }
    }
}
