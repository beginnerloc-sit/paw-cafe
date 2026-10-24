using System.Collections.Generic;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>
    /// Keeps the view clear:
    /// • Dollhouse cut-away — the street-side walls (Front_*) and shop glass (Glass_*) face the camera and block the rooms, so
    ///   they are hidden in the normal close view and come back when the player zooms right out to see the storefront.
    /// • Photo mode — chairs, tables, customers or other cats between the lens and the cat being photographed are hidden
    ///   (also from the saved photo) and pop back as soon as they are out of the way.
    /// Colliders are never touched.
    /// </summary>
    [RequireComponent(typeof(CafeCamera))]
    public class CafeCutaway : MonoBehaviour
    {
        [Tooltip("Camera zoom (1 = default) above which the storefront walls are shown again.")]
        public float ShowWallsAboveZoom = 1.35f;
        [Tooltip("Radius of the clear corridor between the photo camera and the cat.")]
        public float PhotoClearRadius = 0.45f;
        readonly List<Renderer> walls = new();
        readonly Dictionary<GameObject, List<Renderer>> hidden = new(); readonly HashSet<GameObject> now = new();
        readonly RaycastHit[] hits = new RaycastHit[32];
        bool shown = true, ready;

        void Start()
        {
            foreach (var r in FindObjectsByType<Renderer>())
                if (r.name.StartsWith("Front_") || r.name.StartsWith("Glass_")) walls.Add(r);
            ready = true; ApplyWalls(false);
        }

        void LateUpdate()
        {
            var cc = CafeCamera.I; if (!ready || cc == null) return;
            bool want = cc.ZoomLevel > ShowWallsAboveZoom && !cc.Focused;
            if (want != shown) ApplyWalls(want);
            ClearPhotoView(cc);
        }

        void ApplyWalls(bool on) { shown = on; foreach (var r in walls) if (r) r.enabled = on; }

        void ClearPhotoView(CafeCamera cc)
        {
            now.Clear();
            var target = cc.FocusTarget;
            if (target)
            {
                Vector3 from = cc.transform.position, to = target.position + Vector3.up * 0.35f, dir = to - from;
                float len = dir.magnitude - 0.35f;
                if (len > 0.05f)
                {
                    int n = Physics.SphereCastNonAlloc(from, PhotoClearRadius, dir.normalized, hits, len, ~0, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < n; i++)
                    {
                        var col = hits[i].collider; if (!col || col.transform.IsChildOf(target)) continue;
                        Component owner = col.GetComponentInParent<FurnitureInstance>();
                        if (!owner) owner = col.GetComponentInParent<Customer>();
                        if (!owner) owner = col.GetComponentInParent<CafeCat>();
                        if (owner && !target.IsChildOf(owner.transform)) now.Add(owner.gameObject);
                    }
                }
            }
            foreach (var go in now)
                if (!hidden.ContainsKey(go))
                {
                    var off = new List<Renderer>();   // remember only what we switched off, so we restore exactly that
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; off.Add(r); }
                    hidden[go] = off;
                }
            if (hidden.Count == now.Count) return;
            var back = new List<GameObject>();
            foreach (var kv in hidden) if (!kv.Key || !now.Contains(kv.Key)) back.Add(kv.Key);
            foreach (var go in back) { foreach (var r in hidden[go]) if (r) r.enabled = true; hidden.Remove(go); }
        }
    }
}
