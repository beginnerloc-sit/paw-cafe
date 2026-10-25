using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using PawCollector.Cafe.Games;

namespace PawCollector.Cafe
{
    /// <summary>The Game Corner: a picker of repeatable minigames, a "who's playing?" cat chooser, the full-screen game
    /// host (countdown, score / timer / lives HUD, pointer forwarding) and the results card with stars and rewards.
    /// Also used by strays: some of their trust steps are minigames (PlayGame with mode "stray").</summary>
    public partial class CafeUI
    {
        VisualElement arcBg, arcade, arcStage, arcLives, arcTimePill, arcResult, arcScorePill, arcIntro, arcRing, arcVig, arcSBar, arcSFill; Label arcScore, arcTime, arcCount, arcBanner, arcTitle;
        readonly List<VisualElement> arcSStars = new();
        MiniGame game; Action<bool> gameDone; float countT, resultT, barShown, hudT; bool gameBegan, ptrDown; string lastHero, lastBanner; int hudScore, hudLives, hudStars;
        class UiBit { public VisualElement e; public Vector2 p, v; public float rot, spin, t, life, sway; }
        readonly List<UiBit> uiBits = new();
        public bool ArcadeActive => game != null;
        bool GamePlaying => game != null && gameBegan && countT <= 0 && !game.Over;
        const int FullRewardPlays = 5;   // per game per day; after that coins are reduced (no energy, no waiting)

        static long Today => DateTime.UtcNow.Date.Ticks / TimeSpan.TicksPerDay;
        GameStat Stat(string id, bool create)
        {
            var s = M.Data.games.Find(g => g.id == id);
            if (s == null && create) { s = new GameStat { id = id }; M.Data.games.Add(s); }
            if (s != null && s.day != Today) { s.day = Today; s.today = 0; }
            return s;
        }

        // ------------------------------------------------------------ picker
        public void OpenArcade()
        {
            Modal(m =>
            {
                m.AddToClassList("shop"); m.AddToClassList("arcade-modal");
                var head = Add(m, "shop-head"); L(head, "Game Corner", "shop-title");
                var coins = Add(head, "pill"); Img(Add(coins, "pill-ico"), Icon("ui_fish")); L(coins, M.Data.coins.ToString("N0"), "pill-val");
                Add(head, "grow");
                var x = new Button(CloseModal) { focusable = false }; x.clicked += TapSfx; x.AddToClassList("ibtn"); Img(Add(x, "ibtn-ico"), Icon("ui_close")); head.Add(x);
                L(m, "Play with your cats — earn coins and grow your friendship!", "arc-sub");
                var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                sv.AddToClassList("shop-scroll"); m.Add(sv);
                var grid = Add(sv.contentContainer, "gtiles");
                foreach (var g in MiniGames.All)
                {
                    bool locked = M.Data.level < g.Unlock; var st = Stat(g.Id, false); var gid = g.Id;
                    var tile = new Button(() => { if (locked) { Toast($"{g.Title} unlocks at café level {g.Unlock}"); return; } OpenHeroPick(gid); }) { focusable = false };
                    tile.clicked += TapSfx; tile.AddToClassList("gtile"); grid.Add(tile);
                    var pic = Add(tile, "gtile-pic"); pic.style.backgroundColor = g.TileCol; Img(Add(pic, "gtile-img"), Resources.Load<Texture2D>(g.IconPath));
                    L(tile, g.Title, "gtile-name");
                    var stars = Add(tile, "gtile-stars"); int n = st != null ? g.StarsFor(st.best) : 0;
                    for (int i = 0; i < 3; i++) { var s = Add(stars, "gstar"); Img(s, Icon("ui_star")); s.EnableInClassList("off", i >= n); }
                    L(tile, st != null && st.best > 0 ? $"Best {st.best:N0}{g.Unit}" : "Not played yet", "gtile-best");
                    if (locked) { tile.AddToClassList("locked"); LockPill(tile, g.Unlock); }
                }
                FitAll(m);
            });
        }

