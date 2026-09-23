using System;
using System.Collections.Generic;

namespace ProcurementHub
{
    public enum BusinessUnit { Corporate, SSAMarine, RMS, Tideworks }

    public enum SiteType
    {
        CorporateOffice, ContainerTerminal, ConventionalCargo, CruiseTerminal,
        IntermodalRamp, SwitchingOperation, TrailerRepair, AutoFacility, TechnologyOffice
    }

    public enum EquipmentClass
    {
        StsCrane, YardCrane, MobileHarborCrane, ContainerHandler, Forklift,
        Hostler, Chassis, Bombcart, Locomotive, ServiceTruck
    }

    public enum ContractScope { National, BusinessUnit, Company, Location }

    public enum SourceSystem { IfsErp, LegacyAp, SiteSpreadsheet, PCard }

    public enum Urgency { Routine, Priority, Emergency }

    public enum RequestStage
    {
        Intake, Routed, Sourcing, Approval, PurchaseOrder, Received, Invoiced, Closed, Rejected
    }

    public enum Severity { Info, Warning, Serious, Critical }

    public enum AlertType
    {
        DuplicateSupplier, OffContractSpend, PriceVariance, MatchException, EmergencyReview,
        ContractExpiring, ExpiredContractSpend, SplitPurchase, UnclassifiedSpend, NoPurchaseOrder
    }

    public enum AlertStatus { Open, Acknowledged, Resolved }

    public sealed class Company
    {
        public string Code;
        public string Name;
        public string ShortName;
        public BusinessUnit Unit;
        public DateTime ErpGoLive;
        public bool ErpLiveOn(DateTime d) => d >= ErpGoLive;
    }

    public sealed class Location
    {
        public string Id;
        public string Name;
        public string City;
        public string Country;
        public string CompanyCode;
        public Company Company;
        public SiteType Type;
        public float Lat;
        public float Lon;
        public readonly Dictionary<EquipmentClass, int> Fleet = new Dictionary<EquipmentClass, int>();
        public BusinessUnit Unit => Company.Unit;
        public int FleetCount(EquipmentClass c) => Fleet.TryGetValue(c, out var n) ? n : 0;
    }

    public sealed class CatalogItem
    {
        public string Name;
        public double BasePrice;
        public string Uom;
        public int MinQty;
        public int MaxQty;
        public float Weight = 1f;
        public string SubcategoryCode;
    }

    public sealed class Category
    {
        public string Code;
        public string Name;
        public string ManagerId;
        public string Examples;
        public readonly List<Subcategory> Subcategories = new List<Subcategory>();
    }

    public sealed class Subcategory
    {
        public string Code;
        public string Name;
        public string CategoryCode;
        public Category Category;
        public string[] Keywords;
        public bool Capex;
        public readonly List<CatalogItem> Items = new List<CatalogItem>();
    }

    public sealed class Person
    {
        public string Id;
        public string Name;
        public string Title;
        public string Email;
    }

    public sealed class Supplier
    {
        public string Id;
        public string Name;
        public string City;
        public string Country;
        public float Lat;
        public float Lon;
        public readonly List<string> SubcategoryCodes = new List<string>();
        public float Otif;
        public float Quality;
        public float Responsiveness;
        public string Risk;
        public DateTime Since;
        public float Score => (Otif * 0.4f + Quality * 0.4f + Responsiveness * 0.2f);
        public bool Serves(string subcategoryCode) => SubcategoryCodes.Contains(subcategoryCode);
    }

    /// <summary>A supplier as it exists in one entity's vendor master (legacy or ERP). One real
    /// supplier can have many of these with different spellings, IDs and remit addresses.</summary>
    public sealed class VendorRecord
    {
        public string Id;
        public string CompanyCode;
        public string RawName;
        public string SupplierId;
        public SourceSystem Source;
        public DateTime Created;
    }

    public sealed class PriceItem
    {
        public string Item;
        public double UnitPrice;
        public string Uom;
    }

