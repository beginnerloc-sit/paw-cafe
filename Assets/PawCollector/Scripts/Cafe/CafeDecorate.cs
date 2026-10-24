using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>
    /// Decorate mode: buy furniture, press-and-drag any piece (snapped, never overlapping), rotate, store it away.
    /// While dragging, the piece lifts with a soft shadow under it (red when the spot isn't allowed).
    /// Wall pieces (shelf, photo frames) snap onto the nearest real wall of the room — back or side — and face out from it.
    /// </summary>
    public class CafeDecorate : MonoBehaviour
    {
        public static CafeDecorate I { get; private set; }
        Vector3 lastValid; Quaternion lastValidRot; Renderer[] selRends; bool dragging; float baseY;
        const float Lift = 0.18f;
        SpriteRenderer shadow;
        readonly List<MeshCollider> roomCols = new();
        void Awake() { I = this; }
        void Start()
        {
            // Room shells get mesh colliders so wall pieces can find the real inner wall surfaces (the street-side Front_* walls are excluded).
            foreach (var r in CafeData.Rooms)
            {
                var go = GameObject.Find(r.Model); if (!go || !go.GetComponent<MeshFilter>()) continue;
                var mc = go.GetComponent<MeshCollider>(); if (!mc) mc = go.AddComponent<MeshCollider>(); roomCols.Add(mc);
            }
            shadow = new GameObject("DragShadow").AddComponent<SpriteRenderer>();
            shadow.sprite = BlobSprite(); shadow.transform.rotation = Quaternion.Euler(90, 0, 0); shadow.sortingOrder = -1; shadow.enabled = false;
        }

        static Sprite BlobSprite()
        {
            const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                t.SetPixel(x, y, new Color(0, 0, 0, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>Default mounting height and how far the pivot sits out from the wall surface, per wall piece.</summary>
        static float WallHeight(FurnDef d) => d.Id == "Shelf" ? 2.21f : 2.84f;
        static float WallOffset(FurnDef d) => d.Id == "Shelf" ? 0.14f : 0.04f;

        /// <summary>Nearest inner wall (back, left or right) of the room around <paramref name="floor"/>.</summary>
        public bool WallSnap(FurnDef def, Vector3 floor, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity; float best = float.MaxValue; bool found = false;
            var o = new Vector3(floor.x, WallHeight(def), floor.z);
            foreach (var dir in new[] { Vector3.back, Vector3.left, Vector3.right, Vector3.forward })
                foreach (var mc in roomCols)
                    if (mc && mc.Raycast(new Ray(o, dir), out var h, 4f) && h.distance < best && Vector3.Dot(h.normal, -dir) > 0.7f)
                    {
                        best = h.distance; found = true;
                        var n = h.normal; n.y = 0; n.Normalize();
                        pos = h.point + n * WallOffset(def); rot = Quaternion.LookRotation(n);
                        // slide along the wall on the snap grid
                        var along = Vector3.Cross(Vector3.up, n); float s = Mathf.Round(Vector3.Dot(pos, along) * 4f) / 4f;
                        pos += along * (s - Vector3.Dot(pos, along));
                    }
            return found;
        }

        public void SetMode(bool on)
        {
            if (!on) Select(null);
            CafeInput.I.Decorating = on;
        }

        public bool Buy(string id)
        {
            var def = CafeData.Furn(id); var cm = CafeManager.I;
            if (def == null) return false;
            if (cm.Data.level < def.Unlock) { cm.Say($"Unlocks at café level {def.Unlock}"); return false; }
            if (!cm.Spend(def.Cost)) return false;
            cm.TutorialDone(2);
            var pivot = CafeCamera.I ? CafeCamera.I.transform.position + CafeCamera.I.transform.forward * 10f : Vector3.zero;
            var p = FindSpot(def, pivot); float yaw = 180f;
            if (def.WallMounted) { yaw = 0f; var floor = CafeCamera.I ? CafeCamera.I.Area.center : Vector3.zero; if (CafeInput.Floor(new Vector2(Screen.width / 2f, Screen.height * 0.6f), out var fp)) floor = fp;
                if (FindWallSpot(def, floor, out var wp, out var wr)) { p = wp; yaw = wr.eulerAngles.y; } }
            var room = RoomAt(p) ?? "counter";
            var pf = new PlacedFurn { id = id, x = p.x, y = p.y, z = p.z, yaw = yaw, room = room };
            cm.Data.furniture.Add(pf);
            var fi = cm.Spawn(pf);
            cm.Recalc(); if (def.Beds > 0) cm.MoveInWaiting(); if (id == "PhotoFrame") CafePhoto.I.RefreshFrames();
            AudioManager.I?.Play("sfx_place"); cm.Save();
            SetMode(true); Select(fi); return true;
        }

        Vector3 FindSpot(FurnDef def, Vector3 near)
        {
            near.y = 0;
            for (int ring = 0; ring < 30; ring++)
                for (int k = 0; k < 12; k++)
                {
                    var p = Snap(near + Quaternion.Euler(0, k * 30 + ring * 7, 0) * Vector3.forward * ring * 0.35f);
                    if (def.WallMounted) p.y = 2.6f;
                    if (Valid(def, p, null)) return p;
                }
            return Snap(near);
        }

        bool FindWallSpot(FurnDef def, Vector3 near, out Vector3 pos, out Quaternion rot)
        {
            for (int ring = 0; ring < 20; ring++)
                for (int k = 0; k < 8; k++)
                {
                    var q = near + Quaternion.Euler(0, k * 45 + ring * 11, 0) * Vector3.forward * ring * 0.4f;
                    if (WallSnap(def, q, out pos, out rot) && ValidWall(def, pos, null)) return true;
                }
            pos = default; rot = default; return false;
        }

        static Vector3 Snap(Vector3 p) { p.x = Mathf.Round(p.x * 4) / 4f; p.z = Mathf.Round(p.z * 4) / 4f; return p; }

        public string RoomAt(Vector3 p)
        {
            foreach (var r in CafeData.Rooms)
            {
                var go = GameObject.Find(r.Model); if (!go) continue; var b = go.GetComponent<Renderer>().bounds;
                if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z) return r.Id;
            }
            return null;
        }

        bool ValidWall(FurnDef def, Vector3 p, FurnitureInstance self)
        {
            var room = RoomAt(p); if (room == null || !CafeManager.I.RoomOpen(room)) return false;
            foreach (var f in CafeManager.I.Furniture)
                if (f && f != self && f.Def.WallMounted && Vector3.Distance(f.transform.position, p) < (def.Radius + f.Def.Radius) * 1.6f) return false;
            return true;
        }

        public bool Valid(FurnDef def, Vector3 p, FurnitureInstance self)
        {
            if (def.WallMounted) return ValidWall(def, p, self);
            var room = RoomAt(p); if (room == null || !CafeManager.I.RoomOpen(room)) return false;
            var b = GameObject.Find(CafeData.Room(room).Model).GetComponent<Renderer>().bounds;
            float m = def.WallMounted ? 0.2f : def.Radius + 0.25f;
            if (p.x < b.min.x + m || p.x > b.max.x - m || p.z < b.min.z + m || p.z > b.max.z - m)
                if (!def.WallMounted) return false;
            if (def.Id == "Rug" || def.WallMounted) return true;
            foreach (var f in CafeManager.I.Furniture)
            {
                if (!f || f == self || f.Def.Id == "Rug" || f.Def.WallMounted) continue;
                float need = def.Radius + f.Def.Radius;
                if ((def.Id == "Chair" && f.Def.Table) || (def.Table && f.Def.Id == "Chair")) need *= 0.62f;   // chairs tuck in at tables
                if (Vector3.Distance(CafeManager.Flat(f.transform.position), CafeManager.Flat(p)) < need * 0.92f) return false;
            }
            return true;
        }

        public void Select(FurnitureInstance f)
        {
            Tint(false); CafeInput.I.Selected = f;
            if (f) { lastValid = f.transform.position; lastValidRot = f.transform.rotation; selRends = f.GetComponentsInChildren<Renderer>(); }
            CafeUI.I?.ShowFurnitureTools(f);
        }

        public void DragBegin(FurnitureInstance f)
        {
            if (!f || Busy(f)) return;
            dragging = true; baseY = f.transform.position.y; lastValid = f.transform.position; lastValidRot = f.transform.rotation;
            AudioManager.I?.Play("sfx_pop", 0.4f, 1.25f);
        }
        public void Drag(FurnitureInstance f, Vector3 p)
        {
            if (Busy(f)) { CafeManager.I.Say("Someone is using that right now"); return; }
            if (!dragging) DragBegin(f);
            bool ok;
            if (f.Def.WallMounted)
            {
                ok = WallSnap(f.Def, p, out var wp, out var wr);
                if (ok) { f.transform.SetPositionAndRotation(wp, wr); ok = ValidWall(f.Def, wp, f); }
                else { p.y = f.transform.position.y; f.transform.position = new Vector3(p.x, f.transform.position.y, p.z); }
            }
            else
            {
                p = Snap(p); var basePos = new Vector3(p.x, baseY, p.z);
                ok = Valid(f.Def, basePos, f);
                f.transform.position = basePos + Vector3.up * Lift;   // lifted while held
            }
            Tint(!ok);
            if (ok) { lastValid = new Vector3(f.transform.position.x, f.Def.WallMounted ? f.transform.position.y : baseY, f.transform.position.z); lastValidRot = f.transform.rotation; }
            ShowShadow(f, ok);
        }
        public void DragEnd(FurnitureInstance f)
        {
            dragging = false; shadow.enabled = false;
            var drop = f.Def.WallMounted ? f.transform.position : new Vector3(f.transform.position.x, baseY, f.transform.position.z);
            if (!Valid(f.Def, drop, f)) f.transform.SetPositionAndRotation(lastValid, lastValidRot);
            else f.transform.position = drop;
            Tint(false); if (f.Def.Seats > 0) FaceTable(f); f.Placed.room = RoomAt(f.transform.position) ?? f.Placed.room; f.SavePlacement(); AudioManager.I?.Play("sfx_place"); CafeManager.I.Save();
        }
        void ShowShadow(FurnitureInstance f, bool ok)
        {
            if (f.Def.WallMounted) { shadow.enabled = false; return; }
            shadow.enabled = true; float size = Mathf.Max(0.6f, f.Def.Radius * 2.6f);
            shadow.transform.position = new Vector3(f.transform.position.x, baseY + 0.02f, f.transform.position.z);
            shadow.transform.localScale = new Vector3(size, size, 1f);
            shadow.color = ok ? new Color(0.22f, 0.12f, 0.08f, 0.6f) : new Color(0.9f, 0.15f, 0.15f, 0.65f);
        }
        public void Rotate(FurnitureInstance f)
        {
            if (!f || Busy(f)) return;
            f.transform.Rotate(0, 90, 0); f.SavePlacement(); CafeManager.I.Save();
        }
        public void Store(FurnitureInstance f)
        {
            if (!f || Busy(f)) { CafeManager.I.Say("Someone is using that right now"); return; }
            var cm = CafeManager.I;
            if (f.Def.Seats > 0 && cm.Seats <= 1) { cm.Say("Keep at least one seat for customers"); return; }
            if (f.Def.Beds > 0 && cm.Beds - f.Def.Beds < cm.Data.residents.Count) { cm.Say("A cat sleeps there — add another bed first"); return; }
            cm.AddCoins(f.Def.Cost / 2); Select(null); cm.Remove(f); if (f.Def.Id == "PhotoFrame") CafePhoto.I.RefreshFrames(); cm.Save();
        }
        /// <summary>Chairs turn to face the nearest table so customers sit looking at their drink.</summary>
        public static void FaceTable(FurnitureInstance chair)
        {
            var t = CafeManager.I.TableNear(chair.transform.position); if (!t) return;
            var d = t.transform.position - chair.transform.position; d.y = 0; if (d.sqrMagnitude < 0.01f) return;
            chair.transform.rotation = Quaternion.LookRotation(d.normalized);
        }
        static bool Busy(FurnitureInstance f) => f.Occupant || (f.PerchTaken != null && f.PerchTaken.Any(c => c));

        void Tint(bool bad)
        {
            if (selRends == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in selRends) { if (!r) continue; r.GetPropertyBlock(mpb); mpb.SetColor("_Tint", bad ? new Color(1f, 0.55f, 0.55f) : Color.white); r.SetPropertyBlock(mpb); }
        }
    }
}
