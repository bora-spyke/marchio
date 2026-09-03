using System.Collections;
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
            Assert.AreEqual(1, gm.Waves.Wave);

            float t = 0f;
            while (t < 4f) { t += Time.deltaTime; yield return null; }

            Assert.Greater(gm.Enemies.Count, 0, "wave 1 should have spawned chasers");
            Assert.Greater(gm.PlayerProjectiles.Active.Count + gm.EnemyProjectiles.Active.Count, 0, "auto attack or enemies should have fired");

            var enemy = gm.Enemies[0];
            var pos = enemy.Pos;
            enemy.Kill();
            yield return null;
            Assert.IsFalse(gm.Enemies.Contains(enemy), "dead enemy released back to pool");
            Assert.IsFalse(enemy.gameObject.activeSelf);

            var poly = new System.Collections.Generic.List<Vector2>
            {
                pos + new Vector2(-40, -40), pos + new Vector2(40, -40), pos + new Vector2(40, 40), pos + new Vector2(-40, 40)
            };
            LoopDamage.Resolve(poly);
            Assert.AreEqual(1, gm.Barriers.Active.Count, "closing a loop always spawns a barrier");

            gm.EndRun();
            Assert.AreEqual(GameMode.Over, gm.Mode);
        }
    }
}