    public sealed class Contract
    {
        public string Id;
        public string Title;
        public string SupplierId;
        public string SubcategoryCode;
        public ContractScope Scope;
        public string ScopeRef;
        public DateTime Start;
        public DateTime End;
        public double AnnualCommit;
        public float Discount;
        public string Terms;
        public readonly List<PriceItem> PriceList = new List<PriceItem>();
        public bool IsActive(DateTime d) => d >= Start && d <= End;

        public double? PriceFor(string item)
        {
            foreach (var p in PriceList)
                if (p.Item == item) return p.UnitPrice;
            return null;
        }
    }

    public sealed class PurchaseLine
    {
        public int Id;
        public DateTime Date;
        public string CompanyCode;
        public string LocationId;
        public string VendorRecordId;
        public string SupplierId;
        public string SubcategoryCode;
        /// <summary>Category code as recorded in the source system. "UNCL" when the source left it blank.</summary>
        public string SourceCategoryCode;
        public string Item;
        public string Description;
        public double Qty;
        public double UnitPrice;
        public double Amount;
        public string PoNumber;
        public string ContractId;
        /// <summary>An active contract covered this purchase (whether or not it was used).</summary>
        public string CoveringContractId;
        public double InvoiceAmount;
        public double ReceivedQty;
        public bool Emergency;
        public SourceSystem Source;
        public double CatalogPrice;

        public bool OnContract => ContractId != null;
        public bool HasPo => !string.IsNullOrEmpty(PoNumber);
        public bool MatchException =>
            HasPo && (Math.Abs(InvoiceAmount - Amount) > Math.Max(50.0, Amount * 0.02) || Math.Abs(ReceivedQty - Qty) > 0.001);
    }

    [Serializable]
    public sealed class RequestEvent
    {
        public long AtTicks;
        public RequestStage Stage;
        public string Actor;
        public string Note;
        public DateTime At => new DateTime(AtTicks);
    }

    [Serializable]
    public sealed class ProcurementRequest
    {
        public string Id;
        public long CreatedTicks;
        public string Requester;
        public string CompanyCode;
        public string LocationId;
        public string Description;
        public string SubcategoryCode;
        public float ClassificationConfidence;
        public bool CategoryOverridden;
        public double Quantity;
        public double UnitPrice;
        public Urgency Urgency;
        public bool RequiresContract;
        public string SupplierText;
        public string SupplierId;
        public string ContractId;
        public int NeededInDays;
        public RequestStage Stage;
        public bool EngagementRequired;
        public string TriggeredRules;
        public string EngagementSummary;
        public bool EmergencyPath;
        public bool PostReviewDone;
        public string AssignedManagerId;
        public string PoNumber;
        public string RecommendationText;
        public int RecommendationState; // 0 pending, 1 accepted, 2 overridden
        public string OverrideReason;
        public bool UserCreated;
        public List<RequestEvent> Events = new List<RequestEvent>();

        public DateTime Created => new DateTime(CreatedTicks);
        public double Total => Quantity * UnitPrice;
        public bool IsOpen => Stage != RequestStage.Closed && Stage != RequestStage.Rejected;

        public DateTime StageEntered
        {
            get
            {
                for (int i = Events.Count - 1; i >= 0; i--)
                    if (Events[i].Stage == Stage) return Events[i].At;
                return Created;
            }
        }
    }

    public sealed class Alert
    {
        public string Id;
        public AlertType Type;
        public Severity Severity;
        public string Title;
        public string Detail;
        public string Recommendation;
        public string LocationId;
        public string SupplierId;
        public string ContractId;
        public string RequestId;
        public double Impact;
        public DateTime Raised;
        public AlertStatus Status;
    }

    public static class Labels
    {
        public static string Unit(BusinessUnit u)
        {
            switch (u)
            {
                case BusinessUnit.SSAMarine: return "SSA Marine";
                case BusinessUnit.RMS: return "Rail Management Services";
                case BusinessUnit.Tideworks: return "Tideworks Technology";
                default: return "Carrix Corporate";
            }
        }

