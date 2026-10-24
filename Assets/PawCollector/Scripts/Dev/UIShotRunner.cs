#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector
{
    /// <summary>
    /// Editor-only QA helper: renders every UI state at several device shapes (camera + UI composited)
    /// into Captures/ui_qa/. Start with UIShotRunner.Run() while in play mode.
    /// </summary>
    public class UIShotRunner : MonoBehaviour
    {
        public static void Run(string only = null) { var go = new GameObject("UIShotRunner"); go.AddComponent<UIShotRunner>().only = only; }

        string only;
        static readonly (string name, int w, int h)[] Devices =
        {
            ("phone_19x9", 540, 1170),   // iPhone 13-15 / most Androids
            ("phone_16x9", 540, 960),    // iPhone SE / older Androids
            ("tablet_4x3", 768, 1024),   // iPad
            ("landscape", 845, 512),     // editor Game view
        };
        static readonly string[] States = { "hud", "title", "shop", "placing", "toast", "viewfinder", "polaroid", "album", "cat", "recap", "level", "settings" };

        IEnumerator Start()
        {
            var doc = FindAnyObjectByType<UIDocument>();
            var cam = CameraRig.I.Cam;
            var ps = doc.panelSettings;
            var gm = GameManager.I;
            Directory.CreateDirectory("Captures/ui_qa");

            // a lived-in save: a few cats met, items out, cats in the garden
            gm.ResetProgress();
            gm.Data.fish = 240; gm.Data.level = 4; gm.Data.tutorial = 6; gm.Data.gold = 3;
            UIController.I.StartGame();
            string[] items = { "Bowl", "Yarn", "Box", "Cushion", "Wand" };
            for (int i = 0; i < items.Length && i < gm.Spots.Count; i++) gm.TryPlace(gm.Spots[i], items[i], out _);
            VisitorSpawner.I.SpawnSoon(999);
            foreach (var id in new[] { "Mochi", "Oreo", "Pebble" }) VisitorSpawner.I.TrySpawn(id);
            float until = Time.realtimeSinceStartup + 9f;
            while (Time.realtimeSinceStartup < until) yield return null;
            foreach (var c in new List<CatVisitor>(CatVisitor.All))
            {
                if (!c.InGarden) continue;
                PhotoMode.I.Enter(c); yield return Wait(1.2f);
                PhotoMode.I.Shutter(); yield return Wait(0.3f);
                PhotoMode.I.Exit(); yield return Wait(2.5f);
            }
            gm.AddBond(GameData.Cat("Mochi"), 45);

            var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            foreach (var dev in Devices)
            {
                if (only != null && dev.name != only) continue;
                int inGarden = 0; foreach (var c in CatVisitor.All) if (c.InGarden && c.ShotsLeft > 0) inGarden++;
                if (inGarden < 2) { foreach (var id in new[] { "Patches", "Suki", "Midnight", "Mochi", "Oreo" }) VisitorSpawner.I.TrySpawn(id); yield return Wait(8f); }
                var camRT = new RenderTexture(dev.w, dev.h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var uiRT = new RenderTexture(dev.w, dev.h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = camRT; CameraRig.I.Frame();
                ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
                yield return Wait(0.3f);
                foreach (var st in States)
                {
                    PhotoMode.I.Exit();
                    UIController.I.DevState(st == "viewfinder" || st == "polaroid" ? "hud" : st);
                    if (st == "viewfinder" || st == "polaroid")
                    {
                        CatVisitor target = null;
                        foreach (var c in CatVisitor.All) if (c.CanPhoto) { target = c; break; }
                        if (target)
                        {
                            PhotoMode.I.Enter(target);
                            yield return Wait(1.4f);
                            if (st == "polaroid") { PhotoMode.I.Shutter(); yield return Wait(0.7f); }
                        }
                    }
                    else yield return Wait(st == "toast" ? 0.5f : 0.6f);
                    Save(camRT, uiRT, $"Captures/ui_qa/{dev.name}_{st}.png");
                }
                PhotoMode.I.Exit();
                UIController.I.DevState("hud");
                cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear;
                camRT.Release(); uiRT.Release();
                yield return Wait(0.2f);
            }
            CameraRig.I.Frame();
            Debug.Log("[UIShots] done");
            Destroy(gameObject);
        }

        static IEnumerator Wait(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }

        static void Save(RenderTexture camRT, RenderTexture uiRT, string path)
        {
            var a = Read(camRT); var b = Read(uiRT);
            for (int i = 0; i < a.Length; i++)
            {
                float al = b[i].a / 255f;
                a[i] = Color32.Lerp(a[i], new Color32(b[i].r, b[i].g, b[i].b, 255), al);
            }
            var o = new Texture2D(camRT.width, camRT.height, TextureFormat.RGBA32, false);
            o.SetPixels32(a); o.Apply();
            File.WriteAllBytes(path, o.EncodeToPNG());
            Destroy(o);
        }

        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = prev;
            var px = t.GetPixels32(); Destroy(t); return px;
        }
    }
}
#endif
