using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ProcurementHub.UI
{
    [Serializable]
    public sealed class HubState
    {
        public int Version = 2;
        public string AsOf;
        public List<ProcurementRequest> Requests = new List<ProcurementRequest>();
        public List<string> Resolved = new List<string>();
        public List<string> Acknowledged = new List<string>();
        public List<string> Merged = new List<string>();
        public List<string> NotMerged = new List<string>();
        public List<int> AcceptedClassifications = new List<int>();
        public string ActingAs;
        public bool HasPolicy;
        public double Threshold;
        public bool SplitDetection;
        public int SplitWindowDays;
        public bool ContractRule;
        public bool NewSupplierRule;
        public float SupplierMatchThreshold;
        public int EmergencyReviewHours;
        public float PriceTolerance;
    }

    /// <summary>Persists user work (requests, decisions, policy) as JSON between sessions.</summary>
    public sealed class HubStore
    {
        const int CurrentVersion = 2;
        public string FilePath => Path.Combine(Application.persistentDataPath, "procurement-hub-state.json");

        public HubState Load(DateTime today)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var state = JsonUtility.FromJson<HubState>(File.ReadAllText(FilePath));
                if (state == null || state.Version != CurrentVersion) return null;
                // Keep the timeline current: shift saved timestamps by the days elapsed since the last session.
                if (DateTime.TryParse(state.AsOf, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var asOf))
                {
                    long shift = (today.Date - asOf.Date).Ticks;
                    if (shift != 0)
                        foreach (var r in state.Requests)
                        {
                            r.CreatedTicks += shift;
                            foreach (var e in r.Events) e.AtTicks += shift;
                        }
                }
                return state;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Procurement Hub: could not read saved state (" + ex.Message + "). Starting fresh.");
                return null;
            }
        }

        public void Save(HubServices svc)
        {
            try
            {
                var p = svc.Db.Policy;
                var state = new HubState
                {
                    AsOf = Fmt.Iso(svc.Db.Today),
                    Requests = svc.Db.Requests,
                    Resolved = new List<string>(svc.ResolvedAlerts),
                    Acknowledged = new List<string>(svc.AcknowledgedAlerts),
                    Merged = new List<string>(svc.ApprovedMerges),
                    NotMerged = new List<string>(svc.RejectedMerges),
                    AcceptedClassifications = new List<int>(svc.AcceptedClassifications),
                    ActingAs = svc.ActingAsId,
                    HasPolicy = true,
                    Threshold = p.ValueThreshold,
                    SplitDetection = p.SplitDetection,
                    SplitWindowDays = p.SplitWindowDays,
                    ContractRule = p.ContractRule,
                    NewSupplierRule = p.NewSupplierRule,
                    SupplierMatchThreshold = p.SupplierMatchThreshold,
                    EmergencyReviewHours = p.EmergencyReviewHours,
                    PriceTolerance = p.PriceTolerance,
                };
                File.WriteAllText(FilePath, JsonUtility.ToJson(state));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Procurement Hub: could not save state (" + ex.Message + ").");
            }
        }

        public void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception ex) { Debug.LogWarning("Procurement Hub: could not delete saved state (" + ex.Message + ")."); }
        }

        public static void ApplyPolicy(HubState s, EngagementPolicy p)
        {
            if (s == null || !s.HasPolicy) return;
            p.ValueThreshold = s.Threshold;
            p.SplitDetection = s.SplitDetection;
            p.SplitWindowDays = s.SplitWindowDays;
            p.ContractRule = s.ContractRule;
            p.NewSupplierRule = s.NewSupplierRule;
            p.SupplierMatchThreshold = s.SupplierMatchThreshold;
            p.EmergencyReviewHours = s.EmergencyReviewHours;
            p.PriceTolerance = s.PriceTolerance;
        }

        public static void ApplyDecisions(HubState s, HubServices svc)
        {
            if (s == null) return;
            foreach (var x in s.Resolved) svc.ResolvedAlerts.Add(x);
            foreach (var x in s.Acknowledged) svc.AcknowledgedAlerts.Add(x);
            foreach (var x in s.Merged) svc.ApprovedMerges.Add(x);
            foreach (var x in s.NotMerged) svc.RejectedMerges.Add(x);
            foreach (var x in s.AcceptedClassifications) svc.AcceptedClassifications.Add(x);
            if (!string.IsNullOrEmpty(s.ActingAs)) svc.ActingAsId = s.ActingAs;
        }
    }
}
