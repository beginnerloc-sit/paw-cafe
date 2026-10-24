using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Rain outside the café (the streets behind and in front of it — never indoors), with a slightly dimmer,
    /// cooler sun while it pours. Turned on while a stray is out there.</summary>
    public class CafeRain : MonoBehaviour
    {
        public static CafeRain I { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() => I = null;
        public static CafeRain Ensure() { if (I) return I; var go = new GameObject("CafeRain"); return I = go.AddComponent<CafeRain>(); }

        ParticleSystem back, front; Light sun; float sunI0 = -1; Color sunC0; float amount, target;
        static Vector3 B(float bx, float by, float bz) => new Vector3(-bx * CafeData.S, bz * CafeData.S, -by * CafeData.S);

        void Awake()
        {
            I = this;
            back = Make("RainBack", B(2.5f, 5.0f, 7f), new Vector3(70f, 13f, 1f), 1300);    // road + houses behind the café
            front = Make("RainFront", B(2.5f, -3.4f, 7f), new Vector3(70f, 6f, 1f), 500);   // pavement in front
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun) { sunI0 = sun.intensity; sunC0 = sun.color; }
        }
        ParticleSystem Make(string n, Vector3 pos, Vector3 area, int rate)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));   // emit downwards
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = 0.75f; main.startSpeed = 24f; main.startSize = 0.045f;
            main.startColor = new Color(0.78f, 0.87f, 1f, 0.55f); main.maxParticles = 3000; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = area;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2.8f; r.velocityScale = 0.025f;
            r.sharedMaterial = Resources.Load<Material>("Shelter/Glow"); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        public bool Raining => target > 0.5f;
        public void SetRaining(bool on, bool instant)
        {
            target = on ? 1f : 0f; if (instant) amount = target;
            foreach (var ps in new[] { back, front }) { if (!ps) continue; if (on && !ps.isPlaying) ps.Play(); else if (!on) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }
        void Update()
        {
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * 0.5f);
            if (sun && sunI0 >= 0)
            {
                sun.intensity = Mathf.Lerp(sunI0, sunI0 * 0.78f, amount);
                sun.color = Color.Lerp(sunC0, new Color(0.86f, 0.9f, 1f), amount * 0.8f);
            }
        }
    }
}
