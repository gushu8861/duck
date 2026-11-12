using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;   // Economy lives in the TS namespace

namespace DuckGame.Expedition
{
    /// <summary>
    /// Final expedition result, shown once the boss island is cleared or the player falls.
    /// Kept deliberately small: a summary and a restart, no menus.
    /// </summary>
    public sealed class ExpeditionResult : MonoBehaviour
    {
        public static ExpeditionResult Instance { get; private set; }

        public bool Finished { get; private set; }
        public bool Victory { get; private set; }
        public int IslandsCleared { get; private set; }
        public int Kills { get; private set; }
        public int Coins { get; private set; }
        public int Seconds { get; private set; }

        float startTime;
        GUIStyle box, title, label, small;
        bool styles;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            startTime = Time.time;
            var run = ExpeditionRun.Instance;
            if (run != null) run.OnRunChanged += CheckFinished;
        }

        void OnDestroy()
        {
            var run = ExpeditionRun.Instance;
            if (run != null) run.OnRunChanged -= CheckFinished;
        }

        void CheckFinished()
        {
            var run = ExpeditionRun.Instance;
            if (run == null || Finished) return;

            if (run.BossCleared)
            {
                Victory = true;
                Finished = true;
                Snapshot();
            }
        }

        void Snapshot()
        {
            var run = ExpeditionRun.Instance;
            var cb = CombatBridge.Instance;
            var eco = Economy.Instance;

            IslandsCleared = run != null ? run.ClearedIslands : 0;
            Kills = cb != null ? cb.TotalKills : 0;
            Coins = eco != null ? eco.metaCurrency : 0;
            Seconds = Mathf.RoundToInt(Time.time - startTime);

            // End the run cleanly: stop spawning, drop control back to normal.
            Time.timeScale = 0f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            Finished = false;
            Victory = false;
            startTime = Time.time;

            var run = ExpeditionRun.Instance;
            if (run != null) run.BeginRun(run.runSeed);
            if (WaveController.Instance != null) WaveController.Instance.ClearAll();
        }

        void EnsureStyles()
        {
            if (styles) return;
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 18, 18) };
            title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            styles = true;
        }

        void OnGUI()
        {
            if (!Finished) return;
            EnsureStyles();

            float w = 460f, h = 320f;
            var rect = new Rect((Screen.width - w) * .5f, (Screen.height - h) * .5f, w, h);
            GUILayout.BeginArea(rect, box);

            GUILayout.Label(Victory ? "远征成功" : "远征结束", title);
            GUILayout.Space(14);
            GUILayout.Label("清岛  " + IslandsCleared, label);
            GUILayout.Label("击杀  " + Kills, label);
            GUILayout.Label("金币  " + Coins, label);
            GUILayout.Label("用时  " + (Seconds / 60) + ":" + (Seconds % 60).ToString("00"), label);
            GUILayout.Space(20);

            if (GUILayout.Button("再来一次", GUILayout.Height(42)))
                Restart();

            GUILayout.Label("Exit the boss island to return to the hub on the next pass.", small);
            GUILayout.EndArea();
        }
    }
}
