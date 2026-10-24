#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using PawCollector.Cafe;

namespace PawCollector
{
    /// <summary>Editor QA walkthrough of the café: captures every main screen into Captures/cafe/qa_*.png.</summary>
    public class CafeQA : MonoBehaviour
    {
        public static void Run() { new GameObject("CafeQA").AddComponent<CafeQA>(); }
        IEnumerator Shot(string n) { CafeShot.Take("qa_" + n); yield return Wait(0.6f); }
        static IEnumerator Wait(float s) { float t = Time.realtimeSinceStartup + s; while (Time.realtimeSinceStartup < t) yield return null; }
        IEnumerator Start()
        {
            var cm = CafeManager.I; var ui = CafeUI.I;
            yield return Wait(14f);
            yield return Shot("1_cafe");
            ui.OpenTab("Cats"); yield return Wait(0.8f); yield return Shot("2_cats");
            ui.OpenTab("Decorate"); yield return Wait(0.8f); yield return Shot("3_decorate");
            ui.OpenTab("Photos"); yield return Wait(0.8f); yield return Shot("4_photos");
            ui.OpenTab("Shelter"); yield return Wait(0.8f); yield return Shot("5_shelter");
            ui.OpenTab("Cafe"); yield return Wait(0.5f);
            cm.AddCoins(3000); cm.AddXp(200); yield return Wait(1f); yield return Shot("6_levelup"); ui.CloseModal();
            ui.OpenTab("Shelter"); yield return Wait(0.5f);
            ui.DrawTicket();
            yield return Wait(1f); yield return Shot("7_adopt"); ui.CloseModal(); ui.OpenTab("Cafe");
            cm.TryUnlockRoom("nook"); yield return Wait(1f);
            StraySpawner.I.SpawnNow(); yield return Wait(2f);
            var stray = FindAnyObjectByType<Stray>(); if (stray) { ui.OpenStray(stray); yield return Wait(0.8f); yield return Shot("8_stray"); ui.CloseModal(); }
            yield return Wait(6f); yield return Shot("9_cafe2");
            var cat = cm.Cats.FirstOrDefault(); if (cat) { CafePhoto.I.Enter(cat); yield return Wait(2.2f); yield return Shot("10_viewfinder"); CafePhoto.I.Shutter(); yield return Wait(1f); yield return Shot("11_polaroid"); ui.CloseModal(); CafePhoto.I.Exit(); }
            Debug.Log("[CafeQA] done"); Destroy(gameObject);
        }
    }
}
#endif
