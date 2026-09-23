using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHub
{
    /// <summary>Everything the intake form knows about a request before it is submitted.</summary>
    public sealed class RequestDraft
    {
        public string Requester;
        public string CompanyCode;
        public string LocationId;
        public string Description;
        public string SubcategoryOverride;
        public double Quantity = 1;
        public double UnitPrice;
        public Urgency Urgency = Urgency.Routine;
        public bool RequiresContract;
        public string SupplierText;
        public int NeededInDays = 14;
        public string ExcludeRequestId;
        public double Total => Math.Max(0, Quantity) * Math.Max(0, UnitPrice);
    }

    public sealed class RuleOutcome
    {
        public string Id;
        public string Title;
        public bool Enabled;
        public bool Triggered;
        public string Detail;
    }

    public sealed class EngagementResult
    {
        public bool Required;
        public bool EmergencyPath;
        public readonly List<RuleOutcome> Rules = new List<RuleOutcome>();
        public string Decision;
        public string Summary;
        public double AggregatedSpend;
        public string TriggeredIds => string.Join(",", Rules.Where(r => r.Triggered).Select(r => r.Id));
    }

    public sealed class ApprovalStep
    {
        public string Role;
        public string Name;
        public string Why;
    }

    public sealed class RoutingResult
    {
        public Person Manager;
        public string Reason;
        public readonly List<ApprovalStep> Approvals = new List<ApprovalStep>();
    }

    public sealed class ContractOption
    {
        public Contract Contract;
        public Supplier Supplier;
        public double? UnitPrice;
        public double EstSavings;
        public int DaysLeft;
        public string Coverage;
    }

    public sealed class SupplierOption
    {
        public Supplier Supplier;
        public bool HasContract;
        public double Spend12M;
        public int Sites;
        public double DistanceKm;
    }

    public sealed class PriceBenchmark
    {
        public CatalogItem Item;
        public int Samples;
        public double P25, Median, P75, Min, Max;
        public double? ContractPrice;
        public double Requested;
        public double VarianceVsMedian;
        public double VarianceVsContract;
        public bool Flagged;
    }

    public sealed class Recommendation
    {
        public string Headline;
        public string Action;
        public float Confidence;
        public double EstSavings;
        public ContractOption Contract;
        public readonly List<string> Reasons = new List<string>();
        public readonly List<string> Risks = new List<string>();
        public readonly List<string> Checks = new List<string>();
        public string ToPlainText()
        {
            var lines = new List<string> { Headline };
            lines.AddRange(Reasons.Select(r => "- " + r));
            if (Risks.Count > 0) lines.AddRange(Risks.Select(r => "! " + r));
            return string.Join("\n", lines);
        }
    }

    /// <summary>The full evaluation of a draft: the object the intake decision panel renders.</summary>
    public sealed class Evaluation
    {
        public RequestDraft Draft;
        public Location Location;
        public List<ClassificationResult> Classification = new List<ClassificationResult>();
        public Subcategory Sub;
        public float Confidence;
        public CatalogItem Item;
        public SupplierMatch SupplierMatch;
        public EngagementResult Engagement;
        public RoutingResult Routing;
        public List<ContractOption> Contracts = new List<ContractOption>();
        public List<SupplierOption> Suppliers = new List<SupplierOption>();
        public PriceBenchmark Benchmark;
        public Recommendation Recommendation;
    }

    public sealed class DecisionEngine
    {
        readonly HubDatabase db;
        public readonly CategoryClassifier Classifier;
        public SupplierMatcher Matcher { get; private set; }

        static readonly string[] ContractTerms =
        {
            "contract", "agreement", "lease", "sow", "statement of work", "msa", "master service", "subscription", "multi-year",
            "retainer", "terms and conditions", "service agreement", "renewal", "engagement letter",
        };

        public DecisionEngine(HubDatabase db)
        {
            this.db = db;
            Classifier = new CategoryClassifier(db);
            Matcher = new SupplierMatcher(db);
        }

        public void RefreshMatcher() => Matcher = new SupplierMatcher(db);

        public Evaluation Evaluate(RequestDraft d)
        {
            var ev = new Evaluation { Draft = d, Location = db.Loc(d.LocationId) };
            ev.SupplierMatch = Matcher.Match(d.SupplierText);
            var hint = ev.SupplierMatch != null && ev.SupplierMatch.IsConfident ? ev.SupplierMatch.Supplier : null;
            ev.Classification = Classifier.Classify(d.Description, hint);

            if (!string.IsNullOrEmpty(d.SubcategoryOverride) && db.Sub(d.SubcategoryOverride) != null)
            {
                ev.Sub = db.Sub(d.SubcategoryOverride);
                var hit = ev.Classification.FirstOrDefault(c => c.Sub == ev.Sub);
                ev.Confidence = 1f;
                if (hit == null && ev.Classification.Count > 0) ev.Confidence = 1f;
            }
            else if (ev.Classification.Count > 0)
            {
                ev.Sub = ev.Classification[0].Sub;
                ev.Confidence = ev.Classification[0].Confidence;
            }

            ev.Item = Classifier.MatchItem(ev.Sub, d.Description);
            ev.Engagement = EvaluateRules(d, ev.Sub, ev.SupplierMatch, ev.Location);
            ev.Routing = Route(d, ev.Sub, ev.Engagement, ev.Location);
            if (ev.Location != null && ev.Sub != null)
            {
                ev.Contracts = ContractsFor(ev.Location, ev.Sub, ev.Item, d.UnitPrice, d.Quantity);
                ev.Suppliers = SuppliersFor(ev.Sub, ev.Location);
            }
            ev.Benchmark = Benchmark(ev.Item, d.UnitPrice, ev.Contracts.FirstOrDefault());
            ev.Recommendation = Recommend(ev);
            return ev;
        }

        // ---------------------------------------------------------------- engagement rules

        public EngagementResult EvaluateRules(RequestDraft d, Subcategory sub, SupplierMatch sm, Location loc)
        {
            var p = db.Policy;
            var res = new EngagementResult();
            double total = d.Total;

            // Rule 1: value threshold, including related spend that looks like a split purchase.
            var r1 = new RuleOutcome { Id = "R1", Title = "Rule 1 - Spend threshold", Enabled = true };
            if (total >= p.ValueThreshold)
            {
                r1.Triggered = true;
                r1.Detail = "Estimated value " + Fmt.MoneyExact(total) + " meets the " + Fmt.MoneyExact(p.ValueThreshold) + " engagement threshold.";
            }
            else
            {
                double related = p.SplitDetection && loc != null && sub != null ? RelatedSpend(loc, sub, sm, d.ExcludeRequestId) : 0;
                res.AggregatedSpend = related;
                if (p.SplitDetection && related > 0 && related + total >= p.ValueThreshold && total > 0)
                {
                    r1.Triggered = true;
                    r1.Detail = "Related " + sub.Name.ToLowerInvariant() + " purchases at this site in the last " + p.SplitWindowDays +
                                " days total " + Fmt.MoneyExact(related) + "; combined " + Fmt.MoneyExact(related + total) +
                                " crosses the threshold (possible split purchase).";
                }
                else
                {
                    r1.Detail = total <= 0
                        ? "Enter quantity and unit price to evaluate."
                        : Fmt.MoneyExact(total) + " is below the " + Fmt.MoneyExact(p.ValueThreshold) + " threshold" +
                          (related > 0 ? " (related 30-day spend " + Fmt.MoneyExact(related) + ")." : ".");
                }
            }
            res.Rules.Add(r1);

            // Rule 2: any contractual commitment, regardless of value.
            var r2 = new RuleOutcome { Id = "R2", Title = "Rule 2 - Contractual commitment", Enabled = p.ContractRule };
            string term = FindContractTerm(d.Description);
            if (!p.ContractRule) r2.Detail = "Disabled in policy settings.";
            else if (d.RequiresContract)
            {
                r2.Triggered = true;
                r2.Detail = "Requester indicated a contract, agreement, lease or SOW must be signed. Only Procurement may commit Carrix to terms.";
            }
            else if (term != null)
            {
                r2.Triggered = true;
                r2.Detail = "Description suggests a contractual commitment (\"" + term + "\"). Procurement must review terms before signature.";
            }
            else r2.Detail = "No contract, agreement, lease or SOW required.";
            res.Rules.Add(r2);

            // Rule 3: new or non-approved supplier.
            var r3 = new RuleOutcome { Id = "R3", Title = "Rule 3 - New or non-approved supplier", Enabled = p.NewSupplierRule };
            if (!p.NewSupplierRule) r3.Detail = "Disabled in policy settings.";
            else if (string.IsNullOrWhiteSpace(d.SupplierText))
                r3.Detail = "No supplier named - Procurement will assign a contracted or preferred supplier.";
            else if (sm == null || !sm.IsConfident)
            {
                r3.Triggered = true;
                r3.Detail = "\"" + d.SupplierText.Trim() + "\" is not in the vendor master" +
                            (sm != null ? " (closest: " + sm.Supplier.Name + ", " + Fmt.Pct(sm.Similarity) + " match)" : "") +
                            ". New-supplier onboarding, tax and risk review required.";
            }
            else if (sub != null && !sm.Supplier.Serves(sub.Code))
            {
                r3.Triggered = true;
                r3.Detail = sm.Supplier.Name + " is in the vendor master but not approved for " + sub.Name + ".";
            }
            else
                r3.Detail = sm.Supplier.Name + " is an approved supplier" + (sub != null ? " for " + sub.Name : "") +
                            " (matched \"" + sm.MatchedName + "\", " + Fmt.Pct(sm.Similarity) + ").";
            res.Rules.Add(r3);

            res.Required = res.Rules.Any(r => r.Enabled && r.Triggered);
            res.EmergencyPath = d.Urgency == Urgency.Emergency;
            if (res.EmergencyPath)
            {
                res.Decision = "Emergency - proceed, post-review required";
                res.Summary = "Operations may proceed immediately to restore service. Procurement completes a post-review within " +
                              p.EmergencyReviewHours + " hours" + (res.Required ? " (rules " + res.TriggeredIds.Replace("R", "") + " would otherwise apply)." : ".");
            }
            else if (res.Required)
            {
                res.Decision = "Procurement engagement required";
                res.Summary = "Triggered by " + string.Join(", ", res.Rules.Where(r => r.Triggered).Select(r => r.Title.Split('-')[0].Trim())) +
                              ". The request is routed to the category manager before any commitment is made.";
            }
            else
            {
                res.Decision = "Self-serve purchase allowed";
                res.Summary = "No engagement rule applies. Buy through the catalog or an existing contract; the PO is issued automatically.";
            }
            return res;
        }

        static string FindContractTerm(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return null;
            string c = CategoryClassifier.Clean(description);
            foreach (var t in ContractTerms)
                if (c.Contains(" " + t + " ")) return t;
            return null;
        }

        double RelatedSpend(Location loc, Subcategory sub, SupplierMatch sm, string excludeRequestId)
        {
            var from = db.Today.AddDays(-db.Policy.SplitWindowDays);
            double sum = 0;
            foreach (var l in db.Lines)
            {
                if (l.Date < from || l.LocationId != loc.Id || l.SubcategoryCode != sub.Code) continue;
                if (sm != null && sm.IsConfident && l.SupplierId != sm.Supplier.Id) continue;
                sum += l.Amount;
            }
            foreach (var r in db.Requests)
            {
                if (r.Id == excludeRequestId || r.Created < from || r.LocationId != loc.Id || r.SubcategoryCode != sub.Code) continue;
                if (r.Stage == RequestStage.Rejected || r.Stage == RequestStage.Closed || r.Stage == RequestStage.Invoiced) continue;
                sum += r.Total;
            }
            return sum;
        }

        // ---------------------------------------------------------------- routing & approvals

        public RoutingResult Route(RequestDraft d, Subcategory sub, EngagementResult eng, Location loc)
        {
            var p = db.Policy;
            var res = new RoutingResult { Manager = db.ManagerFor(sub) };
            double total = d.Total;
            res.Reason = sub == null
                ? "Category not determined yet - describe the item to route the request."
                : sub.Name + " belongs to " + sub.Category.Name + ", managed by " + res.Manager.Name + ".";

            string site = loc != null ? loc.Name : "the requesting site";
            res.Approvals.Add(new ApprovalStep { Role = "Budget owner", Name = "Site manager, " + site, Why = "Confirms operational need and cost center." });
            if (eng != null && eng.Required && res.Manager != null)
                res.Approvals.Add(new ApprovalStep { Role = "Category manager", Name = res.Manager.Name, Why = "Engagement rules " + eng.TriggeredIds.Replace("R", "") + " apply." });
            if (total >= p.DirectorThreshold)
                res.Approvals.Add(new ApprovalStep { Role = "Director", Name = db.PersonOf("DIR-GP").Name, Why = "Value at or above " + Fmt.Money(p.DirectorThreshold) + "." });
            if (total >= p.ExecutiveThreshold || (sub != null && sub.Capex && total >= p.DirectorThreshold))
                res.Approvals.Add(new ApprovalStep { Role = "Capital committee", Name = db.PersonOf("VP-FIN").Name, Why = sub != null && sub.Capex ? "Capital equipment purchase." : "Value at or above " + Fmt.Money(p.ExecutiveThreshold) + "." });
            if (eng != null && eng.EmergencyPath && res.Manager != null)
                res.Approvals.Add(new ApprovalStep { Role = "Post-review", Name = res.Manager.Name, Why = "Emergency purchase reviewed within " + p.EmergencyReviewHours + "h." });
            return res;
        }

        // ---------------------------------------------------------------- contracts, suppliers, pricing

        public List<ContractOption> ContractsFor(Location loc, Subcategory sub, CatalogItem item, double requestedUnit, double qty)
        {
            var list = new List<ContractOption>();
            foreach (var k in db.Contracts)
            {
                if (k.SubcategoryCode != sub.Code || !k.IsActive(db.Today) || !db.Covers(k, loc)) continue;
                double? price = item != null ? k.PriceFor(item.Name) : null;
                var opt = new ContractOption
                {
                    Contract = k,
                    Supplier = db.SupplierOf(k.SupplierId),
                    UnitPrice = price,
                    DaysLeft = (int)(k.End - db.Today).TotalDays,
                    Coverage = db.ScopeLabel(k),
                };
                if (price.HasValue && requestedUnit > price.Value) opt.EstSavings = (requestedUnit - price.Value) * Math.Max(1, qty);
                list.Add(opt);
            }
            list.Sort((a, b) => ((int)b.Contract.Scope).CompareTo((int)a.Contract.Scope));
            return list;
        }

        public List<SupplierOption> SuppliersFor(Subcategory sub, Location loc)
        {
            var from = db.T12Start;
            var spend = new Dictionary<string, double>();
            var sites = new Dictionary<string, HashSet<string>>();
            foreach (var l in db.Lines)
            {
                if (l.SubcategoryCode != sub.Code || l.Date < from) continue;
                spend.TryGetValue(l.SupplierId, out var s);
                spend[l.SupplierId] = s + l.Amount;
                if (!sites.TryGetValue(l.SupplierId, out var set)) sites[l.SupplierId] = set = new HashSet<string>();
                set.Add(l.LocationId);
            }
            var contracted = new HashSet<string>(db.Contracts.Where(k => k.SubcategoryCode == sub.Code && k.IsActive(db.Today) && db.Covers(k, loc)).Select(k => k.SupplierId));
            return db.Suppliers.Where(s => s.Serves(sub.Code)).Select(s => new SupplierOption
            {
                Supplier = s,
                HasContract = contracted.Contains(s.Id),
                Spend12M = spend.TryGetValue(s.Id, out var v) ? v : 0,
                Sites = sites.TryGetValue(s.Id, out var set) ? set.Count : 0,
                DistanceKm = Geo.DistanceKm(loc.Lat, loc.Lon, s.Lat, s.Lon),
            })
            .OrderByDescending(o => o.HasContract)
            .ThenByDescending(o => o.Supplier.Score * 0.5 + Math.Min(1.0, o.Spend12M / 2e6) * 0.3 + (o.DistanceKm < 1500 ? 0.2 : 0))
            .ToList();
        }

        public PriceBenchmark Benchmark(CatalogItem item, double requestedUnit, ContractOption contract)
        {
            if (item == null) return null;
            var prices = new List<double>();
            if (db.LinesByItem.TryGetValue(item.Name, out var lines))
                foreach (var l in lines)
                    if (l.Date >= db.T12Start) prices.Add(l.UnitPrice);
            prices.Sort();
            var b = new PriceBenchmark
            {
                Item = item,
                Samples = prices.Count,
                P25 = Stats.Percentile(prices, 0.25),
                Median = Stats.Percentile(prices, 0.5),
                P75 = Stats.Percentile(prices, 0.75),
                Min = prices.Count > 0 ? prices[0] : item.BasePrice,
                Max = prices.Count > 0 ? prices[prices.Count - 1] : item.BasePrice,
                ContractPrice = contract?.UnitPrice,
                Requested = requestedUnit,
            };
            if (b.Samples == 0) { b.P25 = b.Median = b.P75 = item.BasePrice; }
            if (requestedUnit > 0)
            {
                b.VarianceVsMedian = b.Median > 0 ? requestedUnit / b.Median - 1 : 0;
                b.VarianceVsContract = b.ContractPrice.HasValue ? requestedUnit / b.ContractPrice.Value - 1 : 0;
                b.Flagged = b.VarianceVsMedian > db.Policy.PriceTolerance || (b.ContractPrice.HasValue && b.VarianceVsContract > db.Policy.PriceTolerance);
            }
            return b;
        }

        // ---------------------------------------------------------------- assistant recommendation

        public Recommendation Recommend(Evaluation ev)
        {
            var rec = new Recommendation();
            var d = ev.Draft;
            var eng = ev.Engagement;
            var best = ev.Contracts.FirstOrDefault();
            rec.Contract = best;

            rec.Checks.Add("Classified against " + Classifier.SubcategoryCount + " subcategories");
            rec.Checks.Add("Evaluated 3 engagement rules");
            rec.Checks.Add("Searched " + db.Contracts.Count + " agreements for coverage at this site");
            rec.Checks.Add("Matched supplier text against " + db.VendorRecords.Count + " vendor records");
            if (ev.Benchmark != null) rec.Checks.Add("Benchmarked against " + ev.Benchmark.Samples + " purchases of this item (12 months)");

            if (ev.Sub == null)
            {
                rec.Headline = "Describe the item so I can classify and route it.";
                rec.Action = "Needs more detail";
                rec.Confidence = 0f;
                return rec;
            }

            var manager = ev.Routing.Manager;
            var preferred = ev.Suppliers.FirstOrDefault();
            string supplierName = best != null ? best.Supplier.Name : preferred != null ? preferred.Supplier.Name : "a qualified supplier";

            if (eng.EmergencyPath)
            {
                rec.Headline = "Proceed now with " + supplierName + (best != null ? " under " + best.Contract.Id : "") +
                               "; " + manager.Name + " completes the post-review within " + db.Policy.EmergencyReviewHours + "h.";
                rec.Action = "Emergency purchase + post-review";
            }
            else if (best != null && eng.Required)
            {
                rec.Headline = "Route to " + manager.Name + " and buy under " + best.Contract.Id + " (" + best.Supplier.Name + ").";
                rec.Action = "Engage procurement, use existing contract";
            }
            else if (best != null)
            {
                rec.Headline = "Buy directly under " + best.Contract.Id + " with " + best.Supplier.Name + " - no engagement needed.";
                rec.Action = "Self-serve under contract";
            }
            else if (eng.Required)
            {
                var names = ev.Suppliers.Take(3).Select(s => s.Supplier.Name).ToList();
                rec.Headline = "Route to " + manager.Name + " to source competitively" + (names.Count > 0 ? " (e.g. " + string.Join(", ", names) + ")." : ".");
                rec.Action = "Engage procurement, competitive sourcing";
            }
            else
            {
                rec.Headline = "Self-serve purchase from " + supplierName + " is within policy.";
                rec.Action = "Self-serve";
            }

            var top = ev.Classification.FirstOrDefault();
            if (top != null && string.IsNullOrEmpty(d.SubcategoryOverride))
                rec.Reasons.Add("Classified as " + ev.Sub.Category.Name + " / " + ev.Sub.Name + " (" + Fmt.Pct(ev.Confidence) + ") from " +
                                string.Join(", ", top.Evidence.Distinct().Take(3)) + ".");
            else rec.Reasons.Add("Category set manually to " + ev.Sub.Category.Name + " / " + ev.Sub.Name + ".");

            foreach (var r in eng.Rules.Where(r => r.Triggered && r.Enabled)) rec.Reasons.Add(r.Detail);
            if (!eng.Required && !eng.EmergencyPath) rec.Reasons.Add("None of the three engagement rules apply.");

            if (best != null)
            {
                string price = best.UnitPrice.HasValue ? " at " + Fmt.Unit(best.UnitPrice.Value) + "/" + (ev.Item?.Uom ?? "unit") : "";
                rec.Reasons.Add(best.Contract.Id + " covers this site (" + best.Coverage + ")" + price + ", valid " + best.DaysLeft + " more days.");
                if (best.EstSavings > 0)
                {
                    rec.EstSavings = best.EstSavings;
                    rec.Reasons.Add("Using the contract price saves about " + Fmt.MoneyExact(best.EstSavings) + " versus the estimate entered.");
                }
                if (best.DaysLeft < 60) rec.Risks.Add(best.Contract.Id + " expires in " + best.DaysLeft + " days - confirm renewal pricing.");
            }
            else rec.Risks.Add("No active agreement covers " + ev.Sub.Name + " at this site.");

            var sm = ev.SupplierMatch;
            if (sm != null && sm.IsConfident && best != null && sm.Supplier.Id != best.Supplier.Id)
                rec.Risks.Add("Requested supplier " + sm.Supplier.Name + " is not the contracted supplier - this would be off-contract spend.");
            if (ev.Benchmark != null && ev.Benchmark.Flagged)
                rec.Risks.Add("Estimated unit price is " + Fmt.SignedPct(ev.Benchmark.VarianceVsMedian) + " versus the network median (" + Fmt.Unit(ev.Benchmark.Median) + ").");
            if (ev.Confidence < 0.55f && string.IsNullOrEmpty(d.SubcategoryOverride))
                rec.Risks.Add("Classification confidence is low - confirm the category before submitting.");

            float conf = string.IsNullOrEmpty(d.SubcategoryOverride) ? ev.Confidence : 0.95f;
            if (sm != null && !sm.IsConfident) conf *= 0.9f;
            if (d.Total <= 0) conf *= 0.7f;
            rec.Confidence = Math.Max(0.2f, Math.Min(0.97f, 0.35f + conf * 0.62f));
            return rec;
        }
    }
}
