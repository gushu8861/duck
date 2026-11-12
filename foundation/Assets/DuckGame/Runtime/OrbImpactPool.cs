using UnityEngine;

namespace DuckGame
{
    // Bounded scene-owned pool; each hit reuses one flash and six debris meshes.
    public sealed class OrbImpactPool : MonoBehaviour
    {
        public const int Capacity = 16;
        public static OrbImpactPool Current { get; private set; }
        public int ActiveCount { get; private set; }
        public int PeakActive { get; private set; }
        public int EnvironmentHits { get; private set; }
        public int EnemyHits { get; private set; }
        public int RecycledWhileActive { get; private set; }
        sealed class Slot
        {
            public GameObject root;
            public Transform[] pieces = new Transform[7];
            public Vector3 point, normal;
            public float age, duration, size;
            public bool active, enemy;
        }
        readonly Slot[] slots = new Slot[Capacity];
        MaterialPropertyBlock colors;
        Material material;
        int cursor;
        public static void Warmup()
        {
            if (Current == null) Current = new GameObject("OrbImpactPool").AddComponent<OrbImpactPool>();
        }
        public static void Emit(Vector3 point, Vector3 normal, Color color, bool enemy, float scale = 1)
        {
            Warmup();
            Current.Play(point, normal, color, enemy, scale);
        }
        void Awake()
        {
            Current = this;
            colors = new MaterialPropertyBlock();
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            for (int i = 0; i < Capacity; i++)
            {
                var slot = new Slot { root = new GameObject("ImpactSlot" + i) };
                slot.root.transform.SetParent(transform, false);
                for (int j = 0; j < 7; j++)
                {
                    var piece = GameObject.CreatePrimitive(j == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                    piece.name = j == 0 ? "ContactFlash" : "Debris";
                    piece.layer = 2;
                    var collider = piece.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                    piece.transform.SetParent(slot.root.transform, false);
                    piece.GetComponent<Renderer>().sharedMaterial = material;
                    slot.pieces[j] = piece.transform;
                }
                slot.root.SetActive(false); slots[i] = slot;
            }
        }
        void Play(Vector3 point, Vector3 normal, Color color, bool enemy, float scale)
        {
            Slot slot = slots[cursor]; cursor = (cursor + 1) % Capacity;
            if (slot.active) RecycledWhileActive++;
            slot.active = true; slot.enemy = enemy; slot.age = 0;
            slot.point = point; slot.normal = normal.sqrMagnitude > .001f ? normal.normalized : Vector3.up;
            slot.duration = enemy ? .28f : .17f; slot.size = Mathf.Clamp(scale, .5f, 2);
            slot.root.SetActive(true);
            slot.root.transform.SetPositionAndRotation(point + slot.normal * .025f, Quaternion.LookRotation(slot.normal));
            colors.SetColor("_BaseColor", color); colors.SetColor("_Color", color);
            foreach (Transform piece in slot.pieces) piece.GetComponent<Renderer>().SetPropertyBlock(colors);
            for (int j = 1; j < 7; j++) slot.pieces[j].gameObject.SetActive(enemy || j <= 3);
            if (enemy) EnemyHits++; else EnvironmentHits++;
            Pose(slot, 0); Count();
        }
        void Update()
        {
            foreach (Slot slot in slots)
            {
                if (!slot.active) continue;
                slot.age += Time.deltaTime;
                if (slot.age >= slot.duration) { slot.active = false; slot.root.SetActive(false); }
                else Pose(slot, slot.age / slot.duration);
            }
            Count();
        }
        void Count()
        {
            ActiveCount = 0;
            foreach (Slot slot in slots) if (slot.active) ActiveCount++;
            PeakActive = Mathf.Max(PeakActive, ActiveCount);
        }
        static void Pose(Slot slot, float t)
        {
            float fade = 1 - t;
            float burst = slot.enemy ? .42f : .24f;
            slot.pieces[0].localPosition = Vector3.zero;
            slot.pieces[0].localScale = new Vector3(burst, burst, .045f) * slot.size * (1 + .7f * t) * fade;
            for (int j = 1; j < 7; j++)
            {
                float angle = (j - 1) * Mathf.PI * 2 / (slot.enemy ? 6 : 3);
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                Transform piece = slot.pieces[j];
                piece.localPosition = radial * (burst * .25f + t * burst * 1.8f) + Vector3.forward * (.05f + t * .22f);
                piece.localRotation = Quaternion.Euler(25 + t * 90, j * 47, angle * Mathf.Rad2Deg);
                piece.localScale = new Vector3(.065f, slot.enemy ? .18f : .09f, .055f) * slot.size * fade;
            }
        }
        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (material != null) Destroy(material);
        }
    }
}
