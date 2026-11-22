using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The score of one run (round 1 → 2 → 3) and the saved records, so the game is worth replaying.
    ///
    ///   GameScore.StartRound(2);               // clears this round (and later ones): retrying can't farm points
    ///   GameScore.Set(2, "DISTANCE", 640);     // a named line of that round's score, can be updated live
    ///   GameScore.Finish(2, cleared: true);    // round over: saves the best run and the best round
    ///
    /// Records are kept in PlayerPrefs (on each player's computer).
    /// </summary>
    public static class GameScore
    {
        public const int Rounds = 3;
        private const string BestRunKey = "Score.BestRun", BestRoundKey = "Score.BestRound";

        public struct Line
        {
            public string Label;
            public int Points;
        }

        private static readonly List<Line>[] Lines = { new List<Line>(), new List<Line>(), new List<Line>() };
        private static readonly bool[] Cleared = new bool[Rounds];
        private static readonly HashSet<int> Briefed = new HashSet<int>();

        /// <summary>The last finished round set a new best for the whole run.</summary>
        public static bool NewRunRecord { get; private set; }
        /// <summary>The last finished round set a new best for that round.</summary>
        public static bool NewRoundRecord { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => NewRun();

        /// <summary>Main menu: forget the run (records stay) and show the briefings again.</summary>
        public static void NewRun()
        {
            Briefed.Clear();
            StartRound(1);
        }

        public static void StartRound(int round)
        {
            for (int i = round - 1; i < Rounds; i++)
            {
                Lines[i].Clear();
                Cleared[i] = false;
            }
            NewRunRecord = NewRoundRecord = false;
            RunInventory.StartRound(round); // retrying a round also gives back the bag it started with
        }

        /// <summary>True only the first time in a run: retrying a round skips its briefing.</summary>
        public static bool TakeBriefing(int round) => Briefed.Add(round) && !DevSettings.SkipBriefings;

        public static IReadOnlyList<Line> LinesOf(int round) => Lines[round - 1];
        public static bool IsCleared(int round) => Cleared[round - 1];

        public static void Set(int round, string label, int points)
        {
            var lines = Lines[round - 1];
            int i = lines.FindIndex(l => l.Label == label);
            var line = new Line { Label = label, Points = points };
            if (i < 0) lines.Add(line);
            else if (lines[i].Points == points) return;
            else lines[i] = line;
            GameEvents.RaiseScoreChanged(Total);
        }

        public static int RoundTotal(int round)
        {
            int sum = 0;
            foreach (Line l in Lines[round - 1]) sum += l.Points;
            return sum;
        }

        public static int Total
        {
            get
            {
                int sum = 0;
                for (int r = 1; r <= Rounds; r++) sum += RoundTotal(r);
                return sum;
            }
        }

        public static int BestRun => PlayerPrefs.GetInt(BestRunKey, 0);
        public static int BestRound(int round) => PlayerPrefs.GetInt(BestRoundKey + round, 0);

        public static void Finish(int round, bool cleared)
        {
            Cleared[round - 1] = cleared;
            int total = Total, points = RoundTotal(round);
            NewRunRecord = total > BestRun;
            NewRoundRecord = points > BestRound(round);
            if (!NewRunRecord && !NewRoundRecord) return;

            if (NewRunRecord) PlayerPrefs.SetInt(BestRunKey, total);
            if (NewRoundRecord) PlayerPrefs.SetInt(BestRoundKey + round, points);
            PlayerPrefs.Save();
        }

        /// <summary>Letter rank of a whole run: something to beat next time.</summary>
        public static string Rank(int total) =>
            total >= 8500 ? "S" : total >= 7000 ? "A" : total >= 5000 ? "B" : total >= 3000 ? "C" : "D";
    }
}
