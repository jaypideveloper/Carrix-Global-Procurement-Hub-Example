using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class PolicyPage : HubPage
    {
        public override string Id => "policy";
        public override string Title => "Policy & Routing";
        public override string Subtitle => "The rules the hub enforces - engagement triggers, approval thresholds and category-manager routing";

        EngagementPolicy draft;
        Label thresholdValue, matchValue, toleranceValue, impactText;
        SliderInt thresholdSlider, matchSlider, toleranceSlider;
        Switch splitSwitch, contractSwitch, supplierSwitch;
        Segmented windowSeg, emergencySeg;
        VisualElement doaBody;
        static readonly int[] Windows = { 7, 14, 30, 60 };
        static readonly int[] EmergencyHours = { 24, 48, 72 };

        public PolicyPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            var scroll = UIX.Scroll("page-scroll").AddTo(Root);
            var c = scroll.contentContainer;
            var row = UIX.Div("row").AddTo(c);

            var left = UIX.Div("grow", "gap-right").AddTo(row);
            left.style.flexBasis = 0;
            var rules = UIX.Card("Procurement engagement rules", "A request needs procurement engagement when ANY enabled rule applies. These are placeholders modeled on the category poster - set them to the official policy.", out var rb).AddTo(left);

            rb.Add(UIX.Text("RULE 1 - SPEND THRESHOLD", "section-label"));
            var tRow = UIX.Div("row-center").AddTo(rb);
            thresholdSlider = new SliderInt(1, 20) { value = 5 };
            thresholdSlider.AddToClassList("grow");
            tRow.Add(thresholdSlider);
            thresholdValue = UIX.Text("", "total-value").AddTo(tRow);
            thresholdValue.style.width = 90; thresholdValue.style.unityTextAlign = TextAnchor.MiddleRight;
            thresholdSlider.RegisterValueChangedCallback(e => { draft.ValueThreshold = e.newValue * 5000; Preview(); });
            splitSwitch = new Switch("Split-purchase detection: count related spend at the same site and category (and supplier, when known)", true).AddTo(rb);
            splitSwitch.Changed += v => { draft.SplitDetection = v; Preview(); };
            var wRow = UIX.Div("row-center", "mt-8").AddTo(rb);
            wRow.Add(UIX.Text("Look-back window", "muted", "small"));
            windowSeg = new Segmented(new[] { "7 days", "14 days", "30 days", "60 days" }, 2).AddTo(wRow);
            windowSeg.style.marginLeft = 12;
            windowSeg.Changed += i => { draft.SplitWindowDays = Windows[i]; Preview(); };

            rb.Add(UIX.Text("RULE 2 - CONTRACTUAL COMMITMENT", "section-label"));
            contractSwitch = new Switch("Any contract, agreement, lease, SOW, subscription or supplier terms requires procurement - regardless of value", true).AddTo(rb);
            contractSwitch.Changed += v => { draft.ContractRule = v; Preview(); };
            rb.Add(UIX.Text("Detected from the requester's checkbox or description terms: contract, agreement, lease, SOW, statement of work, MSA, subscription, multi-year, retainer, renewal, terms and conditions.", "field-hint"));

            rb.Add(UIX.Text("RULE 3 - NEW OR NON-APPROVED SUPPLIER", "section-label"));
            supplierSwitch = new Switch("Suppliers not in the vendor master, or not approved for the category, require onboarding and risk review", true).AddTo(rb);
            supplierSwitch.Changed += v => { draft.NewSupplierRule = v; Preview(); };
            var mRow = UIX.Div("row-center", "mt-8").AddTo(rb);
            mRow.Add(UIX.Text("Fuzzy-match threshold", "muted", "small"));
            matchSlider = new SliderInt(70, 98) { value = 86 };
            matchSlider.AddToClassList("grow");
            matchSlider.style.marginLeft = 12;
            mRow.Add(matchSlider);
            matchValue = UIX.Text("", "text-1", "strong").AddTo(mRow);
            matchValue.style.width = 50; matchValue.style.unityTextAlign = TextAnchor.MiddleRight;
            matchSlider.RegisterValueChangedCallback(e => { draft.SupplierMatchThreshold = e.newValue / 100f; Preview(); });

            rb.Add(UIX.Text("EMERGENCIES AND PRICE CHECKS", "section-label"));
            var eRow = UIX.Div("row-center").AddTo(rb);
            eRow.Add(UIX.Text("Emergency post-review due within", "muted", "small"));
            emergencySeg = new Segmented(new[] { "24 h", "48 h", "72 h" }, 1).AddTo(eRow);
            emergencySeg.style.marginLeft = 12;
            emergencySeg.Changed += i => { draft.EmergencyReviewHours = EmergencyHours[i]; Preview(); };
            var pRow = UIX.Div("row-center", "mt-12").AddTo(rb);
            pRow.Add(UIX.Text("Price variance tolerance", "muted", "small"));
            toleranceSlider = new SliderInt(5, 50) { value = 20 };
            toleranceSlider.AddToClassList("grow");
            toleranceSlider.style.marginLeft = 12;
            pRow.Add(toleranceSlider);
            toleranceValue = UIX.Text("", "text-1", "strong").AddTo(pRow);
            toleranceValue.style.width = 50; toleranceValue.style.unityTextAlign = TextAnchor.MiddleRight;
            toleranceSlider.RegisterValueChangedCallback(e => { draft.PriceTolerance = e.newValue / 100f; Preview(); });

            var impact = UIX.Card("Impact preview", "Re-evaluated live against the request backlog before you apply", out var ib).AddTo(left);
            impact.AddToClassList("mt-16");
            impactText = UIX.Text("", "body-text").AddTo(ib);
            var btns = UIX.Div("btn-row", "mt-12").AddTo(ib);
            btns.Add(UIX.Btn("Apply policy", Apply, "btn--primary"));
            btns.Add(UIX.Btn("Discard changes", () => { LoadDraft(); Preview(); }));
            btns.Add(UIX.Btn("Restore defaults", () => { draft = new EngagementPolicy(); SyncControls(); Preview(); }, "btn--ghost"));

            var right = UIX.Div("grow").AddTo(row);
            right.style.flexBasis = 0;
            var doa = UIX.Card("Approval matrix (delegation of authority)", "Applied on top of engagement routing", out doaBody).AddTo(right);

            var routing = UIX.Card("Category routing matrix", "Every classified request goes to the category manager who owns it", out var rtb).AddTo(right);
            routing.AddToClassList("mt-16");
            var from = Db.T12Start;
            var spendByCat = Db.Lines.Where(l => l.Date >= from).GroupBy(l => Db.SubById[l.SubcategoryCode].CategoryCode).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            foreach (var cat in Db.Categories)
            {
                var mgr = Db.PersonOf(cat.ManagerId);
                var card = UIX.Div("option-card").AddTo(rtb);
                var head = UIX.Div("row-center").AddTo(card);
                head.Add(UIX.Avatar(mgr.Name));
                var t = UIX.Div("grow").AddTo(head);
                t.Add(UIX.Text(cat.Name + "  →  " + mgr.Name, "option-title"));
                t.Add(UIX.Text(mgr.Email + " · " + Fmt.Money(spendByCat.TryGetValue(cat.Code, out var v) ? v : 0) + " 12-mo spend · " + Db.Requests.Count(r => r.IsOpen && Db.Sub(r.SubcategoryCode)?.CategoryCode == cat.Code) + " open requests", "option-sub"));
                card.Add(UIX.Text(string.Join("  ·  ", cat.Subcategories.Select(s => s.Name)), "check-text", "mt-8"));
            }
        }

        public override void OnShow(object arg)
        {
            LoadDraft();
            Preview();
        }

        void LoadDraft()
        {
            draft = Db.Policy.Clone();
            SyncControls();
        }

        void SyncControls()
        {
            thresholdSlider.SetValueWithoutNotify(Mathf.Clamp((int)Math.Round(draft.ValueThreshold / 5000), 1, 20));
            splitSwitch.Value = draft.SplitDetection;
            windowSeg.Index = Math.Max(0, Array.IndexOf(Windows, draft.SplitWindowDays));
            contractSwitch.Value = draft.ContractRule;
            supplierSwitch.Value = draft.NewSupplierRule;
            matchSlider.SetValueWithoutNotify(Mathf.RoundToInt(draft.SupplierMatchThreshold * 100));
            emergencySeg.Index = Math.Max(0, Array.IndexOf(EmergencyHours, draft.EmergencyReviewHours));
            toleranceSlider.SetValueWithoutNotify(Mathf.RoundToInt(draft.PriceTolerance * 100));
        }

        void Preview()
        {
            thresholdValue.text = Fmt.MoneyExact(draft.ValueThreshold);
            matchValue.text = Fmt.Pct(draft.SupplierMatchThreshold);
            toleranceValue.text = Fmt.Pct(draft.PriceTolerance);

            var recent = Db.Requests.Where(r => (Svc.Now - r.Created).TotalDays <= 90).ToList();
            var saved = Db.Policy;
            int before = recent.Count(r => r.EngagementRequired);
            Db.Policy = draft;
            int after = 0;
            var byRule = new Dictionary<string, int> { ["R1"] = 0, ["R2"] = 0, ["R3"] = 0 };
            try
            {
                foreach (var r in recent)
                {
                    var d = Svc.Workflow.DraftFrom(r);
                    var eng = Svc.Engine.EvaluateRules(d, Db.Sub(r.SubcategoryCode), Svc.Engine.Matcher.Match(d.SupplierText), Db.Loc(r.LocationId));
                    if (eng.Required) after++;
                    foreach (var rule in eng.Rules) if (rule.Triggered && rule.Enabled) byRule[rule.Id]++;
                }
            }
            finally { Db.Policy = saved; }

            bool dirty = !Same(draft, saved);
            impactText.text = (dirty ? "With these changes" : "Under the current policy") + ", " + after + " of the " + recent.Count + " requests from the last 90 days need procurement engagement (" +
                              Fmt.Pct(recent.Count > 0 ? (double)after / recent.Count : 0) + ")" + (dirty ? " versus " + before + " today." : ".") +
                              "\nRule 1 applies to " + byRule["R1"] + ", Rule 2 to " + byRule["R2"] + ", Rule 3 to " + byRule["R3"] + " (a request can trigger several)." +
                              "\nApplying re-evaluates open requests still at intake or routing, and re-runs the alert engine (price tolerance and split detection).";
            RenderDoa(draft);
        }

        static bool Same(EngagementPolicy a, EngagementPolicy b) =>
            a.ValueThreshold == b.ValueThreshold && a.SplitDetection == b.SplitDetection && a.SplitWindowDays == b.SplitWindowDays && a.ContractRule == b.ContractRule &&
            a.NewSupplierRule == b.NewSupplierRule && Math.Abs(a.SupplierMatchThreshold - b.SupplierMatchThreshold) < 0.001f && a.EmergencyReviewHours == b.EmergencyReviewHours &&
            Math.Abs(a.PriceTolerance - b.PriceTolerance) < 0.001f;

        void RenderDoa(EngagementPolicy p)
        {
            doaBody.Clear();
            void Step(string range, string approvers)
            {
                var row = UIX.Div("kv").AddTo(doaBody);
                row.Add(UIX.Text(range, "kv-key"));
                row.Add(UIX.Text(approvers, "kv-value"));
            }
            Step("Below " + Fmt.MoneyExact(p.ValueThreshold), "Site budget owner (self-serve if no rule applies)");
            Step(Fmt.MoneyExact(p.ValueThreshold) + " - " + Fmt.MoneyExact(p.DirectorThreshold), "Budget owner + category manager");
            Step(Fmt.MoneyExact(p.DirectorThreshold) + " - " + Fmt.MoneyExact(p.ExecutiveThreshold), "+ Director, Global Procurement (" + Db.PersonOf("DIR-GP").Name + ")");
            Step("Above " + Fmt.MoneyExact(p.ExecutiveThreshold) + ", or capex above " + Fmt.MoneyExact(p.DirectorThreshold), "+ Capital committee (" + Db.PersonOf("VP-FIN").Name + ")");
            Step("Emergency purchases", "Proceed immediately; category manager post-review within " + p.EmergencyReviewHours + " h");
        }

        void Apply()
        {
            if (Same(draft, Db.Policy)) { App.Toast("No policy changes to apply.", Severity.Warning); return; }
            bool matcherChanged = Math.Abs(draft.SupplierMatchThreshold - Db.Policy.SupplierMatchThreshold) > 0.001f;
            Db.Policy = draft.Clone();
            if (matcherChanged) Svc.Engine.RefreshMatcher();
            int changed = Svc.Workflow.ReapplyPolicy(Svc.ActingAs.Name, Svc.Now);
            App.DataChanged(true);
            App.Toast("Policy applied by " + Svc.ActingAs.Name + ". " + changed + " open request" + (changed == 1 ? "" : "s") + " changed engagement status.", Severity.Info);
            LoadDraft();
            Preview();
        }
    }
}
