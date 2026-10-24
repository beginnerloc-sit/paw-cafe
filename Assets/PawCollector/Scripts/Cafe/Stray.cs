using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>A stray cat waiting on the ledge outside the window. Treat → play → photo → adopt.</summary>
    public class Stray : MonoBehaviour
    {
        public StrayRec Rec; public CatDef Def; Animator anim; float idleT;
        public static readonly string[] Steps = { "Give a treat", "Play together", "Take their photo", "Adopt" };
        public const int TreatCost = 15;
        void Start() { anim = GetComponentInChildren<Animator>(); if (anim) anim.CrossFadeInFixedTime("Idle_Sit", 0f); }
        void Update()
        {
            idleT -= Time.deltaTime;
            if (idleT <= 0 && anim) { idleT = Random.Range(4f, 8f); anim.CrossFadeInFixedTime(Random.value < 0.4f ? "Groom" : "Idle_Sit", 0.3f, 0, 0f); }
        }
        public void Treat()
        {
            if (Rec.trust != 0 || !CafeManager.I.Spend(TreatCost)) return;
            Rec.trust = 1; anim?.CrossFadeInFixedTime("Happy", 0.15f, 0, 0f); Fx.Hearts(transform.position + Vector3.up, 10, 0.9f); AudioManager.I?.Meow(1f);
            CafeManager.I.Save();
        }
        public void PlayWith()
        {
            if (Rec.trust != 1) return;
            Rec.trust = 2; anim?.CrossFadeInFixedTime("Bat", 0.15f, 0, 0f); idleT = 3f; Fx.Hearts(transform.position + Vector3.up, 10, 0.9f);
            CafeManager.I.Save();
        }
        public void Photographed() { if (Rec.trust == 2) { Rec.trust = 3; CafeManager.I.Save(); } }
        public void Adopt()
        {
            if (Rec.trust < 3) return;
            if (CafeManager.I.FreeBeds <= 0) { CafeManager.I.Say("Every cat needs its own bed — buy a cat bed first"); return; }
            CafeManager.I.Adopt(Def.Id, out _);
            CafeManager.I.Data.strays.Remove(Rec); StraySpawner.I.Remove(this);
            CafeManager.I.Say($"{Def.Id} joined your café!");
        }
    }

}
