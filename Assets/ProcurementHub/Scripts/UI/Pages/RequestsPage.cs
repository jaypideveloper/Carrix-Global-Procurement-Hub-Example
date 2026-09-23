using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class RequestsPage : HubPage
    {
        public override string Id => "requests";
        public override string Title => "Request Queue";
        public override string Subtitle => "Every request from intake to closed PO - with rule outcomes, routing, approvals and a full audit trail";

        TextField search;
        DropdownField stageDrop, unitDrop, engDrop;
        Label countLabel;
        DataTable<ProcurementRequest> table;
        VisualElement detail;
        ProcurementRequest selected;

        static readonly string[] StageFilters = { "All open", "All requests", "Intake", "Routed", "Sourcing", "Approval", "PO issued", "Received", "Invoice matched", "Closed", "Rejected" };
        static readonly string[] UnitFilters = { "All business units", "SSA Marine", "RMS", "Tideworks", "Corporate" };
        static readonly string[] EngFilters = { "Any path", "Procurement engaged", "Self-serve", "Emergency", "Needs post-review" };

        public RequestsPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            var bar = UIX.Div("filter-bar").AddTo(Root);
            search = UIX.Input("Search ID, site, supplier, description...").Cls("filter-item", "filter-search", "hub-input--search").AddTo(bar);
            stageDrop = UIX.Dropdown(StageFilters.ToList(), 0).Cls("filter-item", "filter-drop").AddTo(bar);
            unitDrop = UIX.Dropdown(UnitFilters.ToList(), 0).Cls("filter-item", "filter-drop").AddTo(bar);
            engDrop = UIX.Dropdown(EngFilters.ToList(), 0).Cls("filter-item", "filter-drop").AddTo(bar);
            UIX.Div("spacer").AddTo(bar);
            countLabel = UIX.Text("", "count-label").AddTo(bar);
            var newBtn = UIX.Btn("New request", () => App.Navigate("intake"), "btn--primary");
            newBtn.style.marginLeft = 12;
            bar.Add(newBtn);
            search.RegisterValueChangedCallback(_ => Apply());
            stageDrop.RegisterValueChangedCallback(_ => Apply());
            unitDrop.RegisterValueChangedCallback(_ => Apply());
            engDrop.RegisterValueChangedCallback(_ => Apply());

            var split = UIX.Div("split").AddTo(Root);
            var tablePane = UIX.Div("table-pane").AddTo(split);
            table = new DataTable<ProcurementRequest>(new List<DataTable<ProcurementRequest>.Col>
            {
                new DataTable<ProcurementRequest>.Col { Title = "ID", Width = 98, Text = r => r.Id, Sort = r => r.Id, CellClass = "td--strong" },
                new DataTable<ProcurementRequest>.Col { Title = "Created", Width = 84, Text = r => Fmt.ShortDate(r.Created), Sort = r => r.CreatedTicks, CellClass = "td--muted" },
                new DataTable<ProcurementRequest>.Col { Title = "Site", Grow = 1.3f, Text = r => Db.Loc(r.LocationId)?.Name, Sort = r => Db.Loc(r.LocationId)?.Name },
                new DataTable<ProcurementRequest>.Col { Title = "Description", Grow = 2f, Text = r => r.Description, Sort = r => r.Description },
                new DataTable<ProcurementRequest>.Col { Title = "Category", Grow = 1f, Text = r => Db.Sub(r.SubcategoryCode)?.Name, Sort = r => Db.Sub(r.SubcategoryCode)?.Name },
                new DataTable<ProcurementRequest>.Col { Title = "Value", Width = 90, Right = true, Text = r => Fmt.Money(r.Total), Sort = r => r.Total, CellClass = "td--strong" },
                new DataTable<ProcurementRequest>.Col { Title = "Path", Width = 132, Text = PathLabel, Sort = r => PathLabel(r), Status = PathStatus },
                new DataTable<ProcurementRequest>.Col { Title = "Stage", Width = 124, Text = r => Labels.Stage(r.Stage), Sort = r => (int)r.Stage, Status = r => UIX.StageStatus(r.Stage) },
                new DataTable<ProcurementRequest>.Col { Title = "Owner", Width = 118, Text = r => Db.PersonOf(r.AssignedManagerId)?.Name, Sort = r => Db.PersonOf(r.AssignedManagerId)?.Name },
            });
            table.AddTo(tablePane);
            table.Selected += r => ShowDetail(r);
            table.SortBy(1, true);

            var pane = UIX.Scroll("side-scroll", "detail-pane").AddTo(split);
            detail = pane.contentContainer;
            detail.Add(UIX.EmptyState("Select a request", "Its rule outcomes, routing, lifecycle and audit trail appear here."));
        }

        static string PathLabel(ProcurementRequest r) => r.EmergencyPath ? "Emergency" : r.EngagementRequired ? "Engaged " + r.TriggeredRules.Replace(",", "+") : "Self-serve";
        static string PathStatus(ProcurementRequest r) => r.EmergencyPath ? (r.PostReviewDone ? "serious" : "critical") : r.EngagementRequired ? "warning" : "good";

        public override void OnShow(object arg)
        {
            Apply();
            if (arg is string id)
            {
                var r = Db.Requests.FirstOrDefault(x => x.Id == id);
                if (r != null)
                {
                    if (!table.Items.Contains(r)) { stageDrop.SetValueWithoutNotify(StageFilters[1]); unitDrop.SetValueWithoutNotify(UnitFilters[0]); engDrop.SetValueWithoutNotify(EngFilters[0]); search.SetValueWithoutNotify(""); Apply(); }
                    table.Select(r, false);
                    ShowDetail(r);
                }
            }
            else if (selected != null) ShowDetail(selected);
        }

        void Apply()
        {
            IEnumerable<ProcurementRequest> q = Db.Requests;
            int st = stageDrop.index;
            if (st == 0) q = q.Where(r => r.IsOpen);
            else if (st >= 2) { var stage = (RequestStage)(st - 2); q = q.Where(r => r.Stage == stage); }
            int u = unitDrop.index;
            if (u > 0)
            {
                var unit = u == 1 ? BusinessUnit.SSAMarine : u == 2 ? BusinessUnit.RMS : u == 3 ? BusinessUnit.Tideworks : BusinessUnit.Corporate;
                q = q.Where(r => Db.Loc(r.LocationId)?.Unit == unit);
            }
            int e = engDrop.index;
            if (e == 1) q = q.Where(r => r.EngagementRequired && !r.EmergencyPath);
            else if (e == 2) q = q.Where(r => !r.EngagementRequired && !r.EmergencyPath);
            else if (e == 3) q = q.Where(r => r.EmergencyPath);
            else if (e == 4) q = q.Where(r => r.EmergencyPath && !r.PostReviewDone);
            string s = search.value?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(s))
                q = q.Where(r => (r.Id + " " + r.Description + " " + Db.Loc(r.LocationId)?.Name + " " + Db.SupplierOf(r.SupplierId)?.Name + " " + r.SupplierText + " " + r.Requester + " " + Db.Sub(r.SubcategoryCode)?.Name).ToLowerInvariant().Contains(s));
            var list = q.ToList();
            table.SetItems(list);
            countLabel.text = list.Count + " requests · " + Fmt.Money(list.Sum(r => r.Total));
        }

        void ShowDetail(ProcurementRequest r)
        {
            selected = r;
            detail.Clear();
            if (r == null) { detail.Add(UIX.EmptyState("Select a request", null)); return; }
            var loc = Db.Loc(r.LocationId);
            var sub = Db.Sub(r.SubcategoryCode);
            var mgr = Db.PersonOf(r.AssignedManagerId);
            var sup = Db.SupplierOf(r.SupplierId);

            var head = UIX.Div("row-center").AddTo(detail);
            head.Add(UIX.Text(r.Id, "card-title"));
            UIX.Div("spacer").AddTo(head);
            head.Add(UIX.Pill(Labels.Stage(r.Stage), UIX.StageStatus(r.Stage)));
            if (r.Urgency != Urgency.Routine) { var up = UIX.Pill(r.Urgency.ToString(), r.Urgency == Urgency.Emergency ? "critical" : "warning"); up.style.marginLeft = 6; head.Add(up); }
            detail.Add(UIX.Text(r.Description, "detail-title"));
            detail.Add(UIX.Text("Requested by " + r.Requester + " · " + Fmt.Stamp(r.Created) + " · needed in " + r.NeededInDays + " day" + (r.NeededInDays == 1 ? "" : "s"), "muted", "small", "mt-8"));

            // Facts
            var facts = UIX.Card(null, null, out var fb).AddTo(detail);
            facts.AddToClassList("mt-12");
            fb.Add(UIX.KV("Site", loc.Name));
            fb.Add(UIX.KV("Legal entity", loc.Company.Code + " · " + loc.Company.ShortName));
            fb.Add(UIX.KV("Category", sub != null ? sub.Category.Name + " › " + sub.Name + (r.CategoryOverridden ? " (manual)" : " (" + Fmt.Pct(r.ClassificationConfidence) + ")") : "-"));
            fb.Add(UIX.KV("Quantity × unit", Fmt.Num(r.Quantity) + " × " + Fmt.Unit(r.UnitPrice) + " = " + Fmt.MoneyExact(r.Total)));
            fb.Add(UIX.KV("Supplier", sup != null ? sup.Name : (string.IsNullOrWhiteSpace(r.SupplierText) ? "To be sourced" : r.SupplierText + " (not in master)")));
            fb.Add(UIX.KV("Contract", r.ContractId != null ? r.ContractId + " · " + Db.ScopeLabel(Db.ContractOf(r.ContractId)) : "None - competitive sourcing"));
            fb.Add(UIX.KV("Category manager", mgr != null ? mgr.Name : "-"));
            fb.Add(UIX.KV("Purchase order", r.PoNumber ?? "Not issued yet"));

            // Engagement
            var engCard = UIX.Card("Engagement decision", r.EngagementSummary, out var eb).AddTo(detail);
            engCard.AddToClassList("mt-12");
            var ev = Svc.Workflow.Reevaluate(r);
            foreach (var rule in ev.Engagement.Rules)
            {
                var row = UIX.Div("rule-row").AddTo(eb);
                bool on = rule.Enabled && rule.Triggered;
                row.Add(new Icon(!rule.Enabled ? IconKind.Minus : on ? IconKind.Warning : IconKind.Check, !rule.Enabled ? Palette.Text3 : on ? Palette.Warning : Palette.Good).Cls("rule-icon"));
                var b = UIX.Div("grow").AddTo(row);
                b.Add(UIX.Text(rule.Title, "rule-title"));
                b.Add(UIX.Text(rule.Detail, "rule-detail"));
            }
            if (r.EmergencyPath)
            {
                var pr = UIX.Div("row-center", "mt-8").AddTo(eb);
                pr.Add(UIX.Dot(r.PostReviewDone ? "dot--good" : "dot--critical"));
                pr.Add(UIX.Text(r.PostReviewDone ? "Emergency post-review completed" : "Emergency post-review pending (due within " + Db.Policy.EmergencyReviewHours + "h of the purchase)", "body-text"));
            }

            // Lifecycle
            var life = UIX.Card("Lifecycle", r.EngagementRequired ? "Procurement-engaged path" : "Self-serve path (catalog / contract PO)", out var lb).AddTo(detail);
            life.AddToClassList("mt-12");
            RenderTimeline(lb, r);

            // Recommendation
            var recCard = UIX.Card("Assistant recommendation", "Human approval required", out var rb, "assistant-card").AddTo(detail);
            recCard.AddToClassList("mt-12");
            rb.Add(UIX.Text(r.RecommendationText ?? ev.Recommendation.Headline, "assistant-headline"));
            foreach (var reason in ev.Recommendation.Reasons.Take(4)) rb.Add(UIX.Bullet(reason));
            foreach (var risk in ev.Recommendation.Risks.Take(3)) rb.Add(UIX.Bullet(risk, true));
            var gate = UIX.Div("human-gate").AddTo(rb);
            if (r.RecommendationState == 1) gate.Add(UIX.Text("Accepted - see audit trail for who approved it.", "gate-state"));
            else if (r.RecommendationState == 2) gate.Add(UIX.Text("Overridden: \"" + r.OverrideReason + "\"", "gate-state"));
            else
            {
                gate.Add(UIX.Text("Awaiting review by the category manager.", "gate-state"));
                gate.Add(UIX.Btn("Override...", () => App.Prompt("Override recommendation", "Why is the recommendation not being followed? This is stored in the audit trail.", "Reason", "Override",
                    reason => { Svc.Workflow.SetRecommendation(r, false, Svc.ActingAs.Name, reason, Svc.Now); Changed(r, "Recommendation overridden."); }), "btn--small"));
                var acc = UIX.Btn("Accept", () => { Svc.Workflow.SetRecommendation(r, true, Svc.ActingAs.Name, null, Svc.Now); Changed(r, "Recommendation accepted."); }, "btn--small", "btn--primary");
                acc.style.marginLeft = 8;
                gate.Add(acc);
            }

            // Actions
            var actions = UIX.Div("btn-row", "mt-16").AddTo(detail);
            var next = Svc.Workflow.NextStage(r);
            if (next != null)
            {
                var stage = next.Value;
                actions.Add(UIX.Btn(Workflow.ActionLabel(stage), () =>
                {
                    Svc.Workflow.Advance(r, Svc.ActingAs.Name, Svc.Now);
                    Changed(r, r.Id + ": " + Labels.Stage(r.Stage) + ".");
                }, "btn--primary"));
            }
            if (r.EmergencyPath && !r.PostReviewDone && r.Stage != RequestStage.Rejected)
                actions.Add(UIX.Btn("Complete post-review", () => { Svc.Workflow.CompletePostReview(r, Svc.ActingAs.Name, Svc.Now); Changed(r, "Post-review completed for " + r.Id + ".", true); }));
            if (r.IsOpen && (r.Stage == RequestStage.Intake || r.Stage == RequestStage.Routed || r.Stage == RequestStage.Sourcing || r.Stage == RequestStage.Approval))
                actions.Add(UIX.Btn("Reject...", () => App.Prompt("Reject " + r.Id, "The requester is notified with your reason.", "Reason", "Reject",
                    reason => { Svc.Workflow.Reject(r, Svc.ActingAs.Name, reason, Svc.Now); Changed(r, r.Id + " rejected.", true); }), "btn--danger"));
            detail.Add(UIX.Text("Actions are recorded as " + Svc.ActingAs.Name + " (" + Svc.ActingAs.Title + "). Switch persona in the sidebar.", "field-hint"));

            // Audit trail
            var audit = UIX.Card("Audit trail", r.Events.Count + " events", out var ab).AddTo(detail);
            audit.AddToClassList("mt-12");
            foreach (var e in r.Events.OrderByDescending(x => x.AtTicks))
            {
                var item = UIX.Div("log-item").AddTo(ab);
                var h = UIX.Div("log-head").AddTo(item);
                h.Add(UIX.Text(e.Actor, "log-actor"));
                h.Add(UIX.Text(Fmt.Stamp(e.At), "log-time"));
                item.Add(UIX.Text(e.Note, "log-note"));
            }
        }

        void RenderTimeline(VisualElement parent, ProcurementRequest r)
        {
            var path = Workflow.PathFor(r);
            int currentIdx = Array.IndexOf(path, r.Stage);
            bool rejected = r.Stage == RequestStage.Rejected;
            if (rejected)
            {
                // Show how far it got before rejection.
                currentIdx = -1;
                foreach (var e in r.Events) { int i = Array.IndexOf(path, e.Stage); if (i > currentIdx) currentIdx = i; }
            }
            for (int i = 0; i < path.Length; i++)
            {
                var stage = path[i];
                bool done = i < currentIdx || (i == currentIdx && stage == RequestStage.Closed);
                bool current = i == currentIdx && !done;
                var ev = r.Events.LastOrDefault(e => e.Stage == stage);
                var step = UIX.Div("timeline-step").AddTo(parent);
                var rail = UIX.Div("timeline-rail").AddTo(step);
                var node = UIX.Div("timeline-node").AddTo(rail);
                if (done) node.AddToClassList("timeline-node--done");
                if (current) node.AddToClassList(rejected ? "timeline-node--rejected" : "timeline-node--current");
                if (i < path.Length - 1) UIX.Div("timeline-line").AddTo(rail);
                var body = UIX.Div("timeline-body").AddTo(step);
                var title = UIX.Text(Labels.Stage(stage) + (current && rejected ? " - rejected here" : ""), "timeline-title").AddTo(body);
                if (done) title.AddToClassList("timeline-title--done");
                if (current) title.AddToClassList("timeline-title--current");
                if (ev != null && (done || current)) body.Add(UIX.Text(Fmt.Stamp(ev.At) + " · " + ev.Actor, "timeline-time"));
                else if (current) body.Add(UIX.Text("In progress · " + Fmt.Ago(r.StageEntered, Svc.Now), "timeline-time"));
            }
        }

        void Changed(ProcurementRequest r, string message, bool rerunAlerts = false)
        {
            App.DataChanged(rerunAlerts);
            App.Toast(message + " Logged as " + Svc.ActingAs.Name + ".", Severity.Info);
            table.RefreshRows();
            ShowDetail(r);
        }
    }
}
