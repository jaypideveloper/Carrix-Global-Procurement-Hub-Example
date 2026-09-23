using System;
using System.Linq;
using NUnit.Framework;

namespace ProcurementHub.Tests
{
    public class ProcurementRulesTests
    {
        static readonly DateTime AsOf = new DateTime(2026, 9, 21);
        HubDatabase db;
        HubServices svc;

        [OneTimeSetUp]
        public void Setup()
        {
            db = SyntheticDataGenerator.Build(AsOf);
            svc = new HubServices(db, null);
        }

        RequestDraft Draft(string site, string text, double qty, double unit, string supplier = "", bool contract = false, Urgency urgency = Urgency.Routine) =>
            new RequestDraft { LocationId = site, CompanyCode = db.Loc(site).CompanyCode, Description = text, Quantity = qty, UnitPrice = unit, SupplierText = supplier, RequiresContract = contract, Urgency = urgency };

        // ------------------------------------------------------------ vendor normalization

        [Test]
        public void Normalizer_CollapsesAbbreviationsSuffixesAndLocations()
        {
            Assert.AreEqual(VendorNormalizer.Normalize("Pacific Crane Parts Co."), VendorNormalizer.Normalize("PAC CRANE PTS INC - TACOMA"));
            Assert.AreEqual(VendorNormalizer.Normalize("OfficePoint Supplies"), VendorNormalizer.Normalize("OfficePoint Supplies #2 (REMIT)"));
        }

        [Test]
        public void Similarity_ToleratesTyposButSeparatesDifferentSuppliers()
        {
            Assert.GreaterOrEqual(VendorNormalizer.Similarity("Pacfic Crane Parts Inc.", "Pacific Crane Parts Co."), 0.86f, "dropped letter");
            Assert.GreaterOrEqual(VendorNormalizer.Similarity("Paicfic Crane Parts", "Pacific Crane Parts Co."), 0.86f, "transposed letters");
            Assert.Less(VendorNormalizer.Similarity("Pacific Chassis Works", "Pacific Crane Parts Co."), 0.86f, "shared first word only");
            Assert.Less(VendorNormalizer.Similarity("Harbor Trailer Manufacturing", "Harborline Lubricants"), 0.86f, "shared prefix only");
        }

        [Test]
        public void Clustering_IsPreciseAgainstTheSupplierMaster()
        {
            var clusters = VendorNormalizer.Cluster(db.VendorRecords, db.Policy.SupplierMatchThreshold);
            Assert.IsNotEmpty(clusters);
            Assert.GreaterOrEqual(clusters.Average(c => c.Purity), 0.95f);
        }

        // ------------------------------------------------------------ classification

        [TestCase("Main hoist wire rope 28mm x 500m for STS-04", "MRO-WR")]
        [TestCase("Forklift rental for vessel surge", "CAP-RNT")]
        [TestCase("Battery-electric yard tractor for zero-emission pilot", "YRD-HOS")]
        [TestCase("Hi-vis vests case of 50 for cruise staff", "WFS-PPE")]
        [TestCase("Twistlock set for spreader SP-12", "MRO-CP")]
        [TestCase("Annual analytics platform subscription", "BOP-SW")]
        public void Classifier_PicksExpectedSubcategory(string text, string expected)
        {
            var top = svc.Engine.Classifier.Classify(text).FirstOrDefault();
            Assert.IsNotNull(top, "no classification");
            Assert.AreEqual(expected, top.Sub.Code, "evidence: " + string.Join(", ", top.Evidence));
        }

        // ------------------------------------------------------------ engagement rules

        [Test]
        public void Rule1_TriggersAtThreshold()
        {
            db.Policy.SplitDetection = false;
            try
            {
                var at = svc.Engine.Evaluate(Draft("CR-MIA", "Hi-vis vests case of 50", 1, db.Policy.ValueThreshold));
                var below = svc.Engine.Evaluate(Draft("CR-MIA", "Hi-vis vests case of 50", 1, db.Policy.ValueThreshold - 1));
                Assert.IsTrue(at.Engagement.Rules.Single(r => r.Id == "R1").Triggered);
                Assert.IsFalse(below.Engagement.Rules.Single(r => r.Id == "R1").Triggered);
            }
            finally { db.Policy.SplitDetection = true; }
        }

        [Test]
        public void Rule2_TriggersOnContractFlagOrContractLanguage()
        {
            var flagged = svc.Engine.Evaluate(Draft("CT-TAC", "Contract labor surge crew", 1, 5000, contract: true));
            var worded = svc.Engine.Evaluate(Draft("TWT-SEA", "New subscription for design software seats", 1, 5000));
            var plain = svc.Engine.Evaluate(Draft("CT-TAC", "Torque wrench set", 1, 1800));
            Assert.IsTrue(flagged.Engagement.Rules.Single(r => r.Id == "R2").Triggered);
            Assert.IsTrue(worded.Engagement.Rules.Single(r => r.Id == "R2").Triggered);
            Assert.IsFalse(plain.Engagement.Rules.Single(r => r.Id == "R2").Triggered);
        }

