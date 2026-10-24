#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using PawCollector.Cafe;

namespace PawCollector
{
    /// <summary>Editor QA: walks through every café screen and saves phone-sized shots (camera + UI) to Captures/audit/.
    /// Start in Play mode: CafeAudit.Run().</summary>
    public class CafeAudit : MonoBehaviour
    {
        public static void Run(int w = 540, int h = 1170) { var a = new GameObject("CafeAudit").AddComponent<CafeAudit>(); a.w = w; a.h = h; }
        int w, h; RenderTexture camRT, uiRT; PanelSettings ps;
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        IEnumerator Start()
        {
            const string dir = "Captures/audit"; Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            var ui = CafeUI.I; var M = CafeManager.I; var cam = CafeCamera.I;
            camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = FindAnyObjectByType<UIDocument>().panelSettings; var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            cam.Cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            void Call(string m, params object[] a) => typeof(CafeUI).GetMethod(m, NP).Invoke(ui, a);
            int n = 0;
            IEnumerator Shot(string name) { yield return new WaitForSeconds(0.5f); yield return new WaitForEndOfFrame(); Save($"{dir}/{n++:00}_{name}.png"); }

            ui.CloseModal(); ui.OpenTab("Cafe"); yield return Shot("hud");
            foreach (var t in new[] { "Decorate", "Cats", "Photos", "Shelter" }) { ui.OpenTab(t); yield return Shot("tab_" + t); }
            ui.OpenTab("Cafe");
            var furn = M.Furniture.FirstOrDefault(f => f && f.Def.Seats == 0) ?? M.Furniture.FirstOrDefault();
            ui.OpenTab("Decorate"); if (furn) { CafeDecorate.I.Select(furn); yield return Shot("decorate_selected"); CafeDecorate.I.Select(null); }
            ui.OpenTab("Cafe");
            Call("OpenSettings"); yield return Shot("settings"); ui.CloseModal();
            ui.OpenRoomUnlock("nook"); yield return Shot("room_locked"); ui.CloseModal();
            var stray = FindAnyObjectByType<Stray>(); if (stray) { ui.OpenStray(stray); yield return Shot("stray"); ui.CloseModal(); }
            Call("OnLevelUp", 3); yield return Shot("levelup"); ui.CloseModal();
            Call("OnWelcome", 1234L, 2.5f); yield return Shot("welcome"); ui.CloseModal();
            var cat = M.Cats.FirstOrDefault(c => c);
            if (cat)
            {
                CafePhoto.I.Enter(cat); yield return new WaitForSeconds(1.2f); yield return Shot("viewfinder");
                var tex = CafePhoto.Capture();
                Call("OnSnapped", tex, cat.Def.Id, "Sit", 2, true); yield return Shot("snapped"); ui.CloseModal(); CafePhoto.I.Exit();
            }
            ui.Toast("Pinned to the café wall"); yield return Shot("toast");
            // cat profile
            var rid = M.Data.residents.FirstOrDefault(); if (rid != null) { ui.OpenTab("Cats"); yield return Shot("cats_grid"); ui.OpenCat(rid); yield return Shot("cat_profile"); ui.CloseModal(); ui.OpenTab("Cafe"); }
            // decorate: drag a floor piece (lifted + shadow), then a photo frame onto a side wall (moves are undone afterwards)
            ui.OpenTab("Decorate");
            var piece = M.Furniture.FirstOrDefault(f => f && !f.Def.WallMounted && f.Def.Seats == 0 && f.Def.Id != "Rug" && !f.Occupant);
            if (piece)
            {
                var p0 = piece.transform.position; var r0 = piece.transform.rotation;
                CafeDecorate.I.Select(piece); CafeDecorate.I.DragBegin(piece);
                CafeDecorate.I.Drag(piece, cam.Area.center + new Vector3(0.5f, 0, -1.2f)); yield return Shot("drag_floor");
                CafeDecorate.I.Drag(piece, new Vector3(-0.42f, 0, -3.9f)); yield return Shot("drag_floor_blocked");
                CafeDecorate.I.DragEnd(piece); piece.transform.SetPositionAndRotation(p0, r0); piece.SavePlacement(); CafeDecorate.I.Select(null);
            }
            var frame = M.Furniture.FirstOrDefault(f => f && f.Def.WallMounted && f.Def.Id == "PhotoFrame");
            if (frame)
            {
                var p0 = frame.transform.position; var r0 = frame.transform.rotation;
                CafeDecorate.I.Select(frame); CafeDecorate.I.DragBegin(frame);
                CafeDecorate.I.Drag(frame, new Vector3(3.6f, 0, -1.6f)); cam.LookAtPoint(new Vector3(2.4f, 0, -1.6f)); yield return Shot("drag_wall_side");
                CafeDecorate.I.DragEnd(frame); frame.transform.SetPositionAndRotation(p0, r0); frame.SavePlacement(); CafeDecorate.I.Select(null);
            }
            ui.OpenTab("Cafe"); M.Save();
            cam.Cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear; camRT.Release(); uiRT.Release();
            Debug.Log($"[CafeAudit] {n} shots"); Destroy(gameObject);
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
