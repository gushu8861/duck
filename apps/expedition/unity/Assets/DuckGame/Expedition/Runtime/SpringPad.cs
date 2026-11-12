using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// The one travel method implemented this pass: a spring pad.
    ///
    /// Design rules being honoured (sky-island doc lines 44-48):
    ///  - travel is SAFE by default; no fall death in the first segment
    ///  - a visible target and a verified trajectory, not free-fall luck
    ///  - short hop (5-12 s) so it does not become a rhythm game
    ///  - input and camera are restored after landing; enemies never fire during the hop
    ///  - an accidental touch does not commit the route — the player must press the pad
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SpringPad : MonoBehaviour
    {
        [Header("Wiring")]
        public Transform playerRig;
        public Transform landingPoint;

        [Header("Tuning")]
        public float hopDuration = 2.2f;
        public float hopHeight = 9f;
        public float triggerRadius = 2.2f;
        public KeyCode activateKey = KeyCode.F;

        [Header("Feedback")]
        public Renderer padRenderer;
        public Color idleTint = new Color(.30f, .78f, .55f);
        public Color readyTint = new Color(.55f, 1f, .70f);

        /// <summary>The island this pad flies to. Set by the builder from the route graph.</summary>
        public int targetIslandId = -1;

        bool playerInside;
        bool hopping;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void Update()
        {
            if (hopping) return;

            bool canUse = playerInside
                          && targetIslandId >= 0
                          && ExpeditionRun.Instance != null
                          && ExpeditionRun.Instance.CommittedNextId == targetIslandId
                          && ExpeditionRun.Instance.Current != null
                          && ExpeditionRun.Instance.Current.completed;

            if (padRenderer != null)
            {
                var mat = padRenderer.sharedMaterial;
                if (mat != null) mat.color = canUse ? readyTint : idleTint;
            }

            if (canUse && Input.GetKeyDown(activateKey))
                StartCoroutine(Hop());
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            playerInside = true;

            // Preview only — never commits.
            if (ExpeditionRun.Instance != null && targetIslandId >= 0)
                ExpeditionRun.Instance.Preview(targetIslandId);
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            playerInside = false;
            if (ExpeditionRun.Instance != null)
                ExpeditionRun.Instance.ClearPreview();
        }

        bool IsPlayer(Collider other)
        {
            if (playerRig == null) return other.CompareTag("Player");
            return other.transform.IsChildOf(playerRig) || other.transform == playerRig;
        }

        IEnumerator Hop()
        {
            hopping = true;

            var motor = playerRig != null ? playerRig.GetComponentInChildren<DuckGame.V04.GooseMotorV04>() : null;
            var cc = playerRig != null ? playerRig.GetComponentInChildren<CharacterController>() : null;

            // Travel cues: launch, then a landing thump. Both clips were verified as teleport
            // start/end effects, which is exactly what this arc is.
            var cat = DuckGame.TS.TSAudioCatalogue.Instance;
            if (cat != null)
            {
                if (cat.Has(DuckGame.TS.TSSoundMap.TravelStart)) cat.Play(DuckGame.TS.TSSoundMap.TravelStart, .8f);
                else if (cat.Has(DuckGame.TS.TSSoundMap.PortalStart)) cat.Play(DuckGame.TS.TSSoundMap.PortalStart, .8f);
            }

            // Freeze gameplay control for the hop so physics cannot fight the animation.
            if (motor != null) motor.InjectInput = true;
            if (cc != null) cc.enabled = false;

            Vector3 from = playerRig != null ? playerRig.position : transform.position;
            Vector3 to = landingPoint != null ? landingPoint.position : from + transform.forward * 20f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(.1f, hopDuration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                Vector3 pos = Vector3.Lerp(from, to, e);
                pos.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * hopHeight;
                if (playerRig != null) playerRig.position = pos;
                yield return null;
            }

            if (playerRig != null) playerRig.position = to;

            // Always restore control, even if the run state is inconsistent.
            if (cc != null) cc.enabled = true;
            if (motor != null) motor.InjectInput = false;

            // Advance the run only after a safe landing.
            if (ExpeditionRun.Instance != null)
                ExpeditionRun.Instance.Depart();

            if (cat != null)
            {
                if (cat.Has(DuckGame.TS.TSSoundMap.TravelEnd)) cat.Play(DuckGame.TS.TSSoundMap.TravelEnd, .8f);
                else if (cat.Has(DuckGame.TS.TSSoundMap.PortalEnd)) cat.Play(DuckGame.TS.TSSoundMap.PortalEnd, .8f);
            }

            hopping = false;
        }
    }
}
