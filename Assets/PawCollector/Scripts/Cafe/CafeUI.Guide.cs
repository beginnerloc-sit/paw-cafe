using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe
{
    /// <summary>
    /// The starter guide: a glossy card (cat face, "Step 1 of 4", a short title and one helpful line) plus a bouncing arrow
    /// and a pulsing ring on the exact thing to tap — the tip jar, a cat, a tab, the Shop button, the cat bed, ✓, Adopt.
    /// Every step adapts to what the player is doing, so it never asks for something impossible:
    ///  • the tip jar always has a few welcome coins for step 1,
    ///  • the bed step points at tips first if coins are short,
    ///  • the adoption step makes sure there is a free bed, and the first adoption is free.
    /// </summary>
    public partial class CafeUI
    {
        VisualElement guide, gCard, gAva, gPtr, gRing, gTag, gDots; Label gStep, gTitle, gSub, gTagL;
        Button decorShopBtn, shutterBtn, adoptBtn, placerOkBtn;
        readonly Dictionary<string, Button> shopTabBtns = new(), shopBuyBtns = new();
        int gLastStep = -1; float gDoneT, gPop; Vector3 gLastLook; bool gLooked;

        const int GuideSteps = CafeManager.TutorialSteps;
        /// <summary>Adoption needs a free bed: only then does the guide detour to buying one.</summary>
        bool GuideNeedsBed => M.Data.tutorial == 3 && M.FreeBeds <= 0;
        /// <summary>The guide's shopping steps: step 3 buys a chair (cheap, +1 seat), the bed detour buys a cat bed.</summary>
        bool GuideShopping => M.Data.tutorial < GuideSteps && (M.Data.tutorial == 2 || GuideNeedsBed);
        string GuideShopCat => GuideNeedsBed ? "Cat furniture" : "Seating";

        void BuildGuide()
        {
            guide = Add(root, "layer guide"); guide.pickingMode = PickingMode.Ignore;
            gRing = Add(guide, "guide-ring"); Img(gRing, Icon("ui_guide_ring"));
            gPtr = Add(guide, "guide-ptr"); Img(gPtr, Icon("ui_guide_arrow"));
            gTag = Add(guide, "guide-tag"); gTagL = L(gTag, "", "guide-tag-txt");
            var wrap = Add(guide, "guide-wrap");
            gCard = Add(wrap, "guide-card");
            var ring = Add(gCard, "guide-ava-ring"); gAva = Add(ring, "guide-ava");
            var col = Add(gCard, "guide-col");
            var head = Add(col, "guide-head"); gStep = L(head, "", "guide-step"); gDots = Add(head, "guide-dots");
            for (int i = 0; i < GuideSteps; i++) Add(gDots, "gdot");
            gTitle = L(col, "", "guide-title"); gSub = L(col, "", "guide-sub");
            IgnoreTree(guide);
            var skip = new Button(SkipGuide) { text = "Skip", focusable = false }; skip.clicked += TapSfx; skip.AddToClassList("guide-skip"); gCard.Add(skip);
            guide.AddToClassList("hidden");
        }

        void SkipGuide()
        {
            if (M.Data.tutorial >= GuideSteps) return;
            M.Data.tutorial = GuideSteps; gLastStep = GuideSteps; M.Save(); guide.AddToClassList("hidden"); HidePointer();
        }

        struct GuideView { public string title, sub, tag; public VisualElement ui; public Vector3? world; public bool hideCard; }

        void UpdateGuide(bool hideWorld)
        {
            if (guide == null) return;
            int st = M.Data.tutorial;
            // celebrate the end for a few seconds
            if (st >= GuideSteps)
            {
                if (gLastStep >= 0 && gLastStep < GuideSteps) { gDoneT = 3.5f; gLastStep = st; ShowGuideCard("All done — enjoy your café!", "Here's 100 coins to get you started", GuideSteps, true); Pop(); }
                bool wait = title != null || cineActive || CafeVisit.Active || CafePhoto.Active || !modalHost.ClassListContains("hidden");   // celebrate once the minigame / popups are done
                if (gDoneT > 0 && !wait && (gDoneT -= Time.unscaledDeltaTime) > 0) { guide.RemoveFromClassList("hidden"); HidePointer(); PlaceCard(guide.layout.size, tab is "Decorate" or "Shelter"); TickPop(); }
                else guide.AddToClassList("hidden");
                return;
            }
            if (st != gLastStep) { gLastStep = st; gLooked = false; Pop(); }

            // the tip jar always has something in it for the very first step
            if (st == 0 && M.Data.tipJar <= 0) { M.Data.tipJar = 20; }

            bool modalOpen = !modalHost.ClassListContains("hidden");
            bool shopOpen = modalOpen && modalHost.Q(className: "shop") != null;
            bool placing = CafeDecorate.I && CafeDecorate.I.Placing;
            bool busy = title != null || cineActive || CafeVisit.Active || (modalOpen && !(shopOpen && GuideShopping)) || (CafePhoto.Active && st != 1);
            if (busy) { guide.AddToClassList("hidden"); return; }

            var v = GuideFor(st, shopOpen, placing);
            if (v.title == null && v.tag == null) { guide.AddToClassList("hidden"); return; }
            guide.RemoveFromClassList("hidden");

            // where is the target?
            Rect? target = null;
            if (v.ui != null && v.ui.panel != null && v.ui.resolvedStyle.display != DisplayStyle.None && v.ui.worldBound.width > 1)
            {
                var wb = v.ui.worldBound; var o = guide.worldBound.position; target = new Rect(wb.x - o.x, wb.y - o.y, wb.width, wb.height);
            }
            else if (v.world.HasValue && root.panel != null)
            {
                var cam = CafeCamera.I.Cam; var sp = cam.WorldToScreenPoint(v.world.Value);
                if (sp.z > 0)
                {
                    var vp = cam.WorldToViewportPoint(v.world.Value);
                    if (!gLooked && (vp.x < 0.12f || vp.x > 0.88f || vp.y < 0.12f || vp.y > 0.85f) && !CafePhoto.Active) { CafeCamera.I.LookAtPoint(v.world.Value); gLooked = true; }
                    var pp = WorldToPanel(sp); var o = guide.worldBound.position - root.panel.visualTree.worldBound.position;
                    target = new Rect(pp.x - o.x - 55, pp.y - o.y - 55, 110, 110);
                }
            }

            var size = guide.layout.size;
            bool cardTop = (target.HasValue && target.Value.center.y > size.y * 0.55f) || ((tab is "Decorate" or "Shelter") && !placing);   // keep clear of bottom controls
            if (v.hideCard) gCard.parent.style.display = DisplayStyle.None;
            else { gCard.parent.style.display = DisplayStyle.Flex; PlaceCard(size, cardTop); ShowGuideCard(v.title, v.sub, st, false); }
            TickPop();

            if (!target.HasValue) { HidePointer(); return; }
            PlacePointer(target.Value, size, v.tag);
        }

        GuideView GuideFor(int st, bool shopOpen, bool placing)
        {
            var g = new GuideView();
            string catName = M.Data.residents.Count > 0 ? M.Data.Name(M.Data.residents[0]) : "a cat";
            // on a page the step doesn't use: point at the tab that leads on
            bool shopping = GuideShopping;
            bool wrongTab = tab is "Cats" or "Photos" || (tab == "Shelter" && (st != 3 || shopping)) || (tab == "Decorate" && !shopping && !placing);
            string stepTitle = st switch { 0 => "Collect your tips", 1 => "Take a cat photo", 2 => "Add a new seat", _ => shopping ? "First, a bed for the new cat" : "Adopt your first cat" };
            if (wrongTab)
            {
                g.title = stepTitle;
                string to = shopping ? "Decorate" : st == 3 ? "Shelter" : "Cafe";
                g.sub = to == "Decorate" ? "Open Decorate to shop" : to == "Shelter" ? "Open the Shelter" : "Head back to your café";
                g.ui = tabBtns.TryGetValue(to, out var tb) ? tb : null; return g;
            }
            switch (st)
            {
                case 0:
                    g.title = stepTitle; g.sub = "Customers leave coins in the jar — tap it!";
                    g.world = M.TipJarPoint.position + Vector3.up * 0.35f; return g;
                case 1:
                    if (CafePhoto.Active) { g.hideCard = true; g.tag = "Snap!"; g.ui = shutterBtn; return g; }
                    g.title = stepTitle; g.sub = $"Tap {catName} to open your camera";
                    var cat = NearestCat(); if (cat) g.world = cat.transform.position + Vector3.up * 0.45f; return g;
            }
            g.title = stepTitle;
            if (shopping)
            {
                // step 3: a chair (+1 seat = more customers) · bed detour: a cat bed
                string id = GuideNeedsBed ? "CatBed" : "Chair", what = GuideNeedsBed ? "a cat bed" : "a chair";
                int price = M.Price(CafeData.Furn(id));
                if (placing) { g.sub = GuideNeedsBed ? "Drag it to a green spot, then tap ✓" : "Put it by a table, then tap ✓"; g.ui = placerOkBtn; return g; }
                if (M.Data.coins < price)
                {
                    if (shopOpen) return default;   // the greyed price says it all; the card comes back when the shop closes
                    g.sub = $"{char.ToUpper(what[0])}{what.Substring(1)} costs {price} — collect more tips";
                    if (M.Data.tipJar > 0) g.world = M.TipJarPoint.position + Vector3.up * 0.35f;
                    return g;
                }
                if (shopOpen)
                {
                    g.hideCard = true;
                    if (shopCat != GuideShopCat) { g.tag = GuideNeedsBed ? "Cat stuff" : "Seating"; g.ui = shopTabBtns.TryGetValue(GuideShopCat, out var t) ? t : null; }
                    else { g.tag = $"Buy {what}"; g.ui = shopBuyBtns.TryGetValue(id, out var b) ? b : null; }
                    return g;
                }
                if (tab != "Decorate") { g.sub = GuideNeedsBed ? "Every cat needs its own bed — open Decorate" : "More seats bring more customers — open Decorate"; g.ui = tabBtns.TryGetValue("Decorate", out var db) ? db : null; return g; }
                g.sub = $"Tap Shop and pick {what}"; g.ui = decorShopBtn; return g;
            }
            // step 4: adopt
            if (tab != "Shelter") { g.sub = "Open the Shelter — your first adoption is free"; g.ui = tabBtns.TryGetValue("Shelter", out var sb2) ? sb2 : null; return g; }
            g.sub = "Tap Adopt, then wave the feather"; g.ui = adoptBtn; return g;
        }

        CafeCat NearestCat()
        {
            var cam = CafeCamera.I.Cam; CafeCat best = null; float bd = float.MaxValue;
            foreach (var c in M.Cats)
            {
                if (!c) continue; var v = cam.WorldToViewportPoint(c.transform.position); if (v.z <= 0) continue;
                float d = (new Vector2(v.x, v.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude + (v.x < 0 || v.x > 1 || v.y < 0 || v.y > 1 ? 4f : 0f);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        void ShowGuideCard(string t, string s, int step, bool done)
        {
            gCard.parent.style.display = DisplayStyle.Flex;
            var ava = M.Data.residents.Count > 0 ? M.Data.residents[0] : "Mochi"; Img(gAva, Icon("cat_" + ava));
            gStep.text = done ? "GUIDE COMPLETE" : $"STEP {step + 1} OF {GuideSteps}";
            int i = 0; foreach (var d in gDots.Children()) { d.EnableInClassList("on", i < step || done); d.EnableInClassList("now", i == step && !done); i++; }
            if (gTitle.text != t) { gTitle.text = t; GFit(gTitle, 34f); }
            if (gSub.text != (s ?? "")) { gSub.text = s ?? ""; GFit(gSub, 24f); }
            gCard.EnableInClassList("done", done);
        }

        /// <summary>Fixed-width line: shrink the font until the text fits (never clipped, never wraps).</summary>
        static void GFit(Label t, float baseSize)
        {
            t.style.fontSize = baseSize;
            t.schedule.Execute(() =>
            {
                float avail = t.contentRect.width; if (avail <= 1 || string.IsNullOrEmpty(t.text)) return;
                float need = t.MeasureTextSize(t.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                if (need > avail + 0.5f) t.style.fontSize = Mathf.Max(13f, Mathf.Floor(baseSize * avail / need));
            });
        }

        /// <summary>Centre the card in the space right of the tab rail, at the top or the bottom of the screen.</summary>
        void PlaceCard(Vector2 size, bool top)
        {
            if (float.IsNaN(size.x) || size.x < 10) return;
            const float W = 760f, H = 138f, Rail = 156f, Pad = 16f;
            gCard.style.left = Rail + Mathf.Max(0f, (size.x - Rail - Pad - W) / 2f);
            gCard.style.top = top ? 96f : size.y - H - 18f;
        }

        void HidePointer() { gPtr.style.display = gRing.style.display = gTag.style.display = DisplayStyle.None; }

        void PlacePointer(Rect t, Vector2 size, string tag)
        {
            gPtr.style.display = gRing.style.display = DisplayStyle.Flex;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 16f;
            float ringSize = Mathf.Clamp(Mathf.Max(t.width, t.height) + 36f, 110f, 260f);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            gRing.style.width = gRing.style.height = ringSize;
            gRing.style.left = t.center.x - ringSize / 2f; gRing.style.top = t.center.y - ringSize / 2f;
            gRing.style.scale = new Scale(new Vector3(pulse, pulse, 1)); gRing.style.opacity = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
            const float P = 96f; Vector2 c; float rot; Vector2 tagDir;
            if (t.center.x < size.x * 0.16f) { c = new Vector2(t.xMax + P / 2f + 6f + bob, t.center.y); rot = 90f; tagDir = Vector2.right; }       // left rail: arrow points left
            else if (t.yMin < 150f) { c = new Vector2(t.center.x, t.yMax + P / 2f + 6f + bob); rot = 180f; tagDir = Vector2.up; }              // near the top: arrow points up
            else { c = new Vector2(t.center.x, t.yMin - P / 2f - 6f - bob); rot = 0f; tagDir = Vector2.down; }                                  // arrow points down
            gPtr.style.left = c.x - P / 2f; gPtr.style.top = c.y - P / 2f; gPtr.style.rotate = new Rotate(rot);
            if (string.IsNullOrEmpty(tag)) { gTag.style.display = DisplayStyle.None; return; }
            gTag.style.display = DisplayStyle.Flex; if (gTagL.text != tag) gTagL.text = tag;
            var ts = gTag.layout.size; if (float.IsNaN(ts.x) || ts.x < 2) ts = new Vector2(240, 60);
            Vector2 p = tagDir == Vector2.right ? new Vector2(c.x + P / 2f + 4f, c.y - ts.y / 2f)
                      : tagDir == Vector2.up ? new Vector2(c.x - ts.x / 2f, c.y + P / 2f)
                      : new Vector2(c.x - ts.x / 2f, c.y - P / 2f - ts.y);
            p.x = Mathf.Clamp(p.x, 8f, size.x - ts.x - 8f); p.y = Mathf.Clamp(p.y, 8f, size.y - ts.y - 8f);
            gTag.style.left = p.x; gTag.style.top = p.y;
        }

        void Pop() { gPop = 1f; }
        void TickPop()
        {
            if (gPop <= 0) { gCard.style.scale = new Scale(Vector3.one); return; }
            gPop = Mathf.Max(0, gPop - Time.unscaledDeltaTime * 3.5f);
            float t = 1f - gPop, k = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f * (1f - t) + (t < 0.25f ? (0.25f - t) * -0.6f : 0f);
            gCard.style.scale = new Scale(new Vector3(k, k, 1));
        }
    }
}
