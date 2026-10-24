using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PawCollector.Cafe
{
    /// <summary>
    /// Background life on the shopping street across the road: passers-by stroll along the far pavement and stop to look at
    /// shop windows, and neighbourhood cats nap on benches, sit on awnings or wander about. Purely decorative — no colliders,
    /// no NavMesh, not part of the café's customers or cat list. Positions are in Blender units of the café model (×S, X/Z flipped).
    /// </summary>
    public class StreetLife : MonoBehaviour
    {
        public int Walkers = 6;
        public float StreetMinX = -8f, StreetMaxX = 12f;     // Blender X along the house row
        static readonly float[] Lanes = { 5.62f, 5.85f };     // Blender Y of the two walking lines on the far pavement
        Transform holder;

        static Vector3 B(float bx, float by, float bz) => new Vector3(-bx * CafeData.S, bz * CafeData.S, -by * CafeData.S);

        void Start()
        {
            holder = new GameObject("StreetLifeActors").transform; holder.SetParent(transform, false);
            var variants = CafeData.CustomerVariants;
            for (int i = 0; i < Walkers; i++)
            {
                var a = Spawn<Customer>("Customers/Customer_" + variants[(i * 3 + 1) % variants.Length]);
                if (a) StartCoroutine(Walker(a, i));
            }
            // neighbourhood cats: (cat id, Blender pos, yaw, clip)
            var cats = new (string id, Vector3 p, float yaw, string clip)[]
            {
                ("Pepper",  new Vector3(1.9f, 6.2f, 0.245f), 90f, "Sleep"),       // napping on a bench
                ("Pumpkin", new Vector3(-4.1f, 6.42f, 0.83f), 180f, "Idle_Sit"),   // sitting on an awning
                ("Snowy",   new Vector3(7.9f, 6.42f, 0.83f), 160f, "Groom"),       // grooming on an awning
                ("Oreo",    new Vector3(4.9f, 6.3f, 0.0f), 200f, "Idle_Sit"),      // by the shop door
            };
            foreach (var c in cats)
            {
                var g = Spawn<CafeCat>("CafeCats/CafeCat_" + PickAnyCat(c.id));
                if (!g) continue;
                g.transform.SetPositionAndRotation(B(c.p.x, c.p.y, c.p.z), Quaternion.Euler(0, c.yaw, 0));
                var an = g.GetComponentInChildren<Animator>(); if (an) { an.Play(c.clip, 0, Random.value); }
                if (c.clip == "Idle_Sit") StartCoroutine(CatFidget(an));
            }
            var stroller = Spawn<CafeCat>("CafeCats/CafeCat_" + PickAnyCat("Midnight"));
            if (stroller) StartCoroutine(CatStroll(stroller));
        }

        /// <summary>The wanted cat unless it already lives in the café (no doubles); otherwise any non-resident cat.</summary>
        string PickAnyCat(string want)
        {
            bool Free(string id) => !(CafeManager.I && CafeManager.I.Data.residents.Contains(id)) && !used.Contains(id) && Resources.Load<GameObject>("CafeCats/CafeCat_" + id);
            if (Free(want)) { used.Add(want); return want; }
            foreach (var d in GameData.Cats) if (d.Rarity == Rarity.Common && Free(d.Id)) { used.Add(d.Id); return d.Id; }
            return want;
        }
        readonly HashSet<string> used = new();

        /// <summary>Instantiate a character prefab as a lifeless puppet: game logic, agent and colliders removed before it ever wakes.</summary>
        GameObject Spawn<T>(string path) where T : Component
        {
            var prefab = Resources.Load<GameObject>(path); if (!prefab) return null;
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var g = Instantiate(prefab, tmp.transform);
            foreach (var c in g.GetComponentsInChildren<T>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            g.name = "Street_" + prefab.name; g.transform.SetParent(holder, false); Destroy(tmp);
            return g;
        }

        IEnumerator Walker(GameObject g, int i)
        {
            var an = g.GetComponentInChildren<Animator>();
            float lane = Lanes[i % Lanes.Length] + Random.Range(-0.04f, 0.04f);
            float x = Mathf.Lerp(StreetMinX, StreetMaxX, (i + Random.value) / Walkers);
            int dir = i % 2 == 0 ? 1 : -1;
            float speed = Random.Range(0.55f, 0.75f);               // Blender units / s (≈1.2–1.6 m/s in game)
            g.transform.position = B(x, lane, 0);
            yield return new WaitForSeconds(Random.value * 2f);
            while (true)
            {
                // stroll
                if (an) { an.CrossFadeInFixedTime("Walk", 0.2f); an.speed = speed / 0.65f; }
                g.transform.rotation = Quaternion.LookRotation(new Vector3(-dir, 0, 0));
                float leg = Random.Range(2.5f, 7f);
                for (float t = 0; t < leg; t += Time.deltaTime)
                {
                    x += dir * speed * Time.deltaTime;
                    if (x > StreetMaxX) { x = StreetMaxX; dir = -1; g.transform.rotation = Quaternion.LookRotation(Vector3.right); }
                    if (x < StreetMinX) { x = StreetMinX; dir = 1; g.transform.rotation = Quaternion.LookRotation(Vector3.left); }
                    g.transform.position = B(x, lane, 0);
                    yield return null;
                }
                // window shopping
                if (an) { an.speed = 1f; an.CrossFadeInFixedTime("Idle", 0.25f); }
                var face = Quaternion.LookRotation(Vector3.back);   // towards the shops (Blender +Y)
                for (float t = 0; t < 0.4f; t += Time.deltaTime) { g.transform.rotation = Quaternion.Slerp(g.transform.rotation, face, t / 0.4f); yield return null; }
                yield return new WaitForSeconds(Random.Range(1.5f, 4f));
                if (Random.value < 0.3f) dir = -dir;
            }
        }

        IEnumerator CatFidget(Animator an)
        {
            while (an)
            {
                yield return new WaitForSeconds(Random.Range(6f, 14f));
                if (!an) yield break;
                an.CrossFadeInFixedTime(Random.value < 0.5f ? "Groom" : "Happy", 0.25f);
                yield return new WaitForSeconds(Random.Range(2.5f, 4f));
                if (an) an.CrossFadeInFixedTime("Idle_Sit", 0.3f);
            }
        }

        IEnumerator CatStroll(GameObject g)
        {
            var an = g.GetComponentInChildren<Animator>();
            float x = 2f; int dir = -1; const float lane = 6.05f, speed = 0.35f;
            while (true)
            {
                if (an) { an.speed = 1f; an.CrossFadeInFixedTime("Walk", 0.2f); }
                g.transform.rotation = Quaternion.LookRotation(new Vector3(-dir, 0, 0));
                float leg = Random.Range(3f, 6f);
                for (float t = 0; t < leg; t += Time.deltaTime)
                {
                    x += dir * speed * Time.deltaTime;
                    if (x > StreetMaxX - 1f || x < StreetMinX + 1f) { dir = -dir; g.transform.rotation = Quaternion.LookRotation(new Vector3(-dir, 0, 0)); }
                    g.transform.position = B(x, lane, 0);
                    yield return null;
                }
                if (an) an.CrossFadeInFixedTime(Random.value < 0.5f ? "Sniff" : "Idle_Stand", 0.2f);
                yield return new WaitForSeconds(Random.Range(1.5f, 3.5f));
                if (Random.value < 0.4f) dir = -dir;
            }
        }
    }
}
