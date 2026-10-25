using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>A cat with the zoomies runs along the shelf knocking things off — slide the tray to catch cups and cakes.
    /// Hairballs and cucumbers must be avoided. Let a cup smash and you lose a heart.</summary>
    public class ZoomiesCatch : MiniGame
    {
        public override string Id => "zoomies";
        public override string Title => "Zoomies Catch";
        public override string HowTo => "Slide the tray to catch cups and cakes. Avoid hairballs!";
        public override string IconPath => "Games/cup";
        public override int[] Stars => new[] { 15, 35, 60 };
        public override Color Bg => Hex(255, 228, 232);
        public override Color TileCol => Hex(255, 232, 214);

        readonly List<Spr> items = new(); Spr tray, cat; float trayX, targetX, dropT, catDir = 1, catX, shelfY, fallSpeed;

        protected override void Setup()
        {
            Lives = 3; MaxLives = 3;
            Box(new Rect(0, 0, W, H * 0.72f), Hex(255, 214, 224));                        // wallpaper
            for (float x = -U; x < W; x += 1.6f * U) Box(new Rect(x, 0, 0.5f * U, H * 0.72f), Hex(255, 226, 234));
            Box(new Rect(0, H * 0.72f, W, H * 0.28f), Hex(222, 168, 120));                // wooden floor
            for (float x = 0; x < W; x += 2.2f * U) Box(new Rect(x, H * 0.72f, 0.06f * U, H * 0.28f), Hex(200, 146, 102));
            shelfY = 2.1f * U;
            Box(new Rect(W * 0.06f, shelfY, W * 0.88f, 0.32f * U), Hex(200, 140, 96), 0.12f * U, null, "gshelf");
            for (float x = W * 0.12f; x < W * 0.9f; x += 1.7f * U) Sprite(UnityEngine.Random.value < 0.6f ? G("cup") : G("cake"), new Vector2(x, shelfY - 0.45f * U), new Vector2(0.9f * U, 0.9f * U), "soft");
            cat = Sprite(CatTex(Hero), new Vector2(W * 0.2f, shelfY - 0.85f * U), new Vector2(1.5f * U, 1.5f * U), "gcat");
            catX = W * 0.2f;
            trayX = targetX = W / 2; tray = Sprite(G("tray"), new Vector2(trayX, H - 1.0f * U), new Vector2(3.8f * U, 1.1f * U));
            dropT = 0.8f; fallSpeed = 3.4f * U;
        }
        public override void Down(Vector2 p) { targetX = p.x; }
        public override void Move(Vector2 p) { targetX = p.x; }

        protected override void Tick(float dt)
        {
            // the zoomie cat dashes back and forth along the shelf
            float catSpeed = 5f * U + Score * 0.05f * U;
            catX += catDir * catSpeed * dt; if (catX > W * 0.9f) catDir = -1; if (catX < W * 0.1f) catDir = 1;
            cat.Pos = new Vector2(catX, shelfY - 0.85f * U - Mathf.Abs(Mathf.Sin(T * 14f)) * 0.25f * U); cat.Rot = catDir * 8f; cat.Sync();
            trayX = Mathf.Lerp(trayX, Mathf.Clamp(targetX, 1.5f * U, W - 1.5f * U), 1f - Mathf.Exp(-18f * dt)); tray.Pos.x = trayX; tray.Sync();
            fallSpeed = 3.4f * U + Score * 0.06f * U;
            dropT -= dt;
            if (dropT <= 0)
            {
                dropT = Mathf.Max(0.38f, 1.05f - Score * 0.012f) * Rnd(0.7f, 1.2f);
                float r = UnityEngine.Random.value; int kind = r < 0.14f ? 2 : r < 0.2f ? 3 : r < 0.48f ? 1 : 0;   // 0 cup, 1 cake, 2 hairball, 3 cucumber
                var tex = kind == 0 ? G("cup") : kind == 1 ? G("cake") : kind == 2 ? G("hairball") : G("cucumber");
                var it = Sprite(tex, new Vector2(catX, shelfY + 0.2f * U), kind == 3 ? new Vector2(1.3f * U, 0.7f * U) : new Vector2(1.05f * U, 1.05f * U));
                it.Kind = kind; it.Vel = new Vector2(Rnd(-0.6f, 0.6f) * U, -1.5f * U); items.Add(it); Sfx("sfx_tap", 0.25f, 1.6f);
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i]; it.Vel.y += 7f * U * dt; it.Vel.y = Mathf.Min(it.Vel.y, fallSpeed);
                it.Pos += it.Vel * dt; it.Rot += it.Vel.x * dt * 40f / U; it.Sync();
                bool bad = it.Kind >= 2;
                if (it.Pos.y > tray.Pos.y - 0.55f * U && it.Pos.y < tray.Pos.y + 0.2f * U && Mathf.Abs(it.Pos.x - tray.Pos.x) < 1.9f * U)
                {
                    if (bad) { Pop(it.Kind == 2 ? "Yuck!" : "Eek!", it.Pos, Hex(140, 120, 110)); LoseLife("Too many yucky catches!"); }
                    else { int pts = it.Kind == 1 ? 2 : 1; Score += pts; Pop("+" + pts, it.Pos, Hex(255, 120, 160)); Sfx("sfx_coin", 0.35f, 1.2f + 0.1f * pts); }
                    it.Kill(); items.RemoveAt(i); continue;
                }
                if (it.Pos.y > H + U)
                {
                    if (!bad) { Pop("Crash!", new Vector2(it.Pos.x, H - 1.6f * U), Hex(220, 90, 90)); LoseLife("Too many cups broke!"); }
                    it.Kill(); items.RemoveAt(i);
                }
                if (Over) return;
            }
        }
    }

    /// <summary>Stack sleepy loaf-cats into the tallest tower. Tap to drop the swinging cat; land it on the one below.
    /// A perfect landing is a "Purrfect!"; miss and it tumbles off (3 tries). Lean too far and the tower topples.</summary>
    public class CatStack : MiniGame
    {
        public override string Id => "stack";
        public override string Title => "Cat Stack";
        public override string HowTo => "Tap to drop the sleepy cat onto the tower. Keep it balanced!";
        public override string IconPath => "Games/loaf_0";
        public override int Unlock => 2;
        public override int[] Stars => new[] { 8, 15, 25 };
        public override Color Bg => Hex(255, 236, 214);
        public override Color TileCol => Hex(255, 240, 220);
        public override float CoinsPerPoint => 3f;

        VisualElement world; readonly List<Spr> stack = new(); Spr swing, falling; Spr tumble; float tumbleT;
        float lw, lh, baseY, camY, camTarget, swingT, lean; int perfectRun;

        protected override void Setup()
        {
            Lives = 3; MaxLives = 3;
            Box(new Rect(0, H * 0.8f, W, H * 0.2f), Hex(232, 190, 150));
            world = new VisualElement(); world.AddToClassList("gabs"); world.pickingMode = PickingMode.Ignore; S.Add(world);
            lw = 2.6f * U; lh = lw * 160f / 260f;
            baseY = H * 0.8f;
            var cushion = Box(new Rect(W / 2 - 1.8f * U, baseY - 0.35f * U, 3.6f * U, 0.5f * U), Hex(255, 150, 185), 0.25f * U, world, "gcushion");
            NewSwing();
        }
        float TopY => baseY - 0.35f * U - stack.Count * lh * 0.78f;
        float TopX => stack.Count == 0 ? W / 2 : stack[^1].Pos.x;
        void NewSwing()
        {
            swing = Sprite(G("loaf_" + UnityEngine.Random.Range(0, 6)), new Vector2(W / 2, 0), new Vector2(lw, lh), null, world); swingT = Rnd(0, 6.28f);
        }
        public override void Down(Vector2 p)
        {
            if (swing == null || falling != null) return;
            falling = swing; falling.Vel = Vector2.zero; swing = null; Sfx("sfx_tap", 0.4f, 0.9f);
        }
        protected override void Tick(float dt)
        {
            camY = Mathf.Lerp(camY, camTarget, 1f - Mathf.Exp(-4f * dt)); world.style.translate = new Translate(0, camY);
            float topScreen = TopY + camY;
            if (swing != null)
            {
                float speed = 1.6f + stack.Count * 0.07f, amp = Mathf.Min(W * 0.36f, 2.4f * U + stack.Count * 0.12f * U);
                swingT += dt * speed; swing.Pos = new Vector2(W / 2 + Mathf.Sin(swingT) * amp, TopY - 3.2f * U); swing.Rot = Mathf.Sin(swingT * 2f) * 6f; swing.Sync();
            }
            if (falling != null)
            {
                falling.Vel.y += 30f * U * dt; falling.Pos.y += falling.Vel.y * dt; falling.Rot = Mathf.Lerp(falling.Rot, 0, dt * 8f); falling.Sync();
                float landY = TopY - lh * 0.5f + lh * 0.22f;
                if (falling.Pos.y >= landY)
                {
                    float dx = falling.Pos.x - TopX;
                    if (Mathf.Abs(dx) > lw * 0.62f) { tumble = falling; tumble.Vel = new Vector2(Mathf.Sign(dx) * 3f * U, -2f * U); tumbleT = 0; falling = null; perfectRun = 0; LoseLife("Three cats rolled off!"); if (!Over) NewSwing(); return; }
                    falling.Pos.y = landY; falling.Rot = 0;
                    if (Mathf.Abs(dx) < lw * 0.07f) { falling.Pos.x = TopX; perfectRun++; Score += 1 + Mathf.Min(perfectRun, 3); Pop(perfectRun > 1 ? $"Purrfect x{perfectRun}!" : "Purrfect!", falling.Pos + new Vector2(0, -U), Hex(255, 110, 160), 1.1f); Sfx("sfx_purr", 0.5f); }
                    else { perfectRun = 0; Score += 1; Sfx("sfx_place", 0.5f, 1.1f); }
                    falling.Sync(); stack.Add(falling); falling = null;
                    lean = 0; foreach (var s in stack) lean += s.Pos.x - W / 2; lean /= stack.Count;   // centre of mass of the tower
                    if (Mathf.Abs(lean) > lw * 0.55f && stack.Count > 2) { Topple(); return; }
                    if (TopY + camTarget < H * 0.45f) camTarget = H * 0.45f - TopY;
                    NewSwing();
                }
            }
            if (tumble != null) { tumbleT += dt; tumble.Vel.y += 20f * U * dt; tumble.Pos += tumble.Vel * dt; tumble.Rot += Mathf.Sign(tumble.Vel.x) * 360f * dt; tumble.Sync(); if (tumbleT > 1.5f) { tumble.Kill(); tumble = null; } }
            // gentle sway of the tower that grows with the lean
            for (int i = 0; i < stack.Count; i++) { stack[i].Rot = Mathf.Sin(T * 2f) * lean / lw * 3f * i / Mathf.Max(1, stack.Count); stack[i].Sync(); }
        }
        void Topple() { Sfx("sfx_meow2", 0.7f, 0.9f); for (int i = 0; i < stack.Count; i++) { stack[i].Rot = Mathf.Sign(lean) * (10f + i * 6f); stack[i].Pos.x += Mathf.Sign(lean) * i * 0.25f * U; stack[i].Sync(); } Finish(Score >= Stars[0], "The tower toppled over!"); }
    }
}
