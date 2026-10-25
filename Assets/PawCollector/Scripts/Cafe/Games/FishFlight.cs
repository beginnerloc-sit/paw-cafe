using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>Tap to puff the balloon up; float your cat between chimneys and hanging plants over the rooftops, grabbing fish.</summary>
    public class FishFlight : MiniGame
    {
        public override string Id => "flight";
        public override string Title => "Fish Flight";
        public override string HowTo => "Tap to float up. Dodge the chimneys and grab the fish!";
        public override string IconPath => "Games/balloon";
        public override int[] Stars => new[] { 10, 25, 50 };
        public override Color Bg => Hex(196, 228, 255);
        public override Color TileCol => Hex(214, 236, 255);

        class Gate { public VisualElement top, bot, topCap, botCap; public float x, gapY, gap; public bool passed; public Spr fish; }
        readonly List<Gate> gates = new(); readonly List<Spr> clouds = new();
        VisualElement hero, sky1, sky2; Spr balloon, cat;
        float y, vy, speed, spawnT, skyX; bool started;
        float X => W * 0.28f;

        protected override void Setup()
        {
            for (int i = 0; i < 4; i++) clouds.Add(Sprite(G("cloud"), new Vector2(Rnd(0, W), Rnd(U, H * 0.45f)), new Vector2(3.2f * U, 1.6f * U), "soft"));
            float sh = H * 0.36f, sw = sh * 1024f / 360f;
            sky1 = Box(new Rect(0, H - sh, sw, sh), Color.clear); sky1.AddToClassList("gimg"); sky1.style.backgroundImage = new StyleBackground(G("skyline"));
            sky2 = Box(new Rect(sw, H - sh, sw, sh), Color.clear); sky2.AddToClassList("gimg"); sky2.style.backgroundImage = new StyleBackground(G("skyline"));
            hero = new VisualElement(); hero.AddToClassList("gabs"); hero.pickingMode = PickingMode.Ignore; S.Add(hero);
            balloon = Sprite(G("balloon"), new Vector2(0, -1.05f * U), new Vector2(1.6f * U, 1.6f * U), null, hero);
            cat = Sprite(CatTex(Hero), new Vector2(0, 0.2f * U), new Vector2(1.25f * U, 1.25f * U), "gcat", hero);
            y = H * 0.42f; vy = 0; speed = 3.1f * U; spawnT = 0.6f;
        }

        public override void Down(Vector2 p) { started = true; vy = -6.4f * U; Sfx("sfx_pop", 0.35f, 1.4f); }

        protected override void Tick(float dt)
        {
            skyX -= speed * 0.35f * dt; float sw = sky1.resolvedStyle.width; if (sw > 1 && skyX <= -sw) skyX += sw;
            sky1.style.translate = new Translate(skyX, 0); sky2.style.translate = new Translate(skyX, 0);
            foreach (var c in clouds) { c.Pos.x -= speed * 0.15f * dt; if (c.Pos.x < -2 * U) { c.Pos.x = W + 2 * U; c.Pos.y = Rnd(U, H * 0.45f); } c.Sync(); }
            if (!started) { y = H * 0.42f + Mathf.Sin(T * 3f) * 0.25f * U; Place(); return; }   // hover until the first tap

            vy += 17f * U * dt; vy = Mathf.Min(vy, 9f * U); y += vy * dt;
            speed = Mathf.Min(5.2f * U, 3.1f * U + Score * 0.03f * U);
            spawnT -= dt; if (spawnT <= 0) { spawnT = 2.15f * (3.1f * U / speed); Spawn(); }
            var me = new Vector2(X, y);
            for (int i = gates.Count - 1; i >= 0; i--)
            {
                var g = gates[i]; g.x -= speed * dt; float w = 1.3f * U;
                Place(g.top, new Rect(g.x - w / 2, -10, w, g.gapY - g.gap / 2 + 10)); Place(g.bot, new Rect(g.x - w / 2, g.gapY + g.gap / 2, w, H));
                Place(g.topCap, new Rect(g.x - w * 0.62f, g.gapY - g.gap / 2 - 0.42f * U, w * 1.24f, 0.42f * U));
                Place(g.botCap, new Rect(g.x - w * 0.62f, g.gapY + g.gap / 2, w * 1.24f, 0.42f * U));
                if (g.fish != null) { g.fish.Pos.x = g.x; g.fish.Pos.y = g.gapY + Mathf.Sin(T * 4f + g.x) * 0.15f * U; g.fish.Sync(); if ((g.fish.Pos - me).magnitude < 0.9f * U) { Score += 2; Pop("+2", g.fish.Pos, Hex(255, 170, 40)); Sfx("sfx_coin", 0.4f, 1.3f); g.fish.Kill(); g.fish = null; } }
                if (!g.passed && g.x < X) { g.passed = true; Score++; Sfx("sfx_tap", 0.3f, 1.5f); }
                // collision: cat circle vs the two columns (+ caps)
                float r = 0.5f * U;
                var topR = new Rect(g.x - w * 0.62f, -10, w * 1.24f, g.gapY - g.gap / 2 + 10); var botR = new Rect(g.x - w * 0.62f, g.gapY + g.gap / 2, w * 1.24f, H);
                if (Circle(me, r, topR) || Circle(me, r, botR)) { Crash(); return; }
                if (g.x < -2 * U) { g.top.RemoveFromHierarchy(); g.bot.RemoveFromHierarchy(); g.topCap.RemoveFromHierarchy(); g.botCap.RemoveFromHierarchy(); g.fish?.Kill(); gates.RemoveAt(i); }
            }
            if (y > H - 0.5f * U) { Crash(); return; }
            if (y < 0.9f * U) { y = 0.9f * U; vy = Mathf.Max(vy, 0); }
            Place();
        }
        static bool Circle(Vector2 c, float r, Rect rc) { var p = new Vector2(Mathf.Clamp(c.x, rc.xMin, rc.xMax), Mathf.Clamp(c.y, rc.yMin, rc.yMax)); return (p - c).sqrMagnitude < r * r; }
        void Place() { hero.style.left = X; hero.style.top = y; hero.style.rotate = new Rotate(Mathf.Clamp(vy / U * 3f, -18f, 30f)); }
        void Crash() { Sfx("sfx_meow" + UnityEngine.Random.Range(1, 4), 0.7f, 1.2f); Finish(Score >= Stars[0], "Bonk! The balloon popped"); balloon.Kill(); }

        void Spawn()
        {
            var g = new Gate { x = W + U, gap = Mathf.Max(2.7f * U, 3.6f * U - Score * 0.025f * U) };
            g.gapY = Rnd(g.gap / 2 + 1.0f * U, H - g.gap / 2 - 1.4f * U);
            g.top = Box(default, Hex(118, 186, 118), 0.25f * U); g.topCap = Box(default, Hex(96, 164, 98), 0.2f * U);   // hanging ivy from above
            g.bot = Box(default, Hex(214, 120, 96), 0.15f * U); g.botCap = Box(default, Hex(178, 92, 76), 0.12f * U);   // brick chimney
            g.bot.AddToClassList("gchimney"); g.top.AddToClassList("givy");
            if (UnityEngine.Random.value < 0.65f) g.fish = Sprite(G("fish"), new Vector2(g.x, g.gapY), new Vector2(1.0f * U, 0.62f * U));
            gates.Add(g); hero.BringToFront();
        }
    }

    /// <summary>Whack-a-mouse: toy mice pop out of holes — tap them! Golden mice are worth 5. Never tap a cucumber.</summary>
    public class Pounce : MiniGame
    {
        public override string Id => "pounce";
        public override string Title => "Pounce!";
        public override string HowTo => "Tap the toy mice before they hide. Don't touch the cucumbers!";
        public override string IconPath => "Games/mouse";
        public override int[] Stars => new[] { 25, 45, 70 };
        public override Color Bg => Hex(208, 236, 196);
        public override Color TileCol => Hex(222, 242, 214);

        class Hole { public Vector2 c; public Spr item; public float up, life; public bool hit; public VisualElement mask; }
        readonly List<Hole> holes = new(); float spawnT; Spr cat;

        protected override void Setup()
        {
            TimeLeft = 45f; Lives = 3; MaxLives = 3;
            int cols = 4, rows = 2; float gx = Mathf.Min(W * 0.17f, 2.9f * U), gy = 2.9f * U, x0 = W / 2 - gx * (cols - 1) / 2, y0 = H * 0.42f;
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
            {
                var h = new Hole { c = new Vector2(x0 + c * gx + (r == 1 ? gx * 0.0f : 0), y0 + r * gy) };
                Box(new Rect(h.c.x - 1.1f * U, h.c.y - 0.1f * U, 2.2f * U, 0.75f * U), Hex(120, 84, 60), 1.1f * U, null, "ghole");
                h.mask = Box(new Rect(h.c.x - 1.3f * U, h.c.y + 0.28f * U, 2.6f * U, 1.4f * U), Bg, 0.3f * U, null, "gmound");   // hides the bottom of the toy
                holes.Add(h);
            }
            cat = Sprite(CatTex(Hero), new Vector2(1.3f * U, H - 1.3f * U), new Vector2(1.9f * U, 1.9f * U), "gcat");
            spawnT = 0.5f;
        }
        protected override void Tick(float dt)
        {
            float pace = Mathf.Lerp(1.15f, 0.62f, 1f - TimeLeft / 45f);
            spawnT -= dt;
            if (spawnT <= 0)
            {
                spawnT = pace * Rnd(0.45f, 0.8f);
                var free = holes.FindAll(x => x.item == null); if (free.Count > 0)
                {
                    var h = free[UnityEngine.Random.Range(0, free.Count)]; float r = UnityEngine.Random.value;
                    int kind = r < 0.08f ? 2 : r < 0.3f ? 1 : 0;   // 0 mouse, 1 cucumber, 2 golden mouse
                    var tex = kind == 1 ? G("cucumber") : kind == 2 ? G("mouse_gold") : G("mouse");
                    var size = kind == 1 ? new Vector2(1.9f * U, 1.05f * U) : new Vector2(1.7f * U, 1.36f * U);
                    h.item = Sprite(tex, h.c, size); h.item.Kind = kind; h.up = 0; h.life = pace * (kind == 2 ? 0.75f : 1.1f); h.hit = false;
                    h.mask.BringToFront();
                }
            }
            foreach (var h in holes)
            {
                if (h.item == null) continue;
                h.up += dt; float rise = Mathf.Clamp01(h.up / 0.14f), fall = Mathf.Clamp01((h.up - h.life) / 0.14f);
                float k = h.hit ? 0 : rise * (1 - fall);
                h.item.Pos = h.c + new Vector2(0, (1 - k) * 1.2f * U - 0.45f * U); h.item.Scale = h.hit ? Mathf.Max(0, 1 - (h.up - h.life) * 4f) : 1f; h.item.Sync();
                if (h.up > h.life + 0.16f || (h.hit && h.item.Scale <= 0.01f)) { h.item.Kill(); h.item = null; }
            }
            cat.Scale = 1f + Mathf.Max(0, 0.25f - (T - lastPounce)) * 1.2f; cat.Sync();
        }
        float lastPounce = -9;
        public override void Down(Vector2 p)
        {
            foreach (var h in holes)
            {
                if (h.item == null || h.hit) continue; if (h.up > h.life) continue;
                if ((p - (h.c - new Vector2(0, 0.4f * U))).magnitude > 1.25f * U) continue;
                h.hit = true; h.life = h.up; lastPounce = T;
                if (h.item.Kind == 1) { Pop("Eek!", h.c, Hex(90, 170, 90)); LoseLife("The cucumber spooked your cat!"); }
                else { int pts = h.item.Kind == 2 ? 5 : 1; Score += pts; Pop("+" + pts, h.c - new Vector2(0, U), h.item.Kind == 2 ? Hex(255, 170, 30) : Hex(255, 110, 150)); Sfx("sfx_pop", 0.5f, h.item.Kind == 2 ? 1.5f : 1.2f); }
                return;
            }
        }
    }
}
