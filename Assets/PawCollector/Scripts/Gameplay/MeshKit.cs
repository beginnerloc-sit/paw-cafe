using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    /// <summary>Tiny helpers for small runtime vertex-coloured meshes (feather toy, butterflies).</summary>
    public class MeshKit
    {
        public readonly List<Vector3> V = new();
        public readonly List<Color> C = new();
        public readonly List<int> T = new();

        /// <summary>Triangle fan around v0, emitted double-sided (separate verts so normals stay clean).</summary>
        public void Fan(Vector3 centre, Color centreCol, IList<Vector3> rim, IList<Color> rimCol)
        {
            for (int side = 0; side < 2; side++)
            {
                int b = V.Count;
                V.Add(centre); C.Add(centreCol);
                for (int i = 0; i < rim.Count; i++) { V.Add(rim[i]); C.Add(rimCol[i]); }
                for (int i = 0; i < rim.Count - 1; i++)
                {
                    if (side == 0) { T.Add(b); T.Add(b + 1 + i); T.Add(b + 2 + i); }
                    else { T.Add(b); T.Add(b + 2 + i); T.Add(b + 1 + i); }
                }
            }
        }

        /// <summary>Closed spindle along +Z (body, feather quill, string segment).</summary>
        public void Spindle(Vector3 a, Vector3 b, float r, Color ca, Color cb, int sides = 6, float bulge = 1f)
        {
            var axis = b - a; var up = Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            var u = Vector3.Cross(axis, up).normalized; var w = Vector3.Cross(axis.normalized, u);
            int rings = 4; int b0 = V.Count;
            V.Add(a); C.Add(ca);
            for (int k = 1; k < rings; k++)
            {
                float t = k / (float)rings; float rr = r * Mathf.Pow(Mathf.Sin(t * Mathf.PI), bulge);
                for (int s = 0; s < sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    V.Add(a + axis * t + (u * Mathf.Cos(ang) + w * Mathf.Sin(ang)) * rr); C.Add(Color.Lerp(ca, cb, t));
                }
            }
            V.Add(b); C.Add(cb);
            int tip = V.Count - 1;
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                T.Add(b0); T.Add(b0 + 1 + s1); T.Add(b0 + 1 + s);
                for (int k = 0; k < rings - 2; k++)
                {
                    int r0 = b0 + 1 + k * sides, r1 = r0 + sides;
                    T.Add(r0 + s); T.Add(r0 + s1); T.Add(r1 + s);
                    T.Add(r1 + s); T.Add(r0 + s1); T.Add(r1 + s1);
                }
                int last = b0 + 1 + (rings - 2) * sides;
                T.Add(last + s); T.Add(last + s1); T.Add(tip);
            }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(V); m.SetColors(C); m.SetTriangles(T, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static GameObject Obj(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
