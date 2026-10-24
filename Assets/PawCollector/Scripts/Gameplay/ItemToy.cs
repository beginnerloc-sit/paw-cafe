using UnityEngine;

namespace PawCollector
{
    /// <summary>
    /// Makes a placed toy physical-feeling: the yarn ball rolls (and spins) when a cat bats or pounces on it,
    /// the feather wand twitches and skids. Toys stay within reach of their mat.
    /// </summary>
    public class ItemToy : MonoBehaviour
    {
        public Vector3 Home;              // mat centre
        public float Leash = 0.5f;       // max distance from the mat centre
        public bool Rolls = true;
        public float Radius = 0.13f;

        Transform pivot;                  // rotates around the ball's centre
        Vector3 vel;
        float twitch, twitchYaw;

        public Vector3 Position { get { var p = transform.position; p.y = 0; return p; } }

        void Start()
        {
            if (!Rolls) return;
            // re-parent the meshes under a pivot at the ball centre so rolling spins in place
            pivot = new GameObject("RollPivot").transform;
            pivot.SetParent(transform, false);
            pivot.position = transform.position + Vector3.up * Radius;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c != pivot) c.SetParent(pivot, true);
            }
            var mf = GetComponent<MeshFilter>();
            if (mf)   // mesh on the root itself: move it onto a child of the pivot
            {
                var child = new GameObject("Mesh");
                child.transform.SetParent(pivot, false);
                child.transform.position = transform.position; child.transform.rotation = transform.rotation; child.transform.localScale = Vector3.one;
                child.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var src = GetComponent<MeshRenderer>(); var dst = child.AddComponent<MeshRenderer>();
                dst.sharedMaterials = src.sharedMaterials; dst.shadowCastingMode = src.shadowCastingMode;
                Destroy(src); Destroy(mf);
            }
        }

        /// <summary>Give the toy a push along the ground (metres per second).</summary>
        public void Kick(Vector3 dir, float speed)
        {
            dir.y = 0; if (dir.sqrMagnitude < 1e-4f) return;
            vel += dir.normalized * speed;
            twitch = 1f; twitchYaw = Random.Range(-35f, 35f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (vel.sqrMagnitude > 1e-5f)
            {
                var step = vel * dt;
                var next = transform.position + step;
                // stay near the mat: soft wall at the leash
                var off = new Vector3(next.x - Home.x, 0, next.z - Home.z);
                if (off.magnitude > Leash) { next = Home + off.normalized * Leash; next.y = transform.position.y; vel = Vector3.Reflect(vel, -off.normalized) * 0.4f; }
                if (GardenMap.I && GardenMap.I.BlockedStatic(next, 0f)) vel = -vel * 0.4f;
                else transform.position = next;
                if (Rolls && pivot)
                {
                    var axis = Vector3.Cross(Vector3.up, step.normalized);
                    pivot.Rotate(axis, step.magnitude / Radius * Mathf.Rad2Deg, Space.World);
                }
                vel = Vector3.MoveTowards(vel, Vector3.zero, (Rolls ? 1.1f : 3f) * dt);
                if (GardenMap.I) { var p = transform.position; p.y = Mathf.MoveTowards(p.y, GardenMap.I.GroundY(p), dt * 0.5f); transform.position = p; }
            }
            else if (Rolls && pivot)
            {
                // at rest the ball rocks back so its loose strand lies on the ground again
                var settled = Quaternion.Euler(0, pivot.localEulerAngles.y, 0);
                pivot.localRotation = Quaternion.Slerp(pivot.localRotation, settled, dt * 2.5f);
            }
            if (!Rolls && twitch > 0f)
            {
                twitch = Mathf.MoveTowards(twitch, 0f, dt * 2.5f);
                transform.rotation *= Quaternion.Euler(0, twitchYaw * dt * 2f, 0);
            }
        }
    }
}
