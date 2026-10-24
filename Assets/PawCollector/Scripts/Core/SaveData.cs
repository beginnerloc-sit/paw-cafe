using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    [Serializable] public class PoseRecord { public string pose; public int stars; }
    [Serializable] public class AlbumEntry
    {
        public string cat; public int photos; public int sparkles; public int bond;
        public List<PoseRecord> poses = new();
        public int Hearts => Mathf.Clamp(bond / 20, 0, 5);
        public PoseRecord Pose(string p) => poses.Find(x => x.pose == p);
    }
    [Serializable] public class SpotState { public string item = ""; public int uses; }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int fish = GameData.StartFish;
        public int gold;
        public int level = 1;
        public int xp;
        public int tutorial;              // 0 intro, 1 place item, 2 wait/tap cat, 3 shutter, 4 play with toy, 5 pet, 6 done
        public bool music = true, sfx = true, haptics = true;
        public long lastSeenUtc;
        public List<AlbumEntry> album = new();
        public List<SpotState> spots = new();

        const string Key = "pawcollector.save.v1";

        public AlbumEntry Entry(string cat) => album.Find(a => a.cat == cat);
        public List<string> metCats = new();

        public static SaveData Load()
        {
            SaveData d = null;
            try { var json = PlayerPrefs.GetString(Key, ""); if (!string.IsNullOrEmpty(json)) d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning($"Save corrupt, starting fresh: {e.Message}"); }
            d ??= new SaveData();
            while (d.spots.Count < GameData.MaxSpots) d.spots.Add(new SpotState());
            return d;
        }

        public void Save()
        {
            lastSeenUtc = DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Wipe() { PlayerPrefs.DeleteKey(Key); }
    }
}
