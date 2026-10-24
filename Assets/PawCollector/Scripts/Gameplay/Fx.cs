using UnityEngine;

namespace PawCollector
{
    /// <summary>Tiny runtime particle effects (sparkles on rare visits, hearts on snaps).</summary>
    public static class Fx
    {
        static Material mat;
        static Material Mat()
        {
            if (mat) return mat;
            mat = Resources.Load<Material>("Fx/FxParticle");
            if (mat && mat.mainTexture == null)
            {
                const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float star = Mathf.Max(0, 1 - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) * 9) * Mathf.Max(0, 1 - r);
                    float a = Mathf.Clamp01(Mathf.Pow(Mathf.Max(0, 1 - r), 2.5f) + star);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                tex.Apply(); mat.mainTexture = tex;
            }
            return mat;
        }

        static ParticleSystem Make(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat();
            return ps;
        }

        public static ParticleSystem Sparkles(Transform cat) => Sparkles(cat, cat.position + Vector3.up * 0.3f, 0f);

        /// <summary>Looping sparkles (duration 0) or a short burst that cleans itself up.</summary>
        public static ParticleSystem Sparkles(Transform parent, Vector3 pos, float duration)
        {
            var ps = Make("Sparkles", parent, pos);
            var m = ps.main; m.loop = duration <= 0f; if (duration > 0f) { m.duration = duration; m.stopAction = ParticleSystemStopAction.Destroy; } m.startLifetime = 1.2f; m.startSpeed = 0.15f; m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.5f), new Color(1f, 1f, 1f)); m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = -0.05f;
            var e = ps.emission; e.rateOverTime = 9;
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.35f;
            var c = ps.colorOverLifetime; c.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) }); c.color = g;
            ps.Play();
            return ps;
        }

        public static void Hearts(Vector3 pos) => Hearts(pos, 14, 1f);

        public static void Hearts(Vector3 pos, int count, float size)
        {
            var ps = Make("Hearts", null, pos);
            var m = ps.main; m.loop = false; m.duration = 0.5f; m.startLifetime = 1.1f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.07f * size, 0.13f * size); m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.68f), new Color(1f, 0.8f, 0.4f));
            m.gravityModifier = -0.15f; m.stopAction = ParticleSystemStopAction.Destroy;
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = 0.12f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            ps.Play();
        }
    
        public static void Coins(Vector3 pos, int count = 10)
        {
            var ps = Make("Coins", null, pos);
            var m = ps.main; m.loop = false; m.duration = 0.4f; m.startLifetime = 0.9f; m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.14f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.3f), new Color(1f, 0.95f, 0.6f));
            m.gravityModifier = 0.9f; m.stopAction = ParticleSystemStopAction.Destroy;
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 25; s.radius = 0.05f; s.rotation = new Vector3(-90, 0, 0);
            ps.Play();
        }
    }
}
