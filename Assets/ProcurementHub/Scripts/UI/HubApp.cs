using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcurementHub.Globe;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    /// <summary>Bootstraps the data layer, builds the application shell and routes between pages.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HubApp : MonoBehaviour
    {
        [SerializeField] StyleSheet styleSheet;
        [SerializeField] GlobeController globe;

        public HubServices Svc { get; private set; }
        public HubDatabase Db => Svc.Db;
        public GlobeController Globe => globe;
        public VisualElement Overlay { get; private set; }
        public bool Ready { get; private set; }
        public string CurrentPageId => current?.Id;

        readonly Dictionary<string, HubPage> pages = new Dictionary<string, HubPage>();
        readonly Dictionary<string, VisualElement> navItems = new Dictionary<string, VisualElement>();
        HubPage current;
        VisualElement pageHost, toastStack, root;
        Label titleLabel, subtitleLabel, alertBadge, personaName, personaTitle;
        VisualElement personaAvatar;
        HubStore store;
        float saveTimer = -1;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            var doc = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;
            docRoot.style.flexGrow = 1;
            var panelRoot = docRoot.panel.visualTree;
            if (styleSheet != null && !panelRoot.styleSheets.Contains(styleSheet)) panelRoot.styleSheets.Add(styleSheet);

            root = UIX.Div("app").AddTo(docRoot);
            var loading = UIX.Div("loading").AddTo(docRoot);
            loading.Add(UIX.Text("Global Procurement Operations Hub", "loading-title"));
            var loadingSub = UIX.Text("Generating synthetic network, spend history and vendor master...", "loading-sub").AddTo(loading);
            yield return null;
            yield return null;

            store = new HubStore();
            var today = DateTime.Today;
            var state = store.Load(today);
            var db = SyntheticDataGenerator.Build(today);
            HubStore.ApplyPolicy(state, db.Policy);
            Svc = new HubServices(db, state?.Requests);
            HubStore.ApplyDecisions(state, Svc);
            foreach (var line in db.Lines)
                if (line.SourceCategoryCode == "UNCL" && Svc.AcceptedClassifications.Contains(line.Id))
                    line.SourceCategoryCode = db.SubById[line.SubcategoryCode].CategoryCode;
            Svc.RunAlerts();
            Svc.Changed += () => saveTimer = 0.6f;

            loadingSub.text = "Building globe and views...";
            yield return null;

            BuildShell();
            if (globe != null) globe.Build(Db, SiteSpend, SiteAlert);

            Register(new CommandCenterPage(this));
            Register(new IntakePage(this));
            Register(new RequestsPage(this));
            Register(new AnalyticsPage(this));
            Register(new SuppliersPage(this));
            Register(new DataQualityPage(this));
            Register(new PolicyPage(this));
            Register(new DataModelPage(this));

            Ready = true;
            Navigate("command");
            loading.RemoveFromHierarchy();
            if (state == null) saveTimer = 0.1f;
        }

        void Register(HubPage page) => pages[page.Id] = page;

        void Update()
        {
            if (!Ready) return;
            current?.Tick(Time.unscaledDeltaTime);
            if (saveTimer >= 0)
            {
                saveTimer -= Time.unscaledDeltaTime;
                if (saveTimer < 0) store.Save(Svc);
            }
        }

        void OnApplicationQuit()
        {
            if (Ready) store.Save(Svc);
        }

        // ------------------------------------------------------------------ shell

        static readonly (string id, string label, IconKind icon, string section)[] Nav =
        {
            ("command", "Command Center", IconKind.Globe, "OPERATE"),
            ("intake", "New Request", IconKind.Plus, null),
            ("requests", "Request Queue", IconKind.List, null),
            ("analytics", "Spend Analytics", IconKind.Chart, "ANALYZE"),
            ("suppliers", "Suppliers & Contracts", IconKind.Supplier, null),
            ("quality", "Data Quality", IconKind.Shield, null),
            ("policy", "Policy & Routing", IconKind.Sliders, "GOVERN"),
            ("datamodel", "Data Model & SQL", IconKind.Database, null),
        };

        void BuildShell()
        {
            var sidebar = UIX.Div("sidebar").AddTo(root);
            var brand = UIX.Div("brand").AddTo(sidebar);
            var mark = UIX.Div("brand-mark").AddTo(brand);
            mark.Add(UIX.Text("GP", "brand-mark-label"));
            var bt = UIX.Div().AddTo(brand);
            bt.Add(UIX.Text("Global Procurement", "brand-title"));
            bt.Add(UIX.Text("Operations Hub", "brand-sub"));

            foreach (var n in Nav)
            {
                if (n.section != null) sidebar.Add(UIX.Text(n.section, "nav-section"));
                var item = UIX.Div("nav-item").AddTo(sidebar);
                var icon = new Icon(n.icon, Palette.Text3).Cls("nav-icon");
                item.Add(icon);
                item.Add(UIX.Text(n.label, "nav-label"));
                if (n.id == "command")
                {
                    alertBadge = UIX.Text("0", "nav-badge").AddTo(item);
                    Tooltips.Attach(alertBadge, () => "Open critical alerts");
                }
                string id = n.id;
                item.RegisterCallback<ClickEvent>(_ => Navigate(id));
                item.userData = icon;
                navItems[id] = item;
            }

            UIX.Div("sidebar-spacer").AddTo(sidebar);
            var footer = UIX.Div("sidebar-footer").AddTo(sidebar);
            footer.Add(UIX.Text("ACTING AS", "nav-section"));
            var persona = UIX.Div("persona").AddTo(footer);
            personaAvatar = UIX.Avatar(Svc.ActingAs.Name, true).AddTo(persona);
            var pt = UIX.Div("grow").AddTo(persona);
            personaName = UIX.Text(Svc.ActingAs.Name, "persona-name").AddTo(pt);
            personaTitle = UIX.Text(Svc.ActingAs.Title, "persona-title").AddTo(pt);
            var personas = new[] { "USR-ANL", "MGR-MRO", "MGR-CAP", "DIR-GP", "USR-TAC", "USR-CHI" }.Select(Db.PersonOf).Where(p => p != null).ToList();
            var drop = UIX.Dropdown(personas.Select(p => p.Name + " - " + p.Title).ToList(), Math.Max(0, personas.FindIndex(p => p.Id == Svc.ActingAsId)));
            drop.RegisterValueChangedCallback(e =>
            {
                int idx = drop.index;
                if (idx < 0) return;
                Svc.ActingAsId = personas[idx].Id;
                RefreshPersona();
                Svc.NotifyChanged();
                Toast("Now acting as " + personas[idx].Name + ". Actions are logged under this name.", Severity.Info);
            });
            footer.Add(drop);
            var reset = UIX.Btn("Reset demo data", ConfirmReset, "btn--ghost", "btn--small");
            reset.style.marginTop = 8;
            reset.style.alignSelf = Align.FlexStart;
            footer.Add(reset);
            footer.Add(UIX.Text("Prototype with synthetic data. Not affiliated with or endorsed by Carrix, SSA Marine, RMS or Tideworks.", "disclaimer"));

            var main = UIX.Div("main").AddTo(root);
            var top = UIX.Div("topbar").AddTo(main);
            var titles = UIX.Div("topbar-titles").AddTo(top);
            titleLabel = UIX.Text("", "page-title").AddTo(titles);
            subtitleLabel = UIX.Text("", "page-subtitle").AddTo(titles);
            var meta = UIX.Div("topbar-meta").AddTo(top);
            MetaChip(meta, "As of " + Fmt.Date(Db.Today));
            MetaChip(meta, Db.Locations.Count + " sites · " + Db.Companies.Count + " legal entities");
            MetaChip(meta, ClaudeAssistant.IsConfigured ? "Claude assistant: connected" : "Assistant: rules engine (offline)");

            pageHost = UIX.Div("page-host").AddTo(main);

            Overlay = UIX.Div("overlay").AddTo(root);
            Overlay.pickingMode = PickingMode.Ignore;
            toastStack = UIX.Div("toast-stack").AddTo(Overlay);
            toastStack.pickingMode = PickingMode.Ignore;
            Tooltips.Init(Overlay);
            RefreshBadge();
        }

        static void MetaChip(VisualElement parent, string text)
        {
            var chip = UIX.Div("meta-chip").AddTo(parent);
            chip.Add(UIX.Text(text, "meta-chip-label"));
        }

        void RefreshPersona()
        {
            var p = Svc.ActingAs;
            personaName.text = p.Name;
            personaTitle.text = p.Title;
            personaAvatar.Q<Label>().text = UIX.Initials(p.Name);
        }

        public void RefreshBadge()
        {
            if (alertBadge == null) return;
            int crit = Svc.OpenAlerts.Count(a => a.Severity == Severity.Critical);
            alertBadge.text = crit.ToString();
            alertBadge.Show(crit > 0);
        }

        public void Navigate(string id, object arg = null)
        {
            if (!pages.TryGetValue(id, out var page)) return;
            Tooltips.Hide();
            if (current != null && current != page)
            {
                current.OnHide();
                current.Root.RemoveFromHierarchy();
            }
            foreach (var kv in navItems)
            {
                bool active = kv.Key == id;
                kv.Value.EnableInClassList("nav-item--active", active);
                (kv.Value.userData as Icon)?.Set(Nav.First(n => n.id == kv.Key).icon, active ? Palette.Series1 : Palette.Text3);
            }
            page.EnsureBuilt();
            if (page.Root.parent != pageHost) pageHost.Add(page.Root);
            current = page;
            titleLabel.text = page.Title;
            subtitleLabel.text = page.Subtitle;
            page.OnShow(arg);
            globe?.SetRendering(id == "command");
        }

        // ------------------------------------------------------------------ shared data hooks

        public double SiteSpend(Location l)
        {
            double s = 0;
            var from = Db.T12Start;
            foreach (var line in Db.Lines) if (line.LocationId == l.Id && line.Date >= from) s += line.Amount;
            return s;
        }

        public Severity? SiteAlert(Location l)
        {
            Severity? worst = null;
            foreach (var a in Svc.OpenAlerts)
            {
                if (a.LocationId != l.Id || a.Status == AlertStatus.Resolved) continue;
                if (a.Severity < Severity.Serious) continue;
                if (worst == null || a.Severity > worst.Value) worst = a.Severity;
            }
            return worst;
        }

        /// <summary>Call after anything that changes requests, alerts or policy.</summary>
        public void DataChanged(bool rerunAlerts)
        {
            if (rerunAlerts) Svc.RunAlerts();
            globe?.Refresh(SiteSpend, SiteAlert);
            RefreshBadge();
            Svc.NotifyChanged();
        }

        // ------------------------------------------------------------------ toasts & modals

        public void Toast(string text, Severity severity = Severity.Info)
        {
            var t = UIX.Div("toast").AddTo(toastStack);
            var kind = severity == Severity.Critical ? IconKind.Warning : severity == Severity.Warning || severity == Severity.Serious ? IconKind.Warning : IconKind.Check;
            var color = severity == Severity.Critical ? Palette.Critical : severity == Severity.Serious ? Palette.Serious : severity == Severity.Warning ? Palette.Warning : Palette.Good;
            var icon = new Icon(kind, color);
            icon.style.width = 16; icon.style.height = 16; icon.style.marginRight = 10; icon.style.flexShrink = 0;
            t.Add(icon);
            t.Add(UIX.Text(text, "toast-text"));
            t.schedule.Execute(() => t.AddToClassList("toast--leaving")).StartingIn(4200);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(4600);
        }

        public void Prompt(string title, string message, string placeholder, string okLabel, Action<string> onOk, bool requireText = true)
        {
            var backdrop = UIX.Div("modal-backdrop").AddTo(Overlay);
            backdrop.pickingMode = PickingMode.Position;
            var modal = UIX.Div("modal").AddTo(backdrop);
            modal.Add(UIX.Text(title, "modal-title"));
            modal.Add(UIX.Text(message, "modal-text"));
            TextField input = null;
            if (placeholder != null)
            {
                input = UIX.Input(placeholder, true).AddTo(modal);
                input.style.marginBottom = 14;
            }
            var row = UIX.Div("btn-row").AddTo(modal);
            row.style.justifyContent = Justify.FlexEnd;
            row.Add(UIX.Btn("Cancel", () => backdrop.RemoveFromHierarchy(), "btn--ghost"));
            Button ok = null;
            ok = UIX.Btn(okLabel, () =>
            {
                string v = input?.value?.Trim() ?? "";
                if (requireText && input != null && v.Length < 3) { Toast("Please enter a short reason (it goes into the audit trail).", Severity.Warning); return; }
                backdrop.RemoveFromHierarchy();
                onOk(v);
            }, "btn--primary");
            row.Add(ok);
            input?.schedule.Execute(() => input.Focus()).StartingIn(50);
        }

        void ConfirmReset()
        {
            Prompt("Reset demo data?", "This deletes your submitted requests, alert decisions, merges and policy changes, and regenerates the synthetic network. The app will reload.",
                null, "Reset and reload", _ =>
                {
                    store.Delete();
                    StartCoroutine(Reload());
                }, false);
        }

        IEnumerator Reload()
        {
            Ready = false;
            yield return null;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex >= 0 ? gameObject.scene.buildIndex : 0);
        }
    }

    public abstract class HubPage
    {
        protected readonly HubApp App;
        protected HubServices Svc => App.Svc;
        protected HubDatabase Db => App.Svc.Db;
        public readonly VisualElement Root;
        bool built;

        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Subtitle { get; }

        protected HubPage(HubApp app)
        {
            App = app;
            Root = UIX.Div("page");
        }

        public void EnsureBuilt()
        {
            if (built) return;
            built = true;
            Build();
        }

        protected abstract void Build();
        public virtual void OnShow(object arg) { }
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }
    }
}
