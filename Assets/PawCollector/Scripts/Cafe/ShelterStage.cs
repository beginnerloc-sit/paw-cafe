using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PawCollector.Cafe
{
    /// <summary>The shelter play yard (built at runtime far from the café). Cats wander, groom and nap. Adopting is a little
    /// minigame: wave a feather wand to catch their curiosity, then three cats walk toward you and only one — the cat that
    /// chose you — comes all the way.</summary>
    public class ShelterStage : MonoBehaviour
    {
        static ShelterStage inst;
        /// <summary>The live stage, or null (also null when a previous play session left a destroyed object behind).</summary>
        public static ShelterStage I { get => inst ? inst : null; private set => inst = value; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => inst = null;
        void OnDestroy() { if (inst == this) inst = null; if (CafeCamera.I && CafeCamera.I.Cam) CafeCamera.I.Cam.enabled = true; }
        public static readonly Vector3 Origin = new(400f, 0f, 0f);
        public bool Showing { get; private set; }
        public bool Playing { get; private set; }
        public Camera Cam { get; private set; }

        const float MinX = -2.3f, MaxX = 2.3f, MinZ = 0.4f, MaxZ = 7.5f;
        static readonly Vector3 FrontMark = new(0f, 0f, -1.6f);
        static readonly Vector3 CamYard = new(0f, 4.6f, -7.0f), LookYard = new(0f, 0.3f, 3.0f);   // closer: the yard fills the landscape view
        static readonly Vector3 CamClose = new(0f, 2.3f, -6.4f), LookClose = new(0f, 0.65f, -1.4f);

        class YardCat { public GameObject go; public CatDef def; public Animator anim; public Coroutine brain; public string clip; public Renderer sparkle; }
        readonly List<YardCat> cats = new();
        Renderer pool; Material glow; MaterialPropertyBlock mpb;
        Vector3 camPos = CamYard, camLook = LookYard; float camT;
        Color poolCol = Color.white; float poolA; Vector3 poolPos;
        Vector3? lure; float lureTime;
        bool skip;

        public static ShelterStage Ensure()
        {
            if (I) return I;
            var go = new GameObject("ShelterStage"); go.transform.position = Origin;
            return go.AddComponent<ShelterStage>();
        }

        void Awake()
        {
            I = this; mpb = new MaterialPropertyBlock();
            glow = Resources.Load<Material>("Shelter/Glow");
            var prop = CafeRefs.I ? CafeRefs.I.Prop : null;
            Make("Backdrop", Backdrop(14f, 12f, new Color(1f, 0.86f, 0.80f), new Color(0.76f, 0.86f, 1f)), prop, true);
            Make("Floor", Disc(16f, 56, new Color(0.62f, 0.86f, 0.52f), new Color(0.48f, 0.76f, 0.42f), 0f), prop, false);
            Make("Path", Disc(1.45f, 40, new Color(0.99f, 0.90f, 0.74f), new Color(0.95f, 0.83f, 0.66f), 0.01f), prop, false).transform.localPosition = new Vector3(0, 0, -1.6f);   // welcome spot (stone ring in the garden)
            Fence(prop);
            if (prop) ShelterGarden.Build(transform, prop);   // hills, trees, cottage, bushes, flowers, stepping stones, clouds
            Prop("CatTunnel", new Vector3(-0.7f, 0, 6.3f), 70f); Prop("FoodBowl", new Vector3(1.5f, 0, 2.9f), 0f);
            Prop("CatTree", new Vector3(-2.6f, 0, 6.2f), 160f); Prop("Beanbag", new Vector3(2.3f, 0, 5.2f), 200f); Prop("Cushion", new Vector3(-1.5f, 0, 2.6f), 0f);
            Prop("ToyBasket", new Vector3(2.0f, 0, 1.4f), 220f); Prop("Plant", new Vector3(3.2f, 0, 7.6f), 0f); Prop("Fern", new Vector3(-3.4f, 0, 3.8f), 0f);
            Prop("CatBed", new Vector3(0.9f, 0, 7.0f), 180f); Prop("Rug", new Vector3(0.2f, 0, 3.6f), 0f);
            pool = Make("Glow", Disc(0.9f, 40, new Color(1, 1, 1, 0.95f), new Color(1, 1, 1, 0f), 0.02f), glow, false);
            var cg = new GameObject("ShelterCam"); cg.transform.SetParent(transform, false);
            Cam = cg.AddComponent<Camera>(); Cam.fieldOfView = 40f; Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 60f;
            Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = new Color(0.76f, 0.86f, 1f); Cam.depth = 5; Cam.enabled = false;
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(Cam).renderPostProcessing = true;
            ApplyCam(true);
        }

        Renderer Make(string n, Mesh m, Material mat, bool unlit)
        {
            var g = new GameObject(n); g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = mat == glow ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            if (unlit) { r.GetPropertyBlock(mpb); mpb.SetFloat("_Unlit", 1f); r.SetPropertyBlock(mpb); mpb.Clear(); }
            return r;
        }
        void Prop(string id, Vector3 p, float yaw)
        {
            var prefab = Resources.Load<GameObject>("Furniture/" + id); if (!prefab) return;
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var g = Instantiate(prefab, tmp.transform);
            foreach (var c in g.GetComponentsInChildren<FurnitureInstance>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshObstacle>(true)) DestroyImmediate(c);
            g.transform.SetParent(transform, false); g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, yaw, 0); Destroy(tmp);
        }
        void Fence(Material prop)
        {
            var m = new Mesh(); var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            void Box(Vector3 centre, Vector3 size, Quaternion rot, Color col)
            {
                var h = size * 0.5f;
                Vector3[] p = { new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z), new(-h.x, -h.y, h.z), new(h.x, -h.y, h.z), new(h.x, h.y, h.z), new(-h.x, h.y, h.z) };
                int[][] f = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }, new[] { 0, 1, 5, 4 } };
                foreach (var q in f) { int s = v.Count; foreach (var i in q) { v.Add(centre + rot * p[i]); c.Add(col); } t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
            }
            var white = new Color(1f, 0.97f, 0.92f); float r = 4.65f; var ctr = new Vector3(0, 0, 4.2f);
            Vector3 At(float deg, float y) { float a = deg * Mathf.Deg2Rad; return ctr + new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r); }
            for (float a = -100f; a <= 100f; a += 7f) { var p = At(a, 0.45f); if (p.z >= 0.6f) Box(p, new Vector3(0.16f, 0.9f, 0.16f), Quaternion.identity, white); }
            for (int k = 0; k < 2; k++)
                for (float a = -100f; a < 100f; a += 7f)
                {
                    var p0 = At(a, 0.3f + k * 0.35f); var p1 = At(a + 7f, 0.3f + k * 0.35f);
                    if (p0.z < 0.6f || p1.z < 0.6f) continue;
                    var d = p1 - p0; Box((p0 + p1) * 0.5f, new Vector3(0.06f, 0.1f, d.magnitude + 0.05f), Quaternion.LookRotation(d), white);
                }
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            Make("Fence", m, prop, false);
        }

        // ------------------------------------------------------------------ show / hide
        public void Show(bool on)
        {
            Showing = on;
            if (Cam) Cam.enabled = on;
            if (CafeCamera.I) CafeCamera.I.Cam.enabled = !on;
            if (on) { if (!Playing && cats.Count == 0) FillYard(); }
            else { StopAllCoroutines(); Playing = false; ClearYard(); lure = null; poolA = 0; camPos = CamYard; camLook = LookYard; ApplyCam(true); }
        }

        /// <summary>Fill the yard with ~9 cats. Rarities follow the real odds (so a sparkle in the yard is a treat).</summary>
        void FillYard()
        {
            ClearYard();
            var chosen = new List<CatDef>(); int guard = 0;
            while (chosen.Count < 9 && guard++ < 200)
            {
                int roll = UnityEngine.Random.Range(0, 100), acc = 0; var tier = Rarity.Common;
                for (int i = 0; i < CafeData.TicketOdds.Length; i++) { acc += CafeData.TicketOdds[i]; if (roll < acc) { tier = (Rarity)i; break; } }
                if (tier == Rarity.Legendary && chosen.Any(c => c.Rarity == Rarity.Legendary)) tier = Rarity.Rare;
                var cand = GameData.Cats.Where(c => c.Rarity == tier && !chosen.Contains(c) && Resources.Load<GameObject>("CafeCats/CafeCat_" + c.Id)).ToList();
                if (cand.Count > 0) chosen.Add(cand[UnityEngine.Random.Range(0, cand.Count)]);
            }
            if (!chosen.Any(c => c.Rarity >= Rarity.Rare) && UnityEngine.Random.value < 0.7f && chosen.Count > 1)
            { var r = GameData.Cats.Where(c => c.Rarity == Rarity.Rare && !chosen.Contains(c)).OrderBy(_ => UnityEngine.Random.value).FirstOrDefault(); if (r != null) chosen[^1] = r; }
            foreach (var d in chosen) AddCat(d, RandomSpot());
        }
        YardCat AddCat(CatDef d, Vector3 at)
        {
            var go = Puppet(d.Id); if (!go) return null;
            go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
            var yc = new YardCat { go = go, def = d, anim = go.GetComponentInChildren<Animator>() };
            if (d.Rarity >= Rarity.Rare)
            {
                var sp = Make("Sparkle", Disc(0.55f, 24, new Color(1, 1, 1, 0.8f), new Color(1, 1, 1, 0), 0.03f), glow, false);
                sp.transform.SetParent(go.transform, false); yc.sparkle = sp;
                sp.GetPropertyBlock(mpb); mpb.SetColor("_Color", RarityColour(d.Rarity) * 0.7f); sp.SetPropertyBlock(mpb); mpb.Clear();
            }
            cats.Add(yc); yc.brain = StartCoroutine(Idle(yc));
            return yc;
        }
        void ClearYard() { foreach (var c in cats) { if (c.brain != null) StopCoroutine(c.brain); if (c.go) Destroy(c.go); } cats.Clear(); }
        static Vector3 RandomSpot() => new(UnityEngine.Random.Range(MinX, MaxX) * 0.85f, 0, UnityEngine.Random.Range(MinZ + 0.6f, MaxZ));

        GameObject Puppet(string id)
        {
            var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + id); if (!prefab) return null;
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            var g = Instantiate(prefab, tmp.transform);
            foreach (var c in g.GetComponentsInChildren<CafeCat>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(c);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            g.transform.SetParent(transform, false); Destroy(tmp);
            return g;
        }
        static void Anim(YardCat c, string clip, float fade = 0.2f, bool restart = false)
        {
            if (c?.anim == null || (c.clip == clip && !restart)) return;
            c.clip = clip;
            var face = c.go.GetComponentInChildren<CatFace>(); if (face) face.EyesClosed = clip == "Sleep";
            if (restart) c.anim.CrossFadeInFixedTime(clip, fade, 0, 0f); else c.anim.CrossFadeInFixedTime(clip, fade);
        }

        // ------------------------------------------------------------------ cat behaviour
        IEnumerator Idle(YardCat c)
        {
            Anim(c, UnityEngine.Random.value < 0.5f ? "Idle_Sit" : "Idle_Stand", 0f);
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 3f));
            while (true)
            {
                if (lure.HasValue) { yield return Curious(c); continue; }
                float r = UnityEngine.Random.value;
                if (r < 0.45f) yield return WalkTo(c, RandomSpot(), 0.55f);
                else if (r < 0.65f) { Anim(c, "Groom", 0.25f, true); yield return Wait(UnityEngine.Random.Range(2.5f, 4f)); }
                else if (r < 0.8f) { Anim(c, "Sleep", 0.5f); yield return Wait(UnityEngine.Random.Range(4f, 8f)); }
                else { Anim(c, "Idle_Sit", 0.3f); yield return Wait(UnityEngine.Random.Range(2f, 4f)); }
            }
        }
        IEnumerator Wait(float s) { for (float t = 0; t < s && !lure.HasValue; t += Time.deltaTime) yield return null; }
        IEnumerator WalkTo(YardCat c, Vector3 target, float speed)
        {
            var tr = c.go.transform; Anim(c, "Walk", 0.15f);
            while (tr && (Flat(tr.localPosition) - Flat(target)).magnitude > 0.05f && !lure.HasValue)
            {
                var d = Flat(target) - Flat(tr.localPosition); tr.localPosition += d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime);
                tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
                yield return null;
            }
            Anim(c, "Idle_Stand", 0.2f);
        }
        /// <summary>While the feather is waving: turn to it, creep a little closer, bat and pounce at it.</summary>
        IEnumerator Curious(YardCat c)
        {
            var tr = c.go.transform;
            if (c.clip == "Sleep" && UnityEngine.Random.value < 0.5f) { yield return new WaitForSeconds(0.8f); yield break; }   // too sleepy to care
            Anim(c, "Idle_Stand", 0.2f);
            float next = UnityEngine.Random.Range(0.4f, 1.4f);
            while (lure.HasValue)
            {
                next -= Time.deltaTime;
                var d = Flat(lure.Value) - Flat(tr.localPosition);
                if (d.sqrMagnitude > 0.01f) tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(d), Time.deltaTime * 5f);
                if (d.magnitude > 1.2f) { var np = tr.localPosition + d.normalized * 0.25f * Time.deltaTime; np.x = Mathf.Clamp(np.x, MinX, MaxX); np.z = Mathf.Clamp(np.z, MinZ, MaxZ); tr.localPosition = np; }
                if (next <= 0) { next = UnityEngine.Random.Range(0.9f, 2f); Anim(c, UnityEngine.Random.value < 0.5f ? "Bat" : "Pounce", 0.12f, true); }
                yield return null;
            }
        }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (!Showing) return;
            camT += Time.deltaTime;
            ApplyCam(false);
            if (pool)
            {
                pool.enabled = poolA > 0.01f; pool.transform.localPosition = poolPos;
                pool.GetPropertyBlock(mpb); mpb.SetColor("_Color", poolCol * new Color(1, 1, 1, poolA)); pool.SetPropertyBlock(mpb); mpb.Clear();
            }
            foreach (var c in cats) if (c.sparkle) { float k = 0.8f + 0.25f * Mathf.Sin(Time.time * 4f + c.def.Id.Length); c.sparkle.transform.localScale = new Vector3(k, 1, k); }
            if (lure.HasValue && Time.time - lureTime > 0.6f) lure = null;
        }
        void ApplyCam(bool snap)
        {
            float sway = Playing ? 0f : Mathf.Sin(camT * 0.3f) * 0.25f;
            var p = camPos + new Vector3(sway, 0, 0);
            Cam.transform.localPosition = snap ? p : Vector3.Lerp(Cam.transform.localPosition, p, Time.deltaTime * 2.2f);
            var want = Quaternion.LookRotation(transform.TransformPoint(camLook) - Cam.transform.position);
            Cam.transform.rotation = snap ? want : Quaternion.Slerp(Cam.transform.rotation, want, Time.deltaTime * 3f);
        }

        // ------------------------------------------------------------------ minigame
        public static Color RarityColour(Rarity r) => r switch
        {
            Rarity.Uncommon => new Color(0.45f, 1f, 0.68f),
            Rarity.Rare => new Color(0.42f, 0.66f, 1f),
            Rarity.Legendary => new Color(1f, 0.78f, 0.25f),
            _ => new Color(1f, 0.97f, 0.9f)
        };
        public void Skip() { if (Playing) skip = true; }

        /// <summary>The feather is waved at this screen point (cats react). Returns false if it's not over the yard.</summary>
        public bool Wave(Vector2 screen)
        {
            var ray = Cam.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float d)) return false;
            lure = transform.InverseTransformPoint(ray.GetPoint(d)); lureTime = Time.time;
            return true;
        }

        /// <summary>Three cats come toward you; two lose interest on the way; the one that chose you sits at your feet.</summary>
        public IEnumerator Choose(CatDef winner, Rarity tier, Action onArrive)
        {
            Playing = true; skip = false; lure = null;
            var win = cats.FirstOrDefault(c => c.def == winner) ?? AddCat(winner, new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0, MaxZ - 0.5f));
            var decoys = cats.Where(c => c != win).OrderByDescending(c => (int)c.def.Rarity + UnityEngine.Random.value * 1.5f).Take(2).ToList();
            foreach (var c in cats) { if (c.brain != null) StopCoroutine(c.brain); c.brain = null; if (c.anim) c.anim.speed = 1f; }
            foreach (var c in cats.Where(c => c != win && !decoys.Contains(c))) { Anim(c, UnityEngine.Random.value < 0.5f ? "Idle_Sit" : "Groom", 0.3f); var d = Flat(FrontMark - c.go.transform.localPosition); if (d.sqrMagnitude > 0.01f) c.go.transform.localRotation = Quaternion.LookRotation(d); }
            var runners = decoys.Append(win).Where(c => c != null && c.go).ToList();
            float[] lanes = { -1.3f, 0f, 1.3f };
            var targets = new Dictionary<YardCat, Vector3>(); int li = 0;
            foreach (var r in runners.OrderBy(_ => UnityEngine.Random.value)) targets[r] = new Vector3(lanes[li++ % 3], 0, FrontMark.z);
            if (win != null) targets[win] = FrontMark;
            var start = runners.ToDictionary(r => r, r => r.go.transform.localPosition);
            var stopAt = new Dictionary<YardCat, float>(); if (decoys.Count > 0) stopAt[decoys[0]] = 0.38f; if (decoys.Count > 1) stopAt[decoys[1]] = 0.66f;
            var stopped = new HashSet<YardCat>(); int tease = 0;
            AudioManager.I?.Meow(1.1f);
            camPos = Vector3.Lerp(CamYard, CamClose, 0.3f); camLook = Vector3.Lerp(LookYard, LookClose, 0.3f);
            poolCol = RarityColour(Rarity.Common); poolA = 0f;
            foreach (var r in runners) Anim(r, "Walk", 0.15f);
            float total = 4.4f + (int)tier * 0.5f, t = 0f;
            while (t < total && !skip)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / total), e = Mathf.SmoothStep(0, 1, k);
                foreach (var r in runners)
                {
                    if (stopped.Contains(r) || !r.go) continue;
                    if (stopAt.TryGetValue(r, out var s) && k >= s)
                    {
                        stopped.Add(r);
                        Anim(r, r == decoys[0] ? "Groom" : "Sleep", 0.3f, true);   // lost interest
                        r.go.transform.localRotation = Quaternion.Euler(0, UnityEngine.Random.value < 0.5f ? 90f : -90f, 0);
                        AudioManager.I?.Play("sfx_pop", 0.4f, 0.8f);
                        continue;
                    }
                    var d = Flat(targets[r] - start[r]);
                    r.go.transform.localPosition = Vector3.Lerp(start[r], targets[r], e);
                    if (d.sqrMagnitude > 0.01f) r.go.transform.localRotation = Quaternion.LookRotation(d);
                }
                if (win?.go)
                {
                    poolPos = Flat(win.go.transform.localPosition) + Vector3.up * 0.01f;
                    poolA = Mathf.MoveTowards(poolA, k > 0.25f ? 1f : 0.45f, Time.deltaTime * 1.5f);
                    float[] at = { 0.5f, 0.68f, 0.84f };
                    while (tease < (int)tier && k >= at[tease]) { tease++; poolCol = RarityColour((Rarity)tease); poolA = 1.6f; AudioManager.I?.Play("sfx_levelup", 0.45f, 0.9f + tease * 0.2f); }
                }
                if (k > 0.7f) { camPos = Vector3.Lerp(camPos, CamClose, Time.deltaTime * 1.5f); camLook = Vector3.Lerp(camLook, LookClose, Time.deltaTime * 1.5f); }
                yield return null;
            }
            // final state (also when skipped)
            for (int i = 0; i < decoys.Count; i++) if (!stopped.Contains(decoys[i]) && decoys[i].go)
                { Anim(decoys[i], i == 0 ? "Groom" : "Sleep", 0.2f); decoys[i].go.transform.localPosition = Vector3.Lerp(start[decoys[i]], targets[decoys[i]], i == 0 ? 0.38f : 0.66f); }
            camPos = CamClose; camLook = LookClose; poolCol = RarityColour(tier); poolA = 1f;
            if (win?.go)
            {
                win.go.transform.localPosition = FrontMark; win.go.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                poolPos = FrontMark + Vector3.up * 0.01f;
                Anim(win, "SitDown", 0.15f, true); yield return new WaitForSeconds(0.6f);
                Anim(win, "Happy", 0.15f, true);
                AudioManager.I?.Meow(1f); AudioManager.I?.Play("sfx_purr", 0.6f);
                Fx.Hearts(win.go.transform.position + Vector3.up * 0.9f, 12 + (int)tier * 8, 1.1f);
            }
            onArrive?.Invoke();
            Playing = false;
        }

        /// <summary>After the card: your new cat heads to the café and fresh cats wander into the yard.</summary>
        public void BackToYard()
        {
            StopAllCoroutines(); Playing = false; poolA = 0f; camPos = CamYard; camLook = LookYard; lure = null;
            if (Showing) FillYard();
        }

        // ------------------------------------------------------------------ meshes (vertex coloured)
        static Mesh Backdrop(float radius, float height, Color bottom, Color top)
        {
            var m = new Mesh(); var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            int seg = 40, rows = 8;
            for (int y = 0; y <= rows; y++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.Lerp(-110f, 110f, i / (float)seg) * Mathf.Deg2Rad, h = y / (float)rows;
                    v.Add(new Vector3(Mathf.Sin(a) * radius, h * height - 0.05f, Mathf.Cos(a) * radius)); c.Add(Color.Lerp(bottom, top, Mathf.Pow(h, 0.8f)));
                }
            for (int y = 0; y < rows; y++) for (int i = 0; i < seg; i++) { int k = y * (seg + 1) + i; t.AddRange(new[] { k, k + 1, k + seg + 1, k + 1, k + seg + 2, k + seg + 1 }); }
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        static Mesh Disc(float radius, int seg, Color inner, Color outer, float y)
        {
            var m = new Mesh(); var v = new List<Vector3> { new(0, y, 0) }; var c = new List<Color> { inner }; var t = new List<int>();
            int rings = 4;
            for (int r = 1; r <= rings; r++) for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2f / seg, k = r / (float)rings; v.Add(new Vector3(Mathf.Cos(a) * radius * k, y, Mathf.Sin(a) * radius * k)); c.Add(Color.Lerp(inner, outer, k * k)); }
            for (int i = 0; i < seg; i++) t.AddRange(new[] { 0, 1 + (i + 1) % seg, 1 + i });
            for (int r = 1; r < rings; r++) for (int i = 0; i < seg; i++)
            {
                int a0 = 1 + (r - 1) * seg + i, a1 = 1 + (r - 1) * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
}
