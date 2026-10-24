using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PawCollector.Cafe
{
    /// <summary>Visit a friend's café: your own furniture, cats, guests and strays step aside, and their rooms, furniture
    /// and cats (with the names they gave them) are shown in the same building. Leaving puts everything back.</summary>
    public class CafeVisit : MonoBehaviour
    {
        public static CafeVisit I { get; private set; }
        public static bool Active => I && I.active;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => I = null;

        bool active; public string HostName { get; private set; }
        readonly List<GameObject> spawned = new();
        readonly List<GameObject> hidden = new();
        public readonly List<(Transform t, string name)> Labels = new();

        public static CafeVisit Ensure() { if (I) return I; var go = new GameObject("CafeVisit"); return I = go.AddComponent<CafeVisit>(); }

        public void Begin(CafeCloud.Snapshot snap, string hostName)
        {
            if (active) End();
            var cm = CafeManager.I; if (!cm || snap == null) return;
            active = true; HostName = hostName; cm.Visiting = true;
            // your café steps aside
            foreach (var c in cm.Customers.ToList()) if (c) Destroy(c.gameObject);
            cm.Customers.Clear();
            foreach (var k in cm.Cats.ToList()) if (k) Destroy(k.gameObject);
            cm.Cats.Clear();
            foreach (var f in cm.Furniture) if (f && f.gameObject.activeSelf) { f.Occupant = null; f.PerchTaken = null; f.gameObject.SetActive(false); hidden.Add(f.gameObject); }
            foreach (var s in FindObjectsByType<Stray>()) if (s && s.gameObject.activeSelf) { s.gameObject.SetActive(false); hidden.Add(s.gameObject); }
            // their rooms
            foreach (var r in CafeData.Rooms) cm.SetRoomOpen(r.Id, snap.rooms.Contains(r.Id));
            // their furniture
            var perches = new List<(Vector3 pos, Quaternion rot, bool bed)>();
            foreach (var p in snap.furniture)
            {
                var def = CafeData.Furn(p.id); var prefab = Resources.Load<GameObject>("Furniture/" + p.id); if (def == null || !prefab) continue;
                var go = Puppet(prefab, new Vector3(p.x, p.y, p.z), Quaternion.Euler(0, p.yaw, 0)); spawned.Add(go);
                for (int i = 0; i < def.Perches.Length; i++)
                    perches.Add((go.transform.TransformPoint(def.Perches[i].Pos), go.transform.rotation * Quaternion.Euler(0, def.Perches[i].Yaw, 0), def.Perches[i].Kind is SpotKind.Bed or SpotKind.Cushion));
            }
            // their cats, napping and lounging around
            perches = perches.OrderBy(_ => Random.value).ToList(); int pi = 0;
            foreach (var c in snap.cats)
            {
                var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + c.id); if (!prefab) continue;
                Vector3 pos; Quaternion rot; string clip;
                if (pi < perches.Count) { var pp = perches[pi++]; pos = pp.pos; rot = pp.rot; clip = pp.bed && Random.value < 0.6f ? "Sleep" : "Idle_Sit"; }
                else
                {
                    var room = CafeData.Room(snap.rooms.Count > 0 ? snap.rooms[Random.Range(0, snap.rooms.Count)] : "counter");
                    var model = room != null ? GameObject.Find(room.Model) : null; var b = model ? model.GetComponent<Renderer>().bounds : new Bounds(Vector3.zero, Vector3.one * 4);
                    pos = new Vector3(Random.Range(b.min.x + 1f, b.max.x - 1f), 0, Random.Range(b.min.z + 1f, b.max.z - 1f));
                    if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
                    rot = Quaternion.Euler(0, Random.Range(0, 360f), 0); clip = Random.value < 0.5f ? "Groom" : "Idle_Sit";
                }
                var go = Puppet(prefab, pos, rot); spawned.Add(go);
                var an = go.GetComponentInChildren<Animator>(); if (an) an.CrossFadeInFixedTime(clip, 0f);
                var face = go.GetComponentInChildren<CatFace>(); if (face) face.EyesClosed = clip == "Sleep";
                Labels.Add((go.transform, string.IsNullOrEmpty(c.name) ? c.id : c.name));
            }
            var counter = CafeData.Room("counter"); var cg = counter != null ? GameObject.Find(counter.Model) : null;
            if (CafeCamera.I && cg) CafeCamera.I.LookAtPoint(cg.GetComponent<Renderer>().bounds.center);
        }

        public void End()
        {
            if (!active) return;
            var cm = CafeManager.I;
            foreach (var g in spawned) if (g) Destroy(g);
            spawned.Clear(); Labels.Clear();
            foreach (var g in hidden) if (g) g.SetActive(true);
            hidden.Clear();
            active = false;
            if (cm)
            {
                cm.Visiting = false;
                foreach (var r in CafeData.Rooms) cm.SetRoomOpen(r.Id, cm.Data.rooms.Contains(r.Id));
                foreach (var f in cm.Furniture) if (f) f.PerchTaken = new CafeCat[f.Def.Perches.Length];
                foreach (var id in cm.Data.residents) cm.SpawnCat(id);
            }
        }

        static GameObject Puppet(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var g = Instantiate(prefab, tmp.transform);
            foreach (var c in g.GetComponentsInChildren<FurnitureInstance>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<CafeCat>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshObstacle>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            g.transform.SetParent(null, false); g.transform.SetPositionAndRotation(pos, rot); Destroy(tmp);
            return g;
        }
    }
}
