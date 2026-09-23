using System;
using System.Collections.Generic;

namespace ProcurementHub
{
    /// <summary>
    /// Static reference tables for the synthetic Carrix network. Every company, site, supplier and person
    /// here is illustrative: names, locations, fleets, prices and contracts are invented for the prototype.
    /// </summary>
    public static class ReferenceData
    {
        public static List<Company> Companies(DateTime today)
        {
            return new List<Company>
            {
                C("1000", "Carrix, Inc. (Corporate)", "Carrix Corp", BusinessUnit.Corporate, today.AddDays(-300)),
                C("2100", "SSA Marine - Container Terminals", "SSA Containers", BusinessUnit.SSAMarine, today.AddDays(-270)),
                C("2200", "SSA Marine - Conventional Cargo", "SSA Conventional", BusinessUnit.SSAMarine, today.AddDays(-60)),
                C("2300", "SSA Marine - Cruise Services", "SSA Cruise", BusinessUnit.SSAMarine, today.AddDays(-60)),
                C("3100", "Pacific Rail Services", "PRS", BusinessUnit.RMS, today.AddDays(-180)),
                C("3200", "Rail Terminal Services", "RTS", BusinessUnit.RMS, today.AddDays(-180)),
                C("3300", "Terminal Switching Services", "TSS", BusinessUnit.RMS, today.AddDays(-180)),
                C("3400", "Pacific Trailer Repair Services", "PTRS", BusinessUnit.RMS, today.AddDays(-120)),
                C("3500", "PRS Auto", "PRS Auto", BusinessUnit.RMS, today.AddDays(-120)),
                C("4100", "Tideworks Technology", "Tideworks", BusinessUnit.Tideworks, today.AddDays(90)),
            };
        }

        static Company C(string code, string name, string shortName, BusinessUnit unit, DateTime goLive) =>
            new Company { Code = code, Name = name, ShortName = shortName, Unit = unit, ErpGoLive = goLive };

