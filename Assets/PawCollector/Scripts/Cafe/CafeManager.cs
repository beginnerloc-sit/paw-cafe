using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PawCollector.Cafe
{

    /// <summary>Owns the café state: idle economy, customers, seats, residents, rooms, levels, offline earnings.</summary>
    [DefaultExecutionOrder(-100)]
    public class CafeManager : MonoBehaviour
    {
        public static CafeManager I { get; private set; }
        public CafeSave Data { get; private set; }
        public Transform Door, DoorOutside, DoorOutside2, CounterPoint, TipJarPoint;
        public Transform RandomStreetEnd => DoorOutside2 && UnityEngine.Random.value < 0.5f ? DoorOutside2 : DoorOutside;
        public readonly List<FurnitureInstance> Furniture = new();
        public readonly List<Customer> Customers = new();
        public readonly List<CafeCat> Cats = new();
        public float Popularity { get; private set; }
        public int Cozy { get; private set; }
        public event Action Changed;                 // coins / level / anything the HUD shows
        public event Action<string> Toast;
        public event Action<long, float> WelcomeBack; // coins, hours
        public event Action<int> LevelUp;
        float spawnTimer = 4f, saveTimer; readonly List<string> bag = new();
        readonly Queue<Customer> orders = new(); float prep;

        void Awake()
        {
            I = this;
            Data = CafeSave.Load();
            bool fresh = Data == null;
            if (fresh)
            {
                Data = new CafeSave();
                var dl = GetComponent<DefaultLayout>();
                if (dl) foreach (var p in dl.Items) if (p.room == "counter") Data.furniture.Add(new PlacedFurn { id = p.id, x = p.x, y = p.y, z = p.z, yaw = p.yaw, room = p.room });
                Data.residents.Add("Mochi"); Data.CatOrNew("Mochi");
                Data.lastSeenUtc = DateTime.UtcNow.Ticks;
            }
        }

        void Start()
        {
            foreach (var r in CafeData.Rooms) SetRoomOpen(r.Id, Data.rooms.Contains(r.Id));
            foreach (var p in Data.furniture) Spawn(p);
            foreach (var f in Furniture) if (f.Def.Seats > 0) { CafeDecorate.FaceTable(f); f.SavePlacement(); }
            Recalc();
            foreach (var id in Data.residents) SpawnCat(id);
            Offline();
        }

        // ---------------------------------------------------------------- rooms
        public bool RoomOpen(string id) => Data.rooms.Contains(id);
        public void SetRoomOpen(string id, bool open)
        {
            var def = CafeData.Room(id); if (def == null) return;
            var go = GameObject.Find(def.Model); if (go) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = r.name.StartsWith("Glass_") ? CafeRefs.I.Glass : open ? CafeRefs.I.Prop : CafeRefs.I.Locked;
            var block = GameObject.Find("Block_" + id); if (block) block.SetActive(!open);
            var sign = GameObject.Find("Sign_" + id); if (sign) sign.SetActive(!open);
        }
        public bool TryUnlockRoom(string id)
        {
            var def = CafeData.Room(id);
            if (def == null || RoomOpen(id)) return false;
            if (Data.level < def.Unlock) { Toast?.Invoke($"{def.Label} opens at level {def.Unlock}"); return false; }
            if (!Spend(def.Cost)) return false;
            Data.rooms.Add(id); SetRoomOpen(id, true); Toast?.Invoke($"{def.Label} is open — it comes with some furniture!"); AudioManager.I?.Play("sfx_levelup");
            var dl = GetComponent<DefaultLayout>();
            if (dl) foreach (var p in dl.Items.Where(x => x.room == id)) { var pf = new PlacedFurn { id = p.id, x = p.x, y = p.y, z = p.z, yaw = p.yaw, room = p.room }; Data.furniture.Add(pf); var fi = Spawn(pf); if (fi && fi.Def.Seats > 0) CafeDecorate.FaceTable(fi); }
            MoveInWaiting(); CafePhoto.I?.RefreshFrames();
            Recalc(); Save(); return true;
        }

        // ---------------------------------------------------------------- furniture
        public FurnitureInstance Spawn(PlacedFurn p)
        {
            var def = CafeData.Furn(p.id); if (def == null) return null;
            var prefab = Resources.Load<GameObject>("Furniture/" + def.Id); if (!prefab) return null;
            var go = Instantiate(prefab, new Vector3(p.x, p.y, p.z), Quaternion.Euler(0, p.yaw, 0));
            var fi = go.GetComponent<FurnitureInstance>(); fi.Def = def; fi.Placed = p;
            Furniture.Add(fi); return fi;
        }
        public void Remove(FurnitureInstance fi) { Furniture.Remove(fi); Data.furniture.Remove(fi.Placed); Destroy(fi.gameObject); Recalc(); }

        public IEnumerable<FurnitureInstance> Chairs => Furniture.Where(f => f.Def.Seats > 0);
        public int Seats => Chairs.Count();
        public int Beds => Furniture.Sum(f => f.Def.Beds);

        public void Recalc()
        {
            Cozy = Furniture.Sum(f => f.Def.Cozy);
            int wall = Data.wall.Sum(w => 2 + w.stars);
            int charm = Data.residents.Sum(id => 3 + (Data.Cat(id)?.charm ?? 0) + RarityBonus(id));
            Popularity = Cozy + wall + charm + Data.followers / 25f;
            Changed?.Invoke();
        }
        static int RarityBonus(string id) { var d = GameData.Cat(id); return d == null ? 0 : (int)d.Rarity * 3; }

        // ---------------------------------------------------------------- money & xp
        public bool Spend(long n)
        {
            if (Data.coins < n) { Toast?.Invoke("Not enough coins yet"); return false; }
            Data.coins -= n; Changed?.Invoke(); return true;
        }
        public void AddCoins(long n) { Data.coins += n; Changed?.Invoke(); }
        public void AddTip(long n) { Data.tipJar += n; Changed?.Invoke(); }
        public long CollectTips()
        {
            long n = Data.tipJar; if (n <= 0) return 0;
            Data.tipJar = 0; AddCoins(n); AudioManager.I?.Play("sfx_coin"); TutorialDone(0); Save(); return n;
        }
        public void AddXp(int n)
        {
            Data.xp += n;
            while (Data.xp >= CafeData.XpForLevel(Data.level))
            {
                Data.xp -= CafeData.XpForLevel(Data.level); Data.level++;
                if (Data.level % 3 == 0) Data.tickets++;
                Data.menuTier = CafeData.Menu.Count(m => m.Unlock <= Data.level);
                LevelUp?.Invoke(Data.level); AudioManager.I?.Play("sfx_levelup");
            }
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- customers
        void Update()
        {
            float dt = Time.deltaTime;
            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer = CafeData.ArrivalInterval(Popularity) * UnityEngine.Random.Range(0.7f, 1.3f);
                var seat = FreeSeat();
                if (seat) SpawnCustomer(seat);
            }
            // the counter prepares orders one at a time
            if (orders.Count > 0)
            {
                var c = orders.Peek();
                if (!c) { orders.Dequeue(); }
                else
                {
                    prep += dt;
                    if (prep >= c.Order.Prep * Mathf.Pow(0.85f, Data.Up("speed"))) { orders.Dequeue(); prep = 0f; c.Serve(); }
                }
            }
            saveTimer += dt; if (saveTimer > 20f) { saveTimer = 0f; Save(); }
        }

        public FurnitureInstance FreeSeat()
        {
            var free = Chairs.Where(c => !c.Occupant && RoomOpen(c.Placed.room)).ToList();
            return free.Count == 0 ? null : free[UnityEngine.Random.Range(0, free.Count)];
        }

        void SpawnCustomer(FurnitureInstance seat)
        {
            if (bag.Count == 0) { bag.AddRange(CafeData.CustomerVariants.OrderBy(_ => UnityEngine.Random.value)); }
            var v = bag[0]; bag.RemoveAt(0);
            var prefab = Resources.Load<GameObject>("Customers/Customer_" + v); if (!prefab) return;
            var from = RandomStreetEnd; var go = Instantiate(prefab, from.position, Quaternion.LookRotation(Door.position - from.position));
            var c = go.GetComponent<Customer>(); c.Begin(seat); Customers.Add(c);
        }
        public void PlaceOrder(Customer c) { orders.Enqueue(c); }
        public FurnitureInstance TableNear(Vector3 p)
        {
            FurnitureInstance best = null; float bd = 2.2f;
            foreach (var f in Furniture) if (f.Def.Table) { float d = Vector3.Distance(Flat(f.transform.position), Flat(p)); if (d < bd) { bd = d; best = f; } }
            return best;
        }
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        public void CustomerPaid(Customer c, long bill, long tip, int xp)
        {
            bill = Mathf.RoundToInt(bill * (1f + 0.1f * Data.Up("menu"))); tip = Mathf.RoundToInt(tip * (1f + 0.15f * Data.Up("tips")));
            AddTip(bill + tip); AddXp(xp); AudioManager.I?.Play("sfx_coin", 0.35f, 1.15f);
            CafeUI.I?.Float($"+{bill + tip}", c.transform.position + Vector3.up * 1.2f);
        }
        public bool BuyUpgrade(string id)
        {
            var u = CafeData.Upgrades.Find(x => x.Id == id); if (u == null) return false;
            int lv = Data.Up(id); if (lv >= u.MaxLevel) return false;
            if (!Spend(CafeData.UpgradeCost(u, lv))) return false;
            Data.SetUp(id, lv + 1); AudioManager.I?.Play("sfx_levelup", 0.6f, 1.2f); Toast?.Invoke($"{u.Label} level {lv + 1}!"); Save(); Changed?.Invoke(); return true;
        }
        public void CustomerLeft(Customer c) { Customers.Remove(c); }

        // ---------------------------------------------------------------- cats
        public void SpawnCat(string id)
        {
            var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + id); if (!prefab) { Debug.LogWarning("no cat " + id); return; }
            var spot = Furniture.Where(f => f.Def.Perches.Length > 0).OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
            var pos = spot ? spot.transform.position + spot.transform.forward * 0.9f : Door.position;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0));
            var cat = go.GetComponent<CafeCat>(); cat.Def = GameData.Cat(id); Cats.Add(cat);
        }
        public bool HasRoomForCat => Data.residents.Count < Mathf.Max(1, Beds);

        /// <summary>A new cat joins the café (from a stray or the shelter). Duplicates become friendship treats.</summary>
        public void Adopt(string id, out bool duplicate)
        {
            TutorialDone(3);
            duplicate = Data.residents.Contains(id) || Data.waiting.Contains(id);
            var rec = Data.CatOrNew(id);
            if (duplicate) { rec.treats++; rec.charm++; Recalc(); Save(); return; }
            if (HasRoomForCat) { Data.residents.Add(id); SpawnCat(id); }
            else Data.waiting.Add(id);
            AddXp(25); Recalc(); Save();
        }
        public void MoveInWaiting()
        {
            while (Data.waiting.Count > 0 && HasRoomForCat) { var id = Data.waiting[0]; Data.waiting.RemoveAt(0); Data.residents.Add(id); SpawnCat(id); Toast?.Invoke($"{id} moved in!"); }
            Recalc();
        }

        // ---------------------------------------------------------------- offline
        void Offline()
        {
            double hours = (DateTime.UtcNow.Ticks - Data.lastSeenUtc) / (double)TimeSpan.TicksPerHour;
            if (hours < 0.03) return;
            float h = Mathf.Min((float)hours, Data.offlineHours + 2 * Data.Up("offline"));
            float perHour = 3600f / CafeData.ArrivalInterval(Popularity);
            perHour = Mathf.Min(perHour, Seats * 3600f / 45f);    // a seat turns over about every 45 s
            float avgBill = (float)CafeData.Menu.Take(Data.menuTier).Average(m => m.Price) * 1.2f;
            long coins = (long)(perHour * h * avgBill * 0.5f);   // away earnings are half rate
            if (coins > 0) { Data.tipJar += coins; WelcomeBack?.Invoke(coins, h); }
            if (DateTime.UtcNow.Date.Ticks != Data.lastFreeTicketDay) { Data.lastFreeTicketDay = DateTime.UtcNow.Date.Ticks; Data.tickets++; }
        }

        public void Save() { Data?.Save(); }
        void OnApplicationPause(bool p) { if (p) Save(); }
        void OnApplicationQuit() => Save();
        public void Say(string msg) => Toast?.Invoke(msg);
        public const int TutorialSteps = 4;
        public void TutorialDone(int step)
        {
            if (Data.tutorial != step) return;
            Data.tutorial++; Save(); Changed?.Invoke();
            if (Data.tutorial >= TutorialSteps) { AddCoins(100); Toast?.Invoke("Tutorial complete! +100 coins"); }
        }
    }
}
