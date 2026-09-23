using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class DataQualityPage : HubPage
    {
        public override string Id => "quality";
        public override string Title => "Data Quality";
        public override string Subtitle => "Turning fragmented legacy and ERP data into one trustworthy procurement dataset - every fix needs human approval";

        sealed class Suggestion
        {
            public PurchaseLine Line;
            public ClassificationResult Top;
        }

        KpiTile kRecords, kClusters, kUncl, kMatch, kLegacy;
        VisualElement clusterList;
        Label clusterSummary;
        StackedColumns migration;
        DataTable<Suggestion> uncl;
        DataTable<PurchaseLine> matches;
        Label unclSummary;
        List<Suggestion> suggestions = new List<Suggestion>();
        int clusterFilter;

        public DataQualityPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            var scroll = UIX.Scroll("page-scroll").AddTo(Root);
            var c = scroll.contentContainer;
            var kpis = UIX.Div("kpi-row").AddTo(c);
            kRecords = new KpiTile("Vendor records → real suppliers").AddTo(kpis);
            kClusters = new KpiTile("Duplicate clusters to review").AddTo(kpis);
            kUncl = new KpiTile("Unclassified spend (12 mo)").AddTo(kpis);
            kMatch = new KpiTile("3-way match exceptions (120 d)").AddTo(kpis);
            kLegacy = new KpiTile("Spend still from legacy sources", false, false, true).AddTo(kpis);

            var row1 = UIX.Div("row", "gap-bottom").AddTo(c);
            var cc = UIX.Card("Supplier match clusters", "Fuzzy matching (normalization + token overlap + Jaro-Winkler) groups vendor records that are the same real supplier", out var cb).AddTo(row1);
            cc.style.flexGrow = 1.2f; cc.style.flexBasis = 0; cc.AddToClassList("gap-right");
            var chips = new ChipGroup(new[] { "Pending review", "Merged", "All" }, null, 0).AddTo(cb);
            chips.Changed += i => { clusterFilter = i; RenderClusters(); };
            clusterSummary = UIX.Text("", "field-hint").AddTo(cb);
            clusterList = UIX.Div("mt-8").AddTo(cb);
            clusterList.style.maxHeight = 640;
            var clusterScroll = UIX.Scroll().AddTo(clusterList);
            clusterScroll.style.maxHeight = 620;
            clusterList.userData = clusterScroll;

            var mc = UIX.Card("Migration to IFS ERP", "Monthly spend by source system - legacy AP, site spreadsheets and P-cards are phased out as entities go live", out var mb).AddTo(row1);
            mc.style.flexGrow = 1; mc.style.flexBasis = 0;
            migration = new StackedColumns(220).AddTo(mb);
            mb.Add(UIX.Text("GO-LIVE SCHEDULE", "section-label"));
            foreach (var co in Db.Companies.OrderBy(x => x.ErpGoLive))
            {
                bool live = co.ErpLiveOn(Db.Today);
                var row = UIX.Div("kv").AddTo(mb);
                var k = UIX.Div("row-center").AddTo(row);
                k.Add(UIX.Dot(live ? "dot--good" : "dot--neutral"));
                k.Add(UIX.Text(co.ShortName, "kv-key"));
                row.Add(UIX.Text((live ? "Live since " : "Planned ") + Fmt.Date(co.ErpGoLive), "kv-value"));
            }

            var row2 = UIX.Div("row").AddTo(c);
            var uc = UIX.Card("Unclassified spend - auto-classification", "Legacy lines with no category, classified from item text and supplier. Accept to apply.", out var ub).AddTo(row2);
            uc.style.flexGrow = 1.3f; uc.style.flexBasis = 0; uc.AddToClassList("gap-right");
            var ubar = UIX.Div("row-center").AddTo(ub);
            unclSummary = UIX.Text("", "field-hint", "grow").AddTo(ubar);
            ubar.Add(UIX.Btn("Accept all ≥ 80% confidence", AcceptHighConfidence, "btn--small", "btn--primary"));
            uncl = new DataTable<Suggestion>(new List<DataTable<Suggestion>.Col>
            {
                new DataTable<Suggestion>.Col { Title = "Date", Width = 70, Text = s => Fmt.ShortDate(s.Line.Date), Sort = s => s.Line.Date, CellClass = "td--muted" },
                new DataTable<Suggestion>.Col { Title = "Raw vendor name", Grow = 1.2f, Text = s => Db.VendorById[s.Line.VendorRecordId].RawName, Sort = s => Db.VendorById[s.Line.VendorRecordId].RawName },
                new DataTable<Suggestion>.Col { Title = "Line text", Grow = 1.6f, Text = s => s.Line.Description, Sort = s => s.Line.Description },
                new DataTable<Suggestion>.Col { Title = "Amount", Width = 76, Right = true, Text = s => Fmt.Money(s.Line.Amount), Sort = s => s.Line.Amount },
                new DataTable<Suggestion>.Col { Title = "Suggested", Grow = 1.1f, Text = s => s.Top != null ? s.Top.Sub.Name : "-", Sort = s => s.Top?.Sub.Name, Status = s => s.Top == null ? "neutral" : s.Top.Confidence >= 0.8f ? "good" : s.Top.Confidence >= 0.55f ? "warning" : "serious" },
                new DataTable<Suggestion>.Col { Title = "Conf.", Width = 56, Right = true, Text = s => s.Top != null ? Fmt.Pct(s.Top.Confidence) : "-", Sort = s => s.Top?.Confidence ?? 0 },
            }, 32);
            uncl.style.height = 360;
            uncl.style.flexGrow = 0;
            uncl.style.marginTop = 8;
            ub.Add(uncl);
            uncl.Selected += s =>
            {
                if (s.Top == null) return;
                App.Prompt("Accept classification?", "\"" + s.Line.Description + "\" (" + Db.VendorById[s.Line.VendorRecordId].RawName + ")\n→ " + s.Top.Sub.Category.Name + " › " + s.Top.Sub.Name + " (" + Fmt.Pct(s.Top.Confidence) + ")\nEvidence: " + string.Join(", ", s.Top.Evidence.Distinct().Take(4)),
                    null, "Accept", _ => { Accept(new[] { s }); }, false);
            };

            var xc = UIX.Card("3-way match exceptions", "PO, goods receipt and invoice disagree - payment on hold", out var xb).AddTo(row2);
            xc.style.flexGrow = 1; xc.style.flexBasis = 0;
            matches = new DataTable<PurchaseLine>(new List<DataTable<PurchaseLine>.Col>
            {
                new DataTable<PurchaseLine>.Col { Title = "PO", Width = 108, Text = l => l.PoNumber, Sort = l => l.PoNumber, CellClass = "td--strong" },
                new DataTable<PurchaseLine>.Col { Title = "Site", Grow = 1f, Text = l => Db.Loc(l.LocationId).Name, Sort = l => Db.Loc(l.LocationId).Name },
                new DataTable<PurchaseLine>.Col { Title = "Issue", Grow = 1f, Text = MatchIssue, Sort = l => MatchIssue(l) },
                new DataTable<PurchaseLine>.Col { Title = "Variance", Width = 80, Right = true, Text = l => Fmt.Money(Variance(l)), Sort = l => Variance(l) },
            }, 32);
            matches.style.height = 396;
            matches.style.flexGrow = 0;
            xb.Add(matches);
            matches.SortBy(3, true);
        }

        static string MatchIssue(PurchaseLine l) =>
            Math.Abs(l.ReceivedQty - l.Qty) > 0.001 ? "Received " + Fmt.Num(l.ReceivedQty) + " of " + Fmt.Num(l.Qty) :
            l.InvoiceAmount > l.Amount ? "Invoice over PO by " + Fmt.Pct(l.InvoiceAmount / l.Amount - 1) : "Invoice under PO by " + Fmt.Pct(1 - l.InvoiceAmount / l.Amount);

        static double Variance(PurchaseLine l) => Math.Abs(l.InvoiceAmount - l.Amount) + Math.Abs(l.Qty - l.ReceivedQty) * l.UnitPrice;

        public override void OnShow(object arg) => Refresh();

        void Refresh()
        {
            var clusters = Svc.VendorClusters;
            int pending = clusters.Count(k => !Svc.ApprovedMerges.Contains(k.Key) && !Svc.RejectedMerges.Contains(k.Key));
            float purity = clusters.Count > 0 ? clusters.Average(k => k.Purity) : 1;
            kRecords.Set(Db.VendorRecords.Count + " → " + Db.Suppliers.Count, Fmt.Inv2((double)Db.VendorRecords.Count / Db.Suppliers.Count) + " records per real supplier");
            kClusters.Set(pending.ToString(), clusters.Count + " clusters found · " + Fmt.Pct(purity, 1) + " precision vs master");

            var recent = Db.Lines.Where(l => l.Date >= Db.T12Start).ToList();
            var unclLines = recent.Where(l => l.SourceCategoryCode == "UNCL" && !Svc.AcceptedClassifications.Contains(l.Id)).ToList();
            kUncl.Set(Fmt.Money(unclLines.Sum(l => l.Amount)), unclLines.Count + " lines · " + Svc.AcceptedClassifications.Count + " fixed so far");
            var from120 = Db.Today.AddDays(-120);
            var exc = Db.Lines.Where(l => l.Date >= from120 && l.MatchException).ToList();
            kMatch.Set(exc.Count.ToString(), Fmt.Money(exc.Sum(Variance)) + " at risk");
            double legacy = recent.Where(l => l.Source != SourceSystem.IfsErp).Sum(l => l.Amount);
            double total = recent.Sum(l => l.Amount);
            kLegacy.Set(Fmt.Pct(total > 0 ? legacy / total : 0), Fmt.Money(legacy) + " outside IFS in 12 months");

            RenderClusters();

            var f = new SpendFilter { Months = 24 };
            var months = Svc.Analytics.Monthly(Svc.Analytics.Lines(f), f);
            double Src(MonthBucket m, SourceSystem s) => m.BySource.TryGetValue(s, out var v) ? v : 0;
            migration.SetData(months.Select(m => Fmt.MonthYear(m.Month)).ToArray(), new List<(string, Color, double[])>
            {
                ("IFS ERP", Palette.Series1, months.Select(m => Src(m, SourceSystem.IfsErp)).ToArray()),
                ("Legacy AP", Palette.Series2, months.Select(m => Src(m, SourceSystem.LegacyAp)).ToArray()),
                ("Site spreadsheet", Palette.Series3, months.Select(m => Src(m, SourceSystem.SiteSpreadsheet)).ToArray()),
                ("P-card", Palette.Series4, months.Select(m => Src(m, SourceSystem.PCard)).ToArray()),
            }, i =>
            {
                var m = months[i];
                double t = Math.Max(1, m.Total);
                return Fmt.MonthYear(m.Month) + "\nIFS ERP " + Fmt.Pct(Src(m, SourceSystem.IfsErp) / t) + "\nLegacy AP " + Fmt.Pct(Src(m, SourceSystem.LegacyAp) / t) +
                       "\nSpreadsheets " + Fmt.Pct(Src(m, SourceSystem.SiteSpreadsheet) / t) + "\nP-card " + Fmt.Pct(Src(m, SourceSystem.PCard) / t);
            }, 3);

            var sug = new List<Suggestion>();
            foreach (var l in unclLines)
            {
                var sup = Db.SupplierOf(l.SupplierId);
                var top = Svc.Engine.Classifier.Classify(Db.VendorById[l.VendorRecordId].RawName + " " + l.Description, sup, 1).FirstOrDefault();
                sug.Add(new Suggestion { Line = l, Top = top });
            }
            suggestions = sug;
            int correct = sug.Count(s => s.Top != null && s.Top.Sub.Code == s.Line.SubcategoryCode);
            unclSummary.text = sug.Count + " lines awaiting a category. Suggestions match the true category on " + Fmt.Pct(sug.Count > 0 ? (double)correct / sug.Count : 1) + " of lines (measured against the synthetic ground truth). Click a row to review.";
            uncl.SetItems(sug);
            uncl.SortBy(3, true);
            matches.SetItems(exc);
        }

        void RenderClusters()
        {
            var scroll = (ScrollView)clusterList.userData;
            scroll.Clear();
            var clusters = Svc.VendorClusters.Where(k =>
            {
                bool merged = Svc.ApprovedMerges.Contains(k.Key);
                bool rejected = Svc.RejectedMerges.Contains(k.Key);
                return clusterFilter == 2 || (clusterFilter == 0 ? !merged && !rejected : merged);
            }).ToList();
            clusterSummary.text = clusters.Count + " clusters. Merging maps every record in a cluster to one golden supplier so spend, contracts and performance roll up correctly.";
            foreach (var k in clusters.Take(40))
            {
                var sup = Db.SupplierOf(k.TrueSupplierId);
                bool merged = Svc.ApprovedMerges.Contains(k.Key);
                var card = UIX.Div("option-card").AddTo(scroll);
                var head = UIX.Div("row-center").AddTo(card);
                var ht = UIX.Div("grow").AddTo(head);
                ht.Add(UIX.Text(sup.Name, "option-title"));
                ht.Add(UIX.Text(k.Members.Count + " records · " + k.SpellingCount + " spellings · " + k.EntityCount + " entities · lowest match " + Fmt.Pct(k.MinSimilarity), "option-sub"));
                if (merged) head.Add(UIX.Pill("Merged", "good"));
                foreach (var m in k.Members.GroupBy(x => x.Record.RawName).Take(6))
                {
                    var row = UIX.Div("kv").AddTo(card);
                    row.Add(UIX.Text(m.Key, "kv-key"));
                    row.Add(UIX.Text(string.Join(", ", m.Select(x => Db.CompanyByCode[x.Record.CompanyCode].ShortName).Distinct()) + " · " + Fmt.Pct(m.First().Similarity), "kv-value"));
                }
                if (k.SpellingCount > 6) card.Add(UIX.Text("+" + (k.SpellingCount - 6) + " more spellings", "muted", "small"));
                if (!merged && !Svc.RejectedMerges.Contains(k.Key))
                {
                    var btns = UIX.Div("btn-row", "mt-8").AddTo(card);
                    var kk = k;
                    btns.Add(UIX.Btn("Approve merge", () =>
                    {
                        Svc.ApprovedMerges.Add(kk.Key);
                        Resolve("DUP|" + kk.TrueSupplierId);
                        App.Toast(kk.Members.Count + " vendor records merged into " + sup.Name + " (approved by " + Svc.ActingAs.Name + ").", Severity.Info);
                        Refresh();
                    }, "btn--small", "btn--primary"));
                    btns.Add(UIX.Btn("Not the same supplier", () =>
                    {
                        Svc.RejectedMerges.Add(kk.Key);
                        Svc.NotifyChanged();
                        RenderClusters();
                    }, "btn--small", "btn--ghost"));
                }
            }
            if (clusters.Count == 0) scroll.Add(UIX.EmptyState(clusterFilter == 0 ? "Nothing left to review" : "No clusters here yet", null));
        }

        void Resolve(string alertId)
        {
            var a = Db.Alerts.FirstOrDefault(x => x.Id == alertId);
            if (a != null) Svc.SetAlertStatus(a, AlertStatus.Resolved);
            App.DataChanged(false);
        }

        void AcceptHighConfidence()
        {
            var list = suggestions.Where(s => s.Top != null && s.Top.Confidence >= 0.8f).ToList();
            if (list.Count == 0) { App.Toast("No suggestions at or above 80% confidence.", Severity.Warning); return; }
            App.Prompt("Accept " + list.Count + " classifications?", "Applies the suggested category to " + list.Count + " lines worth " + Fmt.Money(list.Sum(s => s.Line.Amount)) + ". Lower-confidence lines stay for manual review.", null, "Accept all", _ => Accept(list), false);
        }

        void Accept(IEnumerable<Suggestion> list)
        {
            int n = 0;
            foreach (var s in list)
            {
                Svc.AcceptedClassifications.Add(s.Line.Id);
                s.Line.SourceCategoryCode = s.Top.Sub.CategoryCode;
                n++;
            }
            App.DataChanged(true);
            App.Toast(n + " line" + (n == 1 ? "" : "s") + " classified (approved by " + Svc.ActingAs.Name + ").", Severity.Info);
            Refresh();
        }
    }
}
