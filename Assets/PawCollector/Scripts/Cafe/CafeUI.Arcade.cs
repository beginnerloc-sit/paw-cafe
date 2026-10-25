using System;
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
        VisualElement arcBg, arcade, arcStage, arcLives, arcTimePill, arcResult; Label arcScore, arcTime, arcCount, arcBanner, arcTitle;
        MiniGame game; Action<bool> gameDone; float countT, resultT; bool gameBegan, ptrDown; string lastHero;
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
            gameBegan = false; countT = 3.4f; resultT = -1; ptrDown = false;
            SetChrome(false); world.style.display = DisplayStyle.None; ShowCafeExtras(false);
            arcBg = Add(root, "layer arc-bg"); arcBg.style.backgroundColor = game.UsesWorld ? new Color(0, 0, 0, 0) : game.Bg; arcBg.pickingMode = PickingMode.Ignore;
            arcade = Add(root, "layer arcade"); arcade.pickingMode = PickingMode.Ignore;
            arcStage = Add(arcade, "layer arc-stage"); arcStage.pickingMode = PickingMode.Position;
            arcStage.RegisterCallback<PointerDownEvent>(e => { if (!GamePlaying) return; ptrDown = true; arcStage.CapturePointer(e.pointerId); game.Down(arcStage.WorldToLocal(e.position)); });
            arcStage.RegisterCallback<PointerMoveEvent>(e => { if (GamePlaying && ptrDown) game.Move(arcStage.WorldToLocal(e.position)); });
            arcStage.RegisterCallback<PointerUpEvent>(e => { if (arcStage.HasPointerCapture(e.pointerId)) arcStage.ReleasePointer(e.pointerId); ptrDown = false; if (GamePlaying) game.Up(arcStage.WorldToLocal(e.position)); });
            arcStage.RegisterCallback<PointerCancelEvent>(e => { ptrDown = false; if (GamePlaying) game.Up(arcStage.WorldToLocal(e.position)); });
            // HUD
            var hud = Add(arcade, "arc-hud"); hud.pickingMode = PickingMode.Ignore;
            var quit = new Button(() => EndGame()) { focusable = false }; quit.clicked += TapSfx; quit.AddToClassList("ibtn"); Img(Add(quit, "ibtn-ico"), Icon("ui_close")); hud.Add(quit);
            arcTitle = L(hud, mode == "stray" ? (id == "redlight" ? "Approach gently" : "Guide them home") : game.Title, "arc-title");
            var sp = Add(hud, "arc-pill"); Img(Add(sp, "arc-pill-ico"), Icon("ui_star")); arcScore = L(sp, "0", "arc-pill-val");
            arcTimePill = Add(hud, "arc-pill time"); L(arcTimePill, "Time", "arc-pill-lbl"); arcTime = L(arcTimePill, "", "arc-pill-val");
            arcLives = Add(hud, "arc-lives");
            arcBanner = L(arcade, "", "arc-banner"); arcBanner.pickingMode = PickingMode.Ignore;
            arcCount = L(arcade, "", "arc-count"); arcCount.pickingMode = PickingMode.Ignore;
            var how = L(arcade, game.HowTo, "arc-how"); how.pickingMode = PickingMode.Ignore; how.name = "arc-how";
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
            if (countT > 0)
            {
                float before = countT; countT -= dt;
                int n = Mathf.CeilToInt(countT - 0.4f); string txt = n >= 1 ? n.ToString() : "Go!";
                if (arcCount.text != txt) { arcCount.text = txt; AudioManager.I?.Play("sfx_tap", 0.5f, n >= 1 ? 1f : 1.5f); }
                float f = (countT - 0.4f) - Mathf.Floor(countT - 0.4f); arcCount.style.scale = new Scale(Vector3.one * (1f + f * 0.5f)); arcCount.style.opacity = countT > 0.05f ? 1f : 0f;
                if (countT <= 0) { arcCount.text = ""; var how = arcade.Q("arc-how"); if (how != null) how.style.display = DisplayStyle.None; }
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
            arcScore.text = game.Score.ToString("N0") + game.Unit;
            arcTimePill.style.display = game.TimeLeft >= 0 && game.TimeLeft < 90 ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.TimeLeft >= 0) { int t = Mathf.CeilToInt(game.TimeLeft); arcTime.text = $"{t / 60}:{t % 60:00}"; arcTimePill.EnableInClassList("low", game.TimeLeft < 5.5f); }
            arcLives.style.display = game.Lives >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.Lives >= 0)
            {
                if (arcLives.childCount != game.MaxLives) { arcLives.Clear(); for (int i = 0; i < game.MaxLives; i++) Img(Add(arcLives, "arc-heart"), Icon("ui_heart")); }
                int k = 0; foreach (var h in arcLives.Children()) h.EnableInClassList("off", k++ >= game.Lives);
            }
            arcBanner.text = game.Banner ?? ""; arcBanner.style.color = game.BannerCol;
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
            var card = Add(arcResult, "arc-result");
            L(card, stray ? (game.Won ? "Success!" : "Oh no…") : game.Won ? "Well played!" : "Nice try!", "modal-title");
            L(card, game.EndText ?? "", "modal-text");
            L(card, game.Score.ToString("N0") + game.Unit, "arc-big");
            if (!stray)
            {
                var sr = Add(card, "arc-stars");
                for (int i = 0; i < 3; i++)
                {
                    var s = Add(sr, "gstar big"); Img(s, Icon("ui_star")); bool on = i < stars; s.EnableInClassList("off", !on);
                    if (on) { s.style.scale = new Scale(Vector3.zero); int k = i; s.schedule.Execute(() => { s.experimental.animation.Start(0f, 1f, 260, (e, t) => e.style.scale = new Scale(Vector3.one * (t < 0.7f ? t / 0.7f * 1.25f : 1.25f - (t - 0.7f) / 0.3f * 0.25f))); AudioManager.I?.Play("sfx_coin", 0.5f, 1.1f + k * 0.15f); }).ExecuteLater(250 + i * 260); }
                }
                if (best) L(card, "New best!", "arc-best");
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
            AudioManager.I?.Play(game.Won ? "sfx_levelup" : "sfx_pop", 0.6f);
        }

        /// <summary>Close the game. <paramref name="report"/> = tell the stray flow how it went.</summary>
        void EndGame(bool report = false)
        {
            if (game == null) return;
            var g = game; var done = gameDone; game = null; gameDone = null;
            try { g.Cleanup(); } catch (Exception e) { Debug.LogException(e); }
            arcBg?.RemoveFromHierarchy(); arcade?.RemoveFromHierarchy(); arcBg = arcade = arcStage = arcResult = null;
            SetChrome(true); world.style.display = DisplayStyle.Flex; ShowCafeExtras(true);
            if (CafeCamera.I) CafeCamera.I.Cam.enabled = true;
            ApplySafeArea(); Refresh();
            if (report) done?.Invoke(g.Won);
        }
        void ShowCafeExtras(bool on) { if (reqStack != null) reqStack.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; if (!on && guide != null) guide.AddToClassList("hidden"); }
    }
}
