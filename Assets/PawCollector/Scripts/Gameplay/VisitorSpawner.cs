using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector
{
    /// <summary>Decides who drops by and how they get in. Treats attract specific cats; bond brings friends back more often.</summary>
    public class VisitorSpawner : MonoBehaviour
    {
        public static VisitorSpawner I { get; private set; }
        public Vector2 Interval = new(7f, 13f);
        public float CatScale = 0.8f;
        public readonly List<CatVisitor> Active = new();
        float next;

        void Awake() { I = this; next = Time.time + 3f; }

        public void SpawnSoon(float delay) => next = Time.time + delay;

        int Capacity
        {
            get
            {
                var gm = GameManager.I;
                int t = gm.Data.tutorial;
                if (t < 4) return 1;
                if (t < 6) return 2;
                return Mathf.Clamp(2 + gm.PlacedCount / 2, 2, 5);
            }
        }

        void Update()
        {
            Active.RemoveAll(v => v == null);
            var gm = GameManager.I;
            if (!gm.Playing || gm.Data.tutorial <= 1) return;          // first, put out a snack
            if (Time.time < next) return;
            next = Time.time + Random.Range(Interval.x, Interval.y) * (gm.PlacedCount == 0 ? 1.8f : 1f);
            if (Active.Count(a => a && !a.Leaving) >= Capacity) return;
            TrySpawn(gm.Data.tutorial <= 3 ? "Mochi" : null);
        }

        public CatVisitor TrySpawn(string forced)
        {
            var gm = GameManager.I;
            var present = new HashSet<string>(Active.Where(a => a).Select(a => a.Def.Id));
            CatDef cat = null;
            if (forced != null) { if (present.Contains(forced)) return null; cat = GameData.Cat(forced); }
            else
            {
                var placed = gm.Spots.Where(s => s.Unlocked && !string.IsNullOrEmpty(s.ItemId)).Select(s => s.ItemId).ToList();
                var pool = new List<(CatDef c, float w)>();
                foreach (var c in GameData.Cats)
                {
                    if (present.Contains(c.Id) || gm.Data.level < c.MinLevel || gm.PlacedCount < c.MinItemsPlaced) continue;
                    float w = c.Rarity == Rarity.Legendary ? 0.06f : c.Rarity == Rarity.Rare ? 0.25f : c.Rarity == Rarity.Uncommon ? 0.7f : 1f;
                    float like = 0f; foreach (var it in placed) if (c.Likes.TryGetValue(it, out var l)) like += l;
                    w *= 0.35f + like * 0.5f;                                        // treats they love bring them round
                    w *= 1f + 0.25f * (gm.Data.Entry(c.Id)?.Hearts ?? 0);           // friends visit more
                    if (!gm.Found(c.Id)) w *= 1.5f;
                    pool.Add((c, w));
                }
                if (pool.Count == 0) return null;
                float r = Random.value * pool.Sum(p => p.w);
                foreach (var p in pool) { r -= p.w; if (r <= 0) { cat = p.c; break; } }
                cat ??= pool[^1].c;
            }
            var prefab = Resources.Load<GameObject>("Cats/Cat_" + cat.Id);
            if (!prefab) { Debug.LogWarning("Missing cat prefab " + cat.Id); return null; }
            var entrance = GardenMap.I.RandomEntrance();
            var go = Instantiate(prefab);
            go.transform.localScale = Vector3.one * CatScale;
            var v = go.GetComponent<CatVisitor>();
            v.Begin(cat, entrance, Random.value < GameData.SparkleChance);
            Active.Add(v);
            UIController.I?.OnVisitor(v);
            return v;
        }
    }
}
