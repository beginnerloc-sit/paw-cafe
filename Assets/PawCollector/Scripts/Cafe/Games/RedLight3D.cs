using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe.Games
{
    /// <summary>
    /// Red Light, Green Light — in 3D. A giant cat sits on a stump under a big tree at the end of a sunny race track and
    /// grooms with its back turned (green). Hold to tiptoe forward; when it spins round (red) everyone must freeze
    /// mid-step. Rival cats race in the other lanes and sometimes get caught (they topple over in a poof).
    /// Stray mode: the "watcher" is the shy stray — reach it without startling it, no rivals.
    /// </summary>
    public class RedLightGreenLight : Game3D
    {
        public override string Id => "redlight";
        public override string Title => "Red Light, Green Light";
        public override string HowTo => "Hold to sneak forward on green. Let go on red — don't get caught!";
        public override string IconPath => "Games/toystar";
        public override int Unlock => 2;
        public override int[] Stars => new[] { 100, 250, 350 };
        public override Color TileCol => Hex(255, 226, 214);
        public override float CoinsPerPoint => 0.12f;
        protected override Color Sky => new(0.70f, 0.86f, 1f);

        const float FinishZ = 18f, Speed = 1.45f, Lane = 1.25f;
        class Runner { public Puppet p; public float z, v, speed, react, fallT = -1, x; public bool caught, done, slip; }
        readonly List<Runner> rivals = new(); Runner me;
        Puppet watcher; Transform watcherT; float watcherYaw, watcherZ;
        Renderer redBulb, greenBulb; Transform ribbon;
        enum Phase { Green, Turning, Red }
        Phase phase; float phaseT, phaseLen, fakeAt = -1; bool holding; float caughtT = -1, winT = -1, orbit;
        bool Stray => Mode == "stray";

        protected override void Build()
        {
            TimeLeft = Stray ? 45f : 40f;
            var rnd = new System.Random(7); float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var k = new MeshKit();
            // ground, soft patches and a sandy race track with lanes
            k.Disc(new Vector3(0, 0, 10), 70f, new Color(0.60f, 0.86f, 0.50f), new Color(0.50f, 0.78f, 0.44f), 64);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(R(-22, 22), 0.01f, R(-8, 40)); if (Mathf.Abs(p.x) < 5f && p.z < 21f) continue;
                k.Ellipsoid(p, new Vector3(R(1f, 2.4f), 0.006f, R(0.8f, 1.8f)), i % 2 == 0 ? new Color(0.52f, 0.80f, 0.46f) : new Color(0.68f, 0.90f, 0.57f), 14, 2);
            }
            int lanes = Stray ? 1 : 5; float half = Stray ? 1.3f : lanes * Lane / 2f + 0.5f;
            k.Box(new Vector3(0, 0.015f, 8.6f), new Vector3(half * 2, 0.03f, 25f), new Color(0.98f, 0.88f, 0.70f));
            k.Box(new Vector3(-half, 0.03f, 8.6f), new Vector3(0.14f, 0.05f, 25f), new Color(0.92f, 0.78f, 0.60f));
            k.Box(new Vector3(half, 0.03f, 8.6f), new Vector3(0.14f, 0.05f, 25f), new Color(0.92f, 0.78f, 0.60f));
            if (!Stray) for (int l = 1; l < lanes; l++) for (float z = -3.5f; z < FinishZ; z += 1.4f) k.Box(new Vector3(-half + 0.5f + l * Lane, 0.035f, z), new Vector3(0.07f, 0.02f, 0.7f), Color.white);
            k.Box(new Vector3(0, 0.04f, 0.6f), new Vector3(half * 2, 0.02f, 0.18f), Color.white);                       // start line
            for (int i = 0; i < Mathf.CeilToInt(half * 2 / 0.4f); i++) for (int j = 0; j < 2; j++)              // chequered finish line
                k.Box(new Vector3(-half + 0.2f + i * 0.4f, 0.04f, FinishZ + 0.2f + j * 0.4f), new Vector3(0.4f, 0.02f, 0.4f), (i + j) % 2 == 0 ? Color.white : new Color(0.35f, 0.30f, 0.38f));
            // finish arch (pink poles + rainbow arc) with bunting
            float ax = half + 0.35f;
            foreach (var sx in new[] { -1f, 1f }) { k.Cylinder(new Vector3(sx * ax, 0, FinishZ), 0.16f, 3.0f, new Color(1f, 0.55f, 0.70f), 12); k.Sphere(new Vector3(sx * ax, 3.05f, FinishZ), 0.24f, new Color(1f, 0.85f, 0.35f)); }
            k.Arc(new Vector3(0, 3.0f, FinishZ), ax, 0.34f, 0.3f, 0f, 180f, 22, i => Scenery.Party[(i / 4) % Scenery.Party.Length]);
            // little bunting posts along both sides of the track
            for (int s = -1; s <= 1; s += 2)
            {
                for (float z = -2f; z < FinishZ; z += 5f) k.Cylinder(new Vector3(s * (half + 0.9f), 0, z), 0.06f, 1.6f, new Color(1f, 0.97f, 0.9f), 6);
                for (float z = -2f; z + 5f < FinishZ + 1f; z += 5f) Scenery.Bunting(k, new Vector3(s * (half + 0.9f), 1.58f, z), new Vector3(s * (half + 0.9f), 1.58f, z + 5f), 0.35f, 7, 0.3f);
            }
            // the watcher's stump and the big tree behind it
            watcherZ = Stray ? FinishZ + 1.6f : FinishZ + 4.2f;
            if (!Stray)
            {
                k.Cylinder(new Vector3(0, 0, watcherZ), 1.5f, 0.75f, new Color(0.62f, 0.44f, 0.30f), 18);
                k.Disc(new Vector3(0, 0.76f, watcherZ), 1.4f, new Color(0.92f, 0.78f, 0.56f), new Color(0.80f, 0.62f, 0.42f), 18);
            }
            var tz = watcherZ + 4.5f; k.Cylinder(new Vector3(0, 0, tz), 0.9f, 5.5f, Scenery.Trunk, 14, true, 0.6f);
            k.Ellipsoid(new Vector3(0, 7.2f, tz), new Vector3(4.6f, 3.0f, 3.6f), Scenery.Leaf1, 18, 12);
            k.Ellipsoid(new Vector3(-3.2f, 6.2f, tz - 0.4f), new Vector3(2.6f, 2.1f, 2.4f), Scenery.Leaf2, 14, 10);
            k.Ellipsoid(new Vector3(3.3f, 6.4f, tz - 0.2f), new Vector3(2.7f, 2.2f, 2.4f), Scenery.Leaf3, 14, 10);
            k.Ellipsoid(new Vector3(0.4f, 9.3f, tz), new Vector3(2.8f, 1.8f, 2.4f), Scenery.Leaf2, 14, 10);
            // side scenery: bushes, trees, flowers, hills, clouds
            for (int i = 0; i < 16; i++) { float s = i % 2 == 0 ? -1 : 1; Scenery.Bush(k, new Vector3(s * R(half + 1.6f, half + 3.5f), 0, R(-4, 26)), R(0.9f, 1.3f), rnd); }
            for (int i = 0; i < 18; i++) { float s = i % 2 == 0 ? -1 : 1; Scenery.Tree(k, new Vector3(s * R(7.5f, 16f), 0, R(-4, 34)), R(1.1f, 1.6f), rnd.NextDouble() < 0.4); }
            for (int i = 0; i < 120; i++) { var p = new Vector3(R(-14, 14), R(0.08f, 0.2f), R(-5, 30)); if (Mathf.Abs(p.x) < half + 0.6f) continue; Scenery.Flower(k, p, R(0.06f, 0.1f), Scenery.Petals[rnd.Next(Scenery.Petals.Length)], true); }
            for (int i = 0; i < 9; i++) Scenery.Hill(k, new Vector3(-36 + i * 9 + R(-2, 2), -2.5f, R(44, 50)), new Vector3(R(8, 12), R(6, 9), 5), i % 3 == 0 ? Scenery.Hill1 : i % 3 == 1 ? Scenery.Hill2 : Scenery.Hill3);
            for (int i = 0; i < 6; i++) Scenery.Cloud(k, new Vector3(-30 + i * 12 + R(-3, 3), R(12, 17), 56), R(1.8f, 2.6f));
            MakeMesh(k, "Course");
            // the signal lamp next to the watcher: two bulbs we can light up
            var lx = Stray ? 2.2f : 2.6f; var lz = watcherZ - 0.6f;
            k.Cylinder(new Vector3(lx, 0, lz), 0.09f, 2.6f, new Color(0.55f, 0.50f, 0.58f), 8);
            k.Box(new Vector3(lx, 2.95f, lz), new Vector3(0.62f, 1.25f, 0.4f), new Color(0.40f, 0.36f, 0.45f));
            MakeMesh(k, "LampPost");
            redBulb = Bulb(new Vector3(lx, 3.25f, lz - 0.2f)); greenBulb = Bulb(new Vector3(lx, 2.65f, lz - 0.2f));

            // the watcher: giant real cat (or the stray), back turned
            var wid = Stray && Partner != null ? Partner.Id : PickWatcher();
            watcher = Cat(wid, new Vector3(0, Stray ? 0 : 0.76f, watcherZ), 0f, Stray ? 1.35f : 3.1f); watcherT = watcher?.T; watcherYaw = 0;
            watcher?.Play("Idle_Sit", 0f);
            // runners
            var pool = GameData.Cats.Where(c => (Hero == null || c.Id != Hero.Id) && c.Id != wid && Resources.Load<GameObject>("CafeCats/CafeCat_" + c.Id)).OrderBy(_ => Random.value).ToList();
            int myLane = Stray ? 0 : 2;
            for (int i = 0; i < lanes; i++)
            {
                bool isMe = i == myLane; float x = Stray ? 0 : (i - (lanes - 1) / 2f) * Lane;
                var def = isMe ? Hero : pool.Count > 0 ? pool[i % pool.Count] : null;
                var r = new Runner { x = x, z = 0, speed = isMe ? Speed : Rnd(1.05f, 1.55f), react = Rnd(0.05f, 0.5f) };
                r.p = Cat(def != null ? def.Id : "Mochi", new Vector3(x, 0, 0), 0f, isMe ? 1.05f : 0.95f);
                r.p?.Play("Idle_Stand", 0f);
                if (isMe) me = r; else rivals.Add(r);
            }
            // camera: a sweeping intro from high above the watcher down to behind your cat
            ChaseCam(); SnapCamera(new Vector3(6f, 7.5f, watcherZ - 3f), new Vector3(0, 1.5f, watcherZ), 46f); camSmooth = 1.6f;
            SetPhase(Phase.Green); Banner = null;
        }
        static string PickWatcher()
        {
            var big = GameData.Cats.Where(c => c.Rarity >= Rarity.Rare && Resources.Load<GameObject>("CafeCats/CafeCat_" + c.Id)).ToList();
            return big.Count > 0 ? big[Random.Range(0, big.Count)].Id : "Midnight";
        }
        Renderer Bulb(Vector3 at)
        {
            var k = new MeshKit(); k.Sphere(Vector3.zero, 0.22f, Color.white, 14, 10);
            var g = MakeMesh(k, "Bulb", null, false); g.transform.localPosition = at; return g.GetComponent<Renderer>();
        }

        void ChaseCam()
        {
            var p = new Vector3(me.x, 0, me.z);
            camPos = p + new Vector3(0.0f, 2.4f, -4.4f); camLook = p + new Vector3(0, 0.7f, 5.5f); camFov = 42f; camRoll = 0f;
        }

        void SetPhase(Phase p)
        {
            phase = p; phaseT = 0; fakeAt = -1;
            float hurry = Mathf.Clamp01(T / 30f);
            phaseLen = p == Phase.Green ? Rnd(1.4f, 3.4f) * (1f - 0.3f * hurry) : p == Phase.Turning ? 0.55f : Rnd(1.3f, 2.5f);
            if (p == Phase.Green && phaseLen > 2.2f && Random.value < 0.45f) fakeAt = Rnd(0.6f, phaseLen - 0.9f);   // a sneaky head twitch
            Tint(redBulb, p == Phase.Green ? new Color(0.45f, 0.25f, 0.28f) : new Color(1f, 0.25f, 0.3f), p != Phase.Green);
            Tint(greenBulb, p == Phase.Green ? new Color(0.35f, 1f, 0.45f) : new Color(0.25f, 0.42f, 0.3f), p == Phase.Green);
            if (p == Phase.Red)
            {
                Sfx("sfx_meow" + Random.Range(1, 4), 0.8f, Stray ? 1.3f : 0.7f); watcher?.Squash(-0.35f); Shake(0.35f);
                Danger = true; Banner = Stray ? "It's looking!" : "RED LIGHT!"; BannerCol = Hex(255, 96, 110);
                foreach (var r in rivals) r.slip = !r.caught && !r.done && Random.value < 0.18f + 0.25f * hurry;
            }
            else if (p == Phase.Turning) { Banner = Stray ? "Careful…" : "Red light…"; BannerCol = Hex(255, 190, 80); Sfx("sfx_tap", 0.5f, 0.7f); }
            else { Danger = false; Banner = Stray ? "Sneak!" : "Green light!"; BannerCol = Hex(110, 220, 130); Sfx("sfx_tap", 0.35f, 1.5f); watcher?.Squash(0.2f); }
        }
        public override void Down(Vector2 p) { holding = true; }
        public override void Up(Vector2 p) { holding = false; }

        protected override void Tick(float dt)
        {
            if (caughtT >= 0 || winT >= 0) return;
            phaseT += dt;
            if (phase == Phase.Green && phaseT > 0.9f) Banner = null;
            if (phaseT >= phaseLen) SetPhase(phase == Phase.Green ? Phase.Turning : phase == Phase.Turning ? Phase.Red : Phase.Green);
            bool red = phase == Phase.Red;

            // me — accelerate / brake quickly; the walk freezes mid-step when you let go
            me.v = Mathf.MoveTowards(me.v, holding ? me.speed : 0f, (holding ? 7f : 14f) * dt);
            me.z += me.v * dt; bool moving = me.v > 0.05f;
            Move(me, moving, 1.9f);
            if (red && moving && phaseT > 0.2f) { Caught(); return; }
            if (me.z >= FinishZ) { Win(); return; }
            Score = Mathf.RoundToInt(Mathf.Clamp01(me.z / FinishZ) * 99f);

            // rivals tiptoe on green, mostly freeze on red — some slip up
            foreach (var r in rivals)
            {
                if (r.caught || r.done) continue;
                bool go = (phase == Phase.Green && phaseT > r.react * 0.6f) || (phase == Phase.Turning && phaseT < r.react) || (red && r.slip && phaseT < 0.5f);
                r.v = Mathf.MoveTowards(r.v, go ? r.speed : 0f, 10f * dt); r.z += r.v * dt;
                Move(r, r.v > 0.05f, 1.7f);
                if (red && r.slip && phaseT > 0.25f) { RivalCaught(r); continue; }
                if (r.z >= FinishZ) { r.done = true; r.p?.Play("Happy", 0.1f); Pop3D("Made it!", new Vector3(r.x, 1.4f, r.z), Hex(110, 200, 130), 0.8f); }
            }
        }
        void Move(Runner r, bool moving, float animSpeed)
        {
            if (r.p == null) return;
            if (moving) { r.p.Play("Walk", 0.08f); r.p.Freeze(false, animSpeed); }
            else if (r.p.Clip == "Walk") r.p.Freeze(true);   // frozen mid-step — the comedy pose
            r.p.T.localPosition = new Vector3(r.x, 0, r.z);
        }
        void Caught()
        {
            caughtT = 0; holding = false; Danger = true;
            Pop3D(Stray ? "Spotted!" : "Caught!", new Vector3(me.x, 1.6f, me.z), Hex(240, 70, 90), 1.4f);
            Sfx("sfx_error", 0.8f); Shake(1f); me.p?.Squash(0.4f); Poof(new Vector3(me.x, 0.2f, me.z), 1.1f, 16);
            if (Stray) { watcher?.Play("Walk", 0.1f); }
        }
        void RivalCaught(Runner r)
        {
            r.caught = true; r.fallT = 0; r.p?.Freeze(true);
            Pop3D("Caught!", new Vector3(r.x, 1.5f, r.z), Hex(240, 70, 90), 0.9f); Sfx("sfx_pop", 0.5f, 0.8f); Shake(0.25f);
        }
        void Win()
        {
            winT = 0; me.p?.Freeze(false); me.p?.Play("Happy", 0.1f, true); me.p?.Squash(-0.35f);
            Score = 100 + Mathf.RoundToInt(TimeLeft * 7f); Danger = false; Banner = null;
            Confetti(new Vector3(-FinishArchX, 3.1f, FinishZ), 70); Confetti(new Vector3(FinishArchX, 3.1f, FinishZ), 70);
            Burst(new Vector3(me.x, 0.8f, me.z), 24, Scenery.Party, 3f, 0.22f, 0.4f, 1f);
            Sfx("sfx_levelup", 0.7f); Fx.Hearts(W2(new Vector3(me.x, 1.2f, me.z)), 10, 2.5f);
            Finish(true, Stray ? "You made it — the stray sniffs your paw!" : "You made it across!");
        }
        float FinishArchX => (Stray ? 1.3f : 5 * Lane / 2f + 0.5f) + 0.35f;
        protected override void TimeUp() { Banner = null; Danger = false; Finish(false, "Time's up!"); }

        public override void Frame(float dt)
        {
            // watcher: turn round on red (with a little overshoot), back on green, sneaky twitches
            if (watcherT)
            {
                float target = phase == Phase.Green ? 0f : 180f;
                if (phase == Phase.Turning) target = 180f * EaseOutBack(phaseT / phaseLen);
                if (phase == Phase.Green && fakeAt > 0 && phaseT > fakeAt && phaseT < fakeAt + 0.5f) { target = Mathf.Sin((phaseT - fakeAt) / 0.5f * Mathf.PI) * 38f; if (phaseT - dt <= fakeAt) Sfx("sfx_tap", 0.4f, 0.6f); }
                watcherYaw = phase == Phase.Turning ? target : Damp(watcherYaw, target, 14f, dt);
                if (Stray && caughtT >= 0) { watcherYaw = Damp(watcherYaw, 0f, 6f, dt); watcherT.localPosition += Vector3.forward * dt * 2.2f; }
                watcherT.localRotation = Quaternion.Euler(0, watcherYaw, 0);
            }
            // caught rivals tip over, poof, and shrink away
            foreach (var r in rivals)
            {
                if (!r.caught || r.p == null || r.fallT < 0) continue;
                r.fallT += dt; float side = r.x < 0 ? 1 : -1;
                r.p.T.localRotation = Quaternion.Euler(0, 0, side * 90f * EaseOutBack(Mathf.Clamp01(r.fallT / 0.35f)));
                if (r.fallT > 0.35f && r.fallT - dt <= 0.35f) Poof(new Vector3(r.x, 0.3f, r.z), 0.9f);
                if (r.fallT > 1.2f) { r.p.BaseScale = Mathf.Max(0.001f, r.p.BaseScale - dt * 3f); if (r.p.BaseScale <= 0.01f) { r.p.Go.SetActive(false); r.fallT = -1; } }
            }
            // camera
            if (me != null)
            {
                var p = new Vector3(me.x, 0, me.z);
                if (winT >= 0)
                {
                    winT += dt; orbit = Mathf.Sin(winT * 0.45f) * 0.8f; camSmooth = 3f;
                    camPos = p + new Vector3(Mathf.Sin(orbit) * 3.4f, 1.4f, -Mathf.Cos(orbit) * 3.4f); camLook = p + new Vector3(0, 0.6f, 0); camFov = 40f; camRoll = 0;
                    if (me.p != null) me.p.T.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(me.p.T.localEulerAngles.y, 180f, 1f - Mathf.Exp(-5f * dt)), 0);   // turn round to celebrate at the camera
                    if (winT > 1.2f && winT - dt <= 1.2f) Confetti(p + Vector3.up * 0.5f, 40, 0.8f);
                }
                else if (caughtT >= 0)
                {
                    caughtT += dt; camSmooth = 6f;
                    camPos = p + new Vector3(0.2f, 1.7f, -2.6f); camLook = p + new Vector3(0, 0.45f, 0.3f); camFov = 36f; camRoll = -5f;
                    if (me.p != null) me.p.T.localRotation = Quaternion.Euler(0, 0, -80f * EaseOutBack(Mathf.Clamp01((caughtT - 0.25f) / 0.35f)));
                    if (caughtT > 0.6f && caughtT - dt <= 0.6f) Poof(p + Vector3.up * 0.2f, 1.2f, 14);
                    if (caughtT > 1.1f && !Over) Finish(false, Stray ? "Oh no — the stray ran off scared!" : "Caught moving on red!");
                }
                else if (CountingDown) { ChaseCam(); camSmooth = 1.8f; }
                else if (phase == Phase.Red || phase == Phase.Turning)
                {
                    camSmooth = 3.2f;
                    camPos = p + new Vector3(0f, 1.55f, -3.1f); camLook = Vector3.Lerp(p + Vector3.up * 0.8f, new Vector3(0, 2.6f, watcherZ), 0.65f); camFov = 33f; camRoll = 4.5f;
                }
                else { camSmooth = 3.5f; ChaseCam(); }
            }
            base.Frame(dt);
        }
    }
}
