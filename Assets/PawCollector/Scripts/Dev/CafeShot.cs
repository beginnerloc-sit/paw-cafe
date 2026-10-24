#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using PawCollector.Cafe;

namespace PawCollector
{
    /// <summary>Editor QA: renders the café camera + UI into a phone-sized PNG (Captures/cafe/NAME.png).</summary>
    public class CafeShot : MonoBehaviour
    {
        public static void Take(string name, int w = 1080, int h = 2340)
        {
            var go = new GameObject("CafeShot"); go.AddComponent<CafeShot>().StartCoroutine(go.GetComponent<CafeShot>().Run(name, w, h));
        }
        IEnumerator Run(string name, int w, int h)
        {
            Directory.CreateDirectory("Captures/cafe");
            var cam = CafeCamera.I.Cam; var doc = FindAnyObjectByType<UIDocument>(); var ps = doc.panelSettings;
            var camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            for (int i = 0; i < 3; i++) yield return new WaitForEndOfFrame();
            var a = Read(camRT); var b = Read(uiRT);
            for (int i = 0; i < a.Length; i++) { float al = b[i].a / 255f; a[i] = Color32.Lerp(a[i], new Color32(b[i].r, b[i].g, b[i].b, 255), al); }
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false); o.SetPixels32(a); o.Apply();
            File.WriteAllBytes($"Captures/cafe/{name}.png", o.EncodeToPNG());
            cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear; camRT.Release(); uiRT.Release();
            Debug.Log("[CafeShot] " + name); Destroy(gameObject);
        }
        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(); RenderTexture.active = prev; var px = t.GetPixels32(); Destroy(t); return px;
        }
    }
}
#endif