        [Test]
        public void Rule3_DistinguishesNewApprovedAndWrongCategorySuppliers()
        {
            var unknown = svc.Engine.Evaluate(Draft("CT-OAK", "Hoist brake assembly", 1, 5000, "QuickFix Crane Repair"));
            var alias = svc.Engine.Evaluate(Draft("CT-SEA", "Twistlock set (4)", 1, 3000, "PAC CRANE PTS INC - TACOMA"));
            var wrongCat = svc.Engine.Evaluate(Draft("CT-TAC", "Main hoist wire rope 28mm x 500m", 1, 5000, "Pacific Crane Parts Co."));
            Assert.IsTrue(unknown.Engagement.Rules.Single(r => r.Id == "R3").Triggered);
            Assert.IsFalse(alias.Engagement.Rules.Single(r => r.Id == "R3").Triggered);
            Assert.AreEqual("S11", alias.SupplierMatch.Supplier.Id);
            Assert.IsTrue(wrongCat.Engagement.Rules.Single(r => r.Id == "R3").Triggered);
        }

        [Test]
        public void Emergency_ProceedsWithPostReview()
        {
            var ev = svc.Engine.Evaluate(Draft("PRS-CHI", "Hydraulic pump, top handler - unit down", 1, 30000, urgency: Urgency.Emergency));
            Assert.IsTrue(ev.Engagement.EmergencyPath);
            StringAssert.StartsWith("Emergency", ev.Engagement.Decision);
            Assert.IsTrue(ev.Routing.Approvals.Any(a => a.Role == "Post-review"));
        }

        // ------------------------------------------------------------ routing, contracts, workflow

        [Test]
        public void Routing_AssignsCategoryManagerAndCapitalCommittee()
        {
            var ev = svc.Engine.Evaluate(Draft("CT-LAX", "Battery-electric yard tractor", 5, 340000));
            Assert.AreEqual("MGR-YRD", ev.Routing.Manager.Id);
            Assert.IsTrue(ev.Routing.Approvals.Any(a => a.Role == "Director"));
            Assert.IsTrue(ev.Routing.Approvals.Any(a => a.Role == "Capital committee"));
        }

        [Test]
        public void ContractScope_BusinessUnitAgreementDoesNotCoverOtherUnits()
        {
            var ssaOnly = db.Contracts.First(k => k.Scope == ContractScope.BusinessUnit && k.ScopeRef == "SSAMarine");
            Assert.IsTrue(db.Covers(ssaOnly, db.Loc("CT-TAC")));
            Assert.IsFalse(db.Covers(ssaOnly, db.Loc("PRS-CHI")));
        }

        [Test]
        public void Workflow_SelfServeSkipsSourcing_EngagedPathRunsToClose()
        {
            db.Policy.SplitDetection = false;
            try
            {
                var small = Draft("CR-MIA", "Hi-vis vests (case of 50)", 2, 640, "Northstar Safety Supply");
                var selfServe = svc.Workflow.Submit(small, svc.Engine.Evaluate(small), "Test", true, AsOf);
                Assert.AreEqual(RequestStage.PurchaseOrder, selfServe.Stage);
                Assert.IsNotNull(selfServe.PoNumber);

                var big = Draft("CT-TAC", "Main hoist wire rope 28mm x 500m", 3, 15000, "Titan Wire Rope");
                var engaged = svc.Workflow.Submit(big, svc.Engine.Evaluate(big), "Test", false, AsOf);
                Assert.AreEqual(RequestStage.Routed, engaged.Stage);
                for (int i = 0; i < 10 && engaged.IsOpen; i++) svc.Workflow.Advance(engaged, "Test", AsOf.AddDays(i + 1));
                Assert.AreEqual(RequestStage.Closed, engaged.Stage);
                CollectionAssert.IsSubsetOf(Workflow.EngagedPath, engaged.Events.Select(e => e.Stage).Distinct().ToList());
            }
            finally { db.Policy.SplitDetection = true; }
        }

        [Test]
        public void Generator_IsDeterministic()
        {
            var again = SyntheticDataGenerator.Build(AsOf);
            Assert.AreEqual(db.Lines.Count, again.Lines.Count);
            Assert.AreEqual(db.Lines.Sum(l => l.Amount), again.Lines.Sum(l => l.Amount), 0.01);
            Assert.AreEqual(db.VendorRecords.Count, again.VendorRecords.Count);
        }
    }
}