        // id, name, city, country, company, type, lat, lon
        static readonly object[][] SiteRows =
        {
            new object[] { "CRX-SEA", "Carrix Corporate HQ", "Seattle", "USA", "1000", SiteType.CorporateOffice, 47.604f, -122.335f },
            new object[] { "TWT-SEA", "Tideworks HQ & Engineering", "Seattle", "USA", "4100", SiteType.TechnologyOffice, 47.640f, -122.300f },
            new object[] { "TWT-CLT", "Tideworks Customer Success", "Charlotte", "USA", "4100", SiteType.TechnologyOffice, 35.227f, -80.843f },

            new object[] { "CT-SEA", "Seattle Container Terminal", "Seattle", "USA", "2100", SiteType.ContainerTerminal, 47.578f, -122.352f },
            new object[] { "CT-TAC", "Tacoma Container Terminal", "Tacoma", "USA", "2100", SiteType.ContainerTerminal, 47.268f, -122.413f },
            new object[] { "CT-OAK", "Oakland Container Terminal", "Oakland", "USA", "2100", SiteType.ContainerTerminal, 37.797f, -122.318f },
            new object[] { "CT-LGB", "Long Beach Container Terminal", "Long Beach", "USA", "2100", SiteType.ContainerTerminal, 33.754f, -118.214f },
            new object[] { "CT-LAX", "San Pedro Container Terminal", "Los Angeles", "USA", "2100", SiteType.ContainerTerminal, 33.700f, -118.290f },
            new object[] { "CT-HNL", "Honolulu Container Yard", "Honolulu", "USA", "2100", SiteType.ContainerTerminal, 21.318f, -157.878f },
            new object[] { "CT-NWK", "Port Newark Container Terminal", "Newark", "USA", "2100", SiteType.ContainerTerminal, 40.684f, -74.151f },
            new object[] { "CT-MZO", "Manzanillo Container Terminal", "Manzanillo", "Mexico", "2100", SiteType.ContainerTerminal, 19.065f, -104.300f },
            new object[] { "CT-LZC", "Lazaro Cardenas Terminal", "Lazaro Cardenas", "Mexico", "2100", SiteType.ContainerTerminal, 17.935f, -102.180f },
            new object[] { "CT-PAN", "Balboa Container Terminal", "Panama City", "Panama", "2100", SiteType.ContainerTerminal, 8.955f, -79.567f },
            new object[] { "CT-CTG", "Cartagena Container Terminal", "Cartagena", "Colombia", "2100", SiteType.ContainerTerminal, 10.395f, -75.525f },
            new object[] { "CT-VNM", "Cai Mep International Terminal", "Vung Tau", "Vietnam", "2100", SiteType.ContainerTerminal, 10.530f, 107.020f },

            new object[] { "CV-PDX", "Portland Breakbulk Terminal", "Portland", "USA", "2200", SiteType.ConventionalCargo, 45.582f, -122.722f },
            new object[] { "CV-LGV", "Longview Forest Products", "Longview", "USA", "2200", SiteType.ConventionalCargo, 46.108f, -122.948f },
            new object[] { "CV-SCK", "Stockton Bulk Terminal", "Stockton", "USA", "2200", SiteType.ConventionalCargo, 37.948f, -121.335f },
            new object[] { "CV-RCH", "Richmond Auto & Breakbulk", "Richmond", "USA", "2200", SiteType.ConventionalCargo, 37.918f, -122.372f },
            new object[] { "CV-HUE", "Port Hueneme Vehicle Processing", "Port Hueneme", "USA", "2200", SiteType.ConventionalCargo, 34.148f, -119.206f },
            new object[] { "CV-HOU", "Houston Steel & Project Cargo", "Houston", "USA", "2200", SiteType.ConventionalCargo, 29.731f, -95.268f },
            new object[] { "CV-GLS", "Galveston Grain & Breakbulk", "Galveston", "USA", "2200", SiteType.ConventionalCargo, 29.309f, -94.791f },
            new object[] { "CV-MSY", "New Orleans Steel Terminal", "New Orleans", "USA", "2200", SiteType.ConventionalCargo, 29.925f, -90.061f },
            new object[] { "CV-MOB", "Mobile Forest Products", "Mobile", "USA", "2200", SiteType.ConventionalCargo, 30.692f, -88.040f },
            new object[] { "CV-BAL", "Baltimore Ro-Ro & Breakbulk", "Baltimore", "USA", "2200", SiteType.ConventionalCargo, 39.262f, -76.578f },
            new object[] { "CV-PHL", "Philadelphia Project Cargo", "Philadelphia", "USA", "2200", SiteType.ConventionalCargo, 39.898f, -75.137f },
            new object[] { "CV-SAV", "Savannah Breakbulk Terminal", "Savannah", "USA", "2200", SiteType.ConventionalCargo, 32.118f, -81.139f },
            new object[] { "CV-CHS", "Charleston Breakbulk Terminal", "Charleston", "USA", "2200", SiteType.ConventionalCargo, 32.838f, -79.925f },
            new object[] { "CV-JAX", "Jacksonville Vehicle Terminal", "Jacksonville", "USA", "2200", SiteType.ConventionalCargo, 30.402f, -81.552f },
            new object[] { "CV-VER", "Veracruz Multipurpose Terminal", "Veracruz", "Mexico", "2200", SiteType.ConventionalCargo, 19.205f, -96.130f },
            new object[] { "CV-ALT", "Altamira Bulk Terminal", "Altamira", "Mexico", "2200", SiteType.ConventionalCargo, 22.488f, -97.868f },

            new object[] { "CR-SEA", "Seattle Cruise Terminal", "Seattle", "USA", "2300", SiteType.CruiseTerminal, 47.630f, -122.385f },
            new object[] { "CR-JNU", "Juneau Cruise Services", "Juneau", "USA", "2300", SiteType.CruiseTerminal, 58.298f, -134.408f },
            new object[] { "CR-KTN", "Ketchikan Cruise Services", "Ketchikan", "USA", "2300", SiteType.CruiseTerminal, 55.342f, -131.645f },
            new object[] { "CR-SGY", "Skagway Cruise Services", "Skagway", "USA", "2300", SiteType.CruiseTerminal, 59.452f, -135.323f },
            new object[] { "CR-SFO", "San Francisco Cruise Terminal", "San Francisco", "USA", "2300", SiteType.CruiseTerminal, 37.803f, -122.401f },
            new object[] { "CR-HNL", "Honolulu Cruise Terminal", "Honolulu", "USA", "2300", SiteType.CruiseTerminal, 21.290f, -157.850f },
            new object[] { "CR-MIA", "Miami Cruise Services", "Miami", "USA", "2300", SiteType.CruiseTerminal, 25.778f, -80.170f },
            new object[] { "CR-FLL", "Port Everglades Cruise Services", "Fort Lauderdale", "USA", "2300", SiteType.CruiseTerminal, 26.093f, -80.121f },
            new object[] { "CR-GLS", "Galveston Cruise Services", "Galveston", "USA", "2300", SiteType.CruiseTerminal, 29.330f, -94.820f },
            new object[] { "CR-MSY", "New Orleans Cruise Services", "New Orleans", "USA", "2300", SiteType.CruiseTerminal, 29.948f, -90.030f },
            new object[] { "CR-CZM", "Cozumel Cruise Pier", "Cozumel", "Mexico", "2300", SiteType.CruiseTerminal, 20.508f, -86.948f },
            new object[] { "CR-ENS", "Ensenada Cruise Port", "Ensenada", "Mexico", "2300", SiteType.CruiseTerminal, 31.853f, -116.628f },

            new object[] { "PRS-CHI", "Chicago Intermodal Ramp", "Chicago", "USA", "3100", SiteType.IntermodalRamp, 41.781f, -87.720f },
            new object[] { "PRS-JOL", "Joliet Intermodal Ramp", "Joliet", "USA", "3100", SiteType.IntermodalRamp, 41.472f, -88.082f },
            new object[] { "PRS-KCK", "Kansas City Intermodal Ramp", "Kansas City", "USA", "3100", SiteType.IntermodalRamp, 39.105f, -94.585f },
            new object[] { "PRS-MEM", "Memphis Intermodal Ramp", "Memphis", "USA", "3100", SiteType.IntermodalRamp, 35.048f, -90.023f },
            new object[] { "PRS-DFW", "Alliance Intermodal Ramp", "Fort Worth", "USA", "3100", SiteType.IntermodalRamp, 32.985f, -97.318f },
            new object[] { "PRS-LAX", "Commerce Intermodal Ramp", "Los Angeles", "USA", "3100", SiteType.IntermodalRamp, 34.002f, -118.160f },
            new object[] { "PRS-SBD", "San Bernardino Intermodal Ramp", "San Bernardino", "USA", "3100", SiteType.IntermodalRamp, 34.102f, -117.282f },
            new object[] { "PRS-SEA", "Seattle Intermodal Ramp", "Seattle", "USA", "3100", SiteType.IntermodalRamp, 47.540f, -122.310f },
            new object[] { "PRS-PDX", "Portland Intermodal Ramp", "Portland", "USA", "3100", SiteType.IntermodalRamp, 45.552f, -122.690f },
            new object[] { "PRS-DEN", "Denver Intermodal Ramp", "Denver", "USA", "3100", SiteType.IntermodalRamp, 39.782f, -104.968f },
            new object[] { "PRS-ATL", "Atlanta Intermodal Ramp", "Atlanta", "USA", "3100", SiteType.IntermodalRamp, 33.752f, -84.418f },
            new object[] { "PRS-CMH", "Columbus Intermodal Ramp", "Columbus", "USA", "3100", SiteType.IntermodalRamp, 39.868f, -82.948f },
            new object[] { "PRS-HAR", "Harrisburg Intermodal Ramp", "Harrisburg", "USA", "3100", SiteType.IntermodalRamp, 40.252f, -76.842f },
            new object[] { "PRS-SLC", "Salt Lake City Intermodal Ramp", "Salt Lake City", "USA", "3100", SiteType.IntermodalRamp, 40.772f, -111.968f },

            new object[] { "RTS-ELP", "El Paso Rail Terminal", "El Paso", "USA", "3200", SiteType.IntermodalRamp, 31.772f, -106.438f },
            new object[] { "RTS-LRD", "Laredo Rail Terminal", "Laredo", "USA", "3200", SiteType.IntermodalRamp, 27.532f, -99.502f },
            new object[] { "RTS-HOU", "Houston Rail Terminal", "Houston", "USA", "3200", SiteType.IntermodalRamp, 29.802f, -95.302f },
            new object[] { "RTS-STL", "St. Louis Rail Terminal", "St. Louis", "USA", "3200", SiteType.IntermodalRamp, 38.632f, -90.202f },
            new object[] { "RTS-MSP", "Minneapolis Rail Terminal", "Minneapolis", "USA", "3200", SiteType.IntermodalRamp, 44.982f, -93.268f },

            new object[] { "TSS-LGB", "Long Beach Terminal Switching", "Long Beach", "USA", "3300", SiteType.SwitchingOperation, 33.800f, -118.240f },
            new object[] { "TSS-CHI", "Chicago Terminal Switching", "Chicago", "USA", "3300", SiteType.SwitchingOperation, 41.852f, -87.652f },
            new object[] { "TSS-HOU", "Houston Terminal Switching", "Houston", "USA", "3300", SiteType.SwitchingOperation, 29.762f, -95.252f },

            new object[] { "PTRS-CHI", "Chicago Trailer Repair", "Chicago", "USA", "3400", SiteType.TrailerRepair, 41.742f, -87.762f },
            new object[] { "PTRS-LAX", "Los Angeles Trailer Repair", "Los Angeles", "USA", "3400", SiteType.TrailerRepair, 33.962f, -118.202f },
            new object[] { "PTRS-DFW", "Dallas Trailer Repair", "Dallas", "USA", "3400", SiteType.TrailerRepair, 32.772f, -96.852f },
            new object[] { "PTRS-MEM", "Memphis Trailer Repair", "Memphis", "USA", "3400", SiteType.TrailerRepair, 35.082f, -89.982f },

            new object[] { "AUTO-KC", "Kansas City Auto Facility", "Kansas City", "USA", "3500", SiteType.AutoFacility, 39.142f, -94.552f },
            new object[] { "AUTO-DET", "Detroit Auto Facility", "Detroit", "USA", "3500", SiteType.AutoFacility, 42.332f, -83.048f },
            new object[] { "AUTO-MIR", "Mira Loma Auto Facility", "Mira Loma", "USA", "3500", SiteType.AutoFacility, 34.002f, -117.522f },
            new object[] { "AUTO-ATL", "Atlanta Auto Facility", "Atlanta", "USA", "3500", SiteType.AutoFacility, 33.702f, -84.452f },
        };

