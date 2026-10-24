using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PawCollector.Cafe
{
    /// <summary>A resident café cat: naps on its favourite furniture, hops onto perches, visits customers' tables, eats, plays, grooms.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CafeCat : MonoBehaviour
    {
        public CatDef Def;
        public string Activity { get; private set; } = "";
        public Customer Visiting { get; private set; }
        NavMeshAgent agent; Animator anim; string current = ""; bool sitting, busyJump;
        FurnitureInstance perchFurn; int perchIdx = -1;
        /// <summary>The furniture this cat is sitting/lying on right now (null on the floor).</summary>
        public FurnitureInstance PerchFurniture => perchFurn;
        float petCooldown;
        static readonly HashSet<string> Seated = new() { "Idle_Sit", "Groom", "Happy", "Sleep", "Bat", "SitDown" };

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); anim = GetComponentInChildren<Animator>();
            agent.speed = 0.95f; agent.angularSpeed = 420f; agent.acceleration = 6f; agent.radius = 0.32f; agent.stoppingDistance = 0.05f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance; agent.avoidancePriority = Random.Range(30, 60);
        }
        void Start() { if (Def == null) Def = GameData.Cat(name.Replace("CafeCat_", "").Replace("(Clone)", "")); StartCoroutine(Brain()); }

        // -------------------------------------------------------------- animation
        void Play(string clip, float fade = 0.2f, bool restart = false)
        {
            var face = GetComponentInChildren<CatFace>(); if (face) face.EyesClosed = clip == "Sleep";
            if (!anim || (current == clip && !restart)) return;
            current = clip;
            if (restart) anim.CrossFadeInFixedTime(clip, fade, 0, 0f); else anim.CrossFadeInFixedTime(clip, fade);
        }
        IEnumerator Stand() { if (!sitting) yield break; sitting = false; Play("StandUp", 0.12f); yield return new WaitForSeconds(0.75f); Play("Idle_Stand", 0.2f); }
        IEnumerator Sit() { if (sitting) yield break; sitting = true; Play("SitDown", 0.12f); yield return new WaitForSeconds(0.75f); Play("Idle_Sit", 0.2f); }

        // -------------------------------------------------------------- movement
        IEnumerator WalkTo(Vector3 p, float stopAt = 0.1f, float timeout = 25f)
        {
            yield return Stand();
            agent.enabled = true; agent.isStopped = false;
            if (NavMesh.SamplePosition(p, out var hit, 1.5f, agent.areaMask)) p = hit.position;
            agent.SetDestination(p); float t = 0;
            while (t < timeout)
            {
                t += Time.deltaTime;
                bool moving = agent.velocity.sqrMagnitude > 0.01f;
                Play(moving ? "Walk" : "Idle_Stand", 0.15f);
                if (anim) anim.speed = moving ? Mathf.Clamp(agent.velocity.magnitude / 0.8f, 0.6f, 1.5f) : 1f;
                if (!agent.pathPending && agent.remainingDistance <= stopAt) break;
                yield return null;
            }
            if (anim) anim.speed = 1f;
            agent.isStopped = true; Play("Idle_Stand", 0.2f);
        }
        IEnumerator Face(Vector3 target, float dur = 0.35f)
        {
            var d = target - transform.position; d.y = 0; if (d.sqrMagnitude < 1e-4f) yield break;
            var r0 = transform.rotation; var r1 = Quaternion.LookRotation(d);
            for (float t = 0; t < 1; t += Time.deltaTime / dur) { transform.rotation = Quaternion.Slerp(r0, r1, t); yield return null; }
        }
        /// <summary>Hop between two points along an arc (pounce leap) — used for perches.</summary>
        IEnumerator Hop(Vector3 to, Quaternion rot)
        {
            busyJump = true; agent.enabled = false; sitting = false;
            Vector3 from = transform.position; float h = Mathf.Abs(to.y - from.y);
            Play("Pounce", 0.08f, true);
            if (anim) anim.Play("Pounce", 0, 0.5f);
            float dur = 0.45f + h * 0.12f, arc = 0.35f + h * 0.35f;
            var r0 = transform.rotation;
            for (float t = 0; t < 1f; t += Time.deltaTime / dur)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                var p = Vector3.Lerp(from, to, e); p.y += Mathf.Sin(t * Mathf.PI) * arc;
                transform.position = p; transform.rotation = Quaternion.Slerp(r0, rot, e * 1.4f);
                yield return null;
            }
            transform.SetPositionAndRotation(to, rot); Play("Idle_Stand", 0.15f);
            yield return new WaitForSeconds(0.25f); busyJump = false;
        }
        Vector3 FloorNear(Vector3 p, Vector3 fallbackDir)
        {
            p.y = 0;
            if (NavMesh.SamplePosition(p, out var hit, 1.6f, agent.areaMask)) return hit.position;
            if (NavMesh.SamplePosition(p + fallbackDir, out hit, 2f, agent.areaMask)) return hit.position;
            return transform.position;
        }

        // -------------------------------------------------------------- brain
        IEnumerator Brain()
        {
            Play("Idle_Stand", 0f);
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));
            while (true)
            {
                var opts = new List<(float w, System.Func<IEnumerator> act)>();
                var cm = CafeManager.I;
                var rec = cm.Data.Cat(Def.Id);
                // a sick cat curls up in its own bed until it gets medicine
                if (rec != null && rec.sick) { yield return SickRest(); yield return null; continue; }
                bool hungryNow = rec != null && rec.full < 50;
                float sleepy = Def?.Sleepy ?? 0.5f, social = Def?.Social ?? 0.5f, playful = Def?.Playful ?? 0.5f, hungry = Def?.Hungry ?? 0.5f, curious = Def?.Curious ?? 0.5f;
                opts.Add((0.8f + sleepy * 2f, () => Perch(SpotKind.Bed, SpotKind.Cushion)));
                opts.Add((0.4f + curious * 1.4f, () => Perch(SpotKind.High, SpotKind.Window)));
                if (cm.Customers.Any(c => c && c.Seated)) opts.Add((0.6f + social * 2.5f, VisitTable));
                if (cm.Furniture.Any(f => f.Def.Id == "FoodBowl")) opts.Add(((0.3f + hungry * 1.2f) * (hungryNow && cm.Data.food > 0 ? 6f : 0.5f), EatBowl));
                opts.Add((0.4f + playful * 1.2f, PlayAround));
                opts.Add((0.5f + curious, Wander));
                opts.Add((0.4f, GroomHere));
                float sum = opts.Sum(o => o.w), r = Random.value * sum;
                foreach (var o in opts) { r -= o.w; if (r <= 0) { yield return o.act(); break; } }
                yield return null;
            }
        }

        IEnumerator Perch(params SpotKind[] kinds)
        {
            var cm = CafeManager.I;
            var own = cm.BedOf(Def.Id);
            // beds: a cat naps in its own bed (and leaves other cats' beds alone)
            if (kinds.Contains(SpotKind.Bed) && own && cm.RoomOpen(own.Placed.room) && Random.value < 0.6f)
            {
                int oi = own.FreePerch(this); if (oi >= 0) { yield return PerchAt(own, oi, false, 1f); yield break; }
            }
            var cands = cm.Furniture.Where(f => f && f.Def.Perches.Length > 0 && cm.RoomOpen(f.Placed.room) && (f.Def.Beds == 0 || f == own || cm.BedOwner(f) == null || f.Def.Perches.Length > 1)).ToList();
            var pick = new List<(FurnitureInstance f, int i)>();
            foreach (var f in cands) for (int i = 0; i < f.Def.Perches.Length; i++)
                if (kinds.Contains(f.Def.Perches[i].Kind) && (f.PerchTaken == null || i >= f.PerchTaken.Length || !f.PerchTaken[i])) pick.Add((f, i));
            if (pick.Count == 0) { yield return Wander(); yield break; }
            var (furn, idx) = pick[Random.Range(0, pick.Count)];
            yield return PerchAt(furn, idx, false, 1f);
        }
        IEnumerator SickRest()
        {
            Activity = "sick";
            var cm = CafeManager.I; var bed = cm.BedOf(Def.Id);
            if (!bed || !cm.RoomOpen(bed.Placed.room)) bed = cm.Furniture.Where(f => f && cm.RoomOpen(f.Placed.room) && f.Def.Perches.Any(p => p.Kind is SpotKind.Bed or SpotKind.Cushion)).OrderBy(_ => Random.value).FirstOrDefault();
            int i = bed ? bed.FreePerch(this) : -1;
            if (i >= 0 && bed.Def.Perches[i].Kind is SpotKind.Bed or SpotKind.Cushion) yield return PerchAt(bed, i, true, 2.5f);
            else { yield return Sit(); Play("Sleep", 0.6f); yield return new WaitForSeconds(Random.Range(20f, 30f)); Play("Idle_Sit", 0.5f); yield return new WaitForSeconds(0.8f); }
            Activity = "";
        }
        IEnumerator PerchAt(FurnitureInstance furn, int idx, bool forceNap, float stayMul)
        {
            if (furn.PerchTaken == null || furn.PerchTaken.Length != furn.Def.Perches.Length) furn.PerchTaken = new CafeCat[furn.Def.Perches.Length];
            furn.PerchTaken[idx] = this; perchFurn = furn; perchIdx = idx; Activity = "perch";
            var pp = furn.PerchPos(idx); var pr = furn.PerchRot(idx);
            var outDir = (pp - furn.transform.position); outDir.y = 0; if (outDir.sqrMagnitude < 0.01f) outDir = furn.transform.forward; outDir.Normalize();
            var floor = FloorNear(pp + outDir * (0.55f + furn.Def.Radius * 0.6f), outDir);
            yield return WalkTo(floor, 0.12f);
            if (pp.y > 0.25f) { yield return Face(pp); yield return Hop(pp, pr); }
            else { agent.enabled = false; var p0 = transform.position; var r0 = transform.rotation; for (float t = 0; t < 1; t += Time.deltaTime / 0.5f) { transform.position = Vector3.Lerp(p0, pp, t); transform.rotation = Quaternion.Slerp(r0, pr, t); Play("Walk"); yield return null; } transform.SetPositionAndRotation(pp, pr); }
            yield return Sit();
            float stay = Random.Range(10f, 22f) * (0.6f + (Def?.Sleepy ?? 0.5f)) * stayMul;
            bool nap = forceNap || furn.Def.Perches[idx].Kind is SpotKind.Bed or SpotKind.Cushion && Random.value < 0.4f + (Def?.Sleepy ?? 0.5f) * 0.5f;
            float t2 = 0, next = 2f;
            if (nap) Play("Sleep", 0.6f);
            bool ownBed = furn == CafeManager.I.BedOf(Def.Id);
            if (nap) CafeManager.I.Cheer(Def.Id, ownBed ? 12f : 4f);
            while (t2 < stay)
            {
                if (forceNap && !(CafeManager.I.Data.Cat(Def.Id)?.sick ?? false)) break;   // medicine given: wake up
                t2 += Time.deltaTime; next -= Time.deltaTime;
                if (!nap && next <= 0) { next = Random.Range(3f, 6f); Play(Random.value < 0.35f ? "Groom" : "Idle_Sit", 0.3f, true); }
                yield return null;
            }
            // leave the perch
            if (nap) { Play("Idle_Sit", 0.6f); yield return new WaitForSeconds(0.8f); }
            sitting = true; yield return Stand();
            if (pp.y > 0.25f) yield return Hop(floor, Quaternion.LookRotation(outDir));
            else { var p0 = transform.position; for (float t = 0; t < 1; t += Time.deltaTime / 0.5f) { transform.position = Vector3.Lerp(p0, floor, t); Play("Walk"); yield return null; } }
            furn.Release(this); perchFurn = null; perchIdx = -1;
            agent.enabled = true; agent.Warp(transform.position); Activity = "";
        }

        IEnumerator VisitTable()
        {
            var seated = CafeManager.I.Customers.Where(c => c && c.Seated).ToList();
            if (seated.Count == 0) yield break;
            var target = seated.OrderByDescending(c => (c.Loves(Def) ? 2f : 0f) + Random.value).First();
            if (CafeManager.I.Cats.Count(k => k != this && k.Visiting == target) >= 2) { yield return Wander(); yield break; }
            Visiting = target; Activity = "visit";
            var seat = target.Seat.transform; float side = Random.value < 0.5f ? -1 : 1;
            var spot = FloorNear(seat.position + seat.right * 0.75f * side - seat.forward * 0.1f, seat.right * side);
            yield return WalkTo(spot, 0.15f);
            if (!target) { Visiting = null; yield break; }
            yield return Face(target.transform.position);
            yield return Sit();
            if (target && target.Loves(Def)) Fx.Hearts(transform.position + Vector3.up * 1.1f, 8, 0.8f);
            float stay = Random.Range(8f, 16f), t = 0;
            while (t < stay && target && target.Seated) { t += Time.deltaTime; if (Random.value < 0.004f) Play("Happy", 0.2f, true); yield return null; }
            CafeManager.I.Cheer(Def.Id, 5f);
            Visiting = null; Activity = "";
        }

        IEnumerator EatBowl()
        {
            var bowl = CafeManager.I.Furniture.Where(f => f && f.Def.Id == "FoodBowl").OrderBy(_ => Random.value).FirstOrDefault();
            if (!bowl) yield break;
            Activity = "eat";
            var dir = (transform.position - bowl.transform.position); dir.y = 0; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            yield return WalkTo(FloorNear(bowl.transform.position + dir * 0.55f, dir), 0.08f);
            yield return Face(bowl.transform.position);
            if (CafeManager.I.Data.food <= 0) { Play("Sniff", 0.25f); yield return new WaitForSeconds(2.5f); Activity = ""; yield break; }   // empty bowl
            Play("Eat", 0.25f); yield return new WaitForSeconds(Random.Range(4f, 7f));
            var rec = CafeManager.I.Data.Cat(Def.Id); if (rec != null && rec.full < 70) CafeManager.I.Feed(Def.Id, true);
            Play("Idle_Stand", 0.25f); Activity = "";
        }

        IEnumerator PlayAround()
        {
            Activity = "play";
            yield return Sit();
            Play("Bat", 0.2f, true); yield return new WaitForSeconds(Random.Range(2f, 3.5f));
            yield return Stand();
            Play("Pounce", 0.1f, true); yield return new WaitForSeconds(1.5f);
            CafeManager.I.Cheer(Def.Id, 6f);
            Play("Idle_Stand", 0.2f); Activity = "";
        }

        IEnumerator Wander()
        {
            Activity = "wander";
            for (int k = 0; k < 6; k++)
            {
                var p = transform.position + Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(1.2f, 3.5f);
                if (NavMesh.SamplePosition(p, out var hit, 1f, agent.areaMask) && OpenAt(hit.position)) { yield return WalkTo(hit.position, 0.15f, 10f); break; }
            }
            if (Random.value < 0.5f) { Play("Sniff", 0.2f); yield return new WaitForSeconds(Random.Range(1.5f, 3f)); }
            else yield return new WaitForSeconds(Random.Range(0.8f, 2f));
            Activity = "";
        }
        static bool OpenAt(Vector3 p)
        {
            foreach (var r in CafeData.Rooms)
            {
                var go = GameObject.Find(r.Model); if (!go) continue;
                var b = go.GetComponent<Renderer>().bounds;
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z) return CafeManager.I.RoomOpen(r.Id);
            }
            return false;   // outside the café (street) — cats stay indoors
        }

        IEnumerator GroomHere() { Activity = "groom"; yield return Sit(); Play("Groom", 0.25f, true); yield return new WaitForSeconds(Random.Range(3f, 5f)); Play("Idle_Sit", 0.25f); yield return new WaitForSeconds(1f); Activity = ""; }

        void Update() { petCooldown -= Time.deltaTime; purrT -= Time.deltaTime; }

        // -------------------------------------------------------------- interaction
        /// <summary>Fired on every pet: the cat, and whether a friendship heart just filled up.</summary>
        public static event System.Action<CafeCat, bool> Petted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetPetted() => Petted = null;
        /// <summary>One stroke / pat. Builds friendship (+1 bond, a heart every 20), cheers the cat up, purr + content squint.</summary>
        public bool Pet()
        {
            if (petCooldown > 0 || busyJump) return false;
            petCooldown = 0.28f;
            var rec = CafeManager.I.Data.CatOrNew(Def.Id); int before = rec.Hearts;
            rec.bond = Mathf.Min(100, rec.bond + 1); CafeManager.I.Cheer(Def.Id, 4f);
            bool heartUp = rec.Hearts > before;
            Fx.Hearts(transform.position + Vector3.up * 0.7f, heartUp ? 18 : 5, heartUp ? 1.4f : 1.1f);
            if (purrT <= 0f) { AudioManager.I?.Play("sfx_purr", 0.7f); purrT = 1.6f; }
            if (sitting && current != "Sleep" && current != "Happy") Play("Happy", 0.15f, true);
            if (squint != null) StopCoroutine(squint); squint = StartCoroutine(Squint());
            CafeManager.I.Recalc();
            Petted?.Invoke(this, heartUp);
            return true;
        }
        Coroutine squint; float purrT;
        IEnumerator Squint()
        {
            var face = GetComponentInChildren<CatFace>(); if (!face) yield break;
            face.EyesClosed = true; yield return new WaitForSeconds(0.7f);
            face.EyesClosed = current == "Sleep"; squint = null;
        }

        public string PoseName => current switch
        {
            "Idle_Sit" or "Groom" or "Happy" or "SitDown" => "Sit",
            "Walk" or "Idle_Stand" or "Sniff" => "Stroll",
            "Eat" => "Eat", "Bat" => "Play", "Pounce" => "Pounce", "Sleep" => "Sleep", _ => "Sit"
        };
        public float PoseQuality(Camera cam)
        {
            var to = cam.transform.position - transform.position; to.y = 0;
            float face = Vector3.Dot(transform.forward, to.normalized);
            float q = 0.45f + 0.45f * Mathf.Clamp01((face + 0.2f) / 1.0f);
            if (current is "Happy" or "Pounce" or "Bat") q += 0.15f;
            if (agent.enabled && agent.velocity.magnitude > 0.5f) q -= 0.15f;
            return Mathf.Clamp01(q);
        }
    }
}
