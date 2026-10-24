#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using PawCollector.Cafe;

namespace PawCollector
{
    /// <summary>Editor QA for the starter guide, played as a brand-new player.
    /// Outside Play: CafeAuditGuide.Wipe() (backs up the real save, then clears it). Enter Play, then CafeAuditGuide.Run(w, h).
    /// After Stop: CafeAuditGuide.Restore().</summary>
    public class CafeAuditPet : MonoBehaviour
    {
        const string Backup = "Captures/save_backup_pet.json", SaveKey = "pawcafe.save.v3";
        public static void Wipe() { Directory.CreateDirectory("Captures"); if (!File.Exists(Backup)) File.WriteAllText(Backup, PlayerPrefs.GetString(SaveKey, "")); PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); Debug.Log("[CafeAuditPet] save wiped (backup kept)"); }
        public static void Restore() { if (!File.Exists(Backup)) return; PlayerPrefs.SetString(SaveKey, File.ReadAllText(Backup)); PlayerPrefs.Save(); File.Delete(Backup); Debug.Log("[CafeAuditPet] save restored"); }
        public static void Run(int w = 1389, int h = 642) { var a = new GameObject("CafeAuditPet").AddComponent<CafeAuditPet>(); a.w = w; a.h = h; }

        int w, h; RenderTexture camRT, uiRT; PanelSettings ps;
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        IEnumerator Start()
        {
            if (!File.Exists(Backup)) File.WriteAllText(Backup, PlayerPrefs.GetString(SaveKey, ""));   // Restore() after Stop
            const string dir = "Captures/pet"; Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            var ui = CafeUI.I; var M = CafeManager.I; var cam = CafeCamera.I; var doc = FindAnyObjectByType<UIDocument>();
            camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = doc.panelSettings; var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            cam.Cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            typeof(CafeUI).GetMethod("HideTitle", NP).Invoke(ui, null);
            int n = 0;
            IEnumerator Shot(string name, float wait) { yield return new WaitForSeconds(wait); yield return new WaitForEndOfFrame(); Save($"{dir}/{n++:00}_{name}.png"); }
            var cat = M.Cats.First(); var rec = M.Data.CatOrNew(cat.Def.Id); rec.bond = 37;   // 1 heart + most of the next
            CafePhoto.I.Enter(cat); yield return Shot("photo_start", 1.6f);
            for (int i = 0; i < 2; i++) { CafeInput.LastPetScreen = cam.Cam.WorldToScreenPoint(cat.transform.position + Vector3.up * 0.4f); cat.Pet(); yield return new WaitForSeconds(0.32f); }
            yield return Shot("petting", 0.05f);
            CafeInput.LastPetScreen = cam.Cam.WorldToScreenPoint(cat.transform.position + Vector3.up * 0.4f); cat.Pet();
            yield return Shot("heart_up", 0.25f);
            yield return Shot("after", 1.2f);
            CafePhoto.I.Exit();
            cam.Cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear; camRT.Release(); uiRT.Release();
            Debug.Log($"[CafeAuditPet] {n} shots, bond {rec.bond}"); Destroy(gameObject);
        }

        void Save(string path)
        {
            var a = Read(camRT); var b = Read(uiRT);
            for (int i = 0; i < a.Length; i++) { float al = b[i].a / 255f; a[i] = Color32.Lerp(a[i], new Color32(b[i].r, b[i].g, b[i].b, 255), al); }
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false); o.SetPixels32(a); o.Apply(); File.WriteAllBytes(path, o.EncodeToPNG()); Destroy(o);
        }
        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(); RenderTexture.active = prev; var px = t.GetPixels32(); Destroy(t); return px;
        }
    }
}
#endif
