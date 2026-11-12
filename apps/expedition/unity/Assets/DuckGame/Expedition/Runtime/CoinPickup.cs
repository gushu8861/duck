using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;   // Economy lives in the TS namespace

namespace DuckGame.Expedition
{
    /// <summary>
    /// A single collectible coin. Coins pop out on a kill, arc to the ground and are pulled
    /// toward the player once they are close, which keeps collection frictionless.
    /// </summary>
    public sealed class CoinPickup : MonoBehaviour
    {
        public int value = 1;
        public Transform magnetTarget;
        public float magnetRadius = 4.5f;
        public float magnetSpeed = 12f;

        Vector3 velocity;
        bool grounded;
        float life = 18f;

        public static CoinPickup Spawn(Vector3 origin, int amount, Transform target, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Coin";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.localScale = Vector3.one * .28f;
            go.transform.SetParent(parent);
            go.transform.position = origin + Vector3.up * .4f;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(1f, .82f, .25f);
                mat.SetFloat("_Smoothness", .8f);
                rend.sharedMaterial = mat;
            }

            var c = go.AddComponent<CoinPickup>();
            c.value = amount;
            c.magnetTarget = target;
            c.velocity = new Vector3(UnityEngine.Random.Range(-2.2f, 2.2f), UnityEngine.Random.Range(4f, 6f),
                                     UnityEngine.Random.Range(-2.2f, 2.2f));
            return c;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            if (magnetTarget != null)
            {
                float d = Vector3.Distance(transform.position, magnetTarget.position);
                if (d < magnetRadius)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        magnetTarget.position + Vector3.up * .8f,
                        magnetSpeed * Time.deltaTime);
                    if (d < .5f) Collect();
                    return;
                }
            }

            if (!grounded)
            {
                velocity += Vector3.down * 18f * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

                if (transform.position.y < .15f)
                {
                    transform.position = new Vector3(transform.position.x, .15f, transform.position.z);
                    grounded = true;
                }
            }
            else
            {
                // gentle idle spin so coins read as collectible
                transform.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
            }
        }

        void Collect()
        {
            if (Economy.Instance != null) Economy.Instance.GrantMeta(value);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Coin drop helper. Attached to ExpeditionSystems so kills have somewhere to spawn coins.
    /// Self-bootstrapping: if no instance exists when a drop is requested, one is created, so
    /// the drop path never silently fails just because the component was not pre-placed.
    /// </summary>
    public sealed class CoinSpawner : MonoBehaviour
    {
        static CoinSpawner instance;

        public static CoinSpawner Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindObjectOfType<CoinSpawner>();
                if (instance == null)
                {
                    var host = new GameObject("CoinSpawner");
                    instance = host.AddComponent<CoinSpawner>();
                }
                return instance;
            }
        }

        public Transform coinParent;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            if (coinParent == null)
            {
                var go = new GameObject("Coins");
                go.transform.SetParent(transform);
                coinParent = go.transform;
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void Drop(Vector3 at, int amount, Transform target)
        {
            if (coinParent == null)
            {
                var go = new GameObject("Coins");
                go.transform.SetParent(transform);
                coinParent = go.transform;
            }

            int coins = Mathf.Clamp(amount, 1, 8);
            int per = Mathf.Max(1, amount / coins);
            for (int i = 0; i < coins; i++)
                CoinPickup.Spawn(at, per, target, coinParent);
        }
    }
}
