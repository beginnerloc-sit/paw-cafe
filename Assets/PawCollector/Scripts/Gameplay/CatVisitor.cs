using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PawCollector
{
    /// <summary>
    /// A visiting cat with a little mind of its own. It arrives over the fence, through the cat flap or from
    /// behind a bush, then roams the garden picking activities by personality (eat, play with toys, nap,
    /// sniff flowers, drink at the pond, chase butterflies, greet other cats) and reacts to the player's
    /// feather toy, petting and camera. Behaviour runs as a stack of interruptible coroutine "programs".
    /// </summary>
    public class CatVisitor : MonoBehaviour
    {
        public static readonly List<CatVisitor> All = new();

        public CatDef Def { get; private set; }
        public bool Sparkle { get; private set; }
        public bool SparkleUsed { get; set; }
        public int ShotsLeft { get; private set; } = GameData.ShotsPerVisit;
        public string Activity { get; private set; } = "";
        public bool InGarden { get; private set; }
        public bool Leaving { get; private set; }
        public bool CanPhoto => InGarden && ShotsLeft > 0;
        public bool CanPet => InGarden && Time.time > petCooldown && !(mode == Mode.Lure && Lure.I && Lure.I.Active);
        public bool Interactable => InGarden;
        public bool Busy => mode != Mode.Brain;
        public Spot Spot => spot;

        enum Mode { Script, Brain, Lure, Pet, Greet }
        Mode mode = Mode.Script;

        const float BaseWalk = 0.62f, Trot = 1.3f;
        static readonly HashSet<string> SeatedClips = new() { "Idle_Sit", "Happy", "Groom", "Sleep", "Bat", "SitDown" };

        Animator anim; Transform head;
        readonly Stack<IEnumerator> stack = new();
        float waitUntil;
        string current; bool seated;
        float yaw, look, groundY;
        Spot spot; Poi poi; CatVisitor partner;
        Entrance entry;
        readonly HashSet<Spot> usedSpots = new();
        readonly HashSet<CatVisitor> greeted = new();
        float stayUntil, nextLureCheck, petEnd, lastPat, lastPurr, lastCatch, petCooldown, nextGlance;
        int pats, petBond, playBond;
        bool petting, glancing, airborne;
        float strokes;
        ParticleSystem sparkleFx;
        GameManager G => GameManager.I;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            foreach (var t in GetComponentsInChildren<Transform>()) if (t.name == "head") head = t;
            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
            ReleaseClaims();
        }

        public void Begin(CatDef def, Entrance from, bool sparkle)
        {
            Def = def; Sparkle = sparkle; entry = from;
            transform.position = from.Outside; groundY = from.Outside.y;
            yaw = Yaw(from.Threshold - from.Outside);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            Play("Idle_Stand", 0f);
            if (sparkle) sparkleFx = Fx.Sparkles(transform);
            Run(EnterRoutine(), Mode.Script);
        }

        // ================================================================== program runner
        void Run(IEnumerator program, Mode m)
        {
            ReleaseClaims();
            stack.Clear(); stack.Push(program);
            mode = m; waitUntil = 0f; airborne = false;
            if (anim) anim.speed = 1f;
        }

        void Update()
        {
            ConsiderLure();
            if (Time.time < waitUntil) return;
            int guard = 0;
            while (stack.Count > 0 && guard++ < 64)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                var y = top.Current;
                if (y is IEnumerator sub) { stack.Push(sub); continue; }
                if (y is float f) waitUntil = Time.time + f;
                return;
            }
            if (stack.Count == 0 && !Leaving) Run(Brain(), Mode.Brain);
        }


        void ReleaseClaims()
        {
            if (spot && spot.Occupant == this) spot.Occupant = null;
            spot = null;
            if (poi != null && poi.User == this) poi.User = null;
            poi = null;
            if (partner && partner.partner == this) partner.partner = null;
            partner = null;
        }

        // ================================================================== animation & motion helpers
        CatFace face;
        void Play(string clip, float fade = 0.2f, bool restart = false)
        {
            if (!face) face = GetComponentInChildren<CatFace>();
            if (face) face.EyesClosed = clip == "Sleep";
            if (!anim || (current == clip && !restart)) return;
            current = clip;
            if (restart) anim.CrossFadeInFixedTime(clip, fade, 0, 0f); else anim.CrossFadeInFixedTime(clip, fade);
        }

        static float Yaw(Vector3 d) { d.y = 0; return d.sqrMagnitude < 1e-6f ? 0f : Quaternion.LookRotation(d).eulerAngles.y; }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void TurnTo(float targetYaw, float rate)
        {
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-rate * Time.deltaTime));
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        void Face(Vector3 p, float rate = 8f) { var d = Flat(p - transform.position); if (d.sqrMagnitude > 1e-4f) TurnTo(Yaw(d), rate); }

        float FaceCameraYaw(float jitter = 14f)
        {
            var cam = CameraRig.I ? CameraRig.I.BasePosition : transform.position + Vector3.back;
            return Yaw(cam - transform.position) + Random.Range(-jitter, jitter);
        }

        /// <summary>Cats on the lawn gently keep personal space instead of walking through each other.</summary>
        // body as two circles (chest & haunch) so long cats side by side or nose-to-tail don't interpenetrate
        const float BodyR = 0.19f, BodyHalf = 0.14f;   // BodyR ≈ half a cat head's width
        Vector3 Front => transform.position + transform.forward * BodyHalf * transform.localScale.x / 0.8f;
        Vector3 Back => transform.position - transform.forward * BodyHalf * transform.localScale.x / 0.8f;
        bool Pinned => !InGarden || mode == Mode.Script || spot || airborne;

        /// <summary>Push apart from any cat whose body overlaps ours; mobile cats yield to pinned ones.</summary>
        void Separate()
        {
            if (Pinned) return;
            foreach (var o in All)
            {
                if (o == this || !o.InGarden) continue;
                float need = o == partner ? BodyR * 1.1f : BodyR * 2f;
                Vector3 best = default; float bestGap = float.MaxValue;
                foreach (var a in new[] { Front, Back })
                    foreach (var b in new[] { o.Front, o.Back })
                    {
                        var d = Flat(a - b); float gap = d.magnitude - need;
                        if (gap < bestGap) { bestGap = gap; best = d; }
                    }
                if (bestGap >= 0f) continue;
                var dir = best.sqrMagnitude > 1e-6f ? best.normalized : transform.right;
                float share = o.Pinned ? 1f : 0.5f;
                var p = transform.position + dir * (-bestGap) * share * Mathf.Min(1f, Time.deltaTime * 12f);
                if (GardenMap.I && GardenMap.I.Blocked(p, 0f)) continue;
                transform.position = p;
            }
        }

        /// <summary>Steer a walking direction around cats standing in the way.</summary>
        Vector3 Avoid(Vector3 dir)
        {
            var pos = transform.position; var steer = Vector3.zero;
            foreach (var o in All)
            {
                if (o == this || !o.InGarden || o == partner) continue;
                var to = Flat(o.transform.position - pos); float d = to.magnitude;
                if (d > 0.85f || d < 1e-3f) continue;
                float ahead = Vector3.Dot(dir, to / d);
                if (ahead < 0.35f) continue;
                var side = Vector3.Cross(Vector3.up, dir); if (Vector3.Dot(side, to) > 0f) side = -side;   // pass on the far side
                steer += side * (1f - d / 0.85f) * ahead * 1.6f;
            }
            return steer == Vector3.zero ? dir : (dir + steer).normalized;
        }

        IEnumerator Hold(float seconds, Func<bool> until = null)
        {
            float end = Time.time + seconds;
            while (Time.time < end && (until == null || !until())) yield return null;
        }

        IEnumerator HoldFacing(Vector3 p, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { Face(p, 6f); yield return null; }
        }

        IEnumerator HoldYaw(float targetYaw, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { TurnTo(targetYaw, 5f); yield return null; }
        }

        IEnumerator StandUp()
        {
            if (!seated) yield break;
            seated = false; Play("StandUp", 0.12f);
            yield return 0.38f;
            Play("Idle_Stand", 0.15f);
        }

        IEnumerator SitDown()
        {
            if (seated) yield break;
            seated = true; anim.speed = 1f; Play("SitDown", 0.12f);
            yield return 0.5f;
            Play("Idle_Sit", 0.2f);
        }

        IEnumerator SeatedClip(string clip, float seconds)
        {
            yield return SitDown();
            Play(clip, 0.25f, true);
            yield return seconds;
        }

        /// <summary>Walk (or trot) to dest, bending around obstacles and hopping up/down the porch.</summary>
        IEnumerator Go(Vector3 dest, float speed = BaseWalk, float stop = 0.04f, bool route = true, bool useGround = true)
        {
            yield return StandUp();
            var pts = route && GardenMap.I ? GardenMap.I.Route(transform.position, dest) : new List<Vector3> { dest };
            for (int i = 0; i < pts.Count; i++)
            {
                var wp = pts[i]; bool last = i == pts.Count - 1;
                float giveUp = Time.time + 25f;
                while (Time.time < giveUp)
                {
                    var pos = transform.position;
                    var to = Flat(wp - pos); float d = to.magnitude;
                    if (d <= (last ? stop : 0.15f)) break;
                    var dir = Avoid(to / d);
                    float ang = Mathf.Abs(Mathf.DeltaAngle(yaw, Yaw(dir)));
                    float sp = speed * Mathf.Clamp(1.25f - ang / 110f, 0.15f, 1f);   // slow down to turn instead of moonwalking
                    Play("Walk", 0.15f); anim.speed = Mathf.Max(0.5f, sp / BaseWalk);
                    var np = pos + dir * Mathf.Min(d, sp * Time.deltaTime);
                    if (useGround && GardenMap.I)
                    {
                        float gy = GardenMap.I.GroundY(np + dir * 0.14f);
                        if (Mathf.Abs(gy - groundY) > 0.06f)
                        {
                            if (ang < 35f)
                            {
                                var land = pos + dir * 0.34f; land.y = GardenMap.I.GroundY(land);
                                anim.speed = 1f;
                                yield return Jump(land, 0.13f, 0.32f, false);
                                continue;
                            }
                            np = pos;   // turn on the spot before hopping
                        }
                        else groundY = Mathf.MoveTowards(groundY, gy, Time.deltaTime);
                        np.y = groundY;
                    }
                    else np.y = Mathf.MoveTowards(pos.y, wp.y, Time.deltaTime);
                    transform.position = np;
                    TurnTo(Yaw(dir), 9f);
                    yield return null;
                }
            }
            anim.speed = 1f;
            Play("Idle_Stand", 0.2f);
        }

        /// <summary>One frame of steering toward a moving target (toy, butterfly). Returns the remaining distance.</summary>
        float StepToward(Vector3 target, float speed)
        {
            var pos = transform.position;
            var to = Flat(target - pos); float d = to.magnitude;
            if (d < 0.001f) return 0f;
            var dir = Avoid(to / d);
            float ang = Mathf.Abs(Mathf.DeltaAngle(yaw, Yaw(dir)));
            float sp = speed * Mathf.Clamp(1.25f - ang / 110f, 0.2f, 1f);
            Play("Walk", 0.12f); anim.speed = Mathf.Max(0.6f, sp / BaseWalk);
            var np = pos + dir * Mathf.Min(d, sp * Time.deltaTime);
            if (GardenMap.I) { np = GardenMap.I.Clamp(np); if (GardenMap.I.Blocked(np, 0f)) np = pos; groundY = Mathf.MoveTowards(groundY, GardenMap.I.GroundY(np), Time.deltaTime * 2f); }
            np.y = groundY;
            transform.position = np;
            TurnTo(Yaw(dir), 10f);
            return d;
        }

        /// <summary>Ballistic hop. With pounce=true plays the crouch / butt-wiggle first, then leaps.</summary>
        IEnumerator Jump(Vector3 to, float height, float dur, bool pounce, float windup = 0.5f, bool keepClip = false)
        {
            if (pounce)
            {
                yield return StandUp();
                Play("Pounce", 0.08f, true);
                float end = Time.time + windup;
                while (Time.time < end) { Face(to, 10f); yield return null; }
            }
            else if (!keepClip) Play("Walk", 0.08f);
            var from = transform.position; float t = 0f; float jy = Yaw(to - from);
            airborne = true;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / dur);
                var p = Vector3.Lerp(from, to, t);
                p.y = Mathf.Lerp(from.y, to.y, t) + Mathf.Sin(t * Mathf.PI) * height;
                transform.position = p;
                if (Flat(to - from).sqrMagnitude > 1e-4f) TurnTo(jy, 14f);
                yield return null;
            }
            airborne = false;
            groundY = to.y;
            if (anim) anim.speed = 1f;
        }

        /// <summary>Crouch, wiggle, then spring at wherever the target is when the leap starts.</summary>
        IEnumerator PounceAt(Func<Vector3> target, float maxReach, float height = 0.2f, float stopShort = 0.18f)
        {
            yield return StandUp();
            Play("Pounce", 0.08f, true);
            float end = Time.time + 0.52f;
            while (Time.time < end) { Face(target(), 10f); yield return null; }
            var from = transform.position; var to = Flat(target() - from);
            float reach = Mathf.Min(to.magnitude - stopShort, maxReach);
            var dirN = to.sqrMagnitude > 1e-4f ? to.normalized : transform.forward;
            reach = Mathf.Max(0f, reach);
            // never land on top of another cat: shorten the leap until there's room
            for (int i = 0; i < 6; i++)
            {
                var probe = from + dirN * reach; bool hit = false;
                foreach (var o in All) if (o != this && o.InGarden && Flat(o.transform.position - probe).magnitude < 0.42f) hit = true;
                if (!hit) break;
                reach *= 0.6f;
            }
            var land = from + dirN * reach;
            if (GardenMap.I) { land = GardenMap.I.Clamp(land); land.y = GardenMap.I.GroundY(land); }
            yield return Jump(land, height, 0.4f, false, 0f, true);
        }

        // ================================================================== arriving & leaving
        IEnumerator EnterRoutine()
        {
            mode = Mode.Script; InGarden = false;
            var e = entry;
            switch (e.Kind)
            {
                case EntryKind.FenceHop:
                {
                    var foot = e.Threshold; foot.y = 0; foot.x += Mathf.Sign(e.Threshold.x) * 0.5f;
                    yield return Go(foot, BaseWalk, 0.04f, false, false);
                    yield return Jump(e.Threshold, 0.22f, 0.42f, true, 0.5f);       // up onto the fence
                    Play("Idle_Stand", 0.2f);
                    yield return HoldFacing(e.Inside, 0.35f);
                    if (Random.value < 0.6f) AudioManager.I.Meow(Def.VoicePitch);
                    yield return Hold(0.6f);
                    yield return Jump(e.Inside, 0.18f, 0.42f, false);                 // drop down onto the lawn
                    break;
                }
                case EntryKind.Squeeze:
                    yield return Go(e.Threshold, BaseWalk * 0.8f, 0.04f, false, false);
                    yield return Go(e.Inside, BaseWalk * 0.9f, 0.04f, false, false);  // pads out from behind the bush
                    break;
                case EntryKind.CatFlap:
                    yield return Go(e.Threshold + Vector3.back * 0.12f, BaseWalk, 0.03f, false, false);
                    GardenMap.I.PushFlap(1f); AudioManager.I.Play("sfx_tap", 0.5f, 0.6f);
                    yield return Go(e.Inside, BaseWalk, 0.04f, false, false);
                    break;
            }
            groundY = GardenMap.I ? GardenMap.I.GroundY(transform.position) : 0f;
            InGarden = true;
            int hearts = G.Data.Entry(Def.Id)?.Hearts ?? 0;
            stayUntil = Time.time + Random.Range(50f, 80f) * (1f + 0.15f * hearts);
            AudioManager.I.Meow(Def.VoicePitch);
            firstPick = true;
        }

        public void Leave() { if (!Leaving) Run(LeaveRoutine(), Mode.Script); }

        IEnumerator LeaveRoutine()
        {
            mode = Mode.Script; Leaving = true; InGarden = false; Activity = "Leaving";
            ReleaseClaims();
            if (sparkleFx) sparkleFx.Stop();
            TryGift();
            var e = GardenMap.I.RandomEntrance();
            yield return Go(e.Inside);
            switch (e.Kind)
            {
                case EntryKind.FenceHop:
                {
                    yield return Jump(e.Threshold, 0.25f, 0.42f, true, 0.45f);
                    Play("Idle_Stand", 0.2f);
                    yield return HoldYaw(FaceCameraYaw(), 0.5f);                     // one last look back
                    var down = e.Threshold; down.y = 0; down.x += Mathf.Sign(e.Threshold.x) * 0.5f;
                    yield return Jump(down, 0.15f, 0.38f, false);
                    yield return Go(e.Outside, BaseWalk * 1.1f, 0.05f, false, false);
                    break;
                }
                case EntryKind.Squeeze:
                    yield return Go(e.Threshold, BaseWalk * 0.9f, 0.04f, false, false);
                    yield return Go(e.Outside, BaseWalk, 0.05f, false, false);
                    break;
                case EntryKind.CatFlap:
                    yield return Go(e.Threshold + Vector3.forward * 0.12f, BaseWalk, 0.03f, false, false);
                    GardenMap.I.PushFlap(-1f); AudioManager.I.Play("sfx_tap", 0.5f, 0.6f);
                    yield return Go(e.Outside, BaseWalk, 0.05f, false, false);
                    break;
            }
            Destroy(gameObject);
        }

        void TryGift()
        {
            var en = G.Data.Entry(Def.Id);
            int hearts = en?.Hearts ?? 0;
            if (hearts <= 0 || Random.value > 0.14f * hearts) return;
            int fish = G.Gift(Def);
            Fx.Sparkles(null, transform.position + Vector3.up * 0.1f, 2.5f);
            UIController.I?.Toast($"{Def.Id} left you a gift: +{fish} fish!");
            AudioManager.I.Play("sfx_coin");
        }

        // ================================================================== the brain
        bool firstPick;

        IEnumerator Brain()
        {
            mode = Mode.Brain;
            while (true)
            {
                if (Time.time > stayUntil) { yield return LeaveRoutine(); yield break; }
                yield return Choose();
                Activity = "";
                yield return Random.Range(0.2f, 0.9f);
            }
        }

        IEnumerator Choose()
        {
            var opts = new List<(float w, Func<IEnumerator> act)>();
            var map = GardenMap.I;
            foreach (var s in G.Spots)
            {
                if (!s.IsFree || usedSpots.Contains(s)) continue;
                float like = Def.Likes.TryGetValue(s.ItemId, out var l) ? l : 0.2f;
                float trait = s.ItemId == "Bowl" ? Def.Hungry : s.ItemId is "Yarn" or "Wand" ? Def.Playful : Def.Sleepy;
                float w = (0.5f + like) * (0.5f + trait) * (firstPick ? 4f : 1.5f);
                var sp = s; opts.Add((w, () => UseItem(sp)));
            }
            if (map)
            {
                opts.Add((0.35f + Def.Curious, () => VisitPoi("Flower")));
                opts.Add((0.2f + Def.Hungry * 0.5f, () => VisitPoi("Pond")));
                opts.Add((Def.Sleepy * 0.7f, () => VisitPoi("Porch")));
                opts.Add((Def.Sleepy * 0.35f, () => VisitPoi("Shade")));
                opts.Add((0.45f + Def.Curious * 0.4f, Wander));
            }
            opts.Add((0.4f, SitAndGroom));
            if (Butterflies.I && Butterflies.I.Nearest(transform.position, 3.5f)) opts.Add((Def.Playful * 1.1f, ChaseButterfly));
            foreach (var o in All)
                if (o != this && o.InGarden && !o.Busy && !o.spot && !greeted.Contains(o) && (o.transform.position - transform.position).magnitude < 4f)
                { var other = o; opts.Add((Def.Social * 0.8f, () => Greet(other))); break; }
            firstPick = false;

            float total = 0; foreach (var o in opts) total += o.w;
            float r = Random.value * total;
            foreach (var o in opts) { r -= o.w; if (r <= 0f) { yield return o.act(); yield break; } }
            if (opts.Count > 0) yield return opts[^1].act();
        }

        // ------------------------------------------------------------------ activities
        IEnumerator UseItem(Spot s)
        {
            spot = s; s.Occupant = this; Activity = s.ItemId;
            var id = s.ItemId; var item = s.ItemPosition; item.y = 0f;
            var right = CameraRig.FlatRight;
            switch (id)
            {
                case "Bowl":
                {
                    var stand = item + right * 0.42f;   // bowl radius ≈0.27: head over the food, paws outside
                    yield return Go(stand);
                    yield return HoldFacing(item, 0.4f);
                    Play("Eat", 0.25f);
                    yield return HoldFacing(item, Random.Range(6f, 10f) * (0.6f + Def.Hungry));
                    Play("Idle_Stand", 0.25f); yield return 0.6f;
                    yield return SitDown();
                    yield return HoldYaw(FaceCameraYaw(), 0.4f);
                    Play("Groom", 0.25f, true); yield return 2.4f;                   // licks its chops
                    Play("Idle_Sit", 0.25f); yield return 1.2f;
                    break;
                }
                case "Yarn":
                case "Wand":
                {
                    // the toy is live: swipes nudge it, pounces send it rolling, and the cat follows it around
                    const float reachDist = 0.46f;   // root-to-toy distance where the paws just touch it (ball r≈0.19)
                    Vector3 ToyPos() => s && s.Toy ? s.Toy.Position : item;
                    float end = Time.time + Random.Range(9f, 14f) * (0.6f + Def.Playful);
                    while (Time.time < end && s && !string.IsNullOrEmpty(s.ItemId))
                    {
                        var tp = ToyPos();
                        var away = Flat(transform.position - tp); if (away.sqrMagnitude < 1e-4f) away = right;
                        float d = away.magnitude;
                        if (d > reachDist + 0.12f || d < reachDist - 0.12f)
                        {
                            yield return Go(tp + away.normalized * reachDist, BaseWalk * 1.15f, 0.05f);
                            continue;
                        }
                        yield return HoldFacing(tp, 0.25f);
                        if (Random.value < 0.4f + Def.Playful * 0.3f)
                        {
                            // crouch back a step, wiggle, pounce — landing just short of the toy, then send it flying
                            yield return StandUp();
                            yield return Go(tp + away.normalized * (reachDist + 0.22f), BaseWalk, 0.03f, false);
                            yield return PounceAt(ToyPos, 0.5f, 0.16f, reachDist - 0.04f);
                            if (s && s.Toy) s.Toy.Kick(Flat(ToyPos() - transform.position) + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f)), Random.Range(0.7f, 1.1f));
                            AudioManager.I.Play("sfx_pop", 0.5f, Random.Range(1.2f, 1.4f));
                            yield return 0.3f;
                            if (Random.value < 0.4f) { Fx.Hearts(transform.position + Vector3.up * 0.4f, 4, 0.6f); AudioManager.I.Meow(Def.VoicePitch * 1.1f); }
                        }
                        else
                        {
                            yield return SitDown();
                            Play("Bat", 0.2f);
                            // a tap on the toy at each swipe peak (Bat loops every 1.2 s)
                            float batEnd = Time.time + Random.Range(2.4f, 3.6f), nextTap = Time.time + 0.35f;
                            while (Time.time < batEnd)
                            {
                                var p2 = ToyPos(); Face(p2, 6f);
                                if (Time.time > nextTap && s && s.Toy && Flat(p2 - transform.position).magnitude < reachDist + 0.1f)
                                {
                                    s.Toy.Kick(Flat(p2 - transform.position) + transform.right * Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 0.4f));
                                    nextTap = Time.time + 1.2f;
                                }
                                yield return null;
                            }
                        }
                    }
                    yield return SitDown();
                    Play("Happy", 0.2f, true); yield return 1f;
                    break;
                }
                default:   // Box, Cushion, Blanket: hop on/in, groom, nap
                {
                    var seat = s.SeatPosition;
                    var approachDir = Flat(transform.position - seat); if (approachDir.sqrMagnitude < 1e-4f) approachDir = -CameraRig.FlatForward;
                    var approach = seat + approachDir.normalized * (id == "Box" ? 0.92f : 0.66f); approach.y = GardenMap.I ? GardenMap.I.GroundY(approach) : 0f;
                    yield return Go(approach);
                    yield return HoldFacing(seat, 0.3f);
                    float hop = id == "Box" ? 0.5f : id == "Cushion" ? 0.18f : 0.06f;
                    yield return Jump(seat, hop, id == "Box" ? 0.55f : 0.42f, id == "Box", 0.35f);
                    transform.position = seat;
                    yield return SitDown();
                    yield return HoldYaw(FaceCameraYaw(id == "Box" ? 0f : 10f), 1.0f);    // square to the camera so nothing pokes through the walls
                    Play("Groom", 0.25f, true); yield return 2.4f;
                    Play("Sleep", 0.6f);
                    yield return Random.Range(8f, 14f) * (0.5f + Def.Sleepy);
                    Play("Idle_Sit", 0.6f); yield return 1.4f;
                    Play("Happy", 0.2f, true); yield return 1f;
                    yield return StandUp();
                    yield return Jump(approach, hop, 0.38f, false);
                    break;
                }
            }
            G.ConsumeUse(s);
            usedSpots.Add(s);
            if (s.Occupant == this) s.Occupant = null;
            spot = null;
        }

        IEnumerator VisitPoi(string kind)
        {
            var p = GardenMap.I.FreePoi(kind, transform.position);
            if (p == null) yield break;
            poi = p; p.User = this; Activity = kind;
            yield return Go(p.Pos);
            switch (kind)
            {
                case "Flower":
                    yield return HoldFacing(p.LookAt, 0.4f);
                    Play("Sniff", 0.25f);
                    yield return HoldFacing(p.LookAt, Random.Range(3f, 5.5f));
                    if (Random.value < 0.4f) { yield return SitDown(); Play("Idle_Sit"); yield return HoldFacing(p.LookAt, 2.5f); }
                    break;
                case "Pond":
                    yield return HoldFacing(p.LookAt, 0.4f);
                    Play("Eat", 0.3f);                                               // lapping water
                    yield return HoldFacing(p.LookAt, Random.Range(4f, 6.5f));
                    Play("Idle_Stand", 0.3f); yield return 0.8f;
                    break;
                default:   // Porch / Shade: a proper catnap
                    yield return SitDown();
                    yield return HoldYaw(Yaw(p.LookAt - transform.position), 0.6f);
                    Play("Groom", 0.25f, true); yield return 2.4f;
                    Play("Sleep", 0.6f);
                    yield return Random.Range(9f, 16f) * (0.5f + Def.Sleepy);
                    Play("Idle_Sit", 0.6f); yield return 1.5f;
                    break;
            }
            if (poi == p) { p.User = null; poi = null; }
        }

        IEnumerator Wander()
        {
            Activity = "Exploring";
            var to = GardenMap.I.RandomLawnPoint(transform.position, 2.4f);
            yield return Go(to);
            float r = Random.value;
            if (r < 0.35f) { Play("Sniff", 0.25f); yield return Random.Range(2f, 3.5f); }
            else if (r < 0.65f) { Play("Idle_Stand", 0.25f); yield return HoldYaw(FaceCameraYaw(), Random.Range(1.5f, 2.5f)); }
            else { yield return SitDown(); yield return HoldYaw(FaceCameraYaw(), Random.Range(3f, 5f)); }
        }

        IEnumerator SitAndGroom()
        {
            Activity = "Grooming";
            yield return SitDown();
            yield return HoldYaw(FaceCameraYaw(), Random.Range(1.5f, 3f));
            Play("Groom", 0.25f, true); yield return 2.4f;
            Play("Idle_Sit", 0.25f); yield return Random.Range(1.5f, 3f);
        }

        IEnumerator ChaseButterfly()
        {
            var b = Butterflies.I ? Butterflies.I.Nearest(transform.position, 3.5f) : null;
            if (!b) yield break;
            Activity = "Butterfly";
            float end = Time.time + 7f;
            while (Time.time < end && b && b.Catchable)
            {
                float d = StepToward(b.Ground, BaseWalk * 1.5f);
                if (d < 0.75f)
                {
                    var bb = b;
                    yield return PounceAt(() => bb ? bb.Ground : transform.position + transform.forward, 0.55f, 0.28f);
                    if (bb) bb.Flee();
                    break;
                }
                yield return null;
            }
            yield return 0.3f;
            yield return SitDown();
            Play("Idle_Sit", 0.25f);
            yield return 2f;
        }

        IEnumerator Greet(CatVisitor other)
        {
            if (!other || !other.TryBeGreeted(this)) yield break;
            greeted.Add(other); partner = other; Activity = "Greeting";
            var meet = other.transform.position + Flat(transform.position - other.transform.position).normalized * 0.52f;
            meet.y = 0f;
            yield return Go(meet, BaseWalk, 0.05f);
            if (!other || other.partner != this) yield break;
            yield return HoldFacing(other.transform.position, 0.3f);
            Play("Sniff", 0.2f);
            Fx.Hearts((transform.position + other.transform.position) * 0.5f + Vector3.up * 0.45f, 6, 0.7f);
            yield return HoldFacing(other.transform.position, 2.2f);
            yield return SitDown();
            yield return 1.2f;
            if (other && other.partner == this) other.partner = null;
            partner = null;
        }

        bool TryBeGreeted(CatVisitor by)
        {
            if (!InGarden || Busy || Leaving) return false;
            Run(BeingGreeted(by), Mode.Greet);
            partner = by;
            return true;
        }

        IEnumerator BeingGreeted(CatVisitor by)
        {
            greeted.Add(by);
            float giveUp = Time.time + 9f;
            while (by && Time.time < giveUp && (by.transform.position - transform.position).magnitude > 0.6f)
            {
                Face(by.transform.position, 5f);
                if (!seated) Play("Idle_Stand", 0.25f);
                yield return null;
            }
            if (by) { if (seated) Play("Happy", 0.2f, true); else Play("Sniff", 0.2f); yield return HoldFacing(by.transform.position, 2.4f); }
            partner = null;
        }

        // ================================================================== feather toy
        void ConsiderLure()
        {
            var lure = Lure.I;
            if (mode != Mode.Brain || !InGarden || lure == null || !lure.Active || Time.time < nextLureCheck) return;
            nextLureCheck = Time.time + 1.2f;
            float d = Flat(lure.Position - transform.position).magnitude;
            if (d > 4.2f) return;
            float chance = 0.3f + Def.Playful * 0.6f - (current == "Sleep" ? 0.35f : 0f) - d * 0.05f;
            if (G.Data.tutorial == 4) chance = 1f;
            if (Random.value >= chance) return;
            int chasers = 0; foreach (var o in All) if (o != this && o.mode == Mode.Lure && o.Activity == "Toy") chasers++;
            if (chasers >= 2) Run(WatchLure(), Mode.Lure);     // no dog-piling: the rest sit and watch, tails twitching
            else Run(ChaseLure(), Mode.Lure);
        }

        IEnumerator WatchLure()
        {
            Activity = "Watching";
            // pick a spot ~1.1 m from the toy on our side, clear of other cats
            var lp = Lure.I.Position;
            var away = Flat(transform.position - lp); if (away.sqrMagnitude < 1e-4f) away = -CameraRig.FlatForward;
            // fan the audience out round the toy so watchers don't bunch up
            int idx = 0; foreach (var o in All) { if (o == this) break; if (o.Activity == "Watching") idx++; }
            var fan = Quaternion.AngleAxis((idx % 2 == 0 ? 1 : -1) * (25f + 35f * (idx / 2)), Vector3.up) * away.normalized;
            var spotPos = GardenMap.I.Clamp(lp + fan * 1.15f);
            if (GardenMap.I.Blocked(spotPos, 0.2f)) spotPos = GardenMap.I.RandomLawnPoint(lp, 1.4f);
            if (Flat(spotPos - transform.position).magnitude > 0.3f) yield return Go(spotPos, BaseWalk * 1.2f, 0.08f);
            yield return SitDown();
            float end = Time.time + Random.Range(5f, 8f);
            while (Lure.I && Lure.I.Active && Time.time < end)
            {
                Face(Lure.I.Position, 6f);
                Play(Random.value < 0.004f ? "Happy" : "Idle_Sit", 0.3f);
                yield return null;
            }
            Play("Idle_Sit", 0.3f); yield return 0.6f;
        }

        IEnumerator ChaseLure()
        {
            Activity = "Toy";
            if (current == "Sleep") { Play("Idle_Sit", 0.3f); yield return 0.6f; }
            if (Random.value < 0.4f) AudioManager.I.Meow(Def.VoicePitch * 1.15f);
            float watchSince = -1f;
            while (Lure.I && Lure.I.Active)
            {
                var lp = Lure.I.Position;
                float d = Flat(lp - transform.position).magnitude;
                if (d > 0.7f) { watchSince = -1f; StepToward(lp, d > 1.4f ? Trot : BaseWalk * 1.2f); yield return null; continue; }
                // close enough: stalk, eyes on the feather, then pounce when it settles (or it wiggles enticingly)
                if (seated) yield return StandUp();
                anim.speed = 1f; Play("Idle_Stand", 0.15f);
                Face(lp, 12f);
                if (watchSince < 0f) watchSince = Time.time;
                bool crowded = false;
                foreach (var o in All) if (o != this && o.InGarden && Flat(o.transform.position - lp).magnitude < 0.5f) crowded = true;
                bool ready = !crowded && Time.time - lastCatch > 1.1f && Time.time - watchSince > 0.35f;
                if (ready && (Lure.I.Speed < 0.5f || Time.time - watchSince > 2.2f))
                {
                    yield return PounceAt(() => Lure.I ? Lure.I.Position : transform.position + transform.forward, 0.6f, 0.24f);
                    lastCatch = Time.time; watchSince = -1f;
                    var nose = transform.position + transform.forward * 0.22f;
                    if (Lure.I && Lure.I.Active && Flat(Lure.I.Position - nose).magnitude < 0.38f) Caught();
                    yield return 0.3f;
                }
                yield return null;
            }
            yield return SitDown();
            Play("Happy", 0.2f, true); yield return 1f;
            Play("Idle_Sit", 0.25f); yield return 1.2f;
        }

        void Caught()
        {
            Lure.I.Caught();
            AudioManager.I.Play("sfx_pop", 0.9f, Random.Range(0.95f, 1.1f));
            Fx.Hearts(transform.position + Vector3.up * 0.4f, 6, 0.7f);
            if (playBond < GameData.PlayBondCap) { playBond += GameData.BondPerPlay; G.AddBond(Def, GameData.BondPerPlay); }
            if (G.Data.tutorial == 4) G.SetTutorial(5);
            G.AddXp(2);
        }

        // ================================================================== petting
        public void PetBegin()
        {
            if (!CanPet) return;
            petting = true; petEnd = Time.time;
            if (mode != Mode.Pet) Run(Petted(), Mode.Pet);
        }

        public void PetStroke(float px)
        {
            if (mode != Mode.Pet) return;
            petEnd = Time.time;
            strokes += px;
            if (strokes < 150f) return;
            strokes = 0f; pats++; lastPat = Time.time;
            Fx.Hearts(transform.position + Vector3.up * 0.42f, 2, 0.55f);
            if (Time.time - lastPurr > 2.6f) { lastPurr = Time.time; AudioManager.I.Play("sfx_purr", 0.7f); }
            AudioManager.Haptic();
            if (petBond < GameData.PetBondCap) { petBond += GameData.BondPerPet; G.AddBond(Def, GameData.BondPerPet); }
            if (G.Data.tutorial == 5 && pats >= 3) { G.SetTutorial(6); UIController.I?.Toast("You're a natural! Keep collecting cats & poses"); }
        }

        public void PetRelease() { petting = false; }

        IEnumerator Petted()
        {
            Activity = "Pets";
            bool asleep = current == "Sleep";
            if (!asleep) { yield return SitDown(); yield return HoldYaw(FaceCameraYaw(), 0.2f); }
            int patience = 4 + Mathf.RoundToInt(Def.Social * 10f);
            int startPats = pats;
            while (petting || Time.time < petEnd + 1.4f)
            {
                if (pats - startPats > patience)
                {
                    // aloof cats only take so much fuss
                    petting = false; petCooldown = Time.time + 10f;
                    UIController.I?.Toast($"{Def.Id} has had enough fuss for now");
                    yield return StandUp();
                    yield return Go(GardenMap.I.RandomLawnPoint(transform.position, 1.8f));
                    yield break;
                }
                if (!asleep) Play(Time.time - lastPat < 0.9f ? "Happy" : "Idle_Sit", 0.3f);
                yield return null;
            }
        }

        // ================================================================== photos
        public string PoseName => current switch
        {
            "Walk" or "Idle_Stand" or "Sniff" => "Stroll",
            "Eat" => "Eat",
            "Bat" => "Play",
            "Sleep" => "Sleep",
            "Pounce" => "Pounce",
            _ => "Sit",
        };

        /// <summary>0..1: how good a photo would be right now (facing, look-at-camera moments, peak of the action).</summary>
        public float PoseQuality
        {
            get
            {
                if (!anim || !CameraRig.I || !InGarden) return 0.1f;
                var st = anim.GetCurrentAnimatorStateInfo(0);
                float n = st.normalizedTime, t = n - Mathf.Floor(n), tc = Mathf.Clamp01(n);
                var toCam = Flat(CameraRig.I.transform.position - transform.position).normalized;
                float facing = Vector3.Dot(transform.forward, toCam);
                float face = Mathf.Clamp01((facing + 0.2f) / 1.2f);
                float phase; bool profileOk = false;
                switch (current)
                {
                    case "Pounce": phase = Mathf.Clamp01(1f - Mathf.Abs(tc - 0.66f) / 0.17f); profileOk = true; break;
                    case "Bat": phase = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f)); break;
                    case "Happy": phase = Mathf.Sin(tc * Mathf.PI); break;
                    case "Groom": phase = 0.35f + 0.6f * Mathf.Clamp01(Mathf.Min(tc, 1f - tc) / 0.2f); break;
                    case "Sleep": phase = 0.72f; break;
                    case "Eat": phase = 0.7f; profileOk = true; break;
                    case "Walk": case "Sniff": phase = 0.7f; profileOk = true; break;
                    case "Idle_Sit": phase = 0.45f + 0.55f * look; break;
                    case "Idle_Stand": phase = 0.5f + 0.45f * look; break;
                    default: phase = 0.25f; break;
                }
                if (profileOk) face = Mathf.Max(face, 0.5f + 0.4f * (1f - Mathf.Abs(facing)));
                return Mathf.Clamp01(0.1f + 0.45f * face + 0.45f * phase);
            }
        }

        public void OnPhotographed(int stars)
        {
            ShotsLeft--;
            if (sparkleFx) { sparkleFx.Stop(); sparkleFx = null; }
            SparkleUsed = true;
            if (stars == 3) AudioManager.I.Meow(Def.VoicePitch * 1.1f);
        }

        // ================================================================== head look
        /// <summary>
        /// Safety net: whatever moved the cat (personal-space nudges, chasing, interrupted hops), its paws end up
        /// on the surface under it — never sunk into the porch / mats, never hovering over the lawn.
        /// </summary>
        void KeepOnGround()
        {
            if (!InGarden || airborne || spot || !GardenMap.I) return;
            var p = transform.position;
            float gy = GardenMap.I.GroundY(p);
            if (Mathf.Abs(p.y - gy) < 0.003f) return;
            p.y = p.y < gy ? Mathf.MoveTowards(p.y, gy, Time.deltaTime * 3f)      // pop up quickly if below
                           : Mathf.MoveTowards(p.y, gy, Time.deltaTime * 1.5f);
            transform.position = p; groundY = gy;
        }

        /// <summary>Solid scenery: the chest and haunch circles can't pass through boxes, cushions, bushes, pots…</summary>
        void PushOutOfObstacles()
        {
            if (Pinned || !GardenMap.I) return;
            var f = Front; var b = Back;
            var df = GardenMap.I.PushOut(f, BodyR) - f;
            var db = GardenMap.I.PushOut(b, BodyR) - b;
            var d = df.sqrMagnitude > db.sqrMagnitude ? df : db;
            d.y = 0;
            if (d.sqrMagnitude > 1e-8f) transform.position += d;
        }

        void LateUpdate()
        {
            Separate();
            PushOutOfObstacles();
            KeepOnGround();
            bool target = PhotoMode.I && PhotoMode.I.Target == this;
            if (target && Time.time > nextGlance)
            {
                glancing = !glancing;
                nextGlance = Time.time + (glancing ? Random.Range(0.9f, 1.7f) : Random.Range(1.3f, 3.2f) * (1.5f - Def.Social));
            }
            bool canLook = current is "Idle_Sit" or "Idle_Stand" or "Happy";
            bool want = canLook && (mode == Mode.Pet || (target && glancing));
            look = Mathf.MoveTowards(look, want ? 1f : 0f, Time.deltaTime * 3f);
            if (head && look > 0.001f && CameraRig.I)
            {
                var flat = Flat(CameraRig.I.transform.position - head.position).normalized;
                float yawOff = Mathf.Clamp(Vector3.SignedAngle(Flat(transform.forward).normalized, flat, Vector3.up), -50f, 50f);
                head.rotation = Quaternion.AngleAxis(yawOff * 0.75f * look, Vector3.up) * head.rotation;
            }
        }
    }
}
