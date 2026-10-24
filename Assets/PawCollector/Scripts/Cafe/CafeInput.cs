using UnityEngine;
using UnityEngine.InputSystem;

namespace PawCollector.Cafe
{

    /// <summary>
    /// Touch on the café: drag = pan, pinch / wheel = zoom, tap tip jar = collect, tap cat = photo mode, stroke a cat = pet,
    /// tap a stray = befriend, tap a locked-room sign = unlock. Decorate mode: drag furniture to move it.
    /// </summary>
    public class CafeInput : MonoBehaviour
    {
        public static CafeInput I { get; private set; }
        public bool Decorating;
        public FurnitureInstance Selected;
        enum G { None, Pending, Pan, Pet, Move, Pinch }
        G g; Vector2 downPos, lastPos; float pinchDist; bool down; CafeCat downCat; FurnitureInstance downFurn; Vector3 moveOffset;
        const float Drag = 18f;
        float Px => 1080f / Mathf.Max(1, Screen.width);
        void Awake() { I = this; }

        void Update()
        {
            var p = Pointer.current; if (p == null) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float w = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(w) > 0.01f && !(CafeUI.I && CafeUI.I.IsOverUI(mouse.position.ReadValue()))) CafeCamera.I.Zoom(w > 0 ? 0.92f : 1.08f);
            }
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 1 && ts.touches[0].press.isPressed && ts.touches[1].press.isPressed)
            {
                float d = (ts.touches[0].position.ReadValue() - ts.touches[1].position.ReadValue()).magnitude;
                if (g != G.Pinch) { g = G.Pinch; pinchDist = d; down = true; }
                else if (d > 1 && pinchDist > 1) { CafeCamera.I.Zoom(pinchDist / d); pinchDist = d; }
                return;
            }
            var pos = p.position.ReadValue();
            if (p.press.wasPressedThisFrame)
            {
                if (CafeUI.I && CafeUI.I.IsOverUI(pos)) return;
                down = true; g = G.Pending; downPos = lastPos = pos;
                downCat = CafePhoto.Active || Decorating ? null : Pick<CafeCat>(pos);
                downFurn = Decorating ? Pick<FurnitureInstance>(pos) : null;
                if (downFurn && Floor(pos, out var fp)) moveOffset = downFurn.transform.position - fp;
            }
            if (!down) return;
            if (p.press.isPressed && g != G.Pinch)
            {
                float moved = (pos - downPos).magnitude * Px; var delta = pos - lastPos;
                if (g == G.Pending && moved > Drag)
                {
                    if (downFurn && downFurn == Selected) g = G.Move;
                    else if (downCat) g = G.Pet;
                    else g = G.Pan;
                }
                if (g == G.Pan) CafeCamera.I.Pan(delta);
                else if (g == G.Pet && downCat) { if (delta.magnitude * Px > 4f) downCat.Pet(); }
                else if (g == G.Move && Selected && Floor(pos, out var fp)) CafeDecorate.I.Drag(Selected, fp + moveOffset);
                lastPos = pos;
            }
            if (p.press.wasReleasedThisFrame)
            {
                if (g == G.Pending) Tap(pos);
                if (g == G.Move && Selected) CafeDecorate.I.DragEnd(Selected);
                down = false; g = G.None; downCat = null; downFurn = null;
            }
        }

        void Tap(Vector2 pos)
        {
            if (CafePhoto.Active) return;
            if (Decorating) { var f = Pick<FurnitureInstance>(pos); CafeDecorate.I.Select(f); return; }
            if (Pick<TipJar>(pos)) { long n = CafeManager.I.CollectTips(); if (n > 0) { Fx.Coins(CafeManager.I.TipJarPoint.position + Vector3.up * 0.4f, 14); CafeUI.I?.Float($"+{n}", CafeManager.I.TipJarPoint.position); } else CafeManager.I.Say("The tip jar is empty — customers pay when they leave"); return; }
            var stray = Pick<Stray>(pos); if (stray) { CafeUI.I?.OpenStray(stray); return; }
            var sign = Pick<RoomSign>(pos); if (sign) { CafeUI.I?.OpenRoomUnlock(sign.Room); return; }
            var cat = Pick<CafeCat>(pos); if (cat) { CafePhoto.I.Enter(cat); return; }
            var cust = Pick<Customer>(pos);
            if (cust)
            {
                if (cust.Request != null) CafeManager.I.Say($"They'd love a photo of {cust.Request.Label}! Tap a cat to take it.");
                else if (cust.FavouriteLabel != null) CafeManager.I.Say($"This customer loves {cust.FavouriteLabel}");
                else CafeManager.I.Say(cust.Order != null ? $"Enjoying a {cust.Order.Label.ToLower()}" : "Just arrived");
            }
        }

        T Pick<T>(Vector2 screen) where T : Component
        {
            var ray = CafeCamera.I.Cam.ScreenPointToRay(screen);
            var hits = Physics.SphereCastAll(ray, 0.25f, 60f);
            T best = null; float bd = float.MaxValue;
            foreach (var h in hits) { var c = h.collider.GetComponentInParent<T>(); if (c && h.distance < bd) { bd = h.distance; best = c; } }
            return best;
        }
        public static bool Floor(Vector2 screen, out Vector3 p)
        {
            var ray = CafeCamera.I.Cam.ScreenPointToRay(screen);
            if (ray.direction.y < -0.01f) { p = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); return true; }
            p = default; return false;
        }
    }
}
