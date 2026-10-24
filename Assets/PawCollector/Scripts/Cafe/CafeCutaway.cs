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
        public float PhotoClearRadius = 0.3f;
        [Tooltip("Only things at least this far in front of the cat are hidden — never what it sits on or stands beside.")]
        public float KeepNearCat = 0.6f;
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

        /// <summary>True if the cat's paws are on or right beside this piece (inside its footprint, a little padded).</summary>
        static bool Supports(Component furn, Vector3 feet)
        {
            var cols = furn.GetComponentsInChildren<Collider>(); if (cols.Length == 0) return false;
            var b = cols[0].bounds; foreach (var c in cols) b.Encapsulate(c.bounds);
            b.Expand(new Vector3(0.5f, 0f, 0.5f));
            return feet.x > b.min.x && feet.x < b.max.x && feet.z > b.min.z && feet.z < b.max.z && feet.y > b.min.y - 0.1f;
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
                    var cat = target.GetComponentInParent<CafeCat>(); var perch = cat ? cat.PerchFurniture : null;
                    for (int i = 0; i < n; i++)
                    {
                        var col = hits[i].collider; if (!col || col.transform.IsChildOf(target)) continue;
                        if (hits[i].distance > 0f && hits[i].distance > len - KeepNearCat) continue;   // right next to the cat: keep it
                        Component owner = col.GetComponentInParent<FurnitureInstance>();
                        if (owner && (owner == perch || Supports(owner, target.position))) continue;   // what the cat is on stays put
                        if (!owner) owner = col.GetComponentInParent<Customer>();
                        if (!owner) owner = col.GetComponentInParent<CafeCat>();
                        if (owner && !target.IsChildOf(owner.transform)) now.Add(owner.gameObject);
                    }
                }
            }
            // never leave anyone floating: whoever sits on a hidden piece is hidden with it, and a hidden customer takes their seat along
            if (now.Count > 0 && CafeManager.I)
            {
                var furn = CafeManager.I.Furniture; var extra = new List<GameObject>();
                foreach (var go in now)
                {
                    var f = go.GetComponent<FurnitureInstance>();
                    if (f)
                    {
                        foreach (var c in CafeManager.I.Customers) if (c && Supports(f, c.transform.position)) extra.Add(c.gameObject);
                        foreach (var c in CafeManager.I.Cats) if (c && c.transform != target && (c.PerchFurniture == f)) extra.Add(c.gameObject);
                    }
                    else if (go.GetComponent<Customer>())
                        foreach (var g in furn) if (g && g.Def.Seats > 0 && Supports(g, go.transform.position)) extra.Add(g.gameObject);
                }
                foreach (var e in extra) if (target == null || !target.IsChildOf(e.transform)) now.Add(e);
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
