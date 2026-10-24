using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe
{
    [Serializable] public class PlacedFurn { public string id; public float x, z, yaw, y; public string room; }
    [Serializable] public class CatRec
    {
        public string cat; public int bond; public int charm; public int treats;   // treats = duplicate friendship treats
        public List<PoseRecord> poses = new();
        public int Hearts => Mathf.Clamp(bond / 20, 0, 5);
    }
    [Serializable] public class StrayRec { public string cat; public int trust; public long arrivedUtc; }   // trust 0..3
    [Serializable] public class WallPhoto { public string file; public string cat; public string pose; public int stars; }
    [Serializable] public class FeedPost { public string file; public string cat; public int stars; public long postedUtc; public int likesFinal; public int likesShown; }

    [Serializable]
    public class CafeSave
    {
        public int version = 3;
        public long coins = CafeData.StartCoins;
        public int tickets = CafeData.StartTickets;
        public int level = 1, xp;
        public long tipJar;
        public int followers;
        public int offlineHours = CafeData.MaxOfflineHoursBase;
        public int menuTier = 2;                 // number of unlocked menu items
        public List<string> rooms = new() { "counter" };
        public List<PlacedFurn> furniture = new();
        public List<string> residents = new();   // cats living in the café
        public List<string> waiting = new();     // adopted, waiting for a bed
        public List<CatRec> cats = new();        // every cat met (album + bond)
        public List<StrayRec> strays = new();
        public List<WallPhoto> wall = new();
        public List<FeedPost> feed = new();
        public long lastSeenUtc;
        public long lastFreeTicketDay;
        public bool music = true, sfx = true, haptics = true;
        public int tutorial;
        public List<string> upgradeIds = new(); public List<int> upgradeLevels = new();
        public int Up(string id) { int i = upgradeIds.IndexOf(id); return i < 0 ? 0 : upgradeLevels[i]; }
        public void SetUp(string id, int v) { int i = upgradeIds.IndexOf(id); if (i < 0) { upgradeIds.Add(id); upgradeLevels.Add(v); } else upgradeLevels[i] = v; }

        public CatRec Cat(string id) => cats.Find(c => c.cat == id);
        public CatRec CatOrNew(string id) { var c = Cat(id); if (c == null) { c = new CatRec { cat = id }; cats.Add(c); } return c; }

        const string Key = "pawcafe.save.v3";
        public static CafeSave Load()
        {
            CafeSave d = null;
            try { var j = PlayerPrefs.GetString(Key, ""); if (!string.IsNullOrEmpty(j)) d = JsonUtility.FromJson<CafeSave>(j); }
            catch (Exception e) { Debug.LogWarning("Café save corrupt: " + e.Message); }
            return d;
        }
        public void Save() { lastSeenUtc = DateTime.UtcNow.Ticks; PlayerPrefs.SetString(Key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static void Wipe() { PlayerPrefs.DeleteKey(Key); }
    }
}
