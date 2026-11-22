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
            bool shot = false;
            float start = Time.time;
            while (!d.IsOver && Time.time - start < 75f)
            {
                maxInside = Mathf.Max(maxInside, d.IntrudersInside);
                if (!shot && Time.time - start > 14f) { Shot("shelter_intruders"); shot = true; }
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
                    {
                        thief.Interact(player);
                        Assert.IsFalse(thief.IsCarrying, "A hit thief should drop what they carry.");
                    }
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

        [UnityTest]
        public IEnumerator Level2Win_EnterGoesToShelter()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level2.unity");
            yield return new EnterPlayMode();

            Game.Level2.Level2Manager level2 = null;
            float t = 0f;
            while ((level2 = Object.FindAnyObjectByType<Game.Level2.Level2Manager>()) == null && t < 10f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(level2, "No Level2Manager in Level2.unity.");

            level2.ReachShelter();   // you made it to the shelter
            yield return null;
            level2.EnterShelter();   // what Enter / Space does on the win screen

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);
            Assert.AreEqual("Shelter", SceneManager.GetActiveScene().name);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Debug.Log($"[ShelterTest] Level2 -> Shelter OK, supplies={d.TotalSupplies}");

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Intruder_TakesTwoOrThreeHits_ThenDownOrRunsAway()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);
            var player = Object.FindAnyObjectByType<Game.Interaction.PlayerInteractor>();

            int checkedThieves = 0;
            float start = Time.time;
            while (checkedThieves < 4 && Time.time - start < 40f)
            {
                foreach (var thief in Object.FindObjectsByType<ShelterIntruder>())
                {
                    if (thief.IsStopped || thief.Health != thief.MaxHealth) continue;
                    int max = thief.MaxHealth, hits = 0;
                    Assert.That(max, Is.InRange(2, 3), "Intruders should take 2 or 3 hits.");
                    while (!thief.IsStopped && hits < 5)
                    {
                        thief.Interact(player);
                        hits++;
                        if (hits < max) Assert.IsFalse(thief.IsStopped, $"Stopped after {hits} hit(s) but has {max} health.");
                    }
                    Assert.AreEqual(max, hits, "Should stop exactly on the last hit.");
                    Assert.IsTrue(thief.IsDown || thief.IsFleeing, "After the last hit: down or running away.");
                    checkedThieves++;
                }
                yield return null;
            }
            Debug.Log($"[ShelterTest] Hit test: {checkedThieves} intruders each stopped after 2-3 hits");
            Assert.GreaterOrEqual(checkedThieves, 4);

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DroppedItems_AreStolenOffTheFloor()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);

            // Throw every supply onto the floor: the only way to steal now is picking it up off the floor.
            int onFloor = 0;
            foreach (var shelf in Object.FindObjectsByType<Shelf>())
            {
                Game.Interaction.Pickup item;
                while ((item = shelf.TakeOneItem()) != null)
                {
                    item.KnockByNpc(shelf.transform.forward * 1.5f + Random.insideUnitSphere * 0.5f + Vector3.up);
                    onFloor++;
                }
            }
            Assert.Greater(onFloor, 0);

            float start = Time.time;
            while (d.Stolen == 0 && Time.time - start < 40f) yield return null;
            Debug.Log($"[ShelterTest] Floor: {onFloor} items on the floor, stolen={d.Stolen} after {Time.time - start:0.0}s");
            Assert.Greater(d.Stolen, 0, "Intruders never picked anything up off the floor.");

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Supplies_NeverVanishWithoutBeingStolen()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);

            int lastShelf = -1, worst = 0;
            float start = Time.time;
            while (!d.IsOver && Time.time - start < 60f)
            {
                int shelf = 0, loose = 0, carried = 0, alive = 0;
                foreach (var s in Object.FindObjectsByType<Shelf>()) shelf += s.Stock;
                foreach (var p in Object.FindObjectsByType<Game.Interaction.Pickup>())
                {
                    alive++;
                    if (p.IsHeld) carried++;
                    else if (p.CurrentSlot == null) loose++;
                }
                int accounted = alive + d.Stolen;
                if (accounted != d.TotalSupplies)
                {
                    worst = Mathf.Max(worst, Mathf.Abs(d.TotalSupplies - accounted));
                    Debug.LogWarning($"[ShelterTest] MISMATCH t={Time.time - start:0.0}s total={d.TotalSupplies} " +
                                     $"alive={alive} (shelf={shelf} loose={loose} carried={carried}) stolen={d.Stolen}");
                }
                if (shelf != lastShelf)
                {
                    Debug.Log($"[ShelterTest] t={Time.time - start:0.0}s shelf={shelf} loose={loose} carried={carried} stolen={d.Stolen} supplies={d.SuppliesLeft}");
                    lastShelf = shelf;
                }
                yield return null;
            }
            Assert.AreEqual(0, worst, "Some supplies disappeared without being stolen.");

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ShelfSupplies_CantBeGrabbedWithE_ButDroppedOnesCan()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);

            int onRack = 0;
            foreach (var p in Object.FindObjectsByType<Game.Interaction.Pickup>())
                if (p.CurrentSlot != null)
                {
                    onRack++;
                    Assert.IsFalse(p.isActiveAndEnabled, $"{p.name} on a rack can be picked up with E — this was the 'supplies vanish' bug.");
                }
            Assert.Greater(onRack, 0);

            // Knock one off like an intruder would: it must become pickable again
            var shelf = Object.FindAnyObjectByType<Shelf>();
            var item = shelf.TakeOneItem();
            item.KnockByNpc(Vector3.up);
            Assert.IsTrue(item.isActiveAndEnabled, "A supply on the floor should be pickable again.");
            Debug.Log($"[ShelterTest] Lock: {onRack} rack items locked, dropped item pickable");

            // Pictures of the new bunker interior
            ShotFrom("shelter_room_back", new Vector3(0f, 1.7f, -3.5f), Quaternion.Euler(-5f, 0f, 0f));
            ShotFrom("shelter_room_tunnel", new Vector3(-2f, 1.7f, 0f), Quaternion.Euler(-5f, 100f, 0f));
            ShotFrom("shelter_room_bunks", new Vector3(2.5f, 1.7f, 1f), Quaternion.Euler(-5f, -120f, 0f));

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Intruders_ComeThroughBothEntrances()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitForRound(r => d = r);

            var seen = new System.Collections.Generic.HashSet<ShelterIntruder>();
            int main = 0, tunnel = 0;
            float start = Time.time;
            bool shot = false;
            while (Time.time - start < 50f && (main == 0 || tunnel == 0))
            {
                foreach (var i in Object.FindObjectsByType<ShelterIntruder>())
                {
                    if (!seen.Add(i)) continue;
                    Vector3 p = i.transform.position;
                    if (p.x > 9f) tunnel++;
                    else if (p.z < -8f) main++;
                }
                if (!shot && tunnel > 0) { Shot("shelter_tunnel_intruders"); shot = true; }
                yield return null;
            }
            Debug.Log($"[ShelterTest] Entrances: main corridor={main}, maintenance tunnel={tunnel}");
            Assert.Greater(main, 0, "Nobody came through the main corridor.");
            Assert.Greater(tunnel, 0, "Nobody came through the maintenance tunnel.");

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

        /// <summary>Saves what the player camera sees (only when HUMAN_RENDER_DIR is set) — for checking the look.</summary>
        static void Shot(string name)
        {
            string dir = System.Environment.GetEnvironmentVariable("HUMAN_RENDER_DIR");
            Camera cam = Camera.main;
            if (string.IsNullOrEmpty(dir) || cam == null) return;
            var rt = new RenderTexture(1280, 720, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
        }

        static void ShotFrom(string name, Vector3 pos, Quaternion rot)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 p0 = cam.transform.position; Quaternion r0 = cam.transform.rotation;
            cam.transform.SetPositionAndRotation(pos, rot);
            Shot(name);
            cam.transform.SetPositionAndRotation(p0, r0);
        }
    }
}
