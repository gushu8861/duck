using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Impact feedback layer: damage numbers, hit flashes, hit-stop and screen shake.
    ///
    /// This is what separates a prototype from a game you can feel. Every hit produces:
    ///   - a floating damage number that rises and fades
    ///   - a brief white flash on the enemy
    ///   - a small camera kick
    ///   - a 1-2 frame hit-stop on kills only (not every hit, or combat turns to mud)
    ///
    /// Numbers are drawn through a WorldSpace canvas so they stay crisp and scale with the
    /// display the same way the HUD does.
    /// </summary>
    public sealed class CombatFeedback : MonoBehaviour
    {
        public static CombatFeedback Instance { get; private set; }

        [Header("Damage numbers")]
        public int poolSize = 24;
        public float riseSpeed = 1.6f;
        public float lifetime = .9f;
        public float worldScale = .011f;

        [Header("Shake")]
        public float shakeDecay = 6.5f;
        public float maxShake = .22f;

        Transform camTransform;
        Vector3 camBasePos;
        float shakeAmp;

        readonly List<FloatingNumber> numbers = new List<FloatingNumber>();
        Font font;

        sealed class FloatingNumber
        {
            public GameObject go;
            public TextMesh text;
            public float age;
            public Vector3 origin;
            public bool active;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            var cam = Camera.main;
            if (cam != null) camTransform = cam.transform;

            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "微软雅黑", "Arial" }, 64);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("dmg_" + i);
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 72;
                tm.characterSize = worldScale;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontStyle = FontStyle.Bold;

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }

                numbers.Add(new FloatingNumber { go = go, text = tm });
            }
        }

        /// <summary>Spawn a damage number at a world position. Colour encodes severity.</summary>
        public void DamageNumber(Vector3 worldPos, float amount, bool killing)
        {
            var n = numbers.Find(x => !x.active);
            if (n == null) return;

            n.active = true;
            n.age = 0f;
            n.origin = worldPos + Vector3.up * 1.1f + UnityEngine.Random.insideUnitSphere * .18f;
            n.go.transform.position = n.origin;
            n.go.SetActive(true);

            n.text.text = Mathf.RoundToInt(amount).ToString();
            n.text.color = killing ? new Color(1f, .82f, .22f)
                         : amount >= 25f ? new Color(1f, .55f, .35f)
                         : Color.white;

            if (camTransform != null)
            {
                var dir = n.go.transform.position - camTransform.position;
                if (dir.sqrMagnitude > .001f)
                    n.go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        /// <summary>A bright pulse on the enemy body so the hit registers physically.</summary>
        public static void HitFlash(Renderer rend, float duration = .09f)
        {
            if (rend == null) return;
            var host = rend.gameObject.GetComponent<HitFlashHost>();
            if (host == null) host = rend.gameObject.AddComponent<HitFlashHost>();
            host.Flash(rend, duration);
        }

        public void Shake(float amplitude)
        {
            shakeAmp = Mathf.Min(maxShake, Mathf.Max(shakeAmp, amplitude));
        }

        /// <summary>
        /// Brief time freeze on a kill. Kept very short: enough to feel weight, not enough to
        /// interrupt aiming.
        /// </summary>
        public static IEnumerator HitStop(float seconds)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = prev;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            // --- update floating numbers ---
            foreach (var n in numbers)
            {
                if (!n.active) continue;
                n.age += dt;
                if (n.age >= lifetime)
                {
                    n.active = false;
                    n.go.SetActive(false);
                    continue;
                }

                float k = n.age / lifetime;
                // rise fast then slow, and drift slightly sideways
                float rise = Mathf.Sqrt(k) * riseSpeed * lifetime;
                n.go.transform.position = n.origin + Vector3.up * rise;

                var c = n.text.color;
                c.a = k < .65f ? 1f : Mathf.Lerp(1f, 0f, (k - .65f) / .35f);
                n.text.color = c;

                // pop in: start big, settle
                float s = k < .18f ? Mathf.Lerp(1.45f, 1f, k / .18f) : 1f;
                n.go.transform.localScale = Vector3.one * s;

                if (camTransform != null)
                {
                    var dir = n.go.transform.position - camTransform.position;
                    if (dir.sqrMagnitude > .001f)
                        n.go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }

            // --- camera shake ---
            if (shakeAmp > 0.0001f && camTransform != null)
            {
                if (camBasePos == Vector3.zero) camBasePos = camTransform.localPosition;
                shakeAmp = Mathf.Lerp(shakeAmp, 0f, dt * shakeDecay);
                float t = Time.time * 42f;
                var offset = new Vector3(
                    (Mathf.PerlinNoise(t, 0f) - .5f) * 2f,
                    (Mathf.PerlinNoise(0f, t) - .5f) * 2f,
                    0f) * shakeAmp;
                camTransform.localPosition += offset * .5f;
            }
        }
    }

    /// <summary>Drives a short emissive pulse on a renderer without needing a custom shader.</summary>
    public sealed class HitFlashHost : MonoBehaviour
    {
        Renderer target;
        MaterialPropertyBlock block;
        Color baseColor;
        float remaining;
        float duration;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Flash(Renderer rend, float dur)
        {
            target = rend;
            duration = Mathf.Max(.02f, dur);
            remaining = duration;
            block = new MaterialPropertyBlock();
            baseColor = rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId)
                ? rend.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
        }

        void Update()
        {
            if (target == null || remaining <= 0f) return;
            remaining -= Time.deltaTime;
            float k = Mathf.Clamp01(remaining / duration);

            block.Clear();
            var hot = Color.Lerp(baseColor, Color.white, k);
            if (target.sharedMaterial != null && target.sharedMaterial.HasProperty(BaseColorId))
                block.SetColor(BaseColorId, hot);
            else
                block.SetColor(ColorId, hot);
            target.SetPropertyBlock(block);

            if (remaining <= 0f) target.SetPropertyBlock(null);
        }
    }
}
