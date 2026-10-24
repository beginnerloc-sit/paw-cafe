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
    /// <summary>Editor QA for the shelter gacha, cat naming and cat care. Backs up the save to Captures/save_backup.json first
    /// (restore with CafeAuditCare.Restore() after leaving Play mode). Start in Play mode: CafeAuditCare.Run().</summary>
    public class CafeAuditCare : MonoBehaviour
    {
        public const string Backup = "Captures/save_backup.json", SaveKey = "pawcafe.save.v3";
        public static void Run(int w = 540, int h = 1170) { var a = new GameObject("CafeAuditCare").AddComponent<CafeAuditCare>(); a.w = w; a.h = h; }
        public static void Restore() { if (File.Exists(Backup)) { PlayerPrefs.SetString(SaveKey, File.ReadAllText(Backup)); PlayerPrefs.Save(); Debug.Log("[CafeAuditCare] save restored"); } }
        int w, h; RenderTexture camRT, uiRT; PanelSettings ps;
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        IEnumerator Start()
        {
            Directory.CreateDirectory("Captures"); File.WriteAllText(Backup, PlayerPrefs.GetString(SaveKey, ""));
            const string dir = "Captures/care"; Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            var ui = CafeUI.I; var M = CafeManager.I; var cam = CafeCamera.I; var doc = FindAnyObjectByType<UIDocument>();
            camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = doc.panelSettings; var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            cam.Cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            void Call(string m, params object[] a) => typeof(CafeUI).GetMethod(m, NP).Invoke(ui, a);
            int n = 0;
            IEnumerator Shot(string name, float wait = 0.5f) { yield return new WaitForSeconds(wait); if (ShelterStage.I) ShelterStage.I.Cam.targetTexture = camRT; yield return new WaitForEndOfFrame(); Save($"{dir}/{n++:00}_{name}.png"); }

            // ---- setup: plenty of coins/tickets, a free bed, a sick cat and a hungry cat
            var d = M.Data; d.coins = 6000; d.tickets = 3; d.food = 4; d.medicine = 1; d.shards = 420; d.pity = 9;
            while (M.FreeBeds < 2) CafeDecorate.I.Buy("CatBed");
            CafeDecorate.I.Select(null); ui.OpenTab("Cafe");
            var ids = d.residents.ToList();
            if (ids.Count > 0) { var r = d.Cat(ids[0]); r.sick = true; r.health = 22; r.full = 40; r.happy = 30; }
            if (ids.Count > 1) { var r = d.Cat(ids[1]); r.full = 12; }
            M.Recalc();
            yield return Shot("hud_care_bubbles", 1.5f);
            ui.OpenTab("Cats"); yield return Shot("cats_tab");
            if (ids.Count > 0) { ui.OpenCat(ids[0]); yield return Shot("profile_sick"); }
            if (ids.Count > 1) { ui.OpenCat(ids[1]); yield return Shot("profile_hungry"); }
            if (ids.Count > 0) { ui.Rename(ids[0], null); var tf = doc.rootVisualElement.Q<TextField>(className: "namefield"); if (tf != null) tf.value = "Sir Fluff"; yield return Shot("rename"); }
            ui.CloseModal();
            ui.OpenTab("Shelter");
            // ---- adoption minigame: pity is at 9 so this one is guaranteed Rare+
            yield return Shot("yard", 2.0f);
            Call("ShelterDraw"); yield return Shot("lure_start", 0.6f);
            ui.SimWave(0.45f); yield return Shot("lure_half", 0.3f);
            ui.SimWave(0.7f); yield return Shot("choose_start", 0.8f);
            yield return Shot("choose_mid", 1.6f);
            yield return Shot("choose_late", 1.4f);
            yield return Shot("chosen_card", 3.2f);
            Call("EndCutscene"); yield return Shot("yard_after", 1.2f);
            Call("OpenShardShop"); yield return Shot("shard_shop");
            ui.CloseModal(); ui.OpenTab("Cats"); yield return Shot("cats_after");
            ui.OpenTab("Decorate"); yield return Shot("shop_seating");
            ui.OpenShop("Cat furniture"); yield return Shot("shop_cat");
            ui.OpenShop("Upgrades"); yield return Shot("shop_upgrades");
            ui.OpenShop("Rooms"); yield return Shot("shop_rooms");
            ui.CloseModal(); yield return Shot("decor_bar");
            ui.OpenTab("Cafe");
            cam.Cam.targetTexture = null; if (ShelterStage.I) ShelterStage.I.Cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear; camRT.Release(); uiRT.Release();
            Debug.Log($"[CafeAuditCare] {n} shots"); Destroy(gameObject);
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
