using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHub
{
    /// <summary>In-memory relational store for the hub. Mirrors the SQL schema exported by <see cref="SqlExporter"/>.</summary>
    public sealed class HubDatabase
    {
        public DateTime Today;
        public EngagementPolicy Policy = new EngagementPolicy();

        public List<Company> Companies = new List<Company>();
        public List<Location> Locations = new List<Location>();
        public List<Category> Categories = new List<Category>();
        public List<Person> People = new List<Person>();
        public List<Supplier> Suppliers = new List<Supplier>();
        public List<VendorRecord> VendorRecords = new List<VendorRecord>();
        public List<Contract> Contracts = new List<Contract>();
        public List<PurchaseLine> Lines = new List<PurchaseLine>();
        public List<ProcurementRequest> Requests = new List<ProcurementRequest>();
        public List<Alert> Alerts = new List<Alert>();

        public readonly Dictionary<string, Company> CompanyByCode = new Dictionary<string, Company>();
        public readonly Dictionary<string, Location> LocationById = new Dictionary<string, Location>();
        public readonly Dictionary<string, Category> CategoryByCode = new Dictionary<string, Category>();
        public readonly Dictionary<string, Subcategory> SubById = new Dictionary<string, Subcategory>();
        public readonly Dictionary<string, Person> PersonById = new Dictionary<string, Person>();
        public readonly Dictionary<string, Supplier> SupplierById = new Dictionary<string, Supplier>();
        public readonly Dictionary<string, VendorRecord> VendorById = new Dictionary<string, VendorRecord>();
        public readonly Dictionary<string, Contract> ContractById = new Dictionary<string, Contract>();
        public readonly Dictionary<string, List<PurchaseLine>> LinesByItem = new Dictionary<string, List<PurchaseLine>>();
        public readonly List<CatalogItem> AllItems = new List<CatalogItem>();

        public IEnumerable<Subcategory> Subcategories => Categories.SelectMany(c => c.Subcategories);

        public Company CompanyOf(Location l) => l.Company;
        public Subcategory Sub(string code) => code != null && SubById.TryGetValue(code, out var s) ? s : null;
        public Supplier SupplierOf(string id) => id != null && SupplierById.TryGetValue(id, out var s) ? s : null;
        public Location Loc(string id) => id != null && LocationById.TryGetValue(id, out var l) ? l : null;
        public Person PersonOf(string id) => id != null && PersonById.TryGetValue(id, out var p) ? p : null;
        public Contract ContractOf(string id) => id != null && ContractById.TryGetValue(id, out var c) ? c : null;

        public Person ManagerFor(Subcategory s) => s == null ? null : PersonOf(s.Category.ManagerId);

        public void Reindex()
        {
            CompanyByCode.Clear(); LocationById.Clear(); CategoryByCode.Clear(); SubById.Clear();
            PersonById.Clear(); SupplierById.Clear(); VendorById.Clear(); ContractById.Clear();
            LinesByItem.Clear(); AllItems.Clear();
            foreach (var c in Companies) CompanyByCode[c.Code] = c;
            foreach (var l in Locations) LocationById[l.Id] = l;
            foreach (var c in Categories)
            {
                CategoryByCode[c.Code] = c;
                foreach (var s in c.Subcategories) { SubById[s.Code] = s; AllItems.AddRange(s.Items); }
            }
            foreach (var p in People) PersonById[p.Id] = p;
            foreach (var s in Suppliers) SupplierById[s.Id] = s;
            foreach (var v in VendorRecords) VendorById[v.Id] = v;
            foreach (var k in Contracts) ContractById[k.Id] = k;
            foreach (var line in Lines)
            {
                if (line.Item == null) continue;
                if (!LinesByItem.TryGetValue(line.Item, out var list)) LinesByItem[line.Item] = list = new List<PurchaseLine>();
                list.Add(line);
            }
        }

        public bool Covers(Contract k, Location loc)
        {
            switch (k.Scope)
            {
                case ContractScope.National: return true;
                case ContractScope.BusinessUnit: return loc.Unit.ToString() == k.ScopeRef;
                case ContractScope.Company: return loc.CompanyCode == k.ScopeRef;
                default: return loc.Id == k.ScopeRef;
            }
        }

        public string ScopeLabel(Contract k)
        {
            switch (k.Scope)
            {
                case ContractScope.National: return "Enterprise-wide";
                case ContractScope.BusinessUnit:
                    return Enum.TryParse(k.ScopeRef, out BusinessUnit u) ? Labels.UnitShort(u) + " only" : k.ScopeRef;
                case ContractScope.Company: return CompanyByCode.TryGetValue(k.ScopeRef, out var c) ? c.ShortName + " only" : k.ScopeRef;
                default: return LocationById.TryGetValue(k.ScopeRef, out var l) ? l.Name : k.ScopeRef;
            }
        }

        /// <summary>Most specific active contract covering this site and subcategory on a date.</summary>
        public Contract CoveringContract(Location loc, string subCode, DateTime date)
        {
            Contract best = null;
            foreach (var k in Contracts)
            {
                if (k.SubcategoryCode != subCode || !k.IsActive(date) || !Covers(k, loc)) continue;
                if (best == null || (int)k.Scope > (int)best.Scope) best = k;
            }
            return best;
        }

        public IEnumerable<PurchaseLine> LinesSince(DateTime from) => Lines.Where(l => l.Date >= from);

        public DateTime T12Start => Today.AddMonths(-12);
    }

    public sealed class EngagementPolicy
    {
        public double ValueThreshold = 25000;
        public bool SplitDetection = true;
        public int SplitWindowDays = 30;
        public bool ContractRule = true;
        public bool NewSupplierRule = true;
        public float SupplierMatchThreshold = 0.86f;
        public int EmergencyReviewHours = 48;
        public double DirectorThreshold = 250000;
        public double ExecutiveThreshold = 1000000;
        public float PriceTolerance = 0.20f;

        public EngagementPolicy Clone() => (EngagementPolicy)MemberwiseClone();
    }

    /// <summary>Builds the whole synthetic network deterministically from a seed.</summary>
    public static class SyntheticDataGenerator
    {
        public const int Seed = 20260921;
        const double LineScale = 0.55;

        public static HubDatabase Build(DateTime today)
        {
            var rng = new Rng(Seed);
            var db = new HubDatabase { Today = today.Date };
            db.Companies = ReferenceData.Companies(db.Today);
            foreach (var c in db.Companies) db.CompanyByCode[c.Code] = c;
            db.Locations = ReferenceData.Locations(db.CompanyByCode, rng);
            db.Categories = ReferenceData.Categories();
            db.People = ReferenceData.People();
            db.Suppliers = ReferenceData.Suppliers(rng, db.Today);
            db.Reindex();
            BuildContracts(db);
            db.Reindex();
            new LineGenerator(db, rng).Generate();
            FinalizeContractCommitments(db, rng);
            db.Reindex();
            return db;
        }

        static void BuildContracts(HubDatabase db)
        {
            int seq = 1;
            foreach (var r in ReferenceData.ContractRows)
            {
                var sup = db.SupplierById[(string)r[0]];
                var sub = db.SubById[(string)r[1]];
                var start = db.Today.AddDays((int)r[4]);
                var k = new Contract
                {
                    SupplierId = sup.Id,
                    SubcategoryCode = sub.Code,
                    Scope = (ContractScope)r[2],
                    ScopeRef = (string)r[3],
                    Start = start,
                    End = db.Today.AddDays((int)r[5]),
                    Discount = (float)r[6],
                    Terms = (string)r[7],
                };
                k.Id = "CA-" + (start.Year % 100).ToString("00") + "-" + seq.ToString("000");
                k.Title = sub.Name + " - " + sup.Name;
                foreach (var it in sub.Items)
                    k.PriceList.Add(new PriceItem { Item = it.Name, Uom = it.Uom, UnitPrice = Math.Round(it.BasePrice * (1 - k.Discount), 2) });
                db.Contracts.Add(k);
                seq++;
            }
        }

        static void FinalizeContractCommitments(HubDatabase db, Rng rng)
        {
            var from = db.T12Start;
            foreach (var k in db.Contracts)
            {
                double spent = 0;
                foreach (var l in db.Lines)
                    if (l.ContractId == k.Id && l.Date >= from) spent += l.Amount;
                double commit = Math.Max(50000, spent * rng.Range(0.8, 1.45));
                k.AnnualCommit = Math.Round(commit / 10000.0) * 10000.0;
            }
        }

        sealed class LineGenerator
        {
            readonly HubDatabase db;
            readonly Rng rng;
            readonly Dictionary<string, List<VendorRecord>> recordsByKey = new Dictionary<string, List<VendorRecord>>();
            readonly HashSet<string> duplicatedKeys = new HashSet<string>();
            readonly Dictionary<string, int> vendorSeq = new Dictionary<string, int>();
            readonly Dictionary<string, int> poSeq = new Dictionary<string, int>();
            int lineId = 1;

            public LineGenerator(HubDatabase db, Rng rng) { this.db = db; this.rng = rng; }

            public void Generate()
            {
                var monthStart = new DateTime(db.Today.Year, db.Today.Month, 1);
                for (int m = 23; m >= 0; m--)
                {
                    var ms = monthStart.AddMonths(-m);
                    int days = DateTime.DaysInMonth(ms.Year, ms.Month);
                    foreach (var loc in db.Locations)
                    {
                        foreach (var demand in DemandProfiles.For(loc.Type))
                        {
                            var sub = db.SubById[demand.Key];
                            double lambda = demand.Value * LineScale * SeasonFactor(ms, sub);
                            int n = rng.Poisson(lambda);
                            for (int i = 0; i < n; i++)
                            {
                                var date = ms.AddDays(rng.Range(0, days - 1)).AddHours(rng.Range(6, 19));
                                if (date > db.Today.AddHours(18)) continue;
                                CreateLine(loc, sub, date);
                            }
                        }
                    }
                }
            }

            static double SeasonFactor(DateTime month, Subcategory sub)
            {
                // Peak season (Aug-Nov) lifts rentals and contract labour; cruise season handled by profile.
                if (sub.Code == "CAP-RNT" || sub.Code == "CAP-SVC")
                    return month.Month >= 8 && month.Month <= 11 ? 1.45 : 0.85;
                return 1.0;
            }

            void CreateLine(Location loc, Subcategory sub, DateTime date)
            {
                var company = loc.Company;
                bool erp = company.ErpLiveOn(date);
                var item = rng.Weighted(sub.Items, it => it.Weight);
                int qty = rng.Skewed(item.MinQty, item.MaxQty);
                bool international = loc.Country != "USA";
                bool emergency = rng.Chance(IsBreakdownCategory(sub.Code) ? 0.07 : 0.015);

                var covering = db.CoveringContract(loc, sub.Code, date);
                Supplier supplier;
                Contract used = null;
                double unit;

                if (covering != null)
                {
                    double p = ComplianceBase(company.Code) - (erp ? 0 : 0.10) - (emergency ? 0.40 : 0) - (international ? 0.30 : 0);
                    if (rng.Chance(Math.Max(0.05, p)))
                    {
                        used = covering;
                        supplier = db.SupplierById[covering.SupplierId];
                        double contractPrice = covering.PriceFor(item.Name) ?? item.BasePrice;
                        unit = rng.Chance(0.035) ? contractPrice * rng.Range(1.18, 1.42) : contractPrice * rng.Range(0.99, 1.02);
                    }
                    else if (rng.Chance(0.25))
                    {
                        supplier = db.SupplierById[covering.SupplierId];
                        unit = item.BasePrice * rng.Range(0.98, 1.12);
                    }
                    else
                    {
                        supplier = PickSupplier(loc, sub, covering.SupplierId);
                        unit = item.BasePrice * rng.Range(1.0, 1.38);
                    }
                }
                else
                {
                    supplier = PickSupplier(loc, sub, null);
                    unit = item.BasePrice * rng.Range(0.90, 1.25);
                }

                // Spend continuing with a supplier after its contract lapsed.
                var expired = db.Contracts.FirstOrDefault(k => k.SubcategoryCode == sub.Code && k.End < date && db.Covers(k, loc));
                if (covering == null && expired != null && rng.Chance(0.7))
                {
                    supplier = db.SupplierById[expired.SupplierId];
                    unit = item.BasePrice * rng.Range(1.0, 1.15);
                }

                if (emergency) unit *= rng.Range(1.05, 1.25);
                unit = Math.Round(unit, 2);
                double amount = Math.Round(unit * qty, 2);

                var source = PickSource(erp, amount);
                var vendor = VendorFor(company, supplier, source, date);

                var line = new PurchaseLine
                {
                    Id = lineId++,
                    Date = date,
                    CompanyCode = company.Code,
                    LocationId = loc.Id,
                    VendorRecordId = vendor.Id,
                    SupplierId = supplier.Id,
                    SubcategoryCode = sub.Code,
                    SourceCategoryCode = RecordedCategory(sub, source),
                    Item = item.Name,
                    Description = item.Name + Context(sub.Code, loc),
                    Qty = qty,
                    UnitPrice = unit,
                    Amount = amount,
                    ContractId = used?.Id,
                    CoveringContractId = covering?.Id,
                    Emergency = emergency,
                    Source = source,
                    CatalogPrice = item.BasePrice,
                };

                double poChance = source == SourceSystem.IfsErp ? 0.94 : source == SourceSystem.LegacyAp ? 0.72 : source == SourceSystem.SiteSpreadsheet ? 0.40 : 0.0;
                if (emergency) poChance *= 0.5;
                if (rng.Chance(poChance)) line.PoNumber = NextPo(company.Code, source);

                line.InvoiceAmount = amount;
                line.ReceivedQty = qty;
                if (line.HasPo)
                {
                    if (rng.Chance(0.045)) line.InvoiceAmount = Math.Round(amount * (1 + rng.Range(0.03, 0.22)), 2);
                    else if (rng.Chance(0.01)) line.InvoiceAmount = Math.Round(amount * (1 - rng.Range(0.03, 0.1)), 2);
                    if (qty > 1 && rng.Chance(0.02)) line.ReceivedQty = qty - 1;
                }
                db.Lines.Add(line);
            }

            static bool IsBreakdownCategory(string code) =>
                code == "MRO-CP" || code == "MRO-FLT" || code == "MRO-WR" || code == "CAP-RNT" || code == "MRO-LUB";

            static double ComplianceBase(string companyCode)
            {
                switch (companyCode)
                {
                    case "1000": return 0.92;
                    case "2100": return 0.82;
                    case "2200": return 0.72;
                    case "2300": return 0.76;
                    case "3100": return 0.70;
                    case "3200": return 0.60;
                    case "3300": return 0.66;
                    case "3400": return 0.74;
                    case "3500": return 0.66;
                    default: return 0.86;
                }
            }

            Supplier PickSupplier(Location loc, Subcategory sub, string excludeId)
            {
                var candidates = db.Suppliers.Where(s => s.Serves(sub.Code) && s.Id != excludeId).ToList();
                if (candidates.Count == 0) candidates = db.Suppliers.Where(s => s.Serves(sub.Code)).ToList();
                return rng.Weighted(candidates, s =>
                {
                    double km = Geo.DistanceKm(loc.Lat, loc.Lon, s.Lat, s.Lon);
                    double country = s.Country == loc.Country ? 1.0 : 0.06;
                    return country * (1.0 / (1.0 + km / 1200.0)) * (0.5 + s.Score);
                });
            }

            SourceSystem PickSource(bool erp, double amount)
            {
                bool small = amount < 2500;
                if (erp) return small && rng.Chance(0.25) ? SourceSystem.PCard : SourceSystem.IfsErp;
                double r = rng.Next();
                if (small && r < 0.25) return SourceSystem.PCard;
                return r < 0.82 ? SourceSystem.LegacyAp : SourceSystem.SiteSpreadsheet;
            }

            string RecordedCategory(Subcategory sub, SourceSystem source)
            {
                double correct, unclassified;
                switch (source)
                {
                    case SourceSystem.IfsErp: correct = 0.98; unclassified = 0.0; break;
                    case SourceSystem.LegacyAp: correct = 0.80; unclassified = 0.12; break;
                    case SourceSystem.SiteSpreadsheet: correct = 0.60; unclassified = 0.35; break;
                    default: correct = 0.45; unclassified = 0.50; break;
                }
                double r = rng.Next();
                if (r < correct) return sub.CategoryCode;
                if (r < correct + unclassified) return "UNCL";
                return rng.Pick(db.Categories).Code;
            }

            string Context(string subCode, Location loc)
            {
                int n = rng.Range(1, 40);
                switch (subCode)
                {
                    case "MRO-CP": return rng.Pick(new[] { " - STS-" + (n % 12 + 1).ToString("00"), " - RTG-" + n.ToString("00"), " - spreader SP-" + n, " - PM backlog", " - MHC-" + (n % 3 + 1) });
                    case "MRO-WR": return rng.Pick(new[] { " - STS-" + (n % 12 + 1).ToString("00") + " main hoist", " - annual rope change", " - MHC-" + (n % 3 + 1), "" });
                    case "MRO-FLT": return rng.Pick(new[] { " - hostler #" + n, " - TH-" + n.ToString("00"), " - yard fleet PM", " - service truck " + n, "" });
                    case "MRO-LUB": return rng.Pick(new[] { " - monthly replenishment", " - crane PM", " - yard fleet", "" });
                    case "CAP-RNT": return rng.Pick(new[] { " - vessel surge", " - peak season", " - unit TH-" + n.ToString("00") + " down", " - project support" });
                    case "CAP-SVC": return rng.Pick(new[] { " - vessel call", " - peak season", " - weekend surge", "" });
                    case "YRD-HOS": return rng.Pick(new[] { " - fleet replacement", " - capacity expansion", " - unit #" + n });
                    case "INF-ENR": return rng.Pick(new[] { " - " + loc.City, "" });
                    default: return "";
                }
            }

            VendorRecord VendorFor(Company company, Supplier supplier, SourceSystem source, DateTime date)
            {
                bool erpFamily = source == SourceSystem.IfsErp || source == SourceSystem.PCard && company.ErpLiveOn(date);
                string key = company.Code + "|" + supplier.Id + "|" + (erpFamily ? "E" : "L");
                if (!recordsByKey.TryGetValue(key, out var list))
                {
                    list = new List<VendorRecord>();
                    recordsByKey[key] = list;
                    list.Add(NewRecord(company, supplier, erpFamily, date, rng.Chance(0.35) ? 0 : rng.Range(1, 6)));
                }
                else if (!duplicatedKeys.Contains(key) && list.Count == 1 && rng.Chance(0.02))
                {
                    duplicatedKeys.Add(key);
                    list.Add(NewRecord(company, supplier, erpFamily, date, rng.Range(1, 6)));
                }
                return list.Count == 1 ? list[0] : rng.Pick(list);
            }

            VendorRecord NewRecord(Company company, Supplier supplier, bool erpFamily, DateTime date, int variant)
            {
                string seqKey = company.Code + (erpFamily ? "E" : "L");
                vendorSeq.TryGetValue(seqKey, out int n);
                n++;
                vendorSeq[seqKey] = n;
                var rec = new VendorRecord
                {
                    Id = erpFamily ? "V" + company.Code + "-" + (n + 100).ToString("00000") : "L" + company.Code + "-" + (n * 7 + 3000).ToString("0000"),
                    CompanyCode = company.Code,
                    SupplierId = supplier.Id,
                    RawName = VendorAliases.Variant(supplier.Name, variant, supplier.City, rng),
                    Source = erpFamily ? SourceSystem.IfsErp : SourceSystem.LegacyAp,
                    Created = date,
                };
                db.VendorRecords.Add(rec);
                return rec;
            }

            string NextPo(string companyCode, SourceSystem source)
            {
                poSeq.TryGetValue(companyCode, out int n);
                n++;
                poSeq[companyCode] = n;
                return source == SourceSystem.IfsErp ? "PO-" + companyCode + "-" + (40000 + n) : "L-" + companyCode + "-" + (7000 + n);
            }
        }
    }

    /// <summary>Monthly purchase-line frequency by site type and subcategory.</summary>
    public static class DemandProfiles
    {
        static readonly Dictionary<SiteType, Dictionary<string, double>> Profiles = new Dictionary<SiteType, Dictionary<string, double>>
        {
            [SiteType.ContainerTerminal] = new Dictionary<string, double>
            {
                ["HEQ-CRN"] = 0.015, ["HEQ-CH"] = 0.05, ["HEQ-FL"] = 0.08, ["YRD-HOS"] = 0.18, ["YRD-CHS"] = 0.06, ["YRD-VEH"] = 0.10,
                ["MRO-CP"] = 5.0, ["MRO-WR"] = 0.9, ["MRO-LUB"] = 2.2, ["MRO-FLT"] = 5.0, ["MRO-TL"] = 1.5,
                ["INF-CON"] = 0.03, ["INF-ENR"] = 1.5, ["INF-FAC"] = 1.4,
                ["WFS-PPE"] = 2.0, ["WFS-UNI"] = 0.9, ["WFS-BEN"] = 0.02,
                ["BOP-SW"] = 0.08, ["BOP-IT"] = 0.4, ["BOP-PRO"] = 0.08, ["BOP-TRV"] = 1.0, ["BOP-OFF"] = 1.2,
                ["CAP-RNT"] = 1.2, ["CAP-SVC"] = 1.3,
            },
            [SiteType.ConventionalCargo] = new Dictionary<string, double>
            {
                ["HEQ-CH"] = 0.02, ["HEQ-FL"] = 0.12, ["YRD-HOS"] = 0.05, ["YRD-BMB"] = 0.08, ["YRD-VEH"] = 0.06,
                ["MRO-CP"] = 0.8, ["MRO-WR"] = 1.0, ["MRO-LUB"] = 1.5, ["MRO-FLT"] = 3.5, ["MRO-TL"] = 1.0,
                ["INF-CON"] = 0.015, ["INF-ENR"] = 1.1, ["INF-FAC"] = 0.9,
                ["WFS-PPE"] = 1.6, ["WFS-UNI"] = 0.7,
                ["BOP-IT"] = 0.2, ["BOP-TRV"] = 0.6, ["BOP-OFF"] = 0.9,
                ["CAP-RNT"] = 1.4, ["CAP-SVC"] = 1.1,
            },
            [SiteType.CruiseTerminal] = new Dictionary<string, double>
            {
                ["HEQ-FL"] = 0.02, ["YRD-VEH"] = 0.03, ["MRO-FLT"] = 0.8, ["MRO-TL"] = 0.4, ["INF-ENR"] = 1.0, ["INF-FAC"] = 1.3,
                ["INF-CON"] = 0.015, ["WFS-PPE"] = 0.8, ["WFS-UNI"] = 0.6, ["BOP-IT"] = 0.2, ["BOP-OFF"] = 0.6, ["BOP-TRV"] = 0.4,
                ["CAP-RNT"] = 0.5, ["CAP-SVC"] = 1.8,
            },
            [SiteType.IntermodalRamp] = new Dictionary<string, double>
            {
                ["HEQ-CH"] = 0.05, ["HEQ-FL"] = 0.02, ["YRD-HOS"] = 0.2, ["YRD-VEH"] = 0.08, ["MRO-CP"] = 0.3, ["MRO-LUB"] = 1.8,
                ["MRO-FLT"] = 5.5, ["MRO-TL"] = 1.2, ["INF-ENR"] = 1.4, ["INF-FAC"] = 0.6, ["INF-CON"] = 0.01, ["WFS-PPE"] = 1.5,
                ["WFS-UNI"] = 0.6, ["BOP-IT"] = 0.2, ["BOP-TRV"] = 0.4, ["BOP-OFF"] = 0.6, ["CAP-RNT"] = 0.8, ["CAP-SVC"] = 1.2,
            },
            [SiteType.SwitchingOperation] = new Dictionary<string, double>
            {
                ["MRO-FLT"] = 3.0, ["MRO-LUB"] = 1.2, ["MRO-TL"] = 0.6, ["INF-ENR"] = 1.8, ["WFS-PPE"] = 0.8, ["WFS-UNI"] = 0.4,
                ["BOP-OFF"] = 0.3, ["CAP-SVC"] = 0.4, ["YRD-VEH"] = 0.04,
            },
            [SiteType.TrailerRepair] = new Dictionary<string, double>
            {
                ["MRO-FLT"] = 6.0, ["MRO-TL"] = 2.2, ["MRO-LUB"] = 0.8, ["WFS-PPE"] = 0.9, ["WFS-UNI"] = 0.5, ["INF-FAC"] = 0.4,
                ["INF-ENR"] = 0.6, ["BOP-OFF"] = 0.3, ["YRD-VEH"] = 0.05,
            },
            [SiteType.AutoFacility] = new Dictionary<string, double>
            {
                ["YRD-VEH"] = 0.12, ["MRO-FLT"] = 1.5, ["MRO-TL"] = 0.5, ["INF-FAC"] = 0.8, ["INF-ENR"] = 0.8, ["WFS-PPE"] = 0.8,
                ["WFS-UNI"] = 0.4, ["CAP-SVC"] = 1.2, ["CAP-RNT"] = 0.4, ["BOP-OFF"] = 0.3,
            },
            [SiteType.CorporateOffice] = new Dictionary<string, double>
            {
                ["BOP-SW"] = 0.6, ["BOP-IT"] = 0.8, ["BOP-PRO"] = 1.2, ["BOP-TRV"] = 3.0, ["BOP-OFF"] = 1.5, ["WFS-BEN"] = 0.6,
                ["INF-FAC"] = 0.8, ["INF-ENR"] = 0.4,
            },
            [SiteType.TechnologyOffice] = new Dictionary<string, double>
            {
                ["BOP-SW"] = 1.0, ["BOP-IT"] = 1.2, ["BOP-PRO"] = 0.4, ["BOP-TRV"] = 2.0, ["BOP-OFF"] = 1.0, ["WFS-BEN"] = 0.3,
                ["INF-FAC"] = 0.5, ["INF-ENR"] = 0.3,
            },
        };

        public static Dictionary<string, double> For(SiteType t) => Profiles[t];
    }

    /// <summary>Generates the realistic vendor-name variants that appear across entities and systems.</summary>
    public static class VendorAliases
    {
        static readonly (string word, string abbr)[] Abbreviations =
        {
            ("Pacific", "Pac"), ("Parts", "Pts"), ("Services", "Svcs"), ("Service", "Svc"), ("Manufacturing", "Mfg"),
            ("International", "Intl"), ("Equipment", "Equip"), ("Industrial", "Ind"), ("Supply", "Sup"), ("Supplies", "Sup"),
            ("Distribution", "Dist"), ("Technology", "Tech"), ("Systems", "Sys"), ("Construction", "Const"),
            ("Engineering", "Eng"), ("Material", "Matl"), ("Handling", "Hdlg"), ("Electric", "Elec"), ("Terminal", "Term"),
            ("Advisors", "Adv"), ("Corporate", "Corp"),
        };

        static readonly string[] LegalTails = { " Co.", " Inc.", " S.A.", " JSC", " LLC" };

        public static string BaseName(string name)
        {
            foreach (var t in LegalTails)
                if (name.EndsWith(t, StringComparison.Ordinal)) return name.Substring(0, name.Length - t.Length);
            return name;
        }

        public static string Variant(string name, int variant, string city, Rng rng)
        {
            string baseName = BaseName(name);
            switch (variant)
            {
                case 0: return name;
                case 1: return StripPunct(name).ToUpperInvariant();
                case 2: return name.EndsWith(" Co.", StringComparison.Ordinal) ? baseName + " Company" : baseName + (rng.Chance(0.5) ? ", Inc." : " LLC");
                case 3: return Abbreviate(baseName) + (rng.Chance(0.5) ? " Inc" : "");
                case 4: return StripPunct(baseName).ToUpperInvariant() + " - " + city.ToUpperInvariant();
                case 5: return Typo(baseName, rng) + (rng.Chance(0.5) ? " Inc." : "");
                default: return baseName + " #" + rng.Range(2, 4) + " (REMIT)";
            }
        }

        static string StripPunct(string s)
        {
            var chars = new List<char>(s.Length);
            foreach (var ch in s) if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '&') chars.Add(ch);
            return new string(chars.ToArray());
        }

        static string Abbreviate(string s)
        {
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
                foreach (var (word, abbr) in Abbreviations)
                    if (words[i] == word) { words[i] = abbr; break; }
            return string.Join(" ", words);
        }

        static string Typo(string s, Rng rng)
        {
            var words = s.Split(' ');
            int idx = 0;
            for (int i = 1; i < words.Length; i++) if (words[i].Length > words[idx].Length) idx = i;
            var w = words[idx];
            if (w.Length >= 5)
            {
                int p = rng.Range(1, w.Length - 3);
                w = rng.Chance(0.5)
                    ? w.Remove(p, 1)
                    : w.Substring(0, p) + w[p + 1] + w[p] + w.Substring(p + 2);
            }
            words[idx] = w;
            return string.Join(" ", words);
        }
    }

    public static class Geo
    {
        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0, dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
