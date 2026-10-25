using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Yarn Pop — 3D bubble shooter with real yarn balls on a craft-room wall. Drag to aim (a dotted guide shows the
    /// bounce), let go and your cat bats the ball up. Match 3+ of a colour to pop them; balls left hanging fall for
    /// bonus points. Every 6 shots without a pop a new row slides down — don't let the yarn reach the line!
    /// </summary>
    public class YarnPop : Game3D
    {
        public override string Id => "yarn";
        public override string Title => "Yarn Pop";
        public override string HowTo => "Drag to aim, let go to shoot. Match 3 yarn balls of a colour to pop them!";
        public override string IconPath => "Games/yarn_0";
        public override int Unlock => 5;
        public override int[] Stars => new[] { 300, 800, 1500 };
        public override Color TileCol => Hex(236, 226, 255);
        public override float CoinsPerPoint => 0.05f;
        protected override Color Sky => new(0.90f, 0.86f, 1f);

        const int Cols = 11, MaxRows = 11, Colors = 5; const float D = 0.62f;
        static readonly Color[] YarnCols = { new(1f, 0.45f, 0.58f), new(0.42f, 0.70f, 1f), new(1f, 0.80f, 0.25f), new(0.50f, 0.84f, 0.45f), new(0.74f, 0.52f, 1f) };
        class Ball { public Transform t; public int kind; public Vector3 target, v; public float spin, pop = -1; }
        readonly List<Ball[]> grid = new(); readonly List<Ball> fallers = new(), popping = new(); readonly List<Transform> guide = new();
        readonly Mesh[] yarn = new Mesh[Colors]; int shift, shotsSincePop;
        float left, dangerY, shooterY; Ball shot, next; Vector3 aim = Vector3.up, shotV; bool aiming, flying; Puppet cat; Transform danger;

        protected override void Build()
        {
            left = -D * (Cols - 0.5f) / 2f; dangerY = -7.7f * D; shooterY = -9.3f * D;
            float fieldW = D * (Cols + 0.5f), midY = (0.6f * D + shooterY - 1.2f * D) / 2f;
            var k = new MeshKit();
            // craft-room wall, wooden frame around a soft lilac board, shelves of yarn baskets either side
            k.Box(new Vector3(0, midY, 1.2f), new Vector3(30f, 20f, 0.2f), new Color(1f, 0.93f, 0.86f));
            for (int i = 0; i < 26; i++) k.Box(new Vector3(-15f + i * 1.2f, midY, 1.09f), new Vector3(0.4f, 20f, 0.02f), new Color(1f, 0.88f, 0.84f));
            k.Box(new Vector3(0, (0.7f * D + dangerY) / 2f, 0.6f), new Vector3(fieldW + 0.3f, 0.7f * D - dangerY + 0.3f, 0.12f), new Color(0.96f, 0.93f, 1f));
            Color wood = new(0.82f, 0.60f, 0.42f);
            k.Box(new Vector3(-fieldW / 2 - 0.25f, (0.7f * D + dangerY) / 2f, 0.4f), new Vector3(0.24f, 0.7f * D - dangerY + 0.8f, 0.4f), wood);
            k.Box(new Vector3(fieldW / 2 + 0.25f, (0.7f * D + dangerY) / 2f, 0.4f), new Vector3(0.24f, 0.7f * D - dangerY + 0.8f, 0.4f), wood);
            k.Box(new Vector3(0, 0.7f * D + 0.15f, 0.4f), new Vector3(fieldW + 0.75f, 0.3f, 0.4f), wood);
            k.Box(new Vector3(0, shooterY - 0.75f, 0), new Vector3(30f, 0.3f, 6f), new Color(0.80f, 0.62f, 0.46f));   // floor
            var rnd = new System.Random(2);
            foreach (var s in new[] { -1f, 1f })
                for (int sh = 0; sh < 4; sh++)
                {
                    float sy = shooterY - 0.4f + sh * 1.7f, sx = s * (fieldW / 2 + 2.1f);
                    k.Box(new Vector3(sx, sy, 0.6f), new Vector3(2.4f, 0.1f, 0.8f), wood);
                    for (int b = 0; b < 3; b++) { var c = YarnCols[rnd.Next(Colors)]; k.Sphere(new Vector3(sx - 0.7f + b * 0.7f, sy + 0.32f, 0.6f), 0.27f, c, 12, 8); k.Ellipsoid(new Vector3(sx - 0.7f + b * 0.7f, sy + 0.32f, 0.6f), new Vector3(0.28f, 0.05f, 0.28f), Color.Lerp(c, Color.black, 0.18f), 12, 3); }
                }
            k.Ellipsoid(new Vector3(-1.9f * D - 0.2f, shooterY - 0.55f, -0.2f), new Vector3(0.9f, 0.22f, 0.7f), new Color(1f, 0.62f, 0.74f), 16, 8);   // the cat's cushion
            MakeMesh(k, "Room");
            var kd = new MeshKit(); for (float x = left - D / 2; x < -left + D / 2; x += 0.3f) kd.Box(new Vector3(x + 0.08f, 0, 0), new Vector3(0.18f, 0.05f, 0.05f), new Color(1f, 0.45f, 0.58f));
            danger = MakeMesh(kd, "Danger", null, false).transform; danger.localPosition = new Vector3(0, dangerY, 0.3f);
            for (int c = 0; c < Colors; c++) { Props3D.Yarn(k, D * 0.48f, YarnCols[c]); yarn[c] = k.ToMesh(); }
            var kg = new MeshKit(); kg.Sphere(Vector3.zero, 0.06f, Color.white, 8, 6); var gm = kg.ToMesh();
            for (int i = 0; i < 14; i++) { var g = MeshObj(gm, "Guide", null, false).transform; g.gameObject.SetActive(false); guide.Add(g); }
            for (int r = 0; r < 6; r++) PushRowBottom(r);
            cat = Cat(Hero != null ? Hero.Id : "Mochi", new Vector3(-1.9f * D - 0.2f, shooterY - 0.45f, -0.2f), 150f, 0.8f); cat.Play("Idle_Sit", 0f);
            next = NewBall(RandColor(), new Vector3(1.4f * D + 0.3f, shooterY, -0.2f)); next.t.localScale = Vector3.one * 0.75f;
            Load();
            camPos = new Vector3(0, midY + 0.45f, -11.6f); camLook = new Vector3(0, midY + 0.45f, 0); camFov = 40f;
            SnapCamera(new Vector3(0, shooterY + 0.5f, -5f), new Vector3(0, shooterY + 1.5f, 0), 44f); camSmooth = 1.6f;
        }
        Ball NewBall(int kind, Vector3 at)
        {
            var b = new Ball { kind = kind, t = MeshObj(yarn[kind], "Yarn", null, false).transform }; b.t.localPosition = at; b.target = at;
            b.t.localRotation = Quaternion.Euler(Rnd(0, 360), Rnd(0, 360), Rnd(0, 360)); return b;
        }
        int RandColor()
        {
            var present = new List<int>(); foreach (var row in grid) foreach (var b in row) if (b != null && !present.Contains(b.kind)) present.Add(b.kind);
            return present.Count > 0 && Random.value < 0.85f ? present[Random.Range(0, present.Count)] : Random.Range(0, Colors);
        }
        void Load()
        {
            shot = NewBall(next.kind, next.t.localPosition); shot.target = new Vector3(0, shooterY, -0.2f); shot.pop = 0;
            int kk = RandColor(); next.kind = kk; next.t.GetComponent<MeshFilter>().sharedMesh = yarn[kk]; next.t.localScale = Vector3.zero; next.pop = 0;
            flying = false;
        }
        Vector3 CellPos(int row, int col) => new(left + col * D + (((row + shift) & 1) == 1 ? D / 2 : 0), -row * D * 0.87f, 0);
        int ColsIn(int row) => ((row + shift) & 1) == 1 ? Cols - 1 : Cols;
        void PushRowBottom(int r)
        {
            while (grid.Count <= r) grid.Add(new Ball[Cols]);
            for (int c = 0; c < ColsIn(r); c++) { var b = NewBall(Random.Range(0, Colors), CellPos(r, c)); grid[r][c] = b; }
        }
        void PushRowTop()
        {
            shift ^= 1; grid.Insert(0, new Ball[Cols]);
            for (int c = 0; c < ColsIn(0); c++) { var b = NewBall(Random.Range(0, Colors), CellPos(0, c) + Vector3.up * D); b.target = CellPos(0, c); grid[0][c] = b; }
            Reflow(); Shake(0.25f); Sfx("sfx_place", 0.5f, 0.8f); Banner = "Here comes more yarn!"; BannerCol = Hex(190, 140, 255); bannerT = 1f;
            if (Reached()) Finish(Score >= Stars[0], "The yarn reached the line!");
        }
        void Reflow() { for (int r = 0; r < grid.Count; r++) for (int c = 0; c < Cols; c++) if (grid[r][c] != null) grid[r][c].target = CellPos(r, c); }
        bool Reached() { for (int r = 0; r < grid.Count; r++) foreach (var b in grid[r]) if (b != null && b.target.y - D / 2 <= dangerY) return true; return false; }

        public override void Down(Vector2 p) { if (flying || shot == null) return; aiming = true; SetAim(p); }
        public override void Move(Vector2 p) { if (aiming) SetAim(p); }
        public override void Up(Vector2 p)
        {
            if (!aiming || flying || shot == null) return; aiming = false; foreach (var g in guide) g.gameObject.SetActive(false);
            flying = true; shotV = aim * 19f * D; Sfx("sfx_pop", 0.4f, 1.3f);
            cat.Play("Bat", 0.05f, true); cat.Squash(-0.25f); batT = 0.5f;
        }
        float batT, bannerT;
        void SetAim(Vector2 p)
        {
            if (!StageToPlane(p, Vector3.zero, Vector3.forward, out var l)) return;
            var dir = l - shot.t.localPosition; dir.z = 0; if (dir.y < 0.12f) dir.y = 0.12f;
            float ang = Mathf.Clamp(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 12f, 168f);
            aim = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0);
            // dotted guide with one wall bounce
            var pos = shot.t.localPosition; pos.z = 0; var v = aim; float minX = left, maxX = left + (Cols - 1) * D + D / 2;
            for (int i = 0; i < guide.Count; i++)
            {
                pos += v * D * 0.8f; if (pos.x < minX || pos.x > maxX) { v.x = -v.x; pos.x = Mathf.Clamp(pos.x, minX, maxX); }
                guide[i].gameObject.SetActive(pos.y < 0); guide[i].localPosition = pos + Vector3.back * 0.3f; guide[i].localScale = Vector3.one * (1f - i * 0.04f);
            }
        }
        protected override void Tick(float dt)
        {
            if (bannerT > 0) { bannerT -= dt; if (bannerT <= 0) Banner = null; }
            if (!flying || shot == null) return;
            float minX = left, maxX = left + (Cols - 1) * D + D / 2;
            for (int s = 0; s < 4; s++)
            {
                var p = shot.t.localPosition; p.z = Mathf.Lerp(p.z, 0, 0.3f); p += shotV * dt / 4;
                if (p.x < minX) { p.x = minX; shotV.x = -shotV.x; Sfx("sfx_tap", 0.25f, 1.6f); }
                if (p.x > maxX) { p.x = maxX; shotV.x = -shotV.x; Sfx("sfx_tap", 0.25f, 1.6f); }
                shot.t.localPosition = p; shot.target = p;
                bool stick = p.y >= 0;
                if (!stick) foreach (var row in grid) { foreach (var b in row) if (b != null && (b.target - new Vector3(p.x, p.y, 0)).sqrMagnitude < D * D * 0.78f) { stick = true; break; } if (stick) break; }
                if (stick) { Snap(); return; }
            }
            shot.t.localRotation *= Quaternion.Euler(0, 0, -shotV.x * 60f * dt);
        }
        void Snap()
        {
            var sp = shot.t.localPosition; int br = 0, bc = 0; float best = float.MaxValue; int maxR = Mathf.Min(grid.Count + 1, MaxRows + 2);
            for (int r = 0; r < maxR; r++) for (int c = 0; c < ColsIn(r); c++)
            {
                if (r < grid.Count && grid[r][c] != null) continue;
                float dd = (CellPos(r, c) - new Vector3(sp.x, sp.y, 0)).sqrMagnitude; if (dd < best) { best = dd; br = r; bc = c; }
            }
            while (grid.Count <= br) grid.Add(new Ball[Cols]);
            grid[br][bc] = shot; shot.target = CellPos(br, bc); var placed = shot; shot = null;
            // a little bump wave through the neighbours
            foreach (var (r, c) in Neighbours(br, bc)) if (Valid(r, c)) grid[r][c].t.localPosition += (grid[r][c].target - placed.target).normalized * 0.08f;
            var group = Flood(br, bc, placed.kind);
            if (group.Count >= 3)
            {
                foreach (var (r, c) in group) { var b = grid[r][c]; grid[r][c] = null; b.pop = 0; popping.Add(b); }
                int pts = group.Count * 10; Score += pts; Pop3D("+" + pts, placed.target + Vector3.up * 0.5f, Hex(255, 110, 160), 1f + Mathf.Min(0.5f, group.Count * 0.05f));
                Sfx("sfx_pop", 0.6f, 1.0f + Mathf.Min(0.6f, group.Count * 0.05f)); Shake(0.1f + group.Count * 0.02f);
                int dropped = DropFloating(); if (dropped > 0) { int bonus = dropped * 15; Score += bonus; Pop3D($"+{bonus}", placed.target + Vector3.down * 0.6f, Hex(255, 170, 40), 1.2f); Sfx("sfx_coin", 0.5f, 1.3f); }
                shotsSincePop = 0;
                if (Empty()) { Score += 500; Pop3D("Clear! +500", new Vector3(0, -3f * D, 0), Hex(255, 120, 165), 1.5f); Sfx("sfx_levelup", 0.6f); Confetti(new Vector3(-3f, -6f * D, -0.5f), 50); Confetti(new Vector3(3f, -6f * D, -0.5f), 50); for (int r = 0; r < 5; r++) PushRowBottom(r); }
            }
            else { shotsSincePop++; Sfx("sfx_tap", 0.4f); if (shotsSincePop >= 6) { shotsSincePop = 0; PushRowTop(); if (Over) return; } }
            if (Reached()) { Finish(Score >= Stars[0], "The yarn reached the line!"); return; }
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
            while (q.Count > 0) { var (r, c) = q.Dequeue(); foreach (var n in Neighbours(r, c)) if (!seen.Contains(n) && Valid(n.Item1, n.Item2) && grid[n.Item1][n.Item2].kind == kind) { seen.Add(n); q.Enqueue(n); } }
            return new List<(int, int)>(seen);
        }
        int DropFloating()
        {
            var keep = new HashSet<(int, int)>(); var q = new Queue<(int, int)>();
            if (grid.Count > 0) for (int c = 0; c < Cols; c++) if (Valid(0, c)) { keep.Add((0, c)); q.Enqueue((0, c)); }
            while (q.Count > 0) { var (r, c) = q.Dequeue(); foreach (var n in Neighbours(r, c)) if (!keep.Contains(n) && Valid(n.Item1, n.Item2)) { keep.Add(n); q.Enqueue(n); } }
            int dropped = 0;
            for (int r = 0; r < grid.Count; r++) for (int c = 0; c < Cols; c++)
                if (grid[r][c] != null && !keep.Contains((r, c))) { var b = grid[r][c]; grid[r][c] = null; b.v = new Vector3(Rnd(-1.2f, 1.2f), Rnd(0.5f, 2f), -Rnd(1f, 3f)); b.spin = Rnd(-400f, 400f); fallers.Add(b); dropped++; }
            return dropped;
        }
        public override void Frame(float dt)
        {
            // grid balls glide to their cells (rows sliding down, snaps) and wobble a little
            foreach (var row in grid) foreach (var b in row) if (b != null && b.t) b.t.localPosition = Damp(b.t.localPosition, b.target, 14f, dt);
            if (shot != null && !flying) { shot.t.localPosition = Damp(shot.t.localPosition, shot.target, 12f, dt); if (shot.pop >= 0) { shot.pop += dt * 4f; shot.t.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(shot.pop)); if (shot.pop >= 1) shot.pop = -1; } }
            if (next != null && next.pop >= 0) { next.pop += dt * 4f; next.t.localScale = Vector3.one * 0.75f * EaseOutBack(Mathf.Clamp01(next.pop)); if (next.pop >= 1) next.pop = -1; }
            for (int i = popping.Count - 1; i >= 0; i--)
            {
                var b = popping[i]; b.pop += dt * 5f;
                b.t.localScale = Vector3.one * (b.pop < 0.4f ? 1f + b.pop * 0.6f : Mathf.Max(0, 1.24f - (b.pop - 0.4f) * 2.2f));
                if (b.pop >= 0.4f && b.pop - dt * 5f < 0.4f) Burst(b.t.localPosition, 8, new[] { YarnCols[b.kind], Color.white }, 2.2f, 0.12f, 0.6f, 0.5f);
                if (b.pop >= 1f) { Object.Destroy(b.t.gameObject); popping.RemoveAt(i); }
            }
            for (int i = fallers.Count - 1; i >= 0; i--)
            {
                var f = fallers[i]; f.v.y -= 15f * dt; f.t.localPosition += f.v * dt; f.t.localRotation *= Quaternion.Euler(f.spin * dt, 0, f.spin * 0.5f * dt);
                if (f.t.localPosition.y < shooterY - 0.6f && f.v.y < 0) { f.v.y = -f.v.y * 0.4f; f.spin *= 0.6f; if (Mathf.Abs(f.v.y) < 0.8f) { Object.Destroy(f.t.gameObject); fallers.RemoveAt(i); Poof(f.t.localPosition, 0.4f, 4); } }
            }
            if (batT > 0) { batT -= dt; if (batT <= 0) cat.Play("Idle_Sit", 0.2f); }
            if (danger) { bool near = false; foreach (var row in grid) foreach (var b in row) if (b != null && b.target.y - D / 2 < dangerY + D * 1.2f) near = true; danger.localScale = Vector3.one * (near ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.3f : 1f); Danger = near && !Over; }
            camSmooth = CountingDown ? 1.6f : 3f;
            base.Frame(dt);
        }
    }
}