        public static List<Location> Locations(Dictionary<string, Company> companies, Rng rng)
        {
            var list = new List<Location>();
            foreach (var r in SiteRows)
            {
                var loc = new Location
                {
                    Id = (string)r[0], Name = (string)r[1], City = (string)r[2], Country = (string)r[3],
                    CompanyCode = (string)r[4], Type = (SiteType)r[5], Lat = (float)r[6], Lon = (float)r[7]
                };
                loc.Company = companies[loc.CompanyCode];
                GenerateFleet(loc, rng);
                list.Add(loc);
            }
            return list;
        }

        static void GenerateFleet(Location l, Rng rng)
        {
            void F(EquipmentClass c, int min, int max) { int n = rng.Range(min, max); if (n > 0) l.Fleet[c] = n; }
            switch (l.Type)
            {
                case SiteType.ContainerTerminal:
                    F(EquipmentClass.StsCrane, 3, 12); F(EquipmentClass.YardCrane, 8, 36); F(EquipmentClass.ContainerHandler, 6, 22);
                    F(EquipmentClass.Hostler, 30, 95); F(EquipmentClass.Forklift, 6, 18); F(EquipmentClass.Chassis, 150, 700);
                    F(EquipmentClass.ServiceTruck, 4, 12);
                    break;
                case SiteType.ConventionalCargo:
                    F(EquipmentClass.MobileHarborCrane, 1, 3); F(EquipmentClass.Forklift, 14, 40); F(EquipmentClass.ContainerHandler, 0, 4);
                    F(EquipmentClass.Hostler, 6, 22); F(EquipmentClass.Bombcart, 8, 40); F(EquipmentClass.ServiceTruck, 3, 8);
                    break;
                case SiteType.CruiseTerminal:
                    F(EquipmentClass.Forklift, 3, 10); F(EquipmentClass.ServiceTruck, 1, 4);
                    break;
                case SiteType.IntermodalRamp:
                    F(EquipmentClass.YardCrane, 0, 4); F(EquipmentClass.ContainerHandler, 4, 14); F(EquipmentClass.Hostler, 18, 60);
                    F(EquipmentClass.ServiceTruck, 3, 8);
                    break;
                case SiteType.SwitchingOperation:
                    F(EquipmentClass.Locomotive, 2, 8); F(EquipmentClass.Hostler, 0, 6); F(EquipmentClass.ServiceTruck, 2, 5);
                    break;
                case SiteType.TrailerRepair:
                    F(EquipmentClass.ServiceTruck, 6, 16); F(EquipmentClass.Forklift, 2, 5);
                    break;
                case SiteType.AutoFacility:
                    F(EquipmentClass.ServiceTruck, 4, 10); F(EquipmentClass.Forklift, 2, 6); F(EquipmentClass.Hostler, 2, 8);
                    break;
            }
        }

        public static List<Person> People()
        {
            return new List<Person>
            {
                P("MGR-HEQ", "Marcus Delgado", "Category Manager, Heavy Equipment"),
                P("MGR-YRD", "Priya Raman", "Category Manager, Yard Movement"),
                P("MGR-MRO", "Tom Okafor", "Category Manager, Maintenance & MRO"),
                P("MGR-INF", "Elena Vasquez", "Category Manager, Infrastructure"),
                P("MGR-WFS", "Grace Liu", "Category Manager, Workforce & Safety"),
                P("MGR-BOP", "Samir Haddad", "Category Manager, Business Operations"),
                P("MGR-CAP", "Jordan Pike", "Category Manager, Capacity Flexibility"),
                P("DIR-GP", "Alex Morgan", "Director, Global Procurement"),
                P("VP-FIN", "Renee Castillo", "VP Finance, Capital Committee"),
                P("USR-ANL", "Taylor Reed", "Procurement Data Analyst"),
                P("USR-TAC", "Chris Nakamura", "Terminal Manager, Tacoma"),
                P("USR-CHI", "Dana Brooks", "Ramp Manager, Chicago (PRS)"),
                P("USR-MZO", "Luis Ortega", "Maintenance Superintendent, Manzanillo"),
            };
        }

