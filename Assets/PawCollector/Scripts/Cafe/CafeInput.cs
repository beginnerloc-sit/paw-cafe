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
            if (ShelterStage.I && ShelterStage.I.Showing) { down = false; g = G.None; return; }   // the shelter page owns the screen
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
            var ui = CafeUI.I;
            if (!p.press.isPressed && !p.press.wasReleasedThisFrame && ui) ui.ReleasePointer();
            if (p.press.wasPressedThisFrame)
            {
                if (ui && (ui.IsOverUI(pos) || ui.UIClaimedPress)) { down = false; g = G.None; return; }
                down = true; g = G.Pending; downPos = lastPos = pos; CafeCamera.I.StopFling();
                downCat = CafePhoto.Active || Decorating ? null : Pick<CafeCat>(pos);
                downFurn = Decorating ? Pick<FurnitureInstance>(pos) : null;
                if (downFurn && Floor(pos, out var fp)) moveOffset = downFurn.transform.position - fp;
            }
            if (!down) return;
            // UI Toolkit routed this press to a button/sheet after all: drop the café gesture so the tap doesn't pass through.
            if (ui && ui.UIClaimedPress && (g == G.Pending || g == G.Pan)) { down = false; g = G.None; downCat = null; downFurn = null; return; }
            if (p.press.isPressed && g != G.Pinch)
            {
                float moved = (pos - downPos).magnitude * Px; var delta = pos - lastPos;
                if (g == G.Pending && moved > Drag)
                {
                    if (downFurn) { if (downFurn != Selected) CafeDecorate.I.Select(downFurn); CafeDecorate.I.DragBegin(downFurn); g = G.Move; }   // press-and-drag any piece
                    else if (downCat) g = G.Pet;
                    else g = G.Pan;
                }
                if (g == G.Pan) CafeCamera.I.Pan(delta);
                else if (g == G.Pet && downCat) { if (delta.magnitude * Px > 4f) downCat.Pet(); }
                else if (g == G.Move && Selected && Floor(pos, out var fp)) CafeDecorate.I.Drag(Selected, Selected.Def.WallMounted ? fp : fp + moveOffset);
                lastPos = pos;
            }
            if (p.press.wasReleasedThisFrame)
            {
                if (g == G.Pending && !(ui && (ui.UIClaimedPress || ui.IsOverUI(pos)))) Tap(pos);
                if (g == G.Move && Selected) CafeDecorate.I.DragEnd(Selected);
                if (g == G.Pan) CafeCamera.I.EndPan();
                down = false; g = G.None; downCat = null; downFurn = null;
            }
        }

        void Tap(Vector2 pos)
        {
            if (CafePhoto.Active || CafeVisit.Active) return;
            if (Decorating) { var f = Pick<FurnitureInstance>(pos); CafeDecorate.I.Select(f); return; }
            var hit = PickAny(pos);
            if (hit is TipJar)
            {
                long n = CafeManager.I.CollectTips();
                if (n > 0) { Fx.Coins(CafeManager.I.TipJarPoint.position + Vector3.up * 0.4f, 14); CafeUI.I?.Float($"+{n}", CafeManager.I.TipJarPoint.position); }
                return;   // empty jar: nothing to say
            }
            if (hit is Stray stray) { CafeUI.I?.OpenStray(stray); return; }
            if (hit is RoomSign sign) { CafeUI.I?.OpenRoomUnlock(sign.Room); return; }
            var cat = hit as CafeCat;
            if (cat) { if (CafeManager.I.MoodOf(cat.Def.Id) is CafeManager.Mood.Sick or CafeManager.Mood.Hungry) CafeUI.I?.OpenCat(cat.Def.Id); else CafePhoto.I.Enter(cat); return; }
            var cust = hit as Customer;
            if (cust)
            {
                if (cust.Request != null) CafeManager.I.Say($"They'd love a photo of {cust.Request.Label}! Tap a cat to take it.");
                else if (cust.FavouriteLabel != null) CafeManager.I.Say($"This customer loves {cust.FavouriteLabel}");
                else CafeManager.I.Say(cust.Order != null ? $"Enjoying a {cust.Order.Label.ToLower()}" : "Just arrived");
            }
        }

        /// <summary>What the finger is on: the closest tappable thing along an exact ray; a slightly fat ray only if nothing was hit.</summary>
        Component PickAny(Vector2 screen)
        {
            var ray = CafeCamera.I.Cam.ScreenPointToRay(screen);
            foreach (float radius in new[] { 0f, 0.22f })
            {
                var hits = radius == 0f ? Physics.RaycastAll(ray, 60f) : Physics.SphereCastAll(ray, radius, 60f);
                Component best = null; float bd = float.MaxValue;
                foreach (var h in hits)
                {
                    Component c = h.collider.GetComponentInParent<CafeCat>(); if (!c) c = h.collider.GetComponentInParent<Stray>(); if (!c) c = h.collider.GetComponentInParent<Customer>();
                    if (!c) c = h.collider.GetComponentInParent<TipJar>(); if (!c) c = h.collider.GetComponentInParent<RoomSign>();
                    if (c && h.distance < bd) { bd = h.distance; best = c; }
                }
                if (best) return best;
            }
            return null;
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
