using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ProcurementHub
{
    public sealed class SqlTableInfo
    {
        public string Name;
        public string Purpose;
        public string[] Columns;
        public Func<HubDatabase, int> Rows;
    }

    public sealed class SqlQueryInfo
    {
        public string Title;
        public string Question;
        public string Sql;
        public Func<HubDatabase, Analytics, (string[] headers, List<string[]> rows)> Preview;
    }

    /// <summary>Relational schema, seed data and the analytic queries behind every dashboard, in PostgreSQL dialect.</summary>
    public static class SqlExporter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly SqlTableInfo[] Tables =
        {
            new SqlTableInfo { Name = "company", Purpose = "Legal entities / company codes", Columns = new[] { "company_code PK", "company_name", "business_unit", "erp_go_live" }, Rows = db => db.Companies.Count },
            new SqlTableInfo { Name = "location", Purpose = "Terminals, ramps, shops and offices", Columns = new[] { "location_id PK", "location_name", "company_code FK", "site_type", "city", "country", "lat", "lon" }, Rows = db => db.Locations.Count },
            new SqlTableInfo { Name = "equipment_fleet", Purpose = "Equipment counts per site", Columns = new[] { "location_id FK", "equipment_class", "units" }, Rows = db => db.Locations.Sum(l => l.Fleet.Count) },
            new SqlTableInfo { Name = "category", Purpose = "Procurement categories", Columns = new[] { "category_code PK", "category_name", "manager_id FK" }, Rows = db => db.Categories.Count },
            new SqlTableInfo { Name = "subcategory", Purpose = "Classification leaf nodes", Columns = new[] { "subcategory_code PK", "category_code FK", "subcategory_name", "is_capex" }, Rows = db => db.Subcategories.Count() },
            new SqlTableInfo { Name = "person", Purpose = "Category managers, approvers, users", Columns = new[] { "person_id PK", "full_name", "title", "email" }, Rows = db => db.People.Count },
            new SqlTableInfo { Name = "supplier", Purpose = "Golden supplier master", Columns = new[] { "supplier_id PK", "supplier_name", "city", "country", "otif", "quality", "responsiveness", "risk" }, Rows = db => db.Suppliers.Count },
            new SqlTableInfo { Name = "vendor_record", Purpose = "Raw vendor rows per entity & system", Columns = new[] { "vendor_record_id PK", "company_code FK", "raw_name", "supplier_id FK", "source_system", "created_at" }, Rows = db => db.VendorRecords.Count },
            new SqlTableInfo { Name = "contract", Purpose = "Agreements and scope", Columns = new[] { "contract_id PK", "supplier_id FK", "subcategory_code FK", "scope", "scope_ref", "start_date", "end_date", "annual_commit" }, Rows = db => db.Contracts.Count },
            new SqlTableInfo { Name = "contract_price", Purpose = "Contract price list", Columns = new[] { "contract_id FK", "item_name", "unit_price", "uom" }, Rows = db => db.Contracts.Sum(k => k.PriceList.Count) },
            new SqlTableInfo { Name = "purchase_line", Purpose = "PO / invoice lines (IFS + legacy)", Columns = new[] { "line_id PK", "line_date", "company_code FK", "location_id FK", "vendor_record_id FK", "supplier_id FK", "subcategory_code FK", "source_category", "item_name", "qty", "unit_price", "amount", "po_number", "contract_id FK", "covering_contract_id FK", "invoice_amount", "received_qty", "is_emergency", "source_system" }, Rows = db => db.Lines.Count },
            new SqlTableInfo { Name = "procurement_request", Purpose = "Intake requests", Columns = new[] { "request_id PK", "created_at", "requester", "location_id FK", "subcategory_code FK", "description", "quantity", "unit_price", "urgency", "stage", "engagement_required", "triggered_rules", "manager_id FK", "contract_id FK" }, Rows = db => db.Requests.Count },
            new SqlTableInfo { Name = "request_event", Purpose = "Request audit trail", Columns = new[] { "request_id FK", "event_at", "stage", "actor", "note" }, Rows = db => db.Requests.Sum(r => r.Events.Count) },
        };

        public const string Schema = @"-- Global Procurement Operations Hub - relational schema (PostgreSQL)
-- Synthetic portfolio dataset. Not affiliated with or endorsed by Carrix, SSA Marine, RMS or Tideworks.

CREATE TABLE company (
  company_code   VARCHAR(8) PRIMARY KEY,
  company_name   VARCHAR(120) NOT NULL,
  business_unit  VARCHAR(40)  NOT NULL,
  erp_go_live    DATE
);

CREATE TABLE location (
  location_id    VARCHAR(12) PRIMARY KEY,
  location_name  VARCHAR(120) NOT NULL,
  company_code   VARCHAR(8)  NOT NULL REFERENCES company,
  site_type      VARCHAR(40) NOT NULL,
  city           VARCHAR(60),
  country        VARCHAR(60),
  lat            NUMERIC(8,4),
  lon            NUMERIC(8,4)
);

CREATE TABLE equipment_fleet (
  location_id     VARCHAR(12) REFERENCES location,
  equipment_class VARCHAR(40),
  units           INTEGER NOT NULL,
  PRIMARY KEY (location_id, equipment_class)
);

CREATE TABLE person (
  person_id  VARCHAR(12) PRIMARY KEY,
  full_name  VARCHAR(80) NOT NULL,
  title      VARCHAR(120),
  email      VARCHAR(120)
);

CREATE TABLE category (
  category_code VARCHAR(8) PRIMARY KEY,
  category_name VARCHAR(80) NOT NULL,
  manager_id    VARCHAR(12) REFERENCES person
);

CREATE TABLE subcategory (
  subcategory_code VARCHAR(12) PRIMARY KEY,
  category_code    VARCHAR(8) NOT NULL REFERENCES category,
  subcategory_name VARCHAR(80) NOT NULL,
  is_capex         BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE supplier (
  supplier_id    VARCHAR(8) PRIMARY KEY,
  supplier_name  VARCHAR(120) NOT NULL,
  city           VARCHAR(60),
  country        VARCHAR(60),
  otif           NUMERIC(4,3),
  quality        NUMERIC(4,3),
  responsiveness NUMERIC(4,3),
  risk           VARCHAR(12)
);

CREATE TABLE vendor_record (
  vendor_record_id VARCHAR(16) PRIMARY KEY,
  company_code     VARCHAR(8) NOT NULL REFERENCES company,
  raw_name         VARCHAR(160) NOT NULL,
  supplier_id      VARCHAR(8) REFERENCES supplier,   -- resolved golden record
  source_system    VARCHAR(20) NOT NULL,
  created_at       DATE
);

CREATE TABLE contract (
  contract_id      VARCHAR(12) PRIMARY KEY,
  supplier_id      VARCHAR(8)  NOT NULL REFERENCES supplier,
  subcategory_code VARCHAR(12) NOT NULL REFERENCES subcategory,
  scope            VARCHAR(16) NOT NULL,              -- National | BusinessUnit | Company | Location
  scope_ref        VARCHAR(16),
  start_date       DATE NOT NULL,
  end_date         DATE NOT NULL,
  annual_commit    NUMERIC(14,2)
);

CREATE TABLE contract_price (
  contract_id VARCHAR(12) REFERENCES contract,
  item_name   VARCHAR(120),
  unit_price  NUMERIC(14,2) NOT NULL,
  uom         VARCHAR(12),
  PRIMARY KEY (contract_id, item_name)
);

CREATE TABLE purchase_line (
  line_id              INTEGER PRIMARY KEY,
  line_date            DATE NOT NULL,
  company_code         VARCHAR(8)  NOT NULL REFERENCES company,
  location_id          VARCHAR(12) NOT NULL REFERENCES location,
  vendor_record_id     VARCHAR(16) NOT NULL REFERENCES vendor_record,
  supplier_id          VARCHAR(8)  NOT NULL REFERENCES supplier,
  subcategory_code     VARCHAR(12) NOT NULL REFERENCES subcategory,
  source_category      VARCHAR(8),                    -- as recorded; 'UNCL' when blank
  item_name            VARCHAR(120),
  qty                  NUMERIC(12,2),
  unit_price           NUMERIC(14,2),
  amount               NUMERIC(14,2),
  po_number            VARCHAR(20),
  contract_id          VARCHAR(12) REFERENCES contract,  -- contract actually referenced
  covering_contract_id VARCHAR(12) REFERENCES contract,  -- contract that was available
  invoice_amount       NUMERIC(14,2),
  received_qty         NUMERIC(12,2),
  is_emergency         BOOLEAN,
  source_system        VARCHAR(20)
);
CREATE INDEX ix_line_date     ON purchase_line (line_date);
CREATE INDEX ix_line_location ON purchase_line (location_id, subcategory_code);
CREATE INDEX ix_line_supplier ON purchase_line (supplier_id);

CREATE TABLE procurement_request (
  request_id          VARCHAR(12) PRIMARY KEY,
  created_at          TIMESTAMP NOT NULL,
  requester           VARCHAR(80),
  location_id         VARCHAR(12) REFERENCES location,
  subcategory_code    VARCHAR(12) REFERENCES subcategory,
  description         VARCHAR(400),
  quantity            NUMERIC(12,2),
  unit_price          NUMERIC(14,2),
  urgency             VARCHAR(12),
  stage               VARCHAR(20),
  engagement_required BOOLEAN,
  triggered_rules     VARCHAR(20),
  manager_id          VARCHAR(12) REFERENCES person,
  contract_id         VARCHAR(12) REFERENCES contract
);

CREATE TABLE request_event (
  request_id VARCHAR(12) REFERENCES procurement_request,
  event_at   TIMESTAMP,
  stage      VARCHAR(20),
  actor      VARCHAR(80),
  note       VARCHAR(400)
);
";

        public static readonly SqlQueryInfo[] Queries =
        {
            new SqlQueryInfo
            {
                Title = "Spend by category",
                Question = "Where does the money go, and how much of it is under contract?",
                Sql = @"SELECT c.category_name,
       SUM(pl.amount)                                                          AS spend_12m,
       SUM(pl.amount) FILTER (WHERE pl.contract_id IS NOT NULL) / SUM(pl.amount) AS on_contract_share,
       COUNT(DISTINCT pl.supplier_id)                                          AS suppliers
FROM purchase_line pl
JOIN subcategory s ON s.subcategory_code = pl.subcategory_code
JOIN category    c ON c.category_code    = s.category_code
WHERE pl.line_date >= CURRENT_DATE - INTERVAL '12 months'
GROUP BY c.category_name
ORDER BY spend_12m DESC;",
                Preview = (db, an) =>
                {
                    var lines = db.Lines.Where(l => l.Date >= db.T12Start).ToList();
                    var rows = lines.GroupBy(l => db.SubById[l.SubcategoryCode].Category.Name)
                        .Select(g => new[] { g.Key, Fmt.MoneyExact(g.Sum(l => l.Amount)), Fmt.Pct(g.Where(l => l.OnContract).Sum(l => l.Amount) / g.Sum(l => l.Amount)), g.Select(l => l.SupplierId).Distinct().Count().ToString() })
                        .OrderByDescending(r => double.Parse(r[1].Replace("$", "").Replace(",", ""), Inv)).ToList();
                    return (new[] { "category_name", "spend_12m", "on_contract_share", "suppliers" }, rows);
                },
            },
            new SqlQueryInfo
            {
                Title = "Off-contract leakage by site",
                Question = "Which sites buy outside an agreement that already covers them, and what does it cost?",
                Sql = @"SELECT l.location_name,
       s.subcategory_name,
       k.contract_id,
       SUM(pl.amount)                                         AS off_contract_spend,
       SUM(GREATEST(pl.unit_price - cp.unit_price, 0) * pl.qty) AS premium_paid
FROM purchase_line pl
JOIN location    l  ON l.location_id = pl.location_id
JOIN subcategory s  ON s.subcategory_code = pl.subcategory_code
JOIN contract    k  ON k.contract_id = pl.covering_contract_id
LEFT JOIN contract_price cp ON cp.contract_id = k.contract_id AND cp.item_name = pl.item_name
WHERE pl.contract_id IS NULL
  AND pl.line_date >= CURRENT_DATE - INTERVAL '12 months'
GROUP BY l.location_name, s.subcategory_name, k.contract_id
HAVING SUM(pl.amount) > 20000
ORDER BY premium_paid DESC
LIMIT 15;",
                Preview = (db, an) =>
                {
                    var rows = db.Lines.Where(l => l.Date >= db.T12Start && l.CoveringContractId != null && !l.OnContract)
                        .GroupBy(l => (l.LocationId, l.SubcategoryCode, l.CoveringContractId))
                        .Select(g =>
                        {
                            var k = db.ContractById[g.Key.CoveringContractId];
                            double premium = g.Sum(l => Math.Max(0, l.UnitPrice - (k.PriceFor(l.Item) ?? l.UnitPrice)) * l.Qty);
                            return new { g, k, spend = g.Sum(l => l.Amount), premium };
                        })
                        .Where(x => x.spend > 20000).OrderByDescending(x => x.premium).Take(15)
                        .Select(x => new[] { db.LocationById[x.g.Key.LocationId].Name, db.SubById[x.g.Key.SubcategoryCode].Name, x.k.Id, Fmt.MoneyExact(x.spend), Fmt.MoneyExact(x.premium) }).ToList();
                    return (new[] { "location_name", "subcategory_name", "contract_id", "off_contract_spend", "premium_paid" }, rows);
                },
            },
            new SqlQueryInfo
            {
                Title = "One supplier, many vendor records",
                Question = "Which suppliers are fragmented across entities and spellings?",
                Sql = @"SELECT sp.supplier_name,
       COUNT(*)                          AS vendor_records,
       COUNT(DISTINCT vr.company_code)   AS entities,
       COUNT(DISTINCT UPPER(vr.raw_name)) AS spellings,
       STRING_AGG(DISTINCT vr.raw_name, ' | ') AS example_names
FROM vendor_record vr
JOIN supplier sp ON sp.supplier_id = vr.supplier_id
GROUP BY sp.supplier_name
HAVING COUNT(*) >= 4
ORDER BY vendor_records DESC;",
                Preview = (db, an) =>
                {
                    var rows = db.VendorRecords.GroupBy(v => v.SupplierId).Where(g => g.Count() >= 4).OrderByDescending(g => g.Count())
                        .Select(g => new[] { db.SupplierById[g.Key].Name, g.Count().ToString(), g.Select(v => v.CompanyCode).Distinct().Count().ToString(), g.Select(v => v.RawName.ToUpperInvariant()).Distinct().Count().ToString(), string.Join(" | ", g.Select(v => v.RawName).Distinct().Take(3)) })
                        .ToList();
                    return (new[] { "supplier_name", "vendor_records", "entities", "spellings", "example_names" }, rows);
                },
            },
            new SqlQueryInfo
            {
                Title = "3-way match exceptions",
                Question = "Which invoices disagree with the PO or the goods receipt?",
                Sql = @"SELECT pl.po_number,
       l.location_name,
       sp.supplier_name,
       pl.amount                     AS po_amount,
       pl.invoice_amount,
       pl.qty, pl.received_qty,
       pl.invoice_amount - pl.amount AS variance
FROM purchase_line pl
JOIN location l  ON l.location_id  = pl.location_id
JOIN supplier sp ON sp.supplier_id = pl.supplier_id
WHERE pl.po_number IS NOT NULL
  AND pl.line_date >= CURRENT_DATE - INTERVAL '120 days'
  AND (ABS(pl.invoice_amount - pl.amount) > GREATEST(50, pl.amount * 0.02)
       OR pl.received_qty <> pl.qty)
ORDER BY ABS(pl.invoice_amount - pl.amount) DESC;",
                Preview = (db, an) =>
                {
                    var from = db.Today.AddDays(-120);
                    var rows = db.Lines.Where(l => l.Date >= from && l.MatchException).OrderByDescending(l => Math.Abs(l.InvoiceAmount - l.Amount)).Take(25)
                        .Select(l => new[] { l.PoNumber, db.LocationById[l.LocationId].Name, db.SupplierById[l.SupplierId].Name, Fmt.MoneyExact(l.Amount), Fmt.MoneyExact(l.InvoiceAmount), Fmt.Num(l.Qty) + " / " + Fmt.Num(l.ReceivedQty), Fmt.MoneyExact(l.InvoiceAmount - l.Amount) })
                        .ToList();
                    return (new[] { "po_number", "location_name", "supplier_name", "po_amount", "invoice_amount", "qty / received", "variance" }, rows);
                },
            },
            new SqlQueryInfo
            {
                Title = "Possible split purchases",
                Question = "Where did a site buy repeatedly from one supplier just under the engagement threshold?",
                Sql = @"WITH windowed AS (
  SELECT pl.*,
         SUM(pl.amount) OVER w AS amount_7d,
         COUNT(*)       OVER w AS lines_7d
  FROM purchase_line pl
  WHERE pl.amount < 25000
    AND pl.line_date >= CURRENT_DATE - INTERVAL '12 months'
  WINDOW w AS (PARTITION BY pl.location_id, pl.supplier_id
               ORDER BY pl.line_date
               RANGE BETWEEN INTERVAL '7 days' PRECEDING AND CURRENT ROW)
)
SELECT location_id, supplier_id, line_date, lines_7d, amount_7d
FROM windowed
WHERE lines_7d >= 2 AND amount_7d >= 25000
ORDER BY amount_7d DESC;",
                Preview = (db, an) =>
                {
                    double threshold = db.Policy.ValueThreshold;
                    var rows = new List<string[]>();
                    foreach (var g in db.Lines.Where(l => l.Date >= db.T12Start && l.Amount < threshold).GroupBy(l => (l.LocationId, l.SupplierId)))
                    {
                        var list = g.OrderBy(l => l.Date).ToList();
                        for (int i = 0; i < list.Count; i++)
                        {
                            double sum = 0; int n = 0;
                            for (int j = i; j >= 0 && (list[i].Date - list[j].Date).TotalDays <= 7; j--) { sum += list[j].Amount; n++; }
                            if (n >= 2 && sum >= threshold) rows.Add(new[] { g.Key.LocationId, db.SupplierById[g.Key.SupplierId].Name, Fmt.Iso(list[i].Date), n.ToString(), Fmt.MoneyExact(sum) });
                        }
                    }
                    rows = rows.OrderByDescending(r => double.Parse(r[4].Replace("$", "").Replace(",", ""), Inv)).Take(25).ToList();
                    return (new[] { "location_id", "supplier", "line_date", "lines_7d", "amount_7d" }, rows);
                },
            },
            new SqlQueryInfo
            {
                Title = "Contract utilization & expiry",
                Question = "Which agreements are under-used, over-used or about to lapse?",
                Sql = @"SELECT k.contract_id,
       sp.supplier_name,
       s.subcategory_name,
       k.end_date,
       k.end_date - CURRENT_DATE                                  AS days_left,
       COALESCE(SUM(pl.amount), 0)                                AS spend_12m,
       COALESCE(SUM(pl.amount), 0) / NULLIF(k.annual_commit, 0)   AS utilization
FROM contract k
JOIN supplier    sp ON sp.supplier_id = k.supplier_id
JOIN subcategory s  ON s.subcategory_code = k.subcategory_code
LEFT JOIN purchase_line pl
       ON pl.contract_id = k.contract_id
      AND pl.line_date >= CURRENT_DATE - INTERVAL '12 months'
GROUP BY k.contract_id, sp.supplier_name, s.subcategory_name, k.end_date, k.annual_commit
ORDER BY days_left;",
                Preview = (db, an) =>
                {
                    var rows = db.Contracts.OrderBy(k => k.End).Select(k =>
                    {
                        double spend = db.Lines.Where(l => l.ContractId == k.Id && l.Date >= db.T12Start).Sum(l => l.Amount);
                        return new[] { k.Id, db.SupplierById[k.SupplierId].Name, db.SubById[k.SubcategoryCode].Name, Fmt.Iso(k.End), ((int)(k.End - db.Today).TotalDays).ToString(), Fmt.MoneyExact(spend), Fmt.Pct(k.AnnualCommit > 0 ? spend / k.AnnualCommit : 0) };
                    }).ToList();
                    return (new[] { "contract_id", "supplier_name", "subcategory_name", "end_date", "days_left", "spend_12m", "utilization" }, rows);
                },
            },
            new SqlQueryInfo
            {
                Title = "Maintenance cost per equipment unit",
                Question = "Which terminals spend the most on fleet parts per powered unit?",
                Sql = @"WITH fleet AS (
  SELECT location_id, SUM(units) AS powered_units
  FROM equipment_fleet
  WHERE equipment_class IN ('Hostler','ContainerHandler','Forklift','ServiceTruck','Locomotive')
  GROUP BY location_id
), parts AS (
  SELECT location_id, SUM(amount) AS fleet_mro_12m
  FROM purchase_line
  WHERE subcategory_code = 'MRO-FLT'
    AND line_date >= CURRENT_DATE - INTERVAL '12 months'
  GROUP BY location_id
)
SELECT l.location_name, p.fleet_mro_12m, f.powered_units,
       p.fleet_mro_12m / f.powered_units AS cost_per_unit,
       (p.fleet_mro_12m / f.powered_units)
         / AVG(p.fleet_mro_12m / f.powered_units) OVER () AS index_vs_network
FROM parts p
JOIN fleet f    ON f.location_id = p.location_id
JOIN location l ON l.location_id = p.location_id
WHERE f.powered_units >= 3
ORDER BY cost_per_unit DESC;",
                Preview = (db, an) =>
                {
                    var def = Analytics.BenchmarkDefs[1];
                    var list = an.Equipment(db.Lines.Where(l => l.Date >= db.T12Start).ToList(), def.subcategory, def.units);
                    double avg = list.Count > 0 ? list.Average(b => b.PerUnit) : 1;
                    var rows = list.Select(b => new[] { b.Location.Name, Fmt.MoneyExact(b.Spend), b.Units.ToString(), Fmt.MoneyExact(b.PerUnit), Fmt.Inv2(b.PerUnit / avg) }).ToList();
                    return (new[] { "location_name", "fleet_mro_12m", "powered_units", "cost_per_unit", "index_vs_network" }, rows);
                },
            },
        };

        public static string ExportAll(HubDatabase db, string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "01_schema.sql"), Schema, Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "02_seed.sql"), Seed(db), Encoding.UTF8);
            var q = new StringBuilder("-- Analytic queries behind the hub dashboards (PostgreSQL)\n\n");
            foreach (var query in Queries) q.Append("-- ").Append(query.Title).Append(": ").Append(query.Question).Append('\n').Append(query.Sql).Append("\n\n");
            File.WriteAllText(Path.Combine(folder, "03_analytics_queries.sql"), q.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "purchase_lines.csv"), LinesCsv(db), Encoding.UTF8);
            return folder;
        }

        static string S(string v) => v == null ? "NULL" : "'" + v.Replace("'", "''") + "'";
        static string D(DateTime d) => "'" + d.ToString("yyyy-MM-dd", Inv) + "'";
        static string T(DateTime d) => "'" + d.ToString("yyyy-MM-dd HH:mm:ss", Inv) + "'";
        static string N(double v) => v.ToString("0.##", Inv);
        static string B(bool v) => v ? "TRUE" : "FALSE";

        static void Insert(StringBuilder sb, string table, string columns, IEnumerable<string> values)
        {
            var batch = new List<string>();
            foreach (var v in values)
            {
                batch.Add("(" + v + ")");
                if (batch.Count == 500) { Flush(); }
            }
            Flush();

            void Flush()
            {
                if (batch.Count == 0) return;
                sb.Append("INSERT INTO ").Append(table).Append(" (").Append(columns).Append(") VALUES\n  ")
                  .Append(string.Join(",\n  ", batch)).Append(";\n\n");
                batch.Clear();
            }
        }

        public static string Seed(HubDatabase db)
        {
            var sb = new StringBuilder("-- Seed data generated by the hub (seed " + SyntheticDataGenerator.Seed + ", as of " + Fmt.Iso(db.Today) + ")\n\n");
            Insert(sb, "company", "company_code, company_name, business_unit, erp_go_live",
                db.Companies.Select(c => string.Join(", ", S(c.Code), S(c.Name), S(Labels.UnitShort(c.Unit)), D(c.ErpGoLive))));
            Insert(sb, "location", "location_id, location_name, company_code, site_type, city, country, lat, lon",
                db.Locations.Select(l => string.Join(", ", S(l.Id), S(l.Name), S(l.CompanyCode), S(l.Type.ToString()), S(l.City), S(l.Country), N(l.Lat), N(l.Lon))));
            Insert(sb, "equipment_fleet", "location_id, equipment_class, units",
                db.Locations.SelectMany(l => l.Fleet.Select(f => string.Join(", ", S(l.Id), S(f.Key.ToString()), f.Value.ToString(Inv)))));
            Insert(sb, "person", "person_id, full_name, title, email",
                db.People.Select(p => string.Join(", ", S(p.Id), S(p.Name), S(p.Title), S(p.Email))));
            Insert(sb, "category", "category_code, category_name, manager_id",
                db.Categories.Select(c => string.Join(", ", S(c.Code), S(c.Name), S(c.ManagerId))));
            Insert(sb, "subcategory", "subcategory_code, category_code, subcategory_name, is_capex",
                db.Subcategories.Select(s => string.Join(", ", S(s.Code), S(s.CategoryCode), S(s.Name), B(s.Capex))));
            Insert(sb, "supplier", "supplier_id, supplier_name, city, country, otif, quality, responsiveness, risk",
                db.Suppliers.Select(s => string.Join(", ", S(s.Id), S(s.Name), S(s.City), S(s.Country), N(s.Otif), N(s.Quality), N(s.Responsiveness), S(s.Risk))));
            Insert(sb, "vendor_record", "vendor_record_id, company_code, raw_name, supplier_id, source_system, created_at",
                db.VendorRecords.Select(v => string.Join(", ", S(v.Id), S(v.CompanyCode), S(v.RawName), S(v.SupplierId), S(v.Source.ToString()), D(v.Created))));
            Insert(sb, "contract", "contract_id, supplier_id, subcategory_code, scope, scope_ref, start_date, end_date, annual_commit",
                db.Contracts.Select(k => string.Join(", ", S(k.Id), S(k.SupplierId), S(k.SubcategoryCode), S(k.Scope.ToString()), S(k.ScopeRef), D(k.Start), D(k.End), N(k.AnnualCommit))));
            Insert(sb, "contract_price", "contract_id, item_name, unit_price, uom",
                db.Contracts.SelectMany(k => k.PriceList.Select(p => string.Join(", ", S(k.Id), S(p.Item), N(p.UnitPrice), S(p.Uom)))));
            Insert(sb, "purchase_line", "line_id, line_date, company_code, location_id, vendor_record_id, supplier_id, subcategory_code, source_category, item_name, qty, unit_price, amount, po_number, contract_id, covering_contract_id, invoice_amount, received_qty, is_emergency, source_system",
                db.Lines.Select(l => string.Join(", ", l.Id.ToString(Inv), D(l.Date), S(l.CompanyCode), S(l.LocationId), S(l.VendorRecordId), S(l.SupplierId), S(l.SubcategoryCode), S(l.SourceCategoryCode), S(l.Item), N(l.Qty), N(l.UnitPrice), N(l.Amount), S(l.PoNumber), S(l.ContractId), S(l.CoveringContractId), N(l.InvoiceAmount), N(l.ReceivedQty), B(l.Emergency), S(l.Source.ToString()))));
            Insert(sb, "procurement_request", "request_id, created_at, requester, location_id, subcategory_code, description, quantity, unit_price, urgency, stage, engagement_required, triggered_rules, manager_id, contract_id",
                db.Requests.Select(r => string.Join(", ", S(r.Id), T(r.Created), S(r.Requester), S(r.LocationId), S(r.SubcategoryCode), S(r.Description), N(r.Quantity), N(r.UnitPrice), S(r.Urgency.ToString()), S(r.Stage.ToString()), B(r.EngagementRequired), S(r.TriggeredRules), S(r.AssignedManagerId), S(r.ContractId))));
            Insert(sb, "request_event", "request_id, event_at, stage, actor, note",
                db.Requests.SelectMany(r => r.Events.Select(e => string.Join(", ", S(r.Id), T(e.At), S(e.Stage.ToString()), S(e.Actor), S(e.Note)))));
            return sb.ToString();
        }

        static string LinesCsv(HubDatabase db)
        {
            string Q(string v) => v == null ? "" : v.Contains(",") || v.Contains("\"") ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
            var sb = new StringBuilder("line_id,line_date,company,business_unit,location,supplier,vendor_record,raw_vendor_name,category,subcategory,item,qty,unit_price,amount,po_number,contract_id,covering_contract_id,source_system,emergency\n");
            foreach (var l in db.Lines)
            {
                var sub = db.SubById[l.SubcategoryCode];
                var company = db.CompanyByCode[l.CompanyCode];
                sb.Append(l.Id).Append(',').Append(Fmt.Iso(l.Date)).Append(',').Append(Q(company.ShortName)).Append(',').Append(Q(Labels.UnitShort(company.Unit))).Append(',')
                  .Append(Q(db.LocationById[l.LocationId].Name)).Append(',').Append(Q(db.SupplierById[l.SupplierId].Name)).Append(',').Append(l.VendorRecordId).Append(',')
                  .Append(Q(db.VendorById[l.VendorRecordId].RawName)).Append(',').Append(Q(sub.Category.Name)).Append(',').Append(Q(sub.Name)).Append(',').Append(Q(l.Item)).Append(',')
                  .Append(N(l.Qty)).Append(',').Append(N(l.UnitPrice)).Append(',').Append(N(l.Amount)).Append(',').Append(l.PoNumber ?? "").Append(',').Append(l.ContractId ?? "").Append(',')
                  .Append(l.CoveringContractId ?? "").Append(',').Append(Labels.Source(l.Source)).Append(',').Append(l.Emergency ? "Y" : "N").Append('\n');
            }
            return sb.ToString();
        }
    }
}
