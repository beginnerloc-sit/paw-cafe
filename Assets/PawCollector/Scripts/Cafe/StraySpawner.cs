using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Now and then a single hungry stray turns up on the road behind the café — and it always comes with rain.
    /// Only one stray at a time; the rain stops once it's taken in.</summary>
    public class StraySpawner : MonoBehaviour
    {
        public static StraySpawner I { get; private set; }
        public Transform[] Ledge;                 // legacy (strays used to sit on the window ledge)
        Stray live; float timer = 45f;
        void Awake() { I = this; }
        void Start()
        {
            var cm = CafeManager.I;
            if (cm.Data.strays.Count > 1) cm.Data.strays.RemoveRange(1, cm.Data.strays.Count - 1);   // one at a time
            if (cm.Data.strays.Count == 1) Show(cm.Data.strays[0], false);
            CafeRain.Ensure().SetRaining(live != null, true);
        }
        void Update()
        {
            var cm = CafeManager.I; if (!cm || cm.Visiting) return;
            if (live || cm.Data.level < 2) return;
            timer -= Time.deltaTime;
            if (timer > 0) return;
            if (cm.Data.strays.Count > 0) { timer = Random.Range(150f, 300f); Show(cm.Data.strays[0], true); return; }   // a stray that ran off comes back with the rain
            timer = Random.Range(150f, 300f) * Mathf.Pow(0.8f, cm.Data.Up("strays"));
            var owned = new HashSet<string>(cm.Data.residents.Concat(cm.Data.waiting));
            var pool = GameData.Cats.Where(c => c.Rarity != Rarity.Legendary && !owned.Contains(c.Id) && c.MinLevel <= cm.Data.level + 1 && Resources.Load<GameObject>("CafeCats/CafeCat_" + c.Id)).ToList();
            if (pool.Count == 0) return;
            float W(CatDef c) => c.Rarity == Rarity.Rare ? 0.2f : c.Rarity == Rarity.Uncommon ? 0.6f : 1f;
            float sum = pool.Sum(W), r = Random.value * sum; CatDef pick = pool[0];
            foreach (var c in pool) { r -= W(c); if (r <= 0) { pick = c; break; } }
            var rec = new StrayRec { cat = pick.Id, arrivedUtc = System.DateTime.UtcNow.Ticks };
            cm.Data.strays.Add(rec); Show(rec, true);
        }
        void Show(StrayRec rec, bool announce)
        {
            var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + rec.cat); if (!prefab) return;
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var go = Instantiate(prefab, tmp.transform);
            foreach (var c in go.GetComponentsInChildren<CafeCat>(true)) DestroyImmediate(c);
            foreach (var a in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(a);
            var s = go.AddComponent<Stray>(); s.Rec = rec; s.Def = GameData.Cat(rec.cat);
            go.name = "Stray_" + rec.cat; go.transform.SetParent(null, false); Destroy(tmp);
            live = s;
            CafeRain.Ensure().SetRaining(true, !announce);
            if (announce)
            {
                CafeManager.I.Say("It's raining… a hungry stray is out on the street. Tap it to help!");
                AudioManager.I?.Play("sfx_meow" + Random.Range(1, 4), 0.6f, 1.35f);
                Invoke(nameof(LookAtStray), 3f);
            }
        }
        void LookAtStray() { if (live && CafeCamera.I && !CafePhoto.Active && !CafeVisit.Active) CafeCamera.I.LookAtPoint(live.transform.position); }
        public Stray Live => live;
        public void SpawnNow() { timer = 0f; }
        /// <summary>The stray runs off (lost a trust game). Its record — and trust — stays; it returns next rain.</summary>
        public void Flee(Stray s) { if (live == s) live = null; if (s) Destroy(s.gameObject); CafeRain.Ensure().SetRaining(false, false); timer = Random.Range(150f, 300f); }
        public void Remove(Stray s) { if (live == s) live = null; Destroy(s.gameObject); CafeRain.Ensure().SetRaining(false, false); timer = Random.Range(150f, 300f); }
    }
}
