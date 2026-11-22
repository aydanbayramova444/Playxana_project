using System;
using System.Collections.Generic;
using Game.Gameplay;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The player's bag for one run: what was bought in the store (round 1) and is carried
    /// through the street (round 2). Static, so it survives scene loads.
    ///
    ///   RunInventory.Add(item, 2);             // store exit: everything in the cart goes into the bag
    ///   ItemData lost = RunInventory.TakeRandom();  // round 2: a car hit knocks one thing out
    ///   RunInventory.Count / RunInventory.Items     // HUD
    ///
    /// Retrying a round gives back the bag you had when the round started (like <see cref="GameScore"/>):
    /// <see cref="StartRound"/> is called from <see cref="GameScore.StartRound"/>.
    /// </summary>
    public static class RunInventory
    {
        public struct Stack
        {
            public ItemData Item;
            public int Count;
        }

        private static readonly List<Stack> Bag = new List<Stack>();
        // What the bag held when each round started (index = round), so a retry starts the same way.
        private static readonly Dictionary<int, List<Stack>> Checkpoints = new Dictionary<int, List<Stack>>();

        /// <summary>Raised on every change (add / take / round reset).</summary>
        public static event Action Changed;

        public static IReadOnlyList<Stack> Items => Bag;

        /// <summary>Total number of things in the bag.</summary>
        public static int Count
        {
            get
            {
                int n = 0;
                foreach (var s in Bag) n += s.Count;
                return n;
            }
        }

        /// <summary>How many things the bag held when this round started.</summary>
        public static int CountAtStart(int round)
        {
            if (!Checkpoints.TryGetValue(round, out var list)) return Count;
            int n = 0;
            foreach (var s in list) n += s.Count;
            return n;
        }

        public static bool HasCheckpoint(int round) => Checkpoints.ContainsKey(round);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Bag.Clear();
            Checkpoints.Clear();
            Changed = null;
        }

        /// <summary>
        /// Round 1: empty bag. Later rounds: the first time, remember the bag; on a retry, give it back.
        /// Starting an earlier round forgets the later checkpoints.
        /// </summary>
        public static void StartRound(int round)
        {
            var later = new List<int>();
            foreach (int r in Checkpoints.Keys) if (r > round) later.Add(r);
            foreach (int r in later) Checkpoints.Remove(r);

            if (round <= 1)
            {
                Bag.Clear();
                Checkpoints.Clear();
            }
            else if (Checkpoints.TryGetValue(round, out var saved))
            {
                Bag.Clear();
                Bag.AddRange(saved);
            }
            else
            {
                Checkpoints[round] = new List<Stack>(Bag);
            }
            Changed?.Invoke();
        }

        public static void Add(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return;
            int i = Bag.FindIndex(s => s.Item == item);
            if (i >= 0) Bag[i] = new Stack { Item = item, Count = Bag[i].Count + count };
            else Bag.Add(new Stack { Item = item, Count = count });
            Changed?.Invoke();
        }

        /// <summary>Removes one random thing (each single item is equally likely). Null if the bag is empty.</summary>
        public static ItemData TakeRandom()
        {
            int total = Count;
            if (total == 0) return null;

            int pick = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < Bag.Count; i++)
            {
                if (pick >= Bag[i].Count)
                {
                    pick -= Bag[i].Count;
                    continue;
                }
                ItemData item = Bag[i].Item;
                if (Bag[i].Count == 1) Bag.RemoveAt(i);
                else Bag[i] = new Stack { Item = item, Count = Bag[i].Count - 1 };
                Changed?.Invoke();
                return item;
            }
            return null;
        }

        public static void Clear()
        {
            Bag.Clear();
            Changed?.Invoke();
        }
    }
}
