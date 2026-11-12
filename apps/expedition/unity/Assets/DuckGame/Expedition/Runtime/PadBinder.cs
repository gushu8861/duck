using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Binds the scene's spring pads and preview boards to the CURRENT island's exits at
    /// runtime.
    ///
    /// Pads cannot be bound at edit time because the reachable islands change every time the
    /// run advances. This component re-assigns pad[i] -> exits[i] whenever the run changes, so
    /// the number of usable pads always equals the number of real exits (1 or 2), and each pad
    /// carries the id of the island it actually flies to.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class PadBinder : MonoBehaviour
    {
        public SpringPad[] pads;
        public IslandSign[] signs;

        int lastIsland = -1;

        void Start()
        {
            if (pads == null || pads.Length == 0)
                pads = FindObjectsOfType<SpringPad>();
            if (signs == null || signs.Length == 0)
                signs = FindObjectsOfType<IslandSign>();

            var run = ExpeditionRun.Instance;
            if (run != null) run.OnRunChanged += Rebind;
            Rebind();
        }

        void OnDestroy()
        {
            var run = ExpeditionRun.Instance;
            if (run != null) run.OnRunChanged -= Rebind;
        }

        void Rebind()
        {
            var run = ExpeditionRun.Instance;
            if (run == null || run.Graph == null || run.Current == null) return;
            if (run.Current.islandId == lastIsland) return;
            lastIsland = run.Current.islandId;

            var exits = run.Graph.ExitsOf(run.Current.islandId);

            for (int i = 0; i < pads.Length; i++)
            {
                if (pads[i] == null) continue;
                bool hasTarget = i < exits.Count;
                pads[i].targetIslandId = hasTarget ? exits[i] : -1;
                pads[i].gameObject.SetActive(hasTarget);
            }

            for (int i = 0; i < signs.Length; i++)
            {
                if (signs[i] == null) continue;
                bool hasTarget = i < exits.Count;
                signs[i].islandId = hasTarget ? exits[i] : -1;
                signs[i].gameObject.SetActive(hasTarget);
            }
        }
    }
}
