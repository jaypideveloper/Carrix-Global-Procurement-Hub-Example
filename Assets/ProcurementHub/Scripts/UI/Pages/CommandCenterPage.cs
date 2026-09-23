using System;
using System.Collections.Generic;
using System.Linq;
using ProcurementHub.Globe;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    /// <summary>UI element that displays the globe render texture and turns pointer input into orbit, zoom and picking.</summary>
    public sealed class GlobeViewport : VisualElement
    {
        readonly GlobeController globe;
        RenderTexture rt;
        Vector2 downPos;
        bool pointerDown, dragging;
        public event Action<string> Clicked;
        public event Action<string, Vector2> Hovered;

        public GlobeViewport(GlobeController globe)
        {
            this.globe = globe;
            AddToClassList("globe-viewport");
            pickingMode = PickingMode.Position;
            RegisterCallback<GeometryChangedEvent>(_ => Resize());
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerLeaveEvent>(_ => { if (!pointerDown) Hovered?.Invoke(null, default); });
        }

        float Scale
        {
            get
            {
                if (panel == null) return 1f;
                float w = panel.visualTree.layout.width;
                return w > 1 ? Screen.width / w : 1f;
            }
        }

        public void Resize()
        {
            if (globe == null || layout.width < 4 || layout.height < 4 || float.IsNaN(layout.width)) return;
            float s = Scale;
            int w = Mathf.Clamp(Mathf.RoundToInt(layout.width * s), 16, 4096);
            int h = Mathf.Clamp(Mathf.RoundToInt(layout.height * s), 16, 4096);
            if (rt != null && rt.width == w && rt.height == h) return;
            var old = rt;
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "GlobeRT" };
            rt.Create();
            globe.SetTarget(rt);
            style.backgroundImage = Background.FromRenderTexture(rt);
            if (old != null) { old.Release(); UnityEngine.Object.Destroy(old); }
        }

        public void Activate(bool on)
        {
            if (on) { Resize(); if (rt != null) globe.SetTarget(rt); }
            globe?.SetRendering(on);
        }

        Vector2 ToPixel(Vector2 local) => new Vector2(local.x * Scale, (layout.height - local.y) * Scale);
        public Vector2 PixelToLocal(Vector2 px) => new Vector2(px.x / Scale, layout.height - px.y / Scale);

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            pointerDown = true;
            dragging = false;
            downPos = e.localPosition;
            this.CapturePointer(e.pointerId);
        }

        void OnMove(PointerMoveEvent e)
        {
            if (globe == null) return;
            if (pointerDown)
            {
                if (!dragging && ((Vector2)e.localPosition - downPos).magnitude > 4) dragging = true;
                if (dragging) { globe.Orbit(e.deltaPosition); Hovered?.Invoke(null, default); }
            }
            else
            {
                var id = globe.Pick(ToPixel(e.localPosition), 14 * Scale);
                Hovered?.Invoke(id, e.localPosition);
            }
        }

        void OnUp(PointerUpEvent e)
        {
            if (!pointerDown) return;
            pointerDown = false;
            if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            if (!dragging && globe != null) Clicked?.Invoke(globe.Pick(ToPixel(e.localPosition), 18 * Scale));
            dragging = false;
        }

        void OnWheel(WheelEvent e)
        {
            globe?.Zoom(e.delta.y * 35f);
            e.StopPropagation();
        }
    }

    public sealed class CommandCenterPage : HubPage
    {
        public override string Id => "command";
        public override string Title => "Command Center";
        public override string Subtitle => "Live view of the Carrix operating network: spend, contract coverage and open issues by site";

        GlobeViewport viewport;
        VisualElement siteCard, alertList, tip;
        Label tipTitle, tipSub, alertCount;
        KpiTile kSpend, kContract, kPremium, kRequests, kAlerts;
        BusinessUnit? unit;
        string selectedId, selectedAlertId;
        int severityFilter;
        Dictionary<string, double> spendCache = new Dictionary<string, double>();

        public CommandCenterPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            Root.AddToClassList("row");

            // Left: filters + site detail
            var left = UIX.Div("cc-left").AddTo(Root);
            var leftScroll = UIX.Scroll("side-scroll", "grow").AddTo(left);
            var filterCard = UIX.Card("Business units", "Filter the globe, KPIs and alert feed", out var fb).AddTo(leftScroll);
            var units = new List<string> { "All", "SSA Marine", "RMS", "Tideworks", "Corporate" };
            var dots = new List<Color?> { null, Palette.Unit(BusinessUnit.SSAMarine), Palette.Unit(BusinessUnit.RMS), Palette.Unit(BusinessUnit.Tideworks), Palette.Unit(BusinessUnit.Corporate) };
            var chips = new ChipGroup(units, dots, 0).AddTo(fb);
            chips.Changed += i =>
            {
                unit = i == 0 ? (BusinessUnit?)null : i == 1 ? BusinessUnit.SSAMarine : i == 2 ? BusinessUnit.RMS : i == 3 ? BusinessUnit.Tideworks : BusinessUnit.Corporate;
                App.Globe?.SetUnitFilter(unit);
                if (selectedId != null && unit.HasValue && Db.Loc(selectedId).Unit != unit.Value) Select(null);
                RefreshAll();
            };
            siteCard = UIX.Div("mt-16").AddTo(leftScroll);

            // Center: KPI strip above the globe stage
            var centerCol = UIX.Div("cc-center").AddTo(Root);
            var top = UIX.Div("cc-kpis").AddTo(centerCol);
            kSpend = new KpiTile("Spend, last 12 months", true).AddTo(top);
            kContract = new KpiTile("Spend on contract").AddTo(top);
            kPremium = new KpiTile("Premium paid off-contract").AddTo(top);
            kRequests = new KpiTile("Open requests").AddTo(top);
            kAlerts = new KpiTile("Critical alerts", false, false, true).AddTo(top);

            var center = UIX.Div("cc-stage").AddTo(centerCol);
            viewport = new GlobeViewport(App.Globe).AddTo(center);
            viewport.Clicked += id => { if (id != null) Select(id); };
            viewport.Hovered += OnHover;

            var tools = UIX.Div("globe-tools").AddTo(center);
            tools.Add(UIX.Btn("Reset view", () => { Select(null); App.Globe?.ResetView(); }, "btn--small"));

            var legend = UIX.Div("globe-legend").AddTo(center);
            legend.pickingMode = PickingMode.Ignore;
            foreach (var (label, color) in new[] { ("SSA Marine", Palette.Unit(BusinessUnit.SSAMarine)), ("Rail Management Services", Palette.Unit(BusinessUnit.RMS)), ("Tideworks", Palette.Unit(BusinessUnit.Tideworks)), ("Corporate", Palette.Unit(BusinessUnit.Corporate)) })
            {
                var item = UIX.Div("legend-item").AddTo(legend);
                UIX.ColorDot(color, "legend-swatch").AddTo(item);
                item.Add(UIX.Text(label, "legend-label"));
            }
            legend.Add(UIX.Text("Column height = 12-month spend (log scale)", "muted", "small"));
            var ringRow = UIX.Div("legend-item", "mt-8").AddTo(legend);
            UIX.ColorDot(Palette.Critical, "legend-swatch").AddTo(ringRow);
            ringRow.Add(UIX.Text("Pulsing ring = open critical / serious alert", "legend-label"));

            var hint = UIX.Text("Drag to rotate  ·  Scroll to zoom  ·  Click a site", "globe-hint").AddTo(center);
            hint.pickingMode = PickingMode.Ignore;

            tip = UIX.Div("globe-tip").AddTo(center);
            tip.pickingMode = PickingMode.Ignore;
            tipTitle = UIX.Text("", "globe-tip-title").AddTo(tip);
            tipSub = UIX.Text("", "globe-tip-sub").AddTo(tip);
            tip.Show(false);

            // Right: alert feed
            var right = UIX.Div("cc-right").AddTo(Root);
            var rightScroll = UIX.Scroll("side-scroll", "grow").AddTo(right);
            var head = UIX.Div("card-header").AddTo(rightScroll);
            var ht = UIX.Div("card-titles").AddTo(head);
            ht.Add(UIX.Text("Alert feed", "card-title"));
            alertCount = UIX.Text("", "card-subtitle").AddTo(ht);
            var sev = new ChipGroup(new[] { "All", "Critical", "Serious", "Warning" }, new Color?[] { null, Palette.Critical, Palette.Serious, Palette.Warning }, 0).AddTo(rightScroll);
            sev.Changed += i => { severityFilter = i; RenderAlerts(); };
            alertList = UIX.Div("mt-8").AddTo(rightScroll);
        }

        public override void OnShow(object arg)
        {
            spendCache.Clear();
            viewport.schedule.Execute(() => viewport.Activate(true)).StartingIn(0);
            if (arg is string locId && Db.Loc(locId) != null) Select(locId);
            RefreshAll();
        }

        public override void OnHide()
        {
            viewport.Activate(false);
            tip.Show(false);
        }

        void RefreshAll()
        {
            RefreshKpis();
            RenderSite();
            RenderAlerts();
        }

        bool InUnit(Location l) => !unit.HasValue || l.Unit == unit.Value;

        void RefreshKpis()
        {
            var f = new SpendFilter { Unit = unit, Months = 12 };
            var lines = Svc.Analytics.Lines(f);
            var s = Svc.Analytics.Summarize(lines);
            var monthly = Svc.Analytics.Monthly(lines, f);
            kSpend.Set(Fmt.Money(s.Total), s.Lines.ToString("#,##0") + " purchase lines", monthly.Select(m => m.Total).ToList(), Palette.Series1);
            kContract.Set(Fmt.Pct(s.OnContractShare), Fmt.Pct(s.CoveredCompliance) + " where an agreement existed");
            kPremium.Set(Fmt.Money(s.Premium), "paid above contract prices");
            var open = Db.Requests.Where(r => r.IsOpen && InUnit(Db.Loc(r.LocationId))).ToList();
            double avgAge = open.Count > 0 ? open.Average(r => (Svc.Now - r.Created).TotalDays) : 0;
            kRequests.Set(open.Count.ToString(), open.Count(r => r.EngagementRequired) + " need procurement · avg age " + Fmt.Days(avgAge));
            var alerts = AlertsInScope().ToList();
            kAlerts.Set(alerts.Count(a => a.Severity == Severity.Critical).ToString(), alerts.Count + " open alerts · " + Fmt.Money(alerts.Sum(a => a.Impact)) + " at stake");
        }

        IEnumerable<Alert> AlertsInScope() => Svc.OpenAlerts.Where(a =>
        {
            if (a.LocationId != null) return InUnit(Db.Loc(a.LocationId));
            return !unit.HasValue || unit == BusinessUnit.Corporate || a.Type == AlertType.DuplicateSupplier || a.Type == AlertType.ContractExpiring || a.Type == AlertType.ExpiredContractSpend;
        });

        double Spend(string locId)
        {
            if (!spendCache.TryGetValue(locId, out var v)) spendCache[locId] = v = App.SiteSpend(Db.Loc(locId));
            return v;
        }

        void OnHover(string id, Vector2 local)
        {
            App.Globe?.SetHover(id);
            if (id == null) { tip.Show(false); return; }
            var l = Db.Loc(id);
            tipTitle.text = l.Name;
            int alerts = Svc.OpenAlerts.Count(a => a.LocationId == id);
            tipSub.text = l.Company.ShortName + " · " + Labels.Site(l.Type) + "\n" + Fmt.Money(Spend(id)) + " 12-mo spend" + (alerts > 0 ? " · " + alerts + " open alert" + (alerts == 1 ? "" : "s") : "");
            tip.Show(true);
            tip.style.left = local.x + 14;
            tip.style.top = local.y + 10;
        }

        void Select(string id)
        {
            selectedId = id;
            App.Globe?.SetSelected(id);
            if (id == null) App.Globe?.ClearArcs();
            else ShowFlows(id);
            RenderSite();
            RenderAlerts();
        }

        void ShowFlows(string id)
        {
            var from = Db.T12Start;
            var flows = Db.Lines.Where(l => l.LocationId == id && l.Date >= from)
                .GroupBy(l => l.SupplierId)
                .Select(g => new { Supplier = Db.SupplierOf(g.Key), Spend = g.Sum(l => l.Amount), OnShare = g.Where(l => l.OnContract).Sum(l => l.Amount) / Math.Max(1, g.Sum(l => l.Amount)) })
                .OrderByDescending(x => x.Spend).Take(7).ToList();
            double max = flows.Count > 0 ? flows.Max(f => f.Spend) : 1;
            var list = flows.Select(f => (f.Supplier.Lat, f.Supplier.Lon, id, f.OnShare >= 0.5 ? new Color(0.92f, 0.95f, 1f) : Palette.Warning, (float)(f.Spend / max))).ToList();
            App.Globe?.ShowArcs(list);
        }

        // ------------------------------------------------------------------ left panel

        void RenderSite()
        {
            siteCard.Clear();
            if (selectedId == null) { RenderNetworkOverview(); return; }
            var l = Db.Loc(selectedId);
            var from = Db.T12Start;
            var lines = Db.Lines.Where(x => x.LocationId == l.Id && x.Date >= from).ToList();
            var s = Svc.Analytics.Summarize(lines);

            var card = UIX.Card(l.Name, l.Company.Name + "\n" + Labels.Site(l.Type) + " · " + l.City + ", " + l.Country, out var body).AddTo(siteCard);
            var close = UIX.Btn("Clear", () => Select(null), "btn--ghost", "btn--small");
            UIX.CardHeaderRight(card).Add(close);

            var grid = UIX.Div("stat-grid").AddTo(body);
            Stat(grid, "12-mo spend", Fmt.Money(s.Total));
            Stat(grid, "On contract", Fmt.Pct(s.OnContractShare));
            Stat(grid, "Open requests", Db.Requests.Count(r => r.IsOpen && r.LocationId == l.Id).ToString());
            Stat(grid, "Open alerts", Svc.OpenAlerts.Count(a => a.LocationId == l.Id).ToString());

            body.Add(UIX.Text("ERP STATUS", "section-label"));
            body.Add(UIX.Text(l.Company.ErpLiveOn(Db.Today) ? "Live on IFS ERP since " + Fmt.Date(l.Company.ErpGoLive) : "Legacy systems - IFS go-live planned " + Fmt.Date(l.Company.ErpGoLive), "body-text"));

            if (l.Fleet.Count > 0)
            {
                body.Add(UIX.Text("EQUIPMENT FLEET", "section-label"));
                foreach (var kv in l.Fleet.OrderByDescending(k => k.Value)) body.Add(UIX.KV(Labels.Equipment(kv.Key), kv.Value.ToString()));
            }

            body.Add(UIX.Text("TOP SPEND CATEGORIES", "section-label"));
            var bars = new BarList().AddTo(body);
            var subs = Svc.Analytics.BySubcategory(lines).Take(5).ToList();
            bars.SetData(subs, x => x.label, x => x.spend, x => Fmt.Money(x.spend), x => Fmt.Pct(x.onContractShare),
                tooltip: x => x.label + ": " + Fmt.MoneyExact(x.spend) + ", " + Fmt.Pct(x.onContractShare) + " on contract", labelWidth: 108);

            body.Add(UIX.Text("SUPPLIER FLOWS ON GLOBE", "section-label"));
            var legendRow = UIX.Div("legend").AddTo(body);
            var a = UIX.Div("legend-item").AddTo(legendRow); UIX.ColorDot(new Color(0.92f, 0.95f, 1f), "legend-swatch").AddTo(a); a.Add(UIX.Text("Mostly on contract", "legend-label"));
            var b = UIX.Div("legend-item").AddTo(legendRow); UIX.ColorDot(Palette.Warning, "legend-swatch").AddTo(b); b.Add(UIX.Text("Mostly off contract", "legend-label"));
            foreach (var sup in Svc.Analytics.BySupplier(lines).Take(5))
                body.Add(UIX.KV(sup.Supplier.Name, Fmt.Money(sup.Spend) + " · " + Fmt.Pct(sup.OnContractShare)));

            var btns = UIX.Div("btn-row", "mt-16").AddTo(body);
            btns.Add(UIX.Btn("New request here", () => App.Navigate("intake", l.Id), "btn--primary"));
            btns.Add(UIX.Btn("Spend analytics", () => App.Navigate("analytics", new SpendFilter { LocationId = l.Id })));
        }

        static void Stat(VisualElement grid, string label, string value)
        {
            var s = UIX.Div("stat").AddTo(grid);
            var inner = UIX.Div("stat-inner").AddTo(s);
            inner.Add(UIX.Text(label, "stat-label"));
            inner.Add(UIX.Text(value, "stat-value"));
        }

        void RenderNetworkOverview()
        {
            var card = UIX.Card("Network overview", "Click a site on the globe for its procurement profile", out var body).AddTo(siteCard);
            var locs = Db.Locations.Where(InUnit).ToList();
            var grid = UIX.Div("stat-grid").AddTo(body);
            Stat(grid, "Sites", locs.Count.ToString());
            Stat(grid, "Legal entities", locs.Select(l => l.CompanyCode).Distinct().Count().ToString());
            Stat(grid, "Suppliers used", Db.Lines.Where(x => x.Date >= Db.T12Start && InUnit(Db.Loc(x.LocationId))).Select(x => x.SupplierId).Distinct().Count().ToString());
            Stat(grid, "Vendor records", Db.VendorRecords.Count(v => !unit.HasValue || Db.CompanyByCode[v.CompanyCode].Unit == unit.Value).ToString());

            body.Add(UIX.Text("12-MONTH SPEND BY ENTITY", "section-label"));
            var lines = Svc.Analytics.Lines(new SpendFilter { Unit = unit });
            var bars = new BarList().AddTo(body);
            var byCo = Svc.Analytics.ByCompany(lines);
            bars.SetData(byCo, x => x.label, x => x.spend, x => Fmt.Money(x.spend),
                color: x => Palette.Unit(Db.CompanyByCode[x.key].Unit),
                tooltip: x => Db.CompanyByCode[x.key].Name + ": " + Fmt.MoneyExact(x.spend) + " (" + Fmt.Pct(x.onContractShare) + " on contract)",
                onClick: x => App.Navigate("analytics", new SpendFilter { CompanyCode = x.key }), labelWidth: 108);

            body.Add(UIX.Text("IFS ERP ROLLOUT", "section-label"));
            foreach (var c in Db.Companies.Where(c => !unit.HasValue || c.Unit == unit.Value).OrderBy(c => c.ErpGoLive))
            {
                bool live = c.ErpLiveOn(Db.Today);
                var row = UIX.Div("kv").AddTo(body);
                var k = UIX.Div("row-center").AddTo(row);
                k.Add(UIX.Dot(live ? "dot--good" : "dot--neutral"));
                k.Add(UIX.Text(c.ShortName, "kv-key"));
                row.Add(UIX.Text((live ? "Live " : "Planned ") + Fmt.Date(c.ErpGoLive), "kv-value"));
            }
            body.Add(UIX.Text("Entities still on legacy AP and site spreadsheets produce most unclassified and no-PO spend - see Data Quality.", "field-hint"));
        }

        // ------------------------------------------------------------------ alert feed

        void RenderAlerts()
        {
            alertList.Clear();
            var alerts = AlertsInScope();
            if (selectedId != null) alerts = alerts.Where(a => a.LocationId == selectedId);
            if (severityFilter == 1) alerts = alerts.Where(a => a.Severity == Severity.Critical);
            else if (severityFilter == 2) alerts = alerts.Where(a => a.Severity == Severity.Serious);
            else if (severityFilter == 3) alerts = alerts.Where(a => a.Severity == Severity.Warning);
            var list = alerts.ToList();
            alertCount.text = list.Count + " open" + (selectedId != null ? " at " + Db.Loc(selectedId).Name : unit.HasValue ? " in " + Labels.UnitShort(unit.Value) : " across the network");
            foreach (var a in list.Take(60)) alertList.Add(AlertItem(a));
            if (list.Count > 60) alertList.Add(UIX.Text("+" + (list.Count - 60) + " more - refine the filter.", "muted", "small"));
            if (list.Count == 0) alertList.Add(UIX.EmptyState("No open alerts", selectedId != null ? "This site has nothing that needs attention." : "Nothing matches this filter."));
        }

        VisualElement AlertItem(Alert a)
        {
            var item = UIX.Div("alert-item");
            if (a.Status == AlertStatus.Acknowledged) item.AddToClassList("alert-item--resolved");
            bool selected = a.Id == selectedAlertId;
            if (selected) item.AddToClassList("alert-item--selected");
            var head = UIX.Div("alert-head").AddTo(item);
            head.Add(UIX.Dot("dot--" + UIX.SeverityClass(a.Severity)));
            head.Add(UIX.Text(Labels.Severity(a.Severity).ToUpperInvariant(), "alert-sev"));
            head.Add(UIX.Text(Labels.Alert(a.Type) + (a.Status == AlertStatus.Acknowledged ? " · acknowledged" : ""), "alert-type"));
            item.Add(UIX.Text(a.Title, "alert-title"));
            item.Add(UIX.Text(a.Detail, "alert-detail"));
            var foot = UIX.Div("alert-foot").AddTo(item);
            foot.Add(UIX.Text(Fmt.Money(a.Impact), "alert-impact"));
            foot.Add(UIX.Text((a.LocationId != null ? Db.Loc(a.LocationId).City + " · " : "") + Fmt.Ago(a.Raised, Svc.Now), "alert-meta"));

            if (selected)
            {
                item.Add(UIX.Text(a.Recommendation, "alert-rec"));
                var btns = UIX.Div("btn-row", "mt-8").AddTo(item);
                if (a.Status != AlertStatus.Acknowledged)
                    btns.Add(UIX.Btn("Acknowledge", () => SetStatus(a, AlertStatus.Acknowledged), "btn--small"));
                btns.Add(UIX.Btn("Resolve", () => SetStatus(a, AlertStatus.Resolved), "btn--small", "btn--primary"));
                var target = RelatedTarget(a);
                if (target.page != null) btns.Add(UIX.Btn(target.label, () => App.Navigate(target.page, target.arg), "btn--small", "btn--ghost"));
            }
            item.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return;
                selectedAlertId = selected ? null : a.Id;
                if (!selected && a.LocationId != null && a.LocationId != selectedId)
                {
                    App.Globe?.SetSelected(a.LocationId);
                    App.Globe?.FocusOn(Db.Loc(a.LocationId).Lat, Db.Loc(a.LocationId).Lon);
                }
                RenderAlerts();
            });
            return item;
        }

        (string page, object arg, string label) RelatedTarget(Alert a)
        {
            switch (a.Type)
            {
                case AlertType.EmergencyReview when a.RequestId != null: return ("requests", a.RequestId, "Open request");
                case AlertType.DuplicateSupplier: return ("quality", null, "Review merge");
                case AlertType.UnclassifiedSpend: return ("quality", null, "Classify spend");
                case AlertType.MatchException: return ("datamodel", 3, "See exceptions");
                case AlertType.ContractExpiring:
                case AlertType.ExpiredContractSpend: return ("suppliers", "contract:" + a.ContractId, "Open contract");
                case AlertType.PriceVariance: return ("suppliers", "supplier:" + a.SupplierId, "Open supplier");
                case AlertType.OffContractSpend: return ("suppliers", "contract:" + a.ContractId, "Open contract");
                case AlertType.SplitPurchase:
                case AlertType.NoPurchaseOrder: return ("analytics", new SpendFilter { LocationId = a.LocationId }, "Site spend");
                default: return (null, null, null);
            }
        }

        void SetStatus(Alert a, AlertStatus status)
        {
            Svc.SetAlertStatus(a, status);
            App.DataChanged(false);
            App.Toast((status == AlertStatus.Resolved ? "Resolved: " : "Acknowledged: ") + a.Title + " (logged as " + Svc.ActingAs.Name + ")", Severity.Info);
            selectedAlertId = null;
            RefreshAll();
        }
    }
}
