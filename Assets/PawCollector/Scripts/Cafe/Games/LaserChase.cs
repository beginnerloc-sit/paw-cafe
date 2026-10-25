using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Laser Chase — 3D. Drag the glowing laser dot around the living room; your cat chases it (with a bit of overshoot).
    /// Lead it to the spinning toy stars, but don't let it crash into the flower vases.
    /// </summary>
    public class LaserChase : Game3D
    {
        public override string Id => "laser";
        public override string Title => "Laser Chase";
        public override string HowTo => "Drag the laser dot. Your cat chases it — collect stars, dodge the vases!";
        public override string IconPath => "Games/laser";
        public override int Unlock => 3;
        public override int[] Stars => new[] { 10, 20, 32 };
        public override Color TileCol => Hex(255, 222, 222);
        public override float CoinsPerPoint => 2f;
        protected override Color Sky => new(0.98f, 0.88f, 0.82f);

        const float FX = 5.4f, FZ = 2.9f;
        class Vase { public Transform t; public float fallT = -1, popT = -1; public Vector3 dir; }
        readonly List<Vase> vases = new(); Mesh[] vaseMeshes; Mesh starMesh;
        Puppet cat; Transform dot, star; Renderer dotGlow; Vector3 laser, catV; float hurtT, starPop = -1;

        protected override void Build()
        {
            TimeLeft = 60f; Lives = 3; MaxLives = 3;
            var k = new MeshKit();
            for (int i = 0; i < 44; i++) k.Box(new Vector3(-11f + i * 0.5f + 0.25f, -0.1f, 0), new Vector3(0.49f, 0.2f, 14f), i % 2 == 0 ? new Color(0.90f, 0.72f, 0.54f) : new Color(0.85f, 0.67f, 0.49f));
            k.Disc(new Vector3(0, 0.012f, 0), 4.2f, new Color(1f, 0.84f, 0.88f), new Color(1f, 0.74f, 0.80f), 48, 0.62f);   // big oval rug
            k.Disc(new Vector3(0, 0.016f, 0), 3.4f, new Color(1f, 0.90f, 0.92f), new Color(1f, 0.82f, 0.86f), 48, 0.62f);
            Color wall = new(0.92f, 0.94f, 1f), base_ = new(1f, 0.98f, 0.95f);
            k.Box(new Vector3(0, 2.5f, FZ + 1.4f), new Vector3(22f, 5f, 0.2f), wall); k.Box(new Vector3(0, 0.2f, FZ + 1.28f), new Vector3(22f, 0.4f, 0.06f), base_);
            foreach (var s in new[] { -1f, 1f }) { k.Box(new Vector3(s * (FX + 1.0f), 0.12f, 0), new Vector3(0.16f, 0.25f, FZ * 2 + 2.8f), new Color(0.76f, 0.56f, 0.40f)); }
            // sofa against the back wall, a bookshelf, a window and a floor lamp
            Color sofa = new(0.56f, 0.72f, 0.92f), sofaD = new(0.46f, 0.62f, 0.84f);
            k.Box(new Vector3(-1.2f, 0.45f, FZ + 0.85f), new Vector3(4f, 0.5f, 1.0f), sofa); k.Box(new Vector3(-1.2f, 1.0f, FZ + 1.2f), new Vector3(4f, 0.9f, 0.3f), sofaD);
            foreach (var s in new[] { -1f, 1f }) k.Box(new Vector3(-1.2f + s * 2.05f, 0.7f, FZ + 0.85f), new Vector3(0.3f, 0.75f, 1.0f), sofaD);
            for (int i = 0; i < 2; i++) k.Ellipsoid(new Vector3(-2f + i * 1.6f, 0.82f, FZ + 0.85f), new Vector3(0.7f, 0.18f, 0.42f), sofa, 12, 6);
            k.Ellipsoid(new Vector3(-2.5f, 1.05f, FZ + 0.95f), new Vector3(0.3f, 0.3f, 0.12f), new Color(1f, 0.75f, 0.4f), 10, 6);
            k.Box(new Vector3(3.6f, 1.4f, FZ + 1.05f), new Vector3(2f, 2.8f, 0.5f), new Color(0.82f, 0.62f, 0.44f));
            var rnd = new System.Random(9);
            for (int sh = 0; sh < 3; sh++) { float y = 0.5f + sh * 0.85f; k.Box(new Vector3(3.6f, y - 0.05f, FZ + 0.85f), new Vector3(1.8f, 0.06f, 0.4f), new Color(0.72f, 0.52f, 0.36f)); float x = 2.85f; while (x < 4.3f) { float w = 0.12f + (float)rnd.NextDouble() * 0.1f, h = 0.4f + (float)rnd.NextDouble() * 0.25f; k.Box(new Vector3(x + w / 2, y + h / 2, FZ + 0.85f), new Vector3(w, h, 0.32f), Scenery.Party[rnd.Next(Scenery.Party.Length)]); x += w + 0.02f; } }
            k.Box(new Vector3(-5.4f, 2.6f, FZ + 1.29f), new Vector3(2f, 2f, 0.06f), Color.white); k.Box(new Vector3(-5.4f, 2.6f, FZ + 1.25f), new Vector3(1.7f, 1.7f, 0.04f), new Color(0.70f, 0.87f, 1f));
            MakeMesh(k, "Room");
            Furniture("Plant", new Vector3(-FX - 0.3f, 0, FZ + 0.6f), 0f); Furniture("CatBed", new Vector3(FX + 0.2f, 0, -FZ - 0.6f), -40f);
            vaseMeshes = new Mesh[3]; Color[] vc = { new(0.55f, 0.75f, 1f), new(1f, 0.72f, 0.5f), new(0.78f, 0.62f, 1f) };
            for (int i = 0; i < 3; i++) { Props3D.Vase(k, Vector3.zero, 1.9f, vc[i]); vaseMeshes[i] = k.ToMesh(); }
            Props3D.Star(k, Vector3.zero, 0.5f, 0.14f, Props3D.Gold); starMesh = k.ToMesh();
            // the laser dot: a glowing red spot
            k.Disc(Vector3.zero, 0.42f, Color.white, Color.white, 24); k.Sphere(new Vector3(0, 0.05f, 0), 0.11f, Color.white, 10, 6);
            dot = MakeMesh(k, "Laser", null, false).transform; dotGlow = dot.GetComponent<Renderer>(); Tint(dotGlow, new Color(1f, 0.15f, 0.25f), true);
            laser = new Vector3(0, 0, -0.6f);
            cat = Cat(Hero != null ? Hero.Id : "Mochi", new Vector3(0, 0, -1.6f), 0f, 1.0f); cat.Play("Idle_Stand", 0f);
            for (int i = 0; i < 4; i++) AddVase();
            star = MeshObj(starMesh, "Star").transform; star.localPosition = FreeSpot(1.6f) + Vector3.up * 0.5f; starPop = 0;
            camPos = new Vector3(0, 8.4f, -6.9f); camLook = new Vector3(0, 0, 0.35f); camFov = 40f;
            SnapCamera(new Vector3(0, 2.4f, -5.2f), new Vector3(0, 0.6f, -1f), 44f); camSmooth = 1.6f;
        }
        void AddVase() { var t = MeshObj(vaseMeshes[vases.Count % 3], "Vase").transform; t.localPosition = FreeSpot(1.8f); vases.Add(new Vase { t = t, popT = 0 }); }
        Vector3 FreeSpot(float clear)
        {
            for (int tries = 0; tries < 50; tries++)
            {
                var p = new Vector3(Rnd(-FX + 0.6f, FX - 0.6f), 0, Rnd(-FZ + 0.5f, FZ - 0.5f));
                bool ok = cat == null || (p - cat.T.localPosition).magnitude > 2.0f;
                foreach (var v in vases) if (v.t && (v.t.localPosition - p).magnitude < clear) ok = false;
                if (star && (new Vector3(star.localPosition.x, 0, star.localPosition.z) - p).magnitude < clear) ok = false;
                if (ok) return p;
            }
            return Vector3.zero;
        }
        public override void Down(Vector2 p) { if (StageToPlane(p, Vector3.zero, Vector3.up, out var l)) laser = new Vector3(Mathf.Clamp(l.x, -FX, FX), 0, Mathf.Clamp(l.z, -FZ, FZ)); }
        public override void Move(Vector2 p) => Down(p);

        protected override void Tick(float dt)
        {
            hurtT = Mathf.Max(0, hurtT - dt);
            var cp = cat.T.localPosition; var to = laser - cp; to.y = 0;
            float maxV = 3.1f + Score * 0.04f;
            catV += to.normalized * Mathf.Min(to.magnitude * 9f, 10.5f) * dt; catV *= Mathf.Exp(-2.2f * dt);
            if (catV.magnitude > maxV) catV = catV.normalized * maxV;
            if (hurtT <= 0) cp += catV * dt;
            cp = new Vector3(Mathf.Clamp(cp.x, -FX + 0.3f, FX - 0.3f), 0, Mathf.Clamp(cp.z, -FZ + 0.3f, FZ - 0.3f)); cat.T.localPosition = cp;
            float sp = catV.magnitude;
            if (hurtT > 0) cat.Play("Idle_Sit", 0.1f);
            else if (sp > 0.45f) { cat.Play("Walk", 0.1f); cat.Freeze(false, 0.8f + sp * 0.55f); }
            else if (to.magnitude < 0.9f) cat.Play("Bat", 0.15f);
            else cat.Play("Idle_Stand", 0.2f);
            if (sp > 0.2f && hurtT <= 0) cat.T.localRotation = Quaternion.Slerp(cat.T.localRotation, Quaternion.LookRotation(new Vector3(catV.x, 0, catV.z)), 1f - Mathf.Exp(-10f * dt));
            // star
            var sflat = new Vector3(star.localPosition.x, 0, star.localPosition.z);
            if (starPop < 0 && (sflat - cp).magnitude < 0.7f)
            {
                Score++; Pop3D("+1", star.localPosition + Vector3.up * 0.6f, Hex(255, 170, 30)); Sfx("sfx_coin", 0.5f, 1.2f + Mathf.Min(0.6f, Score * 0.02f));
                Burst(star.localPosition, 16, new[] { Props3D.Gold, Color.white }, 2.6f, 0.16f, 0.2f, 0.7f); cat.Squash(-0.25f);
                star.localPosition = FreeSpot(1.6f) + Vector3.up * 0.5f; starPop = 0;
                if (Score % 6 == 0 && vases.Count < 9) AddVase();
                if (Score % 10 == 0) { Banner = $"{Score} stars!"; BannerCol = Hex(255, 196, 60); bannerT = 1.2f; }
            }
            if (bannerT > 0) { bannerT -= dt; if (bannerT <= 0) Banner = null; }
            // vases
            if (hurtT <= 0)
                foreach (var v in vases)
                {
                    if (v.fallT >= 0 || v.popT >= 0 && v.popT < 0.3f) continue;
                    var d = v.t.localPosition - cp; d.y = 0;
                    if (d.magnitude < 0.7f)
                    {
                        hurtT = 0.9f; v.fallT = 0; v.dir = d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward; catV = -catV * 0.5f;
                        Pop3D("Crash!", v.t.localPosition + Vector3.up * 1.3f, Hex(90, 150, 220), 1.2f); Shake(0.5f); Sfx("sfx_error", 0.5f, 1.2f); cat.Squash(0.35f);
                        Burst(cp + Vector3.up * 1.0f, 8, new[] { new Color(1f, 0.92f, 0.4f), Color.white }, 1.2f, 0.14f, -0.3f, 0.9f);   // seeing stars
                        LoseLife("Too many vases broke!"); break;
                    }
                }
        }
        float bannerT;
        public override void Frame(float dt)
        {
            float t = Time.time;
            dot.localPosition = Damp(dot.localPosition, laser + new Vector3(Mathf.Sin(t * 37f) * 0.025f, 0.05f, Mathf.Cos(t * 41f) * 0.025f), 30f, dt);
            dot.localScale = Vector3.one * (1f + Mathf.Sin(t * 12f) * 0.12f);
            if (star)
            {
                if (starPop >= 0) { starPop += dt * 3f; star.localScale = Vector3.one * EaseOutBack(starPop); if (starPop >= 1) { starPop = -1; star.localScale = Vector3.one; } }
                star.localRotation = Quaternion.Euler(-55f, 0, 0) * Quaternion.Euler(0, 0, t * 90f) * Quaternion.Euler(0, Mathf.Sin(t * 2f) * 25f, 0); var sp = star.localPosition; sp.y = 0.5f + Mathf.Sin(t * 3f) * 0.08f; star.localPosition = sp;
            }
            foreach (var v in vases)
            {
                if (v.popT >= 0) { v.popT += dt * 3f; v.t.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(v.popT)); if (v.popT >= 1) v.popT = -1; }
                if (v.fallT >= 0)
                {   // tip over, smash, pop back up somewhere else
                    v.fallT += dt; var axis = Vector3.Cross(Vector3.up, v.dir);
                    v.t.localRotation = Quaternion.AngleAxis(Mathf.Min(90f, v.fallT * v.fallT * 500f), axis);
                    if (v.fallT > 0.42f)
                    {
                        Burst(v.t.localPosition + v.dir * 0.5f + Vector3.up * 0.2f, 20, new[] { Color.white, new Color(0.6f, 0.75f, 1f), Scenery.Leaf2, Scenery.Petals[0] }, 3f, 0.12f, 1.2f, 0.7f);
                        Poof(v.t.localPosition + v.dir * 0.5f, 0.7f, 8); Sfx("sfx_pop", 0.5f, 0.7f);
                        v.fallT = -1; v.t.localRotation = Quaternion.identity; v.t.localPosition = FreeSpot(1.8f); v.popT = 0; v.t.localScale = Vector3.zero;
                    }
                }
            }
            camSmooth = CountingDown ? 1.6f : 3f;
            base.Frame(dt);
        }
    }

    /// <summary>
    /// Latte Art — 3D. A real cup of coffee on the café table; drag the steel milk pitcher to pour foam along the faint
    /// guide. Five cups (heart, cat face, star, spiral, flower); each finished cup slides away and a fresh one slides in.
    /// </summary>
    public class LatteArt : Game3D
    {
        public override string Id => "latte";
        public override string Title => "Latte Art";
        public override string HowTo => "Hold and trace the shape with milk foam. Start at the pink dot!";
        public override string IconPath => "Games/cup";
        public override int Unlock => 3;
        public override int[] Stars => new[] { 250, 350, 430 };
        public override Color TileCol => Hex(240, 226, 210);
        public override float CoinsPerPoint => 0.12f;
        protected override Color Sky => new(0.72f, 0.52f, 0.38f);

        static readonly string[] Shapes = { "heart", "catface", "star", "spiral", "flower" };
        const float Surf = 0.94f, ShapeS = 0.75f, Tol = 0.141f;
        Transform cupT, pitcher, stream, startDot; GameObject guideObj; ParticleSystem foam; Label roundL;
        List<Vector3> guide = new(); readonly List<Vector3> trace = new();
        int round = -1; float showT, slideT = -1; bool drawing, scored, slidingOut; Vector3 pour; bool hovering;

        protected override void Build()
        {
            var k = new MeshKit();
            for (int i = 0; i < 16; i++) k.Box(new Vector3(-8f + i + 0.5f, -0.1f, 0), new Vector3(0.98f, 0.2f, 12f), i % 2 == 0 ? new Color(0.78f, 0.56f, 0.40f) : new Color(0.74f, 0.52f, 0.37f));
            // table decor: napkin + croissant, sugar cubes, a little flower vase, a spoon
            k.Box(new Vector3(-2.9f, 0.01f, 0.6f), new Vector3(1.4f, 0.02f, 1.4f), Quaternion.Euler(0, 20, 0), new Color(1f, 0.92f, 0.94f));
            k.Ellipsoid(new Vector3(-2.9f, 0.14f, 0.6f), new Vector3(0.6f, 0.16f, 0.26f), new Color(0.95f, 0.70f, 0.36f), 14, 8);
            for (int i = 0; i < 4; i++) k.Ellipsoid(new Vector3(-3.3f + i * 0.27f, 0.2f, 0.6f), new Vector3(0.1f, 0.12f, 0.27f), new Color(0.88f, 0.60f, 0.30f), 8, 6);
            for (int i = 0; i < 3; i++) k.Box(new Vector3(2.4f + i * 0.22f, 0.09f, -1.1f + (i % 2) * 0.1f), new Vector3(0.18f, 0.18f, 0.18f), Quaternion.Euler(0, i * 17, 0), Color.white);
            Props3D.Vase(k, new Vector3(2.9f, 0, 1.4f), 1.2f, new Color(0.55f, 0.78f, 1f));
            k.Stick(new Vector3(1.6f, 0.05f, -1.4f), new Vector3(2.4f, 0.05f, -1.9f), 0.06f, new Color(0.85f, 0.86f, 0.9f)); k.Ellipsoid(new Vector3(1.5f, 0.05f, -1.33f), new Vector3(0.14f, 0.03f, 0.1f), new Color(0.85f, 0.86f, 0.9f), 10, 5);
            MakeMesh(k, "Table");
            cupT = new GameObject("Cup").transform; cupT.SetParent(Root, false);
            k.Disc(new Vector3(0, 0.02f, 0), 1.75f, new Color(1f, 0.99f, 0.97f), new Color(0.96f, 0.94f, 0.92f), 40); k.Cylinder(Vector3.zero, 1.7f, 0.02f, new Color(0.92f, 0.88f, 0.86f), 40, false, 1.78f);
            k.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(1.0f, 0.06f, 1.0f), new Color(0.94f, 0.90f, 0.88f), 24, 4);
            k.Cylinder(new Vector3(0, 0.03f, 0), 0.95f, 0.95f, Props3D.White, 36, false, 1.3f);
            k.Cylinder(new Vector3(0, 0.6f, 0), 1.19f, 0.12f, new Color(1f, 0.72f, 0.8f), 36, false, 1.235f);   // pink band
            k.Cylinder(new Vector3(0, 0.95f, 0), 1.3f, 0.04f, Color.white, 36, false, 1.22f);
            k.Disc(new Vector3(0, Surf - 0.005f, 0), 1.22f, new Color(0.80f, 0.56f, 0.36f), new Color(0.44f, 0.27f, 0.16f), 40);
            k.Arc(new Vector3(1.4f, 0.55f, 0), 0.26f, 0.09f, 0.12f, -90f, 90f, 10, _ => Props3D.White);
            MakeMesh(k, "CupMesh", cupT);
            var fg = new GameObject("Foam"); fg.transform.SetParent(cupT, false); foam = fg.AddComponent<ParticleSystem>(); foam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = foam.main; m.loop = false; m.playOnAwake = false; m.maxParticles = 6000; m.simulationSpace = ParticleSystemSimulationSpace.Local; m.startLifetime = 9999f; m.startSpeed = 0;
            var em = foam.emission; em.enabled = false; var psr = fg.GetComponent<ParticleSystemRenderer>(); psr.sharedMaterial = Fx.Mat(); psr.renderMode = ParticleSystemRenderMode.HorizontalBillboard; psr.sortingFudge = -10;
            foam.Play();
            Props3D.Pitcher(k, Vector3.zero, 1.6f); pitcher = MakeMesh(k, "Pitcher").transform;
            k.Cylinder(Vector3.zero, 0.035f, 1f, new Color(1f, 0.98f, 0.94f), 8); stream = MakeMesh(k, "Stream", null, false).transform; stream.gameObject.SetActive(false);
            k.Sphere(Vector3.zero, 0.09f, new Color(1f, 0.45f, 0.65f), 12, 8); startDot = MakeMesh(k, "Start", cupT, false).transform;
            roundL = new Label(""); roundL.AddToClassList("glatte-round"); roundL.pickingMode = PickingMode.Ignore; S.Add(roundL);
            pour = new Vector3(0.8f, Surf, -0.6f); pitcher.localPosition = pour + new Vector3(-0.35f, 0.9f, 0);
            camPos = new Vector3(0, 4.7f, -2.7f); camLook = new Vector3(0, 0.75f, 0.2f); camFov = 40f;
            SnapCamera(new Vector3(0, 2.2f, -4.5f), new Vector3(0, 0.6f, 0), 42f); camSmooth = 1.6f;
            NextRound(true);
        }
        void NextRound(bool first = false)
        {
            round++; trace.Clear(); scored = false; drawing = false; foam.Clear();
            if (guideObj) Object.Destroy(guideObj);
            if (round >= Shapes.Length) { Finish(Score >= Stars[0], "Beautiful lattes!"); return; }
            guide = BuildShape(Shapes[round]);
            var k = new MeshKit(); for (int i = 0; i < guide.Count; i += 3) k.Ellipsoid(guide[i] + Vector3.up * 0.004f, new Vector3(0.035f, 0.004f, 0.035f), new Color(0.92f, 0.74f, 0.55f), 8, 2);
            guideObj = MakeMesh(k, "Guide", cupT, false); startDot.localPosition = guide[0] + Vector3.up * 0.06f; startDot.gameObject.SetActive(true);
            roundL.text = $"Cup {round + 1} of {Shapes.Length}"; TimeLeft = 12f;
            if (!first) { cupT.localPosition = new Vector3(-7f, 0, 0); slideT = 0; slidingOut = false; TimeLeft = 99f; Sfx("sfx_place", 0.4f, 1.1f); }
        }
        List<Vector3> BuildShape(string shape)
        {
            var pts = new List<Vector3>(); int n = 140;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n; Vector2 p;
                switch (shape)
                {
                    case "heart": { float a = t * Mathf.PI * 2; p = new Vector2(16 * Mathf.Pow(Mathf.Sin(a), 3), -(13 * Mathf.Cos(a) - 5 * Mathf.Cos(2 * a) - 2 * Mathf.Cos(3 * a) - Mathf.Cos(4 * a))) / 17f; p.y += 0.12f; break; }
                    case "catface": { float a = t * Mathf.PI * 2 - Mathf.PI / 2; float ear = Mathf.Max(0, 1 - Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, -90 + 42)) / 22f) + Mathf.Max(0, 1 - Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, -90 - 42)) / 22f); float rr = 0.78f + ear * 0.42f; p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; p.y += 0.08f; break; }
                    case "star": { float a = t * 10f; int k = Mathf.FloorToInt(a) % 10; float f = a - Mathf.Floor(a); p = Vector2.Lerp(StarPt(k), StarPt((k + 1) % 10), f); break; }
                    case "spiral": { float a = t * Mathf.PI * 4.2f; float rr = 0.1f + 0.9f * t; p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; break; }
                    default: { float a = t * Mathf.PI * 2; float rr = 0.62f + 0.36f * Mathf.Cos(5 * a); p = new Vector2(Mathf.Cos(a - Mathf.PI / 2), Mathf.Sin(a - Mathf.PI / 2)) * rr; break; }
                }
                pts.Add(new Vector3(p.x * ShapeS, Surf, -p.y * ShapeS));   // screen-down (2D +y) = towards the camera (−z)
            }
            return pts;
        }
        static Vector2 StarPt(int k) { float a = -Mathf.PI / 2 + k * Mathf.PI / 5; float rr = k % 2 == 0 ? 1f : 0.42f; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; }

        bool ToCup(Vector2 p, out Vector3 local)
        {
            local = default; if (!StageToPlane(p, cupT.localPosition + Vector3.up * Surf, Vector3.up, out var l)) return false;
            local = l - cupT.localPosition; local.y = Surf; return true;
        }
        public override void Down(Vector2 p)
        {
            if (scored || Over || slideT >= 0 || !ToCup(p, out var c)) return;
            pour = c; hovering = true;
            if ((c - guide[0]).magnitude < 0.4f || trace.Count > 0) { drawing = true; Add(c); }
            else Pop3D("Start at the pink dot", guide[0] + Vector3.up * 0.5f + cupT.localPosition, Hex(255, 255, 255), 0.7f);
        }
        public override void Move(Vector2 p) { if (!ToCup(p, out var c)) return; pour = c; if (drawing) Add(c); }
        public override void Up(Vector2 p) { hovering = false; if (drawing && trace.Count > 10) ScoreRound(); drawing = false; }
        void Add(Vector3 p)
        {
            if (new Vector2(p.x, p.z).magnitude > 1.15f) return;   // keep the foam in the cup
            if (trace.Count > 0 && (p - trace[^1]).magnitude < 0.024f) return;
            if (trace.Count > 0) { var a = trace[^1]; int steps = Mathf.CeilToInt((p - a).magnitude / 0.027f); for (int i = 1; i < steps; i++) Foam(Vector3.Lerp(a, p, i / (float)steps)); }
            Foam(p); trace.Add(p); startDot.gameObject.SetActive(false);
        }
        void Foam(Vector3 p)
        {
            var ep = new ParticleSystem.EmitParams { position = p + Vector3.up * 0.006f, startSize = Rnd(0.11f, 0.14f), startColor = new Color(1f, 0.97f, 0.9f, 1f), startLifetime = 9999f, velocity = Vector3.zero };
            foam.Emit(ep, 1);
        }
        void ScoreRound()
        {
            scored = true;
            int cover = 0; foreach (var g in guide) { foreach (var t in trace) if ((t - g).sqrMagnitude < Tol * Tol) { cover++; break; } }
            int near = 0; foreach (var t in trace) { float best = float.MaxValue; foreach (var g in guide) best = Mathf.Min(best, (t - g).sqrMagnitude); if (best < Tol * Tol) near++; }
            float acc = (cover / (float)guide.Count) * 0.6f + (near / (float)trace.Count) * 0.4f;
            int pts = Mathf.RoundToInt(Mathf.Pow(acc, 1.4f) * 100f); Score += pts;
            var at = cupT.localPosition + new Vector3(0, Surf + 0.6f, 0.4f);
            Pop3D(pts >= 85 ? $"Gorgeous! +{pts}" : pts >= 60 ? $"Lovely +{pts}" : $"+{pts}", at, Hex(255, 255, 255), 1.3f);
            Sfx(pts >= 85 ? "sfx_levelup" : "sfx_coin", 0.5f); showT = 1.3f;
            if (pts >= 85) { Confetti(cupT.localPosition + new Vector3(-1.6f, 0.5f, 0.6f), 30, 0.7f); Confetti(cupT.localPosition + new Vector3(1.6f, 0.5f, 0.6f), 30, 0.7f); }
            else if (pts >= 60) Burst(cupT.localPosition + new Vector3(0, Surf + 0.2f, 0), 20, new[] { Props3D.Gold, Color.white, Props3D.Pink }, 2.4f, 0.14f, 0.2f, 0.8f);
        }
        protected override void Tick(float dt)
        {
            if (slideT >= 0) return;
            if (scored) { showT -= dt; if (showT <= 0) { slideT = 0; slidingOut = true; Sfx("sfx_tap", 0.4f, 0.9f); } return; }
        }
        protected override void TimeUp()
        {
            if (slideT >= 0) return;
            if (!scored) { drawing = false; if (trace.Count > 10) ScoreRound(); else { scored = true; showT = 0.8f; Pop3D("Too slow!", cupT.localPosition + Vector3.up * 1.6f, Hex(255, 255, 255)); } }
            TimeLeft = 99f;
        }
        public override void Frame(float dt)
        {
            // cups slide out to the right and a fresh one slides in from the left
            if (slideT >= 0)
            {
                slideT += dt / 0.5f; float t = Mathf.Clamp01(slideT);
                if (slidingOut) { cupT.localPosition = new Vector3(Mathf.Lerp(0, 7.5f, t * t), 0, 0); if (t >= 1) { slideT = -1; NextRound(); } }
                else { cupT.localPosition = new Vector3(Mathf.Lerp(-7f, 0, EaseOutBack(t)), 0, 0); if (t >= 1) { slideT = -1; cupT.localPosition = Vector3.zero; TimeLeft = 12f; } }
            }
            if (startDot && startDot.gameObject.activeSelf) startDot.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 6f) * 0.2f);
            // the pitcher follows your finger and tips over to pour
            var target = cupT.localPosition + pour + new Vector3(-0.42f, drawing ? 0.62f : 0.95f, 0.02f);
            pitcher.localPosition = Damp(pitcher.localPosition, target, 18f, dt);
            pitcher.localRotation = Quaternion.Slerp(pitcher.localRotation, Quaternion.Euler(0, 0, drawing ? -42f : hovering ? -12f : 0f), 1f - Mathf.Exp(-12f * dt));
            stream.gameObject.SetActive(drawing);
            if (drawing) { var top = pitcher.localPosition + pitcher.localRotation * new Vector3(0.38f, 0.58f, 0); var bot = cupT.localPosition + pour; stream.localPosition = bot; stream.localScale = new Vector3(1, Mathf.Max(0.05f, top.y - bot.y), 1); stream.localRotation = Quaternion.FromToRotation(Vector3.up, top - bot); stream.localScale = new Vector3(1, (top - bot).magnitude, 1); }
            camSmooth = CountingDown ? 1.6f : 3f;
            base.Frame(dt);
        }
        public override void Cleanup() { roundL?.RemoveFromHierarchy(); base.Cleanup(); }
    }
}
