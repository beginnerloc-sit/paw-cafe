using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>
    /// Pan area = the whole café footprint (open AND locked rooms, so players can look around and tap locked-room signs).
    /// The camera starts centred on the open rooms.
    /// </summary>
    public class CameraAreaFromRooms : MonoBehaviour
    {
        public float InsetX = 3.5f, InsetZ = 2f; 
        [Tooltip("Extra pan room towards the street behind the café (Unity metres).")] public float StreetPeek = 5f;   // keep the view from drifting into empty street at the extremes
        int lastCount = -1;
        void Update()
        {
            if (CafeManager.I == null || CafeCamera.I == null) return;
            int n = CafeManager.I.Data.rooms.Count; if (n == lastCount) return;
            Bounds? all = null, open = null;
            foreach (var r in CafeData.Rooms)
            {
                var go = GameObject.Find(r.Model); if (!go) continue;
                var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var rb = rs[0].bounds; foreach (var x in rs) rb.Encapsulate(x.bounds);
                all = Grow(all, rb); if (CafeManager.I.RoomOpen(r.Id)) open = Grow(open, rb);
            }
            if (all != null)
            {
                var bb = all.Value;
                var area = new Bounds(new Vector3(bb.center.x, 0, bb.center.z), new Vector3(Mathf.Max(2f, bb.size.x - InsetX * 2f), 0, Mathf.Max(2f, bb.size.z - InsetZ * 2f)));
                // let the player drag up far enough to watch the shopping street across the road (houses are on the −Z side)
                area.Encapsulate(new Vector3(area.center.x, 0, area.min.z - StreetPeek));
                CafeCamera.I.SetArea(area, false);
                if (lastCount < 0) CafeCamera.I.SnapTo(open?.center ?? area.center);
            }
            lastCount = n;
        }
        static Bounds Grow(Bounds? b, Bounds add) { if (b == null) return add; var t = b.Value; t.Encapsulate(add); return t; }
    }
}
