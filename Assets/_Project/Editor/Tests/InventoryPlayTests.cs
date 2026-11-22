using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Gameplay;
using Game.Level2;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.EditorTests
{
    /// <summary>
    /// The bag that runs through all three rounds (<see cref="RunInventory"/>):
    ///   - store: what's counted in the cart goes into the bag at the exit;
    ///   - Level 2: a car hit knocks 1-2 things out onto the road, walking over them puts them back,
    ///     retrying the round gives back the bag it started with;
    ///   - Shelter: the shelves hold exactly what's in the bag.
    /// Run: Window → General → Test Runner → EditMode → InventoryPlayTests, or Unity -runTests in batch mode.
    /// </summary>
    public class InventoryPlayTests
    {
        const string StoreScene = "Assets/_Project/Scenes/Dev/user.unity";
        const string Level2Scene = "Assets/_Project/Scenes/Level2.unity";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        bool _invincibleWas;

        [SetUp]
        public void SetUp()
        {
            // "Invincible" test mode skips damage AND drops: make sure it's off for these tests
            _invincibleWas = EditorPrefs.GetBool(DevSettings.InvincibleKey, false);
            EditorPrefs.SetBool(DevSettings.InvincibleKey, false);
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.SetBool(DevSettings.InvincibleKey, _invincibleWas);
            Time.timeScale = 1f;
        }

        // ---------- The bag on its own ----------

        [Test]
        public void Bag_RetryingARound_GivesBackWhatItStartedWith()
        {
            var apple = Item("apple");
            var soda = Item("soda");

            GameScore.NewRun();                       // round 1: empty bag
            Assert.AreEqual(0, RunInventory.Count);
            RunInventory.Add(apple, 2);
            RunInventory.Add(soda);
            Assert.AreEqual(3, RunInventory.Count);

            RunInventory.StartRound(2);               // first time in round 2: remembered
            Assert.IsNotNull(RunInventory.TakeRandom());
            Assert.IsNotNull(RunInventory.TakeRandom());
            Assert.AreEqual(1, RunInventory.Count);

            RunInventory.StartRound(2);               // retry: bag is back
            Assert.AreEqual(3, RunInventory.Count);
            Assert.AreEqual(3, RunInventory.CountAtStart(2));

            for (int i = 0; i < 3; i++) Assert.IsNotNull(RunInventory.TakeRandom());
            Assert.IsNull(RunInventory.TakeRandom(), "Empty bag should give nothing.");

            GameScore.NewRun();                       // main menu → new run: empty, checkpoints forgotten
            Assert.AreEqual(0, RunInventory.Count);
            Assert.IsFalse(RunInventory.HasCheckpoint(2));

            Object.DestroyImmediate(apple);
            Object.DestroyImmediate(soda);
        }

        // ---------- Level 2 ----------

        [UnityTest]
        public IEnumerator Level2_HitDropsSupplies_WalkingOverPicksThemUp_RetryGivesBagBack()
        {
            EditorSceneManager.OpenScene(Level2Scene);
            yield return new EnterPlayMode();

            Level2Manager m = null;
            yield return WaitFor<Level2Manager>(x => m = x);
            yield return null; // its Start builds the HUD (and the briefing)
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            StopCars();

            int start = RunInventory.Count;
            Debug.Log($"[InventoryTest] Level2 opened on its own: editor test bag = {start}");
            Assert.Greater(start, 2, "Level2 started from the editor should get a test bag.");

            // A car hit
            yield return Hit(m);
            var drops = Object.FindObjectsByType<DroppedSupply>();
            int lost = start - RunInventory.Count;
            Debug.Log($"[InventoryTest] hit: lost {lost}, on the road {drops.Length}");
            Assert.That(lost, Is.InRange(1, 2), "A hit should knock 1-2 things out of the bag.");
            Assert.AreEqual(lost, drops.Length, "Every lost thing should land on the road.");
            Assert.AreEqual(1, m.Hits);

            // Walk over them
            yield return Wait(1.5f); // they fly, land, and become collectable
            foreach (var d in drops)
            {
                if (d == null) continue;
                yield return TeleportPlayer(d.transform.position);
                yield return null;
                yield return null;
            }
            yield return null;
            Debug.Log($"[InventoryTest] picked up: bag {RunInventory.Count}/{start}, left on road {Object.FindObjectsByType<DroppedSupply>().Length}");
            Assert.AreEqual(start, RunInventory.Count, "Walking over the dropped supplies should put them back in the bag.");
            Assert.AreEqual(0, Object.FindObjectsByType<DroppedSupply>().Length);

            // Lose some, then retry with R: the bag comes back as it was at the start
            yield return Wait(2.1f); // hit cooldown
            yield return Hit(m);
            Assert.Less(RunInventory.Count, start);
            GameManager.Instance?.StartGame();
            SceneLoader.ReloadCurrent();
            Level2Manager fresh = null;
            yield return WaitFor<Level2Manager>(x => fresh = x, notThis: m);
            yield return null; // its Start builds the HUD (and the briefing)
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            Debug.Log($"[InventoryTest] retry: bag {RunInventory.Count}/{start}");
            Assert.AreEqual(start, RunInventory.Count, "Retrying Level 2 should give back the bag it started with.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Level2_CarOverADroppedSupply_CrushesIt()
        {
            EditorSceneManager.OpenScene(Level2Scene);
            yield return new EnterPlayMode();

            Level2Manager m = null;
            yield return WaitFor<Level2Manager>(x => m = x);
            yield return null;
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            var car = Object.FindAnyObjectByType<ChaosCar>();
            Assert.IsNotNull(car, "No cars in Level2.");
            var carCollider = car.GetComponentInChildren<Collider>();
            StopCars();

            int start = RunInventory.Count;
            yield return Hit(m);
            int afterHit = RunInventory.Count;
            var drop = Object.FindAnyObjectByType<DroppedSupply>();
            Assert.IsNotNull(drop);

            // Right after the hit the car can't crush it yet (grace time)
            typeof(DroppedSupply).GetMethod("TryCrush", Private).Invoke(drop, new object[] { carCollider });
            Assert.IsFalse(drop.IsCrushed, "Crushed during the grace time.");

            yield return Wait(1.5f);
            typeof(DroppedSupply).GetMethod("TryCrush", Private).Invoke(drop, new object[] { carCollider });
            Assert.IsTrue(drop.IsCrushed, "A car over a dropped supply should crush it.");

            // A crushed one can't be picked up and disappears
            yield return TeleportPlayer(drop.transform.position);
            yield return Wait(2f);
            Debug.Log($"[InventoryTest] crush: bag {start} -> {afterHit} after hit -> {RunInventory.Count} after crush");
            Assert.AreEqual(afterHit, RunInventory.Count, "A crushed supply must not come back into the bag.");
            Assert.IsTrue(drop == null, "The crushed supply should vanish.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Shelter_EmptyBag_LosesRightAway_AndHasNoCrtLines()
        {
            EditorSceneManager.OpenScene(Level2Scene);
            yield return new EnterPlayMode();

            Level2Manager m = null;
            yield return WaitFor<Level2Manager>(x => m = x);
            yield return null;
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            Assert.IsFalse(ScreenLook.CrtOff, "Level 2 should keep the CRT look.");

            RunInventory.Clear();           // lost everything on the street
            m.ReachShelter();
            yield return null;
            m.EnterShelter();

            ShelterDirector d = null;
            yield return WaitFor<ShelterDirector>(x => d = x, timeout: 20f);
            Assert.IsTrue(ScreenLook.CrtOff, "The Shelter should switch the CRT look off.");
            float t = 0f;
            while (!d.IsOver && t < 15f)
            {
                Game.UI.RoundHud.SkipBriefing();
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Debug.Log($"[InventoryTest] empty bag: supplies={d.TotalSupplies} over={d.IsOver} after {t:0.0}s");
            Assert.AreEqual(0, d.TotalSupplies);
            Assert.IsTrue(d.IsOver, "With nothing on the shelves the round should end at once.");
            Assert.AreEqual(GameState.GameOver, GameManager.Instance.State, "An empty shelter is a loss.");

            yield return new ExitPlayMode();
            Assert.IsFalse(ScreenLook.CrtOff, "Leaving the Shelter should give the CRT look back.");
        }

        [UnityTest]
        public IEnumerator Shelter_MoreKindsThanShelves_NothingIsLost()
        {
            EditorSceneManager.OpenScene(Level2Scene);
            yield return new EnterPlayMode();

            Level2Manager m = null;
            yield return WaitFor<Level2Manager>(x => m = x);
            yield return null;
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            int carried = RunInventory.Count;     // editor test bag: one of every product (13 kinds)
            int kinds = RunInventory.Items.Count;

            m.ReachShelter();
            yield return null;
            m.EnterShelter();

            ShelterDirector d = null;
            yield return WaitFor<ShelterDirector>(x => d = x, ready: x => x.IsRunning, timeout: 20f);
            Game.UI.RoundHud.SkipBriefing();
            int shelves = Object.FindObjectsByType<Shelf>().Length;
            Debug.Log($"[InventoryTest] overflow: {kinds} kinds / {carried} items on {shelves} shelves -> director total {d.TotalSupplies}");
            Assert.Greater(kinds, shelves, "This test needs more kinds of products than shelves.");
            Assert.AreEqual(carried, d.TotalSupplies, "Supplies were lost while putting them on the shelves.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Pause_StopsTimeAndSound_ResumeGoesOn()
        {
            EditorSceneManager.OpenScene(Level2Scene);
            yield return new EnterPlayMode();

            Level2Manager m = null;
            yield return WaitFor<Level2Manager>(x => m = x);
            yield return null;
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            StopCars();
            yield return Wait(0.5f);

            GameManager.Instance.TogglePause();   // what ESC / P does
            yield return null;
            yield return null;
            float timeLeft = m.TimeLeft;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(GameState.Paused, GameManager.Instance.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause, "Sound should stop while paused.");
            Assert.AreEqual(timeLeft, m.TimeLeft, "The round timer ran during the pause.");

            GameManager.Instance.TogglePause();
            yield return Wait(0.5f);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Assert.IsFalse(AudioListener.pause);
            Assert.Less(m.TimeLeft, timeLeft, "The round timer should go on after resume.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Shelter_DroppedSupply_GlowsIsProtected_AndGoesBackWhenPickedUp()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Shelter.unity");
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitFor<ShelterDirector>(x => d = x, ready: x => x.IsRunning);
            Game.UI.RoundHud.SkipBriefing();
            var player = Object.FindAnyObjectByType<Game.Interaction.PlayerInteractor>();
            Assert.IsNotNull(player);

            int ShelfTotal()
            {
                int n = 0;
                foreach (var s in Object.FindObjectsByType<Shelf>()) n += s.Stock;
                return n;
            }
            int before = ShelfTotal();

            // A thief takes something off a shelf
            ShelterIntruder thief = null;
            float t = 0f;
            while (thief == null && t < 40f)
            {
                foreach (var i in Object.FindObjectsByType<ShelterIntruder>())
                    if (i.IsCarrying && !i.IsStopped) thief = i;
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(thief, "No thief ever grabbed anything.");
            int afterGrab = ShelfTotal();

            // Hit once: they drop it — it glows and is protected
            var carried = new List<Game.Interaction.Pickup>();
            foreach (var p in Object.FindObjectsByType<Game.Interaction.Pickup>())
                if (p.IsHeld && p.transform.IsChildOf(thief.transform)) carried.Add(p);
            Assert.AreEqual(1, carried.Count, "Couldn't find what the thief carries.");
            var item = carried[0];
            thief.TakeHit(player.transform.position);
            Assert.IsFalse(thief.IsCarrying);
            var lost = item.GetComponent<LostSupply>();
            Assert.IsNotNull(lost, "A dropped supply should be marked (glow + protection).");
            Assert.IsNotNull(lost.Home, "It should remember its shelf.");
            Assert.IsFalse(lost.CanBeTakenBy(thief), "The thief who dropped it can't grab it at once.");

            float dropT = Time.time;
            while (Time.time - dropT < LostSupply.OtherThievesDelay * 0.9f)
            {
                Assert.IsFalse(item.IsHeld, "A thief grabbed a protected supply.");
                yield return null;
            }

            // The player picks it up (E): it goes straight back on the shelf
            Assert.IsFalse(item.IsHeld, "Someone grabbed it before the player could.");
            player.PickUp(item);
            yield return null;
            yield return null;
            Debug.Log($"[InventoryTest] lost supply: shelves {before} -> {afterGrab} after grab -> {ShelfTotal()} after pick-up");
            Assert.IsFalse(player.IsHoldingAnything, "It should not stay in the player's hands.");
            Assert.IsNotNull(item.CurrentSlot, "It should be back on a shelf.");
            Assert.IsTrue(item.GetComponent<LostSupply>() == null, "No glow once it's back.");
            Assert.AreEqual(afterGrab + 1, ShelfTotal());

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Shelter_EveryProductLeavingAShelf_IsTakenByAThiefStandingThere()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Shelter.unity");
            yield return new EnterPlayMode();

            ShelterDirector d = null;
            yield return WaitFor<ShelterDirector>(x => d = x, ready: x => x.IsRunning);
            Game.UI.RoundHud.SkipBriefing();
            Time.timeScale = 3f;

            var stock = new Dictionary<GameObject, int>();
            foreach (var s in Object.FindObjectsByType<Shelf>()) stock[s.gameObject] = s.Stock;
            int takes = 0;
            var problems = new List<string>();

            void OnStock(GameObject shelfGo, string id, int now)
            {
                int was = stock.TryGetValue(shelfGo, out int w) ? w : now;
                stock[shelfGo] = now;
                if (now >= was) return; // put back
                takes++;
                var shelf = shelfGo.GetComponent<Shelf>();
                ShelterIntruder who = null;
                foreach (var i in Object.FindObjectsByType<ShelterIntruder>())
                    if (i.LastTakenFrom == shelf && i.LastTakeFrame == Time.frameCount) who = i;
                if (who == null)
                {
                    problems.Add($"{shelfGo.name}: {id} left with NO thief taking it (frame {Time.frameCount})");
                    return;
                }
                float dist = Vector3.Distance(Flat(who.transform.position), Flat(shelf.FrontPoint));
                float dy = Mathf.Abs(who.transform.position.y - shelf.transform.position.y);
                if (dist > who.ReachDistance + 0.05f || dy >= ShelterIntruder.FloorMaxY)
                    problems.Add($"{shelfGo.name}: {who.name} took {id} from {dist:0.0} m away, {dy:0.0} m above/below");
            }

            GameEvents.ShelfStockChanged += OnStock;
            float start = Time.time;
            float highest = 0f;
            while (!d.IsOver && Time.time - start < 40f)
            {
                foreach (var i in Object.FindObjectsByType<ShelterIntruder>())
                    if (i.transform.position.y > highest) highest = i.transform.position.y;
                yield return null;
            }
            GameEvents.ShelfStockChanged -= OnStock;
            Debug.Log($"[InventoryTest] highest intruder: y = {highest:0.00}");
            Assert.Less(highest, ShelterIntruder.FloorMaxY, "An intruder walked on a roof (above the room).");

            Debug.Log($"[InventoryTest] shelf watch: {takes} products left shelves, problems: {problems.Count}\n" +
                      string.Join("\n", problems));
            Assert.Greater(takes, 0, "Nobody took anything — the check saw nothing.");
            Assert.IsEmpty(problems, "Products left shelves without a thief standing there.");

            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---------- Store → Level 2 → Shelter ----------

        [UnityTest]
        public IEnumerator FullRun_StoreCart_GoesThroughStreet_OntoShelterShelves()
        {
            EditorSceneManager.OpenScene(StoreScene);
            yield return new EnterPlayMode();

            // 1. Store: finish the list as if every item went into the cart
            StoreRound round = null;
            yield return WaitFor<StoreRound>(r => round = r, ready: r => r.IsRunning);
            Game.UI.RoundHud.SkipBriefing();
            var tracker = Object.FindAnyObjectByType<ShoppingListTracker>();
            Assert.IsNotNull(tracker, "No ShoppingListTracker in the store.");
            MethodInfo collect = typeof(ShoppingListTracker).GetMethod("TryCollect", Private);
            int bought = 0;
            foreach (var entry in tracker.Entries)
            {
                for (int i = entry.Collected; i < entry.Required; i++)
                    collect.Invoke(tracker, new object[] { entry.Item.id });
                bought += entry.Collected;
            }
            Assert.IsTrue(tracker.IsComplete);

            // Walk out through the door
            var exit = Object.FindAnyObjectByType<StoreExit>();
            Assert.IsNotNull(exit, "No StoreExit in the store.");
            var playerCol = Object.FindAnyObjectByType<CharacterController>();
            typeof(StoreExit).GetMethod("OnTriggerEnter", Private).Invoke(exit, new object[] { playerCol });

            // 2. Level 2: the bag is the cart
            Level2Manager m = null;
            yield return WaitFor<Level2Manager>(x => m = x, timeout: 20f);
            yield return null; // its Start builds the HUD (and the briefing)
            yield return null;
            Game.UI.RoundHud.SkipBriefing();
            yield return null;
            StopCars();
            Debug.Log($"[InventoryTest] store bought {bought}, bag in Level2 = {RunInventory.Count}");
            Assert.AreEqual(bought, RunInventory.Count, "The bag should hold everything from the cart.");

            yield return Hit(m); // lose some on the way, leave them on the road
            int carried = RunInventory.Count;
            Assert.Less(carried, bought);

            var expected = new Dictionary<string, int>();
            foreach (var st in RunInventory.Items) expected[st.Item.id] = st.Count;

            m.ReachShelter();
            yield return null;
            m.EnterShelter();

            // 3. Shelter: shelves = bag
            ShelterDirector d = null;
            yield return WaitFor<ShelterDirector>(x => d = x, ready: x => x.IsRunning, timeout: 20f);
            Game.UI.RoundHud.SkipBriefing();
            var onShelves = new Dictionary<string, int>();
            foreach (var p in Object.FindObjectsByType<Game.Interaction.Pickup>())
                if (p.CurrentSlot != null)
                    onShelves[p.ItemId] = onShelves.TryGetValue(p.ItemId, out int n) ? n + 1 : 1;

            Debug.Log($"[InventoryTest] carried {carried}: bag [{Join(expected)}], shelves [{Join(onShelves)}], director total {d.TotalSupplies}");
            Assert.AreEqual(carried, d.TotalSupplies, "Shelter should count exactly what was carried.");
            CollectionAssert.AreEquivalent(expected, onShelves, "Shelves should hold exactly what's in the bag.");

            yield return new ExitPlayMode();
        }

        // ---------- helpers ----------

        static ItemData Item(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.id = id;
            item.displayName = id;
            return item;
        }

        /// <summary>WaitForSeconds doesn't wait in EditMode tests (one frame only): count game time instead.</summary>
        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static IEnumerator Hit(Level2Manager m)
        {
            typeof(Level2Manager).GetField("_safeZones", Private).SetValue(m, 0); // not standing in a bus stop
            Assert.IsTrue(m.RegisterHit(Vector3.forward * 8f), "The hit didn't count.");
            yield return null;
        }

        /// <summary>No random car hits during the test.</summary>
        static void StopCars()
        {
            foreach (var car in Object.FindObjectsByType<ChaosCar>()) car.gameObject.SetActive(false);
        }

        static IEnumerator TeleportPlayer(Vector3 pos)
        {
            var cc = Object.FindAnyObjectByType<CharacterController>();
            cc.enabled = false;
            cc.transform.position = pos;
            cc.enabled = true;
            yield return null;
        }

        static IEnumerator WaitFor<T>(System.Action<T> found, T notThis = null, System.Func<T, bool> ready = null,
            float timeout = 15f) where T : Object
        {
            float t = 0f;
            while (true)
            {
                var x = Object.FindAnyObjectByType<T>();
                if (x != null && x != notThis && (ready == null || ready(x)))
                {
                    found(x);
                    yield break;
                }
                t += Time.unscaledDeltaTime;
                Assert.Less(t, timeout, $"{typeof(T).Name} never showed up (scene now: {SceneManager.GetActiveScene().name}).");
                yield return null;
            }
        }

        static string Join(Dictionary<string, int> d)
        {
            var parts = new List<string>();
            foreach (var kv in d) parts.Add($"{kv.Key} x{kv.Value}");
            parts.Sort();
            return string.Join(", ", parts);
        }
    }
}
