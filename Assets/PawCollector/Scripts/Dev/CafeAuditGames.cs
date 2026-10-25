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
    /// <summary>Editor QA for the Game Corner: shoots the picker, the hero chooser, every game mid-play (with simulated
    /// input), a results card and the request card stack. Play mode: CafeAuditGames.Run(w, h). Restore() after Stop.</summary>
    public class CafeAuditGames : MonoBehaviour
    {
        const string Backup = "Captures/save_backup_games.json", SaveKey = "pawcafe.save.v3";
        public static void Restore() { if (!File.Exists(Backup)) return; PlayerPrefs.SetString(SaveKey, File.ReadAllText(Backup)); PlayerPrefs.Save(); File.Delete(Backup); Debug.Log("[CafeAuditGames] save restored"); }
        public static void Run(int w = 1389, int h = 642) { var a = new GameObject("CafeAuditGames").AddComponent<CafeAuditGames>(); a.w = w; a.h = h; }
        int w, h; RenderTexture camRT, uiRT; PanelSettings ps;
        static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

        IEnumerator Start()
        {
            if (!File.Exists(Backup)) File.WriteAllText(Backup, PlayerPrefs.GetString(SaveKey, ""));
            const string dir = "Captures/games"; Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            var ui = CafeUI.I; var M = CafeManager.I; var cam = CafeCamera.I; var doc = FindAnyObjectByType<UIDocument>();
            camRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            uiRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            ps = doc.panelSettings; var prevTT = ps.targetTexture; var prevClear = ps.clearColor;
            cam.Cam.targetTexture = camRT; ps.targetTexture = uiRT; ps.clearColor = true; ps.colorClearValue = new Color(0, 0, 0, 0);
            T Get<T>(string f) => (T)typeof(CafeUI).GetField(f, NP).GetValue(ui);
            void Call(string m, params object[] a) => typeof(CafeUI).GetMethod(m, NP).Invoke(ui, a);
            Call("HideTitle"); M.Data.level = Mathf.Max(M.Data.level, 6); M.Data.tutorial = 99;
            int n = 0;
            IEnumerator Shot(string name, float wait) { yield return new WaitForSeconds(wait); yield return new WaitForEndOfFrame(); Save($"{dir}/{n++:00}_{name}.png"); }
            yield return new WaitForSeconds(1.0f);
            int made = 0; string[] poses = { "Sleep", "Sit", null, "Stroll" };
            foreach (var cu in M.Customers.Where(c => c).Take(4))
            {
                var rq = new PhotoRequest { Cat = made % 2 == 0 ? null : M.Data.residents.FirstOrDefault(), Pose = poses[made % poses.Length], TimeLeft = 40f + made * 15f };
                typeof(Customer).GetProperty("Request").SetValue(cu, rq); typeof(Customer).GetProperty("Bubble").SetValue(cu, Bubble.Photo); made++;
            }
            Debug.Log($"[CafeAuditGames] requests forced: {made}");
            yield return Shot("cafe_requests", 0.8f);
            typeof(CafeUI).GetField("reqOpen", NP).SetValue(ui, true); typeof(CafeUI).GetField("reqSig", NP).SetValue(ui, "");
            yield return Shot("cafe_requests_open", 0.6f);
            typeof(CafeUI).GetField("reqOpen", NP).SetValue(ui, false); typeof(CafeUI).GetField("reqSig", NP).SetValue(ui, "");
            ui.OpenArcade(); yield return Shot("picker", 0.8f);
            Call("OpenHeroPick", "flight"); yield return Shot("hero_pick", 0.8f);
            string hero = M.Data.residents.FirstOrDefault() ?? "Mochi";
            foreach (var g in MiniGames.All)
            {
                ui.PlayGame(g.Id, hero);
                yield return new WaitForSeconds(0.25f);
                if (g.Id == "flight") yield return Shot(g.Id + "_count", 0.3f);
                yield return new WaitForSeconds(3.6f);
                var game = Get<MiniGame>("game"); var stage = Get<VisualElement>("arcStage"); var sz = stage.layout.size;
                float t0 = Time.time;
                while (Time.time - t0 < 2.6f && game != null && !game.Over)
                {
                    float t = Time.time - t0;
                    switch (g.Id)
                    {
                        case "flight": if (Mathf.Repeat(t, 0.38f) < Time.deltaTime) game.Down(new Vector2(sz.x / 2, sz.y / 2)); break;
                        case "pounce": game.Down(new Vector2(Random.Range(0, sz.x), Random.Range(sz.y * 0.3f, sz.y * 0.8f))); break;
                        case "zoomies": game.Move(new Vector2(sz.x / 2 + Mathf.Sin(t * 2f) * sz.x * 0.3f, sz.y * 0.8f)); break;
                        case "redlight": if (t < 0.05f) game.Down(Vector2.zero); break;
                        case "stack": if (Mathf.Repeat(t, 1.2f) < Time.deltaTime) game.Down(Vector2.zero); break;
                        case "laser": game.Move(new Vector2(sz.x / 2 + Mathf.Cos(t * 1.5f) * sz.x * 0.25f, sz.y / 2 + Mathf.Sin(t * 1.5f) * sz.y * 0.25f)); break;
                        case "rooftop": if (Mathf.Repeat(t, 0.9f) < Time.deltaTime) game.Down(new Vector2(sz.x / 2, sz.y / 2)); break;
                        case "yarn": if (Mathf.Repeat(t, 1.0f) < Time.deltaTime) { game.Down(new Vector2(sz.x * Random.Range(0.35f, 0.65f), sz.y * 0.3f)); game.Up(Vector2.zero); } break;
                    }
                    yield return null;
                }
                yield return Shot(g.Id, 0.05f);
                if (g.Id == "pounce") { if (game != null && !game.Over) game.TimeLeft = 0.02f; yield return Shot("results", 2.4f); }
                Call("EndGame", false); yield return new WaitForSeconds(0.3f);
            }
            ui.OpenArcade(); yield return Shot("picker_after", 0.8f); ui.CloseModal();
            // stray flow
            StraySpawner.I.SpawnNow(); yield return new WaitForSeconds(1.5f);
            var stray = FindAnyObjectByType<Stray>();
            if (stray)
            {
                stray.Rec.trust = 1; ui.OpenStray(stray); yield return Shot("stray_steps", 0.8f);
                ui.PlayGame("redlight", hero, "stray", stray.Def, null); yield return Shot("stray_approach", 5.5f);
                Call("EndGame", false); yield return new WaitForSeconds(0.3f);
                ui.PlayGame("rooftop", stray.Def.Id, "stray", stray.Def, null); yield return Shot("stray_home", 5.0f);
                Call("EndGame", false);
            }
            cam.Cam.targetTexture = null; ps.targetTexture = prevTT; ps.clearColor = prevClear; camRT.Release(); uiRT.Release();
            Debug.Log($"[CafeAuditGames] {n} shots"); Destroy(gameObject);
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
