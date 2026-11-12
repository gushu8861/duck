using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Visual polish for the spring pad: a rotating ring, a column of light while the pad is
    /// armed, and a burst on launch. All procedural so no extra assets are needed, and all
    /// driven by state the player can act on (armed only after the island is cleared and the
    /// route is committed), so the pad never looks usable when it is not.
    /// </summary>
    public sealed class PadVisuals : MonoBehaviour
    {
        public SpringPad pad;
        public float ringSpin = 45f;
        public float pulseSpeed = 2.2f;

        Transform ring, column;
        Renderer ringRend, columnRend;
        Material ringMat, columnMat;

        static readonly Color ArmedColor = new Color(.35f, 1f, .62f);
        static readonly Color IdleColor = new Color(.28f, .42f, .38f);

        void Start()
        {
            if (pad == null) pad = GetComponent<SpringPad>();

            // Rotating ring hugging the pad edge
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "PadRing";
            var c = ringGo.GetComponent<Collider>();
            if (c != null) Destroy(c);
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, .02f, 0f);
            ringGo.transform.localScale = new Vector3(4.4f, .015f, 4.4f);
            ring = ringGo.transform;
            ringRend = ringGo.GetComponent<Renderer>();
            ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ringMat.color = IdleColor;
            ringRend.sharedMaterial = ringMat;

            // Light column so the pad is findable from a distance
            var colGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            colGo.name = "PadColumn";
            var c2 = colGo.GetComponent<Collider>();
            if (c2 != null) Destroy(c2);
            colGo.transform.SetParent(transform, false);
            colGo.transform.localPosition = new Vector3(0f, 3f, 0f);
            colGo.transform.localScale = new Vector3(2.4f, 3f, 2.4f);
            column = colGo.transform;
            columnRend = colGo.GetComponent<Renderer>();
            columnMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            columnMat.color = new Color(1f, 1f, 1f, .12f);
            columnRend.sharedMaterial = columnMat;
        }

        void Update()
        {
            if (ring == null) return;

            ring.Rotate(Vector3.up, ringSpin * Time.deltaTime, Space.Self);

            bool armed = IsArmed();
            float pulse = .5f + .5f * Mathf.Sin(Time.time * pulseSpeed);

            if (ringMat != null)
            {
                var target = armed ? ArmedColor : IdleColor;
                ringMat.color = Color.Lerp(target * .6f, target, armed ? pulse : .35f);
            }

            if (column != null)
            {
                // The column only exists when the pad is actually usable, so it doubles as a
                // "you can go now" signal rather than decoration.
                column.gameObject.SetActive(armed);
                if (armed)
                {
                    float s = 1f + pulse * .12f;
                    column.localScale = new Vector3(2.4f * s, 3f, 2.4f * s);
                }
            }
        }

        bool IsArmed()
        {
            var run = ExpeditionRun.Instance;
            if (run == null || pad == null) return false;
            if (run.Current == null || !run.Current.completed) return false;
            return run.CommittedNextId == pad.targetIslandId && pad.targetIslandId >= 0;
        }
    }
}
