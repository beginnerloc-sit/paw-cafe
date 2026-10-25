#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using PawCollector.Cafe;
using PawCollector.Cafe.Games;

namespace PawCollector
{
    /// <summary>Editor QA for the 3D minigames: plays Red Light Green Light (a win and a caught run) and Cat Stack
    /// (perfect drops, misses) with simulated input and shoots the intro, countdown, play, juice moments and results.
    /// Play mode: CafeAudit3D.Run(w, h). Restore() after Stop.</summary>
    public class CafeAudit3D : MonoBehaviour
    {
        const string Backup = "Captures/save_backup_3d.json", SaveKey = "pawcafe.save.v3";
        public static void Restore() { if (!File.Exists(Backup)) return; PlayerPrefs.SetString(SaveKey, File.ReadAllText(Backup)); PlayerPrefs.Save(); File.Delete(Backup); Debug.Log("[CafeAudit3D] save restored"); }
        public static void Run(int w = 1389, int h = 642, string only = null) { var a = new GameObject("CafeAudit3D").AddComponent<CafeAudit3D>(); a.w = w; a.h = h; a.only = only; }
        int w, h, n; string only, dir = "Captures/games3d"; RenderTexture uiRT; PanelSettings ps; CafeUI ui;
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        T Get<T>(object o, string f) => (T)o.GetType().GetField(f, NP | BindingFlags.Public).GetValue(o);
        MiniGame Game => (MiniGame)typeof(CafeUI).GetField("game", NP).GetValue(ui);
        void Call(string m, params object[] a) => typeof(CafeUI).GetMethod(m, NP).Invoke(ui, a);
        IEnumerator Shot(string name, float wait = 0f) { if (wait > 0) yield return new WaitForSeconds(wait); yield return new WaitForEndOfFrame(); Save($"{dir}/{n++:00}_{name}.png"); }

