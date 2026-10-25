using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Hide &amp; Seek — 3D. Your cat hides somewhere in a cosy living room: in a cardboard box, behind the sofa, under the
    /// tablecloth, behind the curtain... Find it and tap it before time runs out. Tapping the wrong hiding place wiggles it
    /// and costs a second. Six rounds, each a little quicker.
    /// </summary>
    public class HideAndSeek : Game3D
    {
        public override string Id => "hide";
        public override string Title => "Hide & Seek";
        public override string HowTo => "Your cat is hiding somewhere in the room. Find it and tap it!";
        public override string IconPath => "Games/cloud";
        public override int Unlock => 5;
        public override int[] Stars => new[] { 150, 300, 450 };
        public override Color TileCol => Hex(214, 242, 232);
        public override float CoinsPerPoint => 0.15f;
        protected override Color Sky => new(0.98f, 0.92f, 0.86f);
        const int Rounds = 6;

        class Spot { public Vector3 pos; public float yaw, sink; public Transform obj; public Vector3 tap; public float wig; }
        readonly List<Spot> spots = new(); Spot cur, last; Puppet cat; Label roundL;
        int round; float roundLen, revealT = -1, foundT = -1, orbit;

        protected override void Build()
        {
            Lives = 3; MaxLives = 3;
            var k = new MeshKit();
            for (int i = 0; i < 34; i++) k.Box(new Vector3(-8.5f + i * 0.5f + 0.25f, -0.1f, 1f), new Vector3(0.49f, 0.2f, 13f), i % 2 == 0 ? new Color(0.88f, 0.70f, 0.52f) : new Color(0.83f, 0.65f, 0.47f));
            k.Disc(new Vector3(0.2f, 0.012f, 1.2f), 3.4f, new Color(0.80f, 0.92f, 0.86f), new Color(0.68f, 0.86f, 0.78f), 48, 0.62f);
            Color wall = new(0.96f, 0.92f, 1f), stripe = new(0.92f, 0.88f, 0.98f);
            k.Box(new Vector3(0, 3f, 5.2f), new Vector3(17f, 6f, 0.2f), wall);
            for (int i = 0; i < 20; i++) k.Box(new Vector3(-8.5f + i * 0.9f, 3f, 5.09f), new Vector3(0.3f, 6f, 0.02f), stripe);
            k.Box(new Vector3(0, 0.25f, 5.05f), new Vector3(17f, 0.5f, 0.08f), Props3D.White);
            k.Box(new Vector3(-7.6f, 3f, 1f), new Vector3(0.2f, 6f, 8.6f), new Color(0.98f, 0.90f, 0.88f));
            // window between the curtains, pictures, a hanging lamp
            k.Box(new Vector3(2.2f, 2.7f, 5.08f), new Vector3(2.4f, 2.2f, 0.06f), Color.white); k.Box(new Vector3(2.2f, 2.7f, 5.04f), new Vector3(2.1f, 1.9f, 0.04f), new Color(0.70f, 0.87f, 1f));
            k.Box(new Vector3(2.2f, 2.7f, 5.0f), new Vector3(0.07f, 1.9f, 0.04f), Color.white); k.Box(new Vector3(2.2f, 2.7f, 5.0f), new Vector3(2.1f, 0.07f, 0.04f), Color.white);
            k.Box(new Vector3(2.2f, 4.0f, 4.9f), new Vector3(3.8f, 0.08f, 0.08f), new Color(0.8f, 0.6f, 0.45f));
            for (int i = 0; i < 3; i++) { var c = new Vector3(-4.6f + i * 1.3f, 3.2f + (i % 2) * 0.4f, 5.07f); k.Box(c, new Vector3(0.9f, 0.7f, 0.05f), new Color(0.95f, 0.78f, 0.45f)); k.Box(c + Vector3.back * 0.03f, new Vector3(0.74f, 0.54f, 0.03f), Scenery.Party[i * 2]); }
            k.Stick(new Vector3(0.4f, 6f, 1.8f), new Vector3(0.4f, 4.4f, 1.8f), 0.03f, new Color(0.4f, 0.36f, 0.4f)); k.Cone(new Vector3(0.4f, 4.05f, 1.8f), 0.55f, 0.45f, new Color(1f, 0.78f, 0.45f), 16);
            MakeMesh(k, "Room");

            // ---- hiding places (each its own object so a wrong tap can wiggle it)
            Color sofa = new(1f, 0.66f, 0.62f), sofaD = new(0.92f, 0.56f, 0.54f);
            k.Box(new Vector3(0, 0.42f, 0), new Vector3(3.4f, 0.5f, 1.1f), sofa); k.Box(new Vector3(0, 0.95f, 0.42f), new Vector3(3.4f, 1.0f, 0.3f), sofaD);
            foreach (var s in new[] { -1f, 1f }) k.Box(new Vector3(s * 1.78f, 0.62f, 0), new Vector3(0.32f, 0.85f, 1.1f), sofaD);
            for (int i = 0; i < 2; i++) k.Ellipsoid(new Vector3(-0.8f + i * 1.6f, 0.76f, -0.05f), new Vector3(0.72f, 0.16f, 0.45f), sofa, 12, 6);
            k.Ellipsoid(new Vector3(-1.1f, 1.08f, 0.15f), new Vector3(0.3f, 0.3f, 0.12f), new Color(0.6f, 0.8f, 1f), 10, 6);
            var sofaT = Obj(k, "Sofa", new Vector3(-3.6f, 0, 3.7f), 0f);
            spots.Add(new Spot { pos = new Vector3(-1.75f, 0, 4.6f), yaw = 150f, obj = sofaT, tap = new Vector3(-3.6f, 0.9f, 3.7f) });
            spots.Add(new Spot { pos = new Vector3(-5.5f, 0, 4.5f), yaw = 210f, obj = sofaT, tap = new Vector3(-4.9f, 0.9f, 3.7f) });
            // table with a long tablecloth
            Color cloth = new(1f, 0.96f, 0.86f), clothD = new(0.95f, 0.86f, 0.72f);
            k.Box(new Vector3(0, 1.0f, 0), new Vector3(2.0f, 0.08f, 1.3f), cloth);
            k.Box(new Vector3(0, 0.66f, -0.66f), new Vector3(2.04f, 0.66f, 0.03f), cloth); k.Box(new Vector3(0, 0.66f, 0.66f), new Vector3(2.04f, 0.66f, 0.03f), cloth);
            k.Box(new Vector3(-1.01f, 0.66f, 0), new Vector3(0.03f, 0.66f, 1.3f), clothD); k.Box(new Vector3(1.01f, 0.66f, 0), new Vector3(0.03f, 0.66f, 1.3f), clothD);
            for (int i = 0; i < 6; i++) k.Box(new Vector3(-0.85f + i * 0.34f, 0.34f, -0.68f), new Vector3(0.14f, 0.04f, 0.02f), Props3D.Pink);
            Props3D.Cup(k, new Vector3(-0.4f, 1.04f, 0.1f), 1.3f, Props3D.Pink); Props3D.Vase(k, new Vector3(0.45f, 1.04f, 0.1f), 1f, new Color(0.6f, 0.78f, 1f));
            var tableT = Obj(k, "Table", new Vector3(0.6f, 0, 1.9f), 0f);
            spots.Add(new Spot { pos = new Vector3(0.6f, 0, 1.9f), yaw = 200f, obj = tableT, tap = new Vector3(0.6f, 0.6f, 1.9f) });
            // curtains by the window
            Color cur1 = new(0.70f, 0.82f, 1f);
            for (int i = 0; i < 3; i++) k.Ellipsoid(new Vector3(-0.2f + i * 0.2f, 2.05f, 0), new Vector3(0.22f, 1.75f, 0.14f), i == 1 ? new Color(0.62f, 0.76f, 0.98f) : cur1, 10, 10);
            var curtL = Obj(k, "CurtainL", new Vector3(0.75f, 0, 4.7f), 0f);
            for (int i = 0; i < 3; i++) k.Ellipsoid(new Vector3(-0.2f + i * 0.2f, 2.05f, 0), new Vector3(0.22f, 1.75f, 0.14f), i == 1 ? new Color(0.62f, 0.76f, 0.98f) : cur1, 10, 10);
            var curtR = Obj(k, "CurtainR", new Vector3(3.65f, 0, 4.7f), 0f);
            spots.Add(new Spot { pos = new Vector3(0.95f, 0, 4.92f), yaw = 90f, obj = curtL, tap = new Vector3(0.75f, 1f, 4.7f) });
            spots.Add(new Spot { pos = new Vector3(3.45f, 0, 4.92f), yaw = 270f, obj = curtR, tap = new Vector3(3.65f, 1f, 4.7f) });
            // cardboard boxes
            Props3D.CardBox(k, Vector3.zero, new Vector3(1.25f, 0.62f, 1.05f), 0f); var boxA = Obj(k, "BoxA", new Vector3(3.4f, 0, 0.2f), 12f);
            Props3D.CardBox(k, Vector3.zero, new Vector3(1.15f, 0.6f, 1.0f), 0f); var boxB = Obj(k, "BoxB", new Vector3(-2.2f, 0, -0.9f), -18f);
            Props3D.CardBox(k, Vector3.zero, new Vector3(1.1f, 0.58f, 0.95f), 0f); var boxC = Obj(k, "BoxC", new Vector3(5.8f, 0, 2.4f), 30f);
            spots.Add(new Spot { pos = new Vector3(3.4f, 0, 0.25f), yaw = 190f, sink = 0.08f, obj = boxA, tap = new Vector3(3.4f, 0.5f, 0.2f) });
            spots.Add(new Spot { pos = new Vector3(-2.2f, 0, -0.85f), yaw = 160f, sink = 0.08f, obj = boxB, tap = new Vector3(-2.2f, 0.5f, -0.9f) });
            spots.Add(new Spot { pos = new Vector3(5.8f, 0, 2.45f), yaw = 220f, sink = 0.08f, obj = boxC, tap = new Vector3(5.8f, 0.5f, 2.4f) });
            // real café furniture: big plant, beanbag, cat tree
            var plant = Furniture("Plant", new Vector3(-6.4f, 0, 1.4f), 0f, 1.8f); var bean = Furniture("Beanbag", new Vector3(5.4f, 0, -1.6f), 200f, 1.4f);
            if (plant) spots.Add(new Spot { pos = new Vector3(-6.7f, 0, 2.1f), yaw = 120f, obj = plant.transform, tap = new Vector3(-6.4f, 1f, 1.4f) });
            if (bean) spots.Add(new Spot { pos = new Vector3(5.7f, 0, -0.75f), yaw = 210f, obj = bean.transform, tap = new Vector3(5.4f, 0.5f, -1.6f) });
            Furniture("CatTree", new Vector3(6.6f, 0, 4.3f), 200f); Furniture("Rug", new Vector3(-4.6f, 0, -1.2f), 0f);

            cat = Cat(Hero != null ? Hero.Id : "Mochi", Vector3.zero, 180f, 0.9f);
            roundL = new Label(""); roundL.AddToClassList("ghide-round"); roundL.pickingMode = PickingMode.Ignore; S.Add(roundL);
            camPos = new Vector3(0, 5.8f, -7.6f); camLook = new Vector3(0, 0.5f, 1.5f); camFov = 40f;
            SnapCamera(new Vector3(0, 2f, -3f), new Vector3(0, 1f, 2f), 44f); camSmooth = 1.6f;
            round = 0; NextRound();
        }
        Transform Obj(MeshKit k, string name, Vector3 pos, float yaw) { var t = MakeMesh(k, name).transform; t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0); return t; }

        void NextRound()
        {
            round++; if (round > Rounds) { Finish(true, "Found every time!"); return; }
            roundL.text = $"Round {round} of {Rounds}";
            roundLen = Mathf.Max(7f, 15f - round * 1.4f); TimeLeft = roundLen; revealT = -1; foundT = -1;
            Spot s; int guard = 0; do { s = spots[Random.Range(0, spots.Count)]; } while ((s == last || (last != null && s.obj == last.obj)) && guard++ < 20);
            cur = last = s;
            cat.T.localPosition = s.pos + Vector3.down * s.sink; cat.T.localRotation = Quaternion.Euler(0, s.yaw + Rnd(-15f, 15f), 0);
            cat.Play(s.sink > 0 ? "Idle_Sit" : Random.value < 0.5f ? "Idle_Sit" : "Sleep", 0f, true);
            Banner = round == 1 ? "Ready or not…" : "Hide again!"; BannerCol = Hex(140, 200, 170); bannerT = 0.9f;
            Sfx("sfx_meow" + Random.Range(1, 4), 0.35f, 1.4f);
        }
        float bannerT;
        Vector3 Head => cat.T.localPosition + Vector3.up * 0.55f;
        public override void Down(Vector2 p)
        {
            if (revealT >= 0 || foundT >= 0) return;
            if (TapHits(p, Head, 0.55f, 70f))
            {
                int pts = 10 + Mathf.CeilToInt(TimeLeft * 5f); Score += pts; foundT = 0;
                Pop($"Found! +{pts}", p + new Vector2(0, -60), Hex(255, 110, 160), 1.3f); Sfx("sfx_meow" + Random.Range(1, 4), 0.7f, 1.2f); Sfx("sfx_coin", 0.5f, 1.3f);
                cat.Play("Happy", 0.1f, true); cat.Squash(-0.35f); Fx.Hearts(W2(Head + Vector3.up * 0.3f), 10, 2.4f); Confetti(cat.T.localPosition + Vector3.up * 0.3f, 30, 0.6f);
                if (cur.obj) cur.wig = 1f;
                return;
            }
            // wiggle the nearest hiding place you tapped on
            Spot best = null; float bd = 160f;
            foreach (var s in spots) { float d = (ToStage(s.tap) - p).magnitude; if (d < bd) { bd = d; best = s; } }
            if (best != null) { best.wig = 1f; foreach (var s in spots) if (s.obj == best.obj) s.wig = 1f; }
            TimeLeft = Mathf.Max(0.01f, TimeLeft - 1f); Pop("Not here…", p, Hex(255, 255, 255), 0.8f); Sfx("sfx_tap", 0.4f, 0.8f);
        }
        protected override void Tick(float dt)
        {
            if (bannerT > 0) { bannerT -= dt; if (bannerT <= 0) Banner = null; }
            if (foundT >= 0) { TimeLeft = Mathf.Max(TimeLeft, 0.5f); foundT += dt; if (foundT > 1.4f) NextRound(); return; }
            if (revealT >= 0) { revealT -= dt; TimeLeft = Mathf.Max(TimeLeft, 0.5f); if (revealT < 0) NextRound(); }
        }
        protected override void TimeUp()
        {
            if (revealT >= 0 || foundT >= 0) return;
            Pop3D("There it was!", Head + Vector3.up * 0.9f, Hex(255, 255, 255)); cat.Play("Idle_Stand", 0.1f); if (cur.obj) cur.wig = 1f;
            revealT = 1.4f; popOut = 0; LoseLife("Your cat is too good at hiding!");
            if (!Over) TimeLeft = 2f;
        }
        float popOut = -1;
        public override void Frame(float dt)
        {
            foreach (var s in spots) if (s.obj && s.wig > 0) { s.wig = Mathf.Max(0, s.wig - dt * 2.5f); var e = s.obj.localEulerAngles; s.obj.localRotation = Quaternion.Euler(Mathf.Sin(s.wig * 40f) * 4f * s.wig, e.y, Mathf.Sin(s.wig * 33f) * 5f * s.wig); }
            // the cat hops up out of its hiding place when found (or when time runs out)
            if ((foundT >= 0 || popOut >= 0) && cur != null)
            {
                float t = foundT >= 0 ? foundT : popOut; if (popOut >= 0) popOut += dt;
                float rise = Mathf.Clamp01(t / 0.35f); var basePos = cur.pos + Vector3.down * cur.sink * (1 - rise);
                cat.T.localPosition = basePos + Vector3.up * Mathf.Max(0, Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI)) * 0.7f;
                if (popOut > 1.4f) popOut = -1;
            }
            // camera: a gentle drift around the room; zoom to the cat when it's found
            orbit += dt * 0.25f;
            if (foundT >= 0 && cur != null) { camSmooth = 3.5f; var c = cat.T.localPosition; camPos = c + new Vector3(0f, 3.4f, -3.6f); camLook = c + Vector3.up * 0.6f; camFov = 40f; }
            else { camSmooth = CountingDown ? 1.6f : 2f; camPos = new Vector3(Mathf.Sin(orbit) * 1.4f, 5.8f, -7.6f); camLook = new Vector3(Mathf.Sin(orbit) * 0.5f, 0.5f, 1.5f); camFov = 40f; }
            base.Frame(dt);
        }
        public override void Cleanup() { roundL?.RemoveFromHierarchy(); base.Cleanup(); }
    }
}
