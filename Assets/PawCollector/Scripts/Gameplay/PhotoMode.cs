using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PawCollector
{
    /// <summary>
    /// Viewfinder mode: the camera eases in on one cat, a live meter shows how good the shot would be
    /// (facing you, glancing at the lens, peak of a pounce or swipe…) and the player times the shutter for 1–3 stars.
    /// </summary>
    public class PhotoMode : MonoBehaviour
    {
        public static PhotoMode I { get; private set; }
        public static bool Active => I && I.Target;

        public CatVisitor Target { get; private set; }
        public float Quality { get; private set; }
        public int LiveStars => StarsFor(Quality);
        float cooldown;

        void Awake() { I = this; }

        public static int StarsFor(float q) => q >= 0.85f ? 3 : q >= 0.6f ? 2 : 1;

        public void Enter(CatVisitor cat)
        {
            if (!cat || !GameManager.I.Playing) return;
            if (!cat.CanPhoto) { UIController.I?.Toast(cat.ShotsLeft <= 0 ? $"{cat.Def.Id} is camera-shy now — try playing or petting!" : $"{cat.Def.Id} is still arriving…"); return; }
            Target = cat; Quality = cat.PoseQuality;
            CameraRig.I.Focus(cat.transform);
            UIController.I?.ShowViewfinder(cat);
            AudioManager.I.Play("sfx_pop", 0.6f, 1.2f);
            if (GameManager.I.Data.tutorial == 2) GameManager.I.SetTutorial(3);
        }

        public void Exit()
        {
            if (!Target && !CameraRig.I.Focused) return;
            Target = null;
            CameraRig.I.Unfocus();
            UIController.I?.ShowViewfinder(null);
        }

        void Update()
        {
            if (!Target) { if (CameraRig.I && CameraRig.I.Focused && cooldown <= 0f) Exit(); return; }
            if (!Target.InGarden) { UIController.I?.Toast($"{Target.Def.Id} wandered off"); Exit(); return; }
            Quality = Mathf.Lerp(Quality, Target.PoseQuality, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (cooldown > 0f) cooldown -= Time.deltaTime;
        }

        public void Shutter()
        {
            if (!Target || cooldown > 0f) return;
            var cat = Target;
            if (cat.ShotsLeft <= 0) return;
            int stars = LiveStars;
            string pose = cat.PoseName;
            bool sparkle = cat.Sparkle && !cat.SparkleUsed;
            var photo = Capture();
            cat.OnPhotographed(stars);
            cooldown = 0.6f;
            CameraRig.I.Punch(); AudioManager.Haptic();
            GameManager.I.Snap(cat.Def, pose, stars, sparkle, photo);
            if (cat.ShotsLeft <= 0) Invoke(nameof(ExitIfSame), 2.3f);
        }

        void ExitIfSame() { if (Target && Target.ShotsLeft <= 0) Exit(); }

        /// <summary>Renders the current view (no UI) into a 4:5 photo.</summary>
        public static Texture2D Capture(int w = 480, int h = 600)
        {
            var cam = CameraRig.I.Cam;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }

    /// <summary>Keeps the player's best shot for every cat × pose on disk for the album.</summary>
    public static class PhotoStore
    {
        static readonly Dictionary<string, Texture2D> cache = new();
        static string Dir => Path.Combine(Application.persistentDataPath, "photos");
        static string Key(string cat, string pose) => $"{cat}_{pose}";

        public static void Save(string cat, string pose, Texture2D tex)
        {
            if (!tex) return;
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(Path.Combine(Dir, Key(cat, pose) + ".png"), tex.EncodeToPNG());
            }
            catch (System.Exception e) { Debug.LogWarning("Photo save failed: " + e.Message); }
            cache[Key(cat, pose)] = tex;
        }

        public static Texture2D Load(string cat, string pose)
        {
            var k = Key(cat, pose);
            if (cache.TryGetValue(k, out var t) && t) return t;
            var path = Path.Combine(Dir, k + ".png");
            if (!File.Exists(path)) return null;
            t = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) return null;
            cache[k] = t; return t;
        }

        /// <summary>The cover shot for a cat: its highest-starred pose.</summary>
        public static Texture2D Best(AlbumEntry e)
        {
            if (e == null) return null;
            PoseRecord best = null;
            foreach (var p in e.poses) if (best == null || p.stars > best.stars) best = p;
            return best != null ? Load(e.cat, best.pose) : null;
        }

        public static void Wipe()
        {
            cache.Clear();
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); } catch { }
        }
    }
}
