using System.Collections;
using System.Reflection;
using Game.Core;
using Game.Gameplay;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.EditorTests
{
    /// <summary>
    /// Plays Scene 1 (the store, Scenes/Dev/user.unity) for real in Play mode, 3x faster:
    ///   - the shopper count from the Inspector is used; shoppers spawn OUTSIDE the door and walk in;
    ///   - they never hold more than 2 items, and with full hands they make a mess;
    ///   - pressing E on a shopper takes their items;
    ///   - 60 s run out → lose, TRY AGAIN restarts; shopping list complete → the timer stops (no lose).
    /// </summary>
    public class StorePlayTests
    {
        const string ScenePath = "Assets/_Project/Scenes/Dev/user.unity";
        const float Speed = 3f;
        const float DoorZ = -1.9f; // the StoreExit trigger sits in the doorway; outside is z < DoorZ

        [UnityTest]
        public IEnumerator Shoppers_SpawnOutside_Count_TwoHandsMax_MakeMess()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            StoreRound round = null;
            yield return WaitForRound(r => round = r, beforeStart: r => r.ShopperCount = 4); // configurable count
            Assert.Less(round.SpawnPosition.z, DoorZ, "Shoppers should spawn outside the entrance door.");

            int maxHeld = 0, mess = 0;
            bool someoneInside = false, someoneFull = false;
            float start = Time.time;
            while (Time.time - start < 45f && !round.IsOver)
            {
                foreach (var s in round.Shoppers)
                {
                    if (s == null) continue;
                    maxHeld = Mathf.Max(maxHeld, s.HeldCount);
                    someoneFull |= s.HandsFull;
                    someoneInside |= s.transform.position.z > DoorZ + 1f;
                }
                yield return null;
            }
            foreach (var s in round.Shoppers) if (s != null) mess += s.MessMade;
            // Look at the nearest shopper and take a picture
            var cam = Camera.main;
            StoreShopper near = null;
            foreach (var s in round.Shoppers) if (s != null && (near == null || Vector3.Distance(s.transform.position, cam.transform.position) < Vector3.Distance(near.transform.position, cam.transform.position))) near = s;
            if (near != null) { cam.transform.parent.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(near.transform.position - cam.transform.position, Vector3.up)); cam.transform.localRotation = Quaternion.identity; }
            Shot("store_shoppers");
            // Wide shots of the decorated store from the middle of the shelves
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var sh in Object.FindObjectsByType<Shelf>()) { c += sh.transform.position; n++; }
            if (n > 0)
            {
                c /= n;
                ShotFrom("store_decor_back", c + new Vector3(0f, 1.7f, -2.5f), Quaternion.Euler(-8f, 0f, 0f));
                ShotFrom("store_decor_left", c + new Vector3(2f, 1.7f, 0f), Quaternion.Euler(-8f, -100f, 0f));
                ShotFrom("store_decor_front", c + new Vector3(0f, 1.7f, 2f), Quaternion.Euler(-5f, 180f, 0f));
            }

            Debug.Log($"[StoreTest] spawned={round.Spawned} maxHeld={maxHeld} full={someoneFull} inside={someoneInside} mess={mess}");
            Assert.AreEqual(4, round.Spawned, "Spawn count from the Inspector was not used.");
            Assert.IsTrue(someoneInside, "No shopper walked into the store.");
            Assert.LessOrEqual(maxHeld, StoreShopper.HandsCapacity, "A shopper held more than 2 items.");
            Assert.IsTrue(someoneFull, "No shopper ever filled both hands.");
            Assert.Greater(mess, 0, "Shoppers with full hands never made a mess.");

            yield return Exit();
        }

        [UnityTest]
        public IEnumerator PressingE_TakesItemsFromShopper()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            StoreRound round = null;
            yield return WaitForRound(r => round = r);
            var player = Object.FindAnyObjectByType<Game.Interaction.PlayerInteractor>();
            Assert.IsNotNull(player, "No player in the store scene.");

            StoreShopper carrier = null;
            float start = Time.time;
            while (carrier == null && Time.time - start < 40f)
            {
                foreach (var s in round.Shoppers) if (s != null && s.HeldCount > 0) carrier = s;
                yield return null;
            }
            Assert.IsNotNull(carrier, "No shopper picked anything up.");
            Assert.IsFalse(player.IsHoldingAnything);
            Assert.IsTrue(carrier.CanInteract(player), "E should be possible on a shopper holding items.");

            carrier.Interact(player); // what pressing E does
            yield return null;

            Assert.AreEqual(0, carrier.HeldCount, "The shopper still holds items after E.");
            Assert.IsTrue(player.IsHoldingAnything, "The item did not go into the player's hand.");
            Debug.Log($"[StoreTest] E took {player.HeldItem.ItemId} from {carrier.name}");

            yield return Exit();
        }

        [UnityTest]
        public IEnumerator TimeRunsOut_Loses_ThenTryAgainRestarts()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            StoreRound round = null;
            yield return WaitForRound(r => round = r);
            float start = Time.time;
            while (!round.IsOver && Time.time - start < 70f) yield return null;

            Debug.Log($"[StoreTest] timer: over={round.IsOver} after {Time.time - start:0.0}s");
            Assert.IsTrue(round.IsOver, "60 s passed but the round did not end.");
            Assert.AreEqual(GameState.GameOver, GameManager.Instance.State);

            round.TryAgain();
            StoreRound fresh = null;
            yield return WaitForRound(r => fresh = r, notThis: round);
            Assert.Greater(fresh.TimeLeft, 55f);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);

            yield return Exit();
        }

        [UnityTest]
        public IEnumerator ListComplete_StopsTimer_NoLose()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();

            StoreRound round = null;
            yield return WaitForRound(r => round = r);

            // Collect the whole list (as if every item went into the cart)
            var tracker = Object.FindAnyObjectByType<ShoppingListTracker>();
            Assert.IsNotNull(tracker, "No ShoppingListTracker in the store scene.");
            MethodInfo collect = typeof(ShoppingListTracker).GetMethod("TryCollect", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var entry in tracker.Entries)
                for (int i = entry.Collected; i < entry.Required; i++)
                    collect.Invoke(tracker, new object[] { entry.Item.id });
            Assert.IsTrue(tracker.IsComplete);
            yield return null;
            Assert.IsTrue(round.ListDone, "StoreRound did not notice the finished list.");

            float start = Time.time;
            while (Time.time - start < 65f) yield return null;
            Debug.Log($"[StoreTest] list done: over={round.IsOver} timeLeft={round.TimeLeft:0.0}");
            Assert.IsFalse(round.IsOver, "Lost even though the shopping list was complete.");
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);

            yield return Exit();
        }

        // ---------- helpers ----------

        static IEnumerator WaitForRound(System.Action<StoreRound> found, StoreRound notThis = null,
            System.Action<StoreRound> beforeStart = null)
        {
            float t = 0f;
            bool configured = false;
            while (true)
            {
                var r = Object.FindAnyObjectByType<StoreRound>();
                if (r != null && r != notThis && !configured && beforeStart != null)
                {
                    beforeStart(r); // before the first shopper spawns
                    configured = true;
                }
                if (r != null && r != notThis && r.IsRunning)
                {
                    Game.UI.RoundHud.SkipBriefing();
                    Time.timeScale = Speed;
                    found(r);
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                Assert.Less(t, 20f, "StoreRound never started — run Tools → Store → Add NPC Shoppers + 60s Timer.");
                yield return null;
            }
        }

        static IEnumerator Exit()
        {
            Time.timeScale = 1f;
            yield return new ExitPlayMode();
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
