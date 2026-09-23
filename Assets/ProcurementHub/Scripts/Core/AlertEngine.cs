using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHub
{
    /// <summary>Scans purchase lines, contracts, vendor records and requests for the data problems procurement cares about.</summary>
    public sealed class AlertEngine
    {
        readonly HubDatabase db;
        public AlertEngine(HubDatabase db) { this.db = db; }

        public List<Alert> Run(ICollection<string> resolved, ICollection<string> acknowledged)
        {
            var alerts = new List<Alert>();
            var t12 = db.T12Start;
            var recent = db.Lines.Where(l => l.Date >= t12).ToList();

            DuplicateSuppliers(alerts, recent);
            OffContract(alerts, recent);
            PriceVariance(alerts, recent);
            MatchExceptions(alerts);
            Emergency(alerts);
            ContractExpiry(alerts, recent);
            ExpiredContractSpend(alerts, recent);
            SplitPurchases(alerts, recent);
            Unclassified(alerts, recent);
            NoPo(alerts, recent);

            foreach (var a in alerts)
            {
                if (resolved != null && resolved.Contains(a.Id)) a.Status = AlertStatus.Resolved;
                else if (acknowledged != null && acknowledged.Contains(a.Id)) a.Status = AlertStatus.Acknowledged;
            }
            alerts.Sort((a, b) =>
            {
                int s = ((int)b.Severity).CompareTo((int)a.Severity);
                return s != 0 ? s : b.Impact.CompareTo(a.Impact);
            });
            return alerts;
        }

        Alert Add(List<Alert> list, string id, AlertType type, Severity sev, string title, string detail, string recommendation, double impact, DateTime raised)
        {
            var a = new Alert { Id = id, Type = type, Severity = sev, Title = title, Detail = detail, Recommendation = recommendation, Impact = impact, Raised = raised };
            list.Add(a);
            return a;
        }

        void DuplicateSuppliers(List<Alert> alerts, List<PurchaseLine> recent)
        {
            var spend = recent.GroupBy(l => l.SupplierId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            foreach (var g in db.VendorRecords.GroupBy(v => v.SupplierId))
            {
                int records = g.Count();
                int entities = g.Select(v => v.CompanyCode).Distinct().Count();
                int spellings = g.Select(v => v.RawName).Distinct().Count();
                if (records < 12 || spellings < 5) continue;
                var s = db.SupplierById[g.Key];
                var a = Add(alerts, "DUP|" + s.Id, AlertType.DuplicateSupplier, records >= 20 ? Severity.Serious : Severity.Warning,
                    s.Name + " exists as " + records + " vendor records",
                    records + " records across " + entities + " entities with " + spellings + " different spellings (e.g. \"" +
                    g.Select(v => v.RawName).Where(n => n != s.Name).FirstOrDefault() + "\"). Spend visibility and leverage are split.",
                    "Approve the suggested merge in Data Quality to create one golden supplier record.",
                    spend.TryGetValue(s.Id, out var v) ? v : 0, g.Max(x => x.Created));
                a.SupplierId = s.Id;
            }
        }

        void OffContract(List<Alert> alerts, List<PurchaseLine> recent)
        {
            var groups = recent.Where(l => l.CoveringContractId != null && l.ContractId == null)
                .GroupBy(l => l.LocationId + "|" + l.SubcategoryCode)
                .Select(g => new { g.Key, Lines = g.ToList(), Spend = g.Sum(l => l.Amount) })
                .Where(g => g.Spend >= 20000)
                .OrderByDescending(g => g.Spend)
                .Take(45);
            foreach (var g in groups)
            {
                var first = g.Lines[0];
                var loc = db.Loc(first.LocationId);
                var sub = db.Sub(first.SubcategoryCode);
                var k = db.ContractOf(first.CoveringContractId);
                double premium = 0;
                foreach (var l in g.Lines)
                {
                    double? cp = k.PriceFor(l.Item);
                    if (cp.HasValue && l.UnitPrice > cp.Value) premium += (l.UnitPrice - cp.Value) * l.Qty;
                }
                var sev = premium >= 150000 ? Severity.Critical : g.Spend >= 100000 ? Severity.Serious : Severity.Warning;
                var a = Add(alerts, "OFF|" + g.Key, AlertType.OffContractSpend, sev,
                    "Off-contract " + sub.Name.ToLowerInvariant() + " at " + loc.Name,
                    Fmt.Money(g.Spend) + " across " + g.Lines.Count + " purchases bypassed " + k.Id + " (" + db.SupplierOf(k.SupplierId).Name +
                    "). Estimated premium paid: " + Fmt.Money(premium) + ".",
                    "Point the site to " + k.Id + " and add the contract to its catalog; recover the premium at the next QBR.",
                    premium, g.Lines.Max(l => l.Date));
                a.LocationId = loc.Id;
                a.ContractId = k.Id;
            }
        }

        void PriceVariance(List<Alert> alerts, List<PurchaseLine> recent)
        {
            var medians = new Dictionary<string, double>();
            foreach (var kv in db.LinesByItem)
                medians[kv.Key] = Stats.Median(kv.Value.Where(l => l.Date >= db.T12Start).Select(l => l.UnitPrice).ToList());

            var flagged = new List<(PurchaseLine line, double reference, bool contract, double excess)>();
            foreach (var l in recent)
            {
                if (l.Amount < 1500) continue;
                double reference;
                bool isContract = false;
                if (l.ContractId != null && db.ContractOf(l.ContractId).PriceFor(l.Item) is double cp) { reference = cp; isContract = true; }
                else if (!medians.TryGetValue(l.Item, out reference) || reference <= 0) continue;
                if (l.UnitPrice <= reference * (1 + db.Policy.PriceTolerance)) continue;
                flagged.Add((l, reference, isContract, (l.UnitPrice - reference) * l.Qty));
            }
            foreach (var g in flagged.GroupBy(f => f.line.SupplierId + "|" + f.line.Item).OrderByDescending(g => g.Sum(x => x.excess)).Take(30))
            {
                var worst = g.OrderByDescending(x => x.line.UnitPrice / x.reference).First();
                var s = db.SupplierOf(worst.line.SupplierId);
                double excess = g.Sum(x => x.excess);
                bool contract = g.Any(x => x.contract);
                var sev = contract ? Severity.Serious : excess >= 20000 ? Severity.Serious : Severity.Warning;
                var a = Add(alerts, "PRC|" + g.Key, AlertType.PriceVariance, sev,
                    (contract ? "Billed above contract: " : "Unusual price: ") + worst.line.Item,
                    s.Name + " charged " + Fmt.Unit(worst.line.UnitPrice) + " vs " + (contract ? "contract " : "network median ") +
                    Fmt.Unit(worst.reference) + " (" + Fmt.SignedPct(worst.line.UnitPrice / worst.reference - 1) + ") on " + g.Count() +
                    " purchase" + (g.Count() == 1 ? "" : "s") + ". Excess " + Fmt.Money(excess) + ".",
                    contract ? "Dispute the invoice variance against the contract price list." : "Benchmark and consolidate this item under an agreement.",
                    excess, g.Max(x => x.line.Date));
                a.SupplierId = s.Id;
                a.LocationId = worst.line.LocationId;
            }
        }

        void MatchExceptions(List<Alert> alerts)
        {
            var from = db.Today.AddDays(-120);
            foreach (var g in db.Lines.Where(l => l.Date >= from && l.MatchException).GroupBy(l => l.LocationId))
            {
                var list = g.ToList();
                double variance = list.Sum(l => Math.Abs(l.InvoiceAmount - l.Amount) + Math.Abs(l.Qty - l.ReceivedQty) * l.UnitPrice);
                if (variance < 6000) continue;
                var loc = db.Loc(g.Key);
                var a = Add(alerts, "MTH|" + g.Key, AlertType.MatchException, variance >= 25000 ? Severity.Serious : Severity.Warning,
                    list.Count + " invoice match exception" + (list.Count == 1 ? "" : "s") + " at " + loc.Name,
                    "Invoice, PO and receipt disagree on " + list.Count + " line" + (list.Count == 1 ? "" : "s") + " in the last 120 days (" +
                    Fmt.Money(variance) + " at risk). Payments are on hold until resolved.",
                    "Confirm receipts with the site and request credit memos for over-billed lines.",
                    variance, list.Max(l => l.Date));
                a.LocationId = loc.Id;
            }
        }

        void Emergency(List<Alert> alerts)
        {
            foreach (var r in db.Requests)
            {
                if (!r.EmergencyPath || r.PostReviewDone || r.Stage == RequestStage.Rejected) continue;
                double hours = (db.Today.AddHours(12) - r.Created).TotalHours;
                bool overdue = hours > db.Policy.EmergencyReviewHours;
                var loc = db.Loc(r.LocationId);
                var a = Add(alerts, "EMR|" + r.Id, AlertType.EmergencyReview, overdue ? Severity.Critical : Severity.Serious,
                    "Emergency purchase " + r.Id + (overdue ? " - post-review overdue" : " awaiting post-review"),
                    r.Description + " at " + loc.Name + " (" + Fmt.Money(r.Total) + "). Raised " + Fmt.Ago(r.Created, db.Today.AddHours(12)) + ".",
                    "Open the request and complete the procurement post-review.",
                    r.Total, r.Created);
                a.LocationId = loc.Id;
                a.RequestId = r.Id;
            }

            var from = db.Today.AddDays(-45);
            foreach (var g in db.Lines.Where(l => l.Date >= from && l.Emergency && !l.HasPo).GroupBy(l => l.LocationId))
            {
                var list = g.ToList();
                double sum = list.Sum(l => l.Amount);
                if (sum < 10000) continue;
                var loc = db.Loc(g.Key);
                var a = Add(alerts, "EML|" + g.Key, AlertType.EmergencyReview, sum > 60000 ? Severity.Serious : Severity.Warning,
                    list.Count + " emergency buys without PO at " + loc.Name,
                    Fmt.Money(sum) + " of breakdown purchases in the last 45 days bypassed requisitioning. Most common: " +
                    list.GroupBy(l => l.SubcategoryCode).OrderByDescending(x => x.Sum(l => l.Amount)).Select(x => db.Sub(x.Key).Name).First() + ".",
                    "Set up an emergency catalog or consignment stock with the contracted supplier.",
                    sum, list.Max(l => l.Date));
                a.LocationId = loc.Id;
            }
        }

        void ContractExpiry(List<Alert> alerts, List<PurchaseLine> recent)
        {
            foreach (var k in db.Contracts)
            {
                int days = (int)(k.End - db.Today).TotalDays;
                if (days < 0 || days > 120) continue;
                double spend = recent.Where(l => l.ContractId == k.Id).Sum(l => l.Amount);
                var a = Add(alerts, "EXP|" + k.Id, AlertType.ContractExpiring, days <= 45 ? Severity.Serious : Severity.Warning,
                    k.Id + " expires in " + days + " days",
                    k.Title + " (" + db.ScopeLabel(k) + ") carries " + Fmt.Money(spend) + " of 12-month spend. Ends " + Fmt.Date(k.End) + ".",
                    "Start renewal or re-bid now to avoid off-contract spend after expiry.",
                    spend, db.Today);
                a.ContractId = k.Id;
                a.SupplierId = k.SupplierId;
            }
        }

        void ExpiredContractSpend(List<Alert> alerts, List<PurchaseLine> recent)
        {
            foreach (var k in db.Contracts.Where(k => k.End < db.Today))
            {
                var lines = recent.Where(l => l.Date > k.End && l.SupplierId == k.SupplierId && l.SubcategoryCode == k.SubcategoryCode).ToList();
                if (lines.Count == 0) continue;
                double sum = lines.Sum(l => l.Amount);
                var a = Add(alerts, "EXS|" + k.Id, AlertType.ExpiredContractSpend, Severity.Serious,
                    "Spend continuing after " + k.Id + " expired",
                    Fmt.Money(sum) + " with " + db.SupplierOf(k.SupplierId).Name + " across " + lines.Select(l => l.LocationId).Distinct().Count() +
                    " sites since the agreement ended on " + Fmt.Date(k.End) + ". Pricing and terms are no longer protected.",
                    "Extend or renew the agreement, or move volume to a contracted supplier.",
                    sum, lines.Max(l => l.Date));
                a.ContractId = k.Id;
                a.SupplierId = k.SupplierId;
            }
        }

        void SplitPurchases(List<Alert> alerts, List<PurchaseLine> recent)
        {
            double threshold = db.Policy.ValueThreshold;
            int count = 0;
            foreach (var g in recent.Where(l => l.Amount < threshold).GroupBy(l => l.LocationId + "|" + l.SupplierId))
            {
                var list = g.OrderBy(l => l.Date).ToList();
                for (int i = 0; i < list.Count && count < 25; i++)
                {
                    double sum = 0;
                    int j = i;
                    while (j < list.Count && (list[j].Date - list[i].Date).TotalDays <= 7) { sum += list[j].Amount; j++; }
                    if (j - i < 2 || sum < threshold) continue;
                    var loc = db.Loc(list[i].LocationId);
                    var s = db.SupplierOf(list[i].SupplierId);
                    var a = Add(alerts, "SPL|" + g.Key + "|" + list[i].Id, AlertType.SplitPurchase, Severity.Warning,
                        "Possible split purchase at " + loc.Name,
                        (j - i) + " purchases from " + s.Name + " within 7 days (" + Fmt.ShortDate(list[i].Date) + " - " + Fmt.ShortDate(list[j - 1].Date) +
                        "), each under " + Fmt.Money(threshold) + " but " + Fmt.Money(sum) + " combined.",
                        "Review with the site; combined need should have followed Rule 1.",
                        sum, list[j - 1].Date);
                    a.LocationId = loc.Id;
                    a.SupplierId = s.Id;
                    count++;
                    i = j - 1;
                }
            }
        }

        void Unclassified(List<Alert> alerts, List<PurchaseLine> recent)
        {
            foreach (var g in recent.Where(l => l.SourceCategoryCode == "UNCL").GroupBy(l => l.CompanyCode))
            {
                double sum = g.Sum(l => l.Amount);
                var c = db.CompanyByCode[g.Key];
                Add(alerts, "UNC|" + g.Key, AlertType.UnclassifiedSpend, sum > 1e6 ? Severity.Serious : Severity.Warning,
                    Fmt.Money(sum) + " unclassified spend in " + c.ShortName,
                    g.Count() + " lines from " + string.Join(" / ", g.Select(l => Labels.Source(l.Source)).Distinct()) +
                    " carry no category, so they are invisible in category reporting.",
                    "Review the auto-classification suggestions in Data Quality and accept in bulk.",
                    sum, g.Max(l => l.Date));
            }
        }

        void NoPo(List<Alert> alerts, List<PurchaseLine> recent)
        {
            foreach (var g in recent.Where(l => !l.HasPo && l.Amount >= 10000).GroupBy(l => l.LocationId))
            {
                double sum = g.Sum(l => l.Amount);
                if (sum < 180000) continue;
                var loc = db.Loc(g.Key);
                var a = Add(alerts, "NPO|" + g.Key, AlertType.NoPurchaseOrder, sum > 500000 ? Severity.Serious : Severity.Warning,
                    Fmt.Money(sum) + " invoiced without a PO at " + loc.Name,
                    g.Count() + " invoices of $10K or more had no purchase order in the last 12 months (after-the-fact buying).",
                    "Enable requisitioning for this site in IFS and brief the site on the engagement rules.",
                    sum, g.Max(l => l.Date));
                a.LocationId = loc.Id;
            }
        }
    }
}
