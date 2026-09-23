using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHub
{
    /// <summary>Composition root for the domain layer. Holds user decisions that persist between sessions.</summary>
    public sealed class HubServices
    {
        public readonly HubDatabase Db;
        public readonly DecisionEngine Engine;
        public readonly AlertEngine Alerts;
        public readonly Analytics Analytics;
        public Workflow Workflow { get; private set; }

        public readonly HashSet<string> ResolvedAlerts = new HashSet<string>();
        public readonly HashSet<string> AcknowledgedAlerts = new HashSet<string>();
        public readonly HashSet<string> ApprovedMerges = new HashSet<string>();
        public readonly HashSet<string> RejectedMerges = new HashSet<string>();
        public readonly HashSet<int> AcceptedClassifications = new HashSet<int>();
        public string ActingAsId = "USR-ANL";

        List<VendorCluster> clusters;
        public event Action Changed;

        public HubServices(HubDatabase db, IList<ProcurementRequest> persistedRequests)
        {
            Db = db;
            Engine = new DecisionEngine(db);
            Alerts = new AlertEngine(db);
            Analytics = new Analytics(db);
            if (persistedRequests != null && persistedRequests.Count > 0)
                db.Requests.AddRange(persistedRequests);
            else
                RequestSeeder.Seed(db, Engine, new Workflow(db, Engine));
            Workflow = new Workflow(db, Engine);
        }

        public Person ActingAs => Db.PersonOf(ActingAsId) ?? Db.People.First();

        public void RunAlerts() => Db.Alerts = Alerts.Run(ResolvedAlerts, AcknowledgedAlerts);

        public List<VendorCluster> VendorClusters
        {
            get
            {
                if (clusters == null) clusters = VendorNormalizer.Cluster(Db.VendorRecords, Db.Policy.SupplierMatchThreshold);
                return clusters;
            }
        }

        public IEnumerable<Alert> OpenAlerts => Db.Alerts.Where(a => a.Status != AlertStatus.Resolved);

        public void SetAlertStatus(Alert a, AlertStatus status)
        {
            a.Status = status;
            ResolvedAlerts.Remove(a.Id);
            AcknowledgedAlerts.Remove(a.Id);
            if (status == AlertStatus.Resolved) ResolvedAlerts.Add(a.Id);
            else if (status == AlertStatus.Acknowledged) AcknowledgedAlerts.Add(a.Id);
            NotifyChanged();
        }

        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>"Now" inside the synthetic timeline: today's date with the real wall-clock time.</summary>
        public DateTime Now => Db.Today.Add(DateTime.Now.TimeOfDay);
    }
}
