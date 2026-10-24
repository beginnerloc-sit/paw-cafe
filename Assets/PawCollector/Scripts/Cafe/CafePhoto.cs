using System;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Photo mode in the café: ease in on a cat (or stray), snap for 1–3 stars, fulfil customer requests, pin to the wall, post to the feed.</summary>
    public class CafePhoto : MonoBehaviour
    {
        public static CafePhoto I { get; private set; }
        public static bool Active => I && I.target;
        public Transform Target => target;
        public CafeCat Cat { get; private set; }
        public Stray StrayTarget { get; private set; }
        public float Quality { get; private set; }
        public int LiveStars => Quality >= 0.85f ? 3 : Quality >= 0.62f ? 2 : 1;
        public string LivePose => Cat ? Cat.PoseName : "Sit";
        public string CatId => Cat ? Cat.Def.Id : StrayTarget ? StrayTarget.Def.Id : null;
        Transform target; float cooldown;
        public event Action<Texture2D, string, string, int, bool> Snapped;   // photo, cat, pose, stars, fulfilled request

        void Awake() { I = this; }
        public void Enter(CafeCat cat) { Cat = cat; StrayTarget = null; Begin(cat.transform); }
        public void Enter(Stray s) { Cat = null; StrayTarget = s; Begin(s.transform); }
        void Begin(Transform t) { target = t; Quality = 0.5f; CafeCamera.I.Focus(t); CafeUI.I?.ShowViewfinder(true); AudioManager.I?.Play("sfx_pop", 0.6f, 1.2f); }
        public void Exit() { target = null; Cat = null; StrayTarget = null; CafeCamera.I.Unfocus(); CafeUI.I?.ShowViewfinder(false); }

        void Update()
        {
            if (!target || !CafeCamera.I) return;
            float q = Cat ? Cat.PoseQuality(CafeCamera.I.Cam) : 0.75f;
            Quality = Mathf.Lerp(Quality, q, 1f - Mathf.Exp(-10f * Time.deltaTime));
            cooldown -= Time.deltaTime;
        }

        public void Shutter()
        {
            if (!target || cooldown > 0) return;
            cooldown = 0.7f;
            string cat = CatId, pose = StrayTarget ? "Sit" : LivePose; int stars = LiveStars;
            var tex = Capture();
            CafeCamera.I.Punch(); AudioManager.I?.Play("sfx_snap"); AudioManager.Haptic();
            var cm = CafeManager.I; var rec = cm.Data.CatOrNew(cat);
            var pr = rec.poses.Find(x => x.pose == pose);
            if (pr == null) { rec.poses.Add(new PoseRecord { pose = pose, stars = stars }); PhotoStore.Save(cat, pose, tex); cm.AddXp(3); }
            else if (stars > pr.stars) { pr.stars = stars; PhotoStore.Save(cat, pose, tex); }
            if (rec.poses.Count >= 6 && rec.charm < 5) rec.charm = Mathf.Max(rec.charm, 5);
            bool fulfilled = false;
            foreach (var c in cm.Customers.ToList()) if (c && c.TryFulfil(cat, pose, stars)) fulfilled = true;   // one great shot can make several customers happy
            if (StrayTarget) StrayTarget.Photographed();
            Snapped?.Invoke(tex, cat, pose, stars, fulfilled);
            CafeManager.I?.TutorialDone(1);
            cm.Recalc(); cm.Save();
        }

        public static Texture2D Capture(int w = 480, int h = 600)
        {
            var cam = CafeCamera.I.Cam;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = cam.targetTexture; var act = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt); return tex;
        }

        // ------------------------------------------------------------ wall & feed
        public static string SaveFile(Texture2D tex, string prefix)
        {
            var name = $"{prefix}_{DateTime.UtcNow.Ticks}.png";
            try { var dir = System.IO.Path.Combine(Application.persistentDataPath, "cafe"); System.IO.Directory.CreateDirectory(dir); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name), tex.EncodeToPNG()); }
            catch (Exception e) { Debug.LogWarning(e.Message); }
            return name;
        }
        public static Texture2D LoadFile(string name)
        {
            try { var p = System.IO.Path.Combine(Application.persistentDataPath, "cafe", name); if (!System.IO.File.Exists(p)) return null; var t = new Texture2D(2, 2); t.LoadImage(System.IO.File.ReadAllBytes(p)); return t; }
            catch { return null; }
        }
        public int FrameCount => CafeManager.I.Furniture.Count(f => f.Def.Id == "PhotoFrame");
        public bool PinToWall(Texture2D tex, string cat, string pose, int stars)
        {
            var cm = CafeManager.I;
            if (cm.Data.wall.Count >= FrameCount)
            {
                // replace the weakest framed photo if this one is better
                var worst = cm.Data.wall.OrderBy(w => w.stars).FirstOrDefault();
                if (worst == null || worst.stars > stars) { cm.Say("Buy another photo frame to pin more photos"); return false; }
                cm.Data.wall.Remove(worst);
            }
            cm.Data.wall.Add(new WallPhoto { file = SaveFile(tex, "wall"), cat = cat, pose = pose, stars = stars });
            RefreshFrames(); cm.Recalc(); cm.Save(); return true;
        }
        public void RefreshFrames()
        {
            var frames = CafeManager.I.Furniture.Where(f => f.Def.Id == "PhotoFrame").ToList();
            for (int i = 0; i < frames.Count; i++)
            {
                var pic = frames[i].transform.Find("Pic"); if (!pic) continue;
                var r = pic.GetComponent<Renderer>(); var wp = i < CafeManager.I.Data.wall.Count ? CafeManager.I.Data.wall[i] : null;
                var tex = wp != null ? LoadFile(wp.file) : null;
                var mpb = new MaterialPropertyBlock(); if (tex) mpb.SetTexture("_BaseMap", tex); r.SetPropertyBlock(mpb);
                r.enabled = tex;
            }
        }
        void Start() { RefreshFrames(); }

        public void Post(Texture2D tex, string cat, int stars)
        {
            var cm = CafeManager.I; var d = GameData.Cat(cat);
            int rar = d != null ? (int)d.Rarity : 0;
            int likes = Mathf.RoundToInt((20 + stars * 35 + rar * 60 + UnityEngine.Random.Range(0, 40)) * (1f + cm.Data.followers / 400f));
            cm.Data.feed.Insert(0, new FeedPost { file = SaveFile(tex, "feed"), cat = cat, stars = stars, postedUtc = DateTime.UtcNow.Ticks, likesFinal = likes });
            if (cm.Data.feed.Count > 40) cm.Data.feed.RemoveAt(cm.Data.feed.Count - 1);
            CafeCloud.I?.PostPhoto(tex, cat, cm.Data.Name(cat), null, stars);   // shared with friends on the Whisker Feed
            cm.AddXp(2); cm.Save();
        }
        /// <summary>Likes trickle in over two hours; matured likes bring followers; follower milestones give tickets.</summary>
        public static void TickFeed(CafeSave d)
        {
            long now = DateTime.UtcNow.Ticks; int before = d.followers / CafeData.FollowersPerTicket;
            foreach (var p in d.feed)
            {
                float t = Mathf.Clamp01((float)((now - p.postedUtc) / (double)TimeSpan.TicksPerHour) / 2f);
                int shown = Mathf.RoundToInt(p.likesFinal * (1f - Mathf.Pow(1f - t, 2f)));
                if (shown > p.likesShown) { d.followers += (shown - p.likesShown) / 8 + (UnityEngine.Random.value < ((shown - p.likesShown) % 8) / 8f ? 1 : 0); p.likesShown = shown; }
            }
            int after = d.followers / CafeData.FollowersPerTicket; if (after > before) { d.tickets += after - before; CafeManager.I?.Say("New follower milestone — adoption ticket earned!"); }
        }
    }
}
