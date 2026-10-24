using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PawCollector.Cafe
{
    public enum Bubble { None, Order, Waiting, Photo, Heart, Coins }

    public class PhotoRequest
    {
        public string Cat;     // null = any cat
        public string Pose;    // Sit, Stroll, Eat, Play, Sleep, Pounce or null
        public float TimeLeft;
        public string Label => $"{(Cat != null ? CafeManager.I.Data.Name(Cat) : "any cat")}{(Pose != null ? " · " + PoseWord(Pose) : "")}";
        public static string PoseWord(string p) => p switch { "Sit" => "sitting", "Stroll" => "walking", "Eat" => "eating", "Play" => "playing", "Sleep" => "napping", "Pounce" => "pouncing", _ => p };
        public bool Matches(string cat, string pose) => (Cat == null || Cat == cat) && (Pose == null || Pose == pose);
    }

    /// <summary>A café customer: walks in, sits, orders, enjoys the cats, sometimes asks for a photo, pays, leaves.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Customer : MonoBehaviour
    {
        public FurnitureInstance Seat { get; private set; }
        public MenuItem Order { get; private set; }
        public Bubble Bubble { get; private set; }
        public PhotoRequest Request { get; private set; }
        public string Favourite { get; private set; }   // a cat type they love
        public bool Seated { get; private set; }
        NavMeshAgent agent; Animator anim; string current; GameObject servedProp;
        bool served, photoDone; int photoStars;
        static readonly string[] Favs = { "fluffy", "kitten", "ginger", "black", "tabby", "chonky", "white", "calico" };

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); anim = GetComponentInChildren<Animator>();
            agent.speed = 1.6f; agent.angularSpeed = 540f; agent.acceleration = 8f; agent.radius = 0.3f; agent.stoppingDistance = 0.05f;
            agent.autoBraking = true; agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        }

        public void Begin(FurnitureInstance seat)
        {
            Seat = seat; seat.Occupant = this;
            Favourite = Random.value < 0.6f ? Favs[Random.Range(0, Favs.Length)] : null;
            StartCoroutine(Run());
        }

        void Play(string clip, float fade = 0.2f) { if (current == clip || !anim) return; current = clip; anim.CrossFadeInFixedTime(clip, fade); }

        IEnumerator WalkTo(Vector3 p)
        {
            agent.enabled = true; agent.isStopped = false;
            if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
            agent.SetDestination(p);
            float t = 0;
            while (true)
            {
                t += Time.deltaTime;
                bool moving = agent.velocity.sqrMagnitude > 0.02f;
                Play(moving ? "Walk" : "Idle", 0.15f);
                if (anim) anim.speed = moving ? Mathf.Clamp(agent.velocity.magnitude / 1.4f, 0.6f, 1.4f) : 1f;
                if (!agent.pathPending && agent.remainingDistance <= 0.12f) break;
                if (t > 30f) break;
                yield return null;
            }
            if (anim) anim.speed = 1f;
        }

        IEnumerator Run()
        {
            // walk in through the door to the chair
            yield return WalkTo(CafeManager.I.Door.position);
            yield return WalkTo(Seat.SeatPoint);
            // sit
            agent.enabled = false;
            Vector3 p0 = transform.position; Quaternion r0 = transform.rotation;
            Play("SitDown", 0.12f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.55f)
            {
                transform.position = Vector3.Lerp(p0, Seat.SeatPoint, Mathf.SmoothStep(0, 1, t));
                transform.rotation = Quaternion.Slerp(r0, Seat.SeatRotation, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            transform.SetPositionAndRotation(Seat.SeatPoint, Seat.SeatRotation);
            Seated = true; Play("Sit_Idle", 0.2f);
            yield return new WaitForSeconds(Random.Range(0.6f, 1.4f));
            // order
            var menu = CafeData.Menu.Take(Mathf.Max(1, CafeManager.I.Data.menuTier)).ToList();
            Order = menu[Mathf.Min(menu.Count - 1, (int)(Mathf.Pow(Random.value, 0.8f) * menu.Count))];
            Bubble = Bubble.Order; CafeManager.I.PlaceOrder(this);
            float wait = 0; while (!served && wait < 25f) { wait += Time.deltaTime; yield return null; }
            Bubble = Bubble.None;
            // maybe ask for a photo of a resident cat
            var cats = CafeManager.I.Cats;
            if (cats.Count > 0 && Random.value < 0.35f)
            {
                var cat = cats[Random.Range(0, cats.Count)];
                string[] poses = { "Sit", "Sleep", "Eat", "Play", "Stroll", null };
                Request = new PhotoRequest { Cat = Random.value < 0.7f ? cat.Def.Id : null, Pose = poses[Random.Range(0, poses.Length)], TimeLeft = 75f };
                Bubble = Bubble.Photo;
            }
            // enjoy
            float stay = Random.Range(22f, 38f) + (Request != null ? 30f : 0f), timer = 0, next = 0;
            while (timer < stay)
            {
                timer += Time.deltaTime; next -= Time.deltaTime;
                if (Request != null && !photoDone) { Request.TimeLeft -= Time.deltaTime; if (Request.TimeLeft <= 0) { Request = null; Bubble = Bubble.None; } }
                if (photoDone && timer < stay - 4f) stay = Mathf.Min(stay, timer + 4f);
                if (next <= 0f)
                {
                    next = Random.Range(3.5f, 6f);
                    float r = Random.value;
                    Play(r < 0.4f ? "Sit_Sip" : r < 0.65f ? "Sit_Eat" : r < 0.75f && CatsNearby() > 0 ? "Sit_Photo" : "Sit_Idle", 0.25f);
                    current = current == "Sit_Idle" ? current : null;   // allow replays of one-shot sit clips
                }
                yield return null;
            }
            // pay
            int nearby = CatsNearby();
            bool fav = FavouriteNearby();
            long bill = Mathf.RoundToInt(Order.Price * (1f + CafeData.CatTableBonus * nearby) * (fav ? 2f : 1f));
            long tip = Mathf.RoundToInt(bill * Random.Range(CafeData.TipMin, CafeData.TipMax)) + (photoDone ? Order.Price * 2 * photoStars : 0);
            Bubble = Bubble.Coins; Fx.Coins(transform.position + Vector3.up * 1.6f);
            if (fav) Fx.Hearts(transform.position + Vector3.up * 1.7f);
            CafeManager.I.CustomerPaid(this, bill, tip, 1 + (photoDone ? 4 + photoStars * 3 : 0));
            Play("Sit_Cheer", 0.15f);
            yield return new WaitForSeconds(1.0f);
            Bubble = Bubble.None;
            if (servedProp) Destroy(servedProp, 0.5f);
            // stand up and leave
            Play("StandUp", 0.15f); Seated = false;
            yield return new WaitForSeconds(0.5f);
            Seat.Occupant = null;
            var outPos = Seat.SeatPoint - Seat.transform.forward * 0.0f;
            if (NavMesh.SamplePosition(outPos, out var h2, 1.2f, NavMesh.AllAreas)) outPos = h2.position;
            agent.enabled = true; agent.Warp(outPos);
            yield return WalkTo(CafeManager.I.Door.position);
            yield return WalkTo(CafeManager.I.RandomStreetEnd.position);
            CafeManager.I.CustomerLeft(this);
            Destroy(gameObject);
        }

        public void Serve()
        {
            served = true;
            var table = CafeManager.I.TableNear(Seat.SeatPoint);
            var prefab = CafeRefs.Prop3D(Order.Prop);
            if (table && prefab)
            {
                var tp = table.transform.position; var sp = Seat.SeatPoint;
                var p = Vector3.Lerp(tp, sp, 0.42f); p.y = tp.y + 0.925f;
                servedProp = Instantiate(prefab, p, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            }
            AudioManager.I?.Play("sfx_pop", 0.4f, 1.3f);
        }

        int CatsNearby() => CafeManager.I.Cats.Count(c => c && Vector3.Distance(CafeManager.Flat(c.transform.position), CafeManager.Flat(transform.position)) < 1.8f);
        bool FavouriteNearby() => Favourite != null && CafeManager.I.Cats.Any(c => c && Vector3.Distance(CafeManager.Flat(c.transform.position), CafeManager.Flat(transform.position)) < 1.8f && Loves(c.Def));

        public bool Loves(CatDef d)
        {
            if (Favourite == null || d == null) return false;
            string b = d.Breed.ToLower();
            return Favourite switch
            {
                "fluffy" => d.Body is "Fluffy" or "Lynx" or "Plume",
                "kitten" => d.Body == "Kitten",
                "ginger" => b.Contains("ginger") || b.Contains("red") || b.Contains("flame") || b.Contains("orange"),
                "black" => b.Contains("black") || b.Contains("tuxedo") || b.Contains("smoke"),
                "tabby" => b.Contains("tabby"),
                "chonky" => d.Body is "Chonky" or "Round",
                "white" => b.Contains("white"),
                "calico" => b.Contains("calico") || b.Contains("tortoise") || b.Contains("tortie"),
                _ => false
            };
        }
        public string FavouriteLabel => Favourite switch { "fluffy" => "fluffy cats", "kitten" => "kittens", "ginger" => "ginger cats", "black" => "black cats",
            "tabby" => "tabbies", "chonky" => "chonky cats", "white" => "white cats", "calico" => "calicos", _ => null };

        /// <summary>Called by photo mode when a shot of <paramref name="cat"/> in <paramref name="pose"/> is taken.</summary>
        public bool TryFulfil(string cat, string pose, int stars)
        {
            if (Request == null || photoDone || !Request.Matches(cat, pose)) return false;
            photoDone = true; photoStars = stars; Request = null; Bubble = Bubble.Heart;
            Fx.Hearts(transform.position + Vector3.up * 1.7f); Play("Sit_Cheer", 0.15f);
            return true;
        }
    }
}
