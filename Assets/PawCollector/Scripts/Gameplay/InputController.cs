using UnityEngine;
using UnityEngine.InputSystem;

namespace PawCollector
{
    /// <summary>
    /// Touch on the 3D garden:
    ///  • tap a cat → photo mode            • stroke a cat → petting
    ///  • drag the lawn → look around        • pinch / mouse wheel → zoom
    ///  • press &amp; hold the lawn, then drag → feather toy
    ///  • tap a mat → place / inspect items  • in photo mode: drag to orbit the cat, pinch to zoom
    /// </summary>
    public class InputController : MonoBehaviour
    {
        enum Gesture { None, Pending, Camera, Lure, Pet, Pinch }
        public const float HoldForToy = 0.3f;
        const float DragThreshold = 22f;   // panel units

        Gesture gesture;
        Vector2 downPos, lastPos;
        float downTime, pinchDist;
        bool down;
        CatVisitor downCat;

        float PxScale => 1080f / Mathf.Max(1, Screen.width);

        void Update()
        {
            var p = Pointer.current;
            if (p == null) return;
            if (!GameManager.I.Playing) { Cancel(); return; }

            // mouse wheel zoom (editor / desktop)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && !(UIController.I && UIController.I.IsOverUI(mouse.position.ReadValue())))
                    CameraRig.I.Zoom(wheel > 0 ? 0.92f : 1.08f);
            }

            // two-finger pinch zoom
            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 1 && ts.touches[0].press.isPressed && ts.touches[1].press.isPressed)
            {
                float d = (ts.touches[0].position.ReadValue() - ts.touches[1].position.ReadValue()).magnitude;
                if (gesture != Gesture.Pinch) { EndCurrent(); gesture = Gesture.Pinch; pinchDist = d; down = true; }
                else if (d > 1f && pinchDist > 1f) { CameraRig.I.Zoom(pinchDist / d); pinchDist = d; }
                return;
            }

            var pos = p.position.ReadValue();
            if (p.press.wasPressedThisFrame)
            {
                if (UIController.I && UIController.I.IsOverUI(pos)) return;
                down = true; gesture = Gesture.Pending; downPos = lastPos = pos; downTime = Time.unscaledTime;
                downCat = PhotoMode.Active ? null : PickCat(pos);
            }
            if (!down) return;

            if (p.press.isPressed && gesture != Gesture.Pinch)
            {
                float moved = (pos - downPos).magnitude * PxScale;
                float held = Time.unscaledTime - downTime;
                var delta = (pos - lastPos) * PxScale;

                if (gesture == Gesture.Pending)
                {
                    if (PhotoMode.Active) { if (moved > DragThreshold * 0.5f) gesture = Gesture.Camera; }
                    else if (downCat)
                    {
                        if (moved > DragThreshold) { if (downCat.CanPet) { gesture = Gesture.Pet; downCat.PetBegin(); } else gesture = Gesture.Camera; }
                    }
                    else if (moved > DragThreshold && held < HoldForToy) gesture = Gesture.Camera;
                    else if (held >= HoldForToy && moved <= DragThreshold && Ground(pos, out var g0))
                    {
                        gesture = Gesture.Lure; Lure.I.Begin(g0); AudioManager.Haptic();
                        if (UIController.I && UIController.I.ShopOpen) UIController.I.ShowShop(false);
                    }
                }

                switch (gesture)
                {
                    case Gesture.Camera: CameraRig.I.Orbit(delta.x, delta.y); break;
                    case Gesture.Pet: if (PickCat(pos) == downCat) downCat.PetStroke(delta.magnitude); break;
                    case Gesture.Lure: if (Ground(pos, out var g)) Lure.I.Move(g); break;
                }
                lastPos = pos;
            }

            if (p.press.wasReleasedThisFrame)
            {
                if (gesture == Gesture.Pending && !PhotoMode.Active) HandleTap(pos);
                Cancel();
            }
        }

        void EndCurrent()
        {
            if (gesture == Gesture.Pet && downCat) downCat.PetRelease();
            if (gesture == Gesture.Lure && Lure.I) Lure.I.End();
        }

        void Cancel()
        {
            EndCurrent();
            gesture = Gesture.None; down = false; downCat = null;
        }

        static CatVisitor PickCat(Vector2 pos)
        {
            var ray = CameraRig.I.Cam.ScreenPointToRay(pos);
            // generous pick: spherecast so small cats are easy to hit on a phone
            foreach (var hit in Physics.SphereCastAll(ray, 0.14f, 100f))
            {
                var cat = hit.collider.GetComponentInParent<CatVisitor>();
                if (cat && cat.InGarden) return cat;
            }
            return null;
        }

        static bool Ground(Vector2 pos, out Vector3 point)
        {
            var ray = CameraRig.I.Cam.ScreenPointToRay(pos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            point = default; return false;
        }

        void HandleTap(Vector2 pos)
        {
            if (UIController.I && UIController.I.ShopOpen) { UIController.I.ShowShop(false); return; }   // tap the garden to close the shop
            var cat = PickCat(pos);
            if (cat) { PhotoMode.I.Enter(cat); return; }
            var ray = CameraRig.I.Cam.ScreenPointToRay(pos);
            if (Physics.SphereCast(ray, 0.08f, out var hit, 100f))
            {
                var spot = hit.collider.GetComponentInParent<Spot>();
                if (spot) UIController.I.OnSpotTapped(spot);
            }
        }
    }
}