        static Person P(string id, string name, string title)
        {
            var parts = name.ToLowerInvariant().Split(' ');
            return new Person { Id = id, Name = name, Title = title, Email = parts[0][0] + parts[parts.Length - 1] + "@example.com" };
        }

        static CatalogItem I(string name, double price, string uom, int minQty, int maxQty, float weight = 1f) =>
            new CatalogItem { Name = name, BasePrice = price, Uom = uom, MinQty = minQty, MaxQty = maxQty, Weight = weight };

        public static List<Category> Categories()
        {
            var cats = new List<Category>();

            var heq = Cat("HEQ", "Heavy Equipment", "MGR-HEQ", "Cranes, container handlers, forklifts");
            Sub(heq, "HEQ-CRN", "STS & Yard Cranes", true,
                new[] { "gantry crane:2.5", "ship-to-shore:2.5", "sts crane:1.6", "rtg:2", "rubber-tyred:2", "rubber tyred:2", "new crane:2", "crane modernization:2.5", "crane electrical:1.5", "spreader:1.2", "twin-lift:1.5", "crane purchase:2.5", "capital crane:2" },
                I("Ship-to-shore gantry crane (new build)", 11500000, "ea", 1, 1, 0.08f),
                I("Rubber-tyred gantry (RTG) crane", 2400000, "ea", 1, 2, 0.35f),
                I("Telescopic twin-lift spreader", 385000, "ea", 1, 2, 1f),
                I("Crane electrical modernization package", 1150000, "ea", 1, 1, 0.4f));
            Sub(heq, "HEQ-CH", "Container Handlers", true,
                new[] { "top handler:2.5", "top pick:2.5", "toppick:2.5", "reach stacker:2.5", "empty handler:2.5", "container handler:2.5", "side pick:2", "side-lift:2", "sidelift:2", "laden handler:2.5", "lift device:1.5" },
                I("Laden top handler, 45t", 780000, "ea", 1, 2),
                I("Empty container handler, 9t", 420000, "ea", 1, 2),
                I("Reach stacker, 45t", 690000, "ea", 1, 1),
                I("Intermodal side-lift device", 510000, "ea", 1, 2));
            Sub(heq, "HEQ-FL", "Forklifts", true,
                new[] { "forklift:2", "fork lift:2", "lift truck:2", "heavy forklift:2.5", "electric forklift:2.5", "pneumatic:1" },
                I("Forklift, 5,000 lb pneumatic", 46000, "ea", 1, 4),
                I("Forklift, 15,000 lb", 98000, "ea", 1, 3),
                I("Heavy forklift, 52,000 lb", 215000, "ea", 1, 2, 0.5f),
                I("Electric forklift, 6,000 lb", 58000, "ea", 1, 4));
            cats.Add(heq);

            var yrd = Cat("YRD", "Yard Movement", "MGR-YRD", "Hostlers, vehicles, chassis, bombcarts");
            Sub(yrd, "YRD-HOS", "Hostlers / Yard Tractors", true,
                new[] { "hostler:2.5", "yard tractor:2.5", "terminal tractor:2.5", "yard truck:2", "spotter:1.5", "fifth-wheel:1.5", "fifth wheel:1.5", "utr:1.5" },
                I("Diesel yard tractor (hostler)", 168000, "ea", 1, 6),
                I("Battery-electric yard tractor", 335000, "ea", 1, 4, 0.6f),
                I("Hostler fifth-wheel & hitch rebuild", 12500, "ea", 1, 4, 1.4f));
            Sub(yrd, "YRD-CHS", "Chassis", true,
                new[] { "chassis:2.5", "yard chassis:2.5", "roadable:1.5", "gooseneck:1.5" },
                I("40' yard chassis", 11200, "ea", 10, 60),
                I("20'/40' roadable combo chassis", 13800, "ea", 10, 40));
            Sub(yrd, "YRD-BMB", "Bombcarts & Cassettes", true,
                new[] { "bombcart:2.5", "bomb cart:2.5", "cassette:2.5", "roll trailer:2.5", "mafi:2", "ro-ro trailer:2" },
                I("Bombcart, 40' heavy-duty", 38500, "ea", 2, 10),
                I("Roll trailer / cassette, 80t", 72000, "ea", 1, 6));
            Sub(yrd, "YRD-VEH", "Fleet Vehicles", true,
                new[] { "pickup:2", "truck:0.8", "van:1.5", "vehicle:1.5", "shuttle:1.5", "suv:1.5", "service truck:2", "crew cab:2" },
                I("Pickup truck, 3/4 ton crew cab", 58000, "ea", 1, 5),
                I("Terminal shuttle van", 49000, "ea", 1, 4),
                I("Service truck with crane body", 142000, "ea", 1, 2, 0.5f));
            cats.Add(yrd);

            var mro = Cat("MRO", "Maintenance & MRO", "MGR-MRO", "Fleet MRO, crane parts, wire rope, lubricants, shop tools");
            Sub(mro, "MRO-CP", "Crane Parts", false,
                new[] { "crane part:2.5", "crane parts:2.5", "hoist brake:2.5", "brake assembly:1.5", "twistlock:2.5", "twist lock:2.5", "flipper:2", "trolley wheel:2.5", "gearbox:2", "festoon:2.5", "sheave:2", "hoist motor:2", "crane repair:2", "spreader part:2.5", "crane:0.6" },
                I("Hoist brake assembly", 18500, "ea", 1, 3),
                I("Twistlock set (4)", 3200, "set", 1, 8, 1.5f),
                I("Spreader flipper arm", 6700, "ea", 1, 4),
                I("Trolley wheel assembly", 4900, "ea", 1, 6),
                I("Main hoist gearbox rebuild kit", 42000, "ea", 1, 1, 0.5f),
                I("Festoon cable set", 9800, "set", 1, 2));
            Sub(mro, "MRO-WR", "Wire Rope", false,
                new[] { "wire rope:3", "rope:1.8", "sling:1.8", "hoist rope:3", "boom rope:3", "trolley rope:3", "cable:0.6" },
                I("Main hoist wire rope 28mm x 500m", 14800, "reel", 1, 4),
                I("Boom hoist rope 32mm x 300m", 11900, "reel", 1, 2),
                I("Trolley rope 22mm x 400m", 7600, "reel", 1, 4),
                I("Lifting sling set", 2300, "set", 1, 6, 1.5f));
            Sub(mro, "MRO-LUB", "Lubricants & Fluids", false,
                new[] { "lubricant:2.5", "grease:2.5", "hydraulic oil:3", "engine oil:3", "oil:1.2", "coolant:2", "def:1.5", "diesel exhaust fluid:3", "fluid:1", "gear oil:2.5" },
                I("Hydraulic oil ISO 46 (drum)", 1150, "drum", 4, 40),
                I("Open gear grease (keg)", 640, "keg", 4, 30),
                I("Engine oil 15W-40 (drum)", 1050, "drum", 4, 30),
                I("Diesel exhaust fluid (tote)", 980, "tote", 2, 20));
            Sub(mro, "MRO-FLT", "Fleet MRO Parts", false,
                new[] { "tire:2.5", "tyre:2.5", "tires:2.5", "filter:2", "battery:2", "brake kit:2.5", "brake pad:2", "alternator:2", "starter:1.5", "hose:1.5", "hydraulic pump:2.5", "fleet part:2.5", "locomotive part:2.5", "pm kit:2" },
                I("Industrial tire, 18.00-25", 4600, "ea", 2, 8),
                I("Hostler tire set", 2900, "set", 1, 8),
                I("Brake kit, yard tractor", 1450, "kit", 2, 12),
                I("Filter service kit", 380, "kit", 5, 40),
                I("Heavy-duty battery", 420, "ea", 2, 12),
                I("Hydraulic pump, top handler", 8900, "ea", 1, 2, 0.6f));
            Sub(mro, "MRO-TL", "Shop Tools & Supplies", false,
                new[] { "tool:2", "tools:2", "wrench:2.5", "welding:2.5", "shop supplies:2.5", "consumables:1.5", "torque:1.5", "jack:2", "compressor:2", "grinder:2" },
                I("Torque wrench set", 1850, "set", 1, 4),
                I("Welding consumables pack", 760, "pack", 2, 10),
                I("Hydraulic jack, 50t", 2400, "ea", 1, 3),
                I("Shop consumables restock", 1100, "lot", 1, 6, 1.5f));
            cats.Add(mro);

            var inf = Cat("INF", "Infrastructure", "MGR-INF", "Construction, energy, facilities");
            Sub(inf, "INF-CON", "Construction & Paving", true,
                new[] { "construction:2.5", "paving:2.5", "repaving:2.5", "asphalt:2.5", "concrete:2", "wharf:2", "berth:2", "fender:2.5", "dredging:2.5", "crane rail:2.5", "building expansion:2.5", "civil:1.5" },
                I("Container yard repaving (per acre)", 410000, "acre", 1, 3),
                I("Wharf fender replacement", 265000, "ea", 1, 2),
                I("Crane rail replacement (per 100 ft)", 180000, "100ft", 1, 4),
                I("Maintenance building expansion", 2850000, "project", 1, 1, 0.08f));
            Sub(inf, "INF-ENR", "Energy & Utilities", false,
                new[] { "electricity:2.5", "utility:2", "power:1.5", "diesel:2", "renewable diesel:3", "fuel:2", "charger:2.5", "ev charging:3", "shore power:3", "substation:2.5" },
                I("Electricity - monthly utility", 52000, "month", 1, 1, 1.4f),
                I("Renewable diesel (per 1,000 gal)", 4900, "kgal", 4, 24, 2f),
                I("EV charger, 150 kW DC", 165000, "ea", 1, 4, 0.3f),
                I("Shore power vault upgrade", 620000, "project", 1, 1, 0.1f));
            Sub(inf, "INF-FAC", "Facilities Services", false,
                new[] { "janitorial:2.5", "hvac:2.5", "roof:2", "lighting:2", "led:1.5", "fence:2.5", "gate:1.5", "facility:1.5", "facilities:1.5", "pest:2", "landscaping:2" },
                I("Janitorial services (monthly)", 9800, "month", 1, 1, 1.6f),
                I("HVAC maintenance (quarterly)", 14500, "qtr", 1, 1),
                I("High-mast LED lighting retrofit", 86000, "project", 1, 1, 0.4f),
                I("Perimeter fence & gate repair", 23000, "job", 1, 1));
            cats.Add(inf);

            var wfs = Cat("WFS", "Workforce & Safety", "MGR-WFS", "Uniforms, safety equipment, benefits");
            Sub(wfs, "WFS-PPE", "PPE & Safety Equipment", false,
                new[] { "ppe:3", "safety:1.5", "vest:2", "hi-vis:1.5", "hard hat:2.5", "glove:2", "gloves:2", "harness:2.5", "fall protection:3", "gas detector:3", "first aid:2.5", "respirator:2.5" },
                I("Hi-vis vests (case of 50)", 640, "case", 1, 12),
                I("Hard hats (case of 20)", 520, "case", 1, 8),
                I("Fall protection harness kit", 1250, "kit", 2, 12),
                I("Multi-gas detector", 1480, "ea", 1, 10),
                I("Cut-resistant gloves (case)", 310, "case", 2, 20, 1.5f));
            Sub(wfs, "WFS-UNI", "Uniforms", false,
                new[] { "uniform:3", "uniforms:3", "workwear:2.5", "work wear:2.5", "boots:2", "rain gear:2.5", "coverall:2.5" },
                I("Uniform service (monthly)", 6800, "month", 1, 1, 1.5f),
                I("Safety boot allowance (batch)", 12500, "batch", 1, 1),
                I("Hi-vis rain gear set", 190, "set", 20, 200));
            Sub(wfs, "WFS-BEN", "Benefits & HR Services", false,
                new[] { "benefits:3", "broker:2", "wellness:2.5", "training:1.5", "certification:2", "recruiting:2.5", "eap:2" },
                I("Benefits broker fee (quarterly)", 85000, "qtr", 1, 1),
                I("Wellness program (annual)", 120000, "yr", 1, 1, 0.5f),
                I("Crane operator certification training", 28000, "cohort", 1, 2));
            cats.Add(wfs);

            var bop = Cat("BOP", "Business Operations", "MGR-BOP", "Software, IT, office supplies, travel, professional services");
            Sub(bop, "BOP-SW", "Software & SaaS", false,
                new[] { "software:2.5", "license:2", "licenses:2", "saas:3", "subscription:2", "cloud:1.5", "analytics platform:2.5", "seats:1.5" },
                I("Analytics platform subscription (annual)", 145000, "yr", 1, 1),
                I("Maintenance management SaaS (annual)", 96000, "yr", 1, 1),
                I("Security software licenses (annual)", 62000, "yr", 1, 1),
                I("Design software seats", 18500, "lot", 1, 3));
            Sub(bop, "BOP-IT", "IT Hardware & Networks", false,
                new[] { "laptop:2.5", "laptops:2.5", "tablet:2.5", "rugged:1.5", "server:2", "network:1.5", "switch:1.2", "router:2", "wi-fi:2.5", "wifi:2.5", "access point:2.5", "radio:2", "radios:2", "handheld:2", "monitor:1.5" },
                I("Rugged tablet", 2450, "ea", 5, 60),
                I("Laptop, standard", 1650, "ea", 5, 40),
                I("Yard Wi-Fi access point", 3800, "ea", 4, 30),
                I("Two-way radio", 780, "ea", 10, 80),
                I("Core network switch", 24000, "ea", 1, 2, 0.5f));
            Sub(bop, "BOP-PRO", "Professional Services", false,
                new[] { "consulting:2.5", "consultant:2.5", "legal:2.5", "audit:2", "assessment:2", "advisory:2.5", "engineering services:2.5", "study:1.5" },
                I("Engineering assessment", 68000, "engagement", 1, 1),
                I("Legal services (matter)", 54000, "matter", 1, 1),
                I("Process improvement consulting", 185000, "engagement", 1, 1, 0.5f),
                I("Environmental compliance audit", 42000, "engagement", 1, 1));
            Sub(bop, "BOP-TRV", "Travel", false,
                new[] { "travel:3", "airfare:3", "flight:2.5", "hotel:2.5", "lodging:2.5", "conference:2", "trip:2" },
                I("Airfare & lodging - site visit", 2300, "trip", 1, 6, 1.6f),
                I("Conference travel", 3900, "trip", 1, 4),
                I("Group travel - training cohort", 14500, "trip", 1, 1, 0.5f));
            Sub(bop, "BOP-OFF", "Office Supplies", false,
                new[] { "office supplies:3", "office:1.5", "paper:2", "toner:2.5", "printer:1.5", "furniture:2.5", "stationery:2.5", "desk:1.5", "chair:1.5" },
                I("Office supplies restock", 640, "order", 1, 3, 2f),
                I("Printer toner (case)", 780, "case", 1, 4),
                I("Office furniture set", 4200, "set", 1, 6, 0.5f));
            cats.Add(bop);

            var cap = Cat("CAP", "Capacity Flexibility", "MGR-CAP", "Rental equipment and contracted services");
            Sub(cap, "CAP-RNT", "Equipment Rental", false,
                new[] { "rental:3", "rent:2.5", "rented:2.5", "hire:2", "short-term:1.5", "forklift rental:3", "crane rental:3", "light tower:2.5", "generator:2" },
                I("Forklift rental, 15k lb (monthly)", 6400, "month", 1, 6, 1.5f),
                I("Mobile crane rental (weekly)", 28500, "week", 1, 3),
                I("Top handler rental (monthly)", 24000, "month", 1, 2),
                I("Light tower & generator rental (monthly)", 2100, "month", 2, 12));
            Sub(cap, "CAP-SVC", "Contracted Services", false,
                new[] { "contract labor:3", "temporary labor:3", "temp labor:3", "surge crew:3", "lashing:3", "security guard:3", "guard services:3", "subcontract:2.5", "marine survey:3", "inspection services:2.5", "services:0.6" },
                I("Contract labor - surge crew (weekly)", 34000, "week", 1, 2, 1.1f),
                I("Security guard services (monthly)", 46000, "month", 1, 1),
                I("Container lashing services (vessel call)", 12800, "call", 1, 4),
                I("Marine survey & inspection", 8400, "survey", 1, 2));
            cats.Add(cap);

            foreach (var c in cats)
                foreach (var s in c.Subcategories)
                    foreach (var it in s.Items)
                        it.SubcategoryCode = s.Code;
            return cats;
        }

