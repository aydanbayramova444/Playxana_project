using System.Collections;
using Game.Core;
using Game.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.EditorTests
{
    /// <summary>
    /// Plays Level 3 (Shelter) for real in Play mode, 3x faster:
    ///   1. A player who does nothing LOSES (everything stolen), then TRY AGAIN starts a fresh round.
    ///   2. A player who stops every thief WINS with all supplies kept, then MAIN MENU opens the menu.
    /// Any error / exception in the Console during a test fails it.
    /// Run: Window → General → Test Runner → EditMode → ShelterPlayTests, or Unity -runTests in batch mode.
    /// </summary>
    public class ShelterPlayTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Shelter.unity";
        const float Speed = 3f;

        [UnityTest]
        public IEnumerator DoingNothing_Loses_ThenTryAgainRestarts()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);
            Assert.Greater(d.TotalSupplies, 0, "No supplies were counted on the shelves.");

            int maxInside = 0;
            float start = Time.time;
            while (!d.IsOver && Time.time - start < 75f)
            {
                maxInside = Mathf.Max(maxInside, d.IntrudersInside);
                yield return null;
            }

            float lostAfter = Time.time - start;
            Debug.Log($"[ShelterTest] Passive: over={d.IsOver} stolen={d.Stolen}/{d.TotalSupplies} " +
                      $"after {lostAfter:0.0}s, max intruders at once={maxInside}");
            Assert.IsTrue(d.IsOver, "Round never ended for a passive player.");
            Assert.Less(lostAfter, 55f, "Too easy: a passive player almost survives the round.");
            Assert.AreEqual(d.TotalSupplies, d.Stolen, "Passive player should lose everything.");
            Assert.AreEqual(GameState.GameOver, GameManager.Instance.State);
            Assert.GreaterOrEqual(maxInside, 2, "Intruders should come in groups of 2-3.");

            // TRY AGAIN → a brand-new round
            d.TryAgain();
            ShelterDirector fresh = null;
            yield return WaitForRound(r => fresh = r, notThis: d);
            Assert.AreEqual(0, fresh.Stolen);
            Assert.Greater(fresh.TimeLeft, 50f);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StoppingEveryThief_Wins_ThenMainMenuOpens()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);
            var player = Object.FindAnyObjectByType<Game.Interaction.PlayerInteractor>();
            Assert.IsNotNull(player, "No PlayerInteractor in the Shelter scene.");
            var bat = GameObject.Find("Bat");
            Assert.IsNotNull(bat, "The bat is not in the player's hand.");
            Assert.IsTrue(bat.transform.IsChildOf(player.Camera.transform), "The bat should follow the camera.");

            float start = Time.time;
            while (!d.IsOver && Time.time - start < 80f)
            {
                // Perfect defender: the moment a thief has something in hand, press E on them.
                foreach (var thief in Object.FindObjectsByType<ShelterIntruder>())
                    if (thief.IsCarrying && thief.CanInteract(player))
                        thief.Interact(player);
                yield return null;
            }

            Debug.Log($"[ShelterTest] Defender: over={d.IsOver} stolen={d.Stolen}/{d.TotalSupplies} " +
                      $"scared={d.ScaredOff} after {Time.time - start:0.0}s");
            Assert.IsTrue(d.IsOver, "Round never ended.");
            Assert.AreEqual(GameState.Victory, GameManager.Instance.State, "Holding out should be a win.");
            Assert.AreEqual(0, d.Stolen, "Nothing should be stolen when every thief is stopped.");
            Assert.Greater(d.ScaredOff, 0, "No intruder was ever scared off — did any come in?");

            // MAIN MENU
            d.ToMenu();
            float t = 0f;
            while (SceneManager.GetActiveScene().name != "MainMenu" && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        /// <summary>Waits until a ShelterDirector has counted the supplies and started, then speeds time up.</summary>
        static IEnumerator WaitForRound(System.Action<ShelterDirector> found, ShelterDirector notThis = null)
        {
            float t = 0f;
            while (true)
            {
                var d = Object.FindAnyObjectByType<ShelterDirector>();
                if (d != null && d != notThis && d.IsRunning)
                {
                    Time.timeScale = Speed;
                    found(d);
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                Assert.Less(t, 15f, "ShelterDirector never started a round.");
                yield return null;
            }
        }
    }
}
