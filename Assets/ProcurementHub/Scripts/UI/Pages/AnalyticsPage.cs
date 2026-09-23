using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class AnalyticsPage : HubPage
    {
        public override string Id => "analytics";
        public override string Title => "Spend Analytics";
        public override string Subtitle => "One normalized spend cube across every entity, site, category and supplier - legacy and ERP data combined";

        readonly SpendFilter filter = new SpendFilter();
        DropdownField unitDrop, companyDrop, categoryDrop;
        Segmented period;
        VisualElement siteChip;
        Label siteChipLabel;
        KpiTile kSpend, kOn, kCompliance, kPremium, kNoPo, kSuppliers;
        StackedColumns monthly;
        BarList byCategory, byCompany, benchBars;
        Heatmap heat;
        DataTable<Analytics.SupplierRow> suppliers;
        Segmented benchSeg;
        Label benchNote;
        List<Company> companyChoices = new List<Company>();
        List<PurchaseLine> current = new List<PurchaseLine>();

        static readonly int[] Periods = { 6, 12, 24 };

        public AnalyticsPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            var bar = UIX.Div("filter-bar").AddTo(Root);
            unitDrop = UIX.Dropdown(new List<string> { "All business units", "SSA Marine", "RMS", "Tideworks", "Corporate" }, 0).Cls("filter-item", "filter-drop").AddTo(bar);
            companyDrop = UIX.Dropdown(new List<string> { "All legal entities" }, 0).Cls("filter-item", "filter-drop").AddTo(bar);
            var cats = new List<string> { "All categories" };
            cats.AddRange(Db.Categories.Select(c => c.Name));
            categoryDrop = UIX.Dropdown(cats, 0).Cls("filter-item", "filter-drop").AddTo(bar);
            period = new Segmented(new[] { "6 months", "12 months", "24 months" }, 1).AddTo(bar);
            period.style.alignSelf = Align.Center;
            siteChip = UIX.Div("chip", "chip--active").AddTo(bar);
            siteChip.style.marginLeft = 10; siteChip.style.marginBottom = 0;
            siteChipLabel = UIX.Text("", "chip-label").AddTo(siteChip);
            siteChip.Add(UIX.Text("   ×", "chip-label"));
            siteChip.RegisterCallback<ClickEvent>(_ => { filter.LocationId = null; Refresh(); });
            UIX.Div("spacer").AddTo(bar);
            bar.Add(UIX.Btn("Export CSV & SQL", () => App.Navigate("datamodel"), "btn--small"));

            unitDrop.RegisterValueChangedCallback(_ =>
            {
                int i = unitDrop.index;
                filter.Unit = i == 0 ? (BusinessUnit?)null : i == 1 ? BusinessUnit.SSAMarine : i == 2 ? BusinessUnit.RMS : i == 3 ? BusinessUnit.Tideworks : BusinessUnit.Corporate;
                filter.CompanyCode = null;
                RefreshCompanyChoices();
                Refresh();
            });
            companyDrop.RegisterValueChangedCallback(_ =>
            {
                int i = companyDrop.index;
                filter.CompanyCode = i <= 0 ? null : companyChoices[i - 1].Code;
                Refresh();
            });
            categoryDrop.RegisterValueChangedCallback(_ =>
            {
                int i = categoryDrop.index;
                filter.CategoryCode = i <= 0 ? null : Db.Categories[i - 1].Code;
                Refresh();
            });
            period.Changed += i => { filter.Months = Periods[i]; Refresh(); };

            var scroll = UIX.Scroll("page-scroll").AddTo(Root);
            var c = scroll.contentContainer;

            var kpis = UIX.Div("kpi-row").AddTo(c);
            kSpend = new KpiTile("Total spend", true).AddTo(kpis);
            kOn = new KpiTile("On contract").AddTo(kpis);
            kCompliance = new KpiTile("Contract compliance").AddTo(kpis);
            kPremium = new KpiTile("Off-contract premium").AddTo(kpis);
            kNoPo = new KpiTile("Spend without PO").AddTo(kpis);
            kSuppliers = new KpiTile("Suppliers / vendor records", false, false, true).AddTo(kpis);

            var row1 = UIX.Div("row", "gap-bottom").AddTo(c);
            var mCard = UIX.Card("Monthly spend by contract status", "Hover a month for the breakdown", out var mb).AddTo(row1);
            mCard.style.flexGrow = 1.6f; mCard.style.flexBasis = 0; mCard.AddToClassList("gap-right");
            monthly = new StackedColumns(220).AddTo(mb);
            var catCard = UIX.Card("Spend by category", "Click a bar to filter", out var cb).AddTo(row1);
            catCard.style.flexGrow = 1; catCard.style.flexBasis = 0;
            byCategory = new BarList().AddTo(cb);

            var row2 = UIX.Div("row", "gap-bottom").AddTo(c);
            var coCard = UIX.Card("Spend by legal entity", "Colored by business unit · click to filter", out var cob).AddTo(row2);
            coCard.style.flexGrow = 1; coCard.style.flexBasis = 0; coCard.AddToClassList("gap-right");
            byCompany = new BarList().AddTo(cob);
            var hCard = UIX.Card("Entity × category spend", "Where each company's money goes - hover a cell", out var hb).AddTo(row2);
            hCard.style.flexGrow = 1.4f; hCard.style.flexBasis = 0;
            heat = new Heatmap().AddTo(hb);

            var row3 = UIX.Div("row").AddTo(c);
            var sCard = UIX.Card("Top suppliers (normalized)", "Spend rolled up across every vendor record and spelling", out var sb).AddTo(row3);
            sCard.style.flexGrow = 1.4f; sCard.style.flexBasis = 0; sCard.AddToClassList("gap-right", "card--flush");
            sCard.style.paddingTop = 16;
            sCard.Q(className: "card-header").style.paddingLeft = 16;
            suppliers = new DataTable<Analytics.SupplierRow>(new List<DataTable<Analytics.SupplierRow>.Col>
            {
                new DataTable<Analytics.SupplierRow>.Col { Title = "Supplier", Grow = 1.6f, Text = r => r.Supplier.Name, Sort = r => r.Supplier.Name, CellClass = "td--strong" },
                new DataTable<Analytics.SupplierRow>.Col { Title = "Main categories", Grow = 1.4f, Text = r => r.Categories, Sort = r => r.Categories },
                new DataTable<Analytics.SupplierRow>.Col { Title = "Spend", Width = 80, Right = true, Text = r => Fmt.Money(r.Spend), Sort = r => r.Spend },
                new DataTable<Analytics.SupplierRow>.Col { Title = "Entities", Width = 66, Right = true, Text = r => r.Entities.ToString(), Sort = r => r.Entities },
                new DataTable<Analytics.SupplierRow>.Col { Title = "Records", Width = 66, Right = true, Text = r => r.VendorRecords.ToString(), Sort = r => r.VendorRecords },
                new DataTable<Analytics.SupplierRow>.Col { Title = "On contract", Width = 84, Right = true, Text = r => Fmt.Pct(r.OnContractShare), Sort = r => r.OnContractShare },
            }, 32);
            suppliers.style.height = 380;
            suppliers.style.flexGrow = 0;
            sb.Add(suppliers);
            suppliers.Selected += r => App.Navigate("suppliers", "supplier:" + r.Supplier.Id);

            var bCard = UIX.Card("Maintenance cost per equipment unit", "Compare crane, hostler and fleet costs across sites on one basis", out var bb).AddTo(row3);
            bCard.style.flexGrow = 1; bCard.style.flexBasis = 0;
            benchSeg = new Segmented(Analytics.BenchmarkDefs.Select(d => d.title.Replace(" per ", " / ")).ToArray(), 1).AddTo(bb);
            benchSeg.Changed += _ => RefreshBenchmark();
            benchNote = UIX.Text("", "field-hint").AddTo(bb);
            benchNote.style.marginBottom = 8;
            benchBars = new BarList().AddTo(bb);

            RefreshCompanyChoices();
        }

        void RefreshCompanyChoices()
        {
            companyChoices = Db.Companies.Where(co => !filter.Unit.HasValue || co.Unit == filter.Unit.Value).ToList();
            var list = new List<string> { "All legal entities" };
            list.AddRange(companyChoices.Select(co => co.ShortName));
            companyDrop.choices = list;
            int idx = filter.CompanyCode != null ? companyChoices.FindIndex(co => co.Code == filter.CompanyCode) + 1 : 0;
            companyDrop.SetValueWithoutNotify(list[Mathf.Max(0, idx)]);
        }

        public override void OnShow(object arg)
        {
            if (arg is SpendFilter f)
            {
                filter.LocationId = f.LocationId;
                filter.CompanyCode = f.CompanyCode;
                filter.CategoryCode = f.CategoryCode;
                if (f.CompanyCode != null) filter.Unit = Db.CompanyByCode[f.CompanyCode].Unit;
                else if (f.LocationId != null) filter.Unit = null;
                unitDrop.SetValueWithoutNotify(unitDrop.choices[filter.Unit.HasValue ? UnitIndex(filter.Unit.Value) : 0]);
                RefreshCompanyChoices();
                categoryDrop.SetValueWithoutNotify(categoryDrop.choices[filter.CategoryCode != null ? Db.Categories.FindIndex(x => x.Code == filter.CategoryCode) + 1 : 0]);
            }
            Refresh();
        }

        void Refresh()
        {
            siteChip.Show(filter.LocationId != null);
            if (filter.LocationId != null) siteChipLabel.text = "Site: " + Db.Loc(filter.LocationId).Name;
            var an = Svc.Analytics;
            current = an.Lines(filter);
            var s = an.Summarize(current);
            var months = an.Monthly(current, filter);

            kSpend.Set(Fmt.Money(s.Total), s.Lines.ToString("#,##0") + " lines · " + filter.Months + " months", months.Select(m => m.Total).ToList(), Palette.Series1);
            kOn.Set(Fmt.Pct(s.OnContractShare), "of spend referenced an agreement");
            kCompliance.Set(Fmt.Pct(s.CoveredCompliance), "used the agreement when one existed");
            kPremium.Set(Fmt.Money(s.Premium), "paid above contract prices");
            kNoPo.Set(Fmt.Pct(s.NoPoShare), Fmt.Money(s.NoPo) + " invoiced with no PO");
            kSuppliers.Set(s.Suppliers + " / " + s.VendorRecords, "real suppliers behind the vendor records used");

            var labels = months.Select(m => filter.Months > 12 ? Fmt.MonthYear(m.Month) : Fmt.Month(m.Month)).ToArray();
            monthly.SetData(labels, new List<(string, Color, double[])>
            {
                ("On contract", Palette.Series1, months.Select(m => m.OnContract).ToArray()),
                ("Off contract (agreement available)", Palette.Series2, months.Select(m => m.OffContract).ToArray()),
                ("No agreement in place", Palette.Series3, months.Select(m => m.Uncovered).ToArray()),
            }, i =>
            {
                var m = months[i];
                return Fmt.MonthYear(m.Month) + "\nTotal " + Fmt.MoneyExact(m.Total) + "\nOn contract " + Fmt.Money(m.OnContract) + " (" + Fmt.Pct(m.Total > 0 ? m.OnContract / m.Total : 0) + ")" +
                       "\nOff contract " + Fmt.Money(m.OffContract) + "\nNo agreement " + Fmt.Money(m.Uncovered);
            }, filter.Months > 12 ? 3 : 1);

            if (filter.CategoryCode == null)
            {
                var byCat = an.ByCategory(current);
                byCategory.SetData(byCat, x => x.label, x => x.spend, x => Fmt.Money(x.spend), x => Fmt.Pct(x.onContractShare),
                    tooltip: x => x.label + "\n" + Fmt.MoneyExact(x.spend) + " · " + Fmt.Pct(x.onContractShare) + " on contract\n" + Db.CategoryByCode[x.key].Examples,
                    onClick: x => { filter.CategoryCode = x.key; categoryDrop.SetValueWithoutNotify(categoryDrop.choices[Db.Categories.FindIndex(k => k.Code == x.key) + 1]); Refresh(); }, labelWidth: 140);
            }
            else
            {
                var bySub = an.BySubcategory(current);
                byCategory.SetData(bySub, x => x.label, x => x.spend, x => Fmt.Money(x.spend), x => Fmt.Pct(x.onContractShare),
                    tooltip: x => x.label + "\n" + Fmt.MoneyExact(x.spend) + " · " + Fmt.Pct(x.onContractShare) + " on contract", labelWidth: 140);
            }

            var byCo = an.ByCompany(current);
            byCompany.SetData(byCo, x => x.label, x => x.spend, x => Fmt.Money(x.spend), x => Fmt.Pct(x.onContractShare),
                color: x => Palette.Unit(Db.CompanyByCode[x.key].Unit),
                tooltip: x => Db.CompanyByCode[x.key].Name + "\n" + Fmt.MoneyExact(x.spend) + " · " + Fmt.Pct(x.onContractShare) + " on contract",
                onClick: x => { filter.CompanyCode = x.key; filter.Unit = Db.CompanyByCode[x.key].Unit; unitDrop.SetValueWithoutNotify(unitDrop.choices[UnitIndex(filter.Unit.Value)]); RefreshCompanyChoices(); Refresh(); }, labelWidth: 120);

            var rows = Db.Companies.Where(co => current.Any(l => l.CompanyCode == co.Code)).ToList();
            var cols = Db.Categories;
            var matrix = an.Heatmap(current, rows, cols);
            heat.SetData(rows.Select(r => r.ShortName).ToList(), cols.Select(k => k.Code).ToList(), cols.Select(k => k.Name).ToList(), matrix,
                (i, j) => rows[i].ShortName + " · " + cols[j].Name + "\n" + Fmt.MoneyExact(matrix[i, j]));

            suppliers.SetItems(an.BySupplier(current));
            RefreshBenchmark();
        }

        static int UnitIndex(BusinessUnit u) => u == BusinessUnit.SSAMarine ? 1 : u == BusinessUnit.RMS ? 2 : u == BusinessUnit.Tideworks ? 3 : 4;

        void RefreshBenchmark()
        {
            var def = Analytics.BenchmarkDefs[benchSeg.Index];
            var list = Svc.Analytics.Equipment(current, def.subcategory, def.units);
            if (list.Count == 0)
            {
                benchNote.text = "No sites in this selection have enough " + string.Join(", ", def.units.Select(Labels.Equipment)).ToLowerInvariant() + " to benchmark.";
                benchBars.SetData(new List<EquipmentBenchmark>(), x => "", x => 0, x => "");
                return;
            }
            double median = Stats.Median(list.Select(b => b.PerUnit).ToList());
            benchNote.text = Db.Sub(def.subcategory).Name + " spend ÷ " + string.Join(" + ", def.units.Select(Labels.Equipment)).ToLowerInvariant() + ". White line = network median (" + Fmt.Money(median) + " per unit).";
            benchBars.SetData(list.Take(12).ToList(), b => b.Location.Name, b => b.PerUnit, b => Fmt.Money(b.PerUnit), b => b.PerUnit > median * 1.5 ? Fmt.SignedPct(b.PerUnit / median - 1) : "",
                color: b => Palette.Unit(b.Location.Unit),
                tooltip: b => b.Location.Name + "\n" + Fmt.MoneyExact(b.Spend) + " across " + b.Units + " units = " + Fmt.MoneyExact(b.PerUnit) + " per unit\n" + Fmt.SignedPct(b.PerUnit / median - 1) + " vs network median",
                onClick: b => App.Navigate("command", b.Location.Id), marker: median, labelWidth: 150);
        }
    }
}
