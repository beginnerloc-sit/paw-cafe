using System.Linq;
using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Decorate mode: buy furniture, drag it around (snapped, never overlapping), rotate, store it away.</summary>
    public class CafeDecorate : MonoBehaviour
    {
        public static CafeDecorate I { get; private set; }
        Vector3 lastValid; float lastValidY; Renderer[] selRends; Material[][] selMats; bool selBad;
        void Awake() { I = this; }

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
            var p = FindSpot(def, pivot);
            var room = RoomAt(p) ?? "counter";
            var pf = new PlacedFurn { id = id, x = p.x, y = p.y, z = p.z, yaw = 180f, room = room };
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

        public bool Valid(FurnDef def, Vector3 p, FurnitureInstance self)
        {
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
            if (f) { lastValid = f.transform.position; selRends = f.GetComponentsInChildren<Renderer>(); }
            CafeUI.I?.ShowFurnitureTools(f);
        }

        public void Drag(FurnitureInstance f, Vector3 p)
        {
            if (Busy(f)) { CafeManager.I.Say("Someone is using that right now"); return; }
            p = Snap(p);
            if (f.Def.WallMounted) { p.y = f.transform.position.y; p.z = f.transform.position.z; }
            else p.y = f.transform.position.y;
            f.transform.position = p;
            bool ok = Valid(f.Def, p, f); Tint(!ok);
            if (ok) lastValid = p;
        }
        public void DragEnd(FurnitureInstance f)
        {
            if (!Valid(f.Def, f.transform.position, f)) f.transform.position = lastValid;
            Tint(false); if (f.Def.Seats > 0) FaceTable(f); f.Placed.room = RoomAt(f.transform.position) ?? f.Placed.room; f.SavePlacement(); AudioManager.I?.Play("sfx_place"); CafeManager.I.Save();
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
