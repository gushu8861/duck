using UnityEngine;
using DuckGame.TS;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Music director. The imported soundtrack has an explicit scene label on each track
    /// ("hub", "Power up selection", "Post combat", numbered stage themes), so this component
    /// only decides WHICH moment the player is in and lets the clip names choose themselves.
    ///
    /// Before this existed, no music ever started: the catalogue had no clips registered and
    /// nothing asked the run state for a track.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class MusicDirector : MonoBehaviour
    {
        static MusicDirector instance;

        enum Mood { None, Hub, Battle, Reward, PostCombat, Victory, Scoring }
        Mood mood = Mood.None;
        int battleTrack = -1;

        /// <summary>Stage themes in order; the run walks this list as depth increases.</summary>
        static readonly string[] StagePrefix = { "Desert Stage 01", "Desert Stage 02", "Desert Stage 03" };

        void OnEnable() { instance = this; }

        void Update()
        {
            var run = ExpeditionRun.Instance;
            var rw = RewardSystem.Instance;

            Mood want;
            if (run == null || run.Current == null) want = Mood.Hub;
            else if (rw != null && rw.Choosing) want = Mood.Reward;
            else if (run.BossCleared) want = Mood.Victory;
            else if (!run.Current.completed) want = Mood.Battle;
            else want = Mood.PostCombat;

            if (want != mood) Enter(want, run);
        }

        void Enter(Mood want, ExpeditionRun run)
        {
            var cat = TSAudioCatalogue.Instance;
            if (cat == null) return;
            mood = want;

            switch (want)
            {
                case Mood.Hub:
                    cat.PlayMusic(TSSoundMap.MusicHub, .40f);
                    break;

                case Mood.Battle:
                    // A deeper island runs a different theme, so progress is audible.
                    int depth = run != null && run.Current != null ? run.Current.depth : 1;
                    if (depth != battleTrack)
                    {
                        battleTrack = depth;
                        string pick = BattleTrackFor(depth);
                        if (!string.IsNullOrEmpty(pick) && cat.Has(pick)) cat.PlayMusic(pick, .38f);
                        else cat.PlayMusic(TSSoundMap.MusicMenu, .34f);
                    }
                    break;

                case Mood.Reward:
                    cat.PlayMusic(TSSoundMap.MusicReward, .45f);
                    break;

                case Mood.PostCombat:
                    cat.PlayMusic(TSSoundMap.MusicPostcombat, .40f);
                    break;

                case Mood.Victory:
                    cat.PlayMusic(TSSoundMap.MusicVictory, .55f);
                    break;

                case Mood.Scoring:
                    cat.PlayMusic(TSSoundMap.MusicScoring, .50f);
                    break;
            }
        }

        /// <summary>
        /// Pick a battle theme for a depth. The import has numbered finale tracks plus the
        /// desert stage set; desert tracks are used only when the island is actually desert,
        /// so a snow island never plays desert music.
        /// </summary>
        string BattleTrackFor(int depth)
        {
            var run = ExpeditionRun.Instance;
            bool desert = run != null && run.Current != null && run.Current.biome == Biome.Desert;

            if (desert && depth >= 1 && depth <= StagePrefix.Length)
                return StagePrefix[depth - 1];

            string numbered = depth.ToString("00") + " Final";
            var cat = TSAudioCatalogue.Instance;
            if (cat != null && cat.Has(numbered)) return numbered;

            // wrap over the nine finale themes
            int idx = ((depth - 1) % 9) + 1;
            return idx.ToString("00") + " Final";
        }
    }
}
