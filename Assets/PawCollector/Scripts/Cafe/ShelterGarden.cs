using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>
    /// Dresses the shelter play yard so it feels like a cosy garden instead of an empty green disc: rolling hills, round and
    /// blossom trees, a little pink-roofed shelter cottage with a paw sign, bushes, flower beds, stepping stones, clouds.
    /// Everything is one vertex-coloured mesh (same toon material as the fence), built once at runtime.
    /// Coordinates are the stage's local space: the yard fence is a ring of radius 4.65 around (0, 0, 4.2), the camera
    /// sits at (0, 5.2, -8.6) looking towards +Z, the chosen cat walks to (0, 0, -1.6).
    /// </summary>
    public static class ShelterGarden
    {
        // palette (vertex alpha must stay 1 — alpha 0 means "glow" in the toon shader)
        static readonly Color Leaf1 = new(0.42f, 0.74f, 0.40f), Leaf2 = new(0.52f, 0.82f, 0.45f), Leaf3 = new(0.34f, 0.64f, 0.36f);
        static readonly Color Blossom1 = new(1f, 0.72f, 0.82f), Blossom2 = new(1f, 0.82f, 0.88f), Trunk = new(0.58f, 0.40f, 0.28f);
        static readonly Color Hill1 = new(0.56f, 0.82f, 0.50f), Hill2 = new(0.47f, 0.76f, 0.46f), Hill3 = new(0.62f, 0.86f, 0.56f);
        static readonly Color Wall = new(1f, 0.95f, 0.88f), Roof = new(0.95f, 0.50f, 0.60f), Wood = new(0.72f, 0.50f, 0.36f), Window = new(0.70f, 0.86f, 1f);
        static readonly Color Stone = new(0.90f, 0.86f, 0.80f), Stone2 = new(0.84f, 0.80f, 0.74f), Cloud = new(1f, 1f, 1f);
        static readonly Color[] Petals = { new(1f, 0.55f, 0.70f), new(1f, 0.85f, 0.35f), new(0.75f, 0.62f, 1f), new(1f, 1f, 1f), new(0.55f, 0.80f, 1f), new(1f, 0.62f, 0.45f) };

        static List<Vector3> v; static List<Color> c; static List<int> t; static System.Random rnd;
        static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        public static GameObject Build(Transform parent, Material mat)
        {
            v = new(); c = new(); t = new(); rnd = new System.Random(11);
            Hills(); Patches(); Trees(); Cottage(new Vector3(0f, 0, 12.4f), 1.2f); Bushes(); Flowers(); Stones(); Clouds();
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var g = new GameObject("Garden"); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            v = null; c = null; t = null;
            return g;
        }

        // ------------------------------------------------------------------ scenery
        static void Hills()
        {
            // a soft horizon of overlapping mounds behind everything (hides the edge of the grass disc)
            for (int i = 0; i < 9; i++)
            {
                float x = -16f + i * 4f + R(-1f, 1f), z = R(12.5f, 14.5f);
                Ellipsoid(new Vector3(x, -1.2f, z), new Vector3(R(4.5f, 6.5f), R(2.4f, 3.6f), 3f), i % 3 == 0 ? Hill1 : i % 3 == 1 ? Hill2 : Hill3, 16, 10);
            }
            for (int i = 0; i < 5; i++) Ellipsoid(new Vector3(-9f + i * 4.5f, -0.9f, R(10.5f, 11.5f)), new Vector3(R(3.5f, 4.5f), R(1.4f, 2f), 2.5f), i % 2 == 0 ? Hill3 : Hill1, 14, 8);
        }

        static void Trees()
        {
            (float x, float z, bool blossom, float s)[] spots =
            {
                (-6.2f, 4.4f, false, 1.0f), (-8.6f, 6.6f, true, 1.15f), (-5.6f, 8.8f, true, 0.95f), (-10.6f, 3.0f, false, 1.2f),
                ( 6.3f, 4.0f, true, 1.0f), ( 8.4f, 7.0f, false, 1.2f), ( 5.4f, 9.2f, false, 0.9f), (10.8f, 3.4f, true, 1.1f),
                (-4.4f, 11.4f, false, 1.05f), (4.6f, 11.6f, true, 1.0f), (-12.5f, 7.5f, false, 1.3f), (12.4f, 8.2f, false, 1.25f),
                (-9.5f, -1.8f, true, 1.0f), (9.8f, -1.2f, false, 1.1f),
            };
            foreach (var (x, z, blossom, s) in spots) Tree(new Vector3(x, 0, z), s, blossom);
        }
        static void Tree(Vector3 p, float s, bool blossom)
        {
            Cylinder(p, 0.16f * s, 1.3f * s, Trunk, 8);
            Color a = blossom ? Blossom1 : Leaf1, b = blossom ? Blossom2 : Leaf2, d = blossom ? new Color(1f, 0.64f, 0.76f) : Leaf3;
            var top = p + Vector3.up * 1.55f * s;
            Ellipsoid(top, Vector3.one * 0.95f * s, a, 14, 10);
            Ellipsoid(top + new Vector3(-0.62f, -0.25f, 0.1f) * s, Vector3.one * 0.66f * s, b, 12, 8);
            Ellipsoid(top + new Vector3(0.6f, -0.2f, -0.15f) * s, Vector3.one * 0.7f * s, d, 12, 8);
            Ellipsoid(top + new Vector3(0.05f, 0.62f, 0f) * s, Vector3.one * 0.6f * s, b, 12, 8);
            if (blossom) for (int i = 0; i < 4; i++) Ellipsoid(p + new Vector3(R(-0.9f, 0.9f), 0.03f, R(-0.9f, 0.9f)) * s, new Vector3(0.14f, 0.02f, 0.14f), Blossom2, 8, 3);   // fallen petals
        }

        static void Cottage(Vector3 at, float k)
        {
            int first = v.Count; var p = Vector3.zero;   // built at the origin, then scaled and moved into place
            // little shelter house: cream walls, pink roof, door, windows, chimney and a paw sign by the door (faces the camera, −Z)
            Box(p + new Vector3(0, 0.95f, 0), new Vector3(3.2f, 1.9f, 2.4f), Wall);
            Prism(p + new Vector3(0, 1.9f, 0), 3.6f, 2.8f, 1.25f, Roof);
            Box(p + new Vector3(1.0f, 2.6f, 0.4f), new Vector3(0.36f, 0.9f, 0.36f), Wood);                  // chimney
            Box(p + new Vector3(0, 0.62f, -1.21f), new Vector3(0.78f, 1.24f, 0.06f), Wood);                 // door
            Ellipsoid(p + new Vector3(0.25f, 0.62f, -1.26f), Vector3.one * 0.05f, new Color(1f, 0.85f, 0.35f), 6, 4);   // knob
            foreach (var wx in new[] { -1.05f, 1.05f })
            {
                Box(p + new Vector3(wx, 1.15f, -1.21f), new Vector3(0.72f, 0.62f, 0.05f), Wood);
                Box(p + new Vector3(wx, 1.15f, -1.24f), new Vector3(0.58f, 0.48f, 0.04f), Window);
                Box(p + new Vector3(wx, 0.80f, -1.30f), new Vector3(0.8f, 0.12f, 0.2f), new Color(0.55f, 0.80f, 0.45f));   // flower box
                for (int i = 0; i < 4; i++) Flower(p + new Vector3(wx - 0.27f + i * 0.18f, 0.9f, -1.32f), 0.07f, Petals[i % Petals.Length]);
            }
            Box(p + new Vector3(0, 1.62f, -1.22f), new Vector3(0.0f, 0f, 0f), Wall);
            // paw sign on a post
            var s = p + new Vector3(-2.2f, 0, -1.6f);
            Box(s + new Vector3(0, 0.55f, 0), new Vector3(0.1f, 1.1f, 0.1f), Wood);
            Box(s + new Vector3(0, 1.2f, 0), new Vector3(0.9f, 0.62f, 0.08f), Wall);
            Paw(s + new Vector3(0, 1.17f, -0.06f), 0.22f, Roof);
            Box(p + new Vector3(0, 0.03f, -1.6f), new Vector3(1.1f, 0.06f, 0.7f), Stone);                  // doorstep
            for (int i = first; i < v.Count; i++) v[i] = at + v[i] * k;
        }
        static void Paw(Vector3 at, float s, Color col)
        {
            Ellipsoid(at + new Vector3(0, -0.08f, 0) * s * 4, new Vector3(0.11f, 0.09f, 0.03f) * s * 4, col, 10, 6);
            foreach (var (dx, dy) in new[] { (-0.11f, 0.05f), (-0.04f, 0.1f), (0.04f, 0.1f), (0.11f, 0.05f) })
                Ellipsoid(at + new Vector3(dx, dy, 0) * s * 4, new Vector3(0.04f, 0.045f, 0.03f) * s * 4, col, 8, 5);
        }

        static void Patches()
        {
            // soft darker/lighter patches so the grass isn't one flat green
            for (int i = 0; i < 26; i++)
            {
                var p = new Vector3(R(-12f, 12f), 0.012f, R(-6f, 11f));
                if (new Vector2(p.x, p.z + 1.6f).magnitude < 3.2f) continue;   // keep the welcome spot clean
                bool dark = i % 2 == 0;
                Ellipsoid(p, new Vector3(R(0.9f, 2.0f), 0.006f, R(0.7f, 1.5f)), dark ? new Color(0.50f, 0.78f, 0.44f) : new Color(0.66f, 0.89f, 0.56f), 14, 2);
            }
        }

        static void Bushes()
        {
            (float x, float z)[] spots = { (-5.1f, 1.6f), (5.2f, 1.4f), (-4.9f, 6.9f), (4.8f, 7.2f), (-2.6f, 9.8f), (2.8f, 9.9f), (-7.6f, 1.0f), (7.8f, 1.6f), (-9.5f, 9.4f), (9.4f, 9.6f),
                                           (-3.4f, -3.0f), (3.6f, -3.2f), (-6.2f, -2.4f), (6.4f, -2.2f), (-2.2f, -4.6f), (2.5f, -4.8f) };   // front corners frame the welcome spot
            foreach (var (x, z) in spots)
            {
                var p = new Vector3(x, 0, z); float s = R(0.8f, 1.15f);
                Ellipsoid(p + Vector3.up * 0.32f * s, new Vector3(0.62f, 0.45f, 0.55f) * s, Leaf2, 12, 8);
                Ellipsoid(p + new Vector3(-0.42f, 0.24f, 0.05f) * s, new Vector3(0.4f, 0.33f, 0.38f) * s, Leaf1, 10, 6);
                Ellipsoid(p + new Vector3(0.45f, 0.22f, -0.05f) * s, new Vector3(0.38f, 0.3f, 0.36f) * s, Leaf3, 10, 6);
                for (int i = 0; i < 5; i++)
                {
                    float a = R(0, Mathf.PI * 2); var q = p + new Vector3(Mathf.Cos(a) * 0.45f * s, R(0.35f, 0.7f) * s, Mathf.Sin(a) * 0.4f * s - 0.15f);
                    Flower(q, 0.07f, Petals[rnd.Next(Petals.Length)]);
                }
            }
        }

        static void Flowers()
        {
            // flower beds along the outside of the fence and around the welcome circle
            for (int i = 0; i < 70; i++)
            {
                float a = R(-110f, 110f) * Mathf.Deg2Rad, r = R(5.0f, 6.2f);
                var p = new Vector3(Mathf.Sin(a) * r, 0, 4.2f + Mathf.Cos(a) * r); if (p.z < -0.5f) continue;
                Flower(p + Vector3.up * R(0.08f, 0.2f), R(0.07f, 0.1f), Petals[rnd.Next(Petals.Length)], true);
            }
            for (int i = 0; i < 26; i++)
            {
                float a = R(0, Mathf.PI * 2), r = R(1.75f, 2.2f);
                var p = new Vector3(Mathf.Cos(a) * r, 0, -1.6f + Mathf.Sin(a) * r); if (p.z > 0.2f) continue;   // keep the yard entrance clear
                Flower(p + Vector3.up * R(0.06f, 0.14f), R(0.06f, 0.08f), Petals[rnd.Next(Petals.Length)], true);
            }
            // scattered meadow flowers on the open grass at the sides and front
            for (int i = 0; i < 90; i++)
            {
                var p = new Vector3(R(-12f, 12f), 0, R(-6f, 11f));
                if (new Vector2(p.x, p.z - 4.2f).magnitude < 5f || new Vector2(p.x, p.z + 1.6f).magnitude < 2.4f || (Mathf.Abs(p.x) < 1.2f && p.z < -1.6f)) continue;
                Flower(p + Vector3.up * 0.06f, R(0.05f, 0.08f), Petals[rnd.Next(Petals.Length)], true);
            }
            // grass tufts
            for (int i = 0; i < 60; i++)
            {
                var p = new Vector3(R(-12f, 12f), 0, R(-6f, 11f));
                if (new Vector2(p.x, p.z + 1.6f).magnitude < 2.2f) continue;
                Ellipsoid(p, new Vector3(R(0.18f, 0.32f), R(0.08f, 0.14f), R(0.15f, 0.25f)), rnd.NextDouble() < 0.5 ? Leaf1 : Leaf3, 8, 4);
            }
        }
        static void Flower(Vector3 p, float s, Color col, bool stem = false)
        {
            if (stem) Cylinder(new Vector3(p.x, 0, p.z), 0.012f, p.y, Leaf3, 4);
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f;
                Ellipsoid(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * s * 0.8f, new Vector3(s * 0.62f, s * 0.35f, s * 0.62f), col, 6, 4);
            }
            Ellipsoid(p + Vector3.up * s * 0.2f, Vector3.one * s * 0.45f, new Color(1f, 0.86f, 0.3f), 6, 4);
        }

        static void Stones()
        {
            // stepping stones from the front of the scene to the welcome circle, and a ring of pebbles around it
            for (int i = 0; i < 5; i++)
            {
                float z = -6.2f + i * 0.95f, x = Mathf.Sin(i * 1.3f) * 0.35f;
                Ellipsoid(new Vector3(x, 0.02f, z), new Vector3(0.42f, 0.05f, 0.32f), i % 2 == 0 ? Stone : Stone2, 12, 4);
            }
            for (int i = 0; i < 22; i++)
            {
                float a = i * Mathf.PI * 2f / 22f; var p = new Vector3(Mathf.Cos(a) * 1.55f, 0.02f, -1.6f + Mathf.Sin(a) * 1.55f);
                Ellipsoid(p, new Vector3(R(0.12f, 0.18f), 0.07f, R(0.1f, 0.15f)), i % 2 == 0 ? Stone : Stone2, 8, 4);
            }
        }

        static void Clouds()
        {
            (float x, float y, float s)[] puffs = { (-9f, 6.4f, 1.1f), (-2.5f, 7.6f, 0.9f), (4.5f, 6.8f, 1.2f), (10.5f, 7.4f, 0.95f), (-13f, 8.2f, 0.8f) };
            foreach (var (x, y, s) in puffs)
            {
                var p = new Vector3(x, y, 13.2f);
                Ellipsoid(p, new Vector3(1.2f, 0.7f, 0.5f) * s, Cloud, 12, 8);
                Ellipsoid(p + new Vector3(-1.0f, -0.2f, 0.05f) * s, new Vector3(0.8f, 0.5f, 0.45f) * s, Cloud, 10, 6);
                Ellipsoid(p + new Vector3(1.05f, -0.15f, 0.05f) * s, new Vector3(0.85f, 0.55f, 0.45f) * s, Cloud, 10, 6);
                Ellipsoid(p + new Vector3(0.3f, 0.45f, 0f) * s, new Vector3(0.7f, 0.5f, 0.4f) * s, Cloud, 10, 6);
            }
        }

        // ------------------------------------------------------------------ primitives
        static void Ellipsoid(Vector3 centre, Vector3 radii, Color col, int lon, int lat)
        {
            int s = v.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat, y = Mathf.Cos(th), r = Mathf.Sin(th);
                for (int j = 0; j <= lon; j++)
                {
                    float ph = 2f * Mathf.PI * j / lon;
                    v.Add(centre + Vector3.Scale(new Vector3(r * Mathf.Cos(ph), y, r * Mathf.Sin(ph)), radii)); c.Add(col);
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = s + i * (lon + 1) + j, b = a + lon + 1;
                    t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
        }
        static void Cylinder(Vector3 bottom, float radius, float height, Color col, int seg)
        {
            int s = v.Count;
            for (int j = 0; j <= seg; j++)
            {
                float ph = 2f * Mathf.PI * j / seg; var o = new Vector3(Mathf.Cos(ph), 0, Mathf.Sin(ph)) * radius;
                v.Add(bottom + o); c.Add(col); v.Add(bottom + o + Vector3.up * height); c.Add(col);
            }
            for (int j = 0; j < seg; j++) { int a = s + j * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
        }
        static void Box(Vector3 centre, Vector3 size, Color col)
        {
            if (size.sqrMagnitude < 1e-6f) return;
            var h = size * 0.5f;
            Vector3[] p = { new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z), new(-h.x, -h.y, h.z), new(h.x, -h.y, h.z), new(h.x, h.y, h.z), new(-h.x, h.y, h.z) };
            int[][] f = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
            foreach (var q in f) { int s = v.Count; foreach (var i in q) { v.Add(centre + p[i]); c.Add(col); } t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
        }
        /// <summary>A gable roof: triangular prism along X, ridge height h, overhanging the walls.</summary>
        static void Prism(Vector3 baseCentre, float lengthX, float depthZ, float h, Color col)
        {
            float hx = lengthX / 2f, hz = depthZ / 2f;
            Vector3 a = new(-hx, 0, -hz), b = new(hx, 0, -hz), cc = new(hx, 0, hz), d = new(-hx, 0, hz), r0 = new(-hx, h, 0), r1 = new(hx, h, 0);
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) { int s = v.Count; foreach (var p in new[] { p0, p1, p2, p3 }) { v.Add(baseCentre + p); c.Add(col); } t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Color k) { int s = v.Count; foreach (var p in new[] { p0, p1, p2 }) { v.Add(baseCentre + p); c.Add(k); } t.AddRange(new[] { s, s + 1, s + 2 }); }
            Quad(a, r0, r1, b);                       // front slope (faces −Z)
            Quad(cc, r1, r0, d);                      // back slope
            Tri(a, d, r0, Wall); Tri(b, r1, cc, Wall); // gable ends in wall colour
            Quad(a, b, cc, d);                        // underside
        }
    }
}