        void OpenHeroPick(string id)
        {
            var g = MiniGames.Make(id); var cats = M.Data.residents.ToList(); if (cats.Count == 0) cats.Add("Mochi");
            string pick = lastHero != null && cats.Contains(lastHero) ? lastHero : cats[0];
            Modal(m =>
            {
                m.AddToClassList("arc-pick");
                var top = Add(m, "arc-pick-top");
                var pic = Add(top, "gtile-pic big"); pic.style.backgroundColor = g.TileCol; Img(Add(pic, "gtile-img"), Resources.Load<Texture2D>(g.IconPath));
                var col = Add(top, "arc-pick-col"); L(col, g.Title, "modal-title left"); L(col, g.HowTo, "arc-howto");
                var goals = Add(col, "arc-goals");
                for (int i = 0; i < 3; i++) { var gp = Add(goals, "arc-goal"); for (int k = 0; k <= i; k++) Img(Add(gp, "gstar small"), Icon("ui_star")); L(gp, $"{g.Stars[i]:N0}{g.Unit}", "arc-goal-txt"); }
                var st = Stat(id, false);
                if (st != null && st.today >= FullRewardPlays) L(col, "Bonus coins are used up for today — still fun to play!", "arc-note");
                L(m, "Who's playing?", "arc-who");
                var row = Add(m, "arc-cats");
                foreach (var c in cats)
                {
                    var cid = c; var b = new Button(() => { pick = cid; foreach (var ch in row.Children()) ch.EnableInClassList("on", (string)ch.userData == pick); }) { focusable = false, userData = c };
                    b.clicked += TapSfx; b.AddToClassList("arc-cat"); b.EnableInClassList("on", c == pick); row.Add(b);
                    var ring = Add(b, "arc-cat-ring"); Img(Add(ring, "arc-cat-pic"), Icon("cat_" + c)); L(b, M.Data.Name(c), "arc-cat-name");
                }
                var br = Add(m, "arc-row");
                B(br, "Back", OpenArcade, "btn ghost arc-btn");
                B(br, "Play!", () => PlayGame(id, pick), "btn gold arc-btn");
            });
        }

