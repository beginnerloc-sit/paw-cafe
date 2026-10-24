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
    public class CafeAuditGuide : MonoBehaviour
    {
        const string Backup = "Captures/save_backup_guide.json", SaveKey = "pawcafe.save.v3";
        public static void Wipe() { Directory.CreateDirectory("Captures"); if (!File.Exists(Backup)) File.WriteAllText(Backup, PlayerPrefs.GetString(SaveKey, "")); PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); Debug.Log("[CafeAuditGuide] save wiped (backup kept)"); }
        public static void Restore() { if (!File.Exists(Backup)) return; PlayerPrefs.SetString(SaveKey, File.ReadAllText(Backup)); PlayerPrefs.Save(); File.Delete(Backup); Debug.Log("[CafeAuditGuide] save restored"); }
        public static void Run(int w = 1389, int h = 642) { var a = new GameObject("CafeAuditGuide").AddComponent<CafeAuditGuide>(); a.w = w; a.h = h; }

        int w, h; RenderTexture camRT, uiRT; PanelSettings ps;
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        IEnumerator Start()
        {
            const string dir = "Captures/guide"; Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            var ui = CafeUI.I; var M = CafeManager.I; var cam = CafeCamera.I; var doc = FindAnyObjectByType<UIDocument>();
            camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = doc.panelSettings; var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            cam.Cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            void Call(string m, params object[] a) => typeof(CafeUI).GetMethod(m, NP).Invoke(ui, a);
            int n = 0;
            IEnumerator Shot(string name, float wait = 0.7f) { yield return new WaitForSeconds(wait); if (ShelterStage.I) ShelterStage.I.Cam.targetTexture = camRT; yield return new WaitForEndOfFrame(); Save($"{dir}/{n++:00}_{name}.png"); Debug.Log($"[CafeAuditGuide] {name}: step {M.Data.tutorial} coins {M.Data.coins} beds {M.Beds} free {M.FreeBeds} jar {M.Data.tipJar}"); }

            Call("HideTitle"); M.Data.tipJar = 0;
            yield return Shot("s1_tips", 1.2f);
            M.CollectTips(); yield return Shot("s2_photo", 1.0f);
            var cat = M.Cats.FirstOrDefault(); CafePhoto.I.Enter(cat); yield return Shot("s2_photo_mode", 1.5f);
            CafePhoto.I.Shutter(); yield return new WaitForSeconds(0.6f); CafePhoto.I.Exit();
            yield return Shot("s3_bed_cafe", 1.0f);
            ui.OpenTab("Cats"); yield return Shot("s3_wrong_tab");
            ui.OpenTab("Decorate"); yield return Shot("s3_shop_seating", 0.9f);
            ui.OpenShop("Decor"); yield return Shot("s3_shop_wrongcat");
            ui.CloseModal(); yield return Shot("s3_decor_bar");
            ui.OpenShop("Seating"); yield return Shot("s3_shop_again"); ui.CloseModal();
            CafeDecorate.I.BeginPlacing("Chair"); yield return Shot("s3_placing", 0.9f);
            for (int i = 0; i < 60 && !CafeDecorate.I.GhostOk; i++) CafeDecorate.I.MoveGhost(new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 2f)));
            yield return Shot("s3_placing_ok", 0.5f);
            if (!CafeDecorate.I.ConfirmPlacing()) Debug.Log("[CafeAuditGuide] place failed");
            ui.CloseModal();
            ui.OpenTab("Cafe"); yield return Shot("s4_adopt_cafe", 1.0f);
            ui.OpenTab("Shelter"); yield return Shot("s4_shelter", 2.0f);
            Call("ShelterDraw"); yield return Shot("s4_minigame", 1.0f);
            Call("EndCutscene"); ui.CloseModal(); ui.OpenTab("Cafe"); yield return Shot("s5_done", 1.0f);
            cam.Cam.targetTexture = null; if (ShelterStage.I) ShelterStage.I.Cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear; camRT.Release(); uiRT.Release();
            Debug.Log($"[CafeAuditGuide] {n} shots"); Destroy(gameObject);
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
