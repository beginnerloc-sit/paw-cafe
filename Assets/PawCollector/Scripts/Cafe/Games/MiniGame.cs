using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe.Games
{
    /// <summary>A sprite on the minigame stage: an absolutely-positioned UI Toolkit element driven by Pos / Size / Rot.</summary>
    public sealed class Spr
    {
        public readonly VisualElement E;
        public Vector2 Pos, Size, Vel; public float Rot, Scale = 1f; public int Kind; public object Tag;
        public Spr(VisualElement parent, Texture2D tex, Vector2 pos, Vector2 size, string cls = null)
        {
            E = new VisualElement(); E.AddToClassList("gspr"); if (cls != null) foreach (var c in cls.Split(' ')) E.AddToClassList(c);
            E.pickingMode = PickingMode.Ignore; if (tex) E.style.backgroundImage = new StyleBackground(tex);
            parent.Add(E); Pos = pos; Size = size; Sync();
        }
        public void Sync()
        {
            E.style.left = Pos.x - Size.x * 0.5f; E.style.top = Pos.y - Size.y * 0.5f; E.style.width = Size.x; E.style.height = Size.y;
            E.style.rotate = new Rotate(Rot); E.style.scale = new Scale(new Vector3(Scale, Scale, 1f));
        }
        public void Tex(Texture2D t) => E.style.backgroundImage = t ? new StyleBackground(t) : StyleKeyword.None;
        public void Tint(Color c) => E.style.unityBackgroundImageTintColor = c;
        public void Kill() => E.RemoveFromHierarchy();
        public bool Alive => E.parent != null;
        /// <summary>Circle-ish overlap using a fraction of each sprite's smaller side.</summary>
        public bool Hits(Spr o, float k = 0.38f) => (Pos - o.Pos).magnitude < (Mathf.Min(Size.x, Size.y) + Mathf.Min(o.Size.x, o.Size.y)) * k;
        public bool HitsRect(Rect r, float k = 0.38f)
        {
            float rad = Mathf.Min(Size.x, Size.y) * k; var c = new Vector2(Mathf.Clamp(Pos.x, r.xMin, r.xMax), Mathf.Clamp(Pos.y, r.yMin, r.yMax));
            return (c - Pos).sqrMagnitude < rad * rad;
        }
    }

    /// <summary>
    /// Base class for every Game Corner minigame. The host (CafeUI.Arcade) gives it a stage (a full-screen VisualElement),
    /// calls Setup once, then Tick every frame and forwards pointer events in stage coordinates. A game sets Score, Lives,
    /// TimeLeft and finally calls Finish(). Units: U = stage height / 10, so games look the same on every screen.
    /// </summary>
    public abstract class MiniGame
    {
        // ---- catalogue
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string HowTo { get; }
        public abstract string IconPath { get; }          // Resources path of the tile picture
        public virtual int Unlock => 1;                    // café level
        public abstract int[] Stars { get; }               // score needed for 1, 2, 3 stars
        public virtual string Unit => "";
        public virtual Color Bg => new(0.78f, 0.89f, 1f);
        public virtual Color TileCol => new(1f, 0.86f, 0.91f);
        public virtual bool UsesWorld => false;            // draws over the 3D café instead of its own background
        public virtual float CoinsPerPoint => 1f;

        // ---- runtime state read by the host
        public int Score; public int Lives = -1; public int MaxLives = 3; public float TimeLeft = -1f;
        public bool Over { get; private set; }
        public bool Won { get; private set; }
        public string EndText { get; private set; }
        public string Mode = "normal";                     // "normal" or "stray"
        public CatDef Hero, Partner;                       // Partner = the stray in stray mode
        public string Banner;                              // optional big centre text the host shows (e.g. "Red light!")
        public Color BannerCol = Color.white;
        public bool Danger;                                // host shows a pulsing red edge (e.g. "Red light!")
        public bool CountingDown;                          // set by the host during the 3-2-1
        public virtual bool Is3D => false;                 // renders its own 3D set with its own camera (see Game3D)
        /// <summary>Called every frame by the host once the game has begun — also during the countdown and behind the results.</summary>
        public virtual void Frame(float dt) { }

        protected VisualElement S; protected float W, H, U; protected float T;
        static readonly Dictionary<string, Texture2D> cache = new();
        protected static Texture2D Tex(string path) { if (!cache.TryGetValue(path, out var t) || !t) cache[path] = t = Resources.Load<Texture2D>(path); return t; }
        protected static Texture2D G(string n) => Tex("Games/" + n);
        protected Texture2D CatTex(CatDef c) => Tex("Icons/cat_" + (c != null ? c.Id : "Mochi"));
        protected static float Rnd(float a, float b) => UnityEngine.Random.Range(a, b);

        public void Begin(VisualElement stage, float w, float h)
        {
            S = stage; W = w; H = h; U = h / 10f; T = 0; Score = 0; Over = false; Won = false; EndText = null; Banner = null; Danger = false;
            Setup();
        }
        public void Step(float dt) { if (Over) return; T += dt; if (TimeLeft >= 0) { TimeLeft = Mathf.Max(0, TimeLeft - dt); } Tick(dt); if (!Over && TimeLeft == 0f) TimeUp(); }
        protected abstract void Setup();
        protected abstract void Tick(float dt);
        protected virtual void TimeUp() => Finish(Score >= Stars[0], "Time's up!");
        public virtual void Down(Vector2 p) { }
        public virtual void Move(Vector2 p) { }
        public virtual void Up(Vector2 p) { }
        public virtual void Cleanup() { }

        protected void Finish(bool won, string text) { if (Over) return; Over = true; Won = won; EndText = text; Banner = null; }
        protected void LoseLife(string why = null)
        {
            if (Lives < 0) return; Lives--; Sfx("sfx_error", 0.5f);
            if (Lives <= 0) Finish(Score >= Stars[0], why ?? "Out of lives!");
        }
        public int StarsFor(int score) { int n = 0; foreach (var s in Stars) if (score >= s) n++; return n; }

        // ---- helpers
        protected Spr Sprite(Texture2D t, Vector2 pos, Vector2 size, string cls = null, VisualElement parent = null) => new(parent ?? S, t, pos, size, cls);
        protected VisualElement Box(Rect r, Color c, float radius = 0f, VisualElement parent = null, string cls = null)
        {
            var e = new VisualElement(); e.AddToClassList("gbox"); if (cls != null) e.AddToClassList(cls); e.pickingMode = PickingMode.Ignore;
            e.style.left = r.x; e.style.top = r.y; e.style.width = r.width; e.style.height = r.height; e.style.backgroundColor = c;
            if (radius > 0) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius; }
            (parent ?? S).Add(e); return e;
        }
        protected static void Place(VisualElement e, Rect r) { e.style.left = r.x; e.style.top = r.y; e.style.width = r.width; e.style.height = r.height; }
        /// <summary>A floating "+5" style label that drifts up and fades.</summary>
        protected void Pop(string text, Vector2 at, Color col, float size = 1f)
        {
            var l = new Label(text); l.AddToClassList("gpop"); l.pickingMode = PickingMode.Ignore; l.style.color = col; l.style.fontSize = 34 * size;
            l.style.left = at.x - 200; l.style.top = at.y - 30; S.Add(l);
            l.experimental.animation.Start(0f, 1f, 800, (e, t) => { e.style.translate = new Translate(0, -70 * t); e.style.opacity = 1f - t * t; }).OnCompleted(() => l.RemoveFromHierarchy());
        }
        protected static void Sfx(string n, float v = 0.6f, float p = 1f) => AudioManager.I?.Play(n, v, p);
        protected static Color Hex(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);
    }

    /// <summary>All Game Corner games, in display order.</summary>
    public static class MiniGames
    {
        static readonly Func<MiniGame>[] makers =
        {
            () => new FishFlight(), () => new Pounce(), () => new ZoomiesCatch(), () => new RedLightGreenLight(), () => new CatStack(),
            () => new LaserChase(), () => new LatteArt(), () => new RooftopRun(), () => new YarnPop(), () => new HideAndSeek(),
        };
        static List<MiniGame> all;
        public static IReadOnlyList<MiniGame> All { get { if (all == null) { all = new(); foreach (var m in makers) all.Add(m()); } return all; } }
        public static MiniGame Make(string id) { foreach (var m in makers) { var g = m(); if (g.Id == id) return g; } return null; }
    }
}