        static Category Cat(string code, string name, string manager, string examples) =>
            new Category { Code = code, Name = name, ManagerId = manager, Examples = examples };

        static void Sub(Category c, string code, string name, bool capex, string[] keywords, params CatalogItem[] items)
        {
            var s = new Subcategory { Code = code, Name = name, CategoryCode = c.Code, Category = c, Keywords = keywords, Capex = capex };
            s.Items.AddRange(items);
            c.Subcategories.Add(s);
        }

        // id, name, city, country, lat, lon, subcategories
        static readonly object[][] SupplierRows =
        {
            new object[] { "S01", "Summit Crane Systems", "Houston", "USA", 29.76f, -95.37f, "HEQ-CRN,MRO-CP" },
            new object[] { "S02", "Northgate Heavy Equipment", "Portland", "USA", 45.52f, -122.68f, "HEQ-CH,HEQ-FL" },
            new object[] { "S03", "Atlas Material Handling", "Chicago", "USA", 41.88f, -87.63f, "HEQ-FL,HEQ-CH,CAP-RNT" },
            new object[] { "S04", "Orion Lift Trucks", "Memphis", "USA", 35.15f, -90.05f, "HEQ-FL" },
            new object[] { "S05", "Bayview Equipment Sales", "Oakland", "USA", 37.80f, -122.27f, "HEQ-CH,HEQ-FL,YRD-HOS" },
            new object[] { "S06", "Ironclad Terminal Tractors", "Kansas City", "USA", 39.10f, -94.58f, "YRD-HOS" },
            new object[] { "S07", "Voltline Electric Hostlers", "San Diego", "USA", 32.72f, -117.16f, "YRD-HOS" },
            new object[] { "S08", "Pacific Chassis Works", "Long Beach", "USA", 33.77f, -118.19f, "YRD-CHS,YRD-BMB" },
            new object[] { "S09", "Harbor Trailer Manufacturing", "Savannah", "USA", 32.08f, -81.09f, "YRD-BMB,YRD-CHS" },
            new object[] { "S10", "Frontier Fleet Vehicles", "Dallas", "USA", 32.78f, -96.80f, "YRD-VEH" },
            new object[] { "S11", "Pacific Crane Parts Co.", "Tacoma", "USA", 47.25f, -122.44f, "MRO-CP" },
            new object[] { "S12", "Titan Wire Rope", "Houston", "USA", 29.70f, -95.40f, "MRO-WR" },
            new object[] { "S13", "Harborline Lubricants", "Long Beach", "USA", 33.80f, -118.17f, "MRO-LUB" },
            new object[] { "S14", "Keystone Fleet Parts", "Columbus", "USA", 39.96f, -83.00f, "MRO-FLT" },
            new object[] { "S15", "Gulf Coast Industrial Supply", "New Orleans", "USA", 29.95f, -90.07f, "MRO-FLT,MRO-TL,MRO-LUB,WFS-PPE" },
            new object[] { "S16", "Precision Gear & Drive", "Milwaukee", "USA", 43.04f, -87.91f, "MRO-CP" },
            new object[] { "S17", "Anchor Tool & Supply", "Seattle", "USA", 47.61f, -122.33f, "MRO-TL,WFS-PPE" },
            new object[] { "S18", "Meridian Construction Group", "Seattle", "USA", 47.62f, -122.32f, "INF-CON" },
            new object[] { "S19", "Coastal Energy Partners", "Los Angeles", "USA", 34.05f, -118.24f, "INF-ENR" },
            new object[] { "S20", "Bluewater Civil & Marine", "Mobile", "USA", 30.69f, -88.04f, "INF-CON" },
            new object[] { "S21", "Evergreen Facility Services", "Portland", "USA", 45.50f, -122.65f, "INF-FAC" },
            new object[] { "S22", "Gridpoint EV Infrastructure", "Denver", "USA", 39.74f, -104.99f, "INF-ENR" },
            new object[] { "S23", "Delta Fuel Distributors", "Houston", "USA", 29.78f, -95.35f, "INF-ENR" },
            new object[] { "S24", "Northstar Safety Supply", "Minneapolis", "USA", 44.98f, -93.27f, "WFS-PPE" },
            new object[] { "S25", "Guardian PPE Direct", "Atlanta", "USA", 33.75f, -84.39f, "WFS-PPE,WFS-UNI" },
            new object[] { "S26", "WorkForce Apparel", "Charlotte", "USA", 35.23f, -80.84f, "WFS-UNI" },
            new object[] { "S27", "Cascade Benefits Advisors", "Seattle", "USA", 47.60f, -122.33f, "WFS-BEN" },
            new object[] { "S28", "SafeHarbor Training Institute", "Baltimore", "USA", 39.29f, -76.61f, "WFS-BEN" },
            new object[] { "S29", "BlueWave IT Solutions", "San Jose", "USA", 37.34f, -121.89f, "BOP-IT" },
            new object[] { "S30", "Stratus Cloud Software", "Austin", "USA", 30.27f, -97.74f, "BOP-SW" },
            new object[] { "S31", "Ledgerline Consulting", "New York", "USA", 40.71f, -74.01f, "BOP-PRO" },
            new object[] { "S32", "OfficePoint Supplies", "Phoenix", "USA", 33.45f, -112.07f, "BOP-OFF" },
            new object[] { "S33", "Wayfare Corporate Travel", "Chicago", "USA", 41.88f, -87.62f, "BOP-TRV" },
            new object[] { "S34", "Ruggedline Mobile Devices", "Salt Lake City", "USA", 40.76f, -111.89f, "BOP-IT" },
            new object[] { "S35", "Beacon Analytics", "Boston", "USA", 42.36f, -71.06f, "BOP-SW,BOP-PRO" },
            new object[] { "S36", "Rapid Rentals Equipment", "Los Angeles", "USA", 34.02f, -118.28f, "CAP-RNT" },
            new object[] { "S37", "Portside Staffing Services", "Oakland", "USA", 37.81f, -122.29f, "CAP-SVC" },
            new object[] { "S38", "Tidewater Marine Services", "Norfolk", "USA", 36.85f, -76.29f, "CAP-SVC" },
            new object[] { "S39", "Sentinel Guard Services", "Phoenix", "USA", 33.44f, -112.05f, "CAP-SVC,INF-FAC" },
            new object[] { "S40", "CrossDock Contract Labor", "Memphis", "USA", 35.14f, -90.03f, "CAP-SVC" },
            new object[] { "S41", "Pacifico Equipos Portuarios S.A.", "Manzanillo", "Mexico", 19.05f, -104.32f, "MRO-CP,MRO-FLT,CAP-RNT" },
            new object[] { "S42", "Caribe Suministros Industriales", "Cartagena", "Colombia", 10.40f, -75.51f, "MRO-FLT,MRO-LUB,WFS-PPE" },
            new object[] { "S43", "Saigon Port Engineering JSC", "Ho Chi Minh City", "Vietnam", 10.82f, 106.63f, "MRO-CP,INF-CON,MRO-FLT" },
            new object[] { "S44", "Istmo Servicios Maritimos", "Panama City", "Panama", 8.98f, -79.52f, "CAP-SVC,MRO-FLT" },
            new object[] { "S45", "Grainline Industrial Distribution", "Chicago", "USA", 41.85f, -87.68f, "MRO-TL,WFS-PPE,MRO-LUB,MRO-FLT" },
            new object[] { "S46", "Heartland Rail Supply", "Omaha", "USA", 41.26f, -95.94f, "MRO-FLT,MRO-TL" },
            new object[] { "S47", "Crescent Tire & Service", "Memphis", "USA", 35.13f, -89.97f, "MRO-FLT" },
            new object[] { "S48", "Liftpoint Rentals", "Houston", "USA", 29.74f, -95.46f, "CAP-RNT" },
            new object[] { "S49", "Metro Office & Print", "Seattle", "USA", 47.59f, -122.30f, "BOP-OFF" },
            new object[] { "S50", "Pinnacle Engineering Advisors", "Denver", "USA", 39.75f, -105.00f, "BOP-PRO,INF-CON" },
            new object[] { "S51", "Harborview Hostler Service", "Tacoma", "USA", 47.24f, -122.40f, "YRD-HOS,MRO-FLT" },
        };

