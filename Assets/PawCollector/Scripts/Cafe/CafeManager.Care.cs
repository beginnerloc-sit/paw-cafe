using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Cat care (food, happiness, health, sickness, personal beds) and the shelter gacha.</summary>
    public partial class CafeManager
    {
        // ================================================================ beds
        void EnsureUids() { foreach (var p in Data.furniture) if (p.uid == 0) p.uid = Data.nextUid++; }

        /// <summary>Each resident cat claims a bed of its own. Cats keep their bed; new or bedless cats take a free one.</summary>
        public void AssignBeds()
        {
            EnsureUids();
            var beds = Furniture.Where(f => f && f.Def.Beds > 0).Select(f => f.Placed.uid).ToList();
            var taken = new HashSet<int>();
            foreach (var id in Data.residents) { var r = Data.CatOrNew(id); if (r.bedUid != 0 && beds.Contains(r.bedUid) && taken.Add(r.bedUid)) continue; r.bedUid = 0; }
            foreach (var id in Data.residents) { var r = Data.CatOrNew(id); if (r.bedUid != 0) continue; var free = beds.FirstOrDefault(b => !taken.Contains(b)); if (free != 0) { r.bedUid = free; taken.Add(free); } }
        }
        public FurnitureInstance BedOf(string id) { var r = Data.Cat(id); if (r == null || r.bedUid == 0) return null; return Furniture.FirstOrDefault(f => f && f.Placed.uid == r.bedUid); }
        public string BedOwner(FurnitureInstance f) => f == null ? null : Data.residents.FirstOrDefault(id => Data.Cat(id)?.bedUid == f.Placed.uid);
        public int BedsOwned => Furniture.Count(f => f && f.Def.Beds > 0);
        public int Price(FurnDef d) => d.Beds > 0 ? CafeData.BedPrice(d, BedsOwned) : d.Cost;

        // ================================================================ care
        void InitCare()
        {
            foreach (var id in Data.residents.Concat(Data.waiting))
            {
                var r = Data.CatOrNew(id);
                if (r.full < 0) r.full = 85; if (r.happy < 0) r.happy = 85; if (r.health < 0) r.health = 95;
            }
        }
        public enum Mood { Sick, Hungry, Sad, Okay, Content }
        public Mood MoodOf(string id)
        {
            var r = Data.Cat(id); if (r == null) return Mood.Okay;
            if (r.sick) return Mood.Sick;
            if (r.full < CafeData.LowNeed) return Mood.Hungry;
            if (r.happy < CafeData.LowNeed) return Mood.Sad;
            if (r.full >= 65 && r.happy >= 65 && r.health >= 70) return Mood.Content;
            return Mood.Okay;
        }
        public static string MoodWord(Mood m) => m switch { Mood.Sick => "Feeling sick", Mood.Hungry => "Hungry", Mood.Sad => "Lonely", Mood.Content => "Content", _ => "Doing fine" };
        /// <summary>Popularity a resident cat brings: sick cats bring none, neglected cats half, content cats a bonus.</summary>
        public int CatCharm(string id)
        {
            int baseCharm = 3 + (Data.Cat(id)?.charm ?? 0) + RarityBonus(id);
            return MoodOf(id) switch { Mood.Sick => 0, Mood.Hungry or Mood.Sad => Mathf.RoundToInt(baseCharm * 0.5f), Mood.Content => Mathf.RoundToInt(baseCharm * 1.25f), _ => baseCharm };
        }

        float careTick, minuteTick;
        void TickCare(float dt)
        {
            careTick += dt; if (careTick < 2f) return;
            float h = careTick / 3600f; careTick = 0f;
            bool changed = false;
            foreach (var id in Data.residents)
            {
                var r = Data.CatOrNew(id); var before = MoodOf(id);
                Decay(r, h, 0f, 0f, 0f);
                if (MoodOf(id) != before) { changed = true; if (MoodOf(id) == Mood.Sick) Toast?.Invoke($"{Data.Name(id)} is feeling sick — open their profile and give medicine"); else if (MoodOf(id) == Mood.Hungry) Toast?.Invoke($"{Data.Name(id)} is hungry!"); }
            }
            if (changed) Recalc();
            minuteTick += 2f; if (minuteTick >= 60f) { minuteTick = 0f; CheckFreeTicket(); }
        }
        /// <summary>Needs drop over time; neglect wears health down until the cat falls sick.</summary>
        static void Decay(CatRec r, float hours, float floorFull, float floorHappy, float floorHealth)
        {
            r.full = Mathf.Max(Mathf.Min(r.full, floorFull), r.full - CafeData.FullDecay * hours);
            r.happy = Mathf.Max(Mathf.Min(r.happy, floorHappy), r.happy - CafeData.HappyDecay * hours);
            bool neglected = r.full < 20 || r.happy < 20;
            if (r.sick) r.health = Mathf.Max(Mathf.Min(r.health, floorHealth), r.health - CafeData.HealthDecay * 0.25f * hours);
            else if (neglected) r.health = Mathf.Max(Mathf.Min(r.health, floorHealth), r.health - CafeData.HealthDecay * hours);
            else r.health = Mathf.Min(100, r.health + CafeData.HealthRegen * hours);
            if (!r.sick && r.health < CafeData.SickBelow) r.sick = true;
        }
        /// <summary>While you're away needs drop at full speed, but food bowls feed cats from your food stock.</summary>
        void OfflineCare(float hours)
        {
            if (hours <= 0) return;
            bool bowl = Furniture.Any(f => f && f.Def.Id == "FoodBowl");
            foreach (var id in Data.residents)
            {
                var r = Data.CatOrNew(id); float left = hours;
                while (left > 0)
                {
                    float step = Mathf.Min(1f, left); left -= step;
                    Decay(r, step, 5f, 10f, 20f);
                    if (bowl && r.full < 40 && Data.food > 0) { Data.food--; r.full = Mathf.Min(100, r.full + CafeData.BowlMeal); }
                }
            }
        }

        public bool Feed(string id, bool fromBowl = false)
        {
            var r = Data.Cat(id); if (r == null) return false;
            if (Data.food <= 0) { if (!fromBowl) Toast?.Invoke("Out of cat food — buy some in the Cats tab"); return false; }
            if (!fromBowl && r.full >= 95) { Toast?.Invoke($"{Data.Name(id)} is already full"); return false; }
            Data.food--; r.full = Mathf.Min(100, r.full + CafeData.BowlMeal); r.happy = Mathf.Min(100, r.happy + (fromBowl ? 3 : 6));
            if (!fromBowl) r.bond = Mathf.Min(100, r.bond + 2);
            Recalc(); Save(); return true;
        }
        public bool GiveMedicine(string id)
        {
            var r = Data.Cat(id); if (r == null || !r.sick) return false;
            if (Data.medicine <= 0) { Toast?.Invoke("No medicine — buy some in the Cats tab"); return false; }
            Data.medicine--; r.sick = false; r.health = Mathf.Max(r.health, 70f); r.happy = Mathf.Min(100, r.happy + 10);
            Toast?.Invoke($"{Data.Name(id)} is feeling better!"); AudioManager.I?.Play("sfx_levelup", 0.6f, 1.3f);
            Recalc(); Save(); return true;
        }
        public void Cheer(string id, float amount) { var r = Data.Cat(id); if (r == null) return; r.happy = Mathf.Min(100, r.happy + amount); }
        public bool BuyFood() { if (!Spend(CafeData.FoodPackCost)) return false; Data.food += CafeData.FoodPackSize; AudioManager.I?.Play("sfx_coin"); Save(); return true; }
        public bool BuyMedicine() { if (!Spend(CafeData.MedicineCost)) return false; Data.medicine++; AudioManager.I?.Play("sfx_coin"); Save(); return true; }
        public int CatsNeedingCare => Data.residents.Count(id => MoodOf(id) is Mood.Sick or Mood.Hungry or Mood.Sad);

        // ================================================================ shelter gacha
        public void CheckFreeTicket()
        {
            long today = DateTime.UtcNow.Date.Ticks;
            if (Data.lastFreeTicketDay == 0 || today - Data.lastFreeTicketDay >= TimeSpan.TicksPerDay * CafeData.FreeTicketDays)
            { bool first = Data.lastFreeTicketDay == 0; Data.lastFreeTicketDay = today; if (!first) { Data.tickets++; Toast?.Invoke("A free adoption ticket arrived!"); Changed?.Invoke(); } }
        }
        public TimeSpan NextFreeTicketIn => new DateTime(Data.lastFreeTicketDay == 0 ? DateTime.UtcNow.Date.Ticks : Data.lastFreeTicketDay).AddDays(CafeData.FreeTicketDays) - DateTime.UtcNow;
        public int AdoptFee => Data.adoptions == 0 ? 0 : CafeData.AdoptFee(CatsOwned);   // the very first adoption is free (starter guide)
        public int PullsToPity => Mathf.Max(1, CafeData.PityAt - Data.pity);

        /// <summary>Can the player adopt from the shelter right now? reason = what's missing.</summary>
        public bool CanAdopt(out string reason)
        {
            if (Data.tickets <= 0) { reason = "No adoption tickets"; return false; }
            if (FreeBeds <= 0) { reason = "Every cat needs its own bed — buy a cat bed first"; return false; }
            if (Data.coins < AdoptFee) { reason = $"The adoption fee is {AdoptFee} coins"; return false; }
            reason = null; return true;
        }

        public struct PullResult { public CatDef Cat; public Rarity Tier; public bool Duplicate, Featured, Pity; public int Shards; }
        /// <summary>One shelter adoption: rolls the rarity (with pity and the featured cat), adopts or turns a duplicate into shards.</summary>
        public bool Pull(out PullResult res)
        {
            res = default;
            if (!CanAdopt(out var why)) { Toast?.Invoke(why); return false; }
            Data.tickets--; Data.coins -= AdoptFee; Data.adoptions++;
            int roll = UnityEngine.Random.Range(0, 100), acc = 0; var tier = Rarity.Common;
            for (int i = 0; i < CafeData.TicketOdds.Length; i++) { acc += CafeData.TicketOdds[i]; if (roll < acc) { tier = (Rarity)i; break; } }
            bool pity = false;
            if (tier < Rarity.Rare && Data.pity + 1 >= CafeData.PityAt) { tier = UnityEngine.Random.value < 0.12f ? Rarity.Legendary : Rarity.Rare; pity = true; }
            Data.pity = tier >= Rarity.Rare ? 0 : Data.pity + 1;
            CatDef pick;
            {
                var pool = GameData.Cats.Where(x => x.Rarity == tier).ToList();
                var fresh = pool.Where(x => !Data.residents.Contains(x.Id) && !Data.waiting.Contains(x.Id)).ToList();
                var from = fresh.Count > 0 && UnityEngine.Random.value < 0.6f ? fresh : pool;
                pick = from[UnityEngine.Random.Range(0, from.Count)];
            }
            int shardsBefore = Data.shards;
            Adopt(pick.Id, out bool dup);
            res = new PullResult { Cat = pick, Tier = tier, Duplicate = dup, Featured = false, Pity = pity, Shards = Data.shards - shardsBefore };
            Changed?.Invoke(); Save(); return true;
        }
        public int ShardPrice(CatDef d) => CafeData.ShardPrice[(int)d.Rarity];
        /// <summary>Spend paw shards to adopt one specific cat you don't have yet (still needs a free bed).</summary>
        public bool ShardAdopt(string id)
        {
            var d = GameData.Cat(id); if (d == null || Data.residents.Contains(id) || Data.waiting.Contains(id)) return false;
            if (FreeBeds <= 0) { Toast?.Invoke("Every cat needs its own bed — buy a cat bed first"); return false; }
            if (Data.shards < ShardPrice(d)) { Toast?.Invoke("Not enough paw shards yet"); return false; }
            Data.shards -= ShardPrice(d); Adopt(id, out _); Changed?.Invoke(); return true;
        }
    }
}
