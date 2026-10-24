using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    public enum EntryKind { FenceHop, CatFlap, Squeeze }

    /// <summary>A believable way into (and out of) the garden.</summary>
    public class Entrance
    {
        public EntryKind Kind;
        public Vector3 Outside;    // off-screen / hidden start point
        public Vector3 Threshold;  // fence-top perch, the cat flap, or the gap in the pickets
        public Vector3 Inside;     // first spot on the lawn
    }

    /// <summary>Somewhere interesting for a cat to go (flowers, pond edge, sunny porch, shade).</summary>
    public class Poi
    {
        public string Kind;        // Flower, Pond, Porch, Shade
        public Vector3 Pos, LookAt;
        public CatVisitor User;
    }

    /// <summary>
    /// Reads the garden layout by object name and gives the cats a sense of place:
    /// entrances, points of interest, obstacles to walk around and the porch height.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class GardenMap : MonoBehaviour
    {
        public static GardenMap I { get; private set; }

        public readonly List<Entrance> Entrances = new();
        public readonly List<Poi> Pois = new();
        public readonly List<Vector3> Flowers = new();
        readonly List<Vector3> obstacles = new();         // (x, radius, z)
        readonly List<Vector3> bushes = new();
        readonly List<Vector4> mats = new();             // (x, z, radius, top)

        // walkable lawn (x/z) the cats keep to when wandering
        public float MinX = -2.6f, MaxX = 2.6f, MinZ = -2.0f, MaxZ = 3.9f;
        const float FenceX = 3.25f, FenceTop = 0.86f;

        Rect porch = new(-2.75f, -3.18f, 5.5f, 1.0f), step = new(-0.75f, -2.21f, 1.5f, 0.3f);
        float porchY = 0.21f, stepY = 0.10f;

        Transform flap; Quaternion flapRest; Vector3 flapPos = new(0, 0.33f, -3.15f); float flapAngle, flapVel;

        void Awake()
        {
            I = this;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var n = r.name; var b = r.bounds; var c = b.center;
                if (n.StartsWith("Bush")) { var o = new Vector3(c.x, Mathf.Max(b.extents.x, b.extents.z) + 0.1f, c.z); obstacles.Add(o); bushes.Add(o); }
                else if (n.StartsWith("TreeATrunk") || n.StartsWith("TreeBTrunk")) obstacles.Add(new Vector3(c.x, 0.32f, c.z));
                else if (n == "Pot" || n.StartsWith("Pot.")) obstacles.Add(new Vector3(c.x, b.extents.x + 0.12f, c.z));
                else if (n.StartsWith("Flowers")) Flowers.Add(new Vector3(c.x, 0f, c.z));
                else if (n.StartsWith("Spot_")) mats.Add(new Vector4(c.x, c.z, Mathf.Max(b.extents.x, b.extents.z) - 0.03f, b.max.y));
                else if (n == "PondWater")
                {
                    // pond + stone ring as three overlapping circles
                    float ex = b.extents.x, ez = b.extents.z;
                    obstacles.Add(new Vector3(c.x, ez + 0.25f, c.z));
                    obstacles.Add(new Vector3(c.x - ex * 0.55f, ez + 0.15f, c.z));
                    obstacles.Add(new Vector3(c.x + ex * 0.55f, ez + 0.15f, c.z));
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.Lerp(-150f, 60f, i / 8f) * Mathf.Deg2Rad;      // the lawn-facing side of the pond
                        var p = new Vector3(c.x + Mathf.Cos(a) * (ex + 0.32f), 0f, c.z + Mathf.Sin(a) * (ez + 0.3f));
                        if (p.x < -2.95f || p.z > MaxZ + 0.3f) continue;
                        Pois.Add(new Poi { Kind = "Pond", Pos = p, LookAt = new Vector3(c.x, 0, c.z) });
                    }
                }
                else if (n == "Porch") { porch = new Rect(b.min.x, b.min.z, b.size.x, b.size.z); porchY = b.max.y; }
                else if (n == "Step") { step = new Rect(b.min.x, b.min.z, b.size.x, b.size.z); stepY = b.max.y; }
                else if (n == "CatFlap")
                {
                    // hinge pivot on the flap's top edge (mesh origins aren't reliable after FBX import)
                    var pivot = new GameObject("CatFlapHinge").transform;
                    pivot.position = new Vector3(c.x, b.max.y, c.z);
                    pivot.SetParent(r.transform.parent, true);
                    r.transform.SetParent(pivot, true);
                    flap = pivot; flapRest = pivot.localRotation; flapPos = c;
                }
            }
            foreach (var f in Flowers)
            {
                var toward = new Vector3(Mathf.Clamp(f.x, MinX + 0.4f, MaxX - 0.4f) * 0.6f, 0, Mathf.Clamp(f.z, MinZ, MaxZ) * 0.8f) - f;
                toward.y = 0; if (toward.sqrMagnitude < 0.01f) toward = Vector3.back;
                Pois.Add(new Poi { Kind = "Flower", Pos = Clamp(f + toward.normalized * 0.42f), LookAt = f });
            }
            // sunny porch boards & tree shade (nap spots)
            foreach (var x in new[] { -1.35f, 1.3f, 0.55f })
                Pois.Add(new Poi { Kind = "Porch", Pos = new Vector3(x, porchY, porch.center.y + 0.05f), LookAt = new Vector3(x, 0, 4f) });
            Pois.Add(new Poi { Kind = "Shade", Pos = new Vector3(1.75f, 0, -1.7f), LookAt = new Vector3(0, 0, 3f) });
            Pois.Add(new Poi { Kind = "Shade", Pos = new Vector3(-2.35f, 0, -1.85f), LookAt = new Vector3(0, 0, 3f) });

            BuildEntrances();
        }

        void BuildEntrances()
        {
            // hop over the side fences (the cat walks in from off-screen, perches on the fence, drops down)
            foreach (var (side, z) in new[] { (1f, 0.3f), (1f, 2.35f), (-1f, 0.05f), (-1f, 1.7f) })
                Entrances.Add(new Entrance { Kind = EntryKind.FenceHop, Outside = new Vector3(side * 4.6f, 0, z),
                    Threshold = new Vector3(side * FenceX, FenceTop, z), Inside = new Vector3(side * 2.75f, 0, z) });
            // squeeze through the pickets in the bush's shadow (camera-far side), then pad out from behind it
            foreach (var side in new[] { 1f, -1f })
            {
                Vector3 best = default; bool found = false;
                foreach (var bsh in bushes)
                    if (Mathf.Sign(bsh.x) == side && Mathf.Abs(bsh.x) > 2.4f && bsh.z > -1.6f && bsh.z < 1.6f && (!found || bsh.z < best.z)) { best = bsh; found = true; }
                if (!found) continue;
                float z = best.z - best.y - 0.12f;   // just behind the bush as seen from the camera
                Entrances.Add(new Entrance { Kind = EntryKind.Squeeze, Outside = new Vector3(side * 4.6f, 0, z),
                    Threshold = new Vector3(side * FenceX, 0, z), Inside = new Vector3(best.x - side * (best.y + 0.25f), 0, z + 0.1f) });
            }
            // through the cat flap in the cottage door
            Entrances.Add(new Entrance { Kind = EntryKind.CatFlap, Outside = new Vector3(flapPos.x, porchY, flapPos.z - 0.7f),
                Threshold = new Vector3(flapPos.x, porchY, flapPos.z), Inside = new Vector3(flapPos.x, porchY, porch.center.y + 0.15f) });
        }

        public Entrance RandomEntrance(Entrance avoid = null)
        {
            // weights: fence hops 0.4 total, bush squeeze 0.3, cat flap 0.3
            for (int tries = 0; tries < 6; tries++)
            {
                float r = Random.value;
                EntryKind k = r < 0.4f ? EntryKind.FenceHop : r < 0.7f ? EntryKind.Squeeze : EntryKind.CatFlap;
                var list = Entrances.FindAll(e => e.Kind == k);
                var e = list[Random.Range(0, list.Count)];
                if (e != avoid) return e;
            }
            return Entrances[Random.Range(0, Entrances.Count)];
        }

        public Entrance NearestEntrance(Vector3 p)
        {
            Entrance best = null; float bd = float.MaxValue;
            foreach (var e in Entrances) { float d = (Flat(e.Inside) - Flat(p)).sqrMagnitude; if (d < bd) { bd = d; best = e; } }
            return best;
        }

        // ------------------------------------------------------------------ ground
        public float GroundY(Vector3 p)
        {
            var q = new Vector2(p.x, p.z);
            if (porch.Contains(q)) return porchY;
            if (step.Contains(q)) return stepY;
            foreach (var m in mats)      // stand ON the woven mats, not sunk into them
                if ((q - new Vector2(m.x, m.y)).sqrMagnitude < m.z * m.z) return m.w;
            return 0f;
        }

        public bool OnPorch(Vector3 p) => porch.Contains(new Vector2(p.x, p.z));

        public Vector3 Clamp(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, MinX, MaxX); p.z = Mathf.Clamp(p.z, MinZ, MaxZ);
            return p;
        }

        public bool Blocked(Vector3 p, float margin = 0.12f)
        {
            foreach (var o in AllObstacles())
                if (new Vector2(p.x - o.x, p.z - o.z).sqrMagnitude < (o.y + margin) * (o.y + margin)) return true;
            return false;
        }

        public Vector3 RandomLawnPoint(Vector3 near, float radius)
        {
            for (int i = 0; i < 24; i++)
            {
                var c = Random.insideUnitCircle * radius;
                var p = Clamp(new Vector3(near.x + c.x, 0, near.z + c.y));
                if (!Blocked(p, 0.2f) && !OnPorch(p)) return p;
            }
            return new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-0.3f, 1.3f));
        }

        // ------------------------------------------------------------------ paths
        /// <summary>Waypoints from a to b that bend around bushes, trees, pots and the pond.</summary>
        public List<Vector3> Route(Vector3 a, Vector3 b)
        {
            var pts = new List<Vector3>();
            Detour(a, b, pts, 0);
            pts.Add(b);
            return pts;
        }

        void Detour(Vector3 a, Vector3 b, List<Vector3> pts, int depth)
        {
            if (depth > 4) return;
            var a2 = new Vector2(a.x, a.z); var b2 = new Vector2(b.x, b.z); var ab = b2 - a2;
            float len = ab.magnitude; if (len < 0.05f) return;
            Vector3 hit = default; float hitT = float.MaxValue; bool found = false;
            foreach (var o in AllObstacles())
            {
                var c = new Vector2(o.x, o.z); float r = o.y + 0.08f;
                if ((a2 - c).magnitude < r || (b2 - c).magnitude < r) continue;     // starting/ending inside: let it be
                float t = Mathf.Clamp01(Vector2.Dot(c - a2, ab) / (len * len));
                if ((a2 + ab * t - c).magnitude < r && t < hitT) { hitT = t; hit = o; found = true; }
            }
            if (!found) return;
            var cc = new Vector2(hit.x, hit.z); var closest = a2 + ab * hitT;
            var away = closest - cc;
            if (away.sqrMagnitude < 1e-4f) away = new Vector2(-ab.y, ab.x);
            var w2 = cc + away.normalized * (hit.y + 0.3f);
            var w = new Vector3(w2.x, 0, w2.y);
            Detour(a, w, pts, depth + 1);
            pts.Add(w);
            Detour(w, b, pts, depth + 1);
        }

        /// <summary>Static scenery plus whatever items are currently placed on the mats (x, radius, z).</summary>
        List<Vector3> AllObstacles()
        {
            var list = new List<Vector3>(obstacles);
            if (GameManager.I)
                foreach (var sp in GameManager.I.Spots)
                {
                    if (!sp || string.IsNullOrEmpty(sp.ItemId)) continue;
                    // footprint radii include the box's open flaps and the cushion's puffy edge
                    float r = sp.ItemId switch { "Box" => 0.72f, "Cushion" => 0.46f, "Bowl" => 0.3f, "Yarn" => 0.2f, _ => 0f };
                    if (r <= 0f) continue;
                    var p = sp.ItemId == "Yarn" ? sp.LivePosition : sp.ItemPosition;
                    list.Add(new Vector3(p.x, r, p.z));
                }
            return list;
        }

        /// <summary>Push a body circle (centre p, radius r) out of every obstacle it overlaps.</summary>
        public Vector3 PushOut(Vector3 p, float r)
        {
            foreach (var o in AllObstacles())
            {
                var d = new Vector2(p.x - o.x, p.z - o.z); float need = o.y + r, m = d.magnitude;
                if (m >= need) continue;
                if (m < 1e-4f) d = Vector2.right; else d /= m;
                p.x = o.x + d.x * need; p.z = o.z + d.y * need;
            }
            return p;
        }

        public bool BlockedStatic(Vector3 p, float margin = 0f)
        {
            foreach (var o in obstacles)
                if (new Vector2(p.x - o.x, p.z - o.z).sqrMagnitude < (o.y + margin) * (o.y + margin)) return true;
            return false;
        }

        public Poi FreePoi(string kind, Vector3 near)
        {
            Poi best = null; float bd = float.MaxValue;
            foreach (var p in Pois)
            {
                if (p.User != null || (kind != null && p.Kind != kind)) continue;
                float d = (Flat(p.Pos) - Flat(near)).magnitude + Random.Range(0f, 1.5f);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        // ------------------------------------------------------------------ cat flap
        public void PushFlap(float dir) { flapVel += dir * 420f; }

        void Update()
        {
            if (!flap) return;
            // damped spring back to rest
            flapVel += (-flapAngle * 90f - flapVel * 6f) * Time.deltaTime;
            flapAngle = Mathf.Clamp(flapAngle + flapVel * Time.deltaTime, -75f, 75f);
            flap.localRotation = flapRest * Quaternion.AngleAxis(flapAngle, Vector3.right);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
