using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Cat Stack — in 3D. A sleepy cat floats in on a bunch of balloons, swinging left and right above a big pink pouf.
    /// Tap to pop the balloons: the cat drops, lands with a squash and curls up into a loaf on top of the tower.
    /// Land it dead centre for a "Purrfect". Miss and it bounces off and wanders away (lose a heart). Lean the tower too
    /// far and it topples — cats everywhere. The camera rises with the tower.
    /// </summary>
    public class CatStack : Game3D
    {
        public override string Id => "stack";
        public override string Title => "Cat Stack";
        public override string HowTo => "Tap to pop the balloons and drop the sleepy cat onto the tower. Keep it balanced!";
        public override string IconPath => "Games/loaf_0";
        public override int Unlock => 2;
        public override int[] Stars => new[] { 8, 15, 25 };
        public override Color TileCol => Hex(255, 240, 220);
        public override float CoinsPerPoint => 3f;
        protected override Color Sky => new(1f, 0.93f, 0.86f);

        const float CushionTop = 0.64f, LoafH = 0.56f, LoafW = 0.9f, Gravity = 22f;
        class Stacked { public Puppet p; public float x; }
        class Body { public Puppet p; public Vector3 v; public float spin, t; public int bounces; public bool settled; public float walk; }
        readonly List<Stacked> stack = new(); readonly List<Body> bodies = new(); readonly List<string> pool = new();
        Transform tower, carrier; GameObject balloons; Puppet swing, falling; Vector3 fallV; float swingT, swingX, lean, toppleAng, toppleV; int perfectRun, next;
        bool toppling; float bannerT; readonly Color[] balloonCols = new Color[3];

        float TopY => CushionTop + stack.Count * LoafH;
        float TopX => stack.Count == 0 ? 0f : stack[^1].x;

        protected override void Build()
        {
            Lives = 3; MaxLives = 3;
            var rnd = new System.Random(5); float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var k = new MeshKit();
            // floor with planks
            Color wood1 = new(0.86f, 0.66f, 0.48f), wood2 = new(0.80f, 0.60f, 0.43f);
            for (int i = 0; i < 48; i++) k.Box(new Vector3(-12f + i * 0.5f + 0.25f, -0.1f, -1f), new Vector3(0.49f, 0.2f, 14f), i % 2 == 0 ? wood1 : wood2);
            k.Disc(new Vector3(0, 0.01f, 0.4f), 2.6f, new Color(1f, 0.80f, 0.85f), new Color(1f, 0.70f, 0.78f), 48, 0.6f);   // round rug
            // back wall with wallpaper stripes, wainscot, windows, shelves and frames all the way up
            Color wall = new(1f, 0.94f, 0.88f), stripe = new(1f, 0.86f, 0.86f), trim = new(0.98f, 0.82f, 0.70f);
            k.Box(new Vector3(0, 11f, 4.15f), new Vector3(28f, 22f, 0.3f), wall);
            for (int i = 0; i < 24; i++) k.Box(new Vector3(-13.8f + i * 1.2f, 11f, 3.99f), new Vector3(0.38f, 22f, 0.02f), stripe);
            k.Box(new Vector3(0, 0.6f, 3.95f), new Vector3(28f, 1.2f, 0.12f), trim);
            k.Box(new Vector3(0, 1.24f, 3.9f), new Vector3(28f, 0.1f, 0.2f), new Color(0.92f, 0.72f, 0.58f));
            (float x, float y)[] windows = { (-4.6f, 2.8f), (4.8f, 4.6f), (-3.8f, 8.2f), (4.2f, 11.6f), (-4.4f, 15.0f), (3.6f, 18.4f) };
            foreach (var (x, y) in windows) Window(k, new Vector3(x, y, 3.95f));
            for (int i = 0; i < 9; i++)
            {
                float y = 2.2f + i * 2.1f, x = (i % 2 == 0 ? 1 : -1) * R(1.9f, 2.6f);
                if (i % 3 == 2) Shelf(k, new Vector3(x, y, 3.8f), rnd); else Frame(k, new Vector3(x, y + 0.4f, 3.95f), Scenery.Party[i % Scenery.Party.Length]);
            }
            // wall clock
            k.M = Matrix4x4.TRS(new Vector3(-1.4f, 6.4f, 3.95f), Quaternion.Euler(-90, 0, 0), Vector3.one);
            k.Cylinder(Vector3.zero, 0.55f, 0.1f, new Color(1f, 0.6f, 0.68f), 24); k.Cylinder(new Vector3(0, 0.1f, 0), 0.45f, 0.02f, Color.white, 24);
            k.Box(new Vector3(0, 0.13f, 0.14f), new Vector3(0.05f, 0.02f, 0.3f), new Color(0.35f, 0.3f, 0.35f)); k.Box(new Vector3(0.1f, 0.13f, 0), new Vector3(0.22f, 0.02f, 0.05f), new Color(0.35f, 0.3f, 0.35f));
            k.M = Matrix4x4.identity;
            // the pouf the tower stands on
            k.Ellipsoid(new Vector3(0, 0.33f, 0), new Vector3(1.25f, 0.33f, 1.0f), new Color(1f, 0.56f, 0.70f), 24, 12);
            k.Ellipsoid(new Vector3(0, 0.46f, 0), new Vector3(1.08f, 0.2f, 0.86f), new Color(1f, 0.68f, 0.78f), 24, 10);
            k.Sphere(new Vector3(0, 0.64f, 0), 0.07f, new Color(1f, 0.85f, 0.4f), 8, 6);
            MakeMesh(k, "Room");
            Furniture("CatTree", new Vector3(-4.2f, 0, 2.0f), 30f); Furniture("Plant", new Vector3(4.4f, 0, 2.6f), 0f); Furniture("Beanbag", new Vector3(3.2f, 0, 0.9f), 200f);
            Furniture("ToyBasket", new Vector3(-2.7f, 0, 0.2f), 20f); Furniture("Fern", new Vector3(-5.6f, 0, 0.9f), 0f); Furniture("CatBed", new Vector3(5.6f, 0, 0.2f), 160f);

            tower = new GameObject("Tower").transform; tower.SetParent(Root, false); tower.localPosition = new Vector3(0, CushionTop, 0);
            carrier = new GameObject("Carrier").transform; carrier.SetParent(Root, false);
            // the cats that will float in: your cat first, then a mix (your residents first)
            var ids = CafeManager.I ? CafeManager.I.Data.residents.Where(id => Resources.Load<GameObject>("CafeCats/CafeCat_" + id)).ToList() : new List<string>();
            var extra = GameData.Cats.Where(c => Resources.Load<GameObject>("CafeCats/CafeCat_" + c.Id)).Select(c => c.Id).OrderBy(_ => Random.value).ToList();
            pool.Add(Hero != null ? Hero.Id : "Mochi"); foreach (var id in ids.OrderBy(_ => Random.value).Concat(extra)) if (!pool.Contains(id)) pool.Add(id);
            NewSwing(true);
            Frame3DCam(); SnapCamera(new Vector3(0, 6.5f, -11f), new Vector3(0, 1.2f, 0), 46f); camSmooth = 1.6f;
        }
        static void Window(MeshKit k, Vector3 c)
        {
            Color frame = Color.white, pane = new(0.70f, 0.87f, 1f), curtain = new(1f, 0.72f, 0.80f);
            k.Box(c, new Vector3(2.0f, 2.4f, 0.12f), frame); k.Box(c + new Vector3(0, 0, -0.05f), new Vector3(1.7f, 2.1f, 0.06f), pane);
            k.Box(c + new Vector3(0, 0, -0.09f), new Vector3(0.08f, 2.1f, 0.04f), frame); k.Box(c + new Vector3(0, 0.1f, -0.09f), new Vector3(1.7f, 0.08f, 0.04f), frame);
            k.Ellipsoid(c + new Vector3(-0.55f, 0.95f, -0.15f), new Vector3(0.35f, 0.18f, 0.05f), Color.white, 10, 6);   // a little cloud in the glass
            k.Box(c + new Vector3(0, -1.28f, -0.12f), new Vector3(2.3f, 0.12f, 0.3f), frame);
            foreach (var s in new[] { -1f, 1f }) k.Ellipsoid(c + new Vector3(s * 1.15f, 0.05f, -0.18f), new Vector3(0.28f, 1.25f, 0.12f), curtain, 10, 8);
            k.Box(c + new Vector3(0, 1.3f, -0.2f), new Vector3(2.9f, 0.07f, 0.07f), new Color(0.85f, 0.65f, 0.45f));
        }
        static void Frame(MeshKit k, Vector3 c, Color art)
        {
            k.Box(c, new Vector3(1.1f, 0.9f, 0.08f), new Color(0.95f, 0.78f, 0.45f)); k.Box(c + new Vector3(0, 0, -0.05f), new Vector3(0.9f, 0.7f, 0.04f), art);
            // a paw print painting
            var paw = Color.white; k.Ellipsoid(c + new Vector3(0, -0.08f, -0.08f), new Vector3(0.14f, 0.11f, 0.02f), paw, 10, 5);
            foreach (var (dx, dy) in new[] { (-0.15f, 0.07f), (-0.05f, 0.15f), (0.05f, 0.15f), (0.15f, 0.07f) }) k.Ellipsoid(c + new Vector3(dx, dy, -0.08f), new Vector3(0.05f, 0.06f, 0.02f), paw, 8, 4);
        }
        static void Shelf(MeshKit k, Vector3 c, System.Random rnd)
        {
            k.Box(c, new Vector3(1.8f, 0.08f, 0.4f), new Color(0.88f, 0.68f, 0.50f));
            float x = -0.75f;
            for (int i = 0; i < 6 && x < 0.6f; i++)
            {
                float w = 0.1f + (float)rnd.NextDouble() * 0.08f, h = 0.32f + (float)rnd.NextDouble() * 0.16f;
                k.Box(c + new Vector3(x + w / 2, 0.04f + h / 2, 0), new Vector3(w, h, 0.28f), Scenery.Party[rnd.Next(Scenery.Party.Length)]); x += w + 0.02f;
            }
            k.Cylinder(c + new Vector3(0.7f, 0.04f, 0), 0.12f, 0.2f, new Color(0.95f, 0.65f, 0.5f), 10);
            k.Ellipsoid(c + new Vector3(0.7f, 0.36f, 0), new Vector3(0.2f, 0.18f, 0.18f), Scenery.Leaf2, 10, 6);
        }

        // ------------------------------------------------------------ the swinging cat
        void NewSwing(bool first = false)
        {
            var id = pool[next++ % pool.Count];
            swing = Cat(id, Vector3.zero, 90f + Rnd(-8f, 8f));   // all loaves face the same way: a neat tower
            if (swing == null) return;
            swing.T.SetParent(carrier, false); swing.T.localPosition = Vector3.zero; swing.Play("Bat", 0f);
            if (balloons) Object.Destroy(balloons);
            var k = new MeshKit(); var top = new Vector3(0, 0.8f, 0);
            Vector3[] at = { new(-0.42f, 1.75f, 0.05f), new(0.4f, 1.85f, -0.05f), new(0f, 2.22f, 0.1f) };
            for (int i = 0; i < 3; i++)
            {
                balloonCols[i] = Scenery.Party[Random.Range(0, Scenery.Party.Length)];
                k.Ellipsoid(at[i], new Vector3(0.36f, 0.44f, 0.36f), balloonCols[i], 16, 12);
                k.Ellipsoid(at[i] + new Vector3(-0.12f, 0.16f, -0.3f), new Vector3(0.07f, 0.1f, 0.03f), Color.Lerp(balloonCols[i], Color.white, 0.7f), 6, 4);   // shine
                k.Cone(at[i] + new Vector3(0, -0.52f, 0), 0.07f, 0.1f, balloonCols[i], 8);
                k.Stick(at[i] + new Vector3(0, -0.5f, 0), top, 0.018f, Color.white);
            }
            balloons = MakeMesh(k, "Balloons", carrier, false);
            swingT = Rnd(0, 6.28f); swing.Squash(-0.25f);
            if (!first) Sfx("sfx_pop", 0.25f, 1.6f);
        }
        public override void Down(Vector2 p)
        {
            if (swing == null || falling != null || toppling) return;
            // pop! the balloons burst and the cat drops
            if (balloons)
            {
                var c = carrier.localPosition;
                Burst(c + new Vector3(-0.42f, 1.75f, 0), 14, new[] { balloonCols[0], Color.white }, 3.2f, 0.16f, 0.8f, 0.5f);
                Burst(c + new Vector3(0.4f, 1.85f, 0), 14, new[] { balloonCols[1], Color.white }, 3.2f, 0.16f, 0.8f, 0.5f);
                Burst(c + new Vector3(0, 2.22f, 0), 14, new[] { balloonCols[2], Color.white }, 3.2f, 0.16f, 0.8f, 0.5f);
                Object.Destroy(balloons); balloons = null;
            }
            Sfx("sfx_pop", 0.7f, 1.35f); Sfx("sfx_meow" + Random.Range(1, 4), 0.35f, 1.4f);
            falling = swing; swing = null; fallV = Vector3.zero;
            var world = falling.T.position; falling.T.SetParent(Root, true); falling.T.position = world; falling.T.localRotation = Quaternion.Euler(0, falling.T.localEulerAngles.y, falling.T.localEulerAngles.z);
            falling.Squash(-0.3f); falling.Play("Sleep", 0.25f);
        }

        protected override void Tick(float dt)
        {
            if (toppling) return;
            if (bannerT > 0) { bannerT -= dt; if (bannerT <= 0) Banner = null; }
            if (swing != null)
            {
                float speed = 1.5f + stack.Count * 0.07f, amp = Mathf.Min(2.7f, 1.5f + stack.Count * 0.06f);
                swingT += dt * speed; swingX = Mathf.Sin(swingT) * amp;
                carrier.localPosition = new Vector3(swingX, TopY + 1.0f + Mathf.Sin(T * 2.3f) * 0.06f, 0);
                carrier.localRotation = Quaternion.Euler(0, 0, -Mathf.Cos(swingT) * 9f);
            }
            if (falling != null)
            {
                fallV.y -= Gravity * dt; var lp = falling.T.localPosition + fallV * dt;
                var e = falling.T.localEulerAngles; falling.T.localRotation = Quaternion.Euler(0, e.y, Mathf.LerpAngle(e.z, 0, dt * 10f));
                falling.T.localPosition = lp;
                if (lp.y <= TopY) Land(lp);
            }
        }
        void Land(Vector3 lp)
        {
            float dx = lp.x - TopX, tol = stack.Count == 0 ? 1.0f : LoafW * 0.62f;
            var p = falling; falling = null;
            if (Mathf.Abs(dx) > tol)
            {   // missed — it bounces off the edge and tumbles to the floor
                bodies.Add(new Body { p = p, v = new Vector3(Mathf.Sign(dx) * 2.4f, 2.5f, -0.6f), spin = Mathf.Sign(dx) * -420f });
                perfectRun = 0; Sfx("sfx_meow2", 0.6f, 1.25f); Shake(0.3f);
                Pop3D("Whoops!", new Vector3(lp.x, TopY + 0.8f, 0), Hex(255, 140, 90), 1.1f);
                LoseLife("Three cats rolled off!"); if (!Over) NewSwing(); return;
            }
            bool perfect = Mathf.Abs(dx) < LoafW * 0.08f;
            float x = perfect ? TopX : lp.x;
            p.T.SetParent(tower, false); p.T.localPosition = new Vector3(x, stack.Count * LoafH, 0); p.T.localRotation = Quaternion.Euler(0, p.T.localEulerAngles.y, 0);
            stack.Add(new Stacked { p = p, x = x });
            p.Squash(0.42f); for (int i = 0; i < stack.Count - 1; i++) stack[i].p.Squash(0.14f * (i + 1) / stack.Count);
            Shake(0.12f + Mathf.Min(0.2f, stack.Count * 0.01f)); Poof(new Vector3(x, TopY - LoafH + 0.05f, 0), 0.55f, 8);
            if (perfect)
            {
                perfectRun++; Score += 1 + Mathf.Min(perfectRun, 3);
                Pop3D(perfectRun > 1 ? $"Purrfect x{perfectRun}!" : "Purrfect!", new Vector3(x, TopY + 0.6f, 0), Hex(255, 110, 160), 1.15f);
                Sfx("sfx_purr", 0.6f); Sfx("sfx_coin", 0.4f, 1.2f + perfectRun * 0.08f);
                Burst(new Vector3(x, TopY, 0), 18, new[] { new Color(1f, 0.92f, 0.5f), Color.white, new Color(1f, 0.7f, 0.85f) }, 2.6f, 0.2f, -0.2f, 0.9f);
                if (perfectRun >= 3) Fx.Hearts(W2(new Vector3(x, TopY + 0.3f, 0)), 10, 2.2f);
            }
            else { perfectRun = 0; Score += 1; Sfx("sfx_place", 0.6f, 1.05f); }
            if (stack.Count % 5 == 0) { Confetti(new Vector3(-3.5f, TopY - 1f, -0.5f), 30, 0.8f); Confetti(new Vector3(3.5f, TopY - 1f, -0.5f), 30, 0.8f); Banner = $"{stack.Count} cats tall!"; BannerCol = Hex(255, 196, 60); bannerT = 1.3f; Sfx("sfx_levelup", 0.45f); }
            lean = stack.Average(s => s.x);
            if (Mathf.Abs(lean) > LoafW * 0.55f && stack.Count > 2) { Topple(); return; }
            NewSwing();
        }
        void Topple()
        {
            toppling = true; toppleAng = 0; toppleV = 0; Sfx("sfx_meow2", 0.8f, 0.85f); Shake(0.4f);
            if (swing != null) { swing.Play("Idle_Stand"); }
            Finish(Score >= Stars[0], "The tower toppled over!");
        }

        public override void Frame(float dt)
        {
            // the tower sways more the further it leans; toppling swings it right over and scatters the cats
            if (tower)
            {
                if (toppling)
                {
                    float s = Mathf.Sign(lean == 0 ? 1 : lean);
                    toppleV += s * 260f * dt; toppleAng += toppleV * dt;
                    if (Mathf.Abs(toppleAng) > 62f && stack.Count > 0)
                    {
                        foreach (var c in stack)
                        {
                            var w = c.p.T.position; float r = c.p.T.localPosition.y + 0.3f; c.p.T.SetParent(Root, true); c.p.T.position = w;
                            bodies.Add(new Body { p = c.p, v = new Vector3(s * (1.5f + r * 0.7f) + Rnd(-0.6f, 0.6f), Rnd(1f, 3f), Rnd(-1.2f, 0.4f)), spin = -s * Rnd(200f, 500f) });
                        }
                        stack.Clear(); Shake(0.7f); Poof(new Vector3(s * 1.2f, 0.2f, 0), 1.4f, 18);
                    }
                    tower.localRotation = Quaternion.Euler(0, 0, -toppleAng);
                }
                else
                {
                    float sway = stack.Count < 2 ? 0f : Mathf.Sin(T * 2.1f) * (0.6f + Mathf.Abs(lean) / LoafW * 5f) * Mathf.Min(1f, stack.Count / 6f);
                    tower.localRotation = Quaternion.Euler(0, 0, -sway);
                }
            }
            // loose cats: fly, spin, bounce on the floor, then shake it off and wander out of the room
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                var b = bodies[i]; if (b.p == null || !b.p.T) { bodies.RemoveAt(i); continue; }
                var t = b.p.T; b.t += dt;
                if (!b.settled)
                {
                    b.v.y -= Gravity * 0.8f * dt; var lp = t.localPosition + b.v * dt;
                    var e = t.localEulerAngles; t.localRotation = Quaternion.Euler(0, e.y, e.z + b.spin * dt);
                    if (lp.y <= 0f)
                    {
                        lp.y = 0; b.bounces++; Poof(new Vector3(lp.x, 0.1f, lp.z), 0.6f, 6); b.p.Squash(0.3f);
                        if (b.bounces < 2 && Mathf.Abs(b.v.y) > 2f) { b.v = new Vector3(b.v.x * 0.5f, -b.v.y * 0.35f, b.v.z * 0.5f); b.spin *= 0.4f; Sfx("sfx_tap", 0.4f, 0.8f); }
                        else
                        {
                            b.settled = true; b.t = 0; b.walk = lp.x >= 0 ? 1 : -1;
                            t.localRotation = Quaternion.Euler(0, b.walk > 0 ? 90f : -90f, 0); b.p.Play(toppling ? "Idle_Sit" : "Walk", 0.15f);
                            if (toppling) t.localRotation = Quaternion.Euler(0, 180f + Rnd(-35f, 35f), 0);
                        }
                    }
                    t.localPosition = lp;
                }
                else if (!toppling)
                {
                    t.localPosition += new Vector3(b.walk * 1.6f * dt, 0, 0);
                    if (Mathf.Abs(t.localPosition.x) > 12f) { Remove(b.p); bodies.RemoveAt(i); }
                }
            }
            // camera rises with the tower; after a topple it pulls back to show the mess
            if (toppling || Over) { camSmooth = 2.2f; camPos = new Vector3(0, 3.0f, -9f); camLook = new Vector3(0, 1.1f, 0); camFov = 42f; camRoll = 0; }
            else if (CountingDown) { Frame3DCam(); camSmooth = 1.8f; }
            else { camSmooth = 3f; Frame3DCam(); }
            if (Over && !toppling && stack.Count > 0) { float th = TopY; camPos = new Vector3(0, th * 0.55f + 1.4f, -(th * 0.75f + 5.5f)); camLook = new Vector3(0, th * 0.5f + 0.3f, 0); }
            base.Frame(dt);
        }
        void Frame3DCam()
        {
            float h = TopY; camPos = new Vector3(swingX * 0.15f, h + 1.9f, -6.2f); camLook = new Vector3(swingX * 0.1f, h + 1.05f, 0); camFov = 40f;
            camRoll = 0f;
        }
    }
}
