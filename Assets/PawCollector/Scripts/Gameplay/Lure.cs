using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    /// <summary>The feather toy: drag a finger across the lawn and it dances there; playful cats give chase.</summary>
    public class Lure : MonoBehaviour
    {
        public static Lure I { get; private set; }
        public Material Mat;

        public bool Active { get; private set; }
        public Vector3 Position { get; private set; }
        public float Speed { get; private set; }

        Transform toy, feathers;
        MeshFilter stringMf; Mesh stringMesh;
        Vector3 target, vel, visual;
        float show, wiggle, bob;

        void Awake() { I = this; }

        void Start()
        {
            toy = new GameObject("FeatherToy").transform;
            feathers = new GameObject("Feathers").transform; feathers.SetParent(toy, false);
            var k = new MeshKit();
            string[] cols = { "#ff8fab", "#ffc94a", "#7fd1ae", "#c8a6f2" };
            for (int i = 0; i < 4; i++)
            {
                float a = (i - 1.5f) * 0.42f;
                var dir = new Vector3(Mathf.Sin(a), -Mathf.Cos(a) * 0.35f - 0.2f, Mathf.Cos(a) * 0.2f).normalized;
                var side = Vector3.Cross(dir, Vector3.forward).normalized;
                var c = MeshKit.Hex(cols[i]); var tip = Color.Lerp(c, Color.white, 0.35f);
                var rim = new List<Vector3>(); var rc = new List<Color>();
                for (int s = 0; s <= 10; s++)
                {
                    float t = s / 10f; float wv = Mathf.Sin(t * Mathf.PI) * 0.032f;
                    rim.Add(dir * (0.02f + t * 0.16f) + side * wv); rc.Add(Color.Lerp(c, tip, t));
                }
                for (int s = 10; s >= 0; s--)
                {
                    float t = s / 10f; float wv = Mathf.Sin(t * Mathf.PI) * 0.032f;
                    rim.Add(dir * (0.02f + t * 0.16f) - side * wv); rc.Add(Color.Lerp(c, tip, t));
                }
                k.Fan(Vector3.zero, c * 0.85f, rim, rc);
            }
            k.Spindle(new Vector3(0, -0.02f, 0), new Vector3(0, 0.05f, 0), 0.026f, MeshKit.Hex("#ffe2a8"), MeshKit.Hex("#e0a060"), 8, 0.6f);
            MeshKit.Obj("FeatherMesh", k.Build("Feathers"), Mat, feathers);
            stringMesh = new Mesh { name = "String" };
            var sgo = MeshKit.Obj("String", stringMesh, Mat, null);
            sgo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stringMf = sgo.GetComponent<MeshFilter>();
            toy.gameObject.SetActive(false); sgo.SetActive(false);
        }

        public void Begin(Vector3 p)
        {
            p = Clamp(p);
            Active = true; target = Position = visual = p; vel = Vector3.zero;
            toy.gameObject.SetActive(true); stringMf.gameObject.SetActive(true);
            AudioManager.I.Play("sfx_pop", 0.5f, 1.4f);
        }

        public void Move(Vector3 p) { if (Active) target = Clamp(p); }

        public void End() { Active = false; }

        public void Caught() { wiggle = 1f; }

        static Vector3 Clamp(Vector3 p)
        {
            if (GardenMap.I) p = GardenMap.I.Clamp(p);
            p.y = 0f; return p;
        }

        void Update()
        {
            if (!toy) return;
            show = Mathf.MoveTowards(show, Active ? 1f : 0f, Time.deltaTime * 5f);
            if (show <= 0f) { if (toy.gameObject.activeSelf) { toy.gameObject.SetActive(false); stringMf.gameObject.SetActive(false); } return; }

            // the toy lags the finger a little, like it's on a string
            var prev = Position;
            Position = Vector3.SmoothDamp(Position, target, ref vel, 0.09f);
            Speed = Mathf.Lerp(Speed, (Position - prev).magnitude / Mathf.Max(Time.deltaTime, 1e-4f), 0.3f);
            wiggle = Mathf.MoveTowards(wiggle, 0f, Time.deltaTime * 1.6f);
            bob += Time.deltaTime * (6f + Speed * 6f);

            float hover = 0.07f + 0.05f * Mathf.Abs(Mathf.Sin(bob)) * Mathf.Clamp01(Speed + 0.3f);
            visual = Position + Vector3.up * hover * show;
            toy.position = visual;
            toy.localScale = Vector3.one * (2.1f * show);
            var flatVel = vel; flatVel.y = 0;
            var tilt = flatVel.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flatVel) * Quaternion.Euler(Mathf.Clamp(flatVel.magnitude * 25f, 0, 35f), 0, 0) : Quaternion.identity;
            toy.rotation = Quaternion.Slerp(toy.rotation, tilt, Time.deltaTime * 8f);
            feathers.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 30f) * 25f * wiggle, Time.time * 40f, Mathf.Sin(bob * 0.7f) * 12f);

            // string from the toy up toward the camera (the wand is held off-screen)
            if (CameraRig.I)
            {
                var top = Vector3.Lerp(visual, CameraRig.I.transform.position, 0.45f) + Vector3.up * 0.4f;
                BuildString(visual + Vector3.up * 0.05f, top, Position - target);
            }
        }

        void BuildString(Vector3 a, Vector3 b, Vector3 sway)
        {
            const int segs = 12; const float r = 0.009f;
            var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>();
            var col = MeshKit.Hex("#fff3e2");
            var cam = CameraRig.I.transform;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = Vector3.Lerp(a, b, t) + sway * (Mathf.Sin(t * Mathf.PI) * 0.8f) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.06f;
                var side = Vector3.Cross((b - a).normalized, (cam.position - p).normalized).normalized * r;
                V.Add(p - side); V.Add(p + side); C.Add(col); C.Add(col);
                if (i < segs) { int k = i * 2; T.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3, k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            stringMesh.Clear(); stringMesh.SetVertices(V); stringMesh.SetColors(C); stringMesh.SetTriangles(T, 0);
            stringMesh.RecalculateNormals(); stringMesh.RecalculateBounds();
        }
    }
}
