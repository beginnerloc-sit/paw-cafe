using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>Red Light, Green Light with cats. Hold to tiptoe toward the giant watcher cat while it looks away;
    /// freeze the moment it turns around. Move while it's watching and you're caught! Rival cats race too.
    /// Stray mode: the watcher is the shy stray — reach it without startling it.</summary>
    public class RedLightGreenLight : MiniGame
    {
        public override string Id => "redlight";
        public override string Title => "Red Light, Green Light";
        public override string HowTo => "Hold to sneak forward on green. Let go on red — don't get caught!";
        public override string IconPath => "Games/toystar";
        public override int Unlock => 2;
        public override int[] Stars => new[] { 100, 250, 350 };
        public override Color Bg => Hex(214, 240, 200);
        public override Color TileCol => Hex(255, 226, 214);
        public override float CoinsPerPoint => 0.12f;

        class Runner { public Spr s; public float x, speed, react, fade; public bool caught, done, moving; public float lane; }
        readonly List<Runner> rivals = new(); Runner me;
        Spr watcher; VisualElement finish, track, light; Label lightL;
        enum Phase { Green, Turning, Red }
        Phase phase; float phaseT, phaseLen, startX, finishX; bool holding; float caughtT = -1;
        bool Stray => Mode == "stray";

        protected override void Setup()
        {
            TimeLeft = Stray ? 45f : 40f;
            Box(new Rect(0, H * 0.32f, W, H * 0.68f), Hex(196, 232, 176));
            Box(new Rect(0, H * 0.32f, W, 0.12f * U), Hex(170, 214, 150));
            startX = W * 0.1f; finishX = W * 0.74f;
            track = Box(new Rect(startX - 0.6f * U, H * 0.4f, finishX - startX + 1.2f * U, H * 0.5f), Hex(236, 222, 190), 0.6f * U);
            finish = Box(new Rect(finishX, H * 0.38f, 0.22f * U, H * 0.54f), Hex(255, 120, 160), 0.1f * U, null, "gfinish");
            // the watcher on its stump at the far right
            Box(new Rect(W * 0.84f - 1.4f * U, H * 0.5f, 2.8f * U, H * 0.36f), Hex(170, 120, 86), 0.4f * U);
            watcher = Sprite(Stray && Partner != null ? CatTex(Partner) : Tex("Icons/cat_Midnight"), new Vector2(W * 0.84f, H * 0.42f), new Vector2(3.6f * U, 3.6f * U), "gcat");
            light = Box(new Rect(W / 2 - 3f * U, 0.95f * U, 6f * U, 1.0f * U), Hex(120, 210, 140), 0.5f * U, null, "glight");
            lightL = new Label("Green light!"); lightL.AddToClassList("glight-txt"); lightL.pickingMode = PickingMode.Ignore; light.Add(lightL);
            float laneTop = H * 0.48f, laneH = (H * 0.84f - laneTop);
            int n = Stray ? 0 : 3; int meLane = Stray ? 0 : 1;
            var pool = new List<CatDef>(GameData.Cats); pool.RemoveAll(c => Hero != null && c.Id == Hero.Id);
            for (int i = 0; i <= n; i++)
            {
                float laneY = Stray ? H * 0.66f : laneTop + laneH * i / n;
                var r = new Runner { x = startX, lane = laneY };
                bool isMe = i == meLane;
                var def = isMe ? Hero : pool[UnityEngine.Random.Range(0, pool.Count)];
                r.s = Sprite(CatTex(def), new Vector2(r.x, laneY), new Vector2((isMe ? 1.35f : 1.1f) * U, (isMe ? 1.35f : 1.1f) * U), isMe ? "gcat gme" : "gcat");
                r.speed = Rnd(0.85f, 1.15f) * U; r.react = Rnd(0.05f, 0.45f);
                if (isMe) me = r; else rivals.Add(r);
            }
            me.s.E.BringToFront();
            SetPhase(Phase.Green);
        }
        void SetPhase(Phase p)
        {
            phase = p; phaseT = 0;
            float hurry = Mathf.Clamp01(T / 30f);
            phaseLen = p == Phase.Green ? Rnd(1.3f, 3.3f) * (1f - 0.3f * hurry) : p == Phase.Turning ? 0.42f : Rnd(1.3f, 2.6f);
            bool watching = p == Phase.Red;
            light.style.backgroundColor = p == Phase.Green ? Hex(120, 210, 140) : p == Phase.Turning ? Hex(255, 200, 90) : Hex(255, 110, 120);
            lightL.text = p == Phase.Green ? "Green light!" : p == Phase.Turning ? "Red li…" : "Red light!";
            // looking away = a dark silhouette (the back of its head); watching = its face, glaring
            watcher.Tint(watching ? Color.white : new Color(0.32f, 0.27f, 0.3f));
            if (p == Phase.Red) Sfx("sfx_meow" + UnityEngine.Random.Range(1, 4), 0.7f, Stray ? 1.3f : 0.75f);
            if (p == Phase.Green) Sfx("sfx_tap", 0.3f, 1.5f);
        }
        public override void Down(Vector2 p) { holding = true; }
        public override void Up(Vector2 p) { holding = false; }

        protected override void Tick(float dt)
        {
            if (caughtT >= 0) { caughtT += dt; me.s.Scale = 1f + Mathf.Sin(caughtT * 30f) * 0.08f; me.s.Sync(); if (caughtT > 1.0f) Finish(false, Stray ? "Oh no — the stray ran off scared!" : "Caught moving on red!"); return; }
            phaseT += dt;
            float turnK = phase == Phase.Turning ? phaseT / phaseLen : phase == Phase.Red ? 1 : 0;
            watcher.Scale = 1f + (phase == Phase.Turning ? Mathf.Sin(turnK * Mathf.PI) * 0.12f : 0f); watcher.Rot = phase == Phase.Turning ? Mathf.Sin(turnK * Mathf.PI * 2) * 8f : 0; watcher.Sync();
            if (phaseT >= phaseLen) SetPhase(phase == Phase.Green ? Phase.Turning : phase == Phase.Turning ? Phase.Red : Phase.Green);
            bool red = phase == Phase.Red;
            // me
            if (!me.done)
            {
                float v = holding ? me.speed * 1.25f : 0f;
                me.x += v * dt; me.moving = v > 0;
                if (red && me.moving && phaseT > 0.18f) { caughtT = 0; Pop("Caught!", me.s.Pos - new Vector2(0, U), Hex(230, 70, 90), 1.3f); Sfx("sfx_error", 0.7f); return; }
                if (me.x >= finishX) { me.done = true; Score = 100 + Mathf.RoundToInt(TimeLeft * 7f); Sfx("sfx_levelup", 0.6f); Finish(true, Stray ? "You made it — the stray sniffs your paw!" : "You made it across!"); }
            }
            float bob = me.moving ? Mathf.Abs(Mathf.Sin(T * 16f)) * 0.18f * U : 0;
            me.s.Pos = new Vector2(me.x, me.lane - bob); me.s.Rot = me.moving ? Mathf.Sin(T * 16f) * 6f : 0; me.s.Sync();
            // rivals: they tiptoe on green, mostly freeze on red — sometimes too late
            foreach (var r in rivals)
            {
                if (r.caught) { r.fade += dt; r.s.E.style.opacity = Mathf.Max(0, 1 - r.fade); r.s.Rot += 400 * dt; r.s.Sync(); continue; }
                if (r.done) continue;
                bool go = phase == Phase.Green || (phase == Phase.Turning && phaseT < r.react) || (red && phaseT < r.react * 0.5f);
                if (go) r.x += r.speed * dt;
                if (red && go && phaseT > 0.2f && UnityEngine.Random.value < 0.5f) { r.caught = true; Pop("Caught!", r.s.Pos - new Vector2(0, U), Hex(230, 70, 90)); continue; }
                if (r.x >= finishX) { r.done = true; }
                r.s.Pos = new Vector2(r.x, r.lane - (go ? Mathf.Abs(Mathf.Sin(T * 14f + r.lane)) * 0.15f * U : 0)); r.s.Sync();
            }
            if (!me.done) Score = Mathf.RoundToInt(Mathf.Clamp01((me.x - startX) / (finishX - startX)) * 99f);
            Banner = null;
        }
        protected override void TimeUp() => Finish(false, "Time's up!");
    }

    /// <summary>Endless rooftop runner in the rain. Tap to jump (twice for a double jump), swipe down to slide under
    /// laundry lines. Collect paw prints. Stray mode: guide the stray home — reach 250 m.</summary>
    public class RooftopRun : MiniGame
    {
        public override string Id => "rooftop";
        public override string Title => "Rooftop Run";
        public override string HowTo => "Tap to jump (tap again in the air to double jump). Swipe down to slide under laundry.";
        public override string IconPath => "Games/laundry";
        public override int Unlock => 4;
        public override int[] Stars => new[] { 150, 400, 800 };
        public override string Unit => "m";
        public override Color Bg => Hex(176, 196, 226);
        public override Color TileCol => Hex(214, 224, 246);
        public override float CoinsPerPoint => 0.12f;
        const float Goal = 250f;

        class Roof { public VisualElement top, wall; public float x, w, y; }
        class Thing { public Spr s; public int kind; public float x, y; }   // 0 paw, 1 chimney (jump), 2 laundry (slide)
        readonly List<Roof> roofs = new(); readonly List<Thing> things = new(); readonly List<Spr> rain = new();
        VisualElement sky1, sky2; Spr cat; float catY, vy, speed, dist, skyX, slideT, nextX; int jumps; Vector2 downP; bool swiped;
        float CatX => W * 0.25f;
        bool Stray => Mode == "stray";

        protected override void Setup()
        {
            float sh = H * 0.45f, sw = sh * 1024f / 360f;
            sky1 = Box(new Rect(0, H * 0.22f, sw, sh), Color.clear); sky1.AddToClassList("gimg"); sky1.style.backgroundImage = new StyleBackground(G("skyline")); sky1.style.opacity = 0.55f;
            sky2 = Box(new Rect(sw, H * 0.22f, sw, sh), Color.clear); sky2.AddToClassList("gimg"); sky2.style.backgroundImage = new StyleBackground(G("skyline")); sky2.style.opacity = 0.55f;
            int drops = Stray ? 70 : 40;
            for (int i = 0; i < drops; i++) { var d = Sprite(null, new Vector2(Rnd(0, W), Rnd(0, H)), new Vector2(0.05f * U, 0.42f * U), "grain"); rain.Add(d); }
            speed = 5.2f * U; nextX = 0;
            while (nextX < W * 1.6f) AddRoof(nextX == 0 ? W * 0.9f : 0);
            cat = Sprite(CatTex(Stray && Partner != null ? Partner : Hero), new Vector2(CatX, 0), new Vector2(1.3f * U, 1.3f * U), "gcat");
            catY = roofs[0].y - 0.62f * U;
        }
        void AddRoof(float forceW)
        {
            float gap = nextX == 0 ? 0 : Rnd(1.1f, 1.9f + Mathf.Min(1f, dist / 300f)) * U;
            float w = forceW > 0 ? forceW : Rnd(4.5f, 9f) * U;
            float y = H * Rnd(0.62f, 0.78f);
            var r = new Roof { x = nextX + gap, w = w, y = y };
            r.wall = Box(default, Hex(196, 170, 196), 0.1f * U, null, "gwall"); r.top = Box(default, Hex(214, 110, 96), 0.12f * U, null, "groof");
            roofs.Add(r);
            // things on the roof
            if (nextX > 0)
            {
                float tx = r.x + Rnd(1.5f, w - 1.5f * U); float rnd = UnityEngine.Random.value;
                if (w > 4f * U && rnd < 0.4f) things.Add(new Thing { kind = 1, x = tx, y = y, s = Sprite(null, default, new Vector2(0.75f * U, 0.95f * U), "gchimney2") });
                else if (w > 4f * U && rnd < 0.72f) things.Add(new Thing { kind = 2, x = tx, y = y, s = Sprite(G("laundry"), default, new Vector2(3.4f * U, 1.5f * U)) });
                for (int k = 0; k < 3; k++) things.Add(new Thing { kind = 0, x = r.x + w * (0.2f + 0.25f * k), y = y - Rnd(0.9f, 1.8f) * U, s = Sprite(Tex("Icons/ui_paw"), default, new Vector2(0.6f * U, 0.6f * U), "gpaw") });
            }
            nextX = r.x + w;
            cat?.E.BringToFront();
        }
        public override void Down(Vector2 p)
        {
            downP = p; swiped = false;
            if (jumps < 2) { vy = (jumps == 0 ? -10.5f : -9f) * U; jumps++; slideT = 0; Sfx("sfx_tap", 0.4f, jumps == 1 ? 1.2f : 1.5f); }
        }
        public override void Move(Vector2 p)
        {
            if (!swiped && p.y - downP.y > 0.9f * U) { swiped = true; slideT = 0.7f; if (jumps > 0) vy = Mathf.Max(vy, 12f * U); Sfx("sfx_pop", 0.3f, 0.8f); }
        }
        protected override void Tick(float dt)
        {
            speed = Mathf.Min(9.5f * U, 5.2f * U + dist * 0.008f * U);
            float dx = speed * dt; dist += dx / U * 1.2f;
            skyX -= dx * 0.2f; float sw = sky1.resolvedStyle.width; if (sw > 1 && skyX <= -sw) skyX += sw;
            sky1.style.translate = new Translate(skyX, 0); sky2.style.translate = new Translate(skyX, 0);
            foreach (var d in rain) { d.Pos += new Vector2(-2f * U, 14f * U) * dt; if (d.Pos.y > H) { d.Pos.y = -U; d.Pos.x = Rnd(0, W * 1.2f); } d.Sync(); }
            foreach (var r in roofs) r.x -= dx; foreach (var t in things) t.x -= dx;
            nextX -= dx; while (nextX < W * 1.6f) AddRoof(0);
            for (int i = roofs.Count - 1; i >= 0; i--)
            {
                var r = roofs[i];
                if (r.x + r.w < -U) { r.top.RemoveFromHierarchy(); r.wall.RemoveFromHierarchy(); roofs.RemoveAt(i); continue; }
                Place(r.top, new Rect(r.x - 0.15f * U, r.y, r.w + 0.3f * U, 0.42f * U)); Place(r.wall, new Rect(r.x, r.y + 0.3f * U, r.w, H));
            }
            // cat physics
            slideT = Mathf.Max(0, slideT - dt);
            vy += 30f * U * dt; float ny = catY + vy * dt;
            Roof under = null; foreach (var r in roofs) if (CatX > r.x - 0.25f * U && CatX < r.x + r.w + 0.25f * U) { under = r; break; }
            float feet = 0.62f * U;
            if (under != null && vy >= 0 && catY + feet <= under.y + 0.15f * U && ny + feet >= under.y) { ny = under.y - feet; vy = 0; jumps = 0; }
            catY = ny;
            bool sliding = slideT > 0 && jumps == 0;
            cat.Pos = new Vector2(CatX, catY + (sliding ? 0.3f * U : -(jumps == 0 ? Mathf.Abs(Mathf.Sin(T * 18f)) * 0.12f * U : 0)));
            cat.Size = sliding ? new Vector2(1.6f * U, 0.75f * U) : new Vector2(1.3f * U, 1.3f * U); cat.Rot = jumps > 0 ? -12f : sliding ? 0 : Mathf.Sin(T * 18f) * 4f; cat.Sync();
            if (catY > H + U) { Fall(); return; }
            // things
            var me = new Rect(CatX - 0.45f * U, cat.Pos.y - cat.Size.y * 0.4f, 0.9f * U, cat.Size.y * 0.8f);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                var t = things[i];
                if (t.kind == 0) t.s.Pos = new Vector2(t.x, t.y + Mathf.Sin(T * 5f + t.x) * 0.08f * U);
                else if (t.kind == 1) t.s.Pos = new Vector2(t.x, t.y - 0.42f * U);
                else t.s.Pos = new Vector2(t.x, t.y - 1.35f * U);
                t.s.Sync();
                if (t.x < -3f * U) { t.s.Kill(); things.RemoveAt(i); continue; }
                Rect hit = t.kind == 0 ? new Rect(t.x - 0.3f * U, t.s.Pos.y - 0.3f * U, 0.6f * U, 0.6f * U)
                         : t.kind == 1 ? new Rect(t.x - 0.32f * U, t.y - 0.85f * U, 0.64f * U, 0.85f * U)
                         : new Rect(t.x - 1.5f * U, t.y - 2.0f * U, 3.0f * U, 1.25f * U);   // laundry: only the low-hanging clothes, slide under them
                if (!me.Overlaps(hit)) continue;
                if (t.kind == 0) { pawBonus += 5; Pop("+5", t.s.Pos, Hex(255, 120, 160)); Sfx("sfx_coin", 0.3f, 1.4f); t.s.Kill(); things.RemoveAt(i); continue; }
                Bump(t.kind == 1 ? "Bonk — a chimney!" : "Tangled in the laundry!"); return;
            }
            Score = Mathf.RoundToInt(dist) + pawBonus;
            if (Stray && dist >= Goal) { Sfx("sfx_levelup", 0.6f); Finish(true, "Home safe and dry!"); }
        }
        int pawBonus;
        void Fall() { Sfx("sfx_meow3", 0.6f, 1.2f); Finish(!Stray && Score >= Stars[0], Stray ? "Splash! The stray ran back into the rain" : "Oops, missed the roof!"); }
        void Bump(string why) { Sfx("sfx_error", 0.6f); Finish(!Stray && Score >= Stars[0], Stray ? "The stray got spooked and ran off" : why); }
    }
}
