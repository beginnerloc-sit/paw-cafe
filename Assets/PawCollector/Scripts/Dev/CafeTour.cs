#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using PawCollector.Cafe;

namespace PawCollector
{
    /// <summary>
    /// Editor QA: plays one customer visit end to end (arrive → order food → served → photo request → take the photo → happy)
    /// and records phone-sized frames (camera + UI) to Captures/tour/NNNN.jpg plus phases.txt for captions.
    /// Start it in Play mode: CafeTour.Run().
    /// </summary>
    public class CafeTour : MonoBehaviour
    {
        public static bool Busy { get; private set; }
        string dir; int w, h, frame; float interval; StringBuilder log = new();
        RenderTexture camRT, uiRT; PanelSettings ps; RenderTexture prevTT; bool prevClear;

        public static void Run(int w = 432, int h = 936, float interval = 0.2f)
        {
            if (Busy) return;
            var t = new GameObject("CafeTour").AddComponent<CafeTour>(); t.w = w; t.h = h; t.interval = interval; t.StartCoroutine(t.Go());
        }

        IEnumerator Go()
        {
            Busy = true;
            dir = "Captures/tour"; if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) File.Delete(f); Directory.CreateDirectory(dir);
            var M = CafeManager.I; var cam = CafeCamera.I; var ui = CafeUI.I;
            camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = FindAnyObjectByType<UIDocument>().panelSettings; prevTT = ps.targetTexture; prevClear = ps.clearColor;
            cam.Cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            ui.OpenTab("Cafe"); ui.CloseModal();

            // 1. a customer walks in from the street
            var seat = M.FreeSeat();
            for (float wait = 0; !seat && wait < 90f; wait += 0.5f) { yield return new WaitForSeconds(0.5f); seat = M.FreeSeat(); }   // café full: wait for someone to leave
            if (!seat) { Debug.LogError("[CafeTour] no free seat"); Finish(); yield break; }
            var before = new HashSet<Customer>(M.Customers);
            typeof(CafeManager).GetMethod("SpawnCustomer", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(M, new object[] { seat });
            var c = M.Customers.FirstOrDefault(x => !before.Contains(x));
            if (!c) { Debug.LogError("[CafeTour] spawn failed"); Finish(); yield break; }
            cam.Zoom(1.3f);
            float t = 0;
            while (!c.Seated && t < 30f) { cam.LookAtPoint(c.transform.position); yield return Shot("A customer arrives"); t += interval; }
            cam.Zoom(1f / 1.3f);

            // 2. orders food (bubble) and waits while the counter prepares it
            t = 0;
            while (c.Bubble != Bubble.Order && t < 4f) { cam.LookAtPoint(c.transform.position); yield return Shot("Takes a seat"); t += interval; }
            t = 0;
            while (c.Bubble == Bubble.Order && t < 30f) { cam.LookAtPoint(c.transform.position); yield return Shot($"Orders: {c.Order?.Label}"); t += interval; }
            for (t = 0; t < 1.6f; t += interval) yield return Shot("Food served!");

            // 3. asks for a photo of a café cat (force one for the demo if the dice said no)
            var cat = M.Cats.FirstOrDefault(x => x);
            if (c.Request == null && cat)
            {
                var req = new PhotoRequest { Cat = cat.Def.Id, Pose = null, TimeLeft = 75f };
                typeof(Customer).GetProperty("Request").SetValue(c, req);
                typeof(Customer).GetProperty("Bubble").SetValue(c, Bubble.Photo);
            }
            string want = c.Request != null ? c.Request.Label : "a cat";
            for (t = 0; t < 2.8f; t += interval) { cam.LookAtPoint(c.transform.position); yield return Shot($"Asks for a photo of {want}"); }

            // 4. the player finds the cat and opens the camera
            var target = c.Request?.Cat != null ? M.Cats.FirstOrDefault(x => x && x.Def.Id == c.Request.Cat) : cat;
            if (target)
            {
                for (t = 0; t < 1.2f; t += interval) { cam.LookAtPoint(target.transform.position); yield return Shot($"Find {target.Def.Id}"); }
                CafePhoto.I.Enter(target);
                for (t = 0; t < 2.4f; t += interval) yield return Shot("Line up the shot");
                CafePhoto.I.Shutter();
                for (t = 0; t < 2.6f; t += interval) yield return Shot("Snap!");
                ui.CloseModal(); CafePhoto.I.Exit();
            }

            // 5. the customer is delighted (hearts) and will tip extra
            for (t = 0; t < 2.6f; t += interval) { cam.LookAtPoint(c.transform.position); yield return Shot("Request done: happy customer, bigger tip"); }
            Finish();
        }

        IEnumerator Shot(string phase)
        {
            yield return new WaitForSeconds(interval);
            yield return new WaitForEndOfFrame();
            var a = Read(camRT); var b = Read(uiRT);
            for (int i = 0; i < a.Length; i++) { float al = b[i].a / 255f; a[i] = Color32.Lerp(a[i], new Color32(b[i].r, b[i].g, b[i].b, 255), al); }
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false); o.SetPixels32(a); o.Apply();
            File.WriteAllBytes($"{dir}/{frame:0000}.jpg", o.EncodeToJPG(88)); Destroy(o);
            log.AppendLine($"{frame:0000}|{phase}"); frame++;
        }

        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(); RenderTexture.active = prev; var px = t.GetPixels32(); Destroy(t); return px;
        }

        void Finish()
        {
            File.WriteAllText($"{dir}/phases.txt", log.ToString());
            CafeCamera.I.Cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear;
            if (camRT) camRT.Release(); if (uiRT) uiRT.Release();
            Debug.Log($"[CafeTour] done, {frame} frames"); Busy = false; Destroy(gameObject);
        }
    }
}
#endif
