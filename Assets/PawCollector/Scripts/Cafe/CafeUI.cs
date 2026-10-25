using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe
{
    /// <summary>All café UI (UI Toolkit, built in code): top bar, five tabs, world bubbles, viewfinder, modals, toasts.</summary>
    [RequireComponent(typeof(UIDocument))]
    public partial class CafeUI : MonoBehaviour
    {
        public static CafeUI I { get; private set; }
        VisualElement root, world, sheetHost, modalHost, vf, flash, toast, tools, vfReqBox; Label toastText, coinsL, ticketsL, popL, lvlL, vfName; VisualElement lvlFill;
        readonly Dictionary<string, Button> tabBtns = new(); string tab = "Cafe"; string sub = "";
        readonly Dictionary<object, VisualElement> bubbles = new(); float toastT;
        CafeManager M => CafeManager.I;

        void Awake() { I = this; }
        void OnEnable() => Build();
        /// <summary>Builds the whole UI into the document's root. Also re-run whenever the UIDocument hands us a new root
        /// (Unity reloads the document when its stylesheet or panel changes) so the UI never just disappears.</summary>
        void Build()
        {
            var doc = GetComponent<UIDocument>(); root = doc.rootVisualElement; root.Clear();
            cineActive = false; luring = false; tab = "Cafe"; if (CafeVisit.Active) CafeVisit.I.End(); visitBar = null;
            ShelterStage.I?.Show(false); if (CafeCamera.I && CafeCamera.I.Cam) CafeCamera.I.Cam.enabled = true;
            var css = Resources.Load<StyleSheet>("UI/Cafe"); if (!root.styleSheets.Contains(css)) root.styleSheets.Add(css); root.AddToClassList("root");
            world = Add(root, "layer"); world.pickingMode = PickingMode.Ignore;
            BuildTop(); sheetHost = Add(root, "layer"); sheetHost.pickingMode = PickingMode.Ignore;
            tools = Add(root, "hidden"); BuildTabs();
            vf = Add(root, "layer vf hidden"); vf.pickingMode = PickingMode.Ignore; BuildViewfinder();
            flash = Add(root, "layer flash"); flash.pickingMode = PickingMode.Ignore;
            cine = Add(root, "layer cine hidden");
            modalHost = Add(root, "layer hidden");
            modalHost.RegisterCallback<PointerDownEvent>(e => { if (e.target == modalHost) CloseModal(); });   // tap outside the card = close
            BuildRequests(); BuildGuide(); game = null; arcBg = arcade = null;
            toast = Add(root, "toast"); toast.pickingMode = PickingMode.Ignore; var tb = Add(toast, "toast-box"); toastText = L(tb, "", "toast-text");
            IgnoreTree(toast);   // info-only overlays: never swallow taps meant for the café (toast stays in the tree at opacity 0)
            // Track presses that UI Toolkit routes to a real UI element, so the café never also reacts to them (taps passing through buttons).
            root.RegisterCallback<PointerDownEvent>(e => { if (e.target is VisualElement t && t != root && t != world && !world.Contains(t)) { PointerOnUI = true; uiDownFrame = Time.frameCount; } }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => PointerOnUI = false, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(_ => PointerOnUI = false, TrickleDown.TrickleDown);
            ApplySafeArea();
            if (!titleShown) ShowTitle();
        }

        // ------------------------------------------------------------ title screen (once per app launch)
        static bool titleShown;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetTitle() => titleShown = false;
        VisualElement title;
        /// <summary>Matches the iOS launch screen (same art and layout), then comes alive: the logo bobs, Mochi waves,
        /// "Tap to start" pulses, and a status line shows the invisible sign-in. Tap anywhere to play.</summary>
        void ShowTitle()
        {
            titleShown = true;
            title = Add(root, "title"); var ti = title;   // the layout callback can fire while it fades out (title is null by then)

            var bgPortrait = Resources.Load<Texture2D>("UI/Title/title_bg"); var bgLand = Resources.Load<Texture2D>("UI/Title/title_bg_land");
            var logo = Add(title, "title-logo"); Img(logo, Resources.Load<Texture2D>("UI/Title/title_logo"));
            // (portrait x, bottom, width) and (landscape x, bottom, width) — fractions matching the iOS launch images
            var cats = new List<(VisualElement e, Vector3 port, Vector3 land)>();
            foreach (var (id, port, land) in new[] { ("Oreo", new Vector3(0.25f, 0.835f, 0.333f), new Vector3(0.62f, 0.83f, 0.17f)), ("Luna", new Vector3(0.75f, 0.835f, 0.333f), new Vector3(0.88f, 0.83f, 0.17f)), ("Mochi", new Vector3(0.5f, 0.85f, 0.434f), new Vector3(0.75f, 0.87f, 0.22f)) })
            { var c = Add(title, "title-cat"); Img(c, Icon("cat_" + id)); cats.Add((c, port, land)); }
            var tap = L(title, "Tap to start", "title-tap");
            var status = L(title, "Opening the café…", "title-status");
            IgnoreTree(title); title.pickingMode = PickingMode.Position;
            // lay out like the launch image: positions are fractions of the full screen
            void Layout()
            {
                var sz = ti.layout.size; if (sz.x < 2) return;
                bool land = sz.x > sz.y; Img(ti, land ? bgLand : bgPortrait);
                if (land)
                {
                    float lw = sz.x * 0.452f, lh = lw * 760f / 1100f; logo.style.width = lw; logo.style.height = lh; logo.style.left = sz.x * 0.27f - lw / 2f; logo.style.top = sz.y * 0.47f - lh / 2f;
                    tap.style.left = sz.x * 0.10f; tap.style.right = sz.x * 0.56f; tap.style.top = sz.y * 0.80f; status.style.top = sz.y * 0.92f;
                    status.style.left = sz.x * 0.04f; status.style.right = sz.x * 0.50f;
                }
                else
                {
                    float lw = sz.x * 0.895f; logo.style.width = lw; logo.style.height = lw * 760f / 1100f; logo.style.left = (sz.x - lw) / 2f; logo.style.top = sz.y * 0.25f;
                    tap.style.left = sz.x * 0.18f; tap.style.right = sz.x * 0.18f; tap.style.top = sz.y * 0.885f; status.style.top = sz.y * 0.93f; status.style.left = 0; status.style.right = 0;
                }
                foreach (var (e, port, landPos) in cats)
                {
                    var v = land ? landPos : port; float px = sz.x * v.z;
                    e.style.width = px; e.style.height = px; e.style.left = sz.x * v.x - px / 2f; e.style.top = sz.y * v.y - px;
                }
            }
            title.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            float t0 = Time.unscaledTime;
            title.schedule.Execute(() =>
            {
                float t = Time.unscaledTime - t0;
                logo.style.translate = new Translate(0, Mathf.Sin(t * 2.2f) * 10f);
                logo.style.scale = new Scale(Vector3.one * (1f + 0.02f * Mathf.Sin(t * 2.2f + 1f)));
                cats[2].e.style.rotate = new Rotate(Mathf.Sin(t * 5f) * 6f * Mathf.Clamp01(Mathf.Sin(t * 0.9f) * 2f));   // Mochi waves now and then
                cats[0].e.style.translate = new Translate(0, -Mathf.Abs(Mathf.Sin(t * 3f)) * 8f * (Mathf.Sin(t * 0.7f) > 0.6f ? 1 : 0));
                cats[1].e.style.rotate = new Rotate(Mathf.Sin(t * 1.6f) * 3f);
                tap.style.scale = new Scale(Vector3.one * (1f + 0.05f * Mathf.Sin(t * 4f)));
                var cloud = CafeCloud.I;
                status.text = cloud == null ? "Opening the café…" : cloud.Online && cloud.Me != null ? $"Welcome back to {cloud.Me.cafe_name}!" : cloud.Online ? "Connected" : t > 6f ? "Playing offline" : "Connecting to the cat cloud…";
            }).Every(16);
            title.RegisterCallback<PointerDownEvent>(_ => HideTitle());
            ApplySafeArea();   // stretches the title under the notch and home bar
            AudioManager.I?.Meow(1.1f);
        }
        void HideTitle()
        {
            if (title == null) return;
            var t = title; title = null; TapSfx();
            t.pickingMode = PickingMode.Ignore;
            t.experimental.animation.Start(1f, 0f, 380, (e, v) => { e.style.opacity = v; e.style.scale = new Scale(Vector3.one * (1f + (1f - v) * 0.08f)); })
             .OnCompleted(() => t.RemoveFromHierarchy());
        }
        void Start()
        {
            M.Changed += Refresh; M.Toast += Toast; M.LevelUp += OnLevelUp; M.WelcomeBack += OnWelcome;
            CafePhoto.I.Snapped += OnSnapped; CafeCat.Petted += OnPetted; Refresh();
        }
        Rect lastSafe; Vector2Int lastScreen;
        /// <summary>Keep the UI inside the phone's safe area (notch / Dynamic Island / home bar). The root is inset so that
        /// absolutely-positioned children (coach, toast, page padding) move too; full-screen layers stretch back out under the notch.</summary>
        void ApplySafeArea()
        {
            if (root?.panel == null) return;
            var sa = Screen.safeArea; lastSafe = sa; lastScreen = new Vector2Int(Screen.width, Screen.height);
            var tl = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sa.xMin, Screen.height - sa.yMax));
            var br = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sa.xMax, Screen.height - sa.yMin));
            var full = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(Screen.width, Screen.height));
            float top = Mathf.Max(0, tl.y), bottom = Mathf.Max(0, full.y - br.y), left = Mathf.Max(0, tl.x), right = Mathf.Max(0, full.x - br.x);
            root.style.paddingTop = 0; root.style.paddingBottom = 0;
            root.style.position = Position.Absolute; root.style.left = left; root.style.right = right; root.style.top = top; root.style.bottom = bottom;
            foreach (var layer in new[] { world, modalHost, flash, title, guide, arcBg }) if (layer != null) { layer.style.top = -top; layer.style.bottom = -bottom; layer.style.left = -left; layer.style.right = -right; }
        }

        // ------------------------------------------------------------ helpers
        static VisualElement Add(VisualElement p, string cls = null) { var e = new VisualElement(); if (cls != null) foreach (var c in cls.Split(' ')) e.AddToClassList(c); p.Add(e); return e; }
        static Label L(VisualElement p, string t, string cls = null) { var l = new Label(t); if (cls != null) foreach (var c in cls.Split(' ')) l.AddToClassList(c); p.Add(l); return l; }
        static void TapSfx() => AudioManager.I?.Play("sfx_tap", 0.45f, UnityEngine.Random.Range(0.96f, 1.06f));
        static Button B(VisualElement p, string t, Action a, string cls = "btn") { var b = new Button(a) { text = t, focusable = false }; b.clicked += TapSfx; foreach (var c in cls.Split(' ')) b.AddToClassList(c); p.Add(b); Fit(b); return b; }
        // only labels whose box has a fixed width (auto-width pills size themselves to their text)
        static readonly string[] FitClasses = { "card-name", "card-sub", "catcard-name", "catcard-sub", "rcard-name", "tab-lbl", "sround-lbl", "adopt-lbl", "supply-lbl", "need-name", "chip", "lure-text", "rt-lbl", "float-name" };
        /// <summary>Boxes keep their fixed size; if a text doesn't fit, its font shrinks until it does (never clipped).</summary>
        static void Fit(TextElement t)
        {
            if (t == null || t.userData as string == "fit") return; t.userData = "fit";
            float baseSize = -1;
            t.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float avail = t.contentRect.width; if (avail <= 1 || string.IsNullOrEmpty(t.text)) return;
                float cur = t.resolvedStyle.fontSize; if (baseSize < 0) baseSize = cur;
                var need = t.MeasureTextSize(t.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                float needBase = need * baseSize / Mathf.Max(1f, cur);   // width at the designed size
                float want = needBase > avail + 0.5f ? Mathf.Max(11f, Mathf.Floor(baseSize * avail / needBase)) : baseSize;
                if (Mathf.Abs(want - cur) >= 1f) t.style.fontSize = want;
            });
        }
        static void FitAll(VisualElement e)
        {
            foreach (var t in e.Query<TextElement>().ToList())
                if (t is Button || FitClasses.Any(c => t.ClassListContains(c))) Fit(t);
        }
        static Texture2D Icon(string n) => Resources.Load<Texture2D>("Icons/" + n);
        static void Img(VisualElement e, Texture2D t) { e.style.backgroundImage = t ? new StyleBackground(t) : StyleKeyword.None; }
        static void IgnoreTree(VisualElement e) { e.pickingMode = PickingMode.Ignore; foreach (var c in e.Children()) IgnoreTree(c); }
        int uiDownFrame = -100;
        /// <summary>True while a finger/mouse that went down on a UI element is still held.</summary>
        public bool PointerOnUI { get; private set; }
        /// <summary>UI claimed a press this frame or last frame (UI Toolkit may dispatch its events a frame after the Input System sees the press).</summary>
        public bool UIClaimedPress => PointerOnUI || Time.frameCount - uiDownFrame <= 1;
        public void ReleasePointer() => PointerOnUI = false;
        public bool IsOverUI(Vector2 screen)
        {
            if (root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var e = root.panel.Pick(p);
            return e != null && e != root && e.pickingMode == PickingMode.Position && !(e == world || world.Contains(e));
        }

        // ------------------------------------------------------------ top bar
        void BuildTop()
        {
            var top = Add(root, "top");
            var lv = Add(top, "lvl"); lvlL = L(lv, "1", "lvl-num"); var bar = Add(lv, "lvl-bar"); lvlFill = Add(bar, "lvl-fill");
            Pill(top, "ui_fish", out coinsL); Pill(top, "ui_paw", out ticketsL); Pill(top, "ui_heart", out popL);
            Add(top, "grow");
            var fr = new Button(OpenFriends) { focusable = false }; fr.clicked += TapSfx; fr.AddToClassList("ibtn"); Img(Add(fr, "ibtn-ico"), Icon("ui_friends")); top.Add(fr);
            var gear = new Button(OpenSettings) { focusable = false }; gear.clicked += TapSfx; gear.AddToClassList("ibtn"); Img(Add(gear, "ibtn-ico"), Icon("ui_gear")); top.Add(gear);
        }
        void Pill(VisualElement p, string icon, out Label val) { var pill = Add(p, "pill"); var ic = Add(pill, "pill-ico"); Img(ic, Icon(icon)); val = L(pill, "0", "pill-val"); }

        void Refresh()
        {
            if (M == null || coinsL == null) return;
            var d = M.Data;
            coinsL.text = d.coins.ToString("N0"); ticketsL.text = d.tickets.ToString(); popL.text = Mathf.RoundToInt(M.Popularity).ToString(); lvlL.text = d.level.ToString();
            lvlFill.style.width = Length.Percent(100f * d.xp / CafeData.XpForLevel(d.level));
            if (tabBtns.TryGetValue("Shelter", out var sb)) Badge(sb, d.tickets);
            if (tabBtns.TryGetValue("Cats", out var cb)) Badge(cb, M.CatsNeedingCare);
            if (!cineActive && tab != "Cafe" && sheetHost.childCount > 0 && Time.unscaledTime - lastSheetBuild > 0.5f && tab != "Decorate") RebuildSheet();
        }
        float lastSheetBuild;
        static void Badge(VisualElement b, int n) { var old = b.Q(className: "badge"); old?.RemoveFromHierarchy(); if (n > 0) L(b, n.ToString(), "badge"); }

        // ------------------------------------------------------------ tabs
        void BuildTabs()
        {
            var bar = Add(root, "tabs");
            (string id, string label, string icon)[] t = { ("Cafe", "Café", "ui_paw"), ("Decorate", "Decorate", "ui_shop"), ("Cats", "Cats", "ui_heart"), ("Photos", "Photos", "ui_cam"), ("Play", "Play", "ui_game"), ("Shelter", "Shelter", "ui_sparkle") };
            foreach (var (id, label, icon) in t)
            {
                var b = new Button(() => OpenTab(id)) { focusable = false }; b.clicked += TapSfx; b.AddToClassList("tab"); var ic = Add(b, "tab-ico"); Img(ic, Icon(icon)); L(b, label, "tab-lbl");
                bar.Add(b); tabBtns[id] = b;
            }
            OpenTab("Cafe"); FitAll(bar);
        }
        public void OpenTab(string id)
        {
            if (id == "Play") { OpenArcade(); return; }   // the Game Corner opens as a window over the café
            if (CafePhoto.Active) CafePhoto.I.Exit();
            tab = (tab == id && id != "Cafe") ? "Cafe" : id;
            foreach (var kv in tabBtns) kv.Value.EnableInClassList("on", kv.Key == tab);
            CafeDecorate.I?.SetMode(tab == "Decorate");
            if (cineActive) EndCutscene();
            if (tab == "Shelter") ShelterStage.Ensure().Show(true); else ShelterStage.I?.Show(false);
            RebuildSheet();
            if (tab == "Decorate") OpenShop(GuideShopping ? GuideShopCat : null);
        }
        void RebuildSheet()
        {
            lastSheetBuild = Time.unscaledTime;
            float scroll = sheetHost.Q<ScrollView>()?.scrollOffset.y ?? 0;
            sheetHost.Clear(); if (tab == "Cafe" || cineActive) return;
            if (tab == "Shelter") { FillShelterPage(sheetHost); FitAll(sheetHost); return; }
            if (tab == "Decorate") { DecorBar(sheetHost); FitAll(sheetHost); return; }
            var sheet = Add(sheetHost, "sheet");
            var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden }; sv.style.flexGrow = 1;
            switch (tab)
            {
                case "Cats": L(sheet, "Your cats", "sheet-title"); L(sheet, $"{M.Data.residents.Count} living here · {M.Beds} cat beds" + (M.Data.waiting.Count > 0 ? $" · {M.Data.waiting.Count} waiting for a bed" : ""), "sheet-sub"); Supplies(sheet); sheet.Add(sv); FillCats(sv.contentContainer); break;
                case "Photos": L(sheet, "Photos", "sheet-title"); FillPhotos(sheet, sv); break;
            }
            sv.scrollOffset = new Vector2(0, scroll);
            FitAll(sheetHost);
        }

        // ---- Decorate
        // ---- Decorate: slim bar + shop modal
        void DecorBar(VisualElement host)
        {
            var bar = Add(host, "decor-bar");
            var hint = Add(bar, "decor-hint"); L(hint, "Drag any piece to move it", "decor-hint-txt"); L(hint, "Tap it to rotate or store", "decor-hint-sub");
            var shop = new Button(() => OpenShop(GuideShopping ? GuideShopCat : null)) { focusable = false }; shop.clicked += TapSfx; shop.AddToClassList("shop-btn"); bar.Add(shop); decorShopBtn = shop;
            Img(Add(shop, "shop-btn-ico"), Icon("ui_shop")); L(shop, "Shop", "shop-btn-lbl");
        }
        static readonly (string id, string label, string icon)[] ShopCats =
        {
            ("Seating", "Seating", "furn_Chair"), ("Cat furniture", "Cat stuff", "furn_CatTree"), ("Decor", "Decor", "furn_Plant"),
            ("Rooms", "Rooms", "furn_Rug"), ("Menu", "Menu", "furn_Prop_Cake"), ("Upgrades", "Upgrades", "ui_star"),
        };
        string shopCat = "Seating";
        public void OpenShop(string cat = null)
        {
            if (cat != null) shopCat = cat;
            Modal(m =>
            {
                m.AddToClassList("shop");
                var head = Add(m, "shop-head");
                L(head, "Shop", "shop-title");
                var coins = Add(head, "pill"); Img(Add(coins, "pill-ico"), Icon("ui_fish")); L(coins, M.Data.coins.ToString("N0"), "pill-val");
                Add(head, "grow");
                var x = new Button(CloseModal) { focusable = false }; x.clicked += TapSfx; x.AddToClassList("ibtn"); Img(Add(x, "ibtn-ico"), Icon("ui_close")); head.Add(x);
                var tabs = Add(m, "shop-tabs"); shopTabBtns.Clear(); shopBuyBtns.Clear();
                foreach (var (id, label, icon) in ShopCats)
                {
                    var t = new Button(() => OpenShop(id)) { focusable = false }; t.clicked += TapSfx; t.AddToClassList("shop-tab"); t.EnableInClassList("on", shopCat == id); tabs.Add(t); shopTabBtns[id] = t;
                    var ib = Add(t, "shop-tab-icobg"); Img(Add(ib, "shop-tab-ico"), Icon(icon)); L(t, label, "shop-tab-lbl");
                }
                var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                sv.AddToClassList("shop-scroll"); m.Add(sv);
                FillShop(sv.contentContainer);
            });
        }
        /// <summary>One shop card: picture, name, a perk line and the price button (or lock / status).</summary>
        VisualElement ShopCard(VisualElement grid, string icon, string name, string perk)
        {
            var card = Add(grid, "scard");
            var pic = Add(card, "scard-pic"); Img(Add(pic, "scard-img"), Icon(icon));
            L(card, name, "card-name"); L(card, perk, "card-sub");
            return card;
        }
        Button PriceButton(VisualElement card, long price, Action buy)
        {
            var b = new Button(buy) { focusable = false }; b.clicked += TapSfx; b.AddToClassList("price"); card.Add(b);
            Img(Add(b, "price-ico"), Icon("ui_fish")); L(b, price.ToString("N0"), "price-lbl");
            if (M.Data.coins < price) b.AddToClassList("off");
            return b;
        }
        static void StatusTag(VisualElement card, string text) => L(card, text, "scard-status");
        void FillShop(VisualElement c)
        {
            var grid = Add(c, "sgrid");
            if (shopCat == "Rooms")
            {
                foreach (var r in CafeData.Rooms.Where(r => r.Cost > 0))
                {
                    var card = ShopCard(grid, "furn_Rug", r.Label, M.RoomOpen(r.Id) ? "Open" : "New room + furniture");
                    if (M.RoomOpen(r.Id)) StatusTag(card, "Open");
                    else if (r.Unlock > M.Data.level) { card.AddToClassList("locked"); LockPill(card, r.Unlock); }
                    else PriceButton(card, r.Cost, () => { if (M.TryUnlockRoom(r.Id)) CloseModal(); });
                }
                return;
            }
            if (shopCat == "Upgrades")
            {
                foreach (var u in CafeData.Upgrades)
                {
                    int lv = M.Data.Up(u.Id);
                    var card = ShopCard(grid, "ui_star", u.Label, u.Text); L(card, $"Lv {lv}/{u.MaxLevel}", "scard-corner");
                    if (lv >= u.MaxLevel) StatusTag(card, "MAX");
                    else PriceButton(card, CafeData.UpgradeCost(u, lv), () => { if (M.BuyUpgrade(u.Id)) OpenShop(); });
                }
                return;
            }
            if (shopCat == "Menu")
            {
                foreach (var m in CafeData.Menu)
                {
                    var card = ShopCard(grid, "furn_" + m.Prop, m.Label, $"Sells for {m.Price}");
                    if (m.Unlock > M.Data.level) { card.AddToClassList("locked"); LockPill(card, m.Unlock); }
                    else StatusTag(card, "On the menu");
                }
                return;
            }
            var cat = shopCat == "Seating" ? FurnCat.Seating : shopCat == "Decor" ? FurnCat.Decor : FurnCat.CatFurniture;
            foreach (var f in CafeData.Furniture.Where(f => f.Cat == cat))
            {
                string info = f.Seats > 0 ? "+1 seat" : f.Beds > 0 ? "A cat's own bed" : f.Id == "FoodBowl" ? "Cats eat here" : f.Perches.Length > 0 ? "Cat perch" : "";
                if (f.Cozy > 0) info += (info.Length > 0 ? " · " : "") + $"cozy +{f.Cozy}";
                var card = ShopCard(grid, "furn_" + f.Id, f.Label, info);
                if (f.Unlock > M.Data.level) { card.AddToClassList("locked"); LockPill(card, f.Unlock); }
                else { int price = M.Price(f); shopBuyBtns[f.Id] = PriceButton(card, price, () => { if (M.Data.coins < price) { M.Say("Not enough coins yet"); return; } CloseModal(); CafeDecorate.I.BeginPlacing(f.Id); }); }
            }
        }
        static void LockPill(VisualElement card, int level) { var p = Add(card, "lockpill"); Img(Add(p, "lockpill-ico"), Icon("ui_lock")); L(p, $"Level {level}", "lockpill-txt"); }
        // ---- floating tools next to the piece you're placing or have selected
        Transform floatTarget;
        public void ShowFurnitureTools(FurnitureInstance f)
        {
            if (CafeDecorate.I && CafeDecorate.I.Placing) return;
            tools.Clear(); tools.RemoveFromClassList("hidden"); tools.AddToClassList("float-tools"); tools.RemoveFromClassList("placer");
            sheetHost.style.display = f ? DisplayStyle.None : DisplayStyle.Flex;
            if (!f) { tools.AddToClassList("hidden"); floatTarget = null; return; }
            floatTarget = f.transform;
            L(tools, f.Def.Label, "float-name"); L(tools, f.Def.WallMounted ? "Drag along the walls" : "Drag to move", "float-hint");
            var row = Add(tools, "float-row");
            if (CafeDecorate.CanRotate(f.Def)) RoundTool(row, "ui_rotate", "Rotate", "mint", () => CafeDecorate.I.Rotate(f));
            RoundTool(row, "ui_box", $"Store +{f.Def.Cost / 2}", "cream", () => CafeDecorate.I.Store(f));
            RoundTool(row, "ui_check", "Done", "pink", () => CafeDecorate.I.Select(null));
            FitAll(tools);
        }
        /// <summary>Ghost placement controls: Rotate · ✕ Cancel · ✓ Place (price).</summary>
        public void ShowPlacer(bool on)
        {
            var dc = CafeDecorate.I;
            if (!on || dc == null || !dc.Placing)
            {
                if (floatTarget != null && (dc == null || floatTarget != dc.GhostTransform)) return;   // something else is shown
                tools.Clear(); tools.AddToClassList("hidden"); floatTarget = null; sheetHost.style.display = DisplayStyle.Flex; return;
            }
            var def = dc.GhostDef;
            tools.Clear(); tools.RemoveFromClassList("hidden"); tools.AddToClassList("float-tools"); tools.AddToClassList("placer");
            tools.style.left = StyleKeyword.Null; tools.style.top = StyleKeyword.Null;   // docked at the right edge, never over the piece
            sheetHost.style.display = DisplayStyle.None; floatTarget = dc.GhostTransform;
            L(tools, def.Label, "float-name"); L(tools, "Drag anywhere to move it", "float-hint"); L(tools, "Tap a spot to drop it there", "float-hint");
            var row = Add(tools, "float-row");
            if (!def.WallMounted && CafeDecorate.CanRotate(def)) RoundTool(row, "ui_rotate", "Rotate", "mint", () => dc.RotateGhost());
            RoundTool(row, "ui_close", "Cancel", "cream", () => { dc.CancelPlacing(); OpenShop(); });
            placerOkBtn = RoundTool(row, "ui_check", $"Place · {M.Price(def)}", "gold", () => { if (dc.ConfirmPlacing()) { Toast($"{def.Label} placed!"); OpenShop(); } });
            FitAll(tools);
        }
        Button RoundTool(VisualElement row, string icon, string label, string colour, Action a)
        {
            var col = Add(row, "rt");
            var b = new Button(a) { focusable = false }; b.clicked += TapSfx; b.AddToClassList("rt-btn"); b.AddToClassList("rt-" + colour); col.Add(b);
            Img(Add(b, "rt-ico"), Icon(icon)); L(col, label, "rt-lbl"); return b;
        }
        void PositionFloatTools()
        {
            if (floatTarget == null || tools.ClassListContains("hidden") || tools.ClassListContains("placer") || root?.panel == null) return;
            var cam = CafeCamera.I.Cam; var sp = cam.WorldToScreenPoint(floatTarget.position);
            if (sp.z <= 0) return;
            var pp = WorldToPanel(sp); var size = root.layout.size; var ts = tools.layout.size;
            if (float.IsNaN(ts.x) || ts.x < 2) ts = new Vector2(420, 200);
            float x = Mathf.Clamp(pp.x - ts.x / 2f, 150f, size.x - ts.x - 16f);
            float y = pp.y + 40f; if (y + ts.y > size.y - 16f) y = pp.y - ts.y - 120f;   // below the piece, or above it near the bottom
            y = Mathf.Clamp(y, 90f, size.y - ts.y - 16f);
            tools.style.left = x; tools.style.top = y;
        }

        // ---- Cats
        void FillCats(VisualElement c)
        {
            var grid = Add(c, "catgrid");
            foreach (var id in M.Data.residents.Concat(M.Data.waiting))
            {
                var def = GameData.Cat(id); var rec = M.Data.CatOrNew(id); bool waiting = M.Data.waiting.Contains(id);
                var card = new Button(() => OpenCat(id)) { focusable = false }; card.clicked += TapSfx; grid.Add(card);
                card.AddToClassList("catcard"); card.AddToClassList("r-" + (def?.Rarity ?? Rarity.Common).ToString().ToLower());
                var ring = Add(card, "cat-ring"); Img(Add(ring, "cat-pic"), Icon("cat_" + id));
                L(card, (def?.Rarity ?? Rarity.Common).ToString(), "rarity-chip");
                L(card, M.Data.Name(id), "catcard-name");
                L(card, def?.Breed ?? "", "catcard-sub");
                Hearts(card, rec.Hearts);
                if (!waiting) NeedBars(card, rec, true);
                var mood = M.MoodOf(id);
                bool alert = mood is CafeManager.Mood.Sick or CafeManager.Mood.Hungry or CafeManager.Mood.Sad;
                L(card, waiting ? "Waiting for a bed" : alert ? CafeManager.MoodWord(mood) : CatStatus(id), waiting ? "catcard-status off" : alert ? "catcard-status alert" : "catcard-status");
                if (alert && !waiting) { var ai = Add(card, "care-alert"); Img(ai, Icon(MoodIcon(mood))); }
            }
        }
        static string MoodIcon(CafeManager.Mood m) => m switch { CafeManager.Mood.Sick => "ui_pill", CafeManager.Mood.Hungry => "item_Bowl", CafeManager.Mood.Sad => "ui_heart_empty", _ => "ui_heart" };
        /// <summary>Food / happiness / health bars (compact on cards, labelled in the profile).</summary>
        void NeedBars(VisualElement p, CatRec r, bool mini)
        {
            var box = Add(p, mini ? "needs mini" : "needs");
            foreach (var (label, v, cls, ico) in new[] { ("Food", r.full, "n-food", "item_Bowl"), ("Happy", r.happy, "n-happy", "ui_heart"), ("Health", r.health, "n-health", "ui_pill") })
            {
                var row = Add(box, "need-row"); Img(Add(row, "need-ico"), Icon(ico));
                if (!mini) L(row, label, "need-name");
                var track = Add(row, "need-track"); var fill = Add(track, "need-fill " + cls); fill.style.width = Length.Percent(Mathf.Clamp(v, 0, 100));
                if (v < CafeData.LowNeed) fill.AddToClassList("low");
                if (!mini) L(row, Mathf.RoundToInt(Mathf.Clamp(v, 0, 100)).ToString(), "need-val");
            }
        }
        /// <summary>Cat food and medicine stock, with buy buttons.</summary>
        void Supplies(VisualElement p)
        {
            var row = Add(p, "supplies");
            var f = Add(row, "supply"); Img(Add(f, "supply-ico"), Icon("item_Bowl")); L(f, $"{M.Data.food}", "supply-val"); L(f, "food", "supply-lbl");
            B(f, $"+{CafeData.FoodPackSize}  ·  {CafeData.FoodPackCost}", () => { if (M.BuyFood()) RebuildSheet(); }, "btn small gold");
            var m = Add(row, "supply"); Img(Add(m, "supply-ico"), Icon("ui_pill")); L(m, $"{M.Data.medicine}", "supply-val"); L(m, "medicine", "supply-lbl");
            B(m, $"+1  ·  {CafeData.MedicineCost}", () => { if (M.BuyMedicine()) RebuildSheet(); }, "btn small gold");
        }
        void Hearts(VisualElement p, int n) { var hs = Add(p, "hearts"); for (int i = 0; i < 5; i++) { var h = Add(hs, "heart"); Img(h, Icon("ui_heart")); h.EnableInClassList("off", i >= n); } }
        string CatStatus(string id)
        {
            var cat = M.Cats.FirstOrDefault(k => k && k.Def.Id == id); if (!cat) return "Out and about";
            if (cat.PoseName == "Sleep") return "Napping";
            return cat.Activity switch { "perch" => "Lounging", "visit" => "Charming a guest", "eat" => "Snacking", "play" => "Playing", "wander" => "Exploring", _ => "Relaxing" };
        }
        static readonly string[] AllPoses = { "Sit", "Stroll", "Eat", "Play", "Pounce", "Sleep" };

        /// <summary>A cat's profile: who they are, friendship, personality stats, charm and their photo album.</summary>
        public void OpenCat(string id)
        {
            var def = GameData.Cat(id); if (def == null) return;
            var rec = M.Data.CatOrNew(id); bool home = M.Data.residents.Contains(id);
            var cat = M.Cats.FirstOrDefault(k => k && k.Def.Id == id);
            Modal(m =>
            {
                m.AddToClassList("catmodal"); m.AddToClassList("r-" + def.Rarity.ToString().ToLower());
                var head = Add(m, "cat-head");
                var catScroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden }; catScroll.AddToClassList("catscroll"); m.Add(catScroll); var cols = Add(catScroll, "catcols"); var left = Add(cols, "catcol"); var right = Add(cols, "catcol");   // landscape: two columns
                var ring = Add(head, "cat-ring big"); Img(Add(ring, "cat-pic"), Icon("cat_" + id));
                var col = Add(head, "grow");
                L(col, def.Rarity.ToString(), "rarity-chip");
                var nameRow = Add(col, "row"); L(nameRow, M.Data.Name(id), "cat-name");
                var pen = B(nameRow, "Rename", () => Rename(id, () => OpenCat(id)), "btn small ghost"); pen.AddToClassList("rename");
                L(col, (M.Data.Name(id) != id ? $"born {id} · " : "") + $"{def.Breed} · {def.Personality}", "cat-sub");
                var mood = M.MoodOf(id); bool alert = mood is CafeManager.Mood.Sick or CafeManager.Mood.Hungry or CafeManager.Mood.Sad;
                L(col, home ? (alert ? CafeManager.MoodWord(mood) : CatStatus(id)) : M.Data.waiting.Contains(id) ? "Waiting for a cat bed" : "", alert && home ? "catcard-status alert" : "catcard-status");

                if (home)
                {
                    var care = Add(left, "cat-box"); var cr = Add(care, "row"); L(cr, "Care", "cat-label grow"); L(cr, CafeManager.MoodWord(mood), mood == CafeManager.Mood.Content ? "charm-pill mint" : alert ? "charm-pill warn" : "charm-pill");
                    NeedBars(care, rec, false);
                    var bed = M.BedOf(id);
                    L(care, bed ? $"Sleeps in their own {bed.Def.Label.ToLower()} ({CafeData.Room(bed.Placed.room)?.Label ?? "café"}). Naps there cheer them up." : "No bed of their own yet — buy a cat bed in Decorate so they can rest.", "cat-hint");
                    var br = Add(care, "row"); br.style.justifyContent = Justify.Center;
                    var fb = B(br, $"Feed  ·  {M.Data.food} left", () => { if (M.Feed(id)) { if (cat) Fx.Hearts(cat.transform.position + Vector3.up, 8, 0.8f); AudioManager.I?.Play("sfx_purr", 0.6f); OpenCat(id); } }, "btn small mint");
                    if (M.Data.food <= 0 || rec.full >= 95) fb.AddToClassList("off");
                    if (rec.sick) { var mb = B(br, $"Give medicine  ·  {M.Data.medicine}", () => { if (M.GiveMedicine(id)) OpenCat(id); }, "btn small"); if (M.Data.medicine <= 0) mb.AddToClassList("off"); }
                    if (rec.sick) L(care, "Sick cats sleep all day and bring no charm. Medicine cures them; keep them fed and happy so it doesn't happen again.", "cat-hint");
                    else if (alert) L(care, mood == CafeManager.Mood.Hungry ? "Feed them, or keep food in stock so they eat from the food bowl." : "Pet them, let them play and visit guests — happy cats charm more customers.", "cat-hint");
                }

                var fr = Add(left, "cat-box"); var frRow = Add(fr, "row"); L(frRow, "Friendship", "cat-label grow"); Hearts(frRow, rec.Hearts);
                L(fr, rec.Hearts >= 5 ? "Best friends!" : "Stroke them in photo mode (the heart button) to grow your bond.", "cat-hint");

                var st = Add(right, "cat-box");
                int charm = home ? M.CatCharm(id) : 3 + rec.charm + (int)def.Rarity * 3;
                var cRow = Add(st, "row"); L(cRow, "Charm", "cat-label grow"); L(cRow, $"+{charm} popularity", "charm-pill");
                foreach (var (label, v, cls) in new[] { ("Playful", def.Playful, "c1"), ("Curious", def.Curious, "c2"), ("Social", def.Social, "c3"), ("Sleepy", def.Sleepy, "c4"), ("Hungry", def.Hungry, "c5") })
                {
                    var r = Add(st, "stat-row"); L(r, label, "stat-name");
                    var track = Add(r, "stat-track"); var fill = Add(track, "stat-fill " + cls); fill.style.width = Length.Percent(Mathf.Clamp01(v) * 100f);
                }

                var al = Add(right, "cat-box"); var aRow = Add(al, "row"); L(aRow, "Photo album", "cat-label grow"); L(aRow, $"{rec.poses.Count}/6", "cat-count");
                var g = Add(al, "pose-grid");
                foreach (var pose in AllPoses)
                {
                    var pr = rec.poses.Find(x => x.pose == pose); var slot = Add(g, "pose");
                    var ph = Add(slot, "pose-pic");
                    if (pr != null) { Img(ph, PhotoStore.Load(id, pose) ?? Icon("cat_" + id)); var stars = Add(slot, "pose-stars"); for (int i = 0; i < 3; i++) { var s1 = Add(stars, "star tiny"); Img(s1, Icon("ui_star")); s1.EnableInClassList("off", i >= pr.stars); } }
                    else { ph.AddToClassList("empty"); L(ph, "?", "pose-q"); }
                    L(slot, PhotoRequest.PoseWord(pose), "pose-name");
                }
                if (rec.poses.Count < 6) L(al, "Collect all 6 poses for a big charm boost.", "cat-hint");

                var row = Add(m, "row");
                if (cat)
                {
                    B(row, "Find", () => { CloseModal(); OpenTab("Cafe"); CafeCamera.I.LookAtPoint(cat.transform.position); }, "btn mint");
                    B(row, "Take photo", () => { CloseModal(); OpenTab("Cafe"); CafePhoto.I.Enter(cat); }, "btn");
                }
                B(row, "Close", CloseModal, "btn ghost");
            });
        }

        // ---- Photos
        void FillPhotos(VisualElement sheet, ScrollView sv)
        {
            if (sub == "") sub = "Wall";
            var chips = Add(sheet, "chips");
            foreach (var k in new[] { "Wall", "Album", "Whisker Feed" }) { var b = B(chips, k, () => { sub = k; RebuildSheet(); }, "chip"); b.EnableInClassList("on", sub == k); }
            sheet.Add(sv); var c = sv.contentContainer;
            if (sub == "Wall")
            {
                L(c, $"{M.Data.wall.Count}/{CafePhoto.I.FrameCount} frames filled · each framed photo adds popularity. Tap a cat in the café to take photos.", "sheet-sub");
                var g = Add(c, "grid"); foreach (var w in M.Data.wall) { var ph = Add(g, "photo"); Img(ph, CafePhoto.LoadFile(w.file)); }
            }
            else if (sub == "Album")
            {
                int found = M.Data.cats.Count; L(c, $"{found} of {GameData.Cats.Count} cats met · {M.Data.cats.Sum(x => x.poses.Count)} poses", "sheet-sub");
                var g = Add(c, "grid");
                foreach (var d in GameData.Cats)
                {
                    var rec = M.Data.Cat(d.Id); var card = Add(g, "card"); var img = Add(card, "card-img");
                    if (rec != null) { Img(img, PhotoStore.Load(d.Id, rec.poses.OrderByDescending(p => p.stars).FirstOrDefault()?.pose ?? "") ?? Icon("cat_" + d.Id)); L(card, M.Data.Name(d.Id), "card-name"); L(card, $"{rec.poses.Count}/6 poses", "card-sub"); var id = d.Id; card.RegisterCallback<ClickEvent>(_ => { TapSfx(); OpenCat(id); }); }
                    else { Img(img, Icon("cat_" + d.Id)); img.style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.25f); L(card, "???", "card-name"); L(card, d.Rarity.ToString(), "card-sub"); }
                }
            }
            else
            {
                CafePhoto.TickFeed(M.Data);
                var cloud = CafeCloud.I;
                if (cloud != null && cloud.Online)
                {
                    if (Time.unscaledTime - feedFetched > 30f || feedPosts == null) FetchFeed();
                    L(c, $"Your photos and your friends' photos · {M.Data.followers} followers", "sheet-sub");
                    if (feedPosts == null) L(c, "Loading the feed…", "small");
                    else if (feedPosts.Count == 0) L(c, "Nothing here yet — take a photo and tap Post, or add friends to see theirs.", "small");
                    else foreach (var p in feedPosts) FeedCard(c, p);
                    return;
                }
                L(c, $"{M.Data.followers} followers · likes keep arriving for a couple of hours after you post", "sheet-sub");
                foreach (var p in M.Data.feed)
                {
                    var row = Add(c, "listrow"); var ph = Add(row, "portrait"); ph.style.borderTopLeftRadius = ph.style.borderTopRightRadius = ph.style.borderBottomLeftRadius = ph.style.borderBottomRightRadius = 16;
                    Img(ph, CafePhoto.LoadFile(p.file)); var col = Add(row, "grow"); L(col, $"{M.Data.Name(p.cat)} at Paw Café", "name"); L(col, $"{p.likesShown} likes", "small");
                }
                if (M.Data.feed.Count == 0) L(c, "Nothing posted yet — take a photo and tap Post.", "small");
            }
        }

        // ---- Whisker Feed (online)
        List<CafeCloud.Post> feedPosts; HashSet<string> feedLiked = new(); float feedFetched = -999f;
        void FetchFeed()
        {
            feedFetched = Time.unscaledTime;
            CafeCloud.I.Friends(fs => { CafeCloud.I.Remember(fs);
                CafeCloud.I.Feed((posts, liked) => { if (posts == null) return; feedPosts = posts; feedLiked = liked; if (tab == "Photos" && sub == "Whisker Feed") RebuildSheet(); }); });
        }
        void FeedCard(VisualElement c, CafeCloud.Post p)
        {
            var card = Add(c, "feedcard");
            var head = Add(card, "row feed-head"); var av = Add(head, "feed-av"); Img(av, Icon("cat_" + p.cat));
            var col = Add(head, "grow"); L(col, CafeCloud.I.NameOf(p.user_id), "name"); L(col, $"{(string.IsNullOrEmpty(p.cat_name) ? p.cat : p.cat_name)}{(p.pose != null ? " · " + PhotoRequest.PoseWord(p.pose) : "")}", "small");
            var stars = Add(head, "stars"); for (int i = 0; i < 3; i++) { var st = Add(stars, "star tiny"); Img(st, Icon("ui_star")); st.EnableInClassList("off", i >= p.stars); }
            var pic = Add(card, "feed-pic");
            CafeCloud.I.Image(p.image_path, t => { if (t != null && pic.panel != null) Img(pic, t); });
            var foot = Add(card, "row feed-foot");
            bool liked = feedLiked.Contains(p.id);
            var like = new Button(() =>
            {
                bool on = !feedLiked.Contains(p.id);
                if (on) { feedLiked.Add(p.id); p.like_count++; } else { feedLiked.Remove(p.id); p.like_count = Mathf.Max(0, p.like_count - 1); }
                RebuildSheet(); CafeCloud.I.Like(p.id, on, ok => { });
                if (on) AudioManager.I?.Play("sfx_pop", 0.5f, 1.2f);
            }) { focusable = false };
            like.AddToClassList("like"); like.EnableInClassList("on", liked); foot.Add(like);
            Img(Add(like, "like-ico"), Icon(liked ? "ui_heart" : "ui_heart_empty")); L(like, p.like_count.ToString(), "like-num");
            Add(foot, "grow");
        }

        // ---- Friends: your café name, search by name, instant add, visit
        List<CafeCloud.Profile> friendsCache, searchResults; string searchText = "";
        void OpenFriends()
        {
            var cloud = CafeCloud.Ensure();
            Modal(m =>
            {
                m.AddToClassList("shop"); m.AddToClassList("friends");
                var head = Add(m, "shop-head"); L(head, "Friends", "shop-title"); Add(head, "grow");
                var x = new Button(CloseModal) { focusable = false }; x.clicked += TapSfx; x.AddToClassList("ibtn"); Img(Add(x, "ibtn-ico"), Icon("ui_close")); head.Add(x);
                if (!cloud.Online)
                {
                    L(m, "Can't reach the cat cloud right now. Check your internet connection and try again.", "modal-text");
                    B(m, "Try again", () => { cloud.StartCoroutine(cloud.Sync()); CloseModal(); }, "btn wide");
                    return;
                }
                // your café
                var me = Add(m, "cat-box"); var mr = Add(me, "row"); var av = Add(mr, "feed-av"); Img(av, Icon("cat_" + (M.Data.residents.FirstOrDefault() ?? "Mochi")));
                var mc = Add(mr, "grow"); L(mc, "Your café", "small"); L(mc, cloud.Me?.cafe_name ?? "…", "name");
                B(mr, "Rename", RenameCafe, "btn small ghost");
                // search
                var sr = Add(m, "row search-row");
                var tf = new TextField { maxLength = 20, value = searchText }; tf.AddToClassList("searchfield"); tf.textEdition.placeholder = "Search café names"; sr.Add(tf);
                B(sr, "Search", () => DoSearch(tf.value), "btn small mint");
                tf.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter) DoSearch(tf.value); });
                var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden }; sv.AddToClassList("shop-scroll"); m.Add(sv);
                var list = sv.contentContainer;
                if (searchResults != null)
                {
                    L(list, searchResults.Count == 0 ? $"No café called \"{searchText}\"…" : "Results", "list-head");
                    foreach (var p in searchResults) PersonRow(list, p, friendsCache != null && friendsCache.Any(f => f.id == p.id));
                }
                L(list, friendsCache == null ? "Loading friends…" : friendsCache.Count == 0 ? "No friends yet — search a café name and tap Add." : $"Your friends ({friendsCache.Count})", "list-head");
                if (friendsCache != null) foreach (var p in friendsCache) PersonRow(list, p, true);
            });
            if (friendsCache == null && cloud.Online) cloud.Friends(fs => { friendsCache = fs ?? new List<CafeCloud.Profile>(); cloud.Remember(fs); if (modalHost.Q(className: "friends") != null) OpenFriends(); });
        }
        void DoSearch(string q)
        {
            searchText = (q ?? "").Trim(); if (searchText.Length < 2) { Toast("Type at least 2 letters"); return; }
            CafeCloud.I.Search(searchText, res => { searchResults = res ?? new List<CafeCloud.Profile>(); if (modalHost.Q(className: "friends") != null) OpenFriends(); });
        }
        void PersonRow(VisualElement list, CafeCloud.Profile p, bool friend)
        {
            var row = Add(list, "listrow person"); var av = Add(row, "feed-av"); Img(av, Icon("cat_" + (p.avatar_cat ?? "Mochi")));
            var col = Add(row, "grow"); L(col, p.cafe_name, "name"); L(col, $"Level {p.level} · {p.popularity} popularity", "small");
            if (friend)
            {
                B(row, "Visit", () => VisitFriend(p), "btn small mint");
                var rm = new Button(() => CafeCloud.I.RemoveFriend(p.id, ok => { if (ok) { friendsCache?.RemoveAll(f => f.id == p.id); OpenFriends(); } })) { focusable = false };
                rm.clicked += TapSfx; rm.AddToClassList("xbtn"); Img(Add(rm, "ibtn-ico"), Icon("ui_close")); row.Add(rm);
            }
            else B(row, "Add", () => CafeCloud.I.AddFriend(p.id, ok =>
            {
                if (!ok) { Toast("Couldn't add right now"); return; }
                friendsCache ??= new List<CafeCloud.Profile>(); if (!friendsCache.Any(f => f.id == p.id)) friendsCache.Add(p);
                CafeCloud.I.Remember(new[] { p }); Toast($"You and {p.cafe_name} are friends!"); AudioManager.I?.Play("sfx_levelup", 0.5f, 1.3f); OpenFriends();
            }), "btn small");
        }
        void RenameCafe()
        {
            var cloud = CafeCloud.I;
            Modal(m =>
            {
                L(m, "Name your café", "modal-title");
                var tf = new TextField { maxLength = 20, value = cloud.Me?.cafe_name ?? "" }; tf.AddToClassList("namefield"); m.Add(tf);
                L(m, "Friends find you by this name. 3–20 letters, numbers or spaces.", "small");
                var row = Add(m, "row");
                B(row, "Save", () => cloud.Rename(tf.value, err => { if (err != null) Toast(err); else { Toast("Café renamed!"); OpenFriends(); } }), "btn mint");
                B(row, "Cancel", OpenFriends, "btn ghost");
                tf.schedule.Execute(() => tf.Focus()).ExecuteLater(250);
            });
        }
        VisualElement visitBar;
        void VisitFriend(CafeCloud.Profile p)
        {
            CafeCloud.I.LoadSnapshot(p.id, snap =>
            {
                if (snap == null) { Toast("Their café hasn't been shared yet — try again later"); return; }
                CloseModal(); OpenTab("Cafe"); CafeVisit.Ensure().Begin(snap, p.cafe_name);
                SetChrome(false);
                visitBar?.RemoveFromHierarchy();
                visitBar = Add(root, "visit-bar");
                var box = Add(visitBar, "visit-box"); L(box, "Visiting", "small"); L(box, p.cafe_name, "visit-name");
                B(visitBar, "Go home", EndVisit, "btn gold");
                FitAll(visitBar);
            });
        }
        void EndVisit()
        {
            CafeVisit.I?.End(); visitBar?.RemoveFromHierarchy(); visitBar = null; SetChrome(true); OpenTab("Cafe");
        }

        // ---- Shelter: a full-screen 3D page (featured cat on a pedestal) + the spotlight adoption cutscene
        static string Rar(Rarity r) => r.ToString().ToLower();
        VisualElement cine; bool cineActive;
        void FillShelterPage(VisualElement host)
        {
            var d = M.Data;
            var page = Add(host, "spage");
            L(page, "Shelter play yard", "yard-title");
            Add(page, "grow").pickingMode = PickingMode.Ignore;   // the 3D yard shows through here
            var bottom = Add(page, "sbottom");
            var pit = Add(bottom, "spity");
            L(pit, M.PullsToPity == 1 ? "Next adoption is Rare+!" : $"Rare+ guaranteed in {M.PullsToPity}", "spity-txt");
            var pips = Add(pit, "pips"); for (int i = 0; i < CafeData.PityAt; i++) { var p = Add(pips, "pip"); p.EnableInClassList("on", i < d.pity); if (i == CafeData.PityAt - 1) p.AddToClassList("last"); }
            bool ok = M.CanAdopt(out var why);
            var row = Add(bottom, "srow");
            var scol = Add(row, "scol");
            var sh = new Button(OpenShardShop) { focusable = false }; sh.clicked += TapSfx; sh.AddToClassList("sround"); scol.Add(sh);
            var si = Add(sh, "sround-ico shard"); Img(si, Icon("ui_sparkle")); L(sh, d.shards.ToString("N0"), "sround-lbl"); L(scol, "Shards", "scap");
            var b = new Button(ShelterDraw) { focusable = false }; b.clicked += TapSfx; b.AddToClassList("adopt"); if (!ok) b.AddToClassList("off"); row.Add(b); adoptBtn = b;
            L(b, "Adopt", "adopt-lbl");
            if (ok) { var cost = Add(b, "adopt-cost"); Img(Add(cost, "adopt-ico"), Icon("ui_paw")); L(cost, "1", "adopt-num"); Img(Add(cost, "adopt-ico"), Icon("ui_fish")); L(cost, M.AdoptFee == 0 ? "Free" : M.AdoptFee.ToString("N0"), "adopt-num"); }
            else L(b, d.tickets <= 0 ? "No ticket yet" : M.FreeBeds <= 0 ? "Needs a free bed" : $"Need {M.AdoptFee:N0} coins", "adopt-why");
            var ocol = Add(row, "scol");
            var od = new Button(OpenOdds) { focusable = false }; od.clicked += TapSfx; od.AddToClassList("sround"); ocol.Add(od);
            Img(Add(od, "sround-ico big"), Icon("ui_help")); L(ocol, "Odds", "scap");
            var nf = M.NextFreeTicketIn;
            L(bottom, ok ? $"Free ticket in {(nf.TotalHours >= 24 ? $"{nf.Days}d {nf.Hours}h" : $"{Mathf.Max(0, nf.Hours)}h {Mathf.Max(0, nf.Minutes)}m")}" : why, ok ? "shelter-why ok" : "shelter-why");
        }
        public void DrawTicket() => ShelterDraw();
        void ShelterDraw()
        {
            cineActive = true;   // level-ups from the adoption XP wait until the minigame is over
            if (!M.Pull(out var res)) { cineActive = false; RebuildSheet(); return; }
            PlayCutscene(res);
        }
        void SetChrome(bool on)
        {
            root.Q(className: "tabs").style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q(className: "top").style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
        float lureMeter; Vector2 lastWave; bool waving, luring;
        VisualElement feather, meterFill, lureBox; Label lureText;
        /// <summary>Adoption minigame: wave the feather until the cats are curious, then one of them chooses you.</summary>
        void PlayCutscene(CafeManager.PullResult res)
        {
            var stage = ShelterStage.Ensure(); if (!stage.Showing) stage.Show(true);
            cineActive = true; sheetHost.Clear(); SetChrome(false);
            cine.Clear(); cine.RemoveFromClassList("hidden");
            lureMeter = 0; luring = true; waving = false;
            var catcher = Add(cine, "layer");
            lureBox = Add(cine, "lure-box"); lureBox.pickingMode = PickingMode.Ignore;
            lureText = L(lureBox, "Wave the feather to catch their eye!", "lure-text");
            var track = Add(lureBox, "lure-track"); meterFill = Add(track, "lure-fill"); meterFill.style.width = Length.Percent(0);
            IgnoreTree(lureBox);
            feather = Add(cine, "feather"); feather.pickingMode = PickingMode.Ignore; Img(feather, Icon("ui_feather"));
            var size = root.panel.visualTree.layout.size; feather.style.left = size.x * 0.5f; feather.style.top = size.y * 0.55f;
            // idle bob until the player touches the screen
            feather.schedule.Execute(() => { if (!waving && luring) feather.style.rotate = new Rotate(Mathf.Sin(Time.unscaledTime * 3f) * 18f); }).Every(16);
            catcher.RegisterCallback<PointerDownEvent>(e => { if (luring) { waving = true; lastWave = e.position; MoveFeather(e.position, 0); } else stage.Skip(); });
            catcher.RegisterCallback<PointerMoveEvent>(e => { if (luring && waving) MoveFeather(e.position, (((Vector2)e.position) - lastWave).magnitude); });
            catcher.RegisterCallback<PointerUpEvent>(_ => waving = false);
            catcher.RegisterCallback<PointerLeaveEvent>(_ => waving = false);
            pendingRes = res;
        }
        CafeManager.PullResult pendingRes;
        /// <summary>Feather follows the finger; distance waved fills the curiosity meter; cats look at the feather.</summary>
        void MoveFeather(Vector2 panelPos, float moved)
        {
            var stage = ShelterStage.I; if (!stage || !luring) return;
            feather.style.left = panelPos.x; feather.style.top = panelPos.y;
            float dx = panelPos.x - lastWave.x; feather.style.rotate = new Rotate(Mathf.Clamp(dx * 1.5f, -35f, 35f));
            lastWave = panelPos;
            var size = root.panel.visualTree.layout.size; var cam = stage.Cam;
            stage.Wave(new Vector2(panelPos.x / size.x * cam.pixelWidth, (1f - panelPos.y / size.y) * cam.pixelHeight));
            lureMeter = Mathf.Clamp01(lureMeter + moved / 2600f);
            meterFill.style.width = Length.Percent(lureMeter * 100f);
            if (moved > 30f && UnityEngine.Random.value < 0.05f) AudioManager.I?.Play("sfx_tap", 0.3f, UnityEngine.Random.Range(1.2f, 1.5f));
            if (lureMeter >= 1f) StartChoosing();
        }
        /// <summary>Editor QA: wave the feather across the screen programmatically.</summary>
        public void SimWave(float amount)
        {
            var size = root.panel.visualTree.layout.size; lastWave = new Vector2(size.x * 0.5f, size.y * 0.5f);
            for (int i = 0; i < 30 && luring; i++) { var p = new Vector2(size.x * (0.3f + 0.4f * (i % 2)), size.y * (0.45f + 0.03f * (i % 3))); MoveFeather(p, amount / 30f * 2600f); }
        }
        void StartChoosing()
        {
            if (!luring) return; luring = false; waving = false;
            var stage = ShelterStage.I; var res = pendingRes;
            feather.RemoveFromHierarchy(); lureText.text = "Someone has chosen you…"; meterFill.parent.style.display = DisplayStyle.None;
            var hint = L(cine, "Tap to skip", "skip-hint"); hint.pickingMode = PickingMode.Ignore;
            AudioManager.I?.Play("sfx_levelup", 0.4f, 1.3f);
            stage.StartCoroutine(stage.Choose(res.Cat, res.Tier, () => { hint.RemoveFromHierarchy(); lureBox.RemoveFromHierarchy(); foreach (var l in cine.Children().Where(c => c.ClassListContains("layer")).ToList()) l.RemoveFromHierarchy(); ShowRevealCard(res); }));
        }
        void ShowRevealCard(CafeManager.PullResult res)
        {
            foreach (var old in cine.Query(className: "rcard").ToList()) old.RemoveFromHierarchy();
            var cat = res.Cat; string id = cat.Id; string r = Rar(res.Tier);
            var card = Add(cine, "rcard r-" + r);
            var tags = Add(card, "row"); tags.style.justifyContent = Justify.Center;
            L(tags, res.Tier == Rarity.Legendary ? "LEGENDARY!" : res.Tier.ToString(), "rarity-chip big");
            if (res.Pity) L(tags, "Guaranteed!", "tag guar");
            L(tags, res.Duplicate ? $"+{res.Shards} shards" : "NEW!", res.Duplicate ? "tag dup" : "tag new");
            L(card, M.Data.Name(id), "rcard-name");
            L(card, res.Duplicate ? $"{M.Data.Name(id)} already lives with you — they came to say hi." : $"{cat.Breed} · {cat.Personality}", "rcard-sub");
            var row = Add(card, "row"); row.style.justifyContent = Justify.Center;
            if (!res.Duplicate) B(row, "Give a name", () => Rename(id, () => { CloseModal(); ShowRevealCard(res); }), "btn mint");
            B(row, res.Duplicate ? "Nice!" : "Welcome home!", EndCutscene, "btn");
            card.AddToClassList("off"); card.schedule.Execute(() => card.RemoveFromClassList("off")).ExecuteLater(30);
            FitAll(card); Confetti(cine, (int)res.Tier);
        }
        void EndCutscene()
        {
            cineActive = false; cine.Clear(); cine.AddToClassList("hidden"); SetChrome(true);
            luring = false; ShelterStage.I?.BackToYard(); RebuildSheet();
            if (pendingLevel > 0) { int lv = pendingLevel; pendingLevel = 0; OnLevelUp(lv); }
        }

        void OpenOdds()
        {
            Modal(m =>
            {
                L(m, "Odds & rules", "modal-title");
                var box = Add(m, "cat-box"); box.style.alignSelf = Align.Stretch;
                foreach (var (r, pct) in new[] { (Rarity.Common, CafeData.TicketOdds[0]), (Rarity.Uncommon, CafeData.TicketOdds[1]), (Rarity.Rare, CafeData.TicketOdds[2]), (Rarity.Legendary, CafeData.TicketOdds[3]) })
                { var row = Add(box, "row odds-row r-" + Rar(r)); L(row, r.ToString(), "rarity-chip big"); Add(row, "grow"); L(row, pct + "%", "odds-pct"); }
                L(m, $"The {CafeData.PityAt}th adoption without a Rare is always Rare or better.\nDuplicates turn into paw shards.", "modal-text");
                L(m, $"Tickets: free every {CafeData.FreeTicketDays} days, every {CafeData.LevelsPerTicket} levels, every {CafeData.FollowersPerTicket} followers.\nEach cat needs its own bed; the fee grows with every cat.", "modal-text");
                B(m, "Got it", CloseModal, "btn wide");
            });
        }
        void Confetti(VisualElement host, int tier)
        {
            Color[] cols = { new(1f, 0.56f, 0.67f), new(0.5f, 0.82f, 0.68f), new(1f, 0.79f, 0.29f), new(0.61f, 0.79f, 0.94f), new(0.73f, 0.63f, 0.9f) };
            var size = root.layout.size; int n = 26 + tier * 14;
            for (int i = 0; i < n; i++)
            {
                var c = Add(host, "confetti"); c.pickingMode = PickingMode.Ignore;
                c.style.backgroundColor = cols[UnityEngine.Random.Range(0, cols.Length)];
                float x0 = size.x * 0.5f, y0 = size.y * 0.42f, ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f), sp = UnityEngine.Random.Range(220f, 520f), rot0 = UnityEngine.Random.Range(0, 360f);
                float vx = Mathf.Cos(ang) * sp, vy = Mathf.Sin(ang) * sp - 260f;
                c.experimental.animation.Start(0f, 1f, 1400 + UnityEngine.Random.Range(0, 500), (el, t) =>
                {
                    float tt = t * 1.6f;
                    el.style.left = x0 + vx * tt; el.style.top = y0 + vy * tt + 520f * tt * tt;
                    el.style.rotate = new Rotate(rot0 + t * 720f); el.style.opacity = 1f - t * t;
                }).OnCompleted(() => c.RemoveFromHierarchy());
            }
        }
        void OpenShardShop()
        {
            var list = GameData.Cats.Where(x => !M.Data.residents.Contains(x.Id) && !M.Data.waiting.Contains(x.Id)).OrderByDescending(x => x.Rarity).ThenBy(x => x.Id).ToList();
            Modal(m =>
            {
                m.AddToClassList("catmodal");
                var head = Add(m, "row"); L(head, "Shard exchange", "modal-title grow"); var pill = Add(head, "charm-pill shard"); L(pill, $"{M.Data.shards} shards", null);
                L(m, M.FreeBeds > 0 ? "Pick any cat you don't have yet." : "Every cat needs its own bed — buy a cat bed in Decorate first.", "modal-text");
                var sv = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden }; sv.AddToClassList("shard-list"); m.Add(sv);
                var grid = Add(sv.contentContainer, "catgrid");
                foreach (var d in list)
                {
                    var card = Add(grid, "catcard small r-" + Rar(d.Rarity));
                    var ring = Add(card, "cat-ring"); Img(Add(ring, "cat-pic"), Icon("cat_" + d.Id));
                    L(card, d.Rarity.ToString(), "rarity-chip");
                    L(card, d.Id, "catcard-name"); L(card, d.Breed, "catcard-sub");
                    int price = M.ShardPrice(d); var id = d.Id;
                    var b = B(card, $"{price} shards", () => { if (M.ShardAdopt(id)) { CloseModal(); PlayCutscene(new CafeManager.PullResult { Cat = GameData.Cat(id), Tier = GameData.Cat(id).Rarity }); } }, "btn small mint");
                    if (M.Data.shards < price || M.FreeBeds <= 0) b.AddToClassList("off");
                }
                B(m, "Close", CloseModal, "btn ghost wide");
            });
        }

        /// <summary>Let the player give a cat their own name (12 letters max; empty = original name).</summary>
        public void Rename(string id, Action after)
        {
            var rec = M.Data.CatOrNew(id);
            Modal(m =>
            {
                L(m, "Name your cat", "modal-title");
                var ring = Add(m, "cat-ring big"); Img(Add(ring, "cat-pic"), Icon("cat_" + id));
                var tf = new TextField { maxLength = 12, value = string.IsNullOrWhiteSpace(rec.nickname) ? "" : rec.nickname }; tf.AddToClassList("namefield");
                tf.textEdition.placeholder = id; m.Add(tf);
                L(m, $"Leave it empty to keep \"{id}\".", "small");
                var row = Add(m, "row"); row.style.justifyContent = Justify.Center;
                B(row, "Save", () =>
                {
                    var n = (tf.value ?? "").Trim(); if (n.Length > 12) n = n.Substring(0, 12);
                    rec.nickname = n.Length == 0 || n == id ? null : n; M.Save(); M.Recalc();
                    if (rec.nickname != null) Toast($"Say hello to {rec.nickname}!");
                    after?.Invoke();
                }, "btn mint");
                B(row, "Cancel", () => after?.Invoke(), "btn ghost");
                tf.schedule.Execute(() => tf.Focus()).ExecuteLater(250);
            });
        }

        // ------------------------------------------------------------ world bubbles
        void LateUpdate()
        {
            var docRoot = GetComponent<UIDocument>().rootVisualElement;
            if (docRoot != null && (docRoot != root || root.childCount == 0)) { bubbles.Clear(); tabBtns.Clear(); Build(); Refresh(); }
            if (root?.panel == null || M == null) return;
            if (Screen.safeArea != lastSafe || lastScreen.x != Screen.width || lastScreen.y != Screen.height) ApplySafeArea();
            var cam = CafeCamera.I.Cam; var seen = new HashSet<object>();
            bool hideWorld = CafePhoto.Active || tab == "Shelter" || CafeVisit.Active;
            if (CafeVisit.Active) foreach (var (t, n) in CafeVisit.I.Labels) if (t) { var e = Bubble3D(t, "lock", t.position + Vector3.up * 1.0f, seen); SetBubble(e, null, n); }
            TickArcade(Time.unscaledDeltaTime); UpdateRequests();
            UpdateGuide(hideWorld);
            PositionFloatTools();
            foreach (var cu in M.Customers)
            {
                if (!cu || hideWorld) continue;
                if (cu.Bubble == Bubble.None) continue;
                string cls = cu.Bubble == Bubble.Photo ? "photo" : cu.Bubble == Bubble.Coins ? "coins" : "";
                var e = Bubble3D(cu, cls, cu.transform.position + Vector3.up * 2.05f, seen);
                if (cu.Bubble == Bubble.Photo && cu.Request != null)
                {
                    // details live in the request card stack on the right; over the customer just a small camera marker
                    SetBubble(e, "ui_cam", null); e.EnableInClassList("anycat", false); e.EnableInClassList("reqmark", true);
                }
                else if (cu.Bubble == Bubble.Order && cu.Order != null) { SetBubble(e, "furn_" + cu.Order.Prop, null); e.AddToClassList("order"); }
                else SetBubble(e, cu.Bubble == Bubble.Heart ? "ui_heart" : cu.Bubble == Bubble.Coins ? "ui_fish" : null, null);
                if (cu.Bubble != Bubble.Order) e.RemoveFromClassList("order");
            }
            if (!hideWorld && M.Data.tipJar > 0)
            {
                var e = Bubble3D("tip", "coins", M.TipJarPoint.position + Vector3.up * 0.75f, seen); SetBubble(e, "ui_fish", M.Data.tipJar.ToString("N0"));
                e.AddToClassList("tipjar"); float k = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4.2f); e.style.scale = new Scale(new Vector3(k, k, 1));   // "tap me"
            }
            if (!hideWorld) foreach (var k in M.Cats)
            {
                if (!k) continue; var mood = M.MoodOf(k.Def.Id);
                if (mood is not (CafeManager.Mood.Sick or CafeManager.Mood.Hungry or CafeManager.Mood.Sad)) continue;
                var e = Bubble3D(k, "care", k.transform.position + Vector3.up * 1.05f, seen); SetBubble(e, MoodIcon(mood), null);
                e.EnableInClassList("sick", mood == CafeManager.Mood.Sick);
                float bob = 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 3.5f); e.style.scale = new Scale(new Vector3(bob, bob, 1));
            }
            foreach (var s in FindObjectsByType<Stray>()) if (!hideWorld && s.isActiveAndEnabled)
            {
                // a sad little rain cloud until it's been fed, then a paw ("come play with me")
                var e = Bubble3D(s, "stray", s.transform.position + Vector3.up * 0.85f, seen); SetBubble(e, s.Rec != null && s.Rec.trust >= 1 ? "ui_paw" : "ui_rain", null);
                float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 4f; e.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100 - bob));
            }
            foreach (var r in CafeData.Rooms) if (!hideWorld && !M.RoomOpen(r.Id)) { var sign = GameObject.Find("Sign_" + r.Id); if (sign) { var e = Bubble3D("room" + r.Id, "lock", sign.transform.position, seen); SetBubble(e, null, $"{r.Label} · Lv {r.Unlock}"); } }
            foreach (var k in bubbles.Keys.Where(k => !seen.Contains(k)).ToList()) { bubbles[k].RemoveFromHierarchy(); bubbles.Remove(k); }
            if (toastT > 0 && (toastT -= Time.unscaledDeltaTime) <= 0) toast.RemoveFromClassList("on");
            if (flash.ClassListContains("on")) flash.RemoveFromClassList("on");
            if (vf != null && !vf.ClassListContains("hidden")) UpdateViewfinder();
        }
        VisualElement Bubble3D(object key, string cls, Vector3 wp, HashSet<object> seen)
        {
            seen.Add(key);
            if (!bubbles.TryGetValue(key, out var e))
            {
                e = Add(world, "wb"); e.pickingMode = PickingMode.Ignore; var ic = Add(e, "wb-ico"); ic.pickingMode = PickingMode.Ignore; var t = L(e, "", "wb-txt"); t.pickingMode = PickingMode.Ignore;
                var badge = Add(e, "wb-badge"); badge.pickingMode = PickingMode.Ignore; Img(badge, Icon("ui_cam"));
                bubbles[key] = e;
            }
            foreach (var c in new[] { "photo", "coins", "paw", "lock", "care", "stray" }) e.EnableInClassList(c, c == cls);
            var sp = CafeCamera.I.Cam.WorldToScreenPoint(wp);
            e.style.display = sp.z > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var pp = WorldToPanel(sp);
            e.style.left = pp.x; e.style.top = Mathf.Max(pp.y, 178f); return e;   // keep clear of the top bar
        }
        /// <summary>Camera pixel point → panel point, via the camera's own pixel size (works when rendering to a texture too).</summary>
        Vector2 WorldToPanel(Vector3 sp)
        {
            var cam = CafeCamera.I.Cam; var size = root.panel.visualTree.layout.size;
            return new Vector2(sp.x / Mathf.Max(1, cam.pixelWidth) * size.x, (1f - sp.y / Mathf.Max(1, cam.pixelHeight)) * size.y);
        }
        static void SetBubble(VisualElement e, string icon, string text)
        {
            var ic = e.Q(className: "wb-ico"); ic.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None; if (icon != null) Img(ic, Icon(icon));
            var t = e.Q<Label>(className: "wb-txt"); t.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None; if (text != null) t.text = text;
        }
        public void Float(string text, Vector3 wp)
        {
            var sp = CafeCamera.I.Cam.WorldToScreenPoint(wp + Vector3.up * 0.8f); var pp = WorldToPanel(sp);
            var l = L(world, text, "float"); l.style.left = pp.x; l.style.top = pp.y;
            l.experimental.animation.Start(0f, 1f, 1100, (el, t) => { el.style.top = pp.y - 120 * t; el.style.opacity = 1 - t * t; }).OnCompleted(() => l.RemoveFromHierarchy());
        }

        // ------------------------------------------------------------ viewfinder
        Label vfStars, vfHintL; VisualElement vfStarRow, vfBond, vfBondHearts, vfBondFill, vfHint; float bondPulse;
        static int PetsEver { get => PlayerPrefs.GetInt("pawcafe.pets", 0); set => PlayerPrefs.SetInt("pawcafe.pets", value); }

        /// <summary>Petting feedback: hearts pop out under the finger; a full friendship heart gets a big message.</summary>
        void OnPetted(CafeCat cat, bool heartUp)
        {
            if (root?.panel == null || !cat) return;
            PetsEver = PetsEver + 1;
            var sp = CafeInput.LastPetScreen;
            var pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y)) - world.worldBound.position;
            int n = heartUp ? 7 : 3;
            for (int i = 0; i < n; i++)
            {
                var h = Add(world, "pet-heart"); h.pickingMode = PickingMode.Ignore; Img(h, Icon("ui_heart"));
                float dx = UnityEngine.Random.Range(-60f, 60f), rise = UnityEngine.Random.Range(110f, 190f), rot = UnityEngine.Random.Range(-25f, 25f), sz = heartUp ? 1.3f : 1f;
                h.style.left = pp.x - 30; h.style.top = pp.y - 30;
                h.experimental.animation.Start(0f, 1f, 900 + i * 60, (e, t) =>
                {
                    float k = t < 0.2f ? t / 0.2f : 1f;
                    e.style.translate = new Translate(dx * t, -rise * t); e.style.rotate = new Rotate(rot * t);
                    e.style.scale = new Scale(Vector3.one * sz * (0.4f + 0.8f * k)); e.style.opacity = 1f - t * t;
                }).OnCompleted(() => h.RemoveFromHierarchy());
            }
            if (heartUp)
            {
                var rec = M.Data.CatOrNew(cat.Def.Id);
                Toast(rec.Hearts >= 5 ? $"{M.Data.Name(cat.Def.Id)} adores you — best friends!" : $"{M.Data.Name(cat.Def.Id)} trusts you more! Friendship {rec.Hearts}/5");
                AudioManager.I?.Meow(1.2f); bondPulse = 1f;
            }
        }
        void BuildViewfinder()
        {
            Add(vf, "vf-frame").pickingMode = PickingMode.Ignore;
            var top = Add(vf, "vf-top"); top.pickingMode = PickingMode.Ignore; vfReqBox = Add(top); vfName = L(top, "", "vf-name");
            vfStarRow = Add(top, "stars");
            for (int i = 0; i < 3; i++) { var s = Add(vfStarRow, "star"); Img(s, Icon("ui_star")); }
            vfBond = Add(top, "vf-bond"); L(vfBond, "Friendship", "vf-bond-lbl"); vfBondHearts = Add(vfBond, "vf-bond-hearts");
            for (int i = 0; i < 5; i++) { var h = Add(vfBondHearts, "vf-bheart"); Img(h, Icon("ui_heart")); }
            var bar = Add(vfBond, "vf-bond-bar"); vfBondFill = Add(bar, "vf-bond-fill");
            vfHint = Add(vf, "vf-hint"); vfHint.pickingMode = PickingMode.Ignore; Img(Add(vfHint, "vf-hint-ico"), Icon("ui_heart")); vfHintL = L(vfHint, "", "vf-hint-txt");
            IgnoreTree(top); IgnoreTree(vfHint);
            var bottom = Add(vf, "vf-bottom");
            var close = new Button(() => CafePhoto.I.Exit()) { focusable = false }; close.clicked += TapSfx; close.AddToClassList("ibtn"); Img(Add(close, "ibtn-ico"), Icon("ui_close")); bottom.Add(close);
            var sh = new Button(() => { CafePhoto.I.Shutter(); flash.AddToClassList("on"); }) { focusable = false }; sh.AddToClassList("shutter"); bottom.Add(sh); shutterBtn = sh;
        }
        public void ShowViewfinder(bool on) { vf.EnableInClassList("hidden", !on); sheetHost.style.display = on ? DisplayStyle.None : DisplayStyle.Flex; root.Q(className: "tabs").style.display = on ? DisplayStyle.None : DisplayStyle.Flex; root.Q(className: "top").style.display = on ? DisplayStyle.None : DisplayStyle.Flex; }
        void UpdateViewfinder()
        {
            var ph = CafePhoto.I; if (!CafePhoto.Active) return;
            // friendship meter + "stroke to pet" hint (resident cats only)
            bool resident = ph.Cat && !ph.StrayTarget;
            vfBond.style.display = resident ? DisplayStyle.Flex : DisplayStyle.None;
            if (resident)
            {
                var rec = M.Data.CatOrNew(ph.CatId); int hs = rec.Hearts, k = 0;
                foreach (var h in vfBondHearts.Children()) h.EnableInClassList("off", k++ >= hs);
                vfBondFill.style.width = Length.Percent(hs >= 5 ? 100f : (rec.bond % 20) / 20f * 100f);
                if (bondPulse > 0) { bondPulse = Mathf.Max(0, bondPulse - Time.unscaledDeltaTime * 2f); float s = 1f + 0.15f * Mathf.Sin(bondPulse * Mathf.PI); vfBond.style.scale = new Scale(new Vector3(s, s, 1)); }
            }
            bool hint = resident && PetsEver < 6;
            vfHint.style.display = hint ? DisplayStyle.Flex : DisplayStyle.None;
            if (hint) { vfHintL.text = $"Stroke {M.Data.Name(ph.CatId)} to pet"; float b = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f); vfHint.style.scale = new Scale(new Vector3(b, b, 1)); }
            vfName.text = ph.StrayTarget ? $"{ph.CatId} (stray)" : $"{M.Data.Name(ph.CatId)} · {PhotoRequest.PoseWord(ph.LivePose)}";
            int st = ph.LiveStars; int i = 0; foreach (var s in vfStarRow.Children()) s.EnableInClassList("off", i++ >= st);
            vfReqBox.Clear();
            foreach (var c in M.Customers.Where(c => c && c.Request != null).Take(3))
            {
                var box = Add(vfReqBox, "vf-req"); bool ok = c.Request.Matches(ph.CatId, ph.LivePose);
                box.style.flexDirection = FlexDirection.Row; box.style.alignItems = Align.Center;
                var thumb = Add(box, "vf-req-pic"); Img(thumb, Icon(string.IsNullOrEmpty(c.Request.Cat) ? "ui_paw" : "cat_" + c.Request.Cat));
                L(box, (ok ? "Ready! " : "") + $"Request: {c.Request.Label}  ({Mathf.CeilToInt(c.Request.TimeLeft)}s)", "vf-req-text");
                box.style.backgroundColor = ok ? new Color(0.5f, 0.82f, 0.68f, 0.95f) : new Color(1f, 0.56f, 0.67f, 0.95f);
            }
        }
        void OnSnapped(Texture2D tex, string cat, string pose, int stars, bool fulfilled)
        {
            Modal(m =>
            {
                var pol = Add(m, "polaroid"); Img(pol, tex);
                var row = Add(m, "stars"); for (int i = 0; i < 3; i++) { var s = Add(row, "star"); Img(s, Icon("ui_star")); s.EnableInClassList("off", i >= stars); }
                L(m, $"{M.Data.Name(cat)} · {PhotoRequest.PoseWord(pose)}" + (fulfilled ? "  ·  request done! Big tip coming" : ""), "modal-text");
                var r = Add(m, "row");
                B(r, "Pin to wall", () => { if (CafePhoto.I.PinToWall(tex, cat, pose, stars)) { Toast("Pinned to the café wall"); CloseModal(); } }, "btn mint");
                B(r, "Post", () => { CafePhoto.I.Post(tex, cat, stars); Toast("Posted to Whisker Feed — likes are coming in"); CloseModal(); }, "btn");
                B(r, "Keep", CloseModal, "btn ghost");
            });
        }

        // ------------------------------------------------------------ modals
        void Modal(Action<VisualElement> fill)
        {
            bool reopen = !modalHost.ClassListContains("hidden") && modalHost.childCount > 0;   // e.g. settings toggles rebuild in place: no pop
            modalHost.Clear(); modalHost.RemoveFromClassList("hidden"); modalHost.AddToClassList("scrim");
            var m = Add(modalHost, "modal"); fill(m); FitAll(m);
            if (!reopen) { m.AddToClassList("modal-in"); m.schedule.Execute(() => m.RemoveFromClassList("modal-in")).ExecuteLater(20); }
        }
        public void CloseModal() { modalHost.Clear(); modalHost.AddToClassList("hidden"); }
        public void OpenStray(Stray s)
        {
            Modal(m =>
            {
                L(m, s.Rec.trust >= 1 ? $"{s.Def.Id} the stray" : "A stray in the rain", "modal-title"); var pic = Add(m, "big-portrait"); Img(pic, Icon("cat_" + s.Def.Id));
                if (s.Rec.trust < 1) pic.style.unityBackgroundImageTintColor = new Color(0.72f, 0.77f, 0.92f);
                L(m, s.Rec.trust >= 1 ? $"{s.Def.Breed} · {s.Def.Rarity}. Still very shy — earn their trust step by step. If you scare them, they run off until the next rain." : $"Soaked, shivering and so hungry… this little {s.Def.Breed.ToLower()} hasn't eaten in days.", "modal-text");
                string[] steps = { $"Give some food ({Stray.TreatCost} coins)", "Approach gently  ·  minigame", "Guide them home  ·  minigame", "Take their photo" };
                string me = M.Data.residents.Count > 0 ? M.Data.residents[0] : "Mochi";
                for (int i = 0; i < steps.Length; i++)
                {
                    var st = Add(m, "step"); st.EnableInClassList("done", s.Rec.trust > i); L(st, s.Rec.trust > i ? "OK" : (i + 1).ToString(), "step-dot");
                    L(st, steps[i], "grow");
                    if (s.Rec.trust == i)
                    {
                        int k = i;
                        B(st, k is 1 or 2 ? "Play" : "Go", () =>
                        {
                            CloseModal();
                            if (k == 0) s.Treat();
                            else if (k == 1) PlayGame("redlight", me, "stray", s.Def, won => { if (!s) return; if (won) s.Approached(); else s.Flee(); });
                            else if (k == 2) PlayGame("rooftop", s.Def.Id, "stray", s.Def, won => { if (!s) return; if (won) s.Guided(); else s.Flee(); });
                            else CafePhoto.I.Enter(s);
                        }, "btn small mint");
                    }
                }
                var r = Add(m, "row");
                if (s.Rec.trust >= 4) { var ab = B(r, M.FreeBeds > 0 ? "Adopt!" : "Adopt (needs a free cat bed)", () => { s.Adopt(); if (M.Data.residents.Contains(s.Def.Id) || M.Data.waiting.Contains(s.Def.Id)) { CloseModal(); RebuildSheet(); } }, "btn gold"); if (M.FreeBeds <= 0) ab.AddToClassList("off"); }
                B(r, "Later", CloseModal, "btn ghost");
            });
        }
        public void OpenRoomUnlock(string id)
        {
            var r = CafeData.Room(id);
            Modal(m =>
            {
                L(m, r.Label, "modal-title"); L(m, CafeData.RoomBlurb(id), "modal-text");
                if (M.Data.level < r.Unlock) LockPill(m, r.Unlock);
                var row = Add(m, "row");
                if (M.Data.level >= r.Unlock) B(row, $"Open for {r.Cost} coins", () => { if (M.TryUnlockRoom(id)) CloseModal(); }, "btn gold");
                B(row, M.Data.level >= r.Unlock ? "Not now" : "OK", CloseModal, "btn ghost");
            });
        }
        void OnWelcome(long coins, float hours)
        {
            Modal(m =>
            {
                L(m, "Welcome back!", "modal-title");
                L(m, $"Your café served customers for {(hours < 1 ? Mathf.RoundToInt(hours * 60) + " minutes" : hours.ToString("0.#") + " hours")} while you were away.", "modal-text");
                L(m, $"+{coins:N0} coins in the tip jar", "modal-title");
                B(m, "Collect", () => { long n = M.CollectTips(); Fx.Coins(M.TipJarPoint.position + Vector3.up * 0.4f, 20); CloseModal(); }, "btn gold");
            });
        }
        int pendingLevel;
        void OnLevelUp(int lv)
        {
            if (cineActive || ArcadeActive) { pendingLevel = lv; return; }   // shown after the adoption cutscene / the minigame
            var unlocks = CafeData.Furniture.Where(f => f.Unlock == lv).Select(f => f.Label).Concat(CafeData.Menu.Where(x => x.Unlock == lv).Select(x => x.Label)).Concat(CafeData.Rooms.Where(x => x.Unlock == lv).Select(x => x.Label)).ToList();
            Modal(m =>
            {
                L(m, $"Café level {lv}!", "modal-title");
                L(m, unlocks.Count > 0 ? "New: " + string.Join(", ", unlocks) : "Your café is getting famous.", "modal-text");
                if (lv % CafeData.LevelsPerTicket == 0) L(m, "+1 adoption ticket", "modal-text");
                B(m, "Yay!", CloseModal, "btn");
            });
        }
        void OpenSettings()
        {
            Modal(m =>
            {
                L(m, "Settings", "modal-title");
                B(m, $"Music  ·  {(M.Data.music ? "ON" : "OFF")}", () => { M.Data.music = !M.Data.music; AudioManager.MusicOn = M.Data.music; AudioManager.I?.Apply(); M.Save(); OpenSettings(); }, M.Data.music ? "btn mint wide" : "btn ghost wide");
                B(m, $"Sounds  ·  {(M.Data.sfx ? "ON" : "OFF")}", () => { M.Data.sfx = !M.Data.sfx; AudioManager.SfxOn = M.Data.sfx; AudioManager.I?.Apply(); M.Save(); OpenSettings(); }, M.Data.sfx ? "btn mint wide" : "btn ghost wide");
                B(m, "Done", CloseModal, "btn wide");
            });
        }
        public void Toast(string msg) { toastText.text = msg; toast.EnableInClassList("vf", CafePhoto.Active); toast.AddToClassList("on"); toastT = 2.6f; }   // lower in camera mode (clear of the cat label)
    }
}
