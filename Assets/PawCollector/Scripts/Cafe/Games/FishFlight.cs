using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Fish Flight — 3D side-scroller. Your cat hangs from a big balloon and drifts over the town's rooftops.
    /// Tap to puff up and rise; slip between brick chimneys and hanging planters, grab the fish.
    /// </summary>
    public class FishFlight : Game3D
    {
        public override string Id => "flight";
        public override string Title => "Fish Flight";
        public override string HowTo => "Tap to float up. Dodge the chimneys and grab the fish!";
        public override string IconPath => "Games/balloon";
        public override int[] Stars => new[] { 10, 25, 50 };
        public override Color TileCol => Hex(214, 236, 255);
        protected override Color Sky => new(0.64f, 0.83f, 1f);

        const float Gravity = 12f, Flap = 4.7f, MaxFall = 6.4f, ColW = 0.95f, CamY = 3.5f, CamZ = -10f, Tile = 24f;
        class Gate { public GameObject go; public float x, gapY, gap; public bool passed; public Transform fish; }
        readonly List<Gate> gates = new(); readonly List<Transform> near = new(), far = new(), clouds = new();
        Transform carrier, balloon, farRoot, cloudRoot; Puppet cat; Mesh fishMesh; Color balloonCol;
        float catX, y = 3f, vy, speed = 2.2f, spawnX, crashT = -1, puff; bool started, crashed;

        protected override void Build()
        {
            var k = new MeshKit();
            // near rooftops (recycled tiles), far skyline and clouds with parallax
            for (int i = 0; i < 3; i++) { near.Add(Rooftops(k, i, false).transform); near[i].localPosition = new Vector3(-8f + i * Tile, 0, 0); }
            farRoot = new GameObject("Far").transform; farRoot.SetParent(Root, false);
            for (int i = 0; i < 3; i++) { var t = Rooftops(k, 10 + i, true).transform; t.SetParent(farRoot, false); t.localPosition = new Vector3(-12f + i * Tile * 1.4f, 0, 0); far.Add(t); }
            cloudRoot = new GameObject("Clouds").transform; cloudRoot.SetParent(Root, false);
            for (int i = 0; i < 7; i++) { Scenery.Cloud(k, Vector3.zero, Random.Range(1.2f, 1.9f)); var c = MakeMesh(k, "Cloud", cloudRoot, false).transform; c.localPosition = new Vector3(-14f + i * 8f + Random.Range(-2f, 2f), Random.Range(7f, 10.5f), 26f); clouds.Add(c); }
            // the hero hanging from a big balloon
            carrier = new GameObject("Carrier").transform; carrier.SetParent(Root, false);
            balloonCol = Scenery.Party[Random.Range(0, Scenery.Party.Length)];
            Props3D.Balloon(k, new Vector3(0, 1.85f, 0), 0.5f, balloonCol); k.Stick(new Vector3(0, 1.25f, 0), new Vector3(0.05f, 0.75f, 0), 0.02f, Color.white);
            balloon = MakeMesh(k, "Balloon", carrier).transform;
            cat = Cat(Hero != null ? Hero.Id : "Mochi", Vector3.zero, 90f, 0.9f); cat.T.SetParent(carrier, false); cat.T.localPosition = new Vector3(0, -0.05f, 0); cat.Play("Bat", 0f);
            Props3D.Fish(k, Vector3.zero, 0.75f, new Color(1f, 0.62f, 0.32f), new Color(1f, 0.45f, 0.4f)); fishMesh = k.ToMesh();
            spawnX = 8f; Place();
            camPos = new Vector3(catX + 3f, CamY, CamZ); camLook = new Vector3(catX + 3f, CamY, 0); camFov = 40f;
            SnapCamera(new Vector3(catX + 1f, CamY + 1.5f, -6f), new Vector3(catX, 2.6f, 0), 46f); camSmooth = 1.6f;
        }
        GameObject Rooftops(MeshKit k, int seed, bool distant)
        {
            var rnd = new System.Random(seed * 31 + 5); float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float x = 0, z = distant ? 16f : 1.2f, s = distant ? 1.6f : 1f;
            Color[] walls = distant ? new[] { new Color(0.66f, 0.74f, 0.92f), new Color(0.72f, 0.70f, 0.90f), new Color(0.62f, 0.76f, 0.88f) }
                                    : new[] { new Color(1f, 0.86f, 0.74f), new Color(0.98f, 0.80f, 0.82f), new Color(0.86f, 0.84f, 1f), new Color(0.80f, 0.92f, 0.84f) };
            Color[] roofs = distant ? new[] { new Color(0.56f, 0.62f, 0.84f) } : new[] { new Color(0.86f, 0.42f, 0.38f), new Color(0.50f, 0.56f, 0.78f), new Color(0.92f, 0.62f, 0.36f) };
            while (x < Tile * (distant ? 1.4f : 1f))
            {
                float w = R(2.2f, 4f) * s, top = distant ? R(2f, 6f) : R(-0.6f, 0.15f), d = distant ? 3f : 2.6f;
                var wall = walls[rnd.Next(walls.Length)]; var roof = roofs[rnd.Next(roofs.Length)];
                k.Box(new Vector3(x + w / 2, top - 6f, z + d / 2), new Vector3(w - 0.08f, 12f, d), wall);
                k.Prism(new Vector3(x + w / 2, top, z + d / 2), w + 0.2f, d + 0.3f, R(0.6f, 1.1f) * s, roof, wall);
                for (float wy = top - 0.9f; wy > top - 4f; wy -= 1.1f * s)
                    for (float wx = x + 0.6f * s; wx < x + w - 0.5f * s; wx += 0.9f * s)
                        k.Box(new Vector3(wx, wy, z - 0.01f), new Vector3(0.42f * s, 0.55f * s, 0.04f), distant ? new Color(0.86f, 0.90f, 1f) : new Color(0.72f, 0.88f, 1f));
                x += w;
            }
            return MakeMesh(k, distant ? "Skyline" : "Rooftops", null, !distant);
        }

        public override void Down(Vector2 p)
        {
            if (crashed) return;
            started = true; vy = Flap; puff = 1f; Sfx("sfx_pop", 0.35f, 1.4f); cat.Squash(-0.18f);
        }

        void Place()
        {
            carrier.localPosition = new Vector3(catX, y, 0);
            if (!crashed) carrier.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(vy * 3.2f, -16f, 22f));
        }
        protected override void Tick(float dt)
        {
            if (crashed) return;
            if (!started) { y = 3f + Mathf.Sin(T * 3f) * 0.15f; Place(); return; }   // hover until the first tap
            speed = Mathf.Min(3.7f, 2.2f + Score * 0.02f);
            catX += speed * dt;
            vy = Mathf.Max(vy - Gravity * dt, -MaxFall); y += vy * dt;
            if (y > 4.5f) { y = 4.5f; vy = Mathf.Min(vy, 0); }
            if (spawnX < catX + HalfWidthAt(-CamZ) + 5f) Spawn();
            var body = new Vector2(catX, y + 0.42f); var head = new Vector2(catX, y + 1.85f);
            for (int i = gates.Count - 1; i >= 0; i--)
            {
                var g = gates[i];
                if (g.fish)
                {
                    g.fish.localRotation = Quaternion.Euler(0, T * 140f, Mathf.Sin(T * 5f) * 10f); g.fish.localPosition = new Vector3(g.x, g.gapY + Mathf.Sin(T * 4f + g.x) * 0.12f, 0);
                    if ((new Vector2(g.x, g.gapY) - body).magnitude < 0.85f)
                    {
                        Score += 2; Pop3D("+2", g.fish.localPosition + Vector3.up * 0.5f, Hex(255, 170, 40)); Sfx("sfx_coin", 0.45f, 1.3f);
                        Burst(g.fish.localPosition, 14, new[] { Props3D.Gold, Color.white, new Color(1f, 0.6f, 0.3f) }, 2.4f, 0.16f, 0.3f, 0.7f);
                        Object.Destroy(g.fish.gameObject); g.fish = null; cat.Squash(0.2f);
                    }
                }
                if (!g.passed && g.x < catX) { g.passed = true; Score++; Sfx("sfx_tap", 0.3f, 1.5f); }
                float lo = g.gapY - g.gap / 2, hi = g.gapY + g.gap / 2, half = ColW * 0.62f;
                if (Hit(body, 0.42f, g.x, half, lo, hi) || Hit(head, 0.42f, g.x, half, lo, hi)) { Crash(); return; }
                if (g.x < catX - HalfWidthAt(-CamZ) - 6f) { Object.Destroy(g.go); if (g.fish) Object.Destroy(g.fish.gameObject); gates.RemoveAt(i); }
            }
            if (y < 0.15f) { Crash(); return; }
            Place();
        }
        static bool Hit(Vector2 c, float r, float gx, float half, float lo, float hi)
        {
            float cx = Mathf.Clamp(c.x, gx - half, gx + half);
            if (c.y - r < lo) { float cy = Mathf.Min(c.y, lo); if ((new Vector2(cx, cy) - c).sqrMagnitude < r * r) return true; }
            if (c.y + r > hi) { float cy = Mathf.Max(c.y, hi); if ((new Vector2(cx, cy) - c).sqrMagnitude < r * r) return true; }
            return false;
        }
        void Spawn()
        {
            var g = new Gate { x = spawnX, gap = Mathf.Max(2.7f, 3.5f - Score * 0.02f) };
            g.gapY = Rnd(g.gap / 2 + 0.6f, 6.6f - g.gap / 2);
            spawnX += 2.15f * 2.2f * Rnd(0.95f, 1.1f) + 0.6f;
            var k = new MeshKit(); float lo = g.gapY - g.gap / 2, hi = g.gapY + g.gap / 2;
            Props3D.Chimney(k, new Vector3(0, -1f, 0), ColW, lo + 1f - 0.08f, ColW);
            // a hanging planter on a rope, with leaves spilling over
            k.Stick(new Vector3(0, hi + 0.55f, 0), new Vector3(0, 12f, 0), 0.04f, new Color(0.7f, 0.56f, 0.42f));
            k.Box(new Vector3(0, hi + 0.3f, 0), new Vector3(ColW * 1.15f, 0.5f, ColW * 0.9f), new Color(0.82f, 0.58f, 0.40f));
            k.Box(new Vector3(0, hi + 0.06f, 0), new Vector3(ColW * 1.2f, 0.1f, ColW * 0.95f), new Color(0.72f, 0.48f, 0.32f));
            for (int i = 0; i < 9; i++) { float lx = (i % 3 - 1) * 0.3f, ly = hi + 0.58f + (i / 3) * 0.28f; k.Ellipsoid(new Vector3(lx, ly, Rnd(-0.2f, 0.2f)), new Vector3(0.26f, 0.2f, 0.22f), i % 2 == 0 ? Scenery.Leaf1 : Scenery.Leaf2, 8, 6); }
            for (int i = 0; i < 3; i++) { float lx = (i - 1) * 0.33f; for (int j = 0; j < 2; j++) k.Ellipsoid(new Vector3(lx, hi + 0.08f - j * 0.12f, -0.36f), new Vector3(0.09f, 0.09f, 0.05f), Scenery.Leaf3, 6, 4); }
            g.go = MakeMesh(k, "Gate"); g.go.transform.localPosition = new Vector3(g.x, 0, 0);
            if (Random.value < 0.65f) { g.fish = MeshObj(fishMesh, "Fish").transform; g.fish.localPosition = new Vector3(g.x, g.gapY, 0); }
            gates.Add(g);
        }
        void Crash()
        {
            crashed = true; crashT = 0; Shake(0.8f);
            Burst(new Vector3(catX, y + 1.85f, 0), 26, new[] { balloonCol, Color.white }, 4f, 0.2f, 0.8f, 0.6f); if (balloon) balloon.gameObject.SetActive(false);
            Sfx("sfx_pop", 0.7f, 0.9f); Sfx("sfx_meow" + Random.Range(1, 4), 0.7f, 1.2f);
            Pop3D("Bonk!", new Vector3(catX, y + 1.4f, 0), Hex(240, 90, 90), 1.3f);
            Finish(Score >= Stars[0], "Bonk! The balloon popped");
        }

        public override void Frame(float dt)
        {
            if (crashed && crashT >= 0)
            {   // tumble down onto the rooftops, land with a poof and sit there dazed
                crashT += dt;
                if (y > 0.05f)
                {
                    vy -= Gravity * dt; y = Mathf.Max(0.05f, y + vy * dt); carrier.localRotation = Quaternion.Euler(0, 0, crashT * 520f);
                    if (y <= 0.05f) { Poof(new Vector3(catX, 0.1f, 0), 1f); cat.Squash(0.4f); cat.Play("Idle_Sit", 0.1f); Shake(0.3f); carrier.localRotation = Quaternion.Euler(0, -90f, 0); }
                }
                carrier.localPosition = new Vector3(catX, y, 0);
            }
            if (!started && CountingDown) { y = 3f + Mathf.Sin(Time.time * 3f) * 0.15f; Place(); }
            puff = Mathf.Max(0, puff - dt * 4f);
            if (balloon) balloon.localScale = new Vector3(1f + puff * 0.12f, 1f - puff * 0.06f + Mathf.Sin(Time.time * 2.4f) * 0.02f, 1f + puff * 0.12f);
            // recycle scenery tiles around the camera, parallax the distance
            float cx = catX + 3f;
            foreach (var t in near) if (t.localPosition.x + Tile < cx - 16f) t.localPosition += new Vector3(Tile * near.Count, 0, 0);
            farRoot.localPosition = new Vector3(cx * 0.55f, 0, 0);
            foreach (var t in far) if (farRoot.localPosition.x + t.localPosition.x + Tile * 1.4f < cx - 30f) t.localPosition += new Vector3(Tile * 1.4f * far.Count, 0, 0);
            cloudRoot.localPosition = new Vector3(cx * 0.8f, 0, 0);
            foreach (var c in clouds) if (cloudRoot.localPosition.x + c.localPosition.x < cx - 30f) c.localPosition += new Vector3(56f, 0, 0);
            if (crashed) { camPos = new Vector3(catX + 1.2f, 2.2f, -7.5f); camLook = new Vector3(catX, 1.0f, 0); camFov = 40f; camSmooth = 3f; }
            else { camSmooth = CountingDown ? 1.6f : 4f; camPos = new Vector3(cx, CamY, CamZ); camLook = new Vector3(cx, CamY, 0); camFov = 40f; }
            base.Frame(dt);
        }
    }

    /// <summary>
    /// Pounce! — 3D whack-a-mouse in the garden. Wind-up toy mice pop out of molehills; tap one and your cat leaps on it.
    /// Golden mice are worth 5. Never tap a cucumber (cats HATE cucumbers).
    /// </summary>
    public class Pounce : Game3D
    {
        public override string Id => "pounce";
        public override string Title => "Pounce!";
        public override string HowTo => "Tap the toy mice before they hide. Don't touch the cucumbers!";
        public override string IconPath => "Games/mouse";
        public override int[] Stars => new[] { 25, 45, 70 };
        public override Color TileCol => Hex(222, 242, 214);
        protected override Color Sky => new(0.70f, 0.87f, 1f);

        class Hole { public Vector3 c; public Transform item; public int kind; public float up, life; public bool hit, active; }
        readonly List<Hole> holes = new(); Mesh mouse, gold, cucumber; Puppet cat; float spawnT, returnIn;
        Vector3 home = new(0, 0, -1.9f), leapFrom, leapTo; float leapT = -1; bool returning, scaredJump;

        protected override void Build()
        {
            TimeLeft = 45f; Lives = 3; MaxLives = 3;
            var rnd = new System.Random(3); float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var k = new MeshKit();
            k.Disc(new Vector3(0, 0, 4), 40f, new Color(0.62f, 0.87f, 0.50f), new Color(0.52f, 0.80f, 0.45f), 56);
            for (int i = 0; i < 30; i++) { var p = new Vector3(R(-12, 12), 0.01f + i * 0.0003f, R(-4, 14)); var pc = i % 2 == 0 ? new Color(0.57f, 0.84f, 0.48f) : new Color(0.66f, 0.89f, 0.55f); k.Disc(p, R(0.8f, 1.6f), pc, pc, 20, R(0.6f, 0.9f)); }
            float[] xs = { -3.3f, -1.1f, 1.1f, 3.3f }; float[] zs = { 0.7f, 3.0f };
            foreach (var z in zs) foreach (var x in xs)
            {
                var c = new Vector3(x, 0, z); holes.Add(new Hole { c = c });
                k.Ellipsoid(c, new Vector3(0.82f, 0.2f, 0.66f), new Color(0.62f, 0.44f, 0.30f), 16, 8);
                k.Ellipsoid(c + new Vector3(0, 0.06f, 0), new Vector3(0.7f, 0.16f, 0.56f), new Color(0.70f, 0.50f, 0.34f), 16, 6);
                k.Disc(c + new Vector3(0, 0.205f, 0), 0.46f, new Color(0.22f, 0.15f, 0.12f), new Color(0.34f, 0.24f, 0.18f), 18, 0.8f);
                for (int i = 0; i < 4; i++) { float a = i * 1.7f + x; Scenery.Flower(k, c + new Vector3(Mathf.Cos(a) * 0.95f, 0.08f, Mathf.Sin(a) * 0.8f), 0.07f, Scenery.Petals[(i + (int)(x * 3 + 20)) % Scenery.Petals.Length], true); }
            }
            // white picket fence, bushes, trees, a little shed
            for (float x = -12f; x <= 12f; x += 0.5f) { k.Box(new Vector3(x, 0.5f, 6.5f), new Vector3(0.18f, 1f, 0.06f), Props3D.White); k.Cone(new Vector3(x, 1f, 6.5f), 0.12f, 0.15f, Props3D.White, 4); }
            k.Box(new Vector3(0, 0.75f, 6.53f), new Vector3(24f, 0.1f, 0.05f), Props3D.White); k.Box(new Vector3(0, 0.35f, 6.53f), new Vector3(24f, 0.1f, 0.05f), Props3D.White);
            for (int i = 0; i < 10; i++) Scenery.Bush(k, new Vector3(R(-11, 11), 0, R(6.9f, 8f)), R(0.9f, 1.3f), rnd);
            for (int i = 0; i < 9; i++) Scenery.Tree(k, new Vector3(-12 + i * 3f + R(-1, 1), 0, R(9, 13)), R(1.2f, 1.7f), rnd.NextDouble() < 0.4);
            k.Box(new Vector3(-6.8f, 1.1f, 8.6f), new Vector3(2.6f, 2.2f, 2f), new Color(0.80f, 0.92f, 0.84f)); k.Prism(new Vector3(-6.8f, 2.2f, 8.6f), 3f, 2.4f, 1f, new Color(0.92f, 0.48f, 0.5f), new Color(0.80f, 0.92f, 0.84f));
            k.Box(new Vector3(-6.8f, 0.7f, 7.58f), new Vector3(0.8f, 1.4f, 0.05f), new Color(0.72f, 0.5f, 0.36f));
            for (int i = 0; i < 5; i++) Scenery.Cloud(k, new Vector3(-16 + i * 8f, R(7, 9), 26f), R(1.4f, 2f));
            MakeMesh(k, "Garden");
            Props3D.Mouse(k, Vector3.zero, 1.6f, new Color(0.72f, 0.72f, 0.78f), Props3D.Pink); mouse = k.ToMesh();
            Props3D.Mouse(k, Vector3.zero, 1.6f, Props3D.Gold, new Color(1f, 0.6f, 0.4f)); gold = k.ToMesh();
            Props3D.Cucumber(k, Vector3.zero, 1.5f); cucumber = k.ToMesh();
            cat = Cat(Hero != null ? Hero.Id : "Mochi", home, 0f, 1f); cat.Play("Idle_Sit", 0f);
            camPos = new Vector3(0, 5.3f, -4.9f); camLook = new Vector3(0, 0, 1.3f); camFov = 40f;
            SnapCamera(new Vector3(0, 2.2f, -4.2f), new Vector3(0, 0.6f, 0), 44f); camSmooth = 1.6f;
            spawnT = 0.5f;
        }

        protected override void Tick(float dt)
        {
            float pace = Mathf.Lerp(1.15f, 0.62f, 1f - TimeLeft / 45f);
            spawnT -= dt;
            if (spawnT <= 0)
            {
                spawnT = pace * Rnd(0.45f, 0.8f);
                var free = holes.FindAll(h => !h.active); if (free.Count > 0)
                {
                    var h = free[Random.Range(0, free.Count)]; float r = Random.value;
                    h.kind = r < 0.08f ? 2 : r < 0.3f ? 1 : 0;   // 0 mouse, 1 cucumber, 2 golden mouse
                    h.item = MeshObj(h.kind == 1 ? cucumber : h.kind == 2 ? gold : mouse, "Pop").transform;
                    h.item.localRotation = Quaternion.Euler(0, Rnd(-28, 28), 0);
                    h.up = 0; h.life = pace * (h.kind == 2 ? 0.75f : 1.1f); h.hit = false; h.active = true; Place(h);
                    if (h.kind == 2) Burst(h.c + Vector3.up * 0.5f, 10, new[] { Props3D.Gold, Color.white }, 1.6f, 0.14f, -0.2f, 0.6f);
                }
            }
            foreach (var h in holes) if (h.active) { h.up += dt; Place(h); if (h.up > h.life + 0.25f) { Object.Destroy(h.item.gameObject); h.item = null; h.active = false; } }
        }
        void Place(Hole h)
        {
            if (!h.item) return;
            float rise = EaseOutBack(Mathf.Clamp01(h.up / 0.18f)), fall = Mathf.Clamp01((h.up - h.life) / 0.18f);
            float k = h.hit ? 1f : rise * (1 - fall);
            h.item.localPosition = h.c + new Vector3(0, -0.75f + k * 0.85f, 0);
            float wob = h.hit ? 0 : Mathf.Sin(h.up * 18f) * 6f;
            h.item.localRotation = Quaternion.Euler(0, h.item.localEulerAngles.y, wob);
            if (h.hit) { float s = Mathf.Max(0.01f, 1f - (h.up - h.life) * 3.5f); h.item.localScale = new Vector3(1.3f, 0.25f, 1.3f) * s; }   // squashed flat
        }
        public override void Down(Vector2 p)
        {
            foreach (var h in holes)
            {
                if (!h.active || h.hit || h.up > h.life) continue;
                if (!TapHits(p, h.c + Vector3.up * 0.35f, 0.7f, 60f)) continue;
                h.hit = true; h.life = h.up;
                if (h.kind == 1)
                {   // the famous cucumber jump
                    Pop3D("Eek!", h.c + Vector3.up * 1.2f, Hex(90, 170, 90), 1.2f); Shake(0.45f); cat.Squash(-0.4f); Leap(home, home, true);
                    Burst(h.c + Vector3.up * 0.3f, 12, new[] { new Color(0.4f, 0.7f, 0.3f), Color.white }, 2f, 0.15f, 0.6f, 0.5f);
                    LoseLife("The cucumber spooked your cat!");
                }
                else
                {
                    int pts = h.kind == 2 ? 5 : 1; Score += pts;
                    Pop3D("+" + pts, h.c + Vector3.up * 1.1f, h.kind == 2 ? Hex(255, 170, 30) : Hex(255, 110, 150), h.kind == 2 ? 1.3f : 1f);
                    Sfx("sfx_pop", 0.55f, h.kind == 2 ? 1.5f : 1.2f); if (h.kind == 2) Sfx("sfx_coin", 0.5f, 1.3f);
                    Burst(h.c + Vector3.up * 0.3f, h.kind == 2 ? 22 : 12, h.kind == 2 ? new[] { Props3D.Gold, Color.white } : new[] { new Color(1f, 0.9f, 0.5f), Color.white, Props3D.Pink }, 2.4f, 0.15f, 0.5f, 0.6f);
                    Leap(cat.T.localPosition, h.c + new Vector3(0, 0, -0.75f), false);
                }
                return;
            }
        }
        void Leap(Vector3 from, Vector3 to, bool scared)
        {
            leapFrom = from; leapTo = to; leapT = 0; returning = false; scaredJump = scared; returnIn = 0;
            var dir = to - from; dir.y = 0; if (dir.sqrMagnitude > 0.01f) cat.T.localRotation = Quaternion.LookRotation(dir);
            cat.Play(scared ? "Idle_Stand" : "Pounce", 0.05f, true);
        }
        public override void Frame(float dt)
        {
            if (leapT >= 0)
            {
                float dur = scaredJump ? 0.55f : returning ? 0.32f : 0.22f; leapT += dt / dur; float t = Mathf.Clamp01(leapT);
                float hgt = scaredJump ? 1.6f : returning ? 0.35f : 0.7f;
                cat.T.localPosition = Vector3.Lerp(leapFrom, leapTo, Ease(t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * hgt;
                if (t >= 1f)
                {
                    leapT = -1; cat.Squash(0.35f);
                    if (scaredJump) { scaredJump = false; cat.T.localRotation = Quaternion.identity; cat.Play("Idle_Sit", 0.2f); Poof(home + Vector3.up * 0.1f, 0.6f, 8); }
                    else if (!returning) { Poof(leapTo + Vector3.up * 0.1f, 0.6f, 8); Shake(0.12f); returnIn = 0.18f; }
                    else { cat.T.localRotation = Quaternion.identity; cat.Play("Idle_Sit", 0.2f); returning = false; }
                }
            }
            if (returnIn > 0)
            {
                returnIn -= dt;
                if (returnIn <= 0 && leapT < 0)
                {
                    leapFrom = cat.T.localPosition; leapTo = home; leapT = 0; returning = true;
                    var dir = home - leapFrom; dir.y = 0; if (dir.sqrMagnitude > 0.01f) cat.T.localRotation = Quaternion.LookRotation(dir); cat.Play("Walk", 0.1f);
                }
            }
            camSmooth = CountingDown ? 1.6f : 3f;
            base.Frame(dt);
        }
    }
}
