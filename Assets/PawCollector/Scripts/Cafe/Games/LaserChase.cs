using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>Drag the laser dot — your cat chases it. Lead it to the toy stars, but don't let it crash into the vases.</summary>
    public class LaserChase : MiniGame
    {
        public override string Id => "laser";
        public override string Title => "Laser Chase";
        public override string HowTo => "Drag the laser dot. Your cat chases it — collect stars, dodge the vases!";
        public override string IconPath => "Games/laser";
        public override int Unlock => 3;
        public override int[] Stars => new[] { 10, 20, 32 };
        public override Color Bg => Hex(236, 206, 166);
        public override Color TileCol => Hex(255, 222, 222);
        public override float CoinsPerPoint => 2f;

        Spr cat, dot, star; readonly List<Spr> vases = new(); Vector2 catV, laser; float hurtT; Rect field;

        protected override void Setup()
        {
            TimeLeft = 60f; Lives = 3; MaxLives = 3;
            field = new Rect(W * 0.08f, 1.6f * U, W * 0.84f, H - 2.2f * U);
            Box(field, Hex(246, 222, 186), 0.6f * U, null, "gfloor");
            for (float y = field.y + U; y < field.yMax; y += 1.1f * U) Box(new Rect(field.x + 0.3f * U, y, field.width - 0.6f * U, 0.05f * U), Hex(226, 196, 156));
            Box(new Rect(W / 2 - 2.6f * U, H * 0.5f - 1.6f * U, 5.2f * U, 3.2f * U), Hex(255, 190, 210, 0.7f), 1.6f * U);   // rug
            laser = field.center;
            cat = Sprite(CatTex(Hero), field.center + new Vector2(0, U), new Vector2(1.3f * U, 1.3f * U), "gcat");
            for (int i = 0; i < 4; i++) vases.Add(Sprite(G("vase"), FreeSpot(1.8f), new Vector2(0.85f * U, 1.15f * U)));
            star = Sprite(G("toystar"), FreeSpot(1.6f), new Vector2(0.95f * U, 0.95f * U));
            dot = Sprite(G("laser"), laser, new Vector2(1.0f * U, 1.0f * U));
        }
        Vector2 FreeSpot(float clear)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                var p = new Vector2(Rnd(field.x + U, field.xMax - U), Rnd(field.y + U, field.yMax - U)); bool ok = (cat == null || (p - cat.Pos).magnitude > 2.5f * U);
                foreach (var v in vases) if ((v.Pos - p).magnitude < clear * U) ok = false;
                if (star != null && (star.Pos - p).magnitude < clear * U) ok = false;
                if (ok) return p;
            }
            return field.center;
        }
        public override void Down(Vector2 p) { laser = Clamp(p); }
        public override void Move(Vector2 p) { laser = Clamp(p); }
        Vector2 Clamp(Vector2 p) => new(Mathf.Clamp(p.x, field.x + 0.3f * U, field.xMax - 0.3f * U), Mathf.Clamp(p.y, field.y + 0.3f * U, field.yMax - 0.3f * U));

        protected override void Tick(float dt)
        {
            dot.Pos = laser + new Vector2(Mathf.Sin(T * 37f), Mathf.Cos(T * 41f)) * 0.04f * U; dot.Scale = 1f + Mathf.Sin(T * 12f) * 0.08f; dot.Sync();
            hurtT = Mathf.Max(0, hurtT - dt);
            // the cat accelerates toward the dot with a little lag and a top speed (it overshoots on sharp turns)
            var to = laser - cat.Pos; float maxV = (4.6f + Score * 0.06f) * U;
            catV += to.normalized * Mathf.Min(to.magnitude * 9f, 16f * U) * dt; catV *= Mathf.Exp(-2.2f * dt);
            if (catV.magnitude > maxV) catV = catV.normalized * maxV;
            if (hurtT <= 0) cat.Pos += catV * dt;
            cat.Pos = new Vector2(Mathf.Clamp(cat.Pos.x, field.x + 0.6f * U, field.xMax - 0.6f * U), Mathf.Clamp(cat.Pos.y, field.y + 0.6f * U, field.yMax - 0.6f * U));
            cat.Rot = Mathf.Clamp(catV.x / U * 3f, -20, 20); cat.Scale = hurtT > 0 ? 1f + Mathf.Sin(hurtT * 40f) * 0.1f : 1f; cat.Sync();
            star.Rot += 90 * dt; star.Scale = 1f + Mathf.Sin(T * 6f) * 0.08f; star.Sync();
            if ((star.Pos - cat.Pos).magnitude < 0.95f * U) { Score++; Pop("+1", star.Pos, Hex(255, 170, 30)); Sfx("sfx_coin", 0.45f, 1.2f + Mathf.Min(0.6f, Score * 0.02f)); star.Pos = FreeSpot(1.6f); star.Sync(); if (Score % 6 == 0 && vases.Count < 9) vases.Add(Sprite(G("vase"), FreeSpot(1.8f), new Vector2(0.85f * U, 1.15f * U))); }
            if (hurtT <= 0)
                foreach (var v in vases)
                    if ((v.Pos - cat.Pos).magnitude < 0.85f * U)
                    {
                        hurtT = 0.9f; catV = -catV * 0.5f; Pop("Crash!", v.Pos, Hex(90, 150, 220)); v.Pos = FreeSpot(1.8f); v.Sync(); LoseLife("Too many vases broke!"); break;
                    }
            dot.E.BringToFront();
        }
    }

    /// <summary>Latte art: trace the shape in milk foam. Five cups — heart, cat face, star, spiral, flower. Accuracy is your score.</summary>
    public class LatteArt : MiniGame
    {
        public override string Id => "latte";
        public override string Title => "Latte Art";
        public override string HowTo => "Hold and trace the dotted shape with milk foam. Start at the pink dot!";
        public override string IconPath => "Games/cup";
        public override int Unlock => 3;
        public override int[] Stars => new[] { 250, 350, 430 };
        public override Color Bg => Hex(214, 186, 160);
        public override Color TileCol => Hex(240, 226, 210);
        public override float CoinsPerPoint => 0.12f;

        static readonly string[] Shapes = { "heart", "catface", "star", "spiral", "flower" };
        int round = -1; List<Vector2> guide = new(), trace = new(); readonly List<VisualElement> marks = new();
        VisualElement cup, startDot; Label roundL; Vector2 c; float r, roundT, showT; bool drawing, scored;

        protected override void Setup()
        {
            c = new Vector2(W / 2, H * 0.54f); r = H * 0.36f;
            Box(new Rect(c.x - r * 1.18f, c.y - r * 1.18f + 0.25f * U, r * 2.36f, r * 2.36f), Hex(176, 140, 112, 0.6f), r * 1.18f);   // saucer shadow
            Box(new Rect(c.x - r * 1.14f, c.y - r * 1.14f, r * 2.28f, r * 2.28f), Hex(255, 252, 248), r * 1.14f);                     // saucer
            Box(new Rect(c.x - r * 1.02f, c.y - r * 1.02f, r * 2.04f, r * 2.04f), Hex(250, 246, 240), r * 1.02f, null, "gcuprim");     // cup rim
            cup = Box(new Rect(c.x - r * 0.92f, c.y - r * 0.92f, r * 1.84f, r * 1.84f), Hex(150, 96, 60), r * 0.92f);                // coffee
            Box(new Rect(c.x - r * 0.8f, c.y - r * 0.8f, r * 1.6f, r * 1.6f), Hex(182, 124, 80), r * 0.8f, cup == null ? null : null);   // crema
            roundL = new Label(""); roundL.AddToClassList("glatte-round"); roundL.pickingMode = PickingMode.Ignore; S.Add(roundL);
            NextRound();
        }
        void NextRound()
        {
            round++; foreach (var m in marks) m.RemoveFromHierarchy(); marks.Clear(); trace.Clear(); scored = false; drawing = false;
            if (round >= Shapes.Length) { Finish(Score >= Stars[0], "Beautiful lattes!"); return; }
            guide = Build(Shapes[round]); roundT = 12f; showT = 0;
            roundL.text = $"Cup {round + 1} of {Shapes.Length}";
            for (int i = 0; i < guide.Count; i += 2) { var d = Dot(guide[i], 0.14f * U, Hex(255, 255, 255, 0.45f)); marks.Add(d); }
            startDot = Dot(guide[0], 0.42f * U, Hex(255, 120, 165)); startDot.AddToClassList("gstart"); marks.Add(startDot);
            TimeLeft = roundT;
        }
        VisualElement Dot(Vector2 p, float rad, Color col) => Box(new Rect(p.x - rad, p.y - rad, rad * 2, rad * 2), col, rad);
        List<Vector2> Build(string shape)
        {
            var pts = new List<Vector2>(); int n = 140; float s = r * 0.62f;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n; Vector2 p;
                switch (shape)
                {
                    case "heart": { float a = t * Mathf.PI * 2; p = new Vector2(16 * Mathf.Pow(Mathf.Sin(a), 3), -(13 * Mathf.Cos(a) - 5 * Mathf.Cos(2 * a) - 2 * Mathf.Cos(3 * a) - Mathf.Cos(4 * a))) / 17f; p.y += 0.12f; break; }
                    case "catface": { float a = t * Mathf.PI * 2 - Mathf.PI / 2; float ear = Mathf.Max(0, 1 - Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, -90 + 42)) / 22f) + Mathf.Max(0, 1 - Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, -90 - 42)) / 22f); float rr = 0.78f + ear * 0.42f; p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; p.y += 0.08f; break; }
                    case "star": { float a = t * 10f; int k = Mathf.FloorToInt(a) % 10; float f = a - Mathf.Floor(a); Vector2 A = StarPt(k), B = StarPt((k + 1) % 10); p = Vector2.Lerp(A, B, f); break; }
                    case "spiral": { float a = t * Mathf.PI * 4.2f; float rr = 0.1f + 0.9f * t; p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; break; }
                    default: { float a = t * Mathf.PI * 2; float rr = 0.62f + 0.36f * Mathf.Cos(5 * a); p = new Vector2(Mathf.Cos(a - Mathf.PI / 2), Mathf.Sin(a - Mathf.PI / 2)) * rr; break; }
                }
                pts.Add(c + p * s);
            }
            return pts;
        }
        static Vector2 StarPt(int k) { float a = -Mathf.PI / 2 + k * Mathf.PI / 5; float rr = k % 2 == 0 ? 1f : 0.42f; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; }

        public override void Down(Vector2 p) { if (scored || Over) return; if ((p - guide[0]).magnitude < 1.2f * U || trace.Count > 0) { drawing = true; Add(p); } else Pop("Start at the pink dot", guide[0] - new Vector2(0, U), Hex(255, 255, 255), 0.7f); }
        public override void Move(Vector2 p) { if (drawing) Add(p); }
        public override void Up(Vector2 p) { if (drawing && trace.Count > 10) ScoreRound(); drawing = false; }
        void Add(Vector2 p)
        {
            if (trace.Count > 0 && (p - trace[^1]).magnitude < 0.07f * U) return;
            if (trace.Count > 0) { var a = trace[^1]; int steps = Mathf.CeilToInt((p - a).magnitude / (0.08f * U)); for (int i = 1; i < steps; i++) Foam(Vector2.Lerp(a, p, i / (float)steps)); }
            Foam(p); trace.Add(p);
        }
        void Foam(Vector2 p) { var d = Dot(p, 0.17f * U, Hex(255, 248, 236)); marks.Add(d); startDot?.BringToFront(); }
        void ScoreRound()
        {
            scored = true; float tol = 0.42f * U;
            int cover = 0; foreach (var g in guide) { foreach (var t in trace) if ((t - g).sqrMagnitude < tol * tol) { cover++; break; } }
            int near = 0; foreach (var t in trace) { float best = float.MaxValue; foreach (var g in guide) best = Mathf.Min(best, (t - g).sqrMagnitude); if (best < tol * tol) near++; }
            float acc = (cover / (float)guide.Count) * 0.6f + (near / (float)trace.Count) * 0.4f;
            int pts = Mathf.RoundToInt(Mathf.Pow(acc, 1.4f) * 100f);
            Score += pts; Pop(pts >= 85 ? $"Gorgeous! +{pts}" : pts >= 60 ? $"Lovely +{pts}" : $"+{pts}", c - new Vector2(0, r * 0.2f), Hex(255, 255, 255), 1.2f);
            Sfx(pts >= 85 ? "sfx_levelup" : "sfx_coin", 0.5f); showT = 1.3f;
        }
        protected override void Tick(float dt)
        {
            if (scored) { showT -= dt; if (showT <= 0) NextRound(); return; }
            if (startDot != null) startDot.style.scale = new Scale(Vector3.one * (1f + Mathf.Sin(T * 6f) * 0.15f));
        }
        protected override void TimeUp() { if (!scored) { if (trace.Count > 10) ScoreRound(); else { scored = true; showT = 0.8f; Pop("Too slow!", c, Hex(255, 255, 255)); } } TimeLeft = 99f; }
    }
}
