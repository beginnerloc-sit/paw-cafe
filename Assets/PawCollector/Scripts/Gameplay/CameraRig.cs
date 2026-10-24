using UnityEngine;

namespace PawCollector
{
    /// <summary>Portrait garden camera: frames the mats, idles gently, eases in on a cat for photos.</summary>
    [DefaultExecutionOrder(-200)]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        public static Vector3 FlatForward { get; private set; } = Vector3.forward;
        public static Vector3 FlatRight { get; private set; } = Vector3.right;
        public Camera Cam { get; private set; }
        public Vector3 BasePosition => basePos;
        public bool Focused => focus != null;

        public float Pitch = 52f, Fov = 38f, FitHalfWidth = 2.05f, FrontEdge = 3.9f;
        public float PhotoPitch = 32f, PhotoHalfWidth = 0.8f, PhotoFov = 46f;
        Vector3 basePos, focusPoint, photoLook; Quaternion baseRot;
        float punch, baseFov, focusW;
        Transform focus;

        void Awake()
        {
            I = this;
            Cam = GetComponent<Camera>();
            Frame();
        }

        public void Frame()
        {
            var spots = FindObjectsByType<Spot>(FindObjectsSortMode.None);
            if (spots.Length == 0) return;
            var b = new Bounds(spots[0].transform.position, Vector3.zero);
            foreach (var s in spots) b.Encapsulate(s.transform.position);
            // the cottage sits behind the mats: look from the side opposite to it
            var house = GameObject.Find("Walls");
            Vector3 fwd = house ? house.transform.position - b.center : Vector3.forward;
            if (house) { var r = house.GetComponent<Renderer>(); if (r) fwd = r.bounds.center - b.center; }
            fwd.y = 0; fwd.Normalize();
            FlatForward = fwd; FlatRight = Vector3.Cross(Vector3.up, fwd).normalized;
            focusPoint = b.center - fwd * 0.15f;
            Cam.fieldOfView = baseFov = Fov;
            // fit the mat field's width on the narrow (horizontal) portrait axis; tall phones show more garden vertically
            float halfW = Mathf.Max(FitHalfWidth, b.extents.x + 0.8f);
            float hFov = 2f * Mathf.Atan(Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, Cam.aspect));
            float dist = Mathf.Min(halfW / Mathf.Tan(hFov * 0.5f), 14f);
            baseRot = Quaternion.LookRotation(Quaternion.AngleAxis(Pitch, FlatRight) * fwd, Vector3.up);
            basePos = focusPoint - baseRot * Vector3.forward * dist;
            // very tall phones see past the front edge of the garden: slide the view back until the bottom of the
            // screen still lands on the lawn
            var down = Quaternion.AngleAxis(Fov * 0.5f, baseRot * Vector3.right) * (baseRot * Vector3.forward);
            if (down.y < -0.01f)
            {
                var hit = basePos + down * (-basePos.y / down.y);
                float front = Vector3.Dot(hit, -fwd), limit = Vector3.Dot(b.center, -fwd) + FrontEdge;
                if (front > limit) { var shift = fwd * (front - limit); basePos += shift; focusPoint += shift; }
            }
            transform.SetPositionAndRotation(basePos, baseRot);
        }

        public void Punch() => punch = 1f;

        // ---- player camera control (drag to orbit, pinch / wheel to zoom)
        float userYaw, userTilt, userZoom = 1f, tYaw, tTilt, tZoom = 1f;
        float photoYaw, photoTilt, photoZoom = 1f, tpYaw, tpTilt, tpZoom = 1f;

        /// <summary>Drag in panel units (+x right, +y up).</summary>
        public void Orbit(float dx, float dy)
        {
            if (focus)
            {
                tpYaw = Mathf.Clamp(tpYaw + dx * 0.22f, -80f, 80f);
                tpTilt = Mathf.Clamp(tpTilt - dy * 0.12f, -16f, 26f);
            }
            else
            {
                tYaw = Mathf.Clamp(tYaw + dx * 0.12f, -32f, 32f);
                tTilt = Mathf.Clamp(tTilt - dy * 0.06f, -14f, 10f);
            }
        }

        /// <summary>factor &lt; 1 zooms in.</summary>
        public void Zoom(float factor)
        {
            if (focus) tpZoom = Mathf.Clamp(tpZoom * factor, 0.55f, 1.5f);
            else tZoom = Mathf.Clamp(tZoom * factor, 0.62f, 1f);
        }

        public void ResetView() { tYaw = tTilt = 0f; tZoom = 1f; }

        public void Focus(Transform t)
        {
            if (focus == null) photoLook = t.position + Vector3.up * 0.22f;
            if (focus != t) { tpYaw = tpTilt = 0f; tpZoom = 1f; }
            focus = t;
        }

        public void Unfocus() => focus = null;

        float framedAspect;

        void LateUpdate()
        {
            if (Mathf.Abs(Cam.aspect - framedAspect) > 0.01f) { framedAspect = Cam.aspect; Frame(); }   // rotation / simulator device change
            float dt = Time.deltaTime;
            punch = Mathf.MoveTowards(punch, 0f, dt * 2.2f);
            focusW = Mathf.MoveTowards(focusW, focus ? 1f : 0f, dt * 2.4f);
            float w = Mathf.SmoothStep(0f, 1f, focusW);

            float t = Time.time;
            var drift = FlatRight * Mathf.Sin(t * 0.13f) * 0.12f + Vector3.up * Mathf.Sin(t * 0.17f) * 0.05f;
            float k = 1f - Mathf.Exp(-10f * dt);
            userYaw = Mathf.Lerp(userYaw, tYaw, k); userTilt = Mathf.Lerp(userTilt, tTilt, k); userZoom = Mathf.Lerp(userZoom, tZoom, k);
            photoYaw = Mathf.Lerp(photoYaw, tpYaw, k); photoTilt = Mathf.Lerp(photoTilt, tpTilt, k); photoZoom = Mathf.Lerp(photoZoom, tpZoom, k);
            // garden view: orbit around the focus point (yaw about up, tilt about the camera's right), dolly for zoom
            var arm = basePos - focusPoint;
            arm = Quaternion.AngleAxis(userYaw, Vector3.up) * Quaternion.AngleAxis(userTilt, FlatRight) * arm;
            var gardenPos = focusPoint + arm * userZoom + drift;

            if (focus) photoLook = Vector3.Lerp(photoLook, focus.position + Vector3.up * 0.22f, 1f - Mathf.Exp(-5f * dt));
            var pFwd = Quaternion.AngleAxis(photoYaw, Vector3.up) * FlatForward;
            var pRight = Vector3.Cross(Vector3.up, pFwd).normalized;
            var photoRot = Quaternion.LookRotation(Quaternion.AngleAxis(PhotoPitch + photoTilt, pRight) * pFwd, Vector3.up);
            // keep ~1.6 m of garden across the narrow portrait axis around the cat
            float hFov = 2f * Mathf.Atan(Mathf.Tan(PhotoFov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, Cam.aspect));
            float photoDist = Mathf.Clamp(PhotoHalfWidth / Mathf.Tan(hFov * 0.5f), 1.8f, 4.5f) * photoZoom;
            var photoPos = photoLook - photoRot * Vector3.forward * photoDist + drift * 0.3f;

            var pos = Vector3.Lerp(gardenPos, photoPos, w);
            var look = Vector3.Lerp(focusPoint, photoLook, w);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            Cam.fieldOfView = Mathf.Lerp(baseFov, PhotoFov, w) - 2.2f * Mathf.Sin(punch * Mathf.PI);
        }
    }
}
