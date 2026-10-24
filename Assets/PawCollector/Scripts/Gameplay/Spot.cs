using UnityEngine;

namespace PawCollector
{
    /// <summary>A garden mat that holds one item and seats one visiting cat.</summary>
    public class Spot : MonoBehaviour
    {
        public int Index;
        public float ItemScale = 1.9f;

        public string ItemId { get; private set; } = "";
        public int Uses { get; private set; }
        public bool Unlocked { get; private set; }
        public CatVisitor Occupant { get; set; }
        public bool IsFree => Unlocked && !string.IsNullOrEmpty(ItemId) && Occupant == null;

        GameObject itemVisual, lockVisual;
        Renderer mat;
        Vector3 baseScale;
        float highlight, popT = 1f;
        MaterialPropertyBlock mpb;

        void Awake()
        {
            mat = GetComponentInChildren<Renderer>();
            baseScale = transform.localScale;
            mpb = new MaterialPropertyBlock();
        }

        public void Init(bool unlocked, string item, int uses)
        {
            SetUnlocked(unlocked);
            SetItem(unlocked ? item : "", uses, false);
        }

        public void SetUnlocked(bool on)
        {
            Unlocked = on;
            if (!on && lockVisual == null)
            {
                lockVisual = new GameObject("Locked");
                lockVisual.transform.SetParent(transform, false);
            }
            if (mat) { mat.GetPropertyBlock(mpb); mpb.SetColor("_Tint", on ? Color.white : new Color(0.78f, 0.74f, 0.7f)); mat.SetPropertyBlock(mpb); }
            if (on && lockVisual) { Destroy(lockVisual); lockVisual = null; }
        }

        public void SetItem(string id, int uses, bool animate)
        {
            bool changed = id != ItemId;
            ItemId = id ?? ""; Uses = uses;
            if (changed)
            {
                if (itemVisual) Destroy(itemVisual);
                if (!string.IsNullOrEmpty(ItemId))
                {
                    var prefab = Resources.Load<GameObject>("Items/Item_" + ItemId);
                    if (prefab)
                    {
                        itemVisual = Instantiate(prefab, ItemPosition, Quaternion.LookRotation(-CameraRig.FlatForward, Vector3.up), transform.parent);
                        itemVisual.name = "Item_" + ItemId;
                        itemVisual.transform.localScale = Vector3.one * ItemScale;
                        if (animate) popT = 0f;
                        if (ItemId == "Yarn" || ItemId == "Wand")
                        {
                            var toy = itemVisual.AddComponent<ItemToy>();
                            toy.Home = transform.position; toy.Rolls = ItemId == "Yarn"; toy.Radius = 0.1f * ItemScale;
                        }
                    }
                }
            }
            GameManager.I?.NotifySpot(this);
        }

        bool SitsOnItem => ItemId == "Cushion" || ItemId == "Blanket" || ItemId == "Box";
        float Radius => 0.42f * transform.lossyScale.x;

        public Vector3 ItemPosition => SitsOnItem ? transform.position + Vector3.up * 0.03f
                                                  : transform.position - CameraRig.FlatRight * Radius * 0.42f + Vector3.up * 0.03f;

        public Vector3 SeatPosition
        {
            get
            {
                if (ItemId == "Cushion") return transform.position + Vector3.up * (0.03f + 0.11f * ItemScale);
                if (ItemId == "Blanket") return transform.position + Vector3.up * (0.03f + 0.06f * ItemScale);
                if (ItemId == "Box") return transform.position + Vector3.up * (0.03f + 0.012f * ItemScale);
                return transform.position + CameraRig.FlatRight * Radius * 0.38f + Vector3.up * 0.035f;
            }
        }

        public ItemToy Toy => itemVisual ? itemVisual.GetComponent<ItemToy>() : null;

        /// <summary>Where the item is right now (toys get batted around).</summary>
        public Vector3 LivePosition { get { var t = Toy; return t ? t.Position : ItemPosition; } }

        public void Highlight(bool on) => highlight = on ? 1f : 0f;

        void Update()
        {
            float pulse = highlight > 0 ? 1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f)) : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * pulse, Time.deltaTime * 12f);
            if (itemVisual && popT < 1f)
            {
                popT = Mathf.Min(1f, popT + Time.deltaTime * 2.5f);
                float s = EaseOutBack(popT);
                itemVisual.transform.localScale = Vector3.one * ItemScale * Mathf.Max(0.01f, s);
            }
        }

        static float EaseOutBack(float t) { const float c = 1.70158f; float q = t - 1f; return 1f + (c + 1f) * q * q * q + c * q * q; }
    }
}
