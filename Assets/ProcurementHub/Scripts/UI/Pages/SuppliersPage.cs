using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class SuppliersPage : HubPage
    {
        public override string Id => "suppliers";
        public override string Title => "Suppliers & Contracts";
        public override string Subtitle => "Golden supplier records, the vendor-master fragments behind them, and every agreement's coverage and leakage";

        sealed class SupplierRow
        {
            public Supplier S;
            public double Spend;
            public int Entities, Records, Spellings;
            public double OnShare;
            public string Categories;
        }

        sealed class ContractRow
        {
            public Contract K;
            public Supplier S;
            public Subcategory Sub;
            public double Spend, Leakage;
            public int DaysLeft;
            public double Utilization => K.AnnualCommit > 0 ? Spend / K.AnnualCommit : 0;
        }

        VisualElement tabSup, tabCon, supView, conView, supDetail, conDetail;
        DataTable<SupplierRow> supTable;
        DataTable<ContractRow> conTable;
        TextField supSearch;
        List<SupplierRow> supRows;
        List<ContractRow> conRows;

        public SuppliersPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            var tabs = UIX.Div("tabs").AddTo(Root);
            tabSup = Tab(tabs, "Suppliers", () => ShowTab(0));
            tabCon = Tab(tabs, "Contracts", () => ShowTab(1));

            // Suppliers
            supView = UIX.Div("split").AddTo(Root);
            var left = UIX.Div("table-pane").AddTo(supView);
            var sbar = UIX.Div("filter-bar").AddTo(left);
            supSearch = UIX.Input("Search suppliers or categories...").Cls("filter-item", "filter-search", "hub-input--search").AddTo(sbar);
            supSearch.RegisterValueChangedCallback(_ => ApplySupplierSearch());
            UIX.Div("spacer").AddTo(sbar);
            sbar.Add(UIX.Text("Records = vendor-master rows across entities and systems for one real supplier", "count-label"));
            supTable = new DataTable<SupplierRow>(new List<DataTable<SupplierRow>.Col>
            {
                new DataTable<SupplierRow>.Col { Title = "Supplier", Grow = 1.5f, Text = r => r.S.Name, Sort = r => r.S.Name, CellClass = "td--strong" },
                new DataTable<SupplierRow>.Col { Title = "Categories", Grow = 1.5f, Text = r => r.Categories, Sort = r => r.Categories },
                new DataTable<SupplierRow>.Col { Title = "HQ", Width = 110, Text = r => r.S.City, Sort = r => r.S.City, CellClass = "td--muted" },
                new DataTable<SupplierRow>.Col { Title = "12-mo spend", Width = 90, Right = true, Text = r => Fmt.Money(r.Spend), Sort = r => r.Spend },
                new DataTable<SupplierRow>.Col { Title = "Entities", Width = 64, Right = true, Text = r => r.Entities.ToString(), Sort = r => r.Entities },
                new DataTable<SupplierRow>.Col { Title = "Records", Width = 64, Right = true, Text = r => r.Records.ToString(), Sort = r => r.Records },
                new DataTable<SupplierRow>.Col { Title = "On contract", Width = 84, Right = true, Text = r => Fmt.Pct(r.OnShare), Sort = r => r.OnShare },
                new DataTable<SupplierRow>.Col { Title = "Score", Width = 60, Right = true, Text = r => Fmt.Pct(r.S.Score), Sort = r => r.S.Score },
                new DataTable<SupplierRow>.Col { Title = "Risk", Width = 92, Text = r => r.S.Risk, Sort = r => r.S.Risk, Status = r => r.S.Risk == "Low" ? "good" : r.S.Risk == "Moderate" ? "warning" : "serious" },
            });
            supTable.AddTo(left);
            supTable.Selected += RenderSupplier;
            var sPane = UIX.Scroll("side-scroll", "detail-pane").AddTo(supView);
            supDetail = sPane.contentContainer;

            // Contracts
            conView = UIX.Div("split").AddTo(Root);
            var cleft = UIX.Div("table-pane").AddTo(conView);
            conTable = new DataTable<ContractRow>(new List<DataTable<ContractRow>.Col>
            {
                new DataTable<ContractRow>.Col { Title = "Contract", Width = 90, Text = r => r.K.Id, Sort = r => r.K.Id, CellClass = "td--strong" },
                new DataTable<ContractRow>.Col { Title = "Supplier", Grow = 1.4f, Text = r => r.S.Name, Sort = r => r.S.Name },
                new DataTable<ContractRow>.Col { Title = "Subcategory", Grow = 1.1f, Text = r => r.Sub.Name, Sort = r => r.Sub.Name },
                new DataTable<ContractRow>.Col { Title = "Scope", Grow = 1f, Text = r => Db.ScopeLabel(r.K), Sort = r => (int)r.K.Scope, CellClass = "td--muted" },
                new DataTable<ContractRow>.Col { Title = "Ends", Width = 110, Text = r => Fmt.Date(r.K.End), Sort = r => r.K.End, Status = r => r.DaysLeft < 0 ? "critical" : r.DaysLeft <= 45 ? "serious" : r.DaysLeft <= 120 ? "warning" : "good" },
                new DataTable<ContractRow>.Col { Title = "Spend 12 mo", Width = 90, Right = true, Text = r => Fmt.Money(r.Spend), Sort = r => r.Spend },
                new DataTable<ContractRow>.Col { Title = "Utilization", Width = 84, Right = true, Text = r => Fmt.Pct(r.Utilization), Sort = r => r.Utilization },
                new DataTable<ContractRow>.Col { Title = "Leakage", Width = 84, Right = true, Text = r => Fmt.Money(r.Leakage), Sort = r => r.Leakage },
            });
            conTable.AddTo(cleft);
            conTable.Selected += RenderContract;
            var cPane = UIX.Scroll("side-scroll", "detail-pane").AddTo(conView);
            conDetail = cPane.contentContainer;

            BuildRows();
            supTable.SortBy(3, true);
            conTable.SortBy(4, false);
            ShowTab(0);
        }

        static VisualElement Tab(VisualElement parent, string label, Action onClick)
        {
            var t = UIX.Div("tab").AddTo(parent);
            t.Add(UIX.Text(label, "tab-label"));
            t.RegisterCallback<ClickEvent>(_ => onClick());
            return t;
        }

        void ShowTab(int i)
        {
            tabSup.EnableInClassList("tab--active", i == 0);
            tabCon.EnableInClassList("tab--active", i == 1);
            supView.Show(i == 0);
            conView.Show(i == 1);
        }

        void BuildRows()
        {
            var from = Db.T12Start;
            var recent = Db.Lines.Where(l => l.Date >= from).ToList();
            var bySup = recent.GroupBy(l => l.SupplierId).ToDictionary(g => g.Key, g => g.ToList());
            var recs = Db.VendorRecords.GroupBy(v => v.SupplierId).ToDictionary(g => g.Key, g => g.ToList());
            supRows = Db.Suppliers.Select(s =>
            {
                bySup.TryGetValue(s.Id, out var lines);
                lines = lines ?? new List<PurchaseLine>();
                recs.TryGetValue(s.Id, out var vr);
                vr = vr ?? new List<VendorRecord>();
                double spend = lines.Sum(l => l.Amount);
                return new SupplierRow
                {
                    S = s,
                    Spend = spend,
                    Entities = vr.Select(v => v.CompanyCode).Distinct().Count(),
                    Records = vr.Count,
                    Spellings = vr.Select(v => v.RawName).Distinct().Count(),
                    OnShare = spend > 0 ? lines.Where(l => l.OnContract).Sum(l => l.Amount) / spend : 0,
                    Categories = string.Join(", ", s.SubcategoryCodes.Select(c => Db.Sub(c).Name)),
                };
            }).ToList();
            supTable.SetItems(supRows);

            conRows = Db.Contracts.Select(k => new ContractRow
            {
                K = k,
                S = Db.SupplierOf(k.SupplierId),
                Sub = Db.Sub(k.SubcategoryCode),
                Spend = recent.Where(l => l.ContractId == k.Id).Sum(l => l.Amount),
                Leakage = recent.Where(l => l.CoveringContractId == k.Id && !l.OnContract).Sum(l => l.Amount),
                DaysLeft = (int)(k.End - Db.Today).TotalDays,
            }).ToList();
            conTable.SetItems(conRows);
        }

        void ApplySupplierSearch()
        {
            string s = supSearch.value?.Trim().ToLowerInvariant();
            supTable.SetItems(string.IsNullOrEmpty(s) ? supRows : supRows.Where(r => (r.S.Name + " " + r.Categories + " " + r.S.City).ToLowerInvariant().Contains(s)));
        }

        public override void OnShow(object arg)
        {
            if (arg is string key)
            {
                if (key.StartsWith("supplier:"))
                {
                    ShowTab(0);
                    var row = supRows.FirstOrDefault(r => r.S.Id == key.Substring(9));
                    if (row != null) { supSearch.SetValueWithoutNotify(""); ApplySupplierSearch(); supTable.Select(row, false); RenderSupplier(row); }
                }
                else if (key.StartsWith("contract:"))
                {
                    ShowTab(1);
                    var row = conRows.FirstOrDefault(r => r.K.Id == key.Substring(9));
                    if (row != null) { conTable.Select(row, false); RenderContract(row); }
                }
            }
            if (supDetail.childCount == 0) supDetail.Add(UIX.EmptyState("Select a supplier", "See its scorecard, every vendor record that points to it, contracts and spend by entity."));
            if (conDetail.childCount == 0) conDetail.Add(UIX.EmptyState("Select a contract", "See coverage, price list, utilization and who is buying around it."));
        }

        void RenderSupplier(SupplierRow r)
        {
            supDetail.Clear();
            var s = r.S;
            supDetail.Add(UIX.Text(s.Name, "detail-title"));
            supDetail.Add(UIX.Text(s.City + ", " + s.Country + " · supplier since " + s.Since.Year + " · " + s.Risk.ToLowerInvariant() + " risk", "muted", "small", "mt-8"));

            var score = UIX.Card("Scorecard", "Overall " + Fmt.Pct(s.Score), out var sb).AddTo(supDetail);
            score.AddToClassList("mt-12");
            foreach (var (label, v) in new[] { ("On-time in-full", s.Otif), ("Quality", s.Quality), ("Responsiveness", s.Responsiveness) })
            {
                var row = UIX.Div("conf-row").AddTo(sb);
                var head = UIX.Div("conf-head").AddTo(row);
                head.Add(UIX.Text(label, "conf-name"));
                head.Add(UIX.Text(Fmt.Pct(v), "conf-pct"));
                var bar = UIX.Div("progress").AddTo(row);
                var fill = UIX.Div("progress-fill").AddTo(bar);
                fill.style.width = Length.Percent(v * 100);
                fill.style.backgroundColor = v >= 0.92f ? Palette.Good : v >= 0.85f ? Palette.Series1 : Palette.Serious;
            }

            var recs = Db.VendorRecords.Where(v => v.SupplierId == s.Id).OrderBy(v => v.CompanyCode).ThenBy(v => v.Source).ToList();
            var rc = UIX.Card("Vendor-master fragments", recs.Count + " records · " + r.Spellings + " spellings · " + r.Entities + " entities - all resolve to this golden record", out var rb).AddTo(supDetail);
            rc.AddToClassList("mt-12");
            foreach (var v in recs)
            {
                var item = UIX.Div("log-item").AddTo(rb);
                var h = UIX.Div("log-head").AddTo(item);
                h.Add(UIX.Text(v.RawName, "log-actor"));
                h.Add(UIX.Text(v.Id, "log-time"));
                item.Add(UIX.Text(Db.CompanyByCode[v.CompanyCode].ShortName + " · " + Labels.Source(v.Source) + " · created " + Fmt.Date(v.Created), "log-note"));
            }

            var ks = Db.Contracts.Where(k => k.SupplierId == s.Id).ToList();
            var kc = UIX.Card("Agreements", ks.Count == 0 ? "No agreements - all spend is uncontracted" : null, out var kb).AddTo(supDetail);
            kc.AddToClassList("mt-12");
            foreach (var k in ks)
            {
                var card = UIX.Div("option-card").AddTo(kb);
                card.Add(UIX.Text(k.Id + " · " + Db.Sub(k.SubcategoryCode).Name, "option-title"));
                card.Add(UIX.Text(Db.ScopeLabel(k) + " · " + Fmt.Date(k.Start) + " to " + Fmt.Date(k.End) + " · " + Fmt.Pct(k.Discount) + " below list", "option-sub"));
                var kk = k;
                card.RegisterCallback<ClickEvent>(_ => App.Navigate("suppliers", "contract:" + kk.Id));
            }

            var lines = Db.Lines.Where(l => l.SupplierId == s.Id && l.Date >= Db.T12Start).ToList();
            var ec = UIX.Card("12-month spend by entity", Fmt.Money(r.Spend) + " total · " + Fmt.Pct(r.OnShare) + " on contract", out var eb).AddTo(supDetail);
            ec.AddToClassList("mt-12");
            var bars = new BarList().AddTo(eb);
            bars.SetData(Svc.Analytics.ByCompany(lines), x => x.label, x => x.spend, x => Fmt.Money(x.spend), x => Fmt.Pct(x.onContractShare),
                color: x => Palette.Unit(Db.CompanyByCode[x.key].Unit), labelWidth: 110);
            var ic = UIX.Card("Most purchased items", null, out var ib).AddTo(supDetail);
            ic.AddToClassList("mt-12");
            foreach (var g in lines.GroupBy(l => l.Item).OrderByDescending(g => g.Sum(l => l.Amount)).Take(5))
                ib.Add(UIX.KV(g.Key, Fmt.Money(g.Sum(l => l.Amount)) + " · avg " + Fmt.Unit(g.Average(l => l.UnitPrice))));
        }

        void RenderContract(ContractRow r)
        {
            conDetail.Clear();
            var k = r.K;
            conDetail.Add(UIX.Text(k.Id, "card-title"));
            conDetail.Add(UIX.Text(k.Title, "detail-title"));
            string status = r.DaysLeft < 0 ? "Expired " + (-r.DaysLeft) + " days ago" : r.DaysLeft <= 120 ? "Expires in " + r.DaysLeft + " days" : "Active";
            conDetail.Add(UIX.Pill(status, r.DaysLeft < 0 ? "critical" : r.DaysLeft <= 45 ? "serious" : r.DaysLeft <= 120 ? "warning" : "good").Cls("mt-8"));

            var info = UIX.Card(null, null, out var ib).AddTo(conDetail);
            info.AddToClassList("mt-12");
            ib.Add(UIX.KV("Supplier", r.S.Name));
            ib.Add(UIX.KV("Scope", Db.ScopeLabel(k) + " (" + Labels.Scope(k.Scope) + ")"));
            ib.Add(UIX.KV("Term", Fmt.Date(k.Start) + " to " + Fmt.Date(k.End)));
            ib.Add(UIX.KV("Terms", k.Terms));
            ib.Add(UIX.KV("Annual commitment", Fmt.MoneyExact(k.AnnualCommit)));
            ib.Add(UIX.KV("Category manager", Db.ManagerFor(r.Sub)?.Name));
            var util = UIX.Div("conf-row", "mt-8").AddTo(ib);
            var uh = UIX.Div("conf-head").AddTo(util);
            uh.Add(UIX.Text("Utilization (12-mo spend vs commitment)", "conf-name"));
            uh.Add(UIX.Text(Fmt.Pct(r.Utilization), "conf-pct"));
            var bar = UIX.Div("progress").AddTo(util);
            var fill = UIX.Div("progress-fill").AddTo(bar);
            fill.style.width = Length.Percent((float)Math.Min(100, r.Utilization * 100));

            var pc = UIX.Card("Price list", Fmt.Pct(k.Discount) + " below catalog list price", out var pb).AddTo(conDetail);
            pc.AddToClassList("mt-12");
            foreach (var p in k.PriceList) pb.Add(UIX.KV(p.Item, Fmt.Unit(p.UnitPrice) + " / " + p.Uom));

            var from = Db.T12Start;
            var leak = Db.Lines.Where(l => l.CoveringContractId == k.Id && !l.OnContract && l.Date >= from).ToList();
            var lc = UIX.Card("Leakage around this agreement", Fmt.Money(r.Leakage) + " bought outside the contract where it applied (12 months)", out var lb).AddTo(conDetail);
            lc.AddToClassList("mt-12");
            lb.Add(UIX.Text("BY SITE", "section-label"));
            var bars = new BarList().AddTo(lb);
            bars.SetData(Svc.Analytics.ByLocation(leak).Take(6).ToList(), x => x.label, x => x.spend, x => Fmt.Money(x.spend),
                color: x => Palette.Series2, onClick: x => App.Navigate("command", x.key), labelWidth: 150);
            lb.Add(UIX.Text("BY SUPPLIER USED INSTEAD", "section-label"));
            foreach (var g in leak.GroupBy(l => l.SupplierId).OrderByDescending(g => g.Sum(l => l.Amount)).Take(5))
                lb.Add(UIX.KV(Db.SupplierOf(g.Key).Name + (g.Key == k.SupplierId ? " (same supplier, no contract reference)" : ""), Fmt.Money(g.Sum(l => l.Amount))));
        }
    }
}
