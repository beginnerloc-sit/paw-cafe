using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    /// <summary>A few butterflies that drift between the flower beds — and tempt the playful cats.</summary>
    public class Butterflies : MonoBehaviour
    {
        public static Butterflies I { get; private set; }
        public Material Mat;
        public int Count = 3;
        public readonly List<Butterfly> All = new();

        static readonly string[][] Palettes =
        {
            new[] { "#ff9ec0", "#ffd3e2", "#6b4a5a" },
            new[] { "#ffc94a", "#fff0b8", "#6b5236" },
            new[] { "#b9a2f2", "#e6dcff", "#4c4270" },
            new[] { "#8fd3f0", "#e3f6ff", "#3e5c70" },
        };

        void Awake() { I = this; }

        void Start()
        {
            if (!GardenMap.I || GardenMap.I.Flowers.Count == 0) return;
            for (int i = 0; i < Count; i++)
            {
                var pal = Palettes[i % Palettes.Length];
                var b = Make(pal).AddComponent<Butterfly>();
                b.Init(this, GardenMap.I.Flowers[Random.Range(0, GardenMap.I.Flowers.Count)] + Vector3.up * Random.Range(0.4f, 1f));
                All.Add(b);
            }
        }

        GameObject Make(string[] pal)
        {
            var root = new GameObject("Butterfly");
            root.transform.SetParent(transform, false);
            var a = MeshKit.Hex(pal[0]); var edge = MeshKit.Hex(pal[1]); var body = MeshKit.Hex(pal[2]);
            for (int s = -1; s <= 1; s += 2)
            {
                var pivot = new GameObject(s < 0 ? "WingL" : "WingR").transform; pivot.SetParent(root.transform, false);
                var k = new MeshKit();
                // fore wing + hind wing as fans from the hinge (local +x = outward, +z = forward)
                var rim = new List<Vector3>(); var rc = new List<Color>();
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f; float ang = Mathf.Lerp(-10f, 95f, t) * Mathf.Deg2Rad;
                    float r = 0.06f + 0.025f * Mathf.Sin(t * Mathf.PI);
                    rim.Add(new Vector3(Mathf.Sin(ang) * r * s, 0, Mathf.Cos(ang) * r * 0.75f + 0.005f)); rc.Add(Color.Lerp(a, edge, 0.4f + 0.6f * Mathf.Sin(t * Mathf.PI)));
                }
                k.Fan(Vector3.zero, Color.Lerp(a, body, 0.35f), rim, rc);
                rim.Clear(); rc.Clear();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f; float ang = Mathf.Lerp(95f, 175f, t) * Mathf.Deg2Rad;
                    float r = 0.045f + 0.012f * Mathf.Sin(t * Mathf.PI);
                    rim.Add(new Vector3(Mathf.Sin(ang) * r * s, 0, Mathf.Cos(ang) * r)); rc.Add(Color.Lerp(a, edge, 0.3f * Mathf.Sin(t * Mathf.PI)));
                }
                k.Fan(Vector3.zero, Color.Lerp(a, body, 0.35f), rim, rc);
                MeshKit.Obj("Wing", k.Build("Wing"), Mat, pivot);
            }
            var bk = new MeshKit();
            bk.Spindle(new Vector3(0, 0, -0.035f), new Vector3(0, 0, 0.03f), 0.007f, body, body, 6, 0.7f);
            MeshKit.Obj("Body", bk.Build("ButterflyBody"), Mat, root.transform);
            return root;
        }

        public Butterfly Nearest(Vector3 p, float max)
        {
            Butterfly best = null; float bd = max;
            foreach (var b in All)
            {
                if (!b || !b.Catchable) continue;
                float d = Vector3.Distance(b.Ground, new Vector3(p.x, 0, p.z));
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public Vector3 RandomFlower(Vector3 avoid)
        {
            var f = GardenMap.I.Flowers;
            for (int i = 0; i < 6; i++) { var c = f[Random.Range(0, f.Count)]; if ((c - avoid).sqrMagnitude > 1f) return c; }
            return f[Random.Range(0, f.Count)];
        }
    }

    public class Butterfly : MonoBehaviour
    {
        Butterflies owner;
        Transform wl, wr;
        Vector3 vel, flower, target;
        float landedUntil, flee, phase, seed;
        bool landed;

        public Vector3 Ground { get { var p = transform.position; return new Vector3(p.x, 0, p.z); } }
        public bool Catchable => flee <= 0f && transform.position.y < 0.75f;

        public void Init(Butterflies o, Vector3 start)
        {
            owner = o; transform.position = start; seed = Random.value * 100f;
            wl = transform.Find("WingL"); wr = transform.Find("WingR");
            PickFlower();
        }

        void PickFlower()
        {
            flower = owner.RandomFlower(transform.position);
            target = flower + Vector3.up * 0.24f;
            landed = false;
        }

        public void Flee()
        {
            flee = 2.5f; landed = false;
            flower = owner.RandomFlower(transform.position);
            target = transform.position + Vector3.up * 1.3f + Random.insideUnitSphere * 0.6f;
        }

        void Update()
        {
            float dt = Time.deltaTime; float t = Time.time + seed;
            float flapAngle;
            if (landed)
            {
                flapAngle = 25f + 30f * Mathf.Sin(t * 3f);
                vel = Vector3.zero;
                if (Time.time > landedUntil) PickFlower();
            }
            else
            {
                if (flee > 0f) { flee -= dt; if (flee <= 0f) target = flower + Vector3.up * 0.24f; }
                var wobble = new Vector3(Mathf.Sin(t * 2.3f), Mathf.Sin(t * 3.7f) * 0.6f, Mathf.Cos(t * 1.9f)) * 0.25f;
                var to = target + wobble * Mathf.Clamp01((target - transform.position).magnitude) - transform.position;
                float maxSpeed = flee > 0f ? 2.2f : 0.8f;
                vel = Vector3.ClampMagnitude(vel + to * (2.2f * dt) - vel * (0.9f * dt), maxSpeed);
                transform.position += vel * dt;
                flapAngle = Mathf.Sin(t * 26f) * 55f + 10f;
                // flutter dips
                transform.position += Vector3.up * Mathf.Sin(t * 13f) * 0.12f * dt;
                if (flee <= 0f && (transform.position - target).magnitude < 0.07f) { landed = true; landedUntil = Time.time + Random.Range(2.5f, 6f); }
                var fv = vel; fv.y = 0;
                if (fv.sqrMagnitude > 0.002f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fv), dt * 6f);
            }
            if (wl) wl.localRotation = Quaternion.Euler(0, 0, -flapAngle);
            if (wr) wr.localRotation = Quaternion.Euler(0, 0, flapAngle);
        }
    }
}
