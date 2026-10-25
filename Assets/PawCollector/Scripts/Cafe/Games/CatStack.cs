using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Zoomies Catch — 3D. Your cat has the zoomies and races along the café shelf knocking cups and cakes off.
    /// Drag the silver tray to catch them; hairballs and cucumbers are yucky. Let a cup smash on the floor = lose a heart.
    /// </summary>
    public class ZoomiesCatch : Game3D
    {
        public override string Id => "zoomies";
        public override string Title => "Zoomies Catch";
        public override string HowTo => "Slide the tray to catch cups and cakes. Avoid hairballs!";
        public override string IconPath => "Games/cup";
        public override int[] Stars => new[] { 15, 35, 60 };
        public override Color TileCol => Hex(255, 232, 214);
        protected override Color Sky => new(1f, 0.90f, 0.90f);

        const float ShelfY = 3.0f, ShelfZ = 0.95f, TrayY = 0.75f, Field = 4.6f;
        class Item { public Transform t; public int kind; public Vector3 v; public float spin, onTray = -1; public Vector3 trayOff; }
        readonly List<Item> items = new(); readonly Mesh[] meshes = new Mesh[4];
        Transform tray; Puppet cat; float catX = -3f, catDir = 1, trayX, targetX, trayV, dropT = 0.8f; int stacked;
        static readonly Color[] Bands = { new(1f, 0.6f, 0.7f), new(0.5f, 0.75f, 1f), new(1f, 0.82f, 0.3f), new(0.6f, 0.86f, 0.55f) };

        protected override void Build()
        {
            Lives = 3; MaxLives = 3;
            var k = new MeshKit();
            // café wall: pink wallpaper with stripes, wainscot, wooden floor, the long shelf and its brackets
            Color wall = new(1f, 0.88f, 0.90f), stripe = new(1f, 0.94f, 0.95f);
            k.Box(new Vector3(0, 4f, 1.6f), new Vector3(24f, 8f, 0.2f), wall);
            for (int i = 0; i < 30; i++) k.Box(new Vector3(-12f + i * 0.8f, 4f, 1.49f), new Vector3(0.28f, 8f, 0.02f), stripe);
            k.Box(new Vector3(0, 0.55f, 1.45f), new Vector3(24f, 1.1f, 0.1f), new Color(0.98f, 0.80f, 0.70f));
            k.Box(new Vector3(0, 1.12f, 1.4f), new Vector3(24f, 0.08f, 0.18f), new Color(0.90f, 0.66f, 0.52f));
            for (int i = 0; i < 40; i++) k.Box(new Vector3(-12f + i * 0.6f + 0.3f, -0.1f, -3f), new Vector3(0.59f, 0.2f, 9.2f), i % 2 == 0 ? new Color(0.86f, 0.66f, 0.48f) : new Color(0.80f, 0.60f, 0.43f));
            Color wood = new(0.82f, 0.58f, 0.40f);
            k.Box(new Vector3(0, ShelfY - 0.06f, ShelfZ + 0.05f), new Vector3(Field * 2 + 1.4f, 0.12f, 1.1f), wood);
            k.Box(new Vector3(0, ShelfY - 0.14f, ShelfZ - 0.48f), new Vector3(Field * 2 + 1.4f, 0.1f, 0.06f), new Color(0.72f, 0.48f, 0.32f));
            for (int i = -2; i <= 2; i++) k.Box(new Vector3(i * 2.3f, ShelfY - 0.45f, 1.3f), new Vector3(0.12f, 0.6f, 0.5f), new Color(0.72f, 0.48f, 0.32f));
            // windows, a menu board, hanging lamps, plants on the floor
            foreach (var wx in new[] { -6.8f, 6.8f })
            {
                k.Box(new Vector3(wx, 4.6f, 1.45f), new Vector3(2.2f, 2.4f, 0.08f), Color.white); k.Box(new Vector3(wx, 4.6f, 1.4f), new Vector3(1.9f, 2.1f, 0.04f), new Color(0.70f, 0.87f, 1f));
                k.Box(new Vector3(wx, 4.6f, 1.36f), new Vector3(0.08f, 2.1f, 0.04f), Color.white); k.Box(new Vector3(wx, 4.6f, 1.36f), new Vector3(1.9f, 0.08f, 0.04f), Color.white);
            }
            k.Box(new Vector3(0, 4.7f, 1.45f), new Vector3(3.2f, 1.6f, 0.06f), new Color(0.80f, 0.60f, 0.42f)); k.Box(new Vector3(0, 4.7f, 1.4f), new Vector3(2.9f, 1.3f, 0.04f), new Color(0.30f, 0.36f, 0.34f));
            for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.9f + (i % 2) * 1.1f, 5.0f - (i / 2) * 0.5f, 1.37f), new Vector3(0.8f, 0.08f, 0.02f), new Color(0.95f, 0.95f, 0.9f));
            MakeMesh(k, "Cafe");
            Furniture("Plant", new Vector3(-6.2f, 0, 0.6f), 0f); Furniture("Plant", new Vector3(6.4f, 0, 0.6f), 90f); Furniture("Fern", new Vector3(-4.8f, 0, 0.9f), 0f);
            // static decor cups on the shelf
            var rnd = new System.Random(4);
            for (float x = -Field; x <= Field; x += 1.15f) { var kk = new MeshKit(); if (rnd.NextDouble() < 0.6) Props3D.Cup(kk, Vector3.zero, 2.0f, Bands[rnd.Next(Bands.Length)]); else Props3D.Cake(kk, Vector3.zero, 2.0f); MakeMesh(kk, "Deco").transform.localPosition = new Vector3(x, ShelfY, ShelfZ + 0.32f); }
            Props3D.Cup(k, Vector3.zero, 2.4f, Bands[0]); meshes[0] = k.ToMesh();
            Props3D.Cake(k, Vector3.zero, 2.4f); meshes[1] = k.ToMesh();
            Props3D.Hairball(k, Vector3.zero, 2.2f); meshes[2] = k.ToMesh();
            Props3D.Cucumber(k, Vector3.zero, 1.6f); meshes[3] = k.ToMesh();
            // the silver tray
            Color steel = new(0.88f, 0.90f, 0.94f), rim = new(0.72f, 0.74f, 0.80f);
            k.Cylinder(Vector3.zero, 1.15f, 0.05f, steel, 28); k.Cylinder(new Vector3(0, 0.05f, 0), 1.17f, 0.06f, rim, 28, false, 1.2f);
            k.Ellipsoid(new Vector3(-0.4f, 0.056f, -0.35f), new Vector3(0.35f, 0.005f, 0.12f), Color.white, 10, 2);
            tray = MakeMesh(k, "Tray").transform; tray.localPosition = new Vector3(0, TrayY, 0); trayX = targetX = 0;
            cat = Cat(Hero != null ? Hero.Id : "Mochi", new Vector3(catX, ShelfY, ShelfZ - 0.05f), 90f, 0.75f); cat.Play("Walk", 0f);
            camPos = new Vector3(0, 2.4f, -8.2f); camLook = new Vector3(0, 1.75f, 0); camFov = 40f;
            SnapCamera(new Vector3(-2.5f, 3.6f, -3.6f), new Vector3(-3f, 3f, 1f), 42f); camSmooth = 1.6f;
        }
        public override void Down(Vector2 p) { if (StageToPlane(p, new Vector3(0, TrayY, 0), Vector3.forward, out var l)) targetX = l.x; }
        public override void Move(Vector2 p) => Down(p);

        protected override void Tick(float dt)
        {
            // the zoomie cat dashes back and forth along the shelf
            float catSpeed = 2.6f + Score * 0.03f;
            catX += catDir * catSpeed * dt;
            if (catX > Field) { catDir = -1; cat.Squash(0.25f); } if (catX < -Field) { catDir = 1; cat.Squash(0.25f); }
            cat.T.localPosition = new Vector3(catX, ShelfY + Mathf.Abs(Mathf.Sin(T * 12f)) * 0.06f, ShelfZ - 0.05f);
            cat.T.localRotation = Quaternion.Slerp(cat.T.localRotation, Quaternion.Euler(0, catDir > 0 ? 90f : -90f, 0), 1f - Mathf.Exp(-14f * dt));
            cat.Freeze(false, 1.6f + catSpeed * 0.15f);
            float nx = Mathf.Lerp(trayX, Mathf.Clamp(targetX, -Field - 0.4f, Field + 0.4f), 1f - Mathf.Exp(-18f * dt)); trayV = (nx - trayX) / Mathf.Max(dt, 1e-4f); trayX = nx;
            tray.localPosition = new Vector3(trayX, tray.localPosition.y, 0); tray.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(-trayV * 1.6f, -12f, 12f));
            float maxFall = 2.4f + Score * 0.04f;
            dropT -= dt;
            if (dropT <= 0)
            {
                dropT = Mathf.Max(0.38f, 1.05f - Score * 0.012f) * Rnd(0.7f, 1.2f);
                float r = Random.value; int kind = r < 0.14f ? 2 : r < 0.2f ? 3 : r < 0.48f ? 1 : 0;   // 0 cup, 1 cake, 2 hairball, 3 cucumber
                var go = MeshObj(meshes[kind], "Item"); var it = new Item { t = go.transform, kind = kind, v = new Vector3(Rnd(-0.5f, 0.5f) + catDir * 0.4f, 1.2f, -0.5f), spin = Rnd(-240f, 240f) };
                it.t.localPosition = new Vector3(catX + catDir * 0.3f, ShelfY + 0.1f, ShelfZ - 0.3f); items.Add(it); Sfx("sfx_tap", 0.25f, 1.6f); cat.Squash(-0.15f);
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.onTray >= 0)
                {   // riding the tray for a moment, then it pops away in sparkles
                    it.onTray += dt; it.t.localPosition = tray.localPosition + tray.localRotation * it.trayOff; it.t.localRotation = tray.localRotation;
                    float s = it.onTray < 0.45f ? 1f : Mathf.Max(0.01f, 1f - (it.onTray - 0.45f) * 6f); it.t.localScale = Vector3.one * s;
                    if (it.onTray > 0.62f) { Object.Destroy(it.t.gameObject); items.RemoveAt(i); stacked = Mathf.Max(0, stacked - 1); }
                    continue;
                }
                it.v.y = Mathf.Max(it.v.y - 7f * dt, -maxFall); var p = it.t.localPosition + it.v * dt; p.z = Mathf.Lerp(p.z, 0, 1f - Mathf.Exp(-2.5f * dt)); it.t.localPosition = p;
                it.t.localRotation *= Quaternion.Euler(it.spin * 0.4f * dt, 0, it.spin * dt);
                bool bad = it.kind >= 2;
                if (it.v.y < 0 && p.y < TrayY + 0.25f && p.y > TrayY - 0.25f && Mathf.Abs(p.x - trayX) < 1.15f)
                {
                    if (bad)
                    {
                        Pop3D(it.kind == 2 ? "Yuck!" : "Eek!", p + Vector3.up * 0.6f, Hex(140, 120, 110), 1.1f); Poof(p, 0.7f, 10); Shake(0.3f);
                        Object.Destroy(it.t.gameObject); items.RemoveAt(i); LoseLife("Too many yucky catches!");
                    }
                    else
                    {
                        int pts = it.kind == 1 ? 2 : 1; Score += pts; Pop3D("+" + pts, p + Vector3.up * 0.7f, Hex(255, 120, 160)); Sfx("sfx_coin", 0.4f, 1.2f + 0.1f * pts);
                        Burst(p + Vector3.up * 0.2f, 10, new[] { new Color(1f, 0.92f, 0.55f), Color.white, Props3D.Pink }, 2f, 0.14f, 0.2f, 0.6f);
                        it.onTray = 0; it.trayOff = Quaternion.Inverse(tray.localRotation) * new Vector3(Mathf.Clamp(p.x - trayX, -0.8f, 0.8f), 0.06f + stacked * 0.12f, 0); stacked++;
                        it.t.localRotation = Quaternion.identity; tray.localPosition += Vector3.down * 0.06f;
                    }
                    if (Over) return; continue;
                }
                if (p.y < 0.02f)
                {
                    if (!bad)
                    {   // smash!
                        Pop3D("Crash!", p + Vector3.up * 0.8f, Hex(220, 90, 90), 1.2f); Shake(0.45f); Sfx("sfx_error", 0.5f, 1.3f);
                        Burst(p + Vector3.up * 0.1f, 22, new[] { Color.white, it.kind == 0 ? Bands[0] : Props3D.Pink, new Color(0.62f, 0.40f, 0.26f) }, 3.2f, 0.12f, 1.4f, 0.8f);
                        Object.Destroy(it.t.gameObject); items.RemoveAt(i); LoseLife("Too many cups broke!");
                    }
                    else { Poof(p, 0.5f, 6); Object.Destroy(it.t.gameObject); items.RemoveAt(i); }
                    if (Over) return;
                }
            }
        }
        public override void Frame(float dt)
        {
            tray.localPosition = new Vector3(tray.localPosition.x, Mathf.Lerp(tray.localPosition.y, TrayY, 1f - Mathf.Exp(-10f * dt)), 0);
            if (Over) cat.Freeze(true);
            camSmooth = CountingDown ? 1.6f : 3f;
            base.Frame(dt);
        }
    }
}
