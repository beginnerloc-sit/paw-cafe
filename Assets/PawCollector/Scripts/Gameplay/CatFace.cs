using UnityEngine;

namespace PawCollector
{
    /// <summary>Drives the "Blink" blend shape: natural random blinks, and eyes closed while sleeping.</summary>
    public class CatFace : MonoBehaviour
    {
        public bool EyesClosed;
        SkinnedMeshRenderer smr; int idx = -1;
        float w, nextBlink, blinkT = -1f;

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.sharedMesh == null) continue;
                int i = r.sharedMesh.GetBlendShapeIndex("Blink");
                if (i >= 0) { smr = r; idx = i; break; }
            }
            nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        void Update()
        {
            if (idx < 0) return;
            float target = 0f;
            if (EyesClosed) target = 100f;
            else
            {
                if (blinkT < 0f && Time.time > nextBlink) blinkT = 0f;
                if (blinkT >= 0f)
                {
                    blinkT += Time.deltaTime / 0.16f;
                    target = 100f * Mathf.Sin(Mathf.Clamp01(blinkT) * Mathf.PI);
                    if (blinkT >= 1f) { blinkT = -1f; nextBlink = Time.time + Random.Range(2.5f, 6f); }
                }
            }
            w = EyesClosed ? Mathf.MoveTowards(w, target, Time.deltaTime * 250f) : target;
            smr.SetBlendShapeWeight(idx, w);
        }
    }
}
