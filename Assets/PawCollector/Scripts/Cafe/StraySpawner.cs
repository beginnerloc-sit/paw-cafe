using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Puts strays on the window ledge over time (once the Window Nook is open).</summary>
    public class StraySpawner : MonoBehaviour
    {
        public static StraySpawner I { get; private set; }
        public Transform[] Ledge;
        readonly List<Stray> live = new(); float timer = 6f;
        void Awake() { I = this; }
        void Start() { foreach (var r in CafeManager.I.Data.strays.ToList()) Show(r); }
        void Update()
        {
            var cm = CafeManager.I; if (!cm || !cm.RoomOpen("nook")) return;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = Random.Range(90f, 200f) * Mathf.Pow(0.8f, cm.Data.Up("strays"));
            if (cm.Data.strays.Count >= Mathf.Min(2, Ledge.Length)) return;
            var owned = new HashSet<string>(cm.Data.residents.Concat(cm.Data.waiting).Concat(cm.Data.strays.Select(s => s.cat)));
            var pool = GameData.Cats.Where(c => c.Rarity != Rarity.Legendary && !owned.Contains(c.Id) && c.MinLevel <= cm.Data.level + 1).ToList();
            if (pool.Count == 0) return;
            float W(CatDef c) => c.Rarity == Rarity.Rare ? 0.2f : c.Rarity == Rarity.Uncommon ? 0.6f : 1f;
            float sum = pool.Sum(W), r = Random.value * sum; CatDef pick = pool[0];
            foreach (var c in pool) { r -= W(c); if (r <= 0) { pick = c; break; } }
            var rec = new StrayRec { cat = pick.Id, arrivedUtc = System.DateTime.UtcNow.Ticks };
            cm.Data.strays.Add(rec); Show(rec); cm.Say($"A stray is waiting at the window!"); AudioManager.I?.Meow(1.1f);
        }
        void Show(StrayRec rec)
        {
            int slot = Enumerable.Range(0, Ledge.Length).FirstOrDefault(i => live.All(s => s.transform.parent != Ledge[i]));
            var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + rec.cat); if (!prefab) return;
            var go = Instantiate(prefab, Ledge[slot]); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            foreach (var c in go.GetComponents<CafeCat>()) Destroy(c);
            var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag) Destroy(ag);
            var s = go.AddComponent<Stray>(); s.Rec = rec; s.Def = GameData.Cat(rec.cat); live.Add(s);
        }
        public void SpawnNow() { timer = 0f; }
        public void Remove(Stray s) { live.Remove(s); Destroy(s.gameObject); }
    }
}
