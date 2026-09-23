using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHub
{
    public sealed class SpendFilter
    {
        public BusinessUnit? Unit;
        public string CompanyCode;
        public string CategoryCode;
        public string LocationId;
        public int Months = 12;

        public SpendFilter Clone() => (SpendFilter)MemberwiseClone();
    }

    public sealed class MonthBucket
    {
        public DateTime Month;
        public double OnContract;
        public double OffContract;
        public double Uncovered;
        public double Total => OnContract + OffContract + Uncovered;
        public readonly Dictionary<SourceSystem, double> BySource = new Dictionary<SourceSystem, double>();
    }

    public sealed class SpendSummary
    {
        public double Total;
        public double OnContract;
        public double Covered;
        public double Premium;
        public double NoPo;
        public int Lines;
        public int Suppliers;
        public int VendorRecords;
        public double OnContractShare => Total > 0 ? OnContract / Total : 0;
        public double CoveredCompliance => Covered > 0 ? OnContract / Covered : 0;
        public double NoPoShare => Total > 0 ? NoPo / Total : 0;
    }

    public sealed class EquipmentBenchmark
    {
        public Location Location;
        public double Spend;
        public int Units;
        public double PerUnit => Units > 0 ? Spend / Units : 0;
    }

    /// <summary>Spend cube queries. Each method has an equivalent SQL statement in <see cref="SqlExporter"/>.</summary>
    public sealed class Analytics
    {
        readonly HubDatabase db;
        public Analytics(HubDatabase db) { this.db = db; }

        public DateTime From(SpendFilter f) => new DateTime(db.Today.Year, db.Today.Month, 1).AddMonths(-(f.Months - 1));

        public List<PurchaseLine> Lines(SpendFilter f)
        {
            var from = From(f);
            var list = new List<PurchaseLine>();
            foreach (var l in db.Lines)
            {
                if (l.Date < from) continue;
                if (f.LocationId != null && l.LocationId != f.LocationId) continue;
                if (f.CompanyCode != null && l.CompanyCode != f.CompanyCode) continue;
                if (f.Unit.HasValue && db.CompanyByCode[l.CompanyCode].Unit != f.Unit.Value) continue;
                if (f.CategoryCode != null && db.SubById[l.SubcategoryCode].CategoryCode != f.CategoryCode) continue;
                list.Add(l);
            }
            return list;
        }

        public SpendSummary Summarize(List<PurchaseLine> lines)
        {
            var s = new SpendSummary { Lines = lines.Count };
            var suppliers = new HashSet<string>();
            var vendors = new HashSet<string>();
            foreach (var l in lines)
            {
                s.Total += l.Amount;
                if (l.OnContract) s.OnContract += l.Amount;
                if (l.CoveringContractId != null)
                {
                    s.Covered += l.Amount;
                    if (!l.OnContract)
                    {
                        double? cp = db.ContractOf(l.CoveringContractId).PriceFor(l.Item);
                        if (cp.HasValue && l.UnitPrice > cp.Value) s.Premium += (l.UnitPrice - cp.Value) * l.Qty;
                    }
                }
                if (!l.HasPo) s.NoPo += l.Amount;
                suppliers.Add(l.SupplierId);
                vendors.Add(l.VendorRecordId);
            }
            s.Suppliers = suppliers.Count;
            s.VendorRecords = vendors.Count;
            return s;
        }

        public List<MonthBucket> Monthly(List<PurchaseLine> lines, SpendFilter f)
        {
            var from = From(f);
            var buckets = new List<MonthBucket>();
            for (int i = 0; i < f.Months; i++) buckets.Add(new MonthBucket { Month = from.AddMonths(i) });
            foreach (var l in lines)
            {
                int idx = (l.Date.Year - from.Year) * 12 + l.Date.Month - from.Month;
                if (idx < 0 || idx >= buckets.Count) continue;
                var b = buckets[idx];
                if (l.OnContract) b.OnContract += l.Amount;
                else if (l.CoveringContractId != null) b.OffContract += l.Amount;
                else b.Uncovered += l.Amount;
                b.BySource.TryGetValue(l.Source, out var v);
                b.BySource[l.Source] = v + l.Amount;
            }
            return buckets;
        }

        public List<(string key, string label, double spend, double onContractShare)> ByCategory(List<PurchaseLine> lines) =>
            lines.GroupBy(l => db.SubById[l.SubcategoryCode].CategoryCode)
                .Select(g => (g.Key, db.CategoryByCode[g.Key].Name, g.Sum(l => l.Amount), Share(g)))
                .OrderByDescending(x => x.Item3).ToList();

        public List<(string key, string label, double spend, double onContractShare)> BySubcategory(List<PurchaseLine> lines) =>
            lines.GroupBy(l => l.SubcategoryCode)
                .Select(g => (g.Key, db.SubById[g.Key].Name, g.Sum(l => l.Amount), Share(g)))
                .OrderByDescending(x => x.Item3).ToList();

        public List<(string key, string label, double spend, double onContractShare)> ByCompany(List<PurchaseLine> lines) =>
            lines.GroupBy(l => l.CompanyCode)
                .Select(g => (g.Key, db.CompanyByCode[g.Key].ShortName, g.Sum(l => l.Amount), Share(g)))
                .OrderByDescending(x => x.Item3).ToList();

        public List<(string key, string label, double spend, double onContractShare)> ByLocation(List<PurchaseLine> lines) =>
            lines.GroupBy(l => l.LocationId)
                .Select(g => (g.Key, db.LocationById[g.Key].Name, g.Sum(l => l.Amount), Share(g)))
                .OrderByDescending(x => x.Item3).ToList();

        static double Share(IEnumerable<PurchaseLine> g)
        {
            double t = 0, on = 0;
            foreach (var l in g) { t += l.Amount; if (l.OnContract) on += l.Amount; }
            return t > 0 ? on / t : 0;
        }

        public sealed class SupplierRow
        {
            public Supplier Supplier;
            public double Spend;
            public int Entities;
            public int Sites;
            public int VendorRecords;
            public double OnContractShare;
            public string Categories;
        }

        public List<SupplierRow> BySupplier(List<PurchaseLine> lines)
        {
            var recordsBySupplier = db.VendorRecords.GroupBy(v => v.SupplierId).ToDictionary(g => g.Key, g => g.Count());
            return lines.GroupBy(l => l.SupplierId).Select(g => new SupplierRow
            {
                Supplier = db.SupplierById[g.Key],
                Spend = g.Sum(l => l.Amount),
                Entities = g.Select(l => l.CompanyCode).Distinct().Count(),
                Sites = g.Select(l => l.LocationId).Distinct().Count(),
                VendorRecords = recordsBySupplier.TryGetValue(g.Key, out var n) ? n : 0,
                OnContractShare = Share(g),
                Categories = string.Join(", ", g.Select(l => db.SubById[l.SubcategoryCode].Name).Distinct().Take(2)),
            }).OrderByDescending(r => r.Spend).ToList();
        }

        public double[,] Heatmap(List<PurchaseLine> lines, List<Company> rows, List<Category> cols)
        {
            var m = new double[rows.Count, cols.Count];
            var ri = new Dictionary<string, int>();
            var ci = new Dictionary<string, int>();
            for (int i = 0; i < rows.Count; i++) ri[rows[i].Code] = i;
            for (int j = 0; j < cols.Count; j++) ci[cols[j].Code] = j;
            foreach (var l in lines)
                if (ri.TryGetValue(l.CompanyCode, out int r) && ci.TryGetValue(db.SubById[l.SubcategoryCode].CategoryCode, out int c))
                    m[r, c] += l.Amount;
            return m;
        }

        public static readonly (string key, string title, string subcategory, EquipmentClass[] units)[] BenchmarkDefs =
        {
            ("crane", "Crane parts per crane", "MRO-CP", new[] { EquipmentClass.StsCrane, EquipmentClass.YardCrane, EquipmentClass.MobileHarborCrane }),
            ("fleet", "Fleet MRO parts per powered unit", "MRO-FLT", new[] { EquipmentClass.Hostler, EquipmentClass.ContainerHandler, EquipmentClass.Forklift, EquipmentClass.ServiceTruck, EquipmentClass.Locomotive }),
            ("lube", "Lubricants per powered unit", "MRO-LUB", new[] { EquipmentClass.Hostler, EquipmentClass.ContainerHandler, EquipmentClass.Forklift, EquipmentClass.StsCrane, EquipmentClass.YardCrane, EquipmentClass.Locomotive }),
        };

        public List<EquipmentBenchmark> Equipment(List<PurchaseLine> lines, string subcategory, EquipmentClass[] units)
        {
            var spend = new Dictionary<string, double>();
            foreach (var l in lines)
            {
                if (l.SubcategoryCode != subcategory) continue;
                spend.TryGetValue(l.LocationId, out var v);
                spend[l.LocationId] = v + l.Amount;
            }
            var result = new List<EquipmentBenchmark>();
            foreach (var kv in spend)
            {
                var loc = db.LocationById[kv.Key];
                int n = units.Sum(u => loc.FleetCount(u));
                if (n < 3) continue;
                result.Add(new EquipmentBenchmark { Location = loc, Spend = kv.Value, Units = n });
            }
            result.Sort((a, b) => b.PerUnit.CompareTo(a.PerUnit));
            return result;
        }
    }
}
