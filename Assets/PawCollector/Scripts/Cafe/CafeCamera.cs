using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Portrait café camera: pan across the open rooms, pinch to zoom, eases in on a cat for photos.</summary>
    [DefaultExecutionOrder(-150)]
    public class CafeCamera : MonoBehaviour
    {
        public static CafeCamera I { get; private set; }
        public Camera Cam { get; private set; }
        public Vector3 LookDir = new Vector3(0, 0, -1);    // horizontal direction the camera faces (towards the back wall)
        public float Pitch = 56f, Fov = 34f, MinDist = 6f, MaxDist = 17f;
        public float PhotoPitch = 26f, PhotoDist = 4.4f, PhotoFov = 40f;
        public Bounds Area;                                   // pannable floor area (open rooms)
        public bool Focused => focus;
        Vector3 pivot, tPivot; float dist = 12f, zoom = 1f, tZoom = 1f, punch;
        Transform focus; float focusW, pYaw, tpYaw, pTilt, tpTilt, pZoom = 1, tpZoom = 1; Vector3 photoLook;

        void Awake() { I = this; Cam = GetComponent<Camera>(); LookDir.y = 0; LookDir.Normalize(); }
        public void SetArea(Bounds b, bool snap)
        {
            Area = b; tPivot = ClampPivot(tPivot == Vector3.zero ? b.center : tPivot);
            if (snap) { pivot = tPivot = ClampPivot(b.center); dist = FitDist(); }
        }
        public float FitHalfWidth = 4.8f;
        /// <summary>Distance that fits about one room across the narrow (horizontal) axis of the screen.</summary>
        float FitDist()
        {
            float hFov = 2f * Mathf.Atan(Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, Cam.aspect));
            return Mathf.Clamp(FitHalfWidth / Mathf.Tan(hFov * 0.5f), MinDist, MaxDist * 2f);
        }
        Vector3 ClampPivot(Vector3 p) { p.y = 0; p.x = Mathf.Clamp(p.x, Area.min.x, Area.max.x); p.z = Mathf.Clamp(p.z, Area.min.z, Area.max.z); return p; }
        Vector3 Right => Vector3.Cross(Vector3.up, LookDir).normalized;

        /// <summary>Drag in screen pixels.</summary>
        public void Pan(Vector2 d)
        {
            if (focus) { tpYaw += d.x * 0.25f; tpTilt = Mathf.Clamp(tpTilt - d.y * 0.12f, -12f, 30f); return; }
            float k = dist * 0.0016f * (1080f / Mathf.Max(400, Screen.width));
            tPivot = ClampPivot(tPivot - Right * d.x * k - LookDir * d.y * k * 1.3f);
        }
        public void Zoom(float f) { if (focus) tpZoom = Mathf.Clamp(tpZoom * f, 0.55f, 1.6f); else tZoom = Mathf.Clamp(tZoom * f, 0.5f, 1.25f); }
        public void Focus(Transform t)
        {
            if (!focus) photoLook = t.position + Vector3.up * 0.5f;
            if (focus != t)
            {
                // start in front of the cat (camera looks against the cat's facing), 30° off-axis for a nicer three-quarter view
                var want = -t.forward; want.y = 0;
                float yaw = Vector3.SignedAngle(LookDir, want.sqrMagnitude > 0.01f ? want.normalized : LookDir, Vector3.up);
                tpYaw = yaw + (yaw > 0 ? -25f : 25f); tpTilt = 0; tpZoom = 1;
            }
            focus = t;
        }
        public void Unfocus() => focus = null;
        public void Punch() => punch = 1f;
        public void LookAtPoint(Vector3 p) { tPivot = ClampPivot(p); }

        void LateUpdate()
        {
            float dt = Time.deltaTime, k = 1f - Mathf.Exp(-9f * dt);
            pivot = Vector3.Lerp(pivot, tPivot, k); zoom = Mathf.Lerp(zoom, tZoom, k); dist = FitDist() * zoom;
            pYaw = Mathf.Lerp(pYaw, tpYaw, k); pTilt = Mathf.Lerp(pTilt, tpTilt, k); pZoom = Mathf.Lerp(pZoom, tpZoom, k);
            focusW = Mathf.MoveTowards(focusW, focus ? 1f : 0f, dt * 2.2f); float w = Mathf.SmoothStep(0, 1, focusW);
            punch = Mathf.MoveTowards(punch, 0, dt * 2.2f);
            var rot = Quaternion.LookRotation(Quaternion.AngleAxis(Pitch, Right) * LookDir, Vector3.up);
            var cafePos = pivot - rot * Vector3.forward * dist;
            if (focus) photoLook = Vector3.Lerp(photoLook, focus.position + Vector3.up * 0.5f, 1f - Mathf.Exp(-5f * dt));
            var pf = Quaternion.AngleAxis(pYaw, Vector3.up) * LookDir; var pr = Vector3.Cross(Vector3.up, pf).normalized;
            var prot = Quaternion.LookRotation(Quaternion.AngleAxis(PhotoPitch + pTilt, pr) * pf, Vector3.up);
            var photoPos = photoLook - prot * Vector3.forward * PhotoDist * pZoom;
            var pos = Vector3.Lerp(cafePos, photoPos, w);
            var look = Vector3.Lerp(pivot, photoLook, w);
            transform.position = pos; transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            Cam.fieldOfView = Mathf.Lerp(Fov, PhotoFov, w) - 2f * Mathf.Sin(punch * Mathf.PI);
        }
    }
}
