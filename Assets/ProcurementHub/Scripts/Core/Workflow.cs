using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHub
{
    /// <summary>Request lifecycle: intake -> routing -> sourcing -> approval -> PO -> receipt -> invoice match -> close.</summary>
    public sealed class Workflow
    {
        readonly HubDatabase db;
        readonly DecisionEngine engine;
        int nextId;

        public Workflow(HubDatabase db, DecisionEngine engine)
        {
            this.db = db;
            this.engine = engine;
            nextId = 10400;
            foreach (var r in db.Requests)
            {
                if (r.Id != null && r.Id.StartsWith("REQ-") && int.TryParse(r.Id.Substring(4), out int n)) nextId = Math.Max(nextId, n + 1);
                int dash = r.PoNumber != null ? r.PoNumber.LastIndexOf('-') : -1;
                if (dash > 0 && int.TryParse(r.PoNumber.Substring(dash + 1), out int po) && po >= poCounter) poCounter = po + 1;
            }
        }

        public static readonly RequestStage[] EngagedPath =
        {
            RequestStage.Intake, RequestStage.Routed, RequestStage.Sourcing, RequestStage.Approval,
            RequestStage.PurchaseOrder, RequestStage.Received, RequestStage.Invoiced, RequestStage.Closed,
        };

        public static readonly RequestStage[] SelfServePath =
        {
            RequestStage.Intake, RequestStage.PurchaseOrder, RequestStage.Received, RequestStage.Invoiced, RequestStage.Closed,
        };

        public static RequestStage[] PathFor(ProcurementRequest r) => r.EngagementRequired ? EngagedPath : SelfServePath;

        public ProcurementRequest Submit(RequestDraft d, Evaluation ev, string actor, bool acceptRecommendation, DateTime at)
        {
            var r = new ProcurementRequest
            {
                Id = "REQ-" + nextId++,
                CreatedTicks = at.Ticks,
                Requester = actor,
                UserCreated = true,
            };
            Apply(r, d, ev);
            r.RecommendationState = acceptRecommendation ? 1 : 0;
            Log(r, RequestStage.Intake, actor, "Submitted via intake. " + ev.Engagement.Decision + ".", at);
            if (acceptRecommendation) Log(r, RequestStage.Intake, actor, "Accepted assistant recommendation: " + ev.Recommendation.Headline, at);

            if (r.EngagementRequired && !r.EmergencyPath)
            {
                r.Stage = RequestStage.Routed;
                Log(r, RequestStage.Routed, "Routing engine", "Routed to " + ev.Routing.Manager.Name + " (" + ev.Sub.Category.Name + ").", at.AddSeconds(2));
            }
            else
            {
                r.Stage = RequestStage.PurchaseOrder;
                r.PoNumber = NewPo(r);
                string why = r.EmergencyPath ? "Emergency path: PO issued immediately, post-review queued for " + ev.Routing.Manager.Name + "."
                                             : "Self-serve: catalog PO issued automatically" + (r.ContractId != null ? " under " + r.ContractId : "") + ".";
                Log(r, RequestStage.PurchaseOrder, "Workflow", why + " " + r.PoNumber, at.AddSeconds(2));
            }
            db.Requests.Insert(0, r);
            return r;
        }

        /// <summary>Copies an evaluation onto a request (used at submit and when policy changes).</summary>
        public void Apply(ProcurementRequest r, RequestDraft d, Evaluation ev)
        {
            r.CompanyCode = d.CompanyCode;
            r.LocationId = d.LocationId;
            r.Description = d.Description;
            r.SubcategoryCode = ev.Sub?.Code;
            r.ClassificationConfidence = ev.Confidence;
            r.CategoryOverridden = !string.IsNullOrEmpty(d.SubcategoryOverride);
            r.Quantity = d.Quantity;
            r.UnitPrice = d.UnitPrice;
            r.Urgency = d.Urgency;
            r.RequiresContract = d.RequiresContract;
            r.SupplierText = d.SupplierText;
            r.NeededInDays = d.NeededInDays;
            var best = ev.Contracts.FirstOrDefault();
            r.SupplierId = best != null ? best.Supplier.Id : ev.SupplierMatch != null && ev.SupplierMatch.IsConfident ? ev.SupplierMatch.Supplier.Id : ev.Suppliers.FirstOrDefault()?.Supplier.Id;
            r.ContractId = best?.Contract.Id;
            r.EngagementRequired = ev.Engagement.Required;
            r.TriggeredRules = ev.Engagement.TriggeredIds;
            r.EngagementSummary = ev.Engagement.Decision;
            r.EmergencyPath = ev.Engagement.EmergencyPath;
            r.AssignedManagerId = ev.Routing.Manager?.Id;
            r.RecommendationText = ev.Recommendation.Headline;
        }

        public RequestDraft DraftFrom(ProcurementRequest r) => new RequestDraft
        {
            Requester = r.Requester,
            CompanyCode = r.CompanyCode,
            LocationId = r.LocationId,
            Description = r.Description,
            SubcategoryOverride = r.CategoryOverridden ? r.SubcategoryCode : null,
            Quantity = r.Quantity,
            UnitPrice = r.UnitPrice,
            Urgency = r.Urgency,
            RequiresContract = r.RequiresContract,
            SupplierText = r.SupplierText,
            NeededInDays = r.NeededInDays,
            ExcludeRequestId = r.Id,
        };

        public Evaluation Reevaluate(ProcurementRequest r) => engine.Evaluate(DraftFrom(r));

        public RequestStage? NextStage(ProcurementRequest r)
        {
            if (!r.IsOpen) return null;
            var path = PathFor(r);
            int i = Array.IndexOf(path, r.Stage);
            if (i < 0) return path.Length > 1 ? path[1] : (RequestStage?)null;
            return i + 1 < path.Length ? path[i + 1] : (RequestStage?)null;
        }

        public static string ActionLabel(RequestStage next)
        {
            switch (next)
            {
                case RequestStage.Routed: return "Route to category manager";
                case RequestStage.Sourcing: return "Start sourcing";
                case RequestStage.Approval: return "Submit for approval";
                case RequestStage.PurchaseOrder: return "Approve & issue PO";
                case RequestStage.Received: return "Record goods receipt";
                case RequestStage.Invoiced: return "Match invoice (3-way)";
                case RequestStage.Closed: return "Close request";
                default: return "Advance";
            }
        }

        public void Advance(ProcurementRequest r, string actor, DateTime at)
        {
            var next = NextStage(r);
            if (next == null) return;
            var mgr = db.PersonOf(r.AssignedManagerId);
            string note;
            switch (next.Value)
            {
                case RequestStage.Routed: note = "Routed to " + (mgr?.Name ?? "category manager") + "."; break;
                case RequestStage.Sourcing:
                    note = r.ContractId != null ? "Sourcing under existing agreement " + r.ContractId + "." : "Competitive sourcing started; RFQ sent to 3 suppliers.";
                    break;
                case RequestStage.Approval: note = "Supplier selected (" + (db.SupplierOf(r.SupplierId)?.Name ?? "TBD") + "); submitted for approval."; break;
                case RequestStage.PurchaseOrder:
                    r.PoNumber = r.PoNumber ?? NewPo(r);
                    note = "Approved. " + r.PoNumber + " issued in IFS.";
                    break;
                case RequestStage.Received: note = "Goods/services received and confirmed by site."; break;
                case RequestStage.Invoiced: note = "Invoice matched to PO and receipt (3-way match passed)."; break;
                default: note = "Request closed."; break;
            }
            r.Stage = next.Value;
            Log(r, next.Value, actor, note, at);
        }

        public void Reject(ProcurementRequest r, string actor, string reason, DateTime at)
        {
            r.Stage = RequestStage.Rejected;
            Log(r, RequestStage.Rejected, actor, "Rejected: " + reason, at);
        }

        public void CompletePostReview(ProcurementRequest r, string actor, DateTime at)
        {
            r.PostReviewDone = true;
            Log(r, r.Stage, actor, "Emergency post-review completed; purchase ratified.", at);
        }

        public void SetRecommendation(ProcurementRequest r, bool accepted, string actor, string reason, DateTime at)
        {
            r.RecommendationState = accepted ? 1 : 2;
            r.OverrideReason = accepted ? null : reason;
            Log(r, r.Stage, actor, accepted ? "Accepted assistant recommendation." : "Overrode assistant recommendation: " + reason, at);
        }

        /// <summary>Re-applies the current policy to open requests; returns how many changed engagement status.</summary>
        public int ReapplyPolicy(string actor, DateTime at)
        {
            int changed = 0;
            foreach (var r in db.Requests.Where(x => x.IsOpen && (x.Stage == RequestStage.Intake || x.Stage == RequestStage.Routed)))
            {
                var ev = Reevaluate(r);
                bool before = r.EngagementRequired;
                r.EngagementRequired = ev.Engagement.Required;
                r.TriggeredRules = ev.Engagement.TriggeredIds;
                r.EngagementSummary = ev.Engagement.Decision;
                if (before != r.EngagementRequired)
                {
                    changed++;
                    Log(r, r.Stage, actor, "Policy change re-evaluated: " + ev.Engagement.Decision + ".", at);
                }
            }
            return changed;
        }

        int poCounter = 52000;
        string NewPo(ProcurementRequest r) => "PO-" + r.CompanyCode + "-" + poCounter++;

        static void Log(ProcurementRequest r, RequestStage stage, string actor, string note, DateTime at) =>
            r.Events.Add(new RequestEvent { AtTicks = at.Ticks, Stage = stage, Actor = actor, Note = note });
    }

    /// <summary>Creates the in-flight request backlog so the queue and dashboards have history on first launch.</summary>
    public static class RequestSeeder
    {
        static readonly string[] NewSupplierNames =
        {
            "Coastline Hydraulics LLC", "QuickFix Crane Repair", "Delmar Industrial Group", "Bay Area Lift Rentals", "Apex Port Solutions",
        };

        public static void Seed(HubDatabase db, DecisionEngine engine, Workflow workflow)
        {
            var rng = new Rng(SyntheticDataGenerator.Seed + 7);
            var requesters = new[] { "Chris Nakamura", "Dana Brooks", "Luis Ortega", "Maria Santos", "Kevin O'Neill", "Aisha Patel", "Ben Carter", "Sofia Ramirez" };
            var weightedSites = db.Locations.Where(l => l.Type != SiteType.CorporateOffice).ToList();
            var list = new List<ProcurementRequest>();

            for (int i = 0; i < 96; i++)
            {
                var loc = rng.Weighted(weightedSites, l => l.Type == SiteType.ContainerTerminal ? 3 : l.Type == SiteType.IntermodalRamp ? 2 : l.Type == SiteType.ConventionalCargo ? 1.6 : 0.8);
                var profile = DemandProfiles.For(loc.Type).ToList();
                var pick = rng.Weighted(profile, kv => Math.Sqrt(kv.Value) + (db.Sub(kv.Key).Capex ? 0.12 : 0));
                var sub = db.Sub(pick.Key);
                var item = rng.Weighted(sub.Items, it => it.Weight);
                int qty = rng.Skewed(item.MinQty, item.MaxQty, 2.2);
                double unit = Math.Round(item.BasePrice * rng.Range(0.92, 1.3));
                var urgency = rng.Chance(0.08) ? Urgency.Emergency : rng.Chance(0.24) ? Urgency.Priority : Urgency.Routine;

                string supplierText = "";
                double r = rng.Next();
                var contract = db.CoveringContract(loc, sub.Code, db.Today);
                if (r < 0.45 && contract != null)
                {
                    var rec = db.VendorRecords.Where(v => v.SupplierId == contract.SupplierId).ToList();
                    supplierText = rec.Count > 0 && rng.Chance(0.6) ? rng.Pick(rec).RawName : db.SupplierOf(contract.SupplierId).Name;
                }
                else if (r < 0.72)
                {
                    var servers = db.Suppliers.Where(s => s.Serves(sub.Code)).ToList();
                    if (servers.Count > 0) supplierText = rng.Pick(servers).Name;
                }
                else if (r < 0.78) supplierText = rng.Pick(NewSupplierNames);

                bool requiresContract = (sub.Code == "CAP-SVC" || sub.Code == "BOP-SW" || sub.Code == "BOP-PRO" || sub.Code == "INF-CON" || sub.Code == "WFS-BEN")
                    ? rng.Chance(0.6) : rng.Chance(0.03);

                var draft = new RequestDraft
                {
                    Requester = rng.Pick(requesters),
                    CompanyCode = loc.CompanyCode,
                    LocationId = loc.Id,
                    Description = item.Name + SeedContext(sub.Code, urgency, rng),
                    Quantity = qty,
                    UnitPrice = unit,
                    Urgency = urgency,
                    RequiresContract = requiresContract,
                    SupplierText = supplierText,
                    NeededInDays = urgency == Urgency.Emergency ? 1 : rng.Pick(new[] { 7, 14, 30, 60 }),
                };
                var ev = engine.Evaluate(draft);
                if (ev.Sub == null) continue;

                double ageDays = Math.Pow(rng.Next(), 1.5) * 60;
                var created = db.Today.AddHours(15).AddDays(-ageDays).AddMinutes(-rng.Range(0, 600));
                var req = new ProcurementRequest { CreatedTicks = created.Ticks, Requester = draft.Requester };
                workflow.Apply(req, draft, ev);
                req.Stage = RequestStage.Intake;
                req.Events.Add(new RequestEvent { AtTicks = created.Ticks, Stage = RequestStage.Intake, Actor = draft.Requester, Note = "Submitted via intake. " + ev.Engagement.Decision + "." });
                Progress(db, workflow, req, created, rng);
                list.Add(req);
            }

            list.Sort((a, b) => b.CreatedTicks.CompareTo(a.CreatedTicks));
            int n = 10400;
            foreach (var req in list.AsEnumerable().Reverse()) req.Id = "REQ-" + n++;
            db.Requests.AddRange(list);
        }

        static string SeedContext(string subCode, Urgency urgency, Rng rng)
        {
            int n = rng.Range(1, 40);
            if (urgency == Urgency.Emergency)
                return rng.Pick(new[] { " - unit down, vessel working", " - STS-" + (n % 12 + 1).ToString("00") + " out of service", " - safety-critical repair", " - TH-" + n.ToString("00") + " down" });
            switch (subCode)
            {
                case "CAP-RNT": return rng.Pick(new[] { " for peak-season surge", " while TH-" + n.ToString("00") + " is rebuilt", " for project cargo" });
                case "CAP-SVC": return rng.Pick(new[] { " for peak season", " for weekend vessel windows", " - 6 month service agreement" });
                case "BOP-SW": return rng.Pick(new[] { " - renewal", " - new subscription for maintenance team", "" });
                case "MRO-CP": return rng.Pick(new[] { " for STS-" + (n % 12 + 1).ToString("00"), " for RTG-" + n.ToString("00"), " - PM backlog" });
                case "MRO-WR": return rng.Pick(new[] { " for STS-" + (n % 12 + 1).ToString("00") + " main hoist", " - scheduled rope change" });
                case "YRD-HOS": return rng.Pick(new[] { " - fleet replacement", " - electrification pilot", " - capacity expansion" });
                case "INF-CON": return rng.Pick(new[] { " - phase 2", " - storm damage", " - capital plan" });
                default: return rng.Pick(new[] { "", " - replenishment", " - site request" });
            }
        }

        static void Progress(HubDatabase db, Workflow wf, ProcurementRequest r, DateTime created, Rng rng)
        {
            var now = db.Today.AddHours(16);
            var t = created;
            var mgr = db.PersonOf(r.AssignedManagerId);
            string mgrName = mgr?.Name ?? "Procurement";

            if (r.EngagementRequired && !r.EmergencyPath)
            {
                t = t.AddMinutes(1);
                r.Stage = RequestStage.Routed;
                r.Events.Add(new RequestEvent { AtTicks = t.Ticks, Stage = RequestStage.Routed, Actor = "Routing engine", Note = "Routed to " + mgrName + "." });
            }
            else
            {
                t = t.AddMinutes(1);
                r.Stage = RequestStage.PurchaseOrder;
                r.PoNumber = "PO-" + r.CompanyCode + "-" + rng.Range(41000, 49999);
                r.Events.Add(new RequestEvent
                {
                    AtTicks = t.Ticks, Stage = RequestStage.PurchaseOrder, Actor = "Workflow",
                    Note = (r.EmergencyPath ? "Emergency path: PO issued immediately. " : "Self-serve: catalog PO issued automatically. ") + r.PoNumber,
                });
            }

            if (rng.Chance(0.8))
            {
                r.RecommendationState = rng.Chance(0.86) ? 1 : 2;
                if (r.RecommendationState == 2) r.OverrideReason = rng.Pick(new[] { "Site needs OEM part - contract supplier lead time too long", "Local supplier already mobilized", "Budget owner prefers competitive bid" });
            }

            while (r.IsOpen)
            {
                var next = wf.NextStage(r);
                if (next == null) break;
                double days;
                switch (next.Value)
                {
                    case RequestStage.Sourcing: days = rng.Range(0.3, 3); break;
                    case RequestStage.Approval: days = rng.Range(1, 8); break;
                    case RequestStage.PurchaseOrder: days = rng.Range(0.5, 4); break;
                    case RequestStage.Received: days = r.Urgency == Urgency.Emergency ? rng.Range(0.2, 2) : rng.Range(3, 25); break;
                    case RequestStage.Invoiced: days = rng.Range(2, 12); break;
                    case RequestStage.Closed: days = rng.Range(1, 6); break;
                    default: days = rng.Range(0.1, 1); break;
                }
                var at = t.AddDays(days);
                if (at > now) break;
                if (next.Value == RequestStage.PurchaseOrder && rng.Chance(0.04))
                {
                    wf.Reject(r, mgrName, rng.Pick(new[] { "Existing stock available at sister terminal", "Duplicate of an open request", "Deferred to next budget cycle" }), at);
                    break;
                }
                string actor = next.Value == RequestStage.Received ? r.Requester : next.Value == RequestStage.Invoiced ? "AP automation" : mgrName;
                wf.Advance(r, actor, at);
                t = at;
            }

            if (r.EmergencyPath && (now - created).TotalHours > 30 && rng.Chance(0.7))
            {
                r.PostReviewDone = true;
                var at = created.AddHours(rng.Range(6, 40));
                if (at < now) r.Events.Add(new RequestEvent { AtTicks = at.Ticks, Stage = r.Stage, Actor = mgrName, Note = "Emergency post-review completed; purchase ratified." });
                r.Events.Sort((a, b) => a.AtTicks.CompareTo(b.AtTicks));
            }
        }
    }
}