        // ------------------------------------------------------------ host
        public void PlayGame(string id, string heroId, string mode = "normal", CatDef partner = null, Action<bool> done = null)
        {
            CloseModal(); if (CafePhoto.Active) CafePhoto.I.Exit(); if (tab != "Cafe") OpenTab("Cafe");
            if (game != null) EndGame();
            game = MiniGames.Make(id); if (game == null) return;
            game.Mode = mode; game.Hero = GameData.Cat(heroId) ?? GameData.Cat("Mochi"); game.Partner = partner; gameDone = done; lastHero = mode == "normal" ? heroId : lastHero;
            gameBegan = false; countT = 3.4f; resultT = -1; ptrDown = false; barShown = 0; hudScore = 0; hudLives = -1; hudStars = 0; lastBanner = null; uiBits.Clear(); arcSStars.Clear();
            SetChrome(false); world.style.display = DisplayStyle.None; ShowCafeExtras(false);
            arcBg = Add(root, "layer arc-bg"); arcBg.style.backgroundColor = game.UsesWorld || game.Is3D ? new Color(0, 0, 0, 0) : game.Bg; arcBg.pickingMode = PickingMode.Ignore;
            arcade = Add(root, "layer arcade"); arcade.pickingMode = PickingMode.Ignore;
            arcStage = Add(arcade, "layer arc-stage"); arcStage.pickingMode = PickingMode.Position;
            arcStage.RegisterCallback<PointerDownEvent>(e => { if (!GamePlaying) return; ptrDown = true; arcStage.CapturePointer(e.pointerId); game.Down(arcStage.WorldToLocal(e.position)); });
            arcStage.RegisterCallback<PointerMoveEvent>(e => { if (GamePlaying && ptrDown) game.Move(arcStage.WorldToLocal(e.position)); });
            arcStage.RegisterCallback<PointerUpEvent>(e => { if (arcStage.HasPointerCapture(e.pointerId)) arcStage.ReleasePointer(e.pointerId); ptrDown = false; if (GamePlaying) game.Up(arcStage.WorldToLocal(e.position)); });
            arcStage.RegisterCallback<PointerCancelEvent>(e => { ptrDown = false; if (GamePlaying) game.Up(arcStage.WorldToLocal(e.position)); });
            arcVig = Add(arcade, "layer arc-vig"); arcVig.pickingMode = PickingMode.Ignore; arcVig.style.display = DisplayStyle.None;
            var v2 = Add(arcVig, "layer arc-vig2"); v2.pickingMode = PickingMode.Ignore; Add(v2, "layer arc-vig3").pickingMode = PickingMode.Ignore;   // three soft rings = a glow
            // HUD
            var hud = Add(arcade, "arc-hud"); hud.pickingMode = PickingMode.Ignore;
            var quit = new Button(() => EndGame()) { focusable = false }; quit.clicked += TapSfx; quit.AddToClassList("ibtn"); Img(Add(quit, "ibtn-ico"), Icon("ui_close")); hud.Add(quit);
            arcTitle = L(hud, mode == "stray" ? (id == "redlight" ? "Approach gently" : "Guide them home") : game.Title, "arc-title");
            var sp = Add(hud, "arc-pill"); Img(Add(sp, "arc-pill-ico"), Icon("ui_star")); arcScore = L(sp, "0", "arc-pill-val"); arcScorePill = sp;
            arcTimePill = Add(hud, "arc-pill time"); L(arcTimePill, "Time", "arc-pill-lbl"); arcTime = L(arcTimePill, "", "arc-pill-val");
            arcLives = Add(hud, "arc-lives");
            // star goals bar under the HUD: fills as you score, each star pops when you pass it
            arcSBar = new VisualElement(); arcSBar.AddToClassList("arc-sbar"); hud.Insert(hud.IndexOf(sp), arcSBar); arcSBar.pickingMode = PickingMode.Ignore;
            var track = Add(arcSBar, "arc-sbar-track"); arcSFill = Add(track, "arc-sbar-fill");
            for (int i = 0; i < 3; i++) { var st = Add(arcSBar, "arc-sbar-star"); Img(st, Icon("ui_star")); st.style.left = Length.Percent(100f * game.Stars[i] / game.Stars[2]); arcSStars.Add(st); }
            if (mode == "stray") arcSBar.style.display = DisplayStyle.None;
            foreach (var q in arcSBar.Query<VisualElement>().ToList()) q.pickingMode = PickingMode.Ignore;
            arcBanner = L(arcade, "", "arc-banner"); arcBanner.pickingMode = PickingMode.Ignore;
            arcRing = Add(arcade, "arc-ring"); arcRing.pickingMode = PickingMode.Ignore;
            arcCount = L(arcade, "", "arc-count"); arcCount.pickingMode = PickingMode.Ignore;
            // intro card: the title ribbon drops in with a bounce, the how-to slides up under it
            arcIntro = Add(arcade, "arc-intro"); arcIntro.pickingMode = PickingMode.Ignore;
            var it = L(arcIntro, arcTitle.text, "arc-intro-title"); var ih = L(arcIntro, game.HowTo, "arc-intro-how");
            it.style.translate = new Translate(0, -500); ih.style.opacity = 0;
            it.experimental.animation.Start(0f, 1f, 620, (e, t) => e.style.translate = new Translate(0, -500 * (1 - BackOut(t))));
            ih.schedule.Execute(() => ih.experimental.animation.Start(0f, 1f, 360, (e, t) => { e.style.opacity = t; e.style.translate = new Translate(0, 40 * (1 - BackOut(t))); })).ExecuteLater(300);
            foreach (var q in hud.Query<VisualElement>().ToList()) if (q != quit && !quit.Contains(q)) q.pickingMode = PickingMode.Ignore;
            if (CafeCamera.I) CafeCamera.I.Cam.enabled = game.UsesWorld;
            ApplySafeArea();
            AudioManager.I?.Play("sfx_pop", 0.5f, 1.1f);
        }

