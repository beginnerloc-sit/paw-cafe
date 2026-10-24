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
        enum G { None, Pending, Pan, Pet, Move, Pinch, Ghost }
        Vector3 ghostOffset; bool ghostMoved;
        /// <summary>Where the last pet happened on screen (the UI pops hearts there).</summary>
        public static Vector2 LastPetScreen; float stroke, lastEmptyTap = -1f; Vector2 lastEmptyPos;
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
#if UNITY_EDITOR
            var kb = Keyboard.current;   // editor testing (the Device Simulator can't pinch)
            if (kb != null) { if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) CafeCamera.I.Zoom(1f - Time.deltaTime * 1.5f); if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) CafeCamera.I.Zoom(1f + Time.deltaTime * 1.5f); }
#endif
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
                if (CafeDecorate.I && CafeDecorate.I.Placing)
                {   // placing a new piece: the whole screen drags the ghost (relative, so it never jumps under the finger)
                    g = G.Ghost; ghostMoved = false;
                    ghostOffset = Floor(pos, out var gp) ? CafeDecorate.I.GhostPos - gp : Vector3.zero; ghostOffset.y = 0;
                    return;
                }
                downCat = Decorating ? null : CafePhoto.Active ? PhotoCatUnder(pos) : Pick<CafeCat>(pos);   // in camera mode only the cat you are photographing can be stroked
                downFurn = Decorating ? Pick<FurnitureInstance>(pos) : null;
                if (downFurn && Floor(pos, out var fp)) moveOffset = downFurn.transform.position - fp;
            }
            if (!down) return;
            // UI Toolkit routed this press to a button/sheet after all: drop the café gesture so the tap doesn't pass through.
            if (ui && ui.UIClaimedPress && (g == G.Pending || g == G.Pan)) { down = false; g = G.None; downCat = null; downFurn = null; return; }
            if (g == G.Ghost)
            {
                var dc = CafeDecorate.I; if (!dc || !dc.Placing) { down = false; g = G.None; return; }
                if (p.press.isPressed)
                {
                    if ((pos - downPos).magnitude * Px > Drag) ghostMoved = true;
                    if (ghostMoved && Floor(pos, out var fp)) dc.MoveGhost(fp + ghostOffset);
                    // near a screen edge: slide the café along so the piece can travel further
                    float ex = Screen.width * 0.08f, ey = Screen.height * 0.1f; var edge = Vector2.zero;
                    if (pos.x < ex) edge.x = -1; else if (pos.x > Screen.width - ex) edge.x = 1;
                    if (pos.y < ey) edge.y = -1; else if (pos.y > Screen.height - ey) edge.y = 1;
                    if (ghostMoved && edge != Vector2.zero) CafeCamera.I.Nudge(edge * 5f * Time.deltaTime);
                }
                if (p.press.wasReleasedThisFrame)
                {
                    if (!ghostMoved && !(ui && ui.IsOverUI(pos)) && Floor(pos, out var tp)) dc.MoveGhost(tp);   // tap = put it here
                    down = false; g = G.None;
                }
                return;
            }
            if (p.press.isPressed && g != G.Pinch)
            {
                float moved = (pos - downPos).magnitude * Px; var delta = pos - lastPos;
                if (g == G.Pending && moved > Drag)
                {
                    if (downFurn) { if (downFurn != Selected) CafeDecorate.I.Select(downFurn); CafeDecorate.I.DragBegin(downFurn); g = G.Move; }   // press-and-drag any piece
                    else if (downCat) { g = G.Pet; stroke = 0f; LastPetScreen = pos; downCat.Pet(); }
                    else g = G.Pan;
                }
                if (g == G.Pan) CafeCamera.I.Pan(lastPos, pos);
                else if (g == G.Pet && downCat) { stroke += delta.magnitude * Px; if (stroke > 55f) { LastPetScreen = pos; if (downCat.Pet()) stroke = 0f; } }   // a pet every ~55 px of stroking
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
            if (CafePhoto.Active) { var pc = PhotoCatUnder(pos); if (pc) { LastPetScreen = pos; pc.Pet(); } return; }   // a tap on the cat = a gentle pat
            if (CafeVisit.Active) return;
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
            if (hit == null)
            {   // double-tap on an empty spot: zoom out to see the whole café / back in
                if (Time.unscaledTime - lastEmptyTap < 0.38f && (pos - lastEmptyPos).magnitude * Px < 60f) { CafeCamera.I.ToggleOverview(); lastEmptyTap = -1f; }
                else { lastEmptyTap = Time.unscaledTime; lastEmptyPos = pos; }
                return;
            }
            var cust = hit as Customer;
            if (cust)
            {
                if (cust.Request != null) CafeManager.I.Say($"They'd love a photo of {cust.Request.Label}! Tap a cat to take it.");
                else if (cust.FavouriteLabel != null) CafeManager.I.Say($"This customer loves {cust.FavouriteLabel}");
                else CafeManager.I.Say(cust.Order != null ? $"Enjoying a {cust.Order.Label.ToLower()}" : "Just arrived");
            }
        }

        /// <summary>What the finger is on: the closest tappable thing along an exact ray; a slightly fat ray only if nothing was hit.</summary>
        /// <summary>Generous finger radius in screen pixels (about 7.5% of the short side of the screen).</summary>
        static float TouchRadius => Mathf.Max(60f, Mathf.Min(Screen.width, Screen.height) * 0.075f);
        /// <summary>Where a thing "is" on screen: the middle of its visible body.</summary>
        static bool ScreenCenter(Component c, out Vector2 sp, out float size)
        {
            sp = default; size = 0;
            var rs = c.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return false;
            Bounds b = default; bool any = false;
            foreach (var r in rs) { if (!r.enabled || r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any) return false;
            var cam = CafeCamera.I.Cam; var p = cam.WorldToScreenPoint(b.center); if (p.z <= 0) return false;
            var top = cam.WorldToScreenPoint(b.center + Vector3.up * b.extents.y);
            sp = p; size = Mathf.Abs(top.y - p.y); return true;
        }
        /// <summary>What the finger is on: an exact hit wins; otherwise the nearest tappable thing on screen within the touch radius
        /// (cats and strays are favoured slightly — they're small).</summary>
        Component PickAny(Vector2 screen)
        {
            var ray = CafeCamera.I.Cam.ScreenPointToRay(screen);
            Component best = null; float bd = float.MaxValue;
            foreach (var h in Physics.RaycastAll(ray, 80f))
            {
                Component c = h.collider.GetComponentInParent<CafeCat>(); if (!c) c = h.collider.GetComponentInParent<Stray>(); if (!c) c = h.collider.GetComponentInParent<Customer>();
                if (!c) c = h.collider.GetComponentInParent<TipJar>(); if (!c) c = h.collider.GetComponentInParent<RoomSign>();
                if (c && h.distance < bd) { bd = h.distance; best = c; }
            }
            if (best) return best;
            float bestScore = float.MaxValue, R = TouchRadius;
            void Consider(Component c, float bias)
            {
                if (!c || !c.gameObject.activeInHierarchy || !ScreenCenter(c, out var sp, out var size)) return;
                float d = Vector2.Distance(sp, screen) - size * 0.6f;   // big things are easier to hit anywhere on them
                if (d < R && d * bias < bestScore) { bestScore = d * bias; best = c; }
            }
            foreach (var c in CafeManager.I.Cats) Consider(c, 0.8f);
            foreach (var s in FindObjectsByType<Stray>()) Consider(s, 0.8f);
            foreach (var c in CafeManager.I.Customers) Consider(c, 1.2f);
            foreach (var t in FindObjectsByType<TipJar>()) Consider(t, 1f);
            foreach (var r in FindObjectsByType<RoomSign>()) Consider(r, 1.1f);
            return best;
        }
        /// <summary>Furniture under (or nearest to) the finger in decorate mode — wall pieces and big pieces count from anywhere on them.</summary>
        FurnitureInstance PickFurniture(Vector2 screen)
        {
            var ray = CafeCamera.I.Cam.ScreenPointToRay(screen);
            FurnitureInstance best = null; float bd = float.MaxValue;
            foreach (var h in Physics.RaycastAll(ray, 80f)) { var f = h.collider.GetComponentInParent<FurnitureInstance>(); if (f && f.Def.Id != "Rug" && h.distance < bd) { bd = h.distance; best = f; } }
            if (best) return best;
            float bestD = float.MaxValue, R = TouchRadius * 0.8f;
            foreach (var f in CafeManager.I.Furniture)
            {
                if (!f || !f.gameObject.activeInHierarchy || !ScreenCenter(f, out var sp, out var size)) continue;
                float d = Vector2.Distance(sp, screen) - size * 0.5f + (f.Def.Id == "Rug" ? R * 0.6f : 0f);   // rugs lose ties to things standing on them
                if (d < R && d < bestD) { bestD = d; best = f; }
            }
            return best;
        }
        CafeCat PhotoCatUnder(Vector2 screen) { var c = PickAny(screen) as CafeCat; return c && c == CafePhoto.I.Cat ? c : null; }
        T Pick<T>(Vector2 screen) where T : Component
        {
            if (typeof(T) == typeof(FurnitureInstance)) return PickFurniture(screen) as T;
            return PickAny(screen) as T;
        }
        public static bool Floor(Vector2 screen, out Vector3 p)
        {
            var ray = CafeCamera.I.Cam.ScreenPointToRay(screen);
            if (ray.direction.y < -0.01f) { p = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y); return true; }
            p = default; return false;
        }
    }
}