        public static string UnitShort(BusinessUnit u)
        {
            switch (u)
            {
                case BusinessUnit.SSAMarine: return "SSA Marine";
                case BusinessUnit.RMS: return "RMS";
                case BusinessUnit.Tideworks: return "Tideworks";
                default: return "Corporate";
            }
        }

        public static string Site(SiteType t)
        {
            switch (t)
            {
                case SiteType.CorporateOffice: return "Corporate office";
                case SiteType.ContainerTerminal: return "Container terminal";
                case SiteType.ConventionalCargo: return "Conventional cargo";
                case SiteType.CruiseTerminal: return "Cruise terminal";
                case SiteType.IntermodalRamp: return "Intermodal ramp";
                case SiteType.SwitchingOperation: return "Switching operation";
                case SiteType.TrailerRepair: return "Trailer repair shop";
                case SiteType.AutoFacility: return "Automotive facility";
                default: return "Technology office";
            }
        }

        public static string Equipment(EquipmentClass c)
        {
            switch (c)
            {
                case EquipmentClass.StsCrane: return "STS cranes";
                case EquipmentClass.YardCrane: return "Yard cranes (RTG/WSC)";
                case EquipmentClass.MobileHarborCrane: return "Mobile harbor cranes";
                case EquipmentClass.ContainerHandler: return "Container handlers";
                case EquipmentClass.Forklift: return "Forklifts";
                case EquipmentClass.Hostler: return "Hostlers";
                case EquipmentClass.Chassis: return "Chassis";
                case EquipmentClass.Bombcart: return "Bombcarts";
                case EquipmentClass.Locomotive: return "Switch locomotives";
                default: return "Service trucks";
            }
        }

        public static string Source(SourceSystem s)
        {
            switch (s)
            {
                case SourceSystem.IfsErp: return "IFS ERP";
                case SourceSystem.LegacyAp: return "Legacy AP";
                case SourceSystem.SiteSpreadsheet: return "Site spreadsheet";
                default: return "P-card";
            }
        }

        public static string Stage(RequestStage s)
        {
            switch (s)
            {
                case RequestStage.Intake: return "Intake";
                case RequestStage.Routed: return "Routed";
                case RequestStage.Sourcing: return "Sourcing";
                case RequestStage.Approval: return "Approval";
                case RequestStage.PurchaseOrder: return "PO issued";
                case RequestStage.Received: return "Received";
                case RequestStage.Invoiced: return "Invoice matched";
                case RequestStage.Closed: return "Closed";
                default: return "Rejected";
            }
        }

        public static string Scope(ContractScope s)
        {
            switch (s)
            {
                case ContractScope.National: return "Enterprise-wide";
                case ContractScope.BusinessUnit: return "Business unit";
                case ContractScope.Company: return "Legal entity";
                default: return "Single site";
            }
        }

        public static string Alert(AlertType t)
        {
            switch (t)
            {
                case AlertType.DuplicateSupplier: return "Duplicate supplier";
                case AlertType.OffContractSpend: return "Off-contract spend";
                case AlertType.PriceVariance: return "Unusual pricing";
                case AlertType.MatchException: return "3-way match exception";
                case AlertType.EmergencyReview: return "Emergency post-review";
                case AlertType.ContractExpiring: return "Contract expiring";
                case AlertType.ExpiredContractSpend: return "Spend on expired contract";
                case AlertType.SplitPurchase: return "Possible split purchase";
                case AlertType.UnclassifiedSpend: return "Unclassified spend";
                default: return "Purchase without PO";
            }
        }

        public static string Severity(Severity s)
        {
            switch (s)
            {
                case ProcurementHub.Severity.Critical: return "Critical";
                case ProcurementHub.Severity.Serious: return "Serious";
                case ProcurementHub.Severity.Warning: return "Warning";
                default: return "Info";
            }
        }
    }
}