        void TickArcade(float dt)
        {
            if (game == null) return;
            if (!gameBegan)
            {
                var sz = arcStage.layout.size; if (float.IsNaN(sz.x) || sz.x < 10) return;
                game.Begin(arcStage, sz.x, sz.y); gameBegan = true; ArcadeHud();
            }
            dt = Mathf.Min(dt, 0.05f);
            game.CountingDown = countT > 0;
            try { game.Frame(dt); } catch (Exception e) { Debug.LogException(e); }
            if (game == null) return;
            TickBits(dt);
            if (countT > 0)
            {
                countT -= dt;
                int n = Mathf.CeilToInt(countT - 0.4f); string txt = n >= 1 ? n.ToString() : "Go!";
                if (arcCount.text != txt && countT > 0)
                {
                    arcCount.text = txt; AudioManager.I?.Play(n >= 1 ? "sfx_tap" : "sfx_coin", 0.55f, n >= 1 ? 0.9f + (3 - n) * 0.12f : 1.3f);
                    arcCount.EnableInClassList("go", n < 1); bool go = n < 1;
                    arcCount.experimental.animation.Start(0f, 1f, 420, (e, t) => { float s = (go ? 2.2f : 1.9f) - (go ? 1.2f : 0.9f) * BackOut(t); e.style.scale = new Scale(new Vector3(s, s, 1)); e.style.rotate = new Rotate((1 - t) * (go ? -10 : 8)); e.style.opacity = Mathf.Clamp01(t * 3f); });
                    arcRing.experimental.animation.Start(0f, 1f, 600, (e, t) => { float s = 0.5f + 1.7f * t; e.style.scale = new Scale(new Vector3(s, s, 1)); e.style.opacity = 1f - t; });
                    if (go) { arcRing.AddToClassList("go"); SparkBurst(arcade, new Vector2(arcade.layout.width / 2f, arcade.layout.height / 2f), 18, 420f); }
                }
                if (countT <= 0)
                {
                    var c = arcCount; c.experimental.animation.Start(1f, 0f, 200, (e, t) => { e.style.opacity = t; e.style.scale = new Scale(Vector3.one * (1 + (1 - t) * 0.6f)); }).OnCompleted(() => c.text = "");
                    var intro = arcIntro; arcIntro = null;
                    intro?.experimental.animation.Start(0f, 1f, 260, (e, t) => { e.style.opacity = 1 - t; e.style.translate = new Translate(0, -120 * t * t); }).OnCompleted(() => intro.RemoveFromHierarchy());
                }
                return;
            }
            if (!game.Over) { game.Step(dt); ArcadeHud(); }
            if (!game.Over) return;
            // the game can also end from a tap (e.g. last heart lost on a cucumber), not only inside Step
            if (resultT < 0 && arcResult == null) { resultT = 1.0f; ptrDown = false; ArcadeHud(); }
            else if (resultT > 0) { resultT -= dt; if (resultT <= 0) ShowGameResults(); }
        }
        void ArcadeHud()
        {
            hudT += Time.unscaledDeltaTime;
            arcScore.text = game.Score.ToString("N0") + game.Unit;
            if (game.Score != hudScore) { if (game.Score > hudScore) Bounce(arcScorePill, 1.22f); hudScore = game.Score; }
            arcTimePill.style.display = game.TimeLeft >= 0 && game.TimeLeft < 90 ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.TimeLeft >= 0)
            {
                int t = Mathf.CeilToInt(game.TimeLeft); arcTime.text = $"{t / 60}:{t % 60:00}"; bool low = game.TimeLeft < 5.5f && !game.Over; arcTimePill.EnableInClassList("low", low);
                float s = low ? 1f + Mathf.Abs(Mathf.Sin(hudT * 7f)) * 0.1f : 1f; arcTimePill.style.scale = new Scale(new Vector3(s, s, 1));
            }
            arcLives.style.display = game.Lives >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.Lives >= 0)
            {
                if (arcLives.childCount != game.MaxLives) { arcLives.Clear(); for (int i = 0; i < game.MaxLives; i++) Img(Add(arcLives, "arc-heart"), Icon("ui_heart")); }
                int k = 0; foreach (var h in arcLives.Children())
                {
                    bool off = k >= game.Lives;
                    if (off && !h.ClassListContains("off") && hudLives >= 0)
                    {   // the heart that was just lost shakes, puffs and greys out
                        h.experimental.animation.Start(0f, 1f, 520, (e, t) => { e.style.translate = new Translate(Mathf.Sin(t * 40f) * 9f * (1 - t), 0); float sc = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f; e.style.scale = new Scale(new Vector3(sc, sc, 1)); });
                        Flash(new Color(1f, 0.3f, 0.35f, 0.35f));
                    }
                    h.EnableInClassList("off", off); k++;
                }
                hudLives = game.Lives;
            }
            // star goals bar
            barShown = Mathf.Lerp(barShown, game.Score, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            arcSFill.style.width = Length.Percent(Mathf.Clamp01(barShown / game.Stars[2]) * 100f);
            int stars = game.StarsFor(game.Score);
            for (int i = 0; i < arcSStars.Count; i++)
            {
                bool on = i < stars; var st = arcSStars[i];
                if (on && !st.ClassListContains("on") && game.Mode != "stray") { Bounce(st, 1.8f, 420); AudioManager.I?.Play("sfx_coin", 0.5f, 1.2f + i * 0.15f); SparkBurst(arcade, st.worldBound.center - arcade.worldBound.position, 10, 260f); }
                st.EnableInClassList("on", on);
            }
            hudStars = stars;
            // banner slams in whenever it changes
            var b = game.Banner ?? "";
            if (b != lastBanner)
            {
                lastBanner = b; arcBanner.text = b; arcBanner.style.color = game.BannerCol;
                if (b.Length > 0) arcBanner.experimental.animation.Start(0f, 1f, 380, (e, t) => { float s = 1.9f - 0.9f * BackOut(t); e.style.scale = new Scale(new Vector3(s, s, 1)); e.style.rotate = new Rotate(-6f * (1 - t)); e.style.opacity = Mathf.Clamp01(t * 4f); });
            }
            // danger vignette (e.g. "Red light!")
            arcVig.style.display = game.Danger && !game.Over ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.Danger) arcVig.style.opacity = 0.65f + Mathf.Sin(hudT * 9f) * 0.25f;
        }

        // ------------------------------------------------------------ juice helpers
        static float BackOut(float t) { t = Mathf.Clamp01(t); const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2); }
        static void Bounce(VisualElement e, float peak = 1.25f, int ms = 280)
        {
            e.experimental.animation.Start(0f, 1f, ms, (el, t) => { float s = 1f + (peak - 1f) * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.4f); el.style.scale = new Scale(new Vector3(s, s, 1)); }).OnCompleted(() => e.style.scale = new Scale(Vector3.one));
        }
        void Flash(Color c)
        {
            if (arcade == null) return;
            var f = Add(arcade, "layer arc-flash"); f.pickingMode = PickingMode.Ignore; f.style.backgroundColor = c;
            f.experimental.animation.Start(1f, 0f, 320, (e, t) => e.style.opacity = t).OnCompleted(() => f.RemoveFromHierarchy());
        }
        /// <summary>Little glowing dots flying out from a point (stars lighting up, "Go!").</summary>
        void SparkBurst(VisualElement layer, Vector2 at, int n, float speed)
        {
            if (layer == null) return;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n + UnityEngine.Random.Range(-0.2f, 0.2f), sp = speed * UnityEngine.Random.Range(0.6f, 1.1f);
                var e = Add(layer, "arc-spark"); e.pickingMode = PickingMode.Ignore; float sz = UnityEngine.Random.Range(8f, 16f); e.style.width = e.style.height = sz;
                var col = UnityEngine.Random.value < 0.5f ? new Color(1f, 0.9f, 0.45f) : Color.white; e.style.backgroundColor = col;
                e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = sz / 2;
                uiBits.Add(new UiBit { e = e, p = at, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp, life = 0.6f, spin = 0 });
            }
        }
        static readonly Color[] ConfettiCols = { new(1f, 0.45f, 0.62f), new(1f, 0.80f, 0.28f), new(0.45f, 0.78f, 1f), new(0.55f, 0.88f, 0.50f), new(0.78f, 0.60f, 1f), new(1f, 0.60f, 0.38f) };
        void UiConfetti(VisualElement layer, int n)
        {
            var size = layer.layout.size; if (float.IsNaN(size.x) || size.x < 10) size = new Vector2(1646, 760);
            for (int i = 0; i < n; i++)
            {
                var e = Add(layer, "arc-confetti"); e.pickingMode = PickingMode.Ignore;
                e.style.width = UnityEngine.Random.Range(12f, 20f); e.style.height = UnityEngine.Random.Range(7f, 11f); e.style.backgroundColor = ConfettiCols[i % ConfettiCols.Length];
                bool left = i % 2 == 0; var from = new Vector2(left ? -10 : size.x + 10, size.y * UnityEngine.Random.Range(0.55f, 0.85f));
                var v = new Vector2((left ? 1 : -1) * UnityEngine.Random.Range(380f, 900f), -UnityEngine.Random.Range(700f, 1250f));
                uiBits.Add(new UiBit { e = e, p = from, v = v, rot = UnityEngine.Random.Range(0, 360f), spin = UnityEngine.Random.Range(-720f, 720f), life = 4f, sway = UnityEngine.Random.Range(0, 6.28f) });
            }
        }
        void TickBits(float dt)
        {
            for (int i = uiBits.Count - 1; i >= 0; i--)
            {
                var b = uiBits[i]; b.t += dt;
                if (b.e.parent == null || b.t > b.life) { b.e.RemoveFromHierarchy(); uiBits.RemoveAt(i); continue; }
                if (b.spin != 0)
                {   // confetti: gravity, air drag and a flutter
                    b.v.y += 1500f * dt; b.v *= 1f - Mathf.Min(0.9f, 2.2f * dt); b.p += (b.v + new Vector2(Mathf.Sin(b.t * 6f + b.sway) * 60f, 0)) * dt; b.rot += b.spin * dt;
                    b.e.style.rotate = new Rotate(b.rot); b.e.style.scale = new Scale(new Vector3(Mathf.Cos(b.t * 8f + b.sway), 1, 1));
                }
                else { b.v *= 1f - Mathf.Min(0.9f, 4f * dt); b.p += b.v * dt; b.e.style.opacity = 1f - b.t / b.life; }
                b.e.style.left = b.p.x; b.e.style.top = b.p.y;
            }
        }

        void ShowGameResults()
        {
            bool stray = game.Mode == "stray"; var st = Stat(game.Id, !stray); int stars = game.StarsFor(game.Score);
            bool best = false; long coins = 0; int bond = 0;
            if (!stray)
            {
                best = game.Score > st.best && game.Score > 0; st.best = Mathf.Max(st.best, game.Score); st.plays++; st.today++;
                float daily = st.today <= FullRewardPlays ? 1f : 0.3f;
                coins = (long)Mathf.Round(game.Score * game.CoinsPerPoint * daily); if (game.Score > 0) coins = Math.Max(1, coins);
                if (coins > 0) M.AddCoins(coins);
                if (game.Hero != null && M.Data.residents.Contains(game.Hero.Id)) { var rec = M.Data.CatOrNew(game.Hero.Id); bond = stars + (game.Score > 0 ? 1 : 0); rec.bond = Mathf.Min(100, rec.bond + bond); M.Cheer(game.Hero.Id, 10f); }
                M.AddXp(2 + stars); M.Recalc(); M.Save();
            }
            arcResult = Add(arcade, "layer arc-dim");
            arcResult.style.left = arcResult.style.right = -120; arcResult.style.top = arcResult.style.bottom = -80;   // cover the safe-area margins too
            arcResult.style.opacity = 0; arcResult.experimental.animation.Start(0f, 1f, 250, (e, t) => e.style.opacity = t);
            var card = Add(arcResult, "arc-result");
            card.style.translate = new Translate(0, -900);
            card.experimental.animation.Start(0f, 1f, 560, (e, t) => { e.style.translate = new Translate(0, -900 * (1 - BackOut(t))); e.style.rotate = new Rotate((1 - t) * -4f); });
            L(card, stray ? (game.Won ? "Success!" : "Oh no…") : game.Won ? "Well played!" : "Nice try!", "modal-title");
            L(card, game.EndText ?? "", "modal-text");
            var big = L(card, "0" + game.Unit, "arc-big"); int finalScore = game.Score; string unit = game.Unit; int lastShown = -1;
            big.schedule.Execute(() => big.experimental.animation.Start(0f, 1f, 900, (e, t) =>
            {
                int v = Mathf.RoundToInt(finalScore * (1f - Mathf.Pow(1f - t, 3))); ((Label)e).text = v.ToString("N0") + unit;
                if (v != lastShown && Mathf.FloorToInt(t * 12) != Mathf.FloorToInt(Mathf.Max(0, t - 0.02f) * 12)) AudioManager.I?.Play("sfx_tap", 0.25f, 1f + t * 0.8f); lastShown = v;
            }).OnCompleted(() => Bounce(big, 1.3f, 320))).ExecuteLater(420);
            if (!stray)
            {
                var sr = Add(card, "arc-stars");
                for (int i = 0; i < 3; i++)
                {
                    var s = Add(sr, "gstar big"); Img(s, Icon("ui_star")); bool on = i < stars; s.EnableInClassList("off", !on);
                    if (on)
                    {
                        s.style.scale = new Scale(Vector3.zero); int k = i; var cardRef = card;
                        s.schedule.Execute(() =>
                        {   // each star flies in big, spins and lands with a sparkle burst
                            s.experimental.animation.Start(0f, 1f, 380, (e, t) => { float sc = 2.4f - 1.4f * BackOut(t); e.style.scale = new Scale(new Vector3(sc, sc, 1)); e.style.rotate = new Rotate((1 - t) * 200f); e.style.opacity = Mathf.Clamp01(t * 3f); });
                            AudioManager.I?.Play("sfx_coin", 0.55f, 1.1f + k * 0.18f);
                            s.schedule.Execute(() => SparkBurst(arcResult, s.worldBound.center - arcResult.worldBound.position, 12, 380f)).ExecuteLater(300);
                        }).ExecuteLater(1350 + i * 300);
                    }
                }
                if (best)
                {
                    var nb = L(card, "New best!", "arc-best"); nb.style.scale = new Scale(Vector3.zero);
                    nb.schedule.Execute(() => { nb.experimental.animation.Start(0f, 1f, 420, (e, t) => { float sc = BackOut(t); e.style.scale = new Scale(new Vector3(sc, sc, 1)); }); AudioManager.I?.Play("sfx_levelup", 0.4f, 1.3f); }).ExecuteLater(2300);
                    nb.schedule.Execute(() => nb.style.rotate = new Rotate(Mathf.Sin(Time.unscaledTime * 5f) * 5f)).Every(16).StartingIn(2700);
                }
                var rw = Add(card, "arc-rewards");
                var c1 = Add(rw, "arc-reward"); Img(Add(c1, "arc-reward-ico"), Icon("ui_fish")); L(c1, $"+{coins:N0}", "arc-reward-val");
                if (bond > 0) { var c2 = Add(rw, "arc-reward"); Img(Add(c2, "arc-reward-ico"), Icon("ui_heart")); L(c2, $"+{bond} {M.Data.Name(game.Hero.Id)}", "arc-reward-val"); }
                if (st.today > FullRewardPlays) L(card, "Bonus coins used up for today", "arc-note");
            }
            var row = Add(card, "arc-row");
            string id = game.Id, hero = game.Hero?.Id;
            if (stray) B(row, "Continue", () => EndGame(true), "btn gold arc-btn");
            else
            {
                B(row, "Games", () => { EndGame(); OpenArcade(); }, "btn ghost arc-btn");
                B(row, "Play again", () => { EndGame(); PlayGame(id, hero); }, "btn gold arc-btn");
            }
            FitAll(card);
            int bi = 0; foreach (var bt in row.Children().ToList())
            {   // buttons bounce in last
                bt.style.scale = new Scale(Vector3.zero); int delay = (stray ? 1300 : 2200) + bi++ * 120;
                bt.schedule.Execute(() => bt.experimental.animation.Start(0f, 1f, 380, (e, t) => { float sc = BackOut(t); e.style.scale = new Scale(new Vector3(sc, sc, 1)); })).ExecuteLater(delay);
            }
            if (game.Won || stars > 0) { var lay = arcResult; lay.schedule.Execute(() => UiConfetti(lay, 90)).ExecuteLater(stray ? 500 : 1300); }
            AudioManager.I?.Play(game.Won ? "sfx_levelup" : "sfx_pop", 0.6f);
        }

        /// <summary>Close the game. <paramref name="report"/> = tell the stray flow how it went.</summary>
        void EndGame(bool report = false)
        {
            if (game == null) return;
            var g = game; var done = gameDone; game = null; gameDone = null; uiBits.Clear(); arcSStars.Clear(); arcIntro = null;
            try { g.Cleanup(); } catch (Exception e) { Debug.LogException(e); }
            arcBg?.RemoveFromHierarchy(); arcade?.RemoveFromHierarchy(); arcBg = arcade = arcStage = arcResult = null;
            SetChrome(true); world.style.display = DisplayStyle.Flex; ShowCafeExtras(true);
            if (CafeCamera.I) CafeCamera.I.Cam.enabled = true;
            ApplySafeArea(); Refresh();
            if (report) done?.Invoke(g.Won);
            if (pendingLevel > 0) root.schedule.Execute(() => { if (!ArcadeActive && pendingLevel > 0) { int lv = pendingLevel; pendingLevel = 0; OnLevelUp(lv); } }).ExecuteLater(60);
        }
        void ShowCafeExtras(bool on) { if (reqStack != null) reqStack.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; if (!on && guide != null) guide.AddToClassList("hidden"); }
    }
}
