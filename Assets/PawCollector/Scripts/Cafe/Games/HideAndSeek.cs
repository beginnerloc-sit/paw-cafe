using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>Hide &amp; Seek in your real café: your cat hides behind the furniture — find it and tap it before time runs out.
    /// Six rounds, each a little quicker. A wrong tap costs a second.</summary>
    public class HideAndSeek : MiniGame
    {
        public override string Id => "hide";
        public override string Title => "Hide & Seek";
        public override string HowTo => "Your cat is hiding somewhere in the café. Find it and tap it!";
        public override string IconPath => "Games/cloud";
        public override int Unlock => 5;
        public override int[] Stars => new[] { 150, 300, 450 };
        public override bool UsesWorld => true;
        public override Color TileCol => Hex(214, 242, 232);
        public override float CoinsPerPoint => 0.15f;
        const int Rounds = 6;

        int round; GameObject puppet; Animator anim; Vector3 headPos; float roundLen, revealT = -1;
        readonly List<Renderer> hidden = new(); float prevZoomTarget; Label roundL;

        protected override void Setup()
        {
            Lives = 3; MaxLives = 3;
            // hide the real cats so there is only one to find
            foreach (var c in CafeManager.I.Cats) if (c) foreach (var r in c.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            var pts = CafeManager.I.Furniture.Where(f => f && CafeManager.I.RoomOpen(f.Placed.room)).Select(f => f.transform.position).ToList();
            var centre = pts.Count > 0 ? new Vector3(pts.Average(p => p.x), 0, pts.Average(p => p.z)) : Vector3.zero;
            CafeCamera.I.LookAtPoint(centre); CafeCamera.I.SetZoom(1.45f);
            roundL = new Label(""); roundL.AddToClassList("ghide-round"); roundL.pickingMode = PickingMode.Ignore; S.Add(roundL);
            round = 0; NextRound();
        }
        void NextRound()
        {
            if (puppet) Object.Destroy(puppet);
            round++; if (round > Rounds) { Finish(true, "Found every time!"); return; }
            roundL.text = $"Round {round} of {Rounds}";
            roundLen = Mathf.Max(7f, 15f - round * 1.4f); TimeLeft = roundLen; revealT = -1;
            Hide();
        }
        void Hide()
        {
            var prefab = Resources.Load<GameObject>("CafeCats/CafeCat_" + (Hero != null ? Hero.Id : "Mochi")); if (!prefab) { Finish(false, "No cat to hide"); return; }
            var tmp = new GameObject("tmp"); tmp.SetActive(false);
            puppet = Object.Instantiate(prefab, tmp.transform);
            foreach (var c in puppet.GetComponentsInChildren<CafeCat>(true)) Object.DestroyImmediate(c);
            foreach (var a in puppet.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(a);
            puppet.transform.SetParent(null, false); Object.Destroy(tmp);
            // behind (further from the camera) or beside a random piece of furniture
            var cam = CafeCamera.I.Cam; var furn = CafeManager.I.Furniture.Where(f => f && !f.Def.WallMounted && f.Def.Id != "Rug" && CafeManager.I.RoomOpen(f.Placed.room)).ToList();
            Vector3 pos = Vector3.zero;
            for (int tries = 0; tries < 30 && furn.Count > 0; tries++)
            {
                var f = furn[UnityEngine.Random.Range(0, furn.Count)];
                var away = f.transform.position - cam.transform.position; away.y = 0; away.Normalize();
                var side = Vector3.Cross(Vector3.up, away);
                var cand = f.transform.position + away * Rnd(0.55f, 0.85f) + side * Rnd(-0.45f, 0.45f);
                var vp = cam.WorldToViewportPoint(cand); if (vp.z <= 0 || vp.x < 0.12f || vp.x > 0.88f || vp.y < 0.12f || vp.y > 0.8f) continue;
                if (NavMesh.SamplePosition(cand, out var hit, 0.6f, NavMesh.AllAreas)) { pos = hit.position; break; }
            }
            puppet.transform.position = pos; puppet.transform.rotation = Quaternion.Euler(0, Rnd(0, 360), 0);
            anim = puppet.GetComponentInChildren<Animator>();
            if (anim) anim.CrossFadeInFixedTime(UnityEngine.Random.value < 0.5f ? "Idle_Sit" : "Sleep", 0f);
            headPos = pos + Vector3.up * 0.45f;
        }
        public override void Down(Vector2 p)
        {
            if (!puppet || revealT >= 0) return;
            var cam = CafeCamera.I.Cam; var sp = cam.WorldToScreenPoint(headPos); if (sp.z <= 0) return;
            // stage point -> panel -> camera pixels (the stage sits inside the safe area)
            var wp = S.LocalToWorld(p); var ps = S.panel.visualTree.layout.size;
            var screen = new Vector2(wp.x / ps.x * cam.pixelWidth, (1f - wp.y / ps.y) * cam.pixelHeight);
            float tol = Mathf.Max(70f, cam.pixelHeight * 0.09f);
            if (((Vector2)sp - screen).magnitude < tol)
            {
                int pts = 10 + Mathf.CeilToInt(TimeLeft * 5f); Score += pts; Pop($"Found! +{pts}", p, Hex(255, 110, 160), 1.2f); Sfx("sfx_meow" + UnityEngine.Random.Range(1, 4), 0.7f, 1.2f);
                if (anim) anim.CrossFadeInFixedTime("Happy", 0.1f);
                revealT = 0.9f;
            }
            else { TimeLeft = Mathf.Max(0.01f, TimeLeft - 1f); Pop("Not here…", p, Hex(255, 255, 255), 0.8f); Sfx("sfx_tap", 0.4f, 0.8f); }
        }
        protected override void Tick(float dt)
        {
            if (revealT >= 0) { revealT -= dt; TimeLeft = Mathf.Max(TimeLeft, 0.5f); if (revealT < 0) NextRound(); }
        }
        protected override void TimeUp()
        {
            if (revealT >= 0) return;
            Pop("There it was!", new Vector2(W / 2, H * 0.3f), Hex(255, 255, 255)); LoseLife("Your cat is too good at hiding!");
            if (!Over) { revealT = 1.2f; TimeLeft = 2f; if (anim) anim.CrossFadeInFixedTime("Idle_Stand", 0.1f); }
        }
        public override void Cleanup()
        {
            if (puppet) Object.Destroy(puppet);
            foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear();
            CafeCamera.I?.SetZoom(1f);
        }
    }
}
