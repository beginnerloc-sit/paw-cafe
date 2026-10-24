using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Keeps the camera's pan area matched to the open rooms.</summary>
    public class CameraAreaFromRooms : MonoBehaviour
    {
        int lastCount = -1;
        void Update()
        {
            if (CafeManager.I == null || CafeCamera.I == null) return;
            int n = CafeManager.I.Data.rooms.Count; if (n == lastCount) return;
            Bounds? b = null;
            foreach (var r in CafeData.Rooms.Where(r => CafeManager.I.RoomOpen(r.Id)))
            {
                var go = GameObject.Find(r.Model); if (!go) continue; var rb = go.GetComponent<Renderer>().bounds;
                if (b == null) b = rb; else { var t = b.Value; t.Encapsulate(rb); b = t; }
            }
            if (b != null) { var bb = b.Value; var area = new Bounds(new Vector3(bb.center.x, 0, bb.center.z), new Vector3(Mathf.Max(0.5f, bb.size.x - 7f), 0, Mathf.Max(0.5f, bb.size.z - 6.5f))); CafeCamera.I.SetArea(area, lastCount < 0); }
            lastCount = n;
        }
    }
}
