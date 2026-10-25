using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PawCollector.Cafe
{
    /// <summary>Photo requests as a stack of cards on the right edge (instead of big bubbles over the café).
    /// Collapsed: the most urgent card on top of a little pile + "+2". Tap to drop the stack open; tap a card to look at
    /// that customer; tap the header chip to fold it back up.</summary>
    public partial class CafeUI
    {
        VisualElement reqStack, reqCards; Button reqToggle; Label reqToggleL; bool reqOpen; string reqSig = "";
        readonly Dictionary<PhotoRequest, float> reqTotal = new();
        readonly List<(VisualElement bar, Label time, PhotoRequest r)> reqLive = new();

        void BuildRequests()
        {
            reqStack = Add(root, "reqstack"); reqStack.pickingMode = PickingMode.Ignore;
            reqCards = Add(reqStack, "req-cards"); reqCards.pickingMode = PickingMode.Ignore;
            reqToggle = new Button(() => { reqOpen = !reqOpen; reqSig = ""; }) { focusable = false }; reqToggle.clicked += TapSfx; reqToggle.AddToClassList("req-toggle"); reqStack.Add(reqToggle);
            reqToggleL = L(reqToggle, "", "req-toggle-txt");
            reqStack.style.display = DisplayStyle.None;
        }

        void UpdateRequests()
        {
            if (reqStack == null) return;
            var reqs = M.Customers.Where(c => c && c.Request != null && c.Bubble == Bubble.Photo).OrderBy(c => c.Request.TimeLeft).ToList();
            bool show = reqs.Count > 0 && tab == "Cafe" && title == null && !ArcadeActive && !CafePhoto.Active && !CafeVisit.Active && modalHost.ClassListContains("hidden");
            reqStack.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            foreach (var c in reqs) if (!reqTotal.ContainsKey(c.Request)) reqTotal[c.Request] = Mathf.Max(1f, c.Request.TimeLeft);
            if (reqs.Count <= 1) reqOpen = false;
            string sig = string.Join(",", reqs.Select(c => c.GetHashCode() + ":" + c.Request.GetHashCode())) + reqOpen;
            if (sig != reqSig)
            {
                reqSig = sig; reqCards.Clear(); reqLive.Clear();
                int shown = reqOpen ? Mathf.Min(reqs.Count, 5) : Mathf.Min(reqs.Count, 3);
                for (int i = 0; i < shown; i++)
                {
                    var cu = reqs[i]; var rq = cu.Request; bool any = string.IsNullOrEmpty(rq.Cat);
                    var card = new Button(() => { if (!reqOpen && reqs.Count > 1) { reqOpen = true; reqSig = ""; return; } if (cu) CafeCamera.I.LookAtPoint(cu.transform.position); }) { focusable = false };
                    card.clicked += TapSfx; card.AddToClassList("req-card"); reqCards.Add(card);
                    if (!reqOpen && i > 0)
                    {   // the pile behind the top card: just its edge peeks out
                        card.AddToClassList("peek"); card.style.top = 13 * i; card.style.left = 10 * i; card.style.right = 10 * i; card.style.opacity = 1f - i * 0.2f;
                        card.SendToBack(); continue;
                    }
                    var ring = Add(card, "req-pic"); Img(ring, Icon(any ? "ui_paw" : "cat_" + rq.Cat)); ring.EnableInClassList("any", any);
                    var col = Add(card, "req-col");
                    var head = Add(col, "req-head"); Img(Add(head, "req-cam"), Icon("ui_cam")); L(head, "Photo request", "req-kicker");
                    var txt = L(col, (any ? "Any cat" : M.Data.Name(rq.Cat)) + (rq.Pose != null ? " · " + PhotoRequest.PoseWord(rq.Pose) : ""), "req-txt"); Fit(txt);
                    var bar = Add(col, "req-bar"); var fill = Add(bar, "req-fill");
                    var time = L(card, "", "req-time");
                    reqLive.Add((fill, time, rq));
                }
                reqToggle.style.display = reqs.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                reqToggleL.text = reqOpen ? "Fold up" : $"+{reqs.Count - 1} more";
            }
            foreach (var (bar, time, r) in reqLive)
            {
                float k = Mathf.Clamp01(r.TimeLeft / reqTotal[r]);
                bar.style.width = Length.Percent(k * 100f); bar.EnableInClassList("low", k < 0.3f);
                time.text = Mathf.CeilToInt(r.TimeLeft) + "s";
            }
            // forget finished requests
            if (reqTotal.Count > 20) foreach (var key in reqTotal.Keys.Where(k => !reqs.Any(c => c.Request == k)).ToList()) reqTotal.Remove(key);
        }
    }
}
