using System.Collections;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>A hungry stray out in the rain on the road behind the café. It wanders slowly, sniffs the ground for food,
    /// then sits facing the café, soaked, shivering and mewing softly. Treat → play → photo → adopt.
    /// Positions are in Blender units of the café model (×S, X/Z flipped) like StreetLife.</summary>
    public class Stray : MonoBehaviour
    {
        public StrayRec Rec; public CatDef Def;
        public static readonly string[] Steps = { "Give food", "Play together", "Take their photo", "Adopt" };
        public const int TreatCost = 15;

        // the visible strip of road in front of the houses (Blender X along the street, Y depth)
        public const float MinX = -5.5f, MaxX = 10.5f, NearY = 4.3f, FarY = 4.7f;
        static Vector3 B(float bx, float by) => new Vector3(-bx * CafeData.S, 0f, -by * CafeData.S);
        static readonly Quaternion FaceCafe = Quaternion.LookRotation(Vector3.forward);   // towards the café and the camera

        Animator anim; Renderer[] rends; MaterialPropertyBlock mpb;
        Vector3 basePos; bool shivering; float wet = 1f, mewT;
        string clip = "";
        public bool Busy { get; private set; }   // eating / playing (no wandering)

        void Start()
        {
            anim = GetComponentInChildren<Animator>(); rends = GetComponentsInChildren<Renderer>(); mpb = new MaterialPropertyBlock();
            wet = Rec != null && Rec.trust >= 1 ? 0.35f : 1f;
            StartCoroutine(Life());
        }
        void Play(string c, float fade = 0.25f, bool restart = false)
        {
            if (!anim || (clip == c && !restart)) return; clip = c;
            var face = GetComponentInChildren<CatFace>(); if (face) face.EyesClosed = c == "Sleep";
            if (restart) anim.CrossFadeInFixedTime(c, fade, 0, 0f); else anim.CrossFadeInFixedTime(c, fade);
        }

        IEnumerator Life()
        {
            // trudge in from the end of the street
            float side = Random.value < 0.5f ? MinX - 2f : MaxX + 2f;
            transform.position = B(side, Random.Range(NearY, FarY));
            yield return WalkTo(B(Random.Range(0f, 5f), Random.Range(NearY, FarY)));
            while (true)
            {
                if (Busy) { yield return null; continue; }
                float r = Random.value;
                if (r < 0.35f) yield return WalkTo(B(Random.Range(MinX, MaxX), Random.Range(NearY, FarY)));
                else if (r < 0.55f) { Play("Sniff"); yield return Wait(Random.Range(2.5f, 4f)); }      // looking for scraps
                else yield return SitSadly(Random.Range(7f, 13f));
            }
        }
        IEnumerator Wait(float s) { for (float t = 0; t < s && !Busy; t += Time.deltaTime) yield return null; }
        IEnumerator WalkTo(Vector3 target)
        {
            shivering = false; Play("Walk", 0.2f);
            if (anim) anim.speed = 0.6f;                          // slow, tired steps
            while (!Busy && (target - transform.position).sqrMagnitude > 0.01f)
            {
                var d = target - transform.position; d.y = 0;
                transform.position += d.normalized * Mathf.Min(d.magnitude, 0.45f * CafeData.S * 0.5f * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 4f);
                yield return null;
            }
            if (anim) anim.speed = 1f;
            basePos = transform.position;
        }
        IEnumerator SitSadly(float dur)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.5f) { transform.rotation = Quaternion.Slerp(transform.rotation, FaceCafe, t); yield return null; }
            basePos = transform.position;
            Play("SitDown", 0.15f, true); yield return new WaitForSeconds(0.7f);
            Play("Idle_Sit", 0.3f);
            if (anim) anim.speed = 0.7f;
            shivering = Rec == null || Rec.trust < 1;
            for (float t = 0; t < dur && !Busy; t += Time.deltaTime)
            {
                mewT -= Time.deltaTime;
                if (mewT <= 0) { mewT = Random.Range(6f, 11f); if (IsVisible()) AudioManager.I?.Play("sfx_meow" + Random.Range(1, 4), 0.35f, 1.35f); }
                yield return null;
            }
            shivering = false; transform.position = basePos;
            if (anim) anim.speed = 1f;
            if (!Busy) { Play("StandUp", 0.15f, true); yield return new WaitForSeconds(0.7f); }
        }
        bool IsVisible() { var cam = CafeCamera.I ? CafeCamera.I.Cam : null; if (!cam || !cam.enabled) return false; var v = cam.WorldToViewportPoint(transform.position); return v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1; }

        void Update()
        {
            // soaked fur (bluish, darker) dries a little once fed
            float target = Rec != null && Rec.trust >= 1 ? 0.35f : 1f; wet = Mathf.MoveTowards(wet, target, Time.deltaTime * 0.3f);
            var tint = Color.Lerp(Color.white, new Color(0.70f, 0.75f, 0.90f), wet);
            mpb ??= new MaterialPropertyBlock();   // survives a script reload during Play
            if (rends != null) foreach (var r in rends) { if (!r) continue; r.GetPropertyBlock(mpb); mpb.SetColor("_Tint", tint); r.SetPropertyBlock(mpb); }
            // shivering in the cold
            if (shivering) transform.position = basePos + new Vector3(Mathf.Sin(Time.time * 47f) * 0.012f, 0, Mathf.Cos(Time.time * 39f) * 0.006f);
        }

        // ---------------------------------------------------------------- trust steps
        public void Treat()
        {
            if (Rec.trust != 0 || !CafeManager.I.Spend(TreatCost)) return;
            Rec.trust = 1; StartCoroutine(Eat()); CafeManager.I.Save();
        }
        IEnumerator Eat()
        {
            Busy = true; shivering = false; transform.position = basePos == Vector3.zero ? transform.position : basePos;
            transform.rotation = FaceCafe; Play("Eat", 0.2f, true);
            AudioManager.I?.Play("sfx_purr", 0.6f); yield return new WaitForSeconds(3.2f);
            Play("Happy", 0.15f, true); Fx.Hearts(transform.position + Vector3.up, 10, 0.9f); AudioManager.I?.Meow(1.15f);
            yield return new WaitForSeconds(1.5f); Busy = false;
        }
        public void PlayWith()
        {
            if (Rec.trust != 1) return;
            Rec.trust = 2; StartCoroutine(PlayCo()); CafeManager.I.Save();
        }
        IEnumerator PlayCo()
        {
            Busy = true; shivering = false; transform.rotation = FaceCafe;
            Play("Bat", 0.15f, true); yield return new WaitForSeconds(2f);
            Play("Pounce", 0.1f, true); Fx.Hearts(transform.position + Vector3.up, 10, 0.9f); yield return new WaitForSeconds(1.5f);
            Busy = false;
        }
        public void Photographed() { if (Rec.trust == 2) { Rec.trust = 3; CafeManager.I.Save(); } }
        public void Adopt()
        {
            if (Rec.trust < 3) return;
            if (CafeManager.I.FreeBeds <= 0) { CafeManager.I.Say("Every cat needs its own bed — buy a cat bed first"); return; }
            CafeManager.I.Adopt(Def.Id, out _);
            CafeManager.I.Data.strays.Remove(Rec); StraySpawner.I.Remove(this);
            CafeManager.I.Say($"{Def.Id} is warm and dry at last — welcome home!");
        }
    }
}
