using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Rooftop Run — 3D endless runner over rainy rooftops at dusk. Tap to jump (tap again in the air for a flip
    /// double jump), swipe down to slide under laundry lines. Collect paw tokens. Stray mode: guide the stray home — 250 m.
    /// </summary>
    public class RooftopRun : Game3D
    {
        public override string Id => "rooftop";
        public override string Title => "Rooftop Run";
        public override string HowTo => "Tap to jump (tap again in the air to double jump). Swipe down to slide under laundry.";
        public override string IconPath => "Games/laundry";
        public override int Unlock => 4;
        public override int[] Stars => new[] { 150, 400, 800 };
        public override string Unit => "m";
        public override Color TileCol => Hex(214, 224, 246);
        public override float CoinsPerPoint => 0.12f;
        protected override Color Sky => Stray ? new Color(0.30f, 0.34f, 0.52f) : new Color(0.40f, 0.45f, 0.70f);
        const float Goal = 250f, Gravity = 21f, Tile = 34f;

        class Roof { public float x0, x1, y; public GameObject go; }
        class Thing { public Transform t; public int kind; public float x, y; }   // 0 paw, 1 chimney (jump), 2 laundry (slide)
        readonly List<Roof> roofs = new(); readonly List<Thing> things = new(); readonly List<Transform> far = new();
        Transform body, farRoot; Puppet cat; Mesh pawMesh; float catX, y, vy, speed = 3.6f, dist, nextX = -8f, slideT, flipT = -1, camY, endT = -1;
        int jumps, pawBonus; Vector2 downP; bool swiped, ended, almostShown; bool Stray => Mode == "stray";
        static readonly Color[] Walls = { new(0.62f, 0.56f, 0.74f), new(0.54f, 0.60f, 0.78f), new(0.70f, 0.58f, 0.66f), new(0.56f, 0.66f, 0.70f) };
        static readonly Color[] RoofCols = { new(0.80f, 0.42f, 0.38f), new(0.48f, 0.52f, 0.72f), new(0.86f, 0.58f, 0.36f) };
        static readonly Color[] Clothes = { new(1f, 0.55f, 0.65f), new(0.5f, 0.75f, 1f), new(1f, 0.85f, 0.35f), new(0.6f, 0.86f, 0.55f), Color.white };

        protected override void Build()
        {
            var k = new MeshKit();
            // distant skyline, moon and clouds (parallax)
            farRoot = new GameObject("Far").transform; farRoot.SetParent(Root, false);
            for (int i = 0; i < 3; i++) { var t = Skyline(k, i).transform; t.SetParent(farRoot, false); t.localPosition = new Vector3(-20f + i * Tile, 0, 0); far.Add(t); }
            k.Disc(Vector3.zero, 2.2f, Color.white, Color.white, 32); var moon = MakeMesh(k, "Moon", farRoot, false); moon.transform.localPosition = new Vector3(14f, 13f, 44f); moon.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            Tint(moon.GetComponent<Renderer>(), new Color(1f, 0.96f, 0.8f), true);
            for (int i = 0; i < 6; i++) { Scenery.Cloud(k, new Vector3(-30 + i * 14f, Random.Range(10f, 14f), 40f), Random.Range(2f, 3f)); }
            var cl = MakeMesh(k, "Clouds", farRoot, false); Tint(cl.GetComponent<Renderer>(), new Color(0.62f, 0.66f, 0.82f), false);
            Props3D.Paw(k, Vector3.zero, 1.6f, new Color(1f, 0.55f, 0.72f)); pawMesh = k.ToMesh();
            AddRoof(14f); while (nextX < 30f) AddRoof(0);
            body = new GameObject("Runner").transform; body.SetParent(Root, false);
            var id = Stray && Partner != null ? Partner.Id : Hero != null ? Hero.Id : "Mochi";
            cat = Cat(id, Vector3.zero, 90f, 0.85f); cat.T.SetParent(body, false); cat.T.localPosition = Vector3.zero; cat.Play("Walk", 0f);
            y = roofs[0].y; body.localPosition = new Vector3(catX, y, 0); camY = y;
            // rain that follows the camera
            var rg = new GameObject("Rain"); rg.transform.SetParent(Cam.transform, false); rg.transform.localPosition = new Vector3(0, 6f, 7f);
            var ps = rg.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.startLifetime = 1.1f; m.startSpeed = 0; m.startSize = 0.05f; m.startColor = new Color(0.78f, 0.86f, 1f, 0.55f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 1500;
            var em = ps.emission; em.rateOverTime = Stray ? 520 : 300;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(30f, 0.1f, 12f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = -2.2f; vel.y = -15f; vel.z = 0f;
            var psr = rg.GetComponent<ParticleSystemRenderer>(); psr.sharedMaterial = Fx.Mat(); psr.renderMode = ParticleSystemRenderMode.Stretch; psr.velocityScale = 0.04f; psr.lengthScale = 1f;
            ps.Play();
            CamTarget(); SnapCamera(new Vector3(catX + 1.2f, y + 1.2f, -4.4f), new Vector3(catX, y + 0.6f, 0), 44f); camSmooth = 1.6f;
        }
        GameObject Skyline(MeshKit k, int seed)
        {
            var rnd = new System.Random(seed * 17 + 3); float x = 0;
            while (x < Tile)
            {
                float w = 2f + (float)rnd.NextDouble() * 3f, top = 2f + (float)rnd.NextDouble() * 7f;
                k.Box(new Vector3(x + w / 2, top - 8f, 22f), new Vector3(w - 0.1f, 16f, 3f), new Color(0.30f, 0.33f, 0.52f));
                for (float wy = top - 1f; wy > -3f; wy -= 1.2f) for (float wx = x + 0.5f; wx < x + w - 0.4f; wx += 0.8f) if (rnd.NextDouble() < 0.45) k.Box(new Vector3(wx, wy, 20.48f), new Vector3(0.36f, 0.5f, 0.04f), new Color(1f, 0.86f, 0.52f));
                x += w;
            }
            return MakeMesh(k, "Skyline", null, false);
        }
        void AddRoof(float forceW)
        {
            float gap = roofs.Count == 0 ? 0 : Rnd(0.8f, 1.35f + Mathf.Min(0.7f, dist / 300f * 0.7f));
            float w = forceW > 0 ? forceW : Rnd(3.2f, 6.3f);
            float prevY = roofs.Count > 0 ? roofs[^1].y : 0.4f, ny = Mathf.Clamp(prevY + Rnd(-0.9f, 0.8f), 0f, 1.1f);
            var r = new Roof { x0 = nextX + gap, x1 = nextX + gap + w, y = ny };
            var k = new MeshKit(); var wall = Walls[Random.Range(0, Walls.Length)];
            k.Box(new Vector3(w / 2, -7f, 0.8f), new Vector3(w, 14f, 3.4f), wall);
            k.Box(new Vector3(w / 2, -0.09f, 0.8f), new Vector3(w + 0.24f, 0.18f, 3.6f), RoofCols[Random.Range(0, RoofCols.Length)]);
            k.Box(new Vector3(w / 2, -0.24f, -0.93f), new Vector3(w + 0.1f, 0.1f, 0.06f), new Color(0.40f, 0.36f, 0.50f));
            for (int i = 0; i < 3; i++) k.Cylinder(new Vector3(0.6f + i * (w - 1.2f) / 2f, -0.95f, -0.98f), 0.05f, 0.7f, new Color(0.45f, 0.42f, 0.5f), 6);   // little drainpipes
            r.go = MakeMesh(k, "Roof"); r.go.transform.localPosition = new Vector3(r.x0, r.y, 0);
            // glowing windows on the front
            var kw = new MeshKit();
            for (float wy = -1.1f; wy > -6f; wy -= 1.4f) for (float wx = 0.7f; wx < w - 0.5f; wx += 1.1f) if (Random.value < 0.7f) { kw.Box(new Vector3(wx, wy, -0.92f), new Vector3(0.55f, 0.72f, 0.04f), Color.white); kw.Box(new Vector3(wx, wy, -0.95f), new Vector3(0.05f, 0.72f, 0.02f), new Color(0.6f, 0.5f, 0.4f)); }
            var win = MakeMesh(kw, "Windows", r.go.transform, false); Tint(win.GetComponent<Renderer>(), new Color(1f, 0.84f, 0.5f), true);
            roofs.Add(r);
            if (forceW <= 0)
            {
                float tx = r.x0 + Rnd(1.2f, w - 1.2f), rnd = Random.value;
                if (w > 3.6f && rnd < 0.4f)
                {
                    var kc = new MeshKit(); Props3D.Chimney(kc, Vector3.zero, 0.62f, 0.62f, 0.62f);
                    var t = MakeMesh(kc, "Chimney").transform; t.localPosition = new Vector3(tx, r.y, 0); things.Add(new Thing { t = t, kind = 1, x = tx, y = r.y });
                }
                else if (w > 3.6f && rnd < 0.72f)
                {
                    var kl = new MeshKit(); var pole = new Color(0.55f, 0.5f, 0.55f);
                    kl.Cylinder(new Vector3(-1.6f, 0, 0.1f), 0.05f, 2.15f, pole, 8); kl.Cylinder(new Vector3(1.6f, 0, 0.1f), 0.05f, 2.15f, pole, 8);
                    Vector3 L(float u) => new Vector3(Mathf.Lerp(-1.6f, 1.6f, u), 2.05f - Mathf.Sin(u * Mathf.PI) * 0.18f, 0.1f);
                    for (int i = 0; i < 10; i++) kl.Stick(L(i / 10f), L((i + 1) / 10f), 0.02f, Color.white);
                    for (int i = 0; i < 5; i++)
                    {
                        float u = 0.12f + i * 0.19f; var top = L(u); var col = Clothes[(i + Random.Range(0, 5)) % Clothes.Length];
                        if (i % 2 == 0) { kl.Box(top + new Vector3(0, -0.42f, 0), new Vector3(0.52f, 0.8f, 0.06f), col); kl.Box(top + new Vector3(-0.34f, -0.15f, 0), new Vector3(0.2f, 0.3f, 0.06f), col); kl.Box(top + new Vector3(0.34f, -0.15f, 0), new Vector3(0.2f, 0.3f, 0.06f), col); }
                        else kl.Box(top + new Vector3(0, -0.72f, 0), new Vector3(0.42f, 1.4f, 0.05f), col);   // a long towel
                        kl.Box(top + new Vector3(0, 0.02f, -0.04f), new Vector3(0.05f, 0.1f, 0.04f), new Color(0.9f, 0.75f, 0.5f));   // peg
                    }
                    var t = MakeMesh(kl, "Laundry").transform; t.localPosition = new Vector3(tx, r.y, 0); things.Add(new Thing { t = t, kind = 2, x = tx, y = r.y });
                }
                for (int i = 0; i < 3; i++)
                {
                    float px = r.x0 + w * (0.2f + 0.25f * i), py = r.y + Rnd(0.9f, 1.8f);
                    var t = MeshObj(pawMesh, "Paw", null, false).transform; t.localPosition = new Vector3(px, py, 0); things.Add(new Thing { t = t, kind = 0, x = px, y = py });
                }
            }
            nextX = r.x1;
        }
        public override void Down(Vector2 p)
        {
            if (ended) return;
            downP = p; swiped = false;
            if (jumps < 2)
            {
                vy = jumps == 0 ? 7.35f : 6.4f; jumps++; slideT = 0;
                if (jumps == 1) { cat.Play("Pounce", 0.05f, true); cat.Squash(-0.3f); Poof(new Vector3(catX, y + 0.05f, 0), 0.4f, 5); }
                else { flipT = 0; cat.Squash(-0.2f); Burst(new Vector3(catX, y + 0.4f, 0), 8, new[] { Color.white, new Color(0.7f, 0.85f, 1f) }, 1.6f, 0.12f, 0.2f, 0.5f); }
                Sfx("sfx_tap", 0.4f, jumps == 1 ? 1.2f : 1.5f);
            }
        }
        public override void Move(Vector2 p)
        {
            if (ended || swiped || p.y - downP.y < 60f) return;
            swiped = true; slideT = 0.7f; if (jumps > 0) vy = Mathf.Min(vy, -8.4f); Sfx("sfx_pop", 0.3f, 0.8f);
            if (jumps == 0) { cat.Play("Sleep", 0.06f); Poof(new Vector3(catX, y + 0.05f, 0), 0.5f, 6); }
        }
        protected override void Tick(float dt)
        {
            if (ended) return;
            speed = Mathf.Min(6.6f, 3.6f + dist * 0.0056f);
            float dx = speed * dt; catX += dx; dist += dx * 1.71f;
            while (nextX < catX + HalfWidthAt(9f) + 12f) AddRoof(0);
            for (int i = roofs.Count - 1; i >= 0; i--) if (roofs[i].x1 < catX - 20f) { Object.Destroy(roofs[i].go); roofs.RemoveAt(i); }
            // physics
            slideT = Mathf.Max(0, slideT - dt);
            if (slideT <= 0 && cat.Clip == "Sleep" && jumps == 0) cat.Play("Walk", 0.1f);
            float py = y; vy -= Gravity * dt; float ny = y + vy * dt;
            Roof under = null; foreach (var r in roofs) if (catX > r.x0 - 0.18f && catX < r.x1 + 0.18f) { under = r; break; }
            if (under != null && vy <= 0 && py >= under.y - 0.12f && ny <= under.y)
            {
                ny = under.y; if (jumps > 0) { cat.Squash(0.32f); Poof(new Vector3(catX, ny + 0.05f, 0), 0.45f, 6); cat.Play(slideT > 0 ? "Sleep" : "Walk", 0.08f); flipT = -1; body.localRotation = Quaternion.identity; }
                vy = 0; jumps = 0;
            }
            y = ny;
            if (jumps == 0 && cat.Clip == "Walk") cat.Freeze(false, 1.6f + speed * 0.12f);
            body.localPosition = new Vector3(catX, y, 0);
            if (y < -3.2f) { Fall(); return; }
            // things
            bool sliding = slideT > 0 && jumps == 0; float hgt = sliding ? 0.4f : 0.8f;
            var me = new Rect(catX - 0.32f, y, 0.64f, hgt);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (t.kind == 0) { t.t.localPosition = new Vector3(t.x, t.y + Mathf.Sin(T * 5f + t.x) * 0.08f, 0); t.t.localRotation = Quaternion.Euler(0, T * 160f + t.x * 20f, 0); }
                if (t.x < catX - 20f) { Object.Destroy(t.t.gameObject); things.RemoveAt(i); continue; }
                Rect hit = t.kind == 0 ? new Rect(t.x - 0.3f, t.y - 0.3f, 0.6f, 0.6f) : t.kind == 1 ? new Rect(t.x - 0.32f, t.y, 0.64f, 0.66f) : new Rect(t.x - 1.5f, t.y + 0.55f, 3.0f, 1.2f);
                if (!me.Overlaps(hit)) continue;
                if (t.kind == 0)
                {
                    pawBonus += 5; Pop3D("+5", t.t.localPosition + Vector3.up * 0.4f, Hex(255, 120, 160)); Sfx("sfx_coin", 0.35f, 1.4f);
                    Burst(t.t.localPosition, 10, new[] { new Color(1f, 0.6f, 0.75f), Color.white }, 2f, 0.12f, 0.2f, 0.5f); Object.Destroy(t.t.gameObject); things.RemoveAt(i); continue;
                }
                Bump(t.kind == 1 ? "Bonk — a chimney!" : "Tangled in the laundry!", t); return;
            }
            Score = Mathf.RoundToInt(dist) + pawBonus;
            if (Stray && !almostShown && dist >= Goal - 50f) { almostShown = true; Banner = "Almost home!"; BannerCol = Hex(255, 214, 90); bannerT = 1.5f; }
            if (bannerT > 0) { bannerT -= dt; if (bannerT <= 0) Banner = null; }
            if (Stray && dist >= Goal)
            {
                ended = true; endT = 0; Sfx("sfx_levelup", 0.6f); cat.Play("Happy", 0.1f, true); Confetti(new Vector3(catX + 1.5f, y, -0.5f), 60); Confetti(new Vector3(catX - 1.5f, y, -0.5f), 60);
                Finish(true, "Home safe and dry!");
            }
        }
        float bannerT;
        void Fall() { ended = true; endT = 0; Sfx("sfx_meow3", 0.6f, 1.2f); Finish(!Stray && Score >= Stars[0], Stray ? "Splash! The stray ran back into the rain" : "Oops, missed the roof!"); }
        void Bump(string why, Thing t)
        {
            ended = true; endT = 0; vy = 3.5f; Sfx("sfx_error", 0.6f); Shake(0.7f); cat.Squash(0.4f);
            Pop3D("Bonk!", new Vector3(catX, y + 1.3f, 0), Hex(240, 90, 90), 1.3f); Poof(new Vector3(catX + 0.3f, y + 0.5f, 0), 0.8f, 10);
            if (t.kind == 2) t.t.localRotation = Quaternion.Euler(0, 0, 8f);
            Finish(!Stray && Score >= Stars[0], Stray ? "The stray got spooked and ran off" : why);
        }
        void CamTarget()
        {
            camPos = new Vector3(catX + 2.8f, camY + 2.1f, -8.8f); camLook = new Vector3(catX + 2.8f, camY + 1.0f, 0); camFov = 40f;
        }
        public override void Frame(float dt)
        {
            if (flipT >= 0) { flipT += dt / 0.42f; body.localRotation = Quaternion.Euler(0, 0, -360f * Ease(Mathf.Clamp01(flipT))); if (flipT >= 1) { flipT = -1; body.localRotation = Quaternion.identity; } }
            if (ended && endT >= 0)
            {   // knocked back and tumbling (or falling between the roofs)
                endT += dt;
                if (!Won) { vy -= Gravity * dt; y += vy * dt; catX -= dt * 1.2f * Mathf.Max(0, 1 - endT); body.localPosition = new Vector3(catX, Mathf.Max(y, -6f), 0); body.localRotation = Quaternion.Euler(0, 0, endT * 300f); }
            }
            Roof under = null; foreach (var r in roofs) if (catX > r.x0 - 0.5f && catX < r.x1 + 0.5f) { under = r; break; }
            float targetY = under != null ? under.y : Mathf.Max(y, -1.5f);
            camY = Damp(camY, targetY, 2.5f, dt);
            CamTarget(); camSmooth = CountingDown ? 1.6f : ended ? 2.5f : 6f;
            if (ended && !Won) { camLook = new Vector3(catX + 1f, camY + 0.6f, 0); camPos = new Vector3(catX + 1.5f, camY + 1.6f, -6.5f); }
            if (ended && Won) { camPos = new Vector3(catX + 0.6f, y + 1.2f, -3.6f); camLook = new Vector3(catX, y + 0.5f, 0); }
            farRoot.localPosition = new Vector3(catX * 0.6f, camY * 0.3f, 0);
            foreach (var t in far) if (farRoot.localPosition.x + t.localPosition.x + Tile < catX - 30f) t.localPosition += new Vector3(Tile * far.Count, 0, 0);
            base.Frame(dt);
        }
    }
}
