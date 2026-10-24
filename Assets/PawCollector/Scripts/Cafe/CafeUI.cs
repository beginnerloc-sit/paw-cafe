using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe
{
    /// <summary>All café UI (UI Toolkit, built in code): top bar, five tabs, world bubbles, viewfinder, modals, toasts.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class CafeUI : MonoBehaviour
    {
        public static CafeUI I { get; private set; }
        VisualElement coach; Label coachText;
        VisualElement root, world, sheetHost, modalHost, vf, flash, toast, tools, vfReqBox; Label toastText, coinsL, ticketsL, popL, lvlL, vfName; VisualElement lvlFill;
        readonly Dictionary<string, Button> tabBtns = new(); string tab = "Cafe"; string sub = "";
        readonly Dictionary<object, VisualElement> bubbles = new(); float toastT;
        CafeManager M => CafeManager.I;

        void Awake() { I = this; }
        void OnEnable()
        {
            var doc = GetComponent<UIDocument>(); root = doc.rootVisualElement; root.Clear();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Cafe")); root.AddToClassList("root");
            world = Add(root, "layer"); world.pickingMode = PickingMode.Ignore;
            BuildTop(); sheetHost = Add(root, "layer"); sheetHost.pickingMode = PickingMode.Ignore;
            tools = Add(root, "hidden"); BuildTabs();
            vf = Add(root, "layer vf hidden"); vf.pickingMode = PickingMode.Ignore; BuildViewfinder();
            flash = Add(root, "layer flash"); flash.pickingMode = PickingMode.Ignore;
            modalHost = Add(root, "layer hidden");
            coach = Add(root, "coach hidden"); coach.pickingMode = PickingMode.Ignore; var cb = Add(coach, "coach-box"); var cp = Add(cb, "coach-ico"); Img(cp, Icon("ui_paw")); coachText = L(cb, "", "coach-text");
            toast = Add(root, "toast"); toast.pickingMode = PickingMode.Ignore; var tb = Add(toast, "toast-box"); toastText = L(tb, "", "toast-text");
            ApplySafeArea();
        }
        void Start()
        {
            M.Changed += Refresh; M.Toast += Toast; M.LevelUp += OnLevelUp; M.WelcomeBack += OnWelcome;
            CafePhoto.I.Snapped += OnSnapped; Refresh();
        }
        void ApplySafeArea()
        {
            var sa = Screen.safeArea; float k = 880f / Mathf.Max(1, Screen.width);
            root.style.paddingTop = (Screen.height - sa.yMax) * k; root.style.paddingBottom = sa.yMin * k;
        }

        // ------------------------------------------------------------ helpers
        static VisualElement Add(VisualElement p, string cls = null) { var e = new VisualElement(); if (cls != null) foreach (var c in cls.Split(' ')) e.AddToClassList(c); p.Add(e); return e; }
        static Label L(VisualElement p, string t, string cls = null) { var l = new Label(t); if (cls != null) foreach (var c in cls.Split(' ')) l.AddToClassList(c); p.Add(l); return l; }
        static Button B(VisualElement p, string t, Action a, string cls = "btn") { var b = new Button(a) { text = t }; foreach (var c in cls.Split(' ')) b.AddToClassList(c); p.Add(b); return b; }
        static Texture2D Icon(string n) => Resources.Load<Texture2D>("Icons/" + n);
        static void Img(VisualElement e, Texture2D t) { e.style.backgroundImage = t ? new StyleBackground(t) : StyleKeyword.None; }
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
            var gear = new Button(OpenSettings); gear.AddToClassList("ibtn"); Img(gear, Icon("ui_gear")); top.Add(gear);
        }
        void Pill(VisualElement p, string icon, out Label val) { var pill = Add(p, "pill"); var ic = Add(pill, "pill-ico"); Img(ic, Icon(icon)); val = L(pill, "0", "pill-val"); }

        void Refresh()
        {
            if (M == null || coinsL == null) return;
            var d = M.Data;
            coinsL.text = d.coins.ToString("N0"); ticketsL.text = d.tickets.ToString(); popL.text = Mathf.RoundToInt(M.Popularity).ToString(); lvlL.text = d.level.ToString();
            lvlFill.style.width = Length.Percent(100f * d.xp / CafeData.XpForLevel(d.level));
            if (tabBtns.TryGetValue("Shelter", out var sb)) Badge(sb, d.tickets);
            if (tab != "Cafe" && sheetHost.childCount > 0 && Time.unscaledTime - lastSheetBuild > 0.5f && tab != "Decorate") RebuildSheet();
        }
        float lastSheetBuild;
        static void Badge(VisualElement b, int n) { var old = b.Q(className: "badge"); old?.RemoveFromHierarchy(); if (n > 0) L(b, n.ToString(), "badge"); }

        // ------------------------------------------------------------ tabs
        void BuildTabs()
        {
            var bar = Add(root, "tabs");
            (string id, string label, string icon)[] t = { ("Cafe", "Café", "ui_paw"), ("Decorate", "Decorate", "ui_shop"), ("Cats", "Cats", "ui_heart"), ("Photos", "Photos", "ui_cam"), ("Shelter", "Shelter", "ui_sparkle") };
            foreach (var (id, label, icon) in t)
            {
                var b = new Button(() => OpenTab(id)); b.AddToClassList("tab"); var ic = Add(b, "tab-ico"); Img(ic, Icon(icon)); L(b, label, "tab-lbl");
                bar.Add(b); tabBtns[id] = b;
            }
            OpenTab("Cafe");
        }
        public void OpenTab(string id)
        {
            if (CafePhoto.Active) CafePhoto.I.Exit();
            tab = (tab == id && id != "Cafe") ? "Cafe" : id;
            foreach (var kv in tabBtns) kv.Value.EnableInClassList("on", kv.Key == tab);
            CafeDecorate.I?.SetMode(tab == "Decorate");
            RebuildSheet();
        }
        void RebuildSheet()
        {
            lastSheetBuild = Time.unscaledTime;
            float scroll = sheetHost.Q<ScrollView>()?.scrollOffset.y ?? 0;
            sheetHost.Clear(); if (tab == "Cafe") return;
            var sheet = Add(sheetHost, "sheet"); if (tab == "Decorate") sheet.style.height = Length.Percent(44);
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1;
            switch (tab)
            {
                case "Decorate": L(sheet, "Decorate", "sheet-title"); L(sheet, "Buy furniture, then drag it around the café. Tap a piece to rotate or store it.", "sheet-sub"); sheet.Add(sv); FillShop(sv.contentContainer); break;
                case "Cats": L(sheet, "Your cats", "sheet-title"); L(sheet, $"{M.Data.residents.Count} living here · {M.Beds} cat beds" + (M.Data.waiting.Count > 0 ? $" · {M.Data.waiting.Count} waiting for a bed" : ""), "sheet-sub"); sheet.Add(sv); FillCats(sv.contentContainer); break;
                case "Photos": L(sheet, "Photos", "sheet-title"); FillPhotos(sheet, sv); break;
                case "Shelter": L(sheet, "Shelter", "sheet-title"); sheet.Add(sv); FillShelter(sv.contentContainer); break;
            }
            sv.scrollOffset = new Vector2(0, scroll);
        }

        // ---- Decorate
        string shopCat = "Seating";
        void FillShop(VisualElement c)
        {
            var chips = Add(c, "chips");
            foreach (var k in new[] { "Seating", "Cat furniture", "Decor", "Rooms", "Menu", "Upgrades" }) { var b = B(chips, k, () => { shopCat = k; RebuildSheet(); }, "chip"); b.EnableInClassList("on", shopCat == k); }
            var grid = Add(c, "grid");
            if (shopCat == "Rooms")
            {
                foreach (var r in CafeData.Rooms.Where(r => r.Cost > 0))
                {
                    var card = Add(grid, "card"); L(card, r.Label, "card-name");
                    if (M.RoomOpen(r.Id)) L(card, "Open", "card-sub");
                    else { L(card, $"Level {r.Unlock}", "card-sub"); B(card, $"{r.Cost}", () => { if (M.TryUnlockRoom(r.Id)) RebuildSheet(); }, "btn small gold"); }
                }
                return;
            }
            if (shopCat == "Upgrades")
            {
                foreach (var u in CafeData.Upgrades)
                {
                    int lv = M.Data.Up(u.Id);
                    var row = Add(c, "listrow"); var col = Add(row, "grow");
                    L(col, $"{u.Label}  ·  Lv {lv}/{u.MaxLevel}", "name"); L(col, u.Text, "small");
                    if (lv >= u.MaxLevel) L(row, "MAX", "card-sub");
                    else { int cost = CafeData.UpgradeCost(u, lv); var b = B(row, $"{cost:N0}", () => { if (M.BuyUpgrade(u.Id)) RebuildSheet(); }, "btn small gold"); if (M.Data.coins < cost) b.AddToClassList("off"); }
                }
                return;
            }
            if (shopCat == "Menu")
            {
                foreach (var m in CafeData.Menu)
                {
                    var card = Add(grid, "card"); var img = Add(card, "card-img"); Img(img, Icon("furn_" + m.Prop)); L(card, m.Label, "card-name");
                    L(card, m.Unlock <= M.Data.level ? $"{m.Price} coins" : $"Unlocks at level {m.Unlock}", "card-sub");
                    if (m.Unlock > M.Data.level) card.AddToClassList("locked");
                }
                return;
            }
            var cat = shopCat == "Seating" ? FurnCat.Seating : shopCat == "Decor" ? FurnCat.Decor : FurnCat.CatFurniture;
            foreach (var f in CafeData.Furniture.Where(f => f.Cat == cat))
            {
                var card = Add(grid, "card"); var img = Add(card, "card-img"); Img(img, Icon("furn_" + f.Id)); L(card, f.Label, "card-name");
                string info = f.Seats > 0 ? "+1 seat" : f.Beds > 0 ? $"+{f.Beds} cat bed" : f.Perches.Length > 0 ? "cat perch" : "";
                if (f.Cozy > 0) info += (info.Length > 0 ? " · " : "") + $"cozy +{f.Cozy}";
                L(card, info, "card-sub");
                if (f.Unlock > M.Data.level) { card.AddToClassList("locked"); L(card, $"Level {f.Unlock}", "card-sub"); }
                else { var b = B(card, $"{f.Cost}", () => { if (M.Data.coins >= f.Cost) CafeDecorate.I.Buy(f.Id); else M.Say("Not enough coins yet"); }, "btn small gold"); if (M.Data.coins < f.Cost) b.AddToClassList("off"); }
            }
        }
        public void ShowFurnitureTools(FurnitureInstance f)
        {
            tools.Clear(); tools.RemoveFromClassList("hidden");
            if (!f) { tools.AddToClassList("hidden"); return; }
            tools.style.position = Position.Absolute; tools.style.left = 0; tools.style.right = 0; tools.style.top = Length.Percent(40);
            var row = Add(tools, "row"); row.style.justifyContent = Justify.Center;
            L(row, f.Def.Label, "vf-name");
            var r2 = Add(tools, "row"); r2.style.justifyContent = Justify.Center;
            B(r2, "Rotate", () => CafeDecorate.I.Rotate(f), "btn small mint"); B(r2, $"Store (+{f.Def.Cost / 2})", () => CafeDecorate.I.Store(f), "btn small ghost"); B(r2, "Done", () => CafeDecorate.I.Select(null), "btn small");
        }

        // ---- Cats
        void FillCats(VisualElement c)
        {
            foreach (var id in M.Data.residents.Concat(M.Data.waiting))
            {
                var def = GameData.Cat(id); var rec = M.Data.CatOrNew(id); bool waiting = M.Data.waiting.Contains(id);
                var row = Add(c, "listrow"); var pic = Add(row, "portrait"); Img(pic, Icon("cat_" + id));
                var col = Add(row, "grow"); L(col, id, "name"); L(col, $"{def?.Breed} · {def?.Personality}", "small");
                var hearts = Add(col, "hearts"); for (int i = 0; i < 5; i++) { var h = Add(hearts, "heart"); Img(h, Icon(i < rec.Hearts ? "ui_heart" : "ui_heart_empty")); }
                L(col, waiting ? "Waiting for a cat bed" : $"Photos {rec.poses.Count}/6 · charm {3 + rec.charm + (int)(def?.Rarity ?? 0) * 3}", "small");
                if (!waiting) B(row, "Find", () => { var cat = M.Cats.FirstOrDefault(k => k && k.Def.Id == id); if (cat) { OpenTab("Cafe"); CafeCamera.I.LookAtPoint(cat.transform.position); } }, "btn small mint");
            }
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
                    if (rec != null) { Img(img, PhotoStore.Load(d.Id, rec.poses.OrderByDescending(p => p.stars).FirstOrDefault()?.pose ?? "") ?? Icon("cat_" + d.Id)); L(card, d.Id, "card-name"); L(card, $"{rec.poses.Count}/6 poses", "card-sub"); }
                    else { Img(img, Icon("cat_" + d.Id)); img.style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.25f); L(card, "???", "card-name"); L(card, d.Rarity.ToString(), "card-sub"); }
                }
            }
            else
            {
                CafePhoto.TickFeed(M.Data);
                L(c, $"{M.Data.followers} followers · likes keep arriving for a couple of hours after you post", "sheet-sub");
                foreach (var p in M.Data.feed)
                {
                    var row = Add(c, "listrow"); var ph = Add(row, "portrait"); ph.style.borderTopLeftRadius = ph.style.borderTopRightRadius = ph.style.borderBottomLeftRadius = ph.style.borderBottomRightRadius = 16;
                    Img(ph, CafePhoto.LoadFile(p.file)); var col = Add(row, "grow"); L(col, $"{p.cat} at Paw Café", "name"); L(col, $"{p.likesShown} likes", "small");
                }
                if (M.Data.feed.Count == 0) L(c, "Nothing posted yet — take a photo and tap Post.", "small");
            }
        }

        // ---- Shelter
        void FillShelter(VisualElement c)
        {
            L(c, "Adoption tickets come from levelling up, follower milestones and one free ticket every day. Each ticket brings a random cat: Common 70% · Uncommon 22% · Rare 7% · Legendary 1%.", "sheet-sub");
            var row = Add(c, "row"); row.style.justifyContent = Justify.Center;
            var b = B(row, M.Data.tickets > 0 ? $"Adopt a cat  (1 of {M.Data.tickets} tickets)" : "No tickets yet", DrawTicket, "btn gold"); if (M.Data.tickets <= 0) b.AddToClassList("off");
            if (M.Data.waiting.Count > 0) L(c, $"Waiting for a bed: {string.Join(", ", M.Data.waiting)} — buy a cat bed in Decorate.", "small");
        }
        public void DrawTicket()
        {
            if (M.Data.tickets <= 0) return;
            M.Data.tickets--;
            int roll = UnityEngine.Random.Range(0, 100), acc = 0; var tier = Rarity.Common;
            for (int i = 0; i < CafeData.TicketOdds.Length; i++) { acc += CafeData.TicketOdds[i]; if (roll < acc) { tier = (Rarity)i; break; } }
            var pool = GameData.Cats.Where(x => x.Rarity == tier).ToList();
            var fresh = pool.Where(x => M.Data.Cat(x.Id) == null || (!M.Data.residents.Contains(x.Id) && !M.Data.waiting.Contains(x.Id))).ToList();
            var pick = (fresh.Count > 0 && UnityEngine.Random.value < 0.75f ? fresh : pool)[UnityEngine.Random.Range(0, (fresh.Count > 0 && UnityEngine.Random.value < 0.75f ? fresh : pool).Count)];
            M.Adopt(pick.Id, out bool dup);
            Modal(m =>
            {
                L(m, tier == Rarity.Legendary ? "LEGENDARY!" : tier.ToString(), "small"); L(m, pick.Id, "modal-title"); var pic = Add(m, "big-portrait"); Img(pic, Icon("cat_" + pick.Id));
                L(m, dup ? $"{pick.Id} already lives with you — they got friendship treats (+1 charm)." : M.Data.waiting.Contains(pick.Id) ? $"{pick.Breed}. Waiting for a free cat bed!" : $"{pick.Breed} · {pick.Personality}. Welcome home!", "modal-text");
                B(m, "Lovely!", CloseModal);
            });
            Fx.Hearts(CafeCamera.I.transform.position + CafeCamera.I.transform.forward * 4f, 20, 1.4f); AudioManager.I?.Play("sfx_levelup");
            M.Save(); RebuildSheet();
        }

        // ------------------------------------------------------------ world bubbles
        void LateUpdate()
        {
            if (root?.panel == null || M == null) return;
            var cam = CafeCamera.I.Cam; var seen = new HashSet<object>();
            bool hideWorld = CafePhoto.Active;
            UpdateCoach(hideWorld);
            foreach (var cu in M.Customers)
            {
                if (!cu || hideWorld) continue;
                if (cu.Bubble == Bubble.None) continue;
                string cls = cu.Bubble == Bubble.Photo ? "photo" : cu.Bubble == Bubble.Coins ? "coins" : "";
                var e = Bubble3D(cu, cls, cu.transform.position + Vector3.up * 2.05f, seen);
                if (cu.Bubble == Bubble.Photo && cu.Request != null)
                {
                    var rq = cu.Request; bool any = string.IsNullOrEmpty(rq.Cat);
                    SetBubble(e, any ? "ui_paw" : "cat_" + rq.Cat, (any ? "Any cat" : rq.Cat) + (rq.Pose != null ? " · " + PhotoRequest.PoseWord(rq.Pose) : ""));
                    e.EnableInClassList("anycat", any);
                }
                else SetBubble(e, cu.Bubble == Bubble.Heart ? "ui_heart" : null,
                          cu.Bubble == Bubble.Order ? cu.Order?.Label.Split(' ').Last() : cu.Bubble == Bubble.Coins ? "+" : null);
            }
            if (!hideWorld && M.Data.tipJar > 0) { var e = Bubble3D("tip", "coins", M.TipJarPoint.position + Vector3.up * 0.75f, seen); SetBubble(e, null, M.Data.tipJar.ToString("N0")); }
            foreach (var s in FindObjectsByType<Stray>()) if (!hideWorld) { var e = Bubble3D(s, "paw", s.transform.position + Vector3.up * 1.3f, seen); SetBubble(e, "ui_paw", null); }
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
            foreach (var c in new[] { "photo", "coins", "paw", "lock" }) e.EnableInClassList(c, c == cls);
            var sp = CafeCamera.I.Cam.WorldToScreenPoint(wp);
            e.style.display = sp.z > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
            e.style.left = pp.x; e.style.top = pp.y; return e;
        }
        static void SetBubble(VisualElement e, string icon, string text)
        {
            var ic = e.Q(className: "wb-ico"); ic.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None; if (icon != null) Img(ic, Icon(icon));
            var t = e.Q<Label>(className: "wb-txt"); t.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None; if (text != null) t.text = text;
        }
        public void Float(string text, Vector3 wp)
        {
            var sp = CafeCamera.I.Cam.WorldToScreenPoint(wp + Vector3.up * 0.8f); var pp = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(sp.x, Screen.height - sp.y));
            var l = L(world, text, "float"); l.style.left = pp.x; l.style.top = pp.y;
            l.experimental.animation.Start(0f, 1f, 1100, (el, t) => { el.style.top = pp.y - 120 * t; el.style.opacity = 1 - t * t; }).OnCompleted(() => l.RemoveFromHierarchy());
        }

        // ------------------------------------------------------------ viewfinder
        Label vfStars; VisualElement vfStarRow;
        void BuildViewfinder()
        {
            Add(vf, "vf-frame").pickingMode = PickingMode.Ignore;
            var top = Add(vf, "vf-top"); top.pickingMode = PickingMode.Ignore; vfReqBox = Add(top); vfName = L(top, "", "vf-name");
            vfStarRow = Add(top, "stars");
            for (int i = 0; i < 3; i++) { var s = Add(vfStarRow, "star"); Img(s, Icon("ui_star")); }
            var bottom = Add(vf, "vf-bottom");
            var close = new Button(() => CafePhoto.I.Exit()); close.AddToClassList("ibtn"); Img(close, Icon("ui_close")); bottom.Add(close);
            var sh = new Button(() => { CafePhoto.I.Shutter(); flash.AddToClassList("on"); }); sh.AddToClassList("shutter"); bottom.Add(sh);
            var pet = new Button(() => CafePhoto.I.Cat?.Pet()); pet.AddToClassList("ibtn"); Img(pet, Icon("ui_heart")); bottom.Add(pet);
        }
        public void ShowViewfinder(bool on) { vf.EnableInClassList("hidden", !on); sheetHost.style.display = on ? DisplayStyle.None : DisplayStyle.Flex; root.Q(className: "tabs").style.display = on ? DisplayStyle.None : DisplayStyle.Flex; root.Q(className: "top").style.display = on ? DisplayStyle.None : DisplayStyle.Flex; }
        void UpdateViewfinder()
        {
            var ph = CafePhoto.I; if (!CafePhoto.Active) return;
            vfName.text = ph.StrayTarget ? $"{ph.CatId} (stray)" : $"{ph.CatId} · {PhotoRequest.PoseWord(ph.LivePose)}";
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
                L(m, $"{cat} · {PhotoRequest.PoseWord(pose)}" + (fulfilled ? "  ·  request done! Big tip coming" : ""), "modal-text");
                var r = Add(m, "row");
                B(r, "Pin to wall", () => { if (CafePhoto.I.PinToWall(tex, cat, pose, stars)) { Toast("Pinned to the café wall"); CloseModal(); } }, "btn mint small");
                B(r, "Post", () => { CafePhoto.I.Post(tex, cat, stars); Toast("Posted to Whisker Feed — likes are coming in"); CloseModal(); }, "btn small");
                B(r, "Keep", CloseModal, "btn ghost small");
            });
        }

        // ------------------------------------------------------------ modals
        void Modal(Action<VisualElement> fill)
        {
            modalHost.Clear(); modalHost.RemoveFromClassList("hidden"); modalHost.AddToClassList("scrim");
            var m = Add(modalHost, "modal"); fill(m);
        }
        public void CloseModal() { modalHost.Clear(); modalHost.AddToClassList("hidden"); }
        public void OpenStray(Stray s)
        {
            Modal(m =>
            {
                L(m, $"{s.Def.Id} the stray", "modal-title"); var pic = Add(m, "big-portrait"); Img(pic, Icon("cat_" + s.Def.Id));
                L(m, $"{s.Def.Breed} · {s.Def.Rarity}. Win their trust to adopt them.", "modal-text");
                for (int i = 0; i < 3; i++)
                {
                    var st = Add(m, "step"); st.EnableInClassList("done", s.Rec.trust > i); L(st, s.Rec.trust > i ? "OK" : (i + 1).ToString(), "step-dot");
                    L(st, i == 0 ? $"Give a treat ({Stray.TreatCost} coins)" : i == 1 ? "Play together" : "Take their photo", "grow");
                    if (s.Rec.trust == i)
                    {
                        int k = i;
                        B(st, "Go", () => { if (k == 0) s.Treat(); else if (k == 1) s.PlayWith(); else { CloseModal(); CafePhoto.I.Enter(s); return; } OpenStray(s); }, "btn small mint");
                    }
                }
                var r = Add(m, "row");
                if (s.Rec.trust >= 3) B(r, M.HasRoomForCat ? "Adopt!" : "Adopt (needs a cat bed)", () => { s.Adopt(); CloseModal(); RebuildSheet(); }, "btn gold");
                B(r, "Later", CloseModal, "btn ghost small");
            });
        }
        public void OpenRoomUnlock(string id)
        {
            var r = CafeData.Room(id);
            Modal(m =>
            {
                L(m, r.Label, "modal-title"); L(m, CafeData.RoomBlurb(id), "modal-text");
                var row = Add(m, "row");
                if (M.Data.level < r.Unlock) L(m, $"Opens at café level {r.Unlock}", "small");
                else B(row, $"Open for {r.Cost} coins", () => { if (M.TryUnlockRoom(id)) CloseModal(); }, "btn gold");
                B(row, "Close", CloseModal, "btn ghost small");
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
        void OnLevelUp(int lv)
        {
            var unlocks = CafeData.Furniture.Where(f => f.Unlock == lv).Select(f => f.Label).Concat(CafeData.Menu.Where(x => x.Unlock == lv).Select(x => x.Label)).Concat(CafeData.Rooms.Where(x => x.Unlock == lv).Select(x => x.Label)).ToList();
            Modal(m =>
            {
                L(m, $"Café level {lv}!", "modal-title");
                L(m, unlocks.Count > 0 ? "New: " + string.Join(", ", unlocks) : "Your café is getting famous.", "modal-text");
                if (lv % 3 == 0) L(m, "+1 adoption ticket", "modal-text");
                B(m, "Yay!", CloseModal, "btn");
            });
        }
        void OpenSettings()
        {
            Modal(m =>
            {
                L(m, "Settings", "modal-title");
                B(m, $"Music: {(M.Data.music ? "on" : "off")}", () => { M.Data.music = !M.Data.music; AudioManager.MusicOn = M.Data.music; AudioManager.I?.Apply(); M.Save(); OpenSettings(); }, "btn ghost");
                B(m, $"Sounds: {(M.Data.sfx ? "on" : "off")}", () => { M.Data.sfx = !M.Data.sfx; AudioManager.SfxOn = M.Data.sfx; AudioManager.I?.Apply(); M.Save(); OpenSettings(); }, "btn ghost");
                B(m, "Close", CloseModal, "btn");
            });
        }
        static readonly string[] CoachSteps =
        {
            "Customers drop tips in the jar on the counter. Tap the jar to collect them!",
            "Tap any cat to grab your camera, then press the shutter to take a photo.",
            "Open Decorate and buy something cozy — more seats bring more customers.",
            "You have an adoption ticket! Open Shelter and adopt a new cat.",
        };
        void UpdateCoach(bool hide)
        {
            int st = M.Data.tutorial; bool show = !hide && tab == "Cafe" && st < CoachSteps.Length && modalHost.ClassListContains("hidden")
                && !(st == 2 && tab == "Decorate") && !(st == 3 && tab == "Shelter") && !(st == 0 && M.Data.tipJar <= 0);
            coach.EnableInClassList("hidden", !show);
            if (show) coachText.text = $"{st + 1}/{CoachSteps.Length}  {CoachSteps[st]}";
        }
        public void Toast(string msg) { toastText.text = msg; toast.AddToClassList("on"); toastT = 2.6f; }
    }
}
