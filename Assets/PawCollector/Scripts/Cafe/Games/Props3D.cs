using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>Little vertex-coloured toy models shared by the 3D minigames (all built around a local origin, metres).</summary>
    public static class Props3D
    {
        public static readonly Color White = new(1f, 0.98f, 0.95f), Ink = new(0.22f, 0.18f, 0.22f), Pink = new(1f, 0.62f, 0.72f), Gold = new(1f, 0.80f, 0.25f);
        static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>Balloon with a shine and a knot; the knot sits at c - (0, r*1.25, 0).</summary>
        public static void Balloon(MeshKit k, Vector3 c, float r, Color col)
        {
            k.Ellipsoid(c, new Vector3(r, r * 1.2f, r), col, 16, 12);
            k.Ellipsoid(c + new Vector3(-r * 0.33f, r * 0.45f, -r * 0.82f), new Vector3(r * 0.18f, r * 0.26f, r * 0.08f), Color.Lerp(col, Color.white, 0.75f), 6, 4);
            k.Cone(c + new Vector3(0, -r * 1.42f, 0), r * 0.18f, r * 0.26f, col, 8);
        }
        /// <summary>A cartoon fish facing +X.</summary>
        public static void Fish(MeshKit k, Vector3 c, float s, Color body, Color fin)
        {
            k.Ellipsoid(c, new Vector3(0.5f, 0.27f, 0.15f) * s, body, 14, 9);
            k.Ellipsoid(c + new Vector3(0.05f, -0.07f, 0) * s, new Vector3(0.38f, 0.16f, 0.14f) * s, Color.Lerp(body, Color.white, 0.45f), 12, 7);
            k.Tri(c + new Vector3(-0.42f, 0, 0) * s, c + new Vector3(-0.78f, 0.28f, 0) * s, c + new Vector3(-0.78f, -0.28f, 0) * s, fin);
            k.Tri(c + new Vector3(-0.05f, 0.22f, 0) * s, c + new Vector3(0.18f, 0.24f, 0) * s, c + new Vector3(-0.2f, 0.44f, 0) * s, fin);
            foreach (var z in new[] { -1f, 1f })
            {
                k.Sphere(c + new Vector3(0.3f, 0.06f, 0.11f * z) * s, 0.07f * s, Color.white, 8, 6);
                k.Sphere(c + new Vector3(0.33f, 0.06f, 0.15f * z) * s, 0.04f * s, Ink, 6, 4);
            }
        }
        /// <summary>A wind-up toy mouse facing −Z (towards the camera).</summary>
        public static void Mouse(MeshKit k, Vector3 c, float s, Color body, Color inner)
        {
            k.Ellipsoid(c + new Vector3(0, 0.2f, 0.05f) * s, new Vector3(0.24f, 0.2f, 0.32f) * s, body, 14, 9);
            k.Ellipsoid(c + new Vector3(0, 0.22f, -0.26f) * s, new Vector3(0.14f, 0.13f, 0.16f) * s, body, 12, 8);
            foreach (var x in new[] { -1f, 1f })
            {
                k.Ellipsoid(c + new Vector3(0.14f * x, 0.38f, -0.18f) * s, new Vector3(0.1f, 0.11f, 0.03f) * s, body, 10, 6);
                k.Ellipsoid(c + new Vector3(0.14f * x, 0.38f, -0.2f) * s, new Vector3(0.065f, 0.075f, 0.02f) * s, inner, 8, 5);
                k.Sphere(c + new Vector3(0.06f * x, 0.27f, -0.37f) * s, 0.03f * s, Ink, 6, 4);
            }
            k.Sphere(c + new Vector3(0, 0.2f, -0.42f) * s, 0.04f * s, inner, 6, 4);
            k.Stick(c + new Vector3(0, 0.12f, 0.34f) * s, c + new Vector3(0.1f, 0.2f, 0.55f) * s, 0.03f * s, inner);
            k.Stick(c + new Vector3(0.1f, 0.2f, 0.55f) * s, c + new Vector3(0.02f, 0.32f, 0.66f) * s, 0.03f * s, inner);
            k.Cylinder(c + new Vector3(0, 0.38f, 0.12f) * s, 0.025f * s, 0.12f * s, Gold, 6);                      // wind-up key
            k.Box(c + new Vector3(0, 0.52f, 0.12f) * s, new Vector3(0.18f, 0.08f, 0.03f) * s, Gold);
        }
        public static void Cucumber(MeshKit k, Vector3 c, float s)
        {
            Color g = new(0.36f, 0.66f, 0.30f), l = new(0.62f, 0.84f, 0.45f);
            k.Ellipsoid(c + new Vector3(0, 0.16f, 0) * s, new Vector3(0.48f, 0.16f, 0.16f) * s, g, 16, 9);
            for (int i = 0; i < 6; i++) k.Sphere(c + new Vector3(-0.32f + i * 0.13f, 0.2f + (i % 2) * 0.06f, -0.13f) * s, 0.025f * s, l, 6, 4);
            k.Cylinder(c + new Vector3(-0.5f, 0.16f, 0) * s, 0.04f * s, 0.05f * s, new Color(0.55f, 0.45f, 0.25f), 6);
        }
        /// <summary>A coffee cup on its own, base at c.</summary>
        public static void Cup(MeshKit k, Vector3 c, float s, Color band)
        {
            k.Cylinder(c, 0.16f * s, 0.3f * s, White, 14, true, 0.21f * s);
            k.Cylinder(c + new Vector3(0, 0.12f, 0) * s, 0.188f * s, 0.07f * s, band, 14, false, 0.198f * s);
            k.Disc(c + new Vector3(0, 0.29f, 0) * s, 0.19f * s, new Color(0.62f, 0.40f, 0.26f), new Color(0.52f, 0.32f, 0.2f), 14);
            k.Arc(c + new Vector3(0.22f, 0.16f, 0) * s, 0.08f * s, 0.035f * s, 0.04f * s, -80f, 80f, 6, _ => White);
        }
        public static void Cake(MeshKit k, Vector3 c, float s)
        {
            k.Cylinder(c, 0.2f * s, 0.18f * s, new Color(1f, 0.82f, 0.6f), 14);
            k.Cylinder(c + new Vector3(0, 0.08f, 0) * s, 0.205f * s, 0.03f * s, Pink, 14, false);
            k.Ellipsoid(c + new Vector3(0, 0.19f, 0) * s, new Vector3(0.21f, 0.06f, 0.21f) * s, White, 14, 6);
            k.Sphere(c + new Vector3(0, 0.27f, 0) * s, 0.07f * s, new Color(0.92f, 0.22f, 0.3f), 10, 7);
        }
        public static void Hairball(MeshKit k, Vector3 c, float s)
        {
            Color a = new(0.62f, 0.58f, 0.56f), b = new(0.72f, 0.68f, 0.64f);
            k.Ellipsoid(c + new Vector3(0, 0.17f, 0) * s, Vector3.one * 0.17f * s, a, 10, 7);
            k.Ellipsoid(c + new Vector3(0.11f, 0.24f, 0.05f) * s, Vector3.one * 0.1f * s, b, 8, 6);
            k.Ellipsoid(c + new Vector3(-0.12f, 0.12f, -0.06f) * s, Vector3.one * 0.1f * s, b, 8, 6);
            k.Ellipsoid(c + new Vector3(0.02f, 0.31f, -0.08f) * s, Vector3.one * 0.08f * s, a, 8, 6);
            k.Stick(c + new Vector3(0.15f, 0.12f, 0) * s, c + new Vector3(0.32f, 0.05f, 0.05f) * s, 0.02f * s, b);
        }
        public static void Vase(MeshKit k, Vector3 c, float s, Color col)
        {
            k.Cylinder(c, 0.14f * s, 0.24f * s, col, 14, true, 0.22f * s);
            k.Cylinder(c + new Vector3(0, 0.24f, 0) * s, 0.22f * s, 0.2f * s, col, 14, false, 0.1f * s);
            k.Cylinder(c + new Vector3(0, 0.44f, 0) * s, 0.1f * s, 0.12f * s, col, 14, true, 0.13f * s);
            k.Cylinder(c + new Vector3(0, 0.2f, 0) * s, 0.205f * s, 0.05f * s, Shade(col, 0.82f), 14, false, 0.215f * s);
            for (int i = 0; i < 3; i++)
            {
                var top = c + new Vector3(-0.09f + i * 0.09f, 0.78f + (i % 2) * 0.08f, 0) * s;
                k.Stick(c + new Vector3(0, 0.5f, 0) * s, top, 0.02f * s, Scenery.Leaf3);
                Scenery.Flower(k, top, 0.07f * s, Scenery.Petals[(i * 2) % Scenery.Petals.Length]);
            }
        }
        /// <summary>A chunky 5-point star standing up (faces −Z), centred on c.</summary>
        public static void Star(MeshKit k, Vector3 c, float r, float depth, Color col)
        {
            var side = Shade(col, 0.8f);
            Vector3 P(int i, float z) { float a = Mathf.PI / 2 + i * Mathf.PI / 5; float rr = i % 2 == 0 ? r : r * 0.45f; return c + new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z); }
            for (int i = 0; i < 10; i++)
            {
                int j = (i + 1) % 10;
                k.Tri(c + new Vector3(0, 0, -depth), P(j, -depth * 0.4f), P(i, -depth * 0.4f), col, false);
                k.Tri(c + new Vector3(0, 0, depth), P(i, depth * 0.4f), P(j, depth * 0.4f), col, false);
                k.Quad(P(i, -depth * 0.4f), P(j, -depth * 0.4f), P(j, depth * 0.4f), P(i, depth * 0.4f), side, true);
            }
        }
        /// <summary>A paw print token standing up (faces −Z).</summary>
        public static void Paw(MeshKit k, Vector3 c, float s, Color col)
        {
            k.Ellipsoid(c + new Vector3(0, -0.06f, 0) * s, new Vector3(0.16f, 0.13f, 0.05f) * s, col, 12, 7);
            foreach (var (dx, dy) in new[] { (-0.15f, 0.09f), (-0.055f, 0.17f), (0.055f, 0.17f), (0.15f, 0.09f) })
                k.Ellipsoid(c + new Vector3(dx, dy, 0) * s, new Vector3(0.055f, 0.065f, 0.045f) * s, col, 8, 5);
        }
        /// <summary>A ball of yarn: a sphere wound with a few darker wraps and a loose end.</summary>
        public static void Yarn(MeshKit k, float r, Color col)
        {
            k.Sphere(Vector3.zero, r, col, 16, 12);
            var dark = Shade(col, 0.78f); var light = Color.Lerp(col, Color.white, 0.25f);
            (Vector3 e, Color cc)[] wraps = { (new Vector3(20, 0, 30), dark), (new Vector3(-35, 40, 0), light), (new Vector3(70, -20, 10), dark), (new Vector3(10, 80, -50), light) };
            foreach (var (e, cc) in wraps)
            {
                var keep = k.M; k.M = keep * Matrix4x4.Rotate(Quaternion.Euler(e));
                k.Ellipsoid(Vector3.zero, new Vector3(r * 1.035f, r * 0.13f, r * 1.035f), cc, 18, 4);
                k.M = keep;
            }
            k.Stick(new Vector3(r * 0.7f, -r * 0.5f, -r * 0.4f), new Vector3(r * 1.25f, -r * 0.95f, -r * 0.5f), r * 0.07f, col);
        }
        /// <summary>A milk pitcher, spout towards +X, base at c.</summary>
        public static void Pitcher(MeshKit k, Vector3 c, float s)
        {
            Color steel = new(0.86f, 0.88f, 0.92f), dark = new(0.66f, 0.68f, 0.74f);
            k.Cylinder(c, 0.2f * s, 0.42f * s, steel, 16, true, 0.16f * s);
            k.Cylinder(c + new Vector3(0, 0.36f, 0) * s, 0.162f * s, 0.07f * s, dark, 16, false, 0.17f * s);
            k.Cone(c + new Vector3(0.15f, 0.34f, 0) * s, 0.06f * s, 0.1f * s, steel, 8);
            k.Box(c + new Vector3(0.18f, 0.39f, 0) * s, new Vector3(0.14f, 0.03f, 0.08f) * s, steel);
            k.Arc(c + new Vector3(-0.22f, 0.22f, 0) * s, 0.11f * s, 0.04f * s, 0.05f * s, 100f, 260f, 6, _ => dark);
        }
        /// <summary>An open cardboard box (good hiding place), base centre at c.</summary>
        public static void CardBox(MeshKit k, Vector3 c, Vector3 size, float yaw)
        {
            Color card = new(0.84f, 0.66f, 0.44f), cardD = new(0.74f, 0.56f, 0.36f), tape = new(0.93f, 0.85f, 0.65f);
            var keep = k.M; k.M = keep * Matrix4x4.TRS(c, Quaternion.Euler(0, yaw, 0), Vector3.one);
            float w = size.x, h = size.y, d = size.z, t = 0.04f;
            k.Box(new Vector3(0, t / 2, 0), new Vector3(w, t, d), cardD);
            k.Box(new Vector3(0, h / 2, -d / 2), new Vector3(w, h, t), card); k.Box(new Vector3(0, h / 2, d / 2), new Vector3(w, h, t), card);
            k.Box(new Vector3(-w / 2, h / 2, 0), new Vector3(t, h, d), cardD); k.Box(new Vector3(w / 2, h / 2, 0), new Vector3(t, h, d), cardD);
            k.Box(new Vector3(0, h * 0.5f, -d / 2 - 0.01f), new Vector3(w * 0.22f, h, 0.01f), tape);
            k.Box(new Vector3(0, h + 0.18f, -d / 2 - 0.12f), new Vector3(w * 0.96f, 0.03f, 0.4f), Quaternion.Euler(-55, 0, 0), card);   // open flaps
            k.Box(new Vector3(0, h + 0.18f, d / 2 + 0.12f), new Vector3(w * 0.96f, 0.03f, 0.4f), Quaternion.Euler(55, 0, 0), card);
            k.M = keep;
        }
        /// <summary>A brick chimney (base at c) of a given height.</summary>
        public static void Chimney(MeshKit k, Vector3 c, float w, float h, float d)
        {
            Color brick = new(0.80f, 0.42f, 0.34f), mortar = new(0.70f, 0.34f, 0.28f), cap = new(0.55f, 0.50f, 0.52f);
            k.Box(c + new Vector3(0, h / 2, 0), new Vector3(w, h, d), brick);
            for (float y = 0.25f; y < h - 0.1f; y += 0.25f) k.Box(c + new Vector3(0, y, -d / 2 - 0.005f), new Vector3(w * 0.98f, 0.03f, 0.01f), mortar);
            k.Box(c + new Vector3(0, h + 0.06f, 0), new Vector3(w * 1.2f, 0.14f, d * 1.2f), cap);
        }
    }
}
