using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector
{
    /// <summary>All screens and the HUD, built in code on a UIDocument with Resources/UI/PawCollector.uss.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIController : MonoBehaviour
    {
        public static UIController I { get; private set; }

        VisualElement shop, shopIcon; Button shopBtn; Label shopLabel;
        VisualElement root, hud, bubbles, dock, coach, coachIcon, toast, flash, polaroidWrap, polaroid;
        VisualElement titleScreen, albumScreen, recapScreen, levelScreen, settingsScreen, catScreen;
        Label fishLabel, goldLabel, levelLabel, coachText, toastText, photoName, photoPose, photoGift, photoBadge, albumCount, recapText, levelTitle;
        VisualElement photoImg, photoStars, albumGrid, albumFill, levelLines;
        // viewfinder
        VisualElement vf, vfFill, vfStars, vfHearts; Label vfName, vfPose, vfShots; Button vfShutter;
        // cat detail
        VisualElement catPhoto, catHearts, catPoses; Label catName, catInfo, catBond;
        RingElement ring;
        readonly Dictionary<string, VisualElement> cards = new();
        readonly Dictionary<CatVisitor, VisualElement> bubbleMap = new();
        string selected;
        IVisualElementScheduledItem toastJob;
        int lastTutorial = -1;

        GameManager G => GameManager.I;

        void Awake() { I = this; }

        UIDocument doc;
        bool cloned;
        Vector2 laidOutFor;
        readonly List<VisualElement> safeAreas = new();

        void OnEnable()
        {
            doc = GetComponent<UIDocument>();
            if (!cloned && doc.panelSettings) { doc.panelSettings = Instantiate(doc.panelSettings); cloned = true; }   // runtime tweaks never touch the asset
            root = doc.rootVisualElement;
            root.Clear();
            var ss = Resources.Load<StyleSheet>("UI/PawCollector");
            if (ss) root.styleSheets.Add(ss);
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;
            Build();
        }

        void Start()
        {
            G.Changed += Refresh;
            G.Snapped += OnSnapped;
            G.LeveledUp += OnLevelUp;
            G.BondUp += OnBondUp;
            G.SpotChanged += _ => RefreshHighlights();
            Refresh();
            Show(titleScreen, true);
        }

        // ------------------------------------------------------------------ build helpers
        static VisualElement El(string cls, VisualElement parent = null, string name = null)
        {
            var e = new VisualElement(); if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) e.AddToClassList(c);
            if (name != null) e.name = name; parent?.Add(e); return e;
        }
        static Label L(string text, string cls, VisualElement parent)
        {
            var l = new Label(text); foreach (var c in cls.Split(' ')) l.AddToClassList(c); parent.Add(l); return l;
        }
        static Button B(string text, string cls, VisualElement parent, System.Action onClick)
        {
            var b = new Button(() => { AudioManager.I?.Play("sfx_tap", 0.7f); onClick?.Invoke(); }) { text = text };
            foreach (var c in cls.Split(' ')) b.AddToClassList(c); parent.Add(b); return b;
        }
        static Texture2D Icon(string n) => Resources.Load<Texture2D>("Icons/" + n);
        static VisualElement Img(string icon, string cls, VisualElement parent) { var e = El(cls, parent); e.style.backgroundImage = Icon(icon); e.pickingMode = PickingMode.Ignore; return e; }

        static void HeartRow(VisualElement row, int hearts, string cls = "heart")
        {
            row.Clear();
            for (int i = 0; i < 5; i++) Img(i < hearts ? "ui_heart" : "ui_heart_empty", cls, row);
        }

        static void StarRow(VisualElement row, int stars, string cls = "star")
        {
            row.Clear();
            for (int i = 0; i < 3; i++) { var s = Img("ui_star", cls, row); s.EnableInClassList("off", i >= stars); }
        }

        void Build()
        {
            hud = El("layer", root); hud.pickingMode = PickingMode.Ignore;
            var safe = El("layer", hud); safe.pickingMode = PickingMode.Ignore; ApplySafeArea(safe);

            // top bar
            var top = El("topbar", safe);
            var lvl = El("level", top);
            ring = new RingElement(); ring.AddToClassList("level-ring"); lvl.Add(ring);
            var lvlStack = El("", lvl); levelLabel = L("1", "level-num", lvlStack); L("LV", "level-cap", lvlStack);
            var fish = El("pill", top); Img("ui_fish", "pill-icon", fish); fishLabel = L("0", "pill-value", fish);
            var gold = El("pill", top); Img("ui_star", "pill-icon gold", gold); goldLabel = L("0", "pill-value", gold);
            El("spacer", top).pickingMode = PickingMode.Ignore;
            Img("ui_gear", "btn-icon", B("", "round-btn", top, () => { RefreshSettings(); Show(settingsScreen, true); }));
            top.pickingMode = PickingMode.Ignore;

            bubbles = El("bubbles", safe); bubbles.pickingMode = PickingMode.Ignore;

            // dock: Shop / Album / Help, with the shop as a drawer that opens above it
            dock = El("dock", safe); dock.pickingMode = PickingMode.Ignore;
            shop = El("shop hidden", dock);
            var shopHead = El("shop-head", shop);
            L("Treat shop", "shop-title", shopHead);
            L("Treats & toys bring new cats", "shop-sub", shopHead);
            Img("ui_close", "btn-icon", B("", "round-btn shop-close", shopHead, () => ShowShop(false)));
            var scroll = new ScrollView(ScrollViewMode.Horizontal); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("tray"); shop.Add(scroll);
            foreach (var it in GameData.Items)
            {
                var card = El("card", scroll.contentContainer);
                var ic = El("card-icon", card); ic.style.backgroundImage = Icon("item_" + it.Id);
                L(it.Label, "card-name", card);
                var costRow = El("card-cost-row", card); Img("ui_fish", "cost-icon", costRow); L("", "card-cost", costRow);
                var id = it.Id;
                card.RegisterCallback<ClickEvent>(_ => SelectItem(id));
                cards[id] = card;
            }
            var nav = El("navrow", dock);
            shopBtn = B("", "nav shop-btn", nav, OnShopButton); shopIcon = Img("ui_shop", "nav-icon", shopBtn); shopLabel = L("Shop", "nav-label", shopBtn); shopLabel.pickingMode = PickingMode.Ignore;
            var ab = B("", "nav", nav, OpenAlbum); Img("ui_book", "nav-icon", ab); L("Album", "nav-label", ab).pickingMode = PickingMode.Ignore;
            Img("ui_help", "btn-icon", B("", "nav alt nav-square", nav,
                () => Toast("Tap a cat for photos · stroke a cat to pet it · press & hold the lawn for the feather toy · drag to look around, pinch to zoom")));

            coach = El("coach hidden", safe); coach.pickingMode = PickingMode.Ignore;
            var cb = El("coach-box", coach); coachIcon = Img("ui_feather", "coach-icon", cb); coachText = L("", "coach-text", cb);
            toast = El("toast", safe); toast.pickingMode = PickingMode.Ignore;
            var tb = El("toast-box", toast); toastText = L("", "toast-text", tb);

            BuildViewfinder();

            flash = El("layer flash", root); flash.pickingMode = PickingMode.Ignore;
            polaroidWrap = El("layer polaroid-wrap", root); polaroidWrap.pickingMode = PickingMode.Ignore;
            polaroid = El("polaroid", polaroidWrap);
            photoImg = El("photo", polaroid);
            photoName = L("", "photo-name", polaroid); photoPose = L("", "photo-pose", polaroid);
            photoStars = El("star-row", polaroid);
            photoGift = L("", "photo-gift", polaroid);
            photoBadge = L("NEW!", "photo-badge", polaroid);

            BuildTitle(); BuildAlbum(); BuildCatSheet(); BuildRecap(); BuildLevel(); BuildSettings();
        }

        void BuildViewfinder()
        {
            vf = El("layer vf hidden", root); vf.pickingMode = PickingMode.Ignore;
            var safe = El("layer", vf); safe.pickingMode = PickingMode.Ignore; ApplySafeArea(safe);
            var frame = El("vf-frame", safe); frame.pickingMode = PickingMode.Ignore;
            foreach (var c in new[] { "tl", "tr", "bl", "br" }) El("vf-corner " + c, frame).pickingMode = PickingMode.Ignore;
            var head = El("vf-head", safe); head.pickingMode = PickingMode.Ignore;
            vfName = L("", "vf-name", head);
            vfHearts = El("heart-row", head);
            var meterBox = El("vf-meter-box", safe); meterBox.pickingMode = PickingMode.Ignore;
            vfPose = L("", "vf-pose", meterBox);
            vfStars = El("star-row big", meterBox);
            var meter = El("vf-meter", meterBox);
            vfFill = El("vf-fill", meter);
            El("vf-tick t2", meter); El("vf-tick t3", meter);
            var bar = El("vf-bar", safe); bar.pickingMode = PickingMode.Ignore;
            Img("ui_close", "btn-icon", B("", "round-btn vf-close", bar, () => PhotoMode.I.Exit()));
            vfShutter = B("", "shutter", bar, () => PhotoMode.I.Shutter());
            El("shutter-in", vfShutter).pickingMode = PickingMode.Ignore;
            var shots = El("vf-shots", bar); Img("ui_cam", "vf-shots-icon", shots); vfShots = L("3", "vf-shots-n", shots);
        }

        void ApplySafeArea(VisualElement e) { if (!safeAreas.Contains(e)) safeAreas.Add(e); }

        Vector2 TargetSize
        {
            get
            {
                var rt = doc && doc.panelSettings ? doc.panelSettings.targetTexture : null;
                return rt ? new Vector2(rt.width, rt.height) : new Vector2(Screen.width, Screen.height);
            }
        }

        /// <summary>
        /// Phones (tall) scale by width; tablets / landscape blend toward height so nothing overgrows the screen.
        /// Also re-applies the device safe area (notch, home bar) in panel units.
        /// </summary>
        void Relayout()
        {
            var size = TargetSize; laidOutFor = size;
            var ps = doc.panelSettings;
            float aspect = size.x / Mathf.Max(1f, size.y);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1080, 2340);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = Mathf.Clamp01((aspect - 0.5f) / 0.5f);   // phone 0, tablet ~0.5, landscape 1
            float logW = Mathf.Log(size.x / 1080f, 2f), logH = Mathf.Log(size.y / 2340f, 2f);
            float unitsPerPx = 1f / Mathf.Pow(2f, Mathf.Lerp(logW, logH, ps.match));
            bool toScreen = !ps.targetTexture;
            var sa = toScreen ? Screen.safeArea : new Rect(0, 0, size.x, size.y);
            foreach (var e in safeAreas)
            {
                e.style.top = (size.y - sa.yMax) * unitsPerPx; e.style.bottom = sa.yMin * unitsPerPx;
                e.style.left = sa.xMin * unitsPerPx; e.style.right = (size.x - sa.xMax) * unitsPerPx;
            }
            root.EnableInClassList("wide", aspect > 0.62f);
        }

        VisualElement Overlay(out VisualElement sheet)
        {
            var o = El("layer overlay hidden", root);
            sheet = El("sheet", o);
            return o;
        }

        void BuildTitle()
        {
            titleScreen = Overlay(out var s);
            L("Paw Collector", "title-logo", s);
            L("Befriend the neighbourhood cats. Play, pet, and photograph them all.", "title-tag", s);
            var row = El("title-cats", s);
            foreach (var c in GameData.Cats.Take(5)) El("title-cat", row).style.backgroundImage = Icon("cat_" + c.Id);
            B("Play", "nav title-play", s, StartGame);
        }

        void BuildAlbum()
        {
            albumScreen = Overlay(out var s);
            L("Cat Album", "sheet-title", s);
            var bar = El("album-bar", s); albumFill = El("album-fill", bar);
            albumCount = L("", "album-count", s);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.AddToClassList("album-scroll"); s.Add(sv);
            albumGrid = El("album-grid", sv.contentContainer);
            B("Back to garden", "nav", s, () => Show(albumScreen, false));
        }

        void BuildCatSheet()
        {
            catScreen = Overlay(out var s);
            var headRow = El("cat-head", s);
            catPhoto = El("cat-photo", headRow);
            var info = El("cat-info-col", headRow);
            catName = L("", "cat-name", info);
            catInfo = L("", "cat-info", info);
            catHearts = El("heart-row", info);
            catBond = L("", "cat-bond", info);
            L("Poses", "cat-section", s);
            catPoses = El("pose-grid", s);
            B("Back", "nav", s, () => { Show(catScreen, false); Show(albumScreen, true); });
        }

        void BuildRecap()
        {
            recapScreen = Overlay(out var s);
            L("While you were away…", "sheet-title", s);
            recapText = L("", "sheet-text", s);
            B("Collect", "nav", s, () => { G.CollectOffline(); AudioManager.I.Play("sfx_coin"); Show(recapScreen, false); });
        }

        void BuildLevel()
        {
            levelScreen = Overlay(out var s);
            levelTitle = L("", "sheet-title", s);
            levelLines = El("", s);
            B("Yay!", "nav", s, () => Show(levelScreen, false));
        }

        VisualElement musicT, sfxT, hapT;
        void BuildSettings()
        {
            settingsScreen = Overlay(out var s);
            L("Settings", "sheet-title", s);
            musicT = ToggleRow(s, "Music", () => { G.Data.music = !G.Data.music; G.Persist(); AudioManager.I.Apply(); RefreshSettings(); });
            sfxT = ToggleRow(s, "Sounds", () => { G.Data.sfx = !G.Data.sfx; G.Persist(); AudioManager.I.Apply(); RefreshSettings(); });
            hapT = ToggleRow(s, "Vibration", () => { G.Data.haptics = !G.Data.haptics; G.Persist(); RefreshSettings(); });
            var resetBtn = B("Reset progress", "nav danger", s, null);
            resetBtn.clicked += () =>
            {
                if (resetBtn.text == "Tap again to confirm") { G.ResetProgress(); Show(settingsScreen, false); Show(titleScreen, true); G.Playing = false; resetBtn.text = "Reset progress"; }
                else resetBtn.text = "Tap again to confirm";
            };
            B("Done", "nav", s, () => { resetBtn.text = "Reset progress"; Show(settingsScreen, false); });
        }

        VisualElement ToggleRow(VisualElement parent, string label, System.Action onTap)
        {
            var row = El("row", parent); L(label, "row-label", row);
            var t = El("toggle", row); El("knob", t);
            t.RegisterCallback<ClickEvent>(_ => { AudioManager.I.Play("sfx_tap", 0.7f); onTap(); });
            return t;
        }

        void RefreshSettings()
        {
            musicT.EnableInClassList("on", G.Data.music); sfxT.EnableInClassList("on", G.Data.sfx); hapT.EnableInClassList("on", G.Data.haptics);
        }

        static void Show(VisualElement e, bool on) => e.EnableInClassList("hidden", !on);

        // ------------------------------------------------------------------ flow
        public void StartGame()
        {
            Show(titleScreen, false);
            G.Playing = true;
            if (G.Data.tutorial == 0) G.SetTutorial(1);
            if (G.OfflineFish > 0)
            {
                recapText.text = $"{G.OfflineVisits} cat visit{(G.OfflineVisits == 1 ? "" : "s")} while you were gone.\nThey left you {G.OfflineFish} fish!";
                Show(recapScreen, true);
            }
            Refresh();
        }

        void Refresh()
        {
            var d = G.Data;
            fishLabel.text = d.fish.ToString(); goldLabel.text = d.gold.ToString(); levelLabel.text = d.level.ToString();
            ring.Progress = d.xp / (float)GameData.XpForLevel(d.level);
            foreach (var it in GameData.Items)
            {
                var c = cards[it.Id];
                bool locked = d.level < it.UnlockLevel;
                c.EnableInClassList("locked", locked);
                c.EnableInClassList("poor", !locked && d.fish < it.Cost);
                c.EnableInClassList("selected", selected == it.Id);
                c.EnableInClassList("pulse", d.tutorial == 1 && it.Id == "Bowl" && selected == null);
                c.Q<Label>(className: "card-cost").text = locked ? $"Lv {it.UnlockLevel}" : it.Cost.ToString();
                c.Q(className: "cost-icon").style.backgroundImage = Icon(locked ? "ui_lock" : "ui_fish");
            }
            shopBtn.EnableInClassList("pulse", d.tutorial == 1 && !ShopOpen && selected == null);
            shopLabel.text = selected != null ? "Cancel" : "Shop";
            shopIcon.style.backgroundImage = Icon(selected != null ? "ui_close" : "ui_shop");
            root.EnableInClassList("shop-open", ShopOpen);
            RefreshCoach();
            RefreshHighlights();
        }

        void RefreshCoach()
        {
            string msg = null, icon = null;
            if (G.Playing && !PhotoMode.Active)
            {
                if (selected != null) msg = $"Tap a glowing mat to place the {GameData.Item(selected).Label.ToLower()}";
                else if (ShopOpen && G.Data.tutorial != 1) msg = null;
                else switch (G.Data.tutorial)
                {
                    case 1: msg = ShopOpen ? "Pick the Fish bowl — cats can't resist it" : "Welcome! Open the Shop to put out a snack"; break;
                    case 2: case 3:
                        msg = CatVisitor.All.Any(c => c.InGarden) ? "A visitor! Tap the cat to get your camera out" : "Something rustles by the fence… a cat is coming!";
                        icon = "ui_cam"; break;
                    case 4: msg = "Press and hold the lawn, then drag to wave the feather toy"; icon = "ui_feather"; break;
                    case 5: msg = "Now stroke a cat with your finger to pet it"; icon = "ui_heart"; break;
                }
            }
            else if (PhotoMode.Active && G.Data.tutorial == 3) msg = null;
            coach.EnableInClassList("hidden", msg == null);
            if (msg != null) coachText.text = msg;
            coachIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (icon != null) coachIcon.style.backgroundImage = Icon(icon);
        }

        void RefreshHighlights()
        {
            foreach (var s in G.Spots) s.Highlight(selected != null && s.Unlocked && string.IsNullOrEmpty(s.ItemId));
        }

        public bool ShopOpen => shop != null && !shop.ClassListContains("hidden");

        public void ShowShop(bool on)
        {
            Show(shop, on);
            if (on) selected = null;
            Refresh();
        }

        void OnShopButton()
        {
            if (selected != null) { selected = null; Refresh(); return; }   // "Cancel" while placing
            ShowShop(!ShopOpen);
        }

        void SelectItem(string id)
        {
            var it = GameData.Item(id);
            if (G.Data.level < it.UnlockLevel) { Toast($"{it.Label} unlocks at garden level {it.UnlockLevel}"); AudioManager.I.Play("sfx_error"); return; }
            if (G.Data.fish < it.Cost) { Toast("Not enough fish — take photos and play with cats to earn more!"); AudioManager.I.Play("sfx_error"); return; }
            selected = id;
            AudioManager.I.Play("sfx_pop", 0.8f);
            Show(shop, false);                     // get out of the way so the mats are visible
            Refresh();
        }

        public void OnSpotTapped(Spot spot)
        {
            if (selected != null)
            {
                if (G.TryPlace(spot, selected, out var reason))
                {
                    AudioManager.I.Play("sfx_place");
                    Toast($"{GameData.Item(selected).Label} placed — someone may smell it!");
                    selected = null;
                    if (G.Data.tutorial == 1) { G.SetTutorial(2); VisitorSpawner.I.SpawnSoon(1.5f); }
                }
                else { Toast(reason); AudioManager.I.Play("sfx_error"); }
                Refresh();
                return;
            }
            if (!spot.Unlocked) Toast($"This mat unlocks at garden level {GameManager.LevelForSpot(spot.Index)}");
            else if (!string.IsNullOrEmpty(spot.ItemId)) Toast($"{GameData.Item(spot.ItemId).Label} · {spot.Uses} visit{(spot.Uses == 1 ? "" : "s")} left");
            else Toast("Pick an item below, then tap this mat");
        }

        // ------------------------------------------------------------------ visitors
        public void OnVisitor(CatVisitor v)
        {
            var b = El("bubble-wrap", bubbles);
            var face = El("bubble", b);
            face.style.backgroundImage = Icon("cat_" + v.Def.Id);
            Img("ui_cam", "bubble-cam-icon", El("bubble-cam", face));
            if (!G.Found(v.Def.Id)) L("NEW", "bubble-new", b);
            var hearts = El("heart-row mini", b); hearts.name = "hearts";
            HeartRow(hearts, G.Data.Entry(v.Def.Id)?.Hearts ?? 0, "heart mini");
            face.RegisterCallback<ClickEvent>(_ => { if (v) PhotoMode.I.Enter(v); });
            bubbleMap[v] = b;
            b.style.display = DisplayStyle.None;
        }

        void Update()
        {
            if (TargetSize != laidOutFor) Relayout();
            foreach (var kv in bubbleMap.ToList())
            {
                var v = kv.Key; var b = kv.Value;
                if (v == null || v.Leaving) { b.RemoveFromHierarchy(); bubbleMap.Remove(kv.Key); continue; }
                bool show = v.InGarden;
                if (show && b.style.display == DisplayStyle.None)
                    Toast(G.Found(v.Def.Id) ? $"{v.Def.Id} dropped by!" : "A new cat is visiting!");
                b.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                b.EnableInClassList("spent", v.ShotsLeft <= 0);
            }
            if (G.Data.tutorial != lastTutorial || (G.Data.tutorial is 2 or 3)) { lastTutorial = G.Data.tutorial; RefreshCoach(); }
            UpdateViewfinder();
        }

        // ------------------------------------------------------------------ viewfinder
        public void ShowViewfinder(CatVisitor cat)
        {
            bool on = cat != null;
            Show(vf, on);
            hud.EnableInClassList("photo-on", on);
            if (on)
            {
                vfName.text = G.Found(cat.Def.Id) ? cat.Def.Id : "New cat!";
                HeartRow(vfHearts, G.Data.Entry(cat.Def.Id)?.Hearts ?? 0);
                Toast(G.Data.tutorial <= 3 ? "Wait for a great moment — when the meter's full, press the shutter!" : "Drag to circle the cat · pinch to zoom");
            }
            RefreshCoach();
        }

        void UpdateViewfinder()
        {
            var pm = PhotoMode.I;
            if (!PhotoMode.Active) return;
            var cat = pm.Target;
            float q = pm.Quality;
            vfFill.style.width = Length.Percent(Mathf.Clamp01(q) * 100f);
            int stars = pm.LiveStars;
            vfFill.EnableInClassList("s2", stars == 2); vfFill.EnableInClassList("s3", stars == 3);
            if (vfStars.childCount != 3) StarRow(vfStars, stars, "star big");
            for (int i = 0; i < 3; i++) vfStars[i].EnableInClassList("off", i >= stars);
            vfPose.text = GameData.PoseLabel(cat.PoseName);
            vfShots.text = cat.ShotsLeft.ToString();
            vfShutter.SetEnabled(cat.ShotsLeft > 0);
        }

        void OnSnapped(SnapResult r)
        {
            AudioManager.I.Play("sfx_snap");
            flash.AddToClassList("on");
            flash.schedule.Execute(() => flash.RemoveFromClassList("on")).StartingIn(60);
            if (r.Photo) photoImg.style.backgroundImage = r.Photo; else photoImg.style.backgroundImage = Icon("cat_" + r.Cat.Id);
            photoName.text = r.Cat.Id;
            photoPose.text = GameData.PoseLabel(r.Pose);
            StarRow(photoStars, r.Stars, "star");
            photoGift.text = $"+{r.Fish} fish" + (r.Gold > 0 ? $"   +{r.Gold} gold" : "") + (r.Sparkle ? "\nSparkle bonus!" : "");
            photoBadge.text = r.IsNew ? "NEW CAT!" : r.NewPose ? "NEW POSE!" : r.Stars > r.PrevStars ? "BEST SHOT!" : "";
            photoBadge.style.display = photoBadge.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            polaroid.RemoveFromClassList("away"); polaroid.AddToClassList("on");
            root.schedule.Execute(() => AudioManager.I.Play("sfx_coin")).StartingIn(350);
            root.schedule.Execute(() => { polaroid.AddToClassList("away"); }).StartingIn(1900);
            root.schedule.Execute(() => { polaroid.RemoveFromClassList("on"); polaroid.RemoveFromClassList("away"); }).StartingIn(2600);
            foreach (var kv in bubbleMap) if (kv.Key && kv.Key.Def == r.Cat) kv.Value.Q(className: "bubble-new")?.RemoveFromHierarchy();
            if (r.IsNew && G.CatsFound == GameData.Cats.Count) root.schedule.Execute(() => Toast("Album complete — you found every cat!")).StartingIn(2700);
            else if (r.Stars < 3 && G.Data.tutorial >= 6 && Random.value < 0.4f) root.schedule.Execute(() => Toast("Tip: snap when the cat glances at you or mid-pounce for 3 stars")).StartingIn(2700);
        }

        void OnBondUp(CatDef cat, int hearts)
        {
            Toast(hearts >= 5 ? $"{cat.Id} adores you! Best friends forever" : $"{cat.Id} trusts you more — {hearts} heart{(hearts == 1 ? "" : "s")}!");
            AudioManager.I.Play("sfx_levelup", 0.5f, 1.3f);
            foreach (var kv in bubbleMap) if (kv.Key && kv.Key.Def == cat) HeartRow(kv.Value.Q("hearts"), hearts, "heart mini");
            if (PhotoMode.Active && PhotoMode.I.Target.Def == cat) HeartRow(vfHearts, hearts);
        }

        void OnLevelUp(int level, List<string> lines)
        {
            AudioManager.I.Play("sfx_levelup");
            levelTitle.text = $"Garden level {level}!";
            levelLines.Clear();
            foreach (var l in lines) L(l, "sheet-line", levelLines);
            root.schedule.Execute(() => Show(levelScreen, true)).StartingIn(2700);
        }

        // ------------------------------------------------------------------ album
        void RefreshAlbum()
        {
            int n = G.CatsFound;
            albumFill.style.width = Length.Percent(100f * n / GameData.Cats.Count);
            int poses = G.Data.album.Sum(a => a.poses.Count), maxPoses = GameData.Cats.Count * GameData.Poses.Length;
            albumCount.text = $"{n} of {GameData.Cats.Count} cats found  ·  {poses}/{maxPoses} poses";
            albumGrid.Clear();
            foreach (var c in GameData.Cats)
            {
                var e = G.Data.Entry(c.Id);
                bool found = G.Found(c.Id);
                var slot = El("slot" + (found ? "" : " locked"), albumGrid);
                var img = El("slot-img", slot);
                var best = found ? PhotoStore.Best(e) : null;
                if (best) { img.style.backgroundImage = best; img.AddToClassList("is-photo"); }
                else img.style.backgroundImage = Icon("cat_" + c.Id);
                L(found ? c.Id : "???", "slot-name", slot);
                if (found)
                {
                    HeartRow(El("heart-row mini", slot), e.Hearts, "heart mini");
                    L($"{e.poses.Count}/{GameData.Poses.Length} poses", "slot-info", slot);
                    var id = c.Id;
                    slot.RegisterCallback<ClickEvent>(_ => { AudioManager.I.Play("sfx_tap", 0.7f); OpenCat(id); });
                }
                else
                {
                    var il = L(c.Rarity == Rarity.Legendary ? "Legendary · a story brings them" : c.Rarity == Rarity.Rare ? "Rare · visits busy gardens" : $"Loves the {GameData.Item(c.FavouriteItem).Label.ToLower()}", "slot-info", slot);
                    if (c.Rarity >= Rarity.Rare) il.AddToClassList("slot-rare");
                }
            }
        }

        public void OpenAlbum() { RefreshAlbum(); Show(albumScreen, true); }

        public void OpenCat(string id)
        {
            var c = GameData.Cat(id); var e = G.Data.Entry(id);
            var best = PhotoStore.Best(e);
            catPhoto.style.backgroundImage = best ? best : Icon("cat_" + id);
            catName.text = c.Id;
            catInfo.text = $"{c.Breed} · {c.Personality}\nPhotos taken: {e.photos}" + (e.sparkles > 0 ? $" · Sparkles: {e.sparkles}" : "");
            HeartRow(catHearts, e.Hearts);
            catBond.text = e.Hearts >= 5 ? "Best friends! Visits often and brings gifts." : "Pet, play and take photos to grow your bond";
            catPoses.Clear();
            foreach (var p in GameData.Poses)
            {
                var rec = e.Pose(p);
                var tile = El("pose" + (rec == null ? " locked" : ""), catPoses);
                var img = El("pose-img", tile);
                var tex = rec != null ? PhotoStore.Load(id, p) : null;
                if (tex) img.style.backgroundImage = tex; else L("?", "pose-q", img);
                L(GameData.PoseLabel(p), "pose-label", tile);
                StarRow(El("star-row small", tile), rec?.stars ?? 0, "star small");
            }
            Show(albumScreen, false);
            Show(catScreen, true);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: put the UI into a named state for layout screenshots.</summary>
        public void DevState(string state)
        {
            foreach (var o in new[] { titleScreen, albumScreen, recapScreen, levelScreen, settingsScreen, catScreen }) Show(o, false);
            selected = null; Show(shop, false);
            switch (state)
            {
                case "title": Show(titleScreen, true); break;
                case "shop": Show(shop, true); break;
                case "placing": selected = "Yarn"; break;
                case "toast": Toast("Tip: snap when the cat glances at you or mid-pounce for 3 stars"); break;
                case "album": OpenAlbum(); break;
                case "cat": { var e = G.Data.album.FirstOrDefault(a => a.photos > 0); if (e != null) OpenCat(e.cat); break; }
                case "recap": recapText.text = "4 cat visits while you were gone.\nThey left you 18 fish!"; Show(recapScreen, true); break;
                case "level":
                    levelTitle.text = "Garden level 5!"; levelLines.Clear();
                    foreach (var l in new[] { "+20 fish  ·  +1 gold fish", "New item: Sunny blanket", "2 new cats may visit…", "New garden mat unlocked" }) L(l, "sheet-line", levelLines);
                    Show(levelScreen, true); break;
                case "settings": RefreshSettings(); Show(settingsScreen, true); break;
            }
            Refresh();
        }
#endif

        public void Toast(string msg)
        {
            toastText.text = msg;
            toast.AddToClassList("on");
            toastJob?.Pause();
            toastJob = toast.schedule.Execute(() => toast.RemoveFromClassList("on")).StartingIn(2600);
        }

        public bool IsOverUI(Vector2 screenPos)
        {
            if (root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = root.panel.Pick(p);
            return picked != null && picked != root;
        }
    }

    /// <summary>Circular XP progress ring drawn with Painter2D.</summary>
    public class RingElement : VisualElement
    {
        float progress;
        public float Progress { get => progress; set { progress = Mathf.Clamp01(value); MarkDirtyRepaint(); } }
        public RingElement() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D; var r = contentRect; var c = r.center; float rad = Mathf.Min(r.width, r.height) * 0.5f - 5f;
            p.lineWidth = 9; p.lineCap = LineCap.Round;
            p.fillColor = new Color(1f, 0.973f, 0.933f); p.BeginPath(); p.Arc(c, rad, 0, 360); p.Fill();
            p.strokeColor = new Color(0.94f, 0.86f, 0.8f); p.BeginPath(); p.Arc(c, rad, 0, 360); p.Stroke();
            if (progress > 0.001f)
            {
                p.strokeColor = new Color(0.5f, 0.82f, 0.68f);
                p.BeginPath(); p.Arc(c, rad, -90, -90 + 360 * progress); p.Stroke();
            }
        }
    }
}
