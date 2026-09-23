using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class IntakePage : HubPage
    {
        public override string Id => "intake";
        public override string Title => "New Request";
        public override string Subtitle => "Describe what the site needs - the hub classifies it, applies the engagement rules and routes it before anyone commits spend";

        TextField requester, description, qty, unit, supplier;
        DropdownField companyDrop, siteDrop, categoryDrop;
        Label totalLabel, detectedLabel, supplierHint;
        Switch contractSwitch;
        Segmented urgencySeg, neededSeg;
        VisualElement decision;
        IVisualElementScheduledItem pending;
        List<Company> companies;
        List<Location> sites = new List<Location>();
        List<Subcategory> subs;
        Evaluation lastEval;
        int recState;
        string recReason, recHeadline;
        string aiAnswer;
        bool aiBusy;
        TextField aiQuestion;

        static readonly int[] NeededDays = { 1, 7, 14, 30, 60 };

        public IntakePage(HubApp app) : base(app) { }

        protected override void Build()
        {
            Root.AddToClassList("row");
            var formScroll = UIX.Scroll("page-scroll", "intake-form").AddTo(Root);
            var form = formScroll.contentContainer;

            // Examples
            var ex = UIX.Card("Try a scenario", "Each example exercises a different rule or path. Edit any field afterwards.", out var exBody).AddTo(form);
            ex.AddToClassList("gap-bottom");
            var exRow = UIX.Div("chip-row").AddTo(exBody);
            foreach (var s in Scenarios)
            {
                var chip = UIX.Div("example-chip").AddTo(exRow);
                chip.Add(UIX.Text(s.label, "example-chip-label"));
                var sc = s;
                chip.RegisterCallback<ClickEvent>(_ => ApplyScenario(sc));
            }

            // Who & where
            var who = UIX.Card("1  Who and where", null, out var whoBody).AddTo(form);
            who.AddToClassList("gap-bottom");
            companies = Db.Companies.ToList();
            requester = UIX.Input("Your name");
            companyDrop = UIX.Dropdown(companies.Select(c => c.Code + "  ·  " + c.Name).ToList(), 1);
            siteDrop = UIX.Dropdown(new List<string> { "-" }, 0);
            var grid = UIX.Div("form-grid").AddTo(whoBody);
            UIX.Div("form-col").AddTo(grid).Add(UIX.Field("REQUESTER", requester));
            UIX.Div("form-col", "form-col--last").AddTo(grid).Add(UIX.Field("LEGAL ENTITY", companyDrop, "Company code drives the vendor master, cost centers and approval chain."));
            whoBody.Add(UIX.Field("SITE", siteDrop));
            companyDrop.RegisterValueChangedCallback(_ => { RefreshSites(null); Schedule(); });
            siteDrop.RegisterValueChangedCallback(_ => Schedule());
            requester.RegisterValueChangedCallback(_ => Schedule());

            // What
            var what = UIX.Card("2  What do you need?", null, out var whatBody).AddTo(form);
            what.AddToClassList("gap-bottom");
            description = UIX.Input("e.g. Main hoist wire rope 28mm x 500m for STS-04", true);
            whatBody.Add(UIX.Field("DESCRIPTION", description));
            description.RegisterValueChangedCallback(_ => Schedule());
            subs = Db.Subcategories.ToList();
            var catChoices = new List<string> { "Auto-detect from description (recommended)" };
            catChoices.AddRange(subs.Select(s => s.Category.Name + "  ›  " + s.Name));
            categoryDrop = UIX.Dropdown(catChoices, 0);
            categoryDrop.RegisterValueChangedCallback(_ => Schedule());
            detectedLabel = UIX.Text("", "field-hint");
            var catField = UIX.Field("CATEGORY", categoryDrop);
            catField.Add(detectedLabel);
            whatBody.Add(catField);
            var g2 = UIX.Div("form-grid").AddTo(whatBody);
            qty = UIX.Input("1");
            unit = UIX.Input("0.00");
            UIX.Div("form-col").AddTo(g2).Add(UIX.Field("QUANTITY", qty));
            UIX.Div("form-col").AddTo(g2).Add(UIX.Field("ESTIMATED UNIT PRICE (USD)", unit));
            var totalBox = UIX.Div("total-box");
            totalLabel = UIX.Text("$0", "total-value").AddTo(totalBox);
            UIX.Div("form-col", "form-col--last").AddTo(g2).Add(UIX.Field("ESTIMATED TOTAL", totalBox));
            qty.RegisterValueChangedCallback(_ => Schedule());
            unit.RegisterValueChangedCallback(_ => Schedule());

            // Commercial
            var com = UIX.Card("3  Commercial details", null, out var comBody).AddTo(form);
            com.AddToClassList("gap-bottom");
            supplier = UIX.Input("Optional - a supplier you would like to use");
            supplierHint = UIX.Text("", "field-hint");
            var supField = UIX.Field("PREFERRED SUPPLIER", supplier);
            supField.Add(supplierHint);
            comBody.Add(supField);
            supplier.RegisterValueChangedCallback(_ => Schedule());
            contractSwitch = new Switch("This purchase requires signing a contract, agreement, lease, SOW or supplier terms", false);
            contractSwitch.Changed += _ => Schedule();
            comBody.Add(UIX.Field("CONTRACTUAL COMMITMENT", contractSwitch));
            urgencySeg = new Segmented(new[] { "Routine", "Priority", "Emergency - equipment down / safety" }, 0, null, null, "segment--emergency");
            urgencySeg.Changed += _ => Schedule();
            comBody.Add(UIX.Field("URGENCY", urgencySeg, "Emergency lets operations proceed immediately; procurement completes a post-review."));
            neededSeg = new Segmented(new[] { "Tomorrow", "1 week", "2 weeks", "1 month", "2 months" }, 2);
            neededSeg.Changed += _ => Schedule();
            comBody.Add(UIX.Field("NEEDED BY", neededSeg));

            var actions = UIX.Div("btn-row").AddTo(form);
            actions.Add(UIX.Btn("Submit request", Submit, "btn--primary"));
            actions.Add(UIX.Btn("Clear form", () => { ClearForm(); Schedule(); }, "btn--ghost"));

            // Decision panel
            var panel = UIX.Scroll("side-scroll", "decision-panel").AddTo(Root);
            decision = panel.contentContainer;

            ClearForm();
        }

        public override void OnShow(object arg)
        {
            if (string.IsNullOrWhiteSpace(requester.value)) requester.value = Svc.ActingAs.Name;
            if (arg is string locId && Db.Loc(locId) != null)
            {
                var loc = Db.Loc(locId);
                companyDrop.SetValueWithoutNotify(companyDrop.choices[companies.FindIndex(c => c.Code == loc.CompanyCode)]);
                RefreshSites(locId);
            }
            Evaluate();
        }

        void ClearForm()
        {
            requester.SetValueWithoutNotify(Svc.ActingAs.Name);
            description.SetValueWithoutNotify("");
            qty.SetValueWithoutNotify("1");
            unit.SetValueWithoutNotify("");
            supplier.SetValueWithoutNotify("");
            categoryDrop.SetValueWithoutNotify(categoryDrop.choices[0]);
            contractSwitch.Value = false;
            urgencySeg.Index = 0;
            neededSeg.Index = 2;
            recState = 0;
            aiAnswer = null;
            RefreshSites(null);
        }

        void RefreshSites(string select)
        {
            int ci = companyDrop.index;
            var company = companies[Mathf.Clamp(ci, 0, companies.Count - 1)];
            sites = Db.Locations.Where(l => l.CompanyCode == company.Code).OrderBy(l => l.Name).ToList();
            siteDrop.choices = sites.Select(l => l.Name + "  ·  " + l.City).ToList();
            int idx = select != null ? sites.FindIndex(l => l.Id == select) : 0;
            siteDrop.SetValueWithoutNotify(siteDrop.choices.Count > 0 ? siteDrop.choices[Mathf.Max(0, idx)] : "-");
        }

        void Schedule()
        {
            pending?.Pause();
            pending = Root.schedule.Execute(Evaluate).StartingIn(140);
        }

        RequestDraft Draft()
        {
            UIX.TryParseNumber(qty.value, out var q);
            UIX.TryParseNumber(unit.value, out var u);
            var site = siteDrop.index >= 0 && siteDrop.index < sites.Count ? sites[siteDrop.index] : null;
            int cat = categoryDrop.index;
            return new RequestDraft
            {
                Requester = requester.value,
                CompanyCode = site?.CompanyCode,
                LocationId = site?.Id,
                Description = description.value ?? "",
                SubcategoryOverride = cat > 0 ? subs[cat - 1].Code : null,
                Quantity = q <= 0 ? 0 : q,
                UnitPrice = u,
                Urgency = (Urgency)urgencySeg.Index,
                RequiresContract = contractSwitch.Value,
                SupplierText = supplier.value,
                NeededInDays = NeededDays[neededSeg.Index],
            };
        }

        void Evaluate()
        {
            var d = Draft();
            totalLabel.text = Fmt.MoneyExact(d.Total);
            if (string.IsNullOrWhiteSpace(d.Description))
            {
                lastEval = null;
                detectedLabel.text = "Start typing a description to auto-classify.";
                supplierHint.text = "";
                RenderIdle();
                return;
            }
            var ev = Svc.Engine.Evaluate(d);
            lastEval = ev;
            if (ev.Recommendation.Headline != recHeadline) { recState = 0; recHeadline = ev.Recommendation.Headline; aiAnswer = null; }

            if (d.SubcategoryOverride != null) detectedLabel.text = "Category set manually. Auto-detect would pick: " + (ev.Classification.Count > 0 ? ev.Classification[0].Sub.Name : "nothing yet");
            else if (ev.Sub != null) detectedLabel.text = "Auto-detected: " + ev.Sub.Category.Name + " › " + ev.Sub.Name + " (" + Fmt.Pct(ev.Confidence) + " confidence)" + (ev.Item != null ? " · closest catalog item: " + ev.Item.Name : "");
            else detectedLabel.text = "No category detected yet - add more detail or pick one manually.";

            var sm = ev.SupplierMatch;
            if (string.IsNullOrWhiteSpace(d.SupplierText)) supplierHint.text = "Leave blank to let procurement assign the contracted or preferred supplier.";
            else if (sm == null || !sm.IsConfident) supplierHint.text = "Not found in the vendor master" + (sm != null ? " - closest match " + sm.Supplier.Name + " (" + Fmt.Pct(sm.Similarity) + ")" : "") + ". Using a new supplier triggers Rule 3.";
            else supplierHint.text = "Matched to " + sm.Supplier.Name + " (" + Fmt.Pct(sm.Similarity) + ")" + (sm.MatchedName != sm.Supplier.Name ? " via vendor record \"" + sm.MatchedName + "\"" : "") + ".";

            RenderDecision(ev);
        }

        // ------------------------------------------------------------------ decision panel

        void RenderIdle()
        {
            decision.Clear();
            var banner = UIX.Div("decision-banner", "decision-banner--idle").AddTo(decision);
            banner.Add(UIX.Text("LIVE DECISION", "decision-kicker"));
            banner.Add(UIX.Text("Waiting for a description", "decision-title"));
            banner.Add(UIX.Text("As you fill the form, this panel classifies the need, checks the three engagement rules, routes it to the right category manager, suggests contracts and benchmarks the price.", "decision-summary"));
            var rules = UIX.Card("The three engagement rules", "Placeholder policy - edit in Policy & Routing to match the official category poster.", out var rb).AddTo(decision);
            var p = Db.Policy;
            RuleRow(rb, IconKind.Minus, Palette.Text3, "Rule 1 - Spend threshold", "", "Purchases at or above " + Fmt.MoneyExact(p.ValueThreshold) + ", including related spend that looks split.", false);
            RuleRow(rb, IconKind.Minus, Palette.Text3, "Rule 2 - Contractual commitment", "", "Any contract, agreement, lease, SOW or supplier terms - regardless of value.", false);
            RuleRow(rb, IconKind.Minus, Palette.Text3, "Rule 3 - New or non-approved supplier", "", "Suppliers not in the vendor master or not approved for the category.", true);
        }

        void RenderDecision(Evaluation ev)
        {
            decision.Clear();
            var eng = ev.Engagement;
            string cls = eng.EmergencyPath ? "decision-banner--emergency" : eng.Required ? "decision-banner--required" : "decision-banner--selfserve";
            var banner = UIX.Div("decision-banner", cls).AddTo(decision);
            banner.Add(UIX.Text("LIVE DECISION", "decision-kicker"));
            banner.Add(UIX.Text(eng.Decision, "decision-title"));
            banner.Add(UIX.Text(eng.Summary, "decision-summary"));

            // Rules
            var rules = UIX.Card("Engagement rules", null, out var rb).AddTo(decision);
            rules.AddToClassList("gap-bottom");
            for (int i = 0; i < eng.Rules.Count; i++)
            {
                var r = eng.Rules[i];
                var kind = !r.Enabled ? IconKind.Minus : r.Triggered ? IconKind.Warning : IconKind.Check;
                var color = !r.Enabled ? Palette.Text3 : r.Triggered ? Palette.Warning : Palette.Good;
                string state = !r.Enabled ? "DISABLED" : r.Triggered ? "APPLIES" : "CLEAR";
                RuleRow(rb, kind, color, r.Title, state, r.Detail, i == eng.Rules.Count - 1);
            }

            // Classification
            var clsCard = UIX.Card("Classification", ev.Draft.SubcategoryOverride != null ? "Set manually by requester" : "Keyword and catalog evidence - every point is traceable", out var cb).AddTo(decision);
            clsCard.AddToClassList("gap-bottom");
            if (ev.Classification.Count == 0) cb.Add(UIX.Text("No matching category terms yet.", "body-text"));
            for (int i = 0; i < ev.Classification.Count; i++)
            {
                var c = ev.Classification[i];
                var row = UIX.Div("conf-row").AddTo(cb);
                var head = UIX.Div("conf-head").AddTo(row);
                head.Add(UIX.Text(c.Sub.Category.Name + "  ›  " + c.Sub.Name, "conf-name", i == 0 ? "conf-name--top" : null));
                head.Add(UIX.Text(Fmt.Pct(c.Confidence), "conf-pct"));
                var bar = UIX.Div("progress").AddTo(row);
                var fill = UIX.Div("progress-fill").AddTo(bar);
                fill.style.width = Length.Percent(c.Confidence * 100);
                if (i > 0) fill.style.backgroundColor = Palette.Text3;
                row.Add(UIX.Text("Evidence: " + string.Join(", ", c.Evidence.Distinct().Take(5)), "conf-evidence"));
            }

            // Routing
            var route = UIX.Card("Routing and approvals", ev.Routing.Reason, out var rtb).AddTo(decision);
            route.AddToClassList("gap-bottom");
            if (ev.Routing.Manager != null)
            {
                var m = UIX.Div("persona").AddTo(rtb);
                m.Add(UIX.Avatar(ev.Routing.Manager.Name, true));
                var mt = UIX.Div("grow").AddTo(m);
                mt.Add(UIX.Text(ev.Routing.Manager.Name, "persona-name"));
                mt.Add(UIX.Text(ev.Routing.Manager.Title + " · " + ev.Routing.Manager.Email, "persona-title"));
            }
            int n = 1;
            foreach (var a in ev.Routing.Approvals)
            {
                var step = UIX.Div("approval-step").AddTo(rtb);
                step.Add(UIX.Text((n++).ToString(), "approval-num"));
                var t = UIX.Div("grow").AddTo(step);
                t.Add(UIX.Text(a.Role + ": " + a.Name, "text-1", "small", "strong"));
                t.Add(UIX.Text(a.Why, "muted", "small", "wrap"));
            }

            // Contracts & suppliers
            var ks = UIX.Card("Contracts and suppliers", ev.Contracts.Count > 0 ? "Existing agreements that cover this site" : "No agreement covers this category at this site", out var kb).AddTo(decision);
            ks.AddToClassList("gap-bottom");
            bool first = true;
            foreach (var c in ev.Contracts)
            {
                var card = UIX.Div("option-card").AddTo(kb);
                if (first) card.AddToClassList("option-card--best");
                var top = UIX.Div("row-center").AddTo(card);
                var tl = UIX.Div("grow").AddTo(top);
                tl.Add(UIX.Text(c.Contract.Id + "  ·  " + c.Supplier.Name, "option-title"));
                tl.Add(UIX.Text(c.Coverage + " · ends " + Fmt.Date(c.Contract.End) + " (" + c.DaysLeft + " days) · " + c.Contract.Terms, "option-sub"));
                if (c.UnitPrice.HasValue) top.Add(UIX.Text(Fmt.Unit(c.UnitPrice.Value), "option-price"));
                var bottom = UIX.Div("row-center", "mt-8").AddTo(card);
                if (c.EstSavings > 0) bottom.Add(UIX.Text("Saves ~" + Fmt.MoneyExact(c.EstSavings) + " vs your estimate", "savings"));
                bottom.Add(UIX.Div("spacer"));
                var sc = c;
                bottom.Add(UIX.Btn("Use this contract", () =>
                {
                    supplier.value = sc.Supplier.Name;
                    if (sc.UnitPrice.HasValue) unit.value = sc.UnitPrice.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                }, "btn--small"));
                first = false;
            }
            if (ev.Contracts.Count == 0)
            {
                kb.Add(UIX.Text("Suggested suppliers (approved for " + (ev.Sub?.Name ?? "this category") + "):", "body-text"));
                foreach (var s in ev.Suppliers.Take(4))
                {
                    var card = UIX.Div("option-card").AddTo(kb);
                    var top = UIX.Div("row-center").AddTo(card);
                    var tl = UIX.Div("grow").AddTo(top);
                    tl.Add(UIX.Text(s.Supplier.Name, "option-title"));
                    tl.Add(UIX.Text(s.Supplier.City + " · score " + Fmt.Pct(s.Supplier.Score) + " · " + Fmt.Money(s.Spend12M) + " network spend · " + s.Sites + " sites" + (s.DistanceKm < 800 ? " · local" : ""), "option-sub"));
                    var sc = s;
                    top.Add(UIX.Btn("Use", () => supplier.value = sc.Supplier.Name, "btn--small"));
                }
            }

            // Price benchmark
            if (ev.Benchmark != null)
            {
                var b = ev.Benchmark;
                var pc = UIX.Card("Price check", b.Item.Name + " · " + b.Samples + " network purchases in 12 months", out var pb).AddTo(decision);
                pc.AddToClassList("gap-bottom");
                var range = new RangeBar().AddTo(pb);
                range.SetData(b);
                string msg = b.Requested <= 0 ? "Enter an estimated unit price to compare." :
                    b.Flagged ? "Your estimate is " + Fmt.SignedPct(b.VarianceVsMedian) + " versus the network median - above the " + Fmt.Pct(Db.Policy.PriceTolerance) + " tolerance." :
                    "Your estimate is within the normal range (" + Fmt.SignedPct(b.VarianceVsMedian) + " vs median).";
                var m = UIX.Text(msg, "body-text").AddTo(pb);
                m.style.marginTop = 22;
            }

            RenderAssistant(ev);
        }

        void RuleRow(VisualElement parent, IconKind kind, Color color, string title, string state, string detail, bool last)
        {
            var row = UIX.Div("rule-row").AddTo(parent);
            if (last) row.AddToClassList("rule-row--last");
            row.Add(new Icon(kind, color).Cls("rule-icon"));
            var body = UIX.Div("grow").AddTo(row);
            var head = UIX.Div("row-center").AddTo(body);
            head.Add(UIX.Text(title, "rule-title"));
            if (!string.IsNullOrEmpty(state))
            {
                var st = UIX.Text(state, "rule-state").AddTo(head);
                st.style.color = color;
            }
            body.Add(UIX.Text(detail, "rule-detail"));
        }

        void RenderAssistant(Evaluation ev)
        {
            var rec = ev.Recommendation;
            var card = UIX.Card("Assistant recommendation", "Explains its reasoning. Nothing happens until a person approves.", out var body, "assistant-card").AddTo(decision);
            var right = UIX.CardHeaderRight(card);
            right.Add(new Icon(IconKind.Sparkle, Palette.Series1) { style = { width = 16, height = 16 } });
            body.Add(UIX.Text(rec.Headline, "assistant-headline"));
            var conf = UIX.Div("row-center", "mt-8").AddTo(body);
            conf.Add(UIX.Text("Confidence " + Fmt.Pct(rec.Confidence), "muted", "small"));
            var bar = UIX.Div("progress", "grow").AddTo(conf);
            bar.style.marginLeft = 10;
            UIX.Div("progress-fill").AddTo(bar).style.width = Length.Percent(rec.Confidence * 100);

            body.Add(UIX.Text("WHY", "section-label"));
            foreach (var r in rec.Reasons) body.Add(UIX.Bullet(r));
            if (rec.Risks.Count > 0)
            {
                body.Add(UIX.Text("WATCH OUTS", "section-label"));
                foreach (var r in rec.Risks) body.Add(UIX.Bullet(r, true));
            }
            body.Add(UIX.Text("WHAT I CHECKED", "section-label"));
            body.Add(UIX.Text(string.Join("  ·  ", rec.Checks), "check-text"));

            var gate = UIX.Div("human-gate").AddTo(body);
            string who = Svc.ActingAs.Name;
            if (recState == 0)
            {
                gate.Add(UIX.Text("Pending human review. Accept to record your approval, or override with a reason.", "gate-state"));
                gate.Add(UIX.Btn("Override...", () => App.Prompt("Override recommendation", "Explain why you are not following the recommendation. This is stored in the request's audit trail.", "Reason", "Override", reason => { recState = 2; recReason = reason; Evaluate(); }), "btn--small"));
                var acc = UIX.Btn("Accept", () => { recState = 1; Evaluate(); }, "btn--small", "btn--primary");
                acc.style.marginLeft = 8;
                gate.Add(acc);
            }
            else
            {
                gate.Add(UIX.Text(recState == 1 ? "Accepted by " + who + " - recorded when you submit." : "Overridden by " + who + ": \"" + recReason + "\"", "gate-state"));
                gate.Add(UIX.Btn("Undo", () => { recState = 0; Evaluate(); }, "btn--small", "btn--ghost"));
            }

            body.Add(UIX.Text("ASK A FOLLOW-UP", "section-label"));
            if (ClaudeAssistant.IsConfigured)
            {
                aiQuestion = UIX.Input("e.g. Why does this need procurement? Could we avoid the director approval?").AddTo(body);
                var askRow = UIX.Div("btn-row", "mt-8").AddTo(body);
                var ask = UIX.Btn(aiBusy ? "Asking Claude..." : "Ask Claude", () => AskClaude(ev), "btn--small");
                ask.SetEnabled(!aiBusy);
                askRow.Add(ask);
                askRow.Add(UIX.Text("Sends this request's summary to the Claude API (claude-opus-5).", "muted", "small"));
                if (aiAnswer != null) body.Add(UIX.Text(aiAnswer, "ai-answer"));
            }
            else
            {
                body.Add(UIX.Text("Optional: set the ANTHROPIC_API_KEY environment variable before launching to ask Claude natural-language questions about this recommendation. The rules engine above works fully offline.", "check-text"));
            }
        }

        void AskClaude(Evaluation ev)
        {
            string q = aiQuestion?.value;
            if (string.IsNullOrWhiteSpace(q)) q = "Explain this recommendation and what the requester should do next.";
            aiBusy = true;
            aiAnswer = null;
            RenderDecision(ev);
            App.StartCoroutine(ClaudeAssistant.Ask(ClaudeAssistant.Describe(Db, ev.Draft, ev), q,
                answer => { aiBusy = false; aiAnswer = answer; if (lastEval != null) RenderDecision(lastEval); },
                error => { aiBusy = false; aiAnswer = "Could not reach Claude: " + error; if (lastEval != null) RenderDecision(lastEval); }));
        }

        // ------------------------------------------------------------------ submit

        void Submit()
        {
            var d = Draft();
            if (d.LocationId == null) { App.Toast("Choose a site.", Severity.Warning); return; }
            if (string.IsNullOrWhiteSpace(d.Description) || d.Description.Trim().Length < 6) { App.Toast("Describe what you need (a few words at least).", Severity.Warning); return; }
            if (d.Quantity <= 0 || d.UnitPrice <= 0) { App.Toast("Enter a quantity and an estimated unit price.", Severity.Warning); return; }
            var ev = Svc.Engine.Evaluate(d);
            if (ev.Sub == null) { App.Toast("The item could not be classified - pick a category manually.", Severity.Warning); return; }
            string actor = string.IsNullOrWhiteSpace(d.Requester) ? Svc.ActingAs.Name : d.Requester.Trim();
            var req = Svc.Workflow.Submit(d, ev, actor, recState == 1, Svc.Now);
            if (recState == 2) Svc.Workflow.SetRecommendation(req, false, Svc.ActingAs.Name, recReason, Svc.Now);
            App.DataChanged(true);
            App.Toast(req.Id + " submitted - " + (req.EngagementRequired && !req.EmergencyPath ? "routed to " + ev.Routing.Manager.Name : req.EmergencyPath ? "emergency PO issued, post-review queued" : "self-serve PO " + req.PoNumber + " issued") + ".", Severity.Info);
            ClearForm();
            App.Navigate("requests", req.Id);
        }

        // ------------------------------------------------------------------ scenarios

        sealed class Scenario
        {
            public string label, site, text, supplier;
            public double qty, unit;
            public Urgency urgency;
            public bool contract;
        }

        static readonly Scenario[] Scenarios =
        {
            new Scenario { label = "Wire rope for STS crane", site = "CT-TAC", text = "Main hoist wire rope 28mm x 500m for STS-04 scheduled rope change", qty = 2, unit = 15800, supplier = "Titan Wire Rope" },
            new Scenario { label = "Small PPE reorder (self-serve)", site = "CR-MIA", text = "Hi-vis vests (case of 50) for cruise season staff", qty = 4, unit = 640, supplier = "Northstar Safety Supply" },
            new Scenario { label = "Forklift rental for vessel surge", site = "CV-HOU", text = "Forklift rental, 15k lb (monthly) for steel vessel surge", qty = 3, unit = 6900, supplier = "Liftpoint Rentals" },
            new Scenario { label = "Emergency: top handler down", site = "PRS-CHI", text = "Hydraulic pump, top handler - TH-07 down, trains waiting", qty = 1, unit = 11200, supplier = "Keystone Fleet Parts", urgency = Urgency.Emergency },
            new Scenario { label = "New SaaS subscription", site = "TWT-SEA", text = "Maintenance management SaaS (annual) subscription for field engineering team", qty = 1, unit = 99000, supplier = "Beacon Analytics", contract = true },
            new Scenario { label = "Unknown crane repair vendor", site = "CT-OAK", text = "Hoist brake assembly replacement on STS-02, quote from QuickFix Crane Repair", qty = 1, unit = 21400, supplier = "QuickFix Crane Repair" },
            new Scenario { label = "Electric hostlers (capex)", site = "CT-LAX", text = "Battery-electric yard tractor for zero-emission pilot", qty = 5, unit = 342000, supplier = "" },
            new Scenario { label = "Messy vendor name", site = "CT-SEA", text = "Twistlock set (4) for spreader SP-12", qty = 6, unit = 3450, supplier = "PAC CRANE PTS INC - TACOMA" },
        };

        void ApplyScenario(Scenario s)
        {
            var loc = Db.Loc(s.site);
            companyDrop.SetValueWithoutNotify(companyDrop.choices[companies.FindIndex(c => c.Code == loc.CompanyCode)]);
            RefreshSites(loc.Id);
            description.SetValueWithoutNotify(s.text);
            qty.SetValueWithoutNotify(s.qty.ToString(System.Globalization.CultureInfo.InvariantCulture));
            unit.SetValueWithoutNotify(s.unit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            supplier.SetValueWithoutNotify(s.supplier);
            categoryDrop.SetValueWithoutNotify(categoryDrop.choices[0]);
            contractSwitch.Value = s.contract;
            urgencySeg.Index = (int)s.urgency;
            neededSeg.Index = s.urgency == Urgency.Emergency ? 0 : 2;
            recState = 0;
            aiAnswer = null;
            Evaluate();
        }
    }
}
