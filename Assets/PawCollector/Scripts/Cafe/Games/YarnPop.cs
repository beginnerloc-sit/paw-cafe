using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>Bubble shooter with yarn balls. Aim with your finger, let go to shoot. Match 3+ of a colour to pop them;
    /// balls left hanging fall for bonus points. Every few shots without a pop a new row slides down — don't let it reach the cat!</summary>
    public class YarnPop : MiniGame
    {
        public override string Id => "yarn";
        public override string Title => "Yarn Pop";
        public override string HowTo => "Drag to aim, let go to shoot. Match 3 yarn balls of a colour to pop them!";
        public override string IconPath => "Games/yarn_0";
        public override int Unlock => 5;
        public override int[] Stars => new[] { 300, 800, 1500 };
        public override Color Bg => Hex(232, 222, 255);
        public override Color TileCol => Hex(236, 226, 255);
        public override float CoinsPerPoint => 0.05f;

        const int Cols = 11, MaxRows = 11; const int Colors = 5;
        readonly List<Spr[]> grid = new();   // grid[row][col], row 0 at the top
        int shift;                            // flips row parity when a new row is pushed in
        float d, left, top, dangerY; Spr shot, next, cat; Vector2 aim = Vector2.up; bool aiming, flying; Vector2 shotV;
        readonly List<VisualElement> guide = new(); readonly List<Spr> fallers = new(); int shotsSincePop;

        protected override void Setup()
        {
            float fieldW = Mathf.Min(W * 0.62f, H * 0.95f); d = fieldW / (Cols + 0.5f); left = W / 2 - fieldW / 2 + d / 2; top = 0.9f * U + d / 2;
            Box(new Rect(W / 2 - fieldW / 2 - 0.15f * U, 0, fieldW + 0.3f * U, H), Hex(248, 242, 255), 0.4f * U, null, "gfield");
            dangerY = H - 2.3f * U;
            Box(new Rect(W / 2 - fieldW / 2, dangerY, fieldW, 0.06f * U), Hex(255, 140, 170, 0.6f));
            for (int i = 0; i < 12; i++) { var g = Box(new Rect(0, 0, 0.16f * U, 0.16f * U), Hex(255, 255, 255, 0.8f), 0.08f * U); guide.Add(g); g.style.display = DisplayStyle.None; }
            for (int r = 0; r < 6; r++) PushRowBottom(r);
            cat = Sprite(CatTex(Hero), new Vector2(W / 2 - 1.6f * U, H - 0.95f * U), new Vector2(1.4f * U, 1.4f * U), "gcat");
            next = Sprite(Yarn(RandColor()), new Vector2(W / 2 + 1.4f * U, H - 0.75f * U), new Vector2(d * 0.75f, d * 0.75f)); next.Kind = cur;
            Load();
        }
        int cur;
        int RandColor()
        {
            // prefer colours still on the board so the game stays winnable
            var present = new List<int>(); foreach (var row in grid) foreach (var b in row) if (b != null && !present.Contains(b.Kind)) present.Add(b.Kind);
            cur = present.Count > 0 && UnityEngine.Random.value < 0.85f ? present[UnityEngine.Random.Range(0, present.Count)] : UnityEngine.Random.Range(0, Colors);
            return cur;
        }
        static Texture2D Yarn(int k) => G("yarn_" + k);
        void Load()
        {
            shot = Sprite(Yarn(next.Kind), new Vector2(W / 2, H - 0.95f * U), new Vector2(d, d)); shot.Kind = next.Kind;
            int k = RandColor(); next.Kind = k; next.Tex(Yarn(k)); flying = false;
        }
        Vector2 CellPos(int row, int col) => new(left + col * d + (((row + shift) & 1) == 1 ? d / 2 : 0), top + row * d * 0.87f);
        int ColsIn(int row) => ((row + shift) & 1) == 1 ? Cols - 1 : Cols;
        void PushRowBottom(int r)
        {
            while (grid.Count <= r) grid.Add(new Spr[Cols]);
            for (int c = 0; c < ColsIn(r); c++) { int k = UnityEngine.Random.Range(0, Colors); var b = Sprite(Yarn(k), CellPos(r, c), new Vector2(d, d)); b.Kind = k; grid[r][c] = b; }
        }
        void PushRowTop()
        {
            shift ^= 1; grid.Insert(0, new Spr[Cols]);
            for (int c = 0; c < ColsIn(0); c++) { int k = UnityEngine.Random.Range(0, Colors); var b = Sprite(Yarn(k), CellPos(0, c), new Vector2(d, d)); b.Kind = k; grid[0][c] = b; }
            Reflow();
            for (int r = 0; r < grid.Count; r++) foreach (var b in grid[r]) if (b != null && b.Pos.y + d / 2 >= dangerY) { Finish(Score >= Stars[0], "The yarn reached the line!"); return; }
        }
        void Reflow() { for (int r = 0; r < grid.Count; r++) for (int c = 0; c < Cols; c++) if (grid[r][c] != null) { grid[r][c].Pos = CellPos(r, c); grid[r][c].Sync(); } }

        public override void Down(Vector2 p) { if (flying) return; aiming = true; SetAim(p); }
        public override void Move(Vector2 p) { if (aiming) SetAim(p); }
        public override void Up(Vector2 p)
        {
            if (!aiming || flying) return; aiming = false; foreach (var g in guide) g.style.display = DisplayStyle.None;
            flying = true; shotV = aim * 19f * d; Sfx("sfx_pop", 0.4f, 1.3f);
        }
        void SetAim(Vector2 p)
        {
            var dir = p - shot.Pos; if (dir.y > -0.2f * U) dir.y = -0.2f * U; aim = dir.normalized;
            float ang = Mathf.Atan2(-aim.y, aim.x) * Mathf.Rad2Deg; ang = Mathf.Clamp(ang, 12f, 168f); aim = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), -Mathf.Sin(ang * Mathf.Deg2Rad));
            // dotted guide with one wall bounce
            var pos = shot.Pos; var v = aim; float minX = left - d / 2 + d / 2, maxX = left + (Cols - 1) * d + d / 2 - d / 2 + d / 2;
            for (int i = 0; i < guide.Count; i++)
            {
                pos += v * d * 0.9f; if (pos.x < minX || pos.x > maxX) { v.x = -v.x; pos.x = Mathf.Clamp(pos.x, minX, maxX); }
                guide[i].style.display = pos.y > top ? DisplayStyle.Flex : DisplayStyle.None; guide[i].style.left = pos.x - 0.08f * U; guide[i].style.top = pos.y - 0.08f * U;
            }
        }
        protected override void Tick(float dt)
        {
            cat.Rot = Mathf.Sin(T * 2f) * 4f; cat.Sync();
            for (int i = fallers.Count - 1; i >= 0; i--) { var f = fallers[i]; f.Vel.y += 30f * U * dt; f.Pos += f.Vel * dt; f.Rot += 200 * dt; f.Sync(); if (f.Pos.y > H + d) { f.Kill(); fallers.RemoveAt(i); } }
            if (!flying) return;
            float minX = left, maxX = left + (Cols - 1) * d + d / 2;
            int sub = 4; for (int s = 0; s < sub; s++)
            {
                shot.Pos += shotV * dt / sub;
                if (shot.Pos.x < minX) { shot.Pos.x = minX; shotV.x = -shotV.x; }
                if (shot.Pos.x > maxX) { shot.Pos.x = maxX; shotV.x = -shotV.x; }
                bool stick = shot.Pos.y <= top;
                if (!stick) foreach (var row in grid) { foreach (var b in row) if (b != null && (b.Pos - shot.Pos).sqrMagnitude < d * d * 0.78f) { stick = true; break; } if (stick) break; }
                if (stick) { Snap(); return; }
            }
            shot.Sync();
        }
        void Snap()
        {
            // nearest free cell
            int br = 0, bc = 0; float best = float.MaxValue;
            int maxR = Mathf.Min(grid.Count + 1, MaxRows + 2);
            for (int r = 0; r < maxR; r++) for (int c = 0; c < ColsIn(r); c++)
            {
                if (r < grid.Count && grid[r][c] != null) continue;
                float dd = (CellPos(r, c) - shot.Pos).sqrMagnitude; if (dd < best) { best = dd; br = r; bc = c; }
            }
            while (grid.Count <= br) grid.Add(new Spr[Cols]);
            grid[br][bc] = shot; shot.Pos = CellPos(br, bc); shot.Sync(); var placed = shot; shot = null;
            // match
            var group = Flood(br, bc, placed.Kind);
            if (group.Count >= 3)
            {
                foreach (var (r, c) in group) { var b = grid[r][c]; grid[r][c] = null; b.Vel = new Vector2(Rnd(-2, 2) * U, -Rnd(2, 5) * U); fallers.Add(b); }
                int pts = group.Count * 10; Score += pts; Pop("+" + pts, placed.Pos, Hex(255, 110, 160)); Sfx("sfx_pop", 0.6f, 1.0f + Mathf.Min(0.6f, group.Count * 0.05f));
                int dropped = DropFloating(); if (dropped > 0) { int bonus = dropped * 15; Score += bonus; Pop($"+{bonus}", placed.Pos + new Vector2(0, U), Hex(255, 170, 40), 1.1f); }
                shotsSincePop = 0;
                if (Empty()) { Score += 500; Pop("Clear! +500", new Vector2(W / 2, H / 2), Hex(255, 120, 165), 1.4f); Sfx("sfx_levelup", 0.6f); for (int r = 0; r < 5; r++) PushRowBottom(r); }
            }
            else { shotsSincePop++; Sfx("sfx_tap", 0.4f); if (shotsSincePop >= 6) { shotsSincePop = 0; PushRowTop(); if (Over) return; } }
            for (int r = 0; r < grid.Count; r++) foreach (var b in grid[r]) if (b != null && b.Pos.y + d / 2 >= dangerY) { Finish(Score >= Stars[0], "The yarn reached the line!"); return; }
            Load();
        }
        bool Empty() { foreach (var row in grid) foreach (var b in row) if (b != null) return false; return true; }
        IEnumerable<(int, int)> Neighbours(int r, int c)
        {
            bool odd = ((r + shift) & 1) == 1;
            yield return (r, c - 1); yield return (r, c + 1);
            int a = odd ? c : c - 1, b = odd ? c + 1 : c;
            yield return (r - 1, a); yield return (r - 1, b); yield return (r + 1, a); yield return (r + 1, b);
        }
        bool Valid(int r, int c) => r >= 0 && r < grid.Count && c >= 0 && c < Cols && grid[r][c] != null;
        List<(int, int)> Flood(int r0, int c0, int kind)
        {
            var seen = new HashSet<(int, int)>(); var q = new Queue<(int, int)>(); q.Enqueue((r0, c0)); seen.Add((r0, c0));
            while (q.Count > 0) { var (r, c) = q.Dequeue(); foreach (var n in Neighbours(r, c)) if (!seen.Contains(n) && Valid(n.Item1, n.Item2) && grid[n.Item1][n.Item2].Kind == kind) { seen.Add(n); q.Enqueue(n); } }
            return new List<(int, int)>(seen);
        }
        int DropFloating()
        {
            var keep = new HashSet<(int, int)>(); var q = new Queue<(int, int)>();
            if (grid.Count > 0) for (int c = 0; c < Cols; c++) if (Valid(0, c)) { keep.Add((0, c)); q.Enqueue((0, c)); }
            while (q.Count > 0) { var (r, c) = q.Dequeue(); foreach (var n in Neighbours(r, c)) if (!keep.Contains(n) && Valid(n.Item1, n.Item2)) { keep.Add(n); q.Enqueue(n); } }
            int dropped = 0;
            for (int r = 0; r < grid.Count; r++) for (int c = 0; c < Cols; c++)
                if (grid[r][c] != null && !keep.Contains((r, c))) { var b = grid[r][c]; grid[r][c] = null; b.Vel = new Vector2(Rnd(-1, 1) * U, 0); fallers.Add(b); dropped++; }
            return dropped;
        }
    }
}
