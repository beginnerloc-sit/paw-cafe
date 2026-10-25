using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>Builds one vertex-coloured mesh out of simple shapes (same toon material as the café props).
    /// Vertex alpha must stay 1 — alpha 0 means "glow" in the toon shader.</summary>
    public class MeshKit
    {
        readonly List<Vector3> v = new(); readonly List<Color> c = new(); readonly List<int> t = new();
        /// <summary>Applied to every vertex added after it is set (move / rotate / scale a whole group).</summary>
        public Matrix4x4 M = Matrix4x4.identity;
        public int Count => v.Count;
        void V(Vector3 p, Color col) { v.Add(M.MultiplyPoint3x4(p)); c.Add(col); }

        public void Ellipsoid(Vector3 centre, Vector3 radii, Color col, int lon = 14, int lat = 9)
        {
            int s = v.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat, y = Mathf.Cos(th), r = Mathf.Sin(th);
                for (int j = 0; j <= lon; j++) { float ph = 2f * Mathf.PI * j / lon; V(centre + Vector3.Scale(new Vector3(r * Mathf.Cos(ph), y, r * Mathf.Sin(ph)), radii), col); }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++) { int a = s + i * (lon + 1) + j, b = a + lon + 1; t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b }); }
        }
        public void Sphere(Vector3 centre, float r, Color col, int lon = 14, int lat = 9) => Ellipsoid(centre, Vector3.one * r, col, lon, lat);

        public void Cylinder(Vector3 bottom, float radius, float height, Color col, int seg = 12, bool caps = true, float topRadius = -1f)
        {
            if (topRadius < 0) topRadius = radius;
            int s = v.Count;
            for (int j = 0; j <= seg; j++)
            {
                float ph = 2f * Mathf.PI * j / seg; var d = new Vector3(Mathf.Cos(ph), 0, Mathf.Sin(ph));
                V(bottom + d * radius, col); V(bottom + d * topRadius + Vector3.up * height, col);
            }
            for (int j = 0; j < seg; j++) { int a = s + j * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            if (!caps) return;
            int ct = v.Count; V(bottom + Vector3.up * height, col);
            int cb = v.Count; V(bottom, col);
            for (int j = 0; j < seg; j++)
            {
                int a = s + j * 2, b = s + (j + 1) * 2;
                t.AddRange(new[] { ct, b + 1, a + 1 });   // top, facing up
                t.AddRange(new[] { cb, a, b });           // bottom, facing down
            }
        }
        public void Cone(Vector3 bottom, float radius, float height, Color col, int seg = 12) => Cylinder(bottom, radius, height, col, seg, true, 0.001f);

        public void Box(Vector3 centre, Vector3 size, Color col) => Box(centre, size, Quaternion.identity, col);
        public void Box(Vector3 centre, Vector3 size, Quaternion rot, Color col)
        {
            if (size.sqrMagnitude < 1e-6f) return;
            var h = size * 0.5f;
            Vector3[] p = { new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z), new(-h.x, -h.y, h.z), new(h.x, -h.y, h.z), new(h.x, h.y, h.z), new(-h.x, h.y, h.z) };
            int[][] f = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
            foreach (var q in f) { int s = v.Count; foreach (var i in q) V(centre + rot * p[i], col); t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
        }
        /// <summary>A thin box from a to b (a stick, a string, a pole).</summary>
        public void Stick(Vector3 a, Vector3 b, float thick, Color col)
        {
            var d = b - a; if (d.sqrMagnitude < 1e-6f) return;
            Box((a + b) * 0.5f, new Vector3(thick, thick, d.magnitude), Quaternion.LookRotation(d, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up), col);
        }
        /// <summary>A flat disc lying on the ground (facing up), optionally fading from inner to outer colour.</summary>
        public void Disc(Vector3 centre, float radius, Color inner, Color outer, int seg = 40, float squashZ = 1f)
        {
            int s = v.Count; V(centre, inner);
            for (int j = 0; j <= seg; j++) { float ph = 2f * Mathf.PI * j / seg; V(centre + new Vector3(Mathf.Cos(ph) * radius, 0, Mathf.Sin(ph) * radius * squashZ), outer); }
            for (int j = 0; j < seg; j++) t.AddRange(new[] { s, s + 2 + j, s + 1 + j });
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color col, bool twoSided = true)
        {
            int s = v.Count; V(a, col); V(b, col); V(cc, col); t.AddRange(new[] { s, s + 1, s + 2 });
            if (twoSided) { s = v.Count; V(a, col); V(b, col); V(cc, col); t.AddRange(new[] { s, s + 2, s + 1 }); }   // own vertices so the normals don't cancel out
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col, bool twoSided = false)
        {
            int s = v.Count; V(a, col); V(b, col); V(cc, col); V(d, col); t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            if (twoSided) { s = v.Count; V(a, col); V(b, col); V(cc, col); V(d, col); t.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 }); }
        }
        /// <summary>A gable roof: triangular prism along X, ridge height h.</summary>
        public void Prism(Vector3 baseCentre, float lengthX, float depthZ, float h, Color col, Color gable)
        {
            float hx = lengthX / 2f, hz = depthZ / 2f;
            Vector3 a = new(-hx, 0, -hz), b = new(hx, 0, -hz), cc = new(hx, 0, hz), d = new(-hx, 0, hz), r0 = new(-hx, h, 0), r1 = new(hx, h, 0);
            Quad(baseCentre + a, baseCentre + r0, baseCentre + r1, baseCentre + b, col);
            Quad(baseCentre + cc, baseCentre + r1, baseCentre + r0, baseCentre + d, col);
            Tri(baseCentre + a, baseCentre + d, baseCentre + r0, gable); Tri(baseCentre + b, baseCentre + r1, baseCentre + cc, gable);
        }
        /// <summary>An arch of boxes in the XY plane (a finish-line gate, a rainbow).</summary>
        public void Arc(Vector3 centre, float radius, float thick, float depth, float fromDeg, float toDeg, int segs, System.Func<int, Color> colour)
        {
            for (int i = 0; i < segs; i++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, i / (float)segs) * Mathf.Deg2Rad, a1 = Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)segs) * Mathf.Deg2Rad;
                var p0 = centre + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * radius; var p1 = centre + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * radius;
                var d = p1 - p0; Box((p0 + p1) * 0.5f, new Vector3(d.magnitude + 0.02f, thick, depth), Quaternion.FromToRotation(Vector3.right, d), colour(i));
            }
        }

        /// <summary>Just the mesh (to share between many objects, e.g. yarn balls of one colour).</summary>
        public Mesh ToMesh()
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            v.Clear(); c.Clear(); t.Clear(); M = Matrix4x4.identity; return m;
        }
        public GameObject Build(string name, Transform parent, Material mat, bool shadows = true)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            v.Clear(); c.Clear(); t.Clear(); M = Matrix4x4.identity;
            return g;
        }
    }

    /// <summary>Shared garden scenery pieces (trees, bushes, flowers, clouds, hills) for the 3D games.</summary>
    public static class Scenery
    {
        public static readonly Color Leaf1 = new(0.42f, 0.74f, 0.40f), Leaf2 = new(0.52f, 0.82f, 0.45f), Leaf3 = new(0.34f, 0.64f, 0.36f);
        public static readonly Color Blossom1 = new(1f, 0.72f, 0.82f), Blossom2 = new(1f, 0.82f, 0.88f), Trunk = new(0.58f, 0.40f, 0.28f);
        public static readonly Color Hill1 = new(0.56f, 0.82f, 0.50f), Hill2 = new(0.47f, 0.76f, 0.46f), Hill3 = new(0.62f, 0.86f, 0.56f);
        public static readonly Color[] Petals = { new(1f, 0.55f, 0.70f), new(1f, 0.85f, 0.35f), new(0.75f, 0.62f, 1f), new(1f, 1f, 1f), new(0.55f, 0.80f, 1f), new(1f, 0.62f, 0.45f) };
        public static readonly Color[] Party = { new(1f, 0.45f, 0.62f), new(1f, 0.80f, 0.28f), new(0.45f, 0.78f, 1f), new(0.55f, 0.88f, 0.50f), new(0.78f, 0.60f, 1f), new(1f, 0.60f, 0.38f) };

        public static void Tree(MeshKit k, Vector3 p, float s, bool blossom)
        {
            k.Cylinder(p, 0.18f * s, 1.4f * s, Trunk, 8);
            Color a = blossom ? Blossom1 : Leaf1, b = blossom ? Blossom2 : Leaf2, d = blossom ? new Color(1f, 0.64f, 0.76f) : Leaf3;
            var top = p + Vector3.up * 1.65f * s;
            k.Ellipsoid(top, Vector3.one * 1.0f * s, a, 14, 10);
            k.Ellipsoid(top + new Vector3(-0.66f, -0.27f, 0.1f) * s, Vector3.one * 0.7f * s, b, 12, 8);
            k.Ellipsoid(top + new Vector3(0.64f, -0.22f, -0.15f) * s, Vector3.one * 0.74f * s, d, 12, 8);
            k.Ellipsoid(top + new Vector3(0.05f, 0.66f, 0f) * s, Vector3.one * 0.64f * s, b, 12, 8);
        }
        public static void Bush(MeshKit k, Vector3 p, float s, System.Random rnd)
        {
            k.Ellipsoid(p + Vector3.up * 0.32f * s, new Vector3(0.62f, 0.45f, 0.55f) * s, Leaf2, 12, 8);
            k.Ellipsoid(p + new Vector3(-0.42f, 0.24f, 0.05f) * s, new Vector3(0.4f, 0.33f, 0.38f) * s, Leaf1, 10, 6);
            k.Ellipsoid(p + new Vector3(0.45f, 0.22f, -0.05f) * s, new Vector3(0.38f, 0.3f, 0.36f) * s, Leaf3, 10, 6);
            for (int i = 0; i < 4; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2; var q = p + new Vector3(Mathf.Cos(a) * 0.45f * s, (0.35f + 0.3f * (float)rnd.NextDouble()) * s, Mathf.Sin(a) * 0.4f * s - 0.15f);
                Flower(k, q, 0.07f, Petals[rnd.Next(Petals.Length)]);
            }
        }
        public static void Flower(MeshKit k, Vector3 p, float s, Color col, bool stem = false)
        {
            if (stem) k.Cylinder(new Vector3(p.x, 0, p.z), 0.012f, p.y, Leaf3, 4, false);
            for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * 2f / 5f; k.Ellipsoid(p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * s * 0.8f, new Vector3(s * 0.62f, s * 0.35f, s * 0.62f), col, 6, 4); }
            k.Ellipsoid(p + Vector3.up * s * 0.2f, Vector3.one * s * 0.45f, new Color(1f, 0.86f, 0.3f), 6, 4);
        }
        public static void Cloud(MeshKit k, Vector3 p, float s)
        {
            var w = Color.white;
            k.Ellipsoid(p, new Vector3(1.2f, 0.7f, 0.5f) * s, w, 12, 8);
            k.Ellipsoid(p + new Vector3(-1.0f, -0.2f, 0.05f) * s, new Vector3(0.8f, 0.5f, 0.45f) * s, w, 10, 6);
            k.Ellipsoid(p + new Vector3(1.05f, -0.15f, 0.05f) * s, new Vector3(0.85f, 0.55f, 0.45f) * s, w, 10, 6);
            k.Ellipsoid(p + new Vector3(0.3f, 0.45f, 0f) * s, new Vector3(0.7f, 0.5f, 0.4f) * s, w, 10, 6);
        }
        public static void Hill(MeshKit k, Vector3 p, Vector3 r, Color col) => k.Ellipsoid(p, r, col, 18, 10);
        /// <summary>A string of little triangle flags hanging between two points.</summary>
        public static void Bunting(MeshKit k, Vector3 a, Vector3 b, float sag, int flags, float size)
        {
            Vector3 P(float u) => Vector3.Lerp(a, b, u) + Vector3.down * sag * 4f * u * (1 - u);
            for (int i = 0; i < 12; i++) k.Stick(P(i / 12f), P((i + 1) / 12f), 0.025f, new Color(1f, 0.97f, 0.9f));
            for (int i = 0; i < flags; i++)
            {
                float u0 = (i + 0.15f) / flags, u1 = (i + 0.85f) / flags; var p0 = P(u0); var p1 = P(u1); var mid = (p0 + p1) * 0.5f + Vector3.down * size;
                k.Tri(p0, p1, mid, Party[i % Party.Length]);
            }
        }
    }

    /// <summary>A real café cat model used as an actor in a 3D minigame (no AI, no nav agent).</summary>
    public class Puppet
    {
        public GameObject Go; public Transform T; public Animator Anim; public CatFace Face; public CatDef Def;
        public float BaseScale = 1f; public string Clip;
        float sq, sqV;   // squash-and-stretch spring: + = squashed (short and wide), − = stretched
        public void Play(string clip, float fade = 0.15f, bool restart = false)
        {
            if (!Anim || (Clip == clip && !restart)) return;
            Clip = clip; Anim.speed = 1f; if (Face) Face.EyesClosed = clip == "Sleep";
            if (restart) Anim.CrossFadeInFixedTime(clip, fade, 0, 0f); else Anim.CrossFadeInFixedTime(clip, fade);
        }
        public void Freeze(bool on, float speed = 1f) { if (Anim) Anim.speed = on ? 0f : speed; }
        /// <summary>Kick the squash spring (≈0.3 is a nice landing squash; negative stretches).</summary>
        public void Squash(float amount) => sqV += amount * 14f;
        public void Update(float dt)
        {
            if (!T) return;
            sqV += (-200f * sq - 11f * sqV) * dt; sq += sqV * dt; float s = Mathf.Clamp(sq, -0.45f, 0.45f);
            T.localScale = new Vector3(1f + s * 0.55f, 1f - s, 1f + s * 0.55f) * BaseScale;
        }
    }

    /// <summary>
    /// Base for the "real 3D" minigames: builds a little set far away from the café (with its own camera, the scene sun
    /// and the toon prop material), spawns real animated cats, and gives the game camera moves, screen shake,
    /// squash-and-stretch and particle bursts. The UI Toolkit HUD from the host draws on top.
    /// Frame() runs every frame (also during the countdown and behind the results card); Tick() only while playing.
    /// </summary>
    public abstract class Game3D : MiniGame
    {
        public override bool Is3D => true;
        static readonly Vector3 Origin = new(800f, 0f, 0f);
        protected Transform Root; protected Camera Cam; protected Material PropMat;
        protected Vector3 camPos, camLook; protected float camFov = 40f, camRoll, camSmooth = 5f;
        Vector3 cPos, cLook; float cFov, cRoll, shake, panK; bool camInit;
        protected readonly List<Puppet> Puppets = new();
        protected virtual Color Sky => new(0.72f, 0.86f, 1f);

        protected sealed override void Setup()
        {
            Root = new GameObject("Game3D_" + Id).transform; Root.position = Origin;
            PropMat = CafeRefs.I ? CafeRefs.I.Prop : null;
            var cg = new GameObject("GameCam"); cg.transform.SetParent(Root, false);
            Cam = cg.AddComponent<Camera>(); Cam.fieldOfView = 40f; Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 140f;
            Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Sky; Cam.depth = 6;
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(Cam).renderPostProcessing = true;
            Build();
            if (!camInit) SnapCamera();
        }
        protected abstract void Build();

        /// <summary>Jump the camera to its target (no easing) — or to a given intro shot that it then glides away from.</summary>
        protected void SnapCamera(Vector3? pos = null, Vector3? look = null, float? fov = null)
        {
            cPos = pos ?? camPos; cLook = look ?? camLook; cFov = fov ?? camFov; cRoll = camRoll; camInit = true; ApplyCam();
        }
        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        public override void Frame(float dt)
        {
            if (!Root) return;
            foreach (var p in Puppets) p.Update(dt);
            float k = 1f - Mathf.Exp(-camSmooth * dt);
            cPos = Vector3.Lerp(cPos, camPos, k); cLook = Vector3.Lerp(cLook, camLook, k); cFov = Mathf.Lerp(cFov, camFov, k); cRoll = Mathf.Lerp(cRoll, camRoll, k);
            shake = Mathf.Max(0, shake - dt * 2.2f);
            panK = Damp(panK, Over ? 1f : 0f, 2.5f, dt);   // behind the results card: slide the hero over to the left third
            ApplyCam();
        }
        void ApplyCam()
        {
            if (!Cam) return;
            var off = shake > 0 ? new Vector3(Mathf.PerlinNoise(Time.time * 28f, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, Time.time * 28f) - 0.5f, 0) * shake * 0.5f : Vector3.zero;
            var dir = cLook - cPos; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 0, cRoll + off.x * 6f);
            var pan = rot * Vector3.right * (panK * 0.74f * Mathf.Tan(cFov * 0.5f * Mathf.Deg2Rad) * Cam.aspect * dir.magnitude);
            Cam.transform.localPosition = cPos + rot * off + pan; Cam.transform.localRotation = rot; Cam.fieldOfView = cFov;
        }

        // ------------------------------------------------------------ actors and props
        protected Puppet Cat(string id, Vector3 pos, float yaw, float scale = 1f)
        {
            var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + id);
            if (!prefab) prefab = Resources.Load<GameObject>("CafeCats/CafeCat_Mochi"); if (!prefab) return null;
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var g = Object.Instantiate(prefab, tmp.transform);
            foreach (var c in g.GetComponentsInChildren<CafeCat>(true)) Object.DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            g.transform.SetParent(Root, false); Object.Destroy(tmp);
            g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, yaw, 0); g.transform.localScale = Vector3.one * scale;
            var p = new Puppet { Go = g, T = g.transform, Anim = g.GetComponentInChildren<Animator>(), Face = g.GetComponentInChildren<CatFace>(), Def = GameData.Cat(id), BaseScale = scale };
            if (p.Anim) { p.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; p.Anim.Rebind(); }
            Puppets.Add(p); return p;
        }
        protected void Remove(Puppet p) { if (p == null) return; Puppets.Remove(p); if (p.Go) Object.Destroy(p.Go); }
        /// <summary>A real café furniture piece as set dressing.</summary>
        protected GameObject Furniture(string id, Vector3 pos, float yaw, float scale = 1f)
        {
            var prefab = Resources.Load<GameObject>("Furniture/" + id); if (!prefab) return null;
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var g = Object.Instantiate(prefab, tmp.transform);
            foreach (var c in g.GetComponentsInChildren<FurnitureInstance>(true)) Object.DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshObstacle>(true)) Object.DestroyImmediate(c);
            g.transform.SetParent(Root, false); Object.Destroy(tmp);
            g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, yaw, 0); g.transform.localScale *= scale;
            return g;
        }
        protected GameObject MakeMesh(MeshKit k, string name, Transform parent = null, bool shadows = true) => k.Build(name, parent ? parent : Root, PropMat, shadows);
        static MaterialPropertyBlock mpb;
        /// <summary>Tint a renderer (and optionally make it glow, unlit).</summary>
        protected static void Tint(Renderer r, Color col, bool glow)
        {
            if (!r) return; mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb); mpb.SetColor("_Tint", col); mpb.SetFloat("_Unlit", glow ? 1f : 0f); r.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------ juice
        protected Vector3 W2(Vector3 local) => Root.TransformPoint(local);
        /// <summary>Stage (UI) position of a point in the set — for floating "+1" labels over 3D things.</summary>
        protected Vector2 ToStage(Vector3 local)
        {
            if (!Cam || S?.panel == null) return new Vector2(W / 2, H / 2);
            var sp = Cam.WorldToScreenPoint(W2(local)); var ps = S.panel.visualTree.layout.size;
            var panel = new Vector2(sp.x / Cam.pixelWidth * ps.x, (1f - sp.y / Cam.pixelHeight) * ps.y);
            return S.WorldToLocal(panel);
        }
        protected void Pop3D(string text, Vector3 local, Color col, float size = 1f) => Pop(text, ToStage(local), col, size);
        /// <summary>Where a stage (UI) point lands on a plane in the set (local space). False if the ray misses.</summary>
        protected bool StageToPlane(Vector2 stage, Vector3 planePoint, Vector3 planeNormal, out Vector3 local)
        {
            local = default; if (!Cam || S?.panel == null) return false;
            var ps = S.panel.visualTree.layout.size; var wp = S.LocalToWorld(stage);
            var ray = Cam.ScreenPointToRay(new Vector3(wp.x / ps.x * Cam.pixelWidth, (1f - wp.y / ps.y) * Cam.pixelHeight, 0));
            var pl = new Plane(planeNormal, W2(planePoint)); if (!pl.Raycast(ray, out float d)) return false;
            local = Root.InverseTransformPoint(ray.GetPoint(d)); return true;
        }
        /// <summary>Is a tap (stage point) on a thing at this set position? Radius in metres, measured on screen.</summary>
        protected bool TapHits(Vector2 stage, Vector3 local, float radius, float minPx = 46f)
        {
            var c = ToStage(local); var e = ToStage(local + Cam.transform.right * radius);
            return (stage - c).magnitude < Mathf.Max(minPx, (e - c).magnitude);
        }
        /// <summary>A GameObject showing a shared mesh with the prop material.</summary>
        protected GameObject MeshObj(Mesh m, string name, Transform parent = null, bool shadows = true)
        {
            var g = new GameObject(name); g.transform.SetParent(parent ? parent : Root, false);
            g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = PropMat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }
        /// <summary>Half the visible width (metres) at a distance from the camera — for spawning just off-screen on any aspect.</summary>
        protected float HalfWidthAt(float dist) => dist * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Cam.aspect;

        static Material confettiMat;
        static Material ConfettiMat()
        {
            if (confettiMat) return confettiMat;
            var baseMat = Fx.Mat(); if (!baseMat) return null;
            const int w = 16, h = 10; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { bool edge = (x == 0 || x == w - 1) && (y == 0 || y == h - 1); tex.SetPixel(x, y, new Color(1, 1, 1, edge ? 0.4f : 1f)); }
            tex.Apply(); confettiMat = new Material(baseMat) { mainTexture = tex }; return confettiMat;
        }
        ParticleSystem MakePs(string name, Vector3 local, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(Root, false); go.transform.localPosition = local;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            var m = ps.main; m.loop = false; m.duration = 0.3f; m.stopAction = ParticleSystemStopAction.Destroy; m.simulationSpace = ParticleSystemSimulationSpace.World; m.playOnAwake = false;
            return ps;
        }
        static ParticleSystem.MinMaxGradient RandomOf(Color[] cols)
        {
            var g = new Gradient(); var keys = new GradientColorKey[Mathf.Min(8, cols.Length)];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(cols[i], keys.Length == 1 ? 0 : i / (float)(keys.Length - 1));
            g.SetKeys(keys, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }); g.mode = GradientMode.Fixed;
            return new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        }
        /// <summary>A round burst of soft sparkly particles.</summary>
        protected void Burst(Vector3 local, int count, Color[] cols, float speed = 2.5f, float size = 0.18f, float gravity = 0.6f, float life = 0.8f)
        {
            var ps = MakePs("Burst", local, Fx.Mat()); if (!ps) return;
            var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life); m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            m.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size); m.startColor = RandomOf(cols); m.gravityModifier = gravity;
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.1f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            ps.Play();
        }
        /// <summary>A soft white dust/poof cloud (landings, cats getting caught).</summary>
        protected void Poof(Vector3 local, float scale = 1f, int count = 12)
        {
            var ps = MakePs("Poof", local, Fx.Mat()); if (!ps) return;
            var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.8f * scale);
            m.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.6f * scale); m.startColor = new Color(1f, 0.98f, 0.95f, 0.9f); m.gravityModifier = -0.05f;
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = 0.2f * scale; s.rotation = new Vector3(-90, 0, 0);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 0.3f; lv.dampen = 0.12f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1, 0f)));
            ps.Play();
        }
        /// <summary>Party confetti fired upward from a point (finish lines, records).</summary>
        protected void Confetti(Vector3 local, int count = 60, float power = 1f)
        {
            var ps = MakePs("Confetti", local, ConfettiMat()); if (!ps) return;
            var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(5f * power, 9f * power);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f); m.startColor = RandomOf(Scenery.Party); m.gravityModifier = 0.55f;
            m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 28; s.radius = 0.2f; s.rotation = new Vector3(-90, 0, 0);
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 1.4f; lv.dampen = 0.06f;
            var ro = ps.rotationOverLifetime; ro.enabled = true; ro.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0)));
            var no = ps.noise; no.enabled = true; no.strength = 0.6f; no.frequency = 0.8f;
            ps.Play();
        }

        public override void Cleanup()
        {
            Puppets.Clear();
            if (Root) Object.Destroy(Root.gameObject); Root = null;
        }

        protected static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        protected static float EaseOutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2); }
        protected static Vector3 Damp(Vector3 a, Vector3 b, float rate, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-rate * dt));
        protected static float Damp(float a, float b, float rate, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-rate * dt));
    }
}
