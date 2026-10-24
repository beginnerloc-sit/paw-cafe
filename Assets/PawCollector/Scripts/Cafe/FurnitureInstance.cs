using UnityEngine;

namespace PawCollector.Cafe
{

    /// <summary>A placed piece of furniture: chairs seat customers, perches seat cats.</summary>
    public class FurnitureInstance : MonoBehaviour
    {
        public FurnDef Def;
        public PlacedFurn Placed;
        public Customer Occupant;
        public CafeCat[] PerchTaken;
        public bool SeatForward = true;     // sitter faces the chair's local +Z

        void Start() { PerchTaken = new CafeCat[Def?.Perches.Length ?? 0]; }

        public Vector3 SeatPoint => transform.position;
        public Quaternion SeatRotation => transform.rotation;

        public Vector3 PerchPos(int i) => transform.TransformPoint(Def.Perches[i].Pos);
        public Quaternion PerchRot(int i) => transform.rotation * Quaternion.Euler(0, Def.Perches[i].Yaw, 0);

        public int FreePerch(CafeCat who)
        {
            if (PerchTaken == null || PerchTaken.Length != Def.Perches.Length) PerchTaken = new CafeCat[Def.Perches.Length];
            for (int i = 0; i < PerchTaken.Length; i++) if (!PerchTaken[i] || PerchTaken[i] == who) return i;
            return -1;
        }
        public void Release(CafeCat who) { if (PerchTaken == null) return; for (int i = 0; i < PerchTaken.Length; i++) if (PerchTaken[i] == who) PerchTaken[i] = null; }

        public void SavePlacement()
        {
            if (Placed == null) return;
            var p = transform.position; Placed.x = p.x; Placed.y = p.y; Placed.z = p.z; Placed.yaw = transform.eulerAngles.y;
        }
    }
}