        IEnumerator Start()
        {
            if (!File.Exists(Backup)) File.WriteAllText(Backup, PlayerPrefs.GetString(SaveKey, ""));
            Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            ui = CafeUI.I; var M = CafeManager.I; var doc = FindAnyObjectByType<UIDocument>();
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = doc.panelSettings; var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            Call("HideTitle"); M.Data.level = Mathf.Max(M.Data.level, 6); M.Data.tutorial = 99;
            string hero = M.Data.residents.FirstOrDefault() ?? "Mochi";

            if (only == null || only == "redlight")
            {
                // run 1: play it properly — sneak on green, freeze on red, cross the line
                ui.PlayGame("redlight", hero);
                yield return Shot("rl_intro", 0.5f); yield return Shot("rl_count", 1.4f); yield return Shot("rl_go", 1.25f);
                var g = Game; float t0 = Time.time; bool shotRun = false, shotRed = false, shotRival = false;
                while (g != null && !g.Over && Time.time - t0 < 40f)
                {
                    bool careful = g.Danger || (g.Banner != null && g.Banner.Contains("…"));
                    if (careful) g.Up(Vector2.zero); else g.Down(Vector2.zero);
                    if (!shotRun && Time.time - t0 > 1.2f && !careful) { shotRun = true; yield return Shot("rl_run"); }
                    if (!shotRed && g.Danger) { shotRed = true; yield return Shot("rl_red", 0.35f); }
                    if (!shotRival && shotRed && Time.time - t0 > 8f && !careful) { shotRival = true; yield return Shot("rl_mid"); }
                    yield return null;
                }
                yield return Shot("rl_finish", 0.3f); yield return Shot("rl_orbit", 0.8f); yield return Shot("rl_results", 1.5f); yield return Shot("rl_results_full", 2.2f);
                Call("EndGame", false); yield return new WaitForSeconds(0.4f);
                // run 2: keep holding through red — get caught
                ui.PlayGame("redlight", hero); yield return new WaitForSeconds(3.6f);
                g = Game; t0 = Time.time;
                while (g != null && !g.Over && Time.time - t0 < 20f) { g.Down(Vector2.zero); yield return null; }
                yield return Shot("rl_caught", 0.05f); yield return Shot("rl_caught2", 0.5f); yield return Shot("rl_caught_results", 2.4f);
                Call("EndGame", false); yield return new WaitForSeconds(0.4f);
            }
            if (only == null || only == "stack")
            {
                ui.PlayGame("stack", hero);
                yield return Shot("st_intro", 0.6f); yield return Shot("st_go", 2.9f);
                var g = Game; var fSwingX = g.GetType().GetField("swingX", NP); var fFalling = g.GetType().GetField("falling", NP); var fSwing = g.GetType().GetField("swing", NP);
                float t0 = Time.time; int drops = 0; bool popShot = false;
                while (g != null && !g.Over && Time.time - t0 < 60f)
                {
                    bool ready = fSwing.GetValue(g) != null && fFalling.GetValue(g) == null; float sx = (float)fSwingX.GetValue(g);
                    bool miss = drops >= 7;
                    if (ready && ((!miss && Mathf.Abs(sx) < 0.07f) || (miss && Mathf.Abs(sx) > 1.9f)))
                    {
                        g.Down(Vector2.zero); drops++;
                        if (!popShot) { popShot = true; yield return Shot("st_pop", 0.05f); }
                        if (drops == 1) yield return Shot("st_land1", 0.45f);
                        if (drops == 4) yield return Shot("st_four", 0.5f);
                        if (drops == 7) yield return Shot("st_seven", 0.5f);
                        if (drops == 8) { yield return Shot("st_miss", 0.25f); yield return Shot("st_miss2", 0.5f); }
                    }
                    yield return null;
                }
                yield return Shot("st_over", 0.3f); yield return Shot("st_results", 1.6f); yield return Shot("st_results_full", 2.4f);
                Call("EndGame", false); yield return new WaitForSeconds(0.4f);
            }
            if (only == null || only == "rest")
                foreach (var id in new[] { "flight", "pounce", "zoomies", "laser", "latte", "rooftop", "yarn", "hide" })
                {
                    ui.PlayGame(id, hero); yield return Shot(id + "_intro", 0.6f); yield return new WaitForSeconds(3.1f);
                    var g = Game; float t0 = Time.time; bool s1 = false, s2 = false; int phase = 0; float nextAct = 0;
                    var toStage = typeof(Game3D).GetMethod("ToStage", NP);
                    Vector2 St(Vector3 l) => (Vector2)toStage.Invoke(g, new object[] { l });
                    var stage = (VisualElement)typeof(CafeUI).GetField("arcStage", NP).GetValue(ui); var sz = stage.layout.size;
                    while (g != null && !g.Over && Time.time - t0 < 9f)
                    {
                        float t = Time.time - t0;
                        if (Time.time >= nextAct)
                            switch (id)
                            {
                                case "flight": g.Down(sz / 2); nextAct = Time.time + 0.42f; break;
                                case "pounce":
                                    foreach (var h in (System.Collections.IList)g.GetType().GetField("holes", NP).GetValue(g))
                                    {
                                        var ht = h.GetType(); if (!(bool)ht.GetField("active").GetValue(h) || (bool)ht.GetField("hit").GetValue(h) || (int)ht.GetField("kind").GetValue(h) == 1) continue;
                                        if ((float)ht.GetField("up").GetValue(h) < 0.2f) continue;
                                        g.Down(St((Vector3)ht.GetField("c").GetValue(h) + Vector3.up * 0.35f)); break;
                                    }
                                    nextAct = Time.time + 0.3f; break;
                                case "zoomies": g.Move(new Vector2(sz.x / 2 + Mathf.Sin(t * 1.7f) * sz.x * 0.3f, sz.y * 0.8f)); break;
                                case "laser": g.Move(new Vector2(sz.x / 2 + Mathf.Cos(t * 1.2f) * sz.x * 0.3f, sz.y * 0.55f + Mathf.Sin(t * 1.2f) * sz.y * 0.25f)); break;
                                case "latte":
                                    {
                                        var guide = (System.Collections.Generic.List<Vector3>)g.GetType().GetField("guide", NP).GetValue(g);
                                        var cup = (Transform)g.GetType().GetField("cupT", NP).GetValue(g);
                                        if ((float)g.GetType().GetField("slideT", NP).GetValue(g) >= 0 || (bool)g.GetType().GetField("scored", NP).GetValue(g)) break;
                                        g.Down(St(cup.localPosition + guide[0]));
                                        for (int i = 0; i < guide.Count; i += 2) g.Move(St(cup.localPosition + guide[i] + new Vector3(Mathf.Sin(i) * 0.04f, 0, 0)));
                                        if (!s1) { s1 = true; yield return Shot(id + "_drawn", 0.05f); }
                                        g.Up(Vector2.zero); nextAct = Time.time + 0.5f; break;
                                    }
                                case "rooftop":
                                    if (phase++ % 4 == 3) { g.Down(sz / 2); g.Move(sz / 2 + new Vector2(0, 120)); } else g.Down(sz / 2);
                                    nextAct = Time.time + 0.8f; break;
                                case "yarn": g.Down(new Vector2(sz.x * Random.Range(0.35f, 0.65f), sz.y * 0.25f)); g.Move(new Vector2(sz.x * Random.Range(0.3f, 0.7f), sz.y * 0.25f)); if (!s1 && t > 2f) { s1 = true; yield return Shot(id + "_aim", 0.05f); } g.Up(Vector2.zero); nextAct = Time.time + 1.1f; break;
                                case "hide":
                                    if (t > 1.2f && phase == 0) { g.Down(new Vector2(sz.x * 0.2f, sz.y * 0.6f)); phase = 1; yield return Shot(id + "_wrong", 0.15f); }
                                    else if (t > 2.4f && phase == 1) { var head = (Vector3)g.GetType().GetProperty("Head", NP).GetValue(g); yield return Shot(id + "_hidden", 0.01f); g.Down(St(head)); phase = 2; yield return Shot(id + "_found", 0.5f); }
                                    else if (phase == 2 && t > 4.6f) phase = 0;
                                    break;
                            }
                        if (!s2 && t > 2.5f) { s2 = true; yield return Shot(id + "_play", 0.01f); }
                        yield return null;
                    }
                    yield return Shot(id + "_late", 0.05f);
                    if (g != null && !g.Over) typeof(MiniGame).GetMethod("Finish", NP).Invoke(g, new object[] { true, "Audit stop" });
                    yield return Shot(id + "_results", 3.0f);
                    Call("EndGame", false); yield return new WaitForSeconds(0.4f);
                }
            ps.targetTexture = prevTT; ps.clearColor = prevClear; uiRT.Release();
            Debug.Log($"[CafeAudit3D] {n} shots"); Destroy(gameObject);
        }

        void Save(string path)
        {
            // composite: every enabled camera (by depth) then the UI on top
            var camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            foreach (var c in Camera.allCameras.OrderBy(c => c.depth)) { var prev = c.targetTexture; c.targetTexture = camRT; c.Render(); c.targetTexture = prev; }
            var a = Read(camRT); var b = Read(uiRT); camRT.Release();
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