        public static List<Supplier> Suppliers(Rng rng, DateTime today)
        {
            var list = new List<Supplier>();
            foreach (var r in SupplierRows)
            {
                var s = new Supplier
                {
                    Id = (string)r[0], Name = (string)r[1], City = (string)r[2], Country = (string)r[3],
                    Lat = (float)r[4], Lon = (float)r[5],
                    Otif = (float)rng.Range(0.78, 0.99), Quality = (float)rng.Range(0.80, 0.99),
                    Responsiveness = (float)rng.Range(0.70, 0.98),
                    Since = today.AddDays(-rng.Range(200, 4000)),
                };
                s.SubcategoryCodes.AddRange(((string)r[6]).Split(','));
                float score = s.Score;
                s.Risk = score > 0.92f ? "Low" : score > 0.85f ? "Moderate" : "Elevated";
                list.Add(s);
            }
            return list;
        }

        // supplierId, subcategory, scope, scopeRef, startOffsetDays, endOffsetDays, discount, terms
        public static readonly object[][] ContractRows =
        {
            new object[] { "S04", "HEQ-FL", ContractScope.National, "", -600, 500, 0.12f, "Net 45; fixed pricing through term; 3-yr parts availability" },
            new object[] { "S06", "YRD-HOS", ContractScope.National, "", -700, 300, 0.09f, "Net 60; volume rebate 2% above 40 units/yr" },
            new object[] { "S11", "MRO-CP", ContractScope.BusinessUnit, "SSAMarine", -500, 45, 0.14f, "Net 30; 48h emergency dispatch; consignment at 4 sites" },
            new object[] { "S12", "MRO-WR", ContractScope.National, "", -400, 700, 0.11f, "Net 45; certified test reports; reel buy-back" },
            new object[] { "S13", "MRO-LUB", ContractScope.National, "", -450, 280, 0.16f, "Net 30; managed inventory; used-oil pickup included" },
            new object[] { "S14", "MRO-FLT", ContractScope.BusinessUnit, "RMS", -380, 400, 0.13f, "Net 45; next-day delivery to ramps" },
            new object[] { "S15", "MRO-FLT", ContractScope.BusinessUnit, "SSAMarine", -300, 65, 0.10f, "Net 30; Gulf & Atlantic coverage" },
            new object[] { "S24", "WFS-PPE", ContractScope.National, "", -520, 210, 0.18f, "Net 30; vending machines at 12 sites" },
            new object[] { "S26", "WFS-UNI", ContractScope.National, "", -610, 120, 0.08f, "Net 30; weekly laundering service" },
            new object[] { "S29", "BOP-IT", ContractScope.National, "", -330, 400, 0.15f, "Net 45; 4-yr warranty; imaging included" },
            new object[] { "S30", "BOP-SW", ContractScope.National, "", -250, 480, 0.20f, "Annual in advance; enterprise license" },
            new object[] { "S33", "BOP-TRV", ContractScope.National, "", -700, 30, 0.06f, "Negotiated airline & hotel program" },
            new object[] { "S32", "BOP-OFF", ContractScope.National, "", -420, 310, 0.22f, "Net 30; free next-day delivery" },
            new object[] { "S36", "CAP-RNT", ContractScope.National, "", -360, 370, 0.12f, "Net 30; damage waiver included" },
            new object[] { "S40", "CAP-SVC", ContractScope.BusinessUnit, "RMS", -280, 450, 0.07f, "Net 30; 24h surge mobilization" },
            new object[] { "S37", "CAP-SVC", ContractScope.Location, "CT-OAK", -200, 160, 0.05f, "Net 30; local labor pool" },
            new object[] { "S19", "INF-ENR", ContractScope.Company, "2100", -540, 190, 0.08f, "Fixed-block power pricing" },
            new object[] { "S01", "HEQ-CRN", ContractScope.BusinessUnit, "SSAMarine", -800, 900, 0.06f, "Framework agreement; milestone payments" },
            new object[] { "S08", "YRD-CHS", ContractScope.National, "", -390, 340, 0.10f, "Net 45; 5-yr structural warranty" },
            new object[] { "S10", "YRD-VEH", ContractScope.National, "", -450, 260, 0.09f, "Fleet pricing; upfit included" },
            new object[] { "S21", "INF-FAC", ContractScope.BusinessUnit, "SSAMarine", -330, 420, 0.07f, "Net 30; West Coast facilities" },
            new object[] { "S23", "INF-ENR", ContractScope.BusinessUnit, "RMS", -300, 380, 0.05f, "Rack-plus pricing; on-site fueling" },
            new object[] { "S41", "MRO-CP", ContractScope.Location, "CT-MZO", -260, 500, 0.09f, "Net 30 MXN; local stock" },
            new object[] { "S02", "HEQ-CH", ContractScope.National, "", -410, 600, 0.07f, "Net 60; dealer PM program" },
            new object[] { "S09", "YRD-BMB", ContractScope.BusinessUnit, "SSAMarine", -900, -40, 0.08f, "Expired; renewal in negotiation" },
            new object[] { "S39", "CAP-SVC", ContractScope.Company, "2300", -310, 250, 0.06f, "Net 30; cruise-season staffing" },
            new object[] { "S47", "MRO-FLT", ContractScope.Company, "3400", -200, 530, 0.12f, "Net 30; road service included" },
            new object[] { "S27", "WFS-BEN", ContractScope.National, "", -640, 88, 0.00f, "Annual broker agreement" },
            new object[] { "S45", "MRO-TL", ContractScope.National, "", -350, 370, 0.15f, "Net 30; punch-out catalog" },
        };
    }
}
