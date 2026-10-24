using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector
{
    public struct SnapResult { public CatDef Cat; public int Fish, Gold, Xp, Stars, PrevStars; public bool IsNew, Sparkle, NewPose; public string Pose; public Texture2D Photo; }

    /// <summary>Owns the save, economy, progression and offline simulation.</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        public SaveData Data { get; private set; }
        public List<Spot> Spots { get; private set; } = new();
        public bool Playing { get; set; }

        public event Action Changed;
        public event Action<int, List<string>> LeveledUp;     // level, unlock lines
        public event Action<SnapResult> Snapped;
        public event Action<Spot> SpotChanged;
        public event Action<CatDef, int> BondUp;          // cat, new heart count

        public int OfflineVisits { get; private set; }
        public int OfflineFish { get; private set; }

        public int UnlockedSpots => GameData.SpotsForLevel(Data.level);
        public int PlacedCount => Spots.Count(s => s.Unlocked && !string.IsNullOrEmpty(s.ItemId));
        public int CatsFound => Data.album.Count(a => a.photos > 0);
        public bool Found(string cat) => (Data.Entry(cat)?.photos ?? 0) > 0;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Data = SaveData.Load();
            Spots = FindObjectsByType<Spot>(FindObjectsSortMode.None).OrderBy(s => s.Index).ToList();
        }

        void Start()
        {
            for (int i = 0; i < Spots.Count; i++)
            {
                var st = Data.spots[i];
                Spots[i].Init(i < UnlockedSpots, st.item, st.uses);
            }
            SimulateOffline();
        }

        void SimulateOffline()
        {
            OfflineVisits = 0; OfflineFish = 0;
            if (Data.lastSeenUtc <= 0) return;
            var minutes = (float)(DateTime.UtcNow - new DateTime(Data.lastSeenUtc, DateTimeKind.Utc)).TotalMinutes;
            minutes = Mathf.Min(minutes, GameData.OfflineCapHours * 60f);
            int budget = Mathf.FloorToInt(minutes / GameData.OfflineVisitMinutes);
            var rng = new System.Random();
            while (budget-- > 0)
            {
                var open = Spots.Where(s => s.Unlocked && !string.IsNullOrEmpty(s.ItemId)).ToList();
                if (open.Count == 0) break;
                var s = open[rng.Next(open.Count)];
                OfflineVisits++; OfflineFish += rng.Next(3, 7);
                ConsumeUse(s, false);
            }
        }

        public void CollectOffline()
        {
            if (OfflineFish <= 0) return;
            Data.fish += OfflineFish; OfflineFish = 0; OfflineVisits = 0;
            Persist();
        }

        public bool TryPlace(Spot spot, string itemId, out string reason)
        {
            var def = GameData.Item(itemId);
            reason = null;
            if (def == null) { reason = "Unknown item"; return false; }
            if (!spot.Unlocked) { reason = $"Unlocks at garden level {LevelForSpot(spot.Index)}"; return false; }
            if (!string.IsNullOrEmpty(spot.ItemId)) { reason = "That mat already has something on it"; return false; }
            if (Data.level < def.UnlockLevel) { reason = $"{def.Label} unlocks at level {def.UnlockLevel}"; return false; }
            if (Data.fish < def.Cost) { reason = "Not enough fish — snap more cats!"; return false; }
            Data.fish -= def.Cost;
            spot.SetItem(itemId, def.Uses, true);
            Persist();
            return true;
        }

        public void ConsumeUse(Spot spot, bool persist = true)
        {
            if (string.IsNullOrEmpty(spot.ItemId)) return;
            int uses = spot.Uses - 1;
            if (uses <= 0) spot.SetItem("", 0, false); else spot.SetItem(spot.ItemId, uses, false);
            if (persist) Persist();
        }

        public static int LevelForSpot(int index)
        {
            for (int l = 1; l < 50; l++) if (GameData.SpotsForLevel(l) > index) return l;
            return 99;
        }

        AlbumEntry EnsureEntry(string cat)
        {
            var e = Data.Entry(cat);
            if (e == null) { e = new AlbumEntry { cat = cat }; Data.album.Add(e); }
            return e;
        }

        /// <summary>Bond grows with photos, play and petting. Hearts (every 20 bond) bring longer, more frequent visits and gifts.</summary>
        public void AddBond(CatDef cat, int amount, bool persist = true)
        {
            var e = EnsureEntry(cat.Id);
            int before = e.Hearts;
            e.bond = Mathf.Min(GameData.MaxBond, e.bond + amount);
            if (e.Hearts > before) BondUp?.Invoke(cat, e.Hearts);
            if (persist) Persist();
        }

        public int Gift(CatDef cat)
        {
            int fish = UnityEngine.Random.Range(5, 11) + 2 * (Data.Entry(cat.Id)?.Hearts ?? 0);
            Data.fish += fish; Persist();
            return fish;
        }

        public SnapResult Snap(CatDef cat, string pose, int stars, bool sparkle, Texture2D photo)
        {
            var e = EnsureEntry(cat.Id);
            bool isNew = e.photos == 0;
            e.photos++; if (sparkle) e.sparkles++;
            var rec = e.Pose(pose); bool newPose = rec == null;
            if (newPose) { rec = new PoseRecord { pose = pose }; e.poses.Add(rec); }
            int prev = rec.stars;
            if (stars > prev) { rec.stars = stars; PhotoStore.Save(cat.Id, pose, photo); }
            var r = new SnapResult { Cat = cat, IsNew = isNew, Sparkle = sparkle, Stars = stars, PrevStars = prev, NewPose = newPose, Pose = pose, Photo = photo };
            r.Fish = isNew ? UnityEngine.Random.Range(cat.Gift.x, cat.Gift.y + 1) : 1 + 2 * stars;
            if (stars > prev) r.Fish += 3 * (stars - prev);
            if (sparkle) r.Fish *= 2;
            r.Gold = (cat.Rarity == Rarity.Legendary ? 5 : cat.Rarity == Rarity.Rare ? 2 : 0) + (sparkle ? 1 : 0) + (stars == 3 && prev < 3 ? 1 : 0);
            r.Xp = (isNew ? GameData.XpNewCat : 0) + 4 * stars + (newPose ? 12 : 0) + (sparkle ? GameData.XpSparkle : 0);
            Data.fish += r.Fish; Data.gold += r.Gold;
            if (Data.tutorial == 2 || Data.tutorial == 3) Data.tutorial = 4;
            Snapped?.Invoke(r);
            AddBond(cat, GameData.BondPerPhoto, false);
            AddXp(r.Xp);
            Persist();
            return r;
        }

        public void AddXp(int xp)
        {
            Data.xp += xp;
            while (Data.xp >= GameData.XpForLevel(Data.level))
            {
                Data.xp -= GameData.XpForLevel(Data.level);
                int before = UnlockedSpots;
                Data.level++;
                Data.fish += 20; Data.gold += 1;
                var lines = new List<string> { "+20 fish  ·  +1 gold fish" };
                foreach (var it in GameData.Items.Where(i => i.UnlockLevel == Data.level)) lines.Add($"New item: {it.Label}");
                int newCats = GameData.Cats.Count(c => c.MinLevel == Data.level);
                if (newCats > 0) lines.Add(newCats == 1 ? "A new cat may visit…" : $"{newCats} new cats may visit…");
                for (int i = before; i < UnlockedSpots && i < Spots.Count; i++) { Spots[i].SetUnlocked(true); lines.Add("New garden mat unlocked"); }
                LeveledUp?.Invoke(Data.level, lines);
            }
        }

        public void SetTutorial(int step) { Data.tutorial = step; Persist(); }

        public void Persist()
        {
            for (int i = 0; i < Spots.Count && i < Data.spots.Count; i++)
            {
                Data.spots[i].item = Spots[i].ItemId ?? "";
                Data.spots[i].uses = Spots[i].Uses;
            }
            Data.Save();
            Changed?.Invoke();
        }

        public void NotifySpot(Spot s) => SpotChanged?.Invoke(s);

        public void ResetProgress()
        {
            SaveData.Wipe();
            PhotoStore.Wipe();
            Data = SaveData.Load();
            for (int i = 0; i < Spots.Count; i++) Spots[i].Init(i < UnlockedSpots, "", 0);
            foreach (var v in FindObjectsByType<CatVisitor>(FindObjectsSortMode.None)) Destroy(v.gameObject);
            Persist();
        }

        void OnApplicationPause(bool paused) { if (paused && Data != null) Persist(); }
        void OnApplicationQuit() { if (Data != null) Persist(); }
    }
}
