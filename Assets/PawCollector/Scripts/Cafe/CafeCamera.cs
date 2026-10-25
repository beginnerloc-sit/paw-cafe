using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Landscape café camera: drag pans 1:1 (the floor under your finger stays under your finger) with a
    /// smooth glide when you let go, pinch to zoom, eases in on a cat for photos.</summary>
    [DefaultExecutionOrder(-150)]
    public class CafeCamera : MonoBehaviour
    {
        public static CafeCamera I { get; private set; }
        public Camera Cam { get; private set; }
        public Vector3 LookDir = new Vector3(0, 0, -1);    // horizontal direction the camera faces (towards the back wall)
        public float Pitch = 48f, Fov = 34f, MinDist = 6f, MaxDist = 17f;
        public float PhotoPitch = 26f, PhotoDist = 4.4f, PhotoFov = 40f;
        public Bounds Area;                                   // pannable floor area (open rooms)
        public bool Focused => focus;
        public Transform FocusTarget => focus;
        /// <summary>Current zoom factor (1 = default view, larger = further out).</summary>
        public float ZoomLevel => zoom;
        [Tooltip("Start this far towards the back wall from the room centre, so the room interior fills the screen.")]
        public float StartDepth = 0.9f;
        Vector3 pivot, tPivot; float dist = 12f, zoom = 1f, tZoom = 1f, punch;
        Transform focus; float focusW, pYaw, tpYaw, pTilt, tpTilt, pZoom = 1, tpZoom = 1; Vector3 photoLook;

        void Awake() { I = this; Cam = GetComponent<Camera>(); LookDir.y = 0; LookDir.Normalize(); }
        public void SetArea(Bounds b, bool snap)
        {
            Area = b; tPivot = ClampPivot(tPivot == Vector3.zero ? b.center : tPivot);
            if (snap) { pivot = tPivot = ClampPivot(b.center); dist = FitDist(); }
        }
        /// <summary>Jump straight to a point (no easing), e.g. on first load.</summary>
        public void SnapTo(Vector3 p) { pivot = tPivot = ClampPivot(p + LookDir * StartDepth); dist = FitDist(); }
        public float FitHalfWidth = 3.5f;
        [Tooltip("Landscape: half of the visible height (Unity metres) at the default zoom.")] public float FitHalfHeight = 4.4f;
        /// <summary>Default distance: portrait fits about one room across the screen; landscape fits a room's depth top-to-bottom.</summary>
        float FitDist()
        {
            if (Cam.aspect >= 1f) return Mathf.Clamp(FitHalfHeight / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad), MinDist, MaxDist * 2f);
            float hFov = 2f * Mathf.Atan(Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * Mathf.Min(1f, Cam.aspect));
            return Mathf.Clamp(FitHalfWidth / Mathf.Tan(hFov * 0.5f), MinDist, MaxDist * 2f);
        }
        Vector3 ClampPivot(Vector3 p) { p.y = 0; p.x = Mathf.Clamp(p.x, Area.min.x, Area.max.x); p.z = Mathf.Clamp(p.z, Area.min.z, Area.max.z); return p; }
        Vector3 Right => Vector3.Cross(Vector3.up, LookDir).normalized;

        /// <summary>Drag from one screen point to another. The café moves 1:1 with the finger (no lag); in photo mode it orbits.</summary>
        public void Pan(Vector2 from, Vector2 to)
        {
            if (focus) { var d = to - from; float px = 1080f / Mathf.Max(400, Mathf.Min(Screen.width, Screen.height)); tpYaw = Mathf.Clamp(tpYaw + d.x * 0.25f * px, -MaxPhotoYaw, MaxPhotoYaw); tpTilt = Mathf.Clamp(tpTilt - d.y * 0.12f * px, -12f, 30f); return; }
            if (!FloorAt(from, out var a) || !FloorAt(to, out var b)) return;
            var move = a - b; move.y = 0;
            dragging = true; vel = Vector3.zero; flingVel = Vector3.zero;
            var before = tPivot; tPivot = ClampPivot(tPivot + move); pivot = tPivot;
            if (Time.deltaTime > 0f) dragVel = Vector3.Lerp(dragVel, (tPivot - before) / Time.deltaTime, 0.35f);
        }
        /// <summary>Old API (screen delta): treated as a drag ending at the current pointer.</summary>
        public void Pan(Vector2 d) { var p = UnityEngine.InputSystem.Pointer.current?.position.ReadValue() ?? new Vector2(Screen.width / 2f, Screen.height / 2f); Pan(p - d, p); }
        /// <summary>Finger lifted after a drag: keep gliding with the drag's speed, easing out like a phone scroll.</summary>
        public void EndPan() { dragging = false; flingVel = dragVel.magnitude > 0.6f ? Vector3.ClampMagnitude(dragVel, 40f) : Vector3.zero; dragVel = Vector3.zero; }
        /// <summary>Finger touched down: stop any glide.</summary>
        public void StopFling() { flingVel = Vector3.zero; dragVel = Vector3.zero; }
        Vector3 dragVel, flingVel, vel; bool dragging;
        bool FloorAt(Vector2 screen, out Vector3 p)
        {
            var ray = Cam.ScreenPointToRay(screen);
            if (ray.direction.y < -0.02f) { p = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); return true; }
            p = default; return false;
        }
        /// <summary>Double-tap: jump between the normal view and the zoomed-out overview of the whole café.</summary>
        public void ToggleOverview() { if (focus) return; tZoom = tZoom > 1.35f ? 1f : 1.8f; }
        public void Zoom(float f) { if (focus) tpZoom = Mathf.Clamp(tpZoom * f, 0.55f, 1.6f); else tZoom = Mathf.Clamp(tZoom * f, 0.6f, 1.8f); }
        public void Focus(Transform t)
        {
            if (!focus) photoLook = t.position + Vector3.up * 0.5f;
            if (focus != t)
            {
                // Swing round towards the cat's face (a three-quarter view, 20° off its nose) but stay within MaxPhotoYaw of the
                // open, cut-away café front so the camera never ends up behind a side or back wall.
                var want = -t.forward; want.y = 0;
                float yaw = Vector3.SignedAngle(LookDir, want.sqrMagnitude > 0.01f ? want.normalized : LookDir, Vector3.up);
                float lim = MaxPhotoYaw - 5f;
                tpYaw = Mathf.Clamp(yaw + (yaw > 0 ? -20f : 20f), -lim, lim); tpTilt = 0; tpZoom = 1;
            }
            focus = t; FindPhotoRoom(t.position);
        }
        public void Unfocus() => focus = null;
        // ---- keep the photo camera inside the cat's own room (side to side), so it never looks back through a divider wall
        bool hasRoom; float roomMinX, roomMaxX;
        void FindPhotoRoom(Vector3 p)
        {
            hasRoom = false;
            foreach (var r in CafeData.Rooms)
            {
                var go = GameObject.Find(r.Model); var rend = go ? go.GetComponent<Renderer>() : null; if (!rend) continue;
                var b = rend.bounds; if (p.x <= b.min.x || p.x >= b.max.x || p.z <= b.min.z || p.z >= b.max.z) continue;
                if (r.Id is "counter" or "nook")   // one wide room now (no wall between them)
                    foreach (var other in new[] { "Room_Counter", "Room_Nook" }) { var og = GameObject.Find(other); var orr = og ? og.GetComponent<Renderer>() : null; if (orr) b.Encapsulate(orr.bounds); }
                hasRoom = true; roomMinX = b.min.x + 0.9f; roomMaxX = b.max.x - 0.9f; return;   // keep clear of the side walls and front corner posts
            }
        }
        Vector3 PhotoPosFor(float yaw, out Quaternion prot)
        {
            var pf = Quaternion.AngleAxis(yaw, Vector3.up) * LookDir; var pr = Vector3.Cross(Vector3.up, pf).normalized;
            prot = Quaternion.LookRotation(Quaternion.AngleAxis(PhotoPitch + pTilt, pr) * pf, Vector3.up);
            return photoLook - prot * Vector3.forward * PhotoDist * pZoom;
        }
        bool InRoom(Vector3 camPos) => !hasRoom || (camPos.x >= roomMinX && camPos.x <= roomMaxX) || photoLook.x < roomMinX || photoLook.x > roomMaxX;
        [Tooltip("How far (degrees) the player can swing around a cat in photo mode, measured from the open café front.")]
        public float MaxPhotoYaw = 45f;
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        public void Punch() => punch = 1f;
        public void LookAtPoint(Vector3 p) { tPivot = ClampPivot(p); }
        public void SetZoom(float z) { tZoom = Mathf.Clamp(z, 0.6f, 1.8f); }
        /// <summary>Slide the view in screen directions (x = right, y = up), in metres — used while dragging a piece at a screen edge.</summary>
        public void Nudge(Vector2 screenDir) { tPivot = ClampPivot(tPivot + Right * screenDir.x + LookDir * screenDir.y); }

        void LateUpdate()
        {
            float dt = Time.deltaTime, k = 1f - Mathf.Exp(-9f * dt);
            if (flingVel.sqrMagnitude > 0.0004f && !focus && !dragging)
            {
                var before = tPivot; tPivot = ClampPivot(tPivot + flingVel * dt); pivot = tPivot;
                flingVel *= Mathf.Exp(-3.2f * dt); if ((tPivot - before).sqrMagnitude < 1e-8f) flingVel = Vector3.zero;   // stop at the edge
            }
            else flingVel = Vector3.zero;
            if (!dragging) pivot = Vector3.Lerp(pivot, tPivot, k);
            zoom = Mathf.Lerp(zoom, tZoom, 1f - Mathf.Exp(-12f * dt)); dist = FitDist() * zoom;
            pYaw = Mathf.Lerp(pYaw, tpYaw, k); pTilt = Mathf.Lerp(pTilt, tpTilt, k); pZoom = Mathf.Lerp(pZoom, tpZoom, k);
            focusW = Mathf.MoveTowards(focusW, focus ? 1f : 0f, dt * 2.2f); float w = Mathf.SmoothStep(0, 1, focusW);
            punch = Mathf.MoveTowards(punch, 0, dt * 2.2f);
            var rot = Quaternion.LookRotation(Quaternion.AngleAxis(Pitch, Right) * LookDir, Vector3.up);
            var cafePos = pivot - rot * Vector3.forward * dist;
            if (focus) photoLook = Vector3.Lerp(photoLook, focus.position + Vector3.up * 0.5f, 1f - Mathf.Exp(-5f * dt));
            if (focus)
            {   // swing back towards straight-on until the lens is inside the cat's room (cats move, the player drags, zoom changes)
                for (int i = 0; i < 40 && Mathf.Abs(tpYaw) > 0.01f && !InRoom(PhotoPosFor(tpYaw, out _)); i++) tpYaw = Mathf.MoveTowards(tpYaw, 0f, 2f);
                for (int i = 0; i < 40 && Mathf.Abs(pYaw) > 0.01f && !InRoom(PhotoPosFor(pYaw, out _)); i++) pYaw = Mathf.MoveTowards(pYaw, tpYaw, 2f);
                if (CafeDecorate.I && Time.frameCount % 15 == 0) FindPhotoRoom(focus.position);   // the cat may walk into another room
            }
            var photoPos = PhotoPosFor(pYaw, out var prot);
            var pos = Vector3.Lerp(cafePos, photoPos, w);
            var look = Vector3.Lerp(pivot, photoLook, w);
            transform.position = pos; transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            Cam.fieldOfView = Mathf.Lerp(Fov, PhotoFov, w) - 2f * Mathf.Sin(punch * Mathf.PI);
        }
    }
}
