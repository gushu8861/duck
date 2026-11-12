using System;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Makes the risk tier actually visible in the world.
    ///
    /// The design doc is explicit that an elite must not be guessable only from weather or
    /// colour: it needs its own silhouette and a marker. This component adds a floating ring
    /// and a taller scale for elites, and a distinct crown for the boss, so the promise made
    /// by the preview board matches what the player walks into.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class RiskVisuals : MonoBehaviour
    {
        /// <summary>Configure one spawned enemy to read as its risk tier.</summary>
        public static void Apply(GameObject enemy, RiskTier tier, EnemyElement element)
        {
            if (enemy == null) return;

            float scale = tier switch
            {
                RiskTier.Elite => 1.55f,
                RiskTier.Boss => 2.6f,
                _ => 1f,
            };
            enemy.transform.localScale *= scale;

            Color elementColor = element switch
            {
                EnemyElement.Ice => new Color(.45f, .82f, 1f),
                EnemyElement.Fire => new Color(1f, .42f, .22f),
                EnemyElement.Lightning => new Color(.72f, .52f, 1f),
                EnemyElement.Witchcraft => new Color(.58f, .38f, .72f),
                _ => Color.white,
            };

            // Ground disc: always present, colour = element. A flat disc survives fog and
            // distance better than a colour tint, which satisfies "colour alone is not enough".
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "RiskRing";
            var col = ring.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            ring.transform.SetParent(enemy.transform, false);
            ring.transform.localPosition = new Vector3(0f, -.45f / Mathf.Max(.01f, scale), 0f);
            ring.transform.localScale = new Vector3(1.6f, .02f, 1.6f) / Mathf.Max(.01f, scale);

            var rend = ring.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = elementColor;
                rend.sharedMaterial = mat;
            }

            if (tier == RiskTier.Boss)
            {
                // Crown so the boss is unmistakable from any angle.
                var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crown.name = "BossCrown";
                var cc = crown.GetComponent<Collider>();
                if (cc != null) UnityEngine.Object.Destroy(cc);
                crown.transform.SetParent(enemy.transform, false);
                crown.transform.localPosition = new Vector3(0f, 1.15f / Mathf.Max(.01f, scale), 0f);
                crown.transform.localScale = new Vector3(.55f, .18f, .55f) / Mathf.Max(.01f, scale);
                var cr = crown.GetComponent<Renderer>();
                if (cr != null)
                {
                    var cm = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    cm.color = new Color(1f, .85f, .2f);
                    cr.sharedMaterial = cm;
                }
            }
        }
    }
}
