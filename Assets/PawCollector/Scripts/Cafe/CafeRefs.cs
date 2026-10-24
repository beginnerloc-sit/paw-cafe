using UnityEngine;

namespace PawCollector.Cafe
{
    /// <summary>Shared materials / prefabs the café needs at runtime (set up by the builder).</summary>
    public class CafeRefs : MonoBehaviour
    {
        public static CafeRefs I { get; private set; }
        public Material Prop, Locked, Ghost, GhostBad, Glass;
        void Awake() { I = this; }
        public static GameObject Prop3D(string name) => Resources.Load<GameObject>("Props/" + name);
    }
}
