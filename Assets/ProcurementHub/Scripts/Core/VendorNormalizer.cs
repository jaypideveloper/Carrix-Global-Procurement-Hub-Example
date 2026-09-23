using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProcurementHub
{
    /// <summary>
    /// Vendor-name cleansing and fuzzy matching: normalises spellings ("PAC CRANE PTS INC - TACOMA"
    /// becomes "PACIFIC CRANE PART") and scores similarity with soft token overlap plus Jaro-Winkler.
    /// </summary>
    public static class VendorNormalizer
    {
        static readonly HashSet<string> Drop = new HashSet<string>
        {
            "INC", "INCORPORATED", "CO", "COMPANY", "CORP", "CORPORATION", "LLC", "LTD", "LIMITED", "LP", "LLP",
            "SA", "CV", "DE", "JSC", "PLC", "GMBH", "REMIT", "DBA", "THE", "AND", "OF",
        };

        static readonly Dictionary<string, string> Expand = new Dictionary<string, string>
        {
            ["PAC"] = "PACIFIC", ["PTS"] = "PARTS", ["SVCS"] = "SERVICES", ["SVC"] = "SERVICES", ["SERV"] = "SERVICES",
            ["MFG"] = "MANUFACTURING", ["INTL"] = "INTERNATIONAL", ["EQUIP"] = "EQUIPMENT", ["EQPT"] = "EQUIPMENT",
            ["IND"] = "INDUSTRIAL", ["SUP"] = "SUPPLY", ["SUPP"] = "SUPPLY", ["SUPPLIES"] = "SUPPLY", ["DIST"] = "DISTRIBUTION",
            ["TECH"] = "TECHNOLOGY", ["SYS"] = "SYSTEMS", ["CONST"] = "CONSTRUCTION", ["ENG"] = "ENGINEERING",
            ["MATL"] = "MATERIAL", ["HDLG"] = "HANDLING", ["ELEC"] = "ELECTRIC", ["TERM"] = "TERMINAL", ["ADV"] = "ADVISORS",
            ["INTERNATL"] = "INTERNATIONAL", ["MGMT"] = "MANAGEMENT",
        };

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.ToUpperInvariant();
            int dash = s.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0) s = s.Substring(0, dash);
            int paren = s.IndexOf('(');
            if (paren > 0) s = s.Substring(0, paren);
            int dba = s.IndexOf(" DBA ", StringComparison.Ordinal);
            if (dba > 0) s = s.Substring(dba + 5);

            var sb = new StringBuilder(s.Length);
            foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            var tokens = new List<string>();
            foreach (var t in sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (t.Length == 1 || t.All(char.IsDigit)) continue;
                string tok = Expand.TryGetValue(t, out var e) ? e : t;
                if (Drop.Contains(tok)) continue;
                tokens.Add(Stem(tok));
            }
            return string.Join(" ", tokens);
        }

        static string Stem(string t)
        {
            if (t.Length > 4 && t.EndsWith("IES", StringComparison.Ordinal)) return t.Substring(0, t.Length - 3) + "Y";
            if (t.Length > 4 && t.EndsWith("S", StringComparison.Ordinal) && !t.EndsWith("SS", StringComparison.Ordinal)) return t.Substring(0, t.Length - 1);
            return t;
        }

        /// <summary>0..1 similarity of two raw vendor names.</summary>
        public static float Similarity(string rawA, string rawB) => SimilarityNormalized(Normalize(rawA), Normalize(rawB));

        public static float SimilarityNormalized(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return 0f;
            if (a == b) return 1f;
            var ta = a.Split(' ');
            var tb = b.Split(' ');
            float tokenScore = SoftJaccard(ta, tb);
            float jw = JaroWinkler(a, b);
            return 0.55f * tokenScore + 0.45f * jw;
        }

        static float SoftJaccard(string[] a, string[] b)
        {
            var used = new bool[b.Length];
            float matched = 0;
            foreach (var x in a)
            {
                int best = -1;
                float bestScore = 0;
                for (int j = 0; j < b.Length; j++)
                {
                    if (used[j]) continue;
                    float s = x == b[j] ? 1f : LevenshteinRatio(x, b[j]);
                    if (s > bestScore) { bestScore = s; best = j; }
                }
                if (best >= 0 && bestScore >= 0.75f) { used[best] = true; matched += bestScore; }
            }
            float union = a.Length + b.Length - matched;
            return union <= 0 ? 0 : matched / union;
        }

        /// <summary>1 - normalised edit distance, counting an adjacent transposition as one edit (optimal string alignment).</summary>
        public static float LevenshteinRatio(string a, string b)
        {
            int n = a.Length, m = b.Length;
            if (n == 0 || m == 0) return 0;
            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        v = Math.Min(v, d[i - 2, j - 2] + 1);
                    d[i, j] = v;
                }
            }
            return 1f - (float)d[n, m] / Math.Max(n, m);
        }

        public static float JaroWinkler(string s1, string s2)
        {
            if (s1 == s2) return 1f;
            int l1 = s1.Length, l2 = s2.Length;
            if (l1 == 0 || l2 == 0) return 0f;
            int range = Math.Max(0, Math.Max(l1, l2) / 2 - 1);
            var m1 = new bool[l1];
            var m2 = new bool[l2];
            int matches = 0;
            for (int i = 0; i < l1; i++)
            {
                int lo = Math.Max(0, i - range), hi = Math.Min(l2 - 1, i + range);
                for (int j = lo; j <= hi; j++)
                {
                    if (m2[j] || s1[i] != s2[j]) continue;
                    m1[i] = m2[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0f;
            int t = 0, k = 0;
            for (int i = 0; i < l1; i++)
            {
                if (!m1[i]) continue;
                while (!m2[k]) k++;
                if (s1[i] != s2[k]) t++;
                k++;
            }
            float m = matches;
            float jaro = (m / l1 + m / l2 + (m - t / 2f) / m) / 3f;
            int prefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(l1, l2)); i++)
            {
                if (s1[i] == s2[i]) prefix++;
                else break;
            }
            return jaro + prefix * 0.1f * (1f - jaro);
        }

        /// <summary>Groups vendor records that probably refer to the same real supplier (union-find over pairwise similarity).</summary>
        public static List<VendorCluster> Cluster(IList<VendorRecord> records, float threshold)
        {
            int n = records.Count;
            var norm = new string[n];
            for (int i = 0; i < n; i++) norm[i] = Normalize(records[i].RawName);
            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }

            var pairScore = new Dictionary<long, float>();
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    // Cheap bound: even a perfect token score cannot lift a weak Jaro-Winkler over the threshold.
                    if (norm[i] != norm[j] && 0.55f + 0.45f * JaroWinkler(norm[i], norm[j]) < threshold) continue;
                    float s = SimilarityNormalized(norm[i], norm[j]);
                    if (s < threshold) continue;
                    pairScore[(long)i * n + j] = s;
                    int a = Find(i), b = Find(j);
                    if (a != b) parent[a] = b;
                }
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<int>();
                g.Add(i);
            }

            var result = new List<VendorCluster>();
            foreach (var g in groups.Values)
            {
                if (g.Count < 2) continue;
                var names = g.Select(i => records[i].RawName).Distinct().Count();
                if (names < 2 && g.Select(i => records[i].CompanyCode).Distinct().Count() < 2) continue;
                // Representative = most common normalised form.
                string rep = g.GroupBy(i => norm[i]).OrderByDescending(x => x.Count()).First().Key;
                var cluster = new VendorCluster { Key = rep };
                foreach (var i in g)
                    cluster.Members.Add(new VendorClusterMember { Record = records[i], Normalized = norm[i], Similarity = SimilarityNormalized(norm[i], rep) });
                cluster.Members.Sort((x, y) => y.Similarity.CompareTo(x.Similarity));
                cluster.MinSimilarity = cluster.Members.Min(m => m.Similarity);
                var truth = cluster.Members.GroupBy(m => m.Record.SupplierId).OrderByDescending(x => x.Count()).First();
                cluster.TrueSupplierId = truth.Key;
                cluster.Purity = (float)truth.Count() / cluster.Members.Count;
                result.Add(cluster);
            }
            result.Sort((a, b) => b.Members.Count.CompareTo(a.Members.Count));
            return result;
        }
    }

    public sealed class VendorClusterMember
    {
        public VendorRecord Record;
        public string Normalized;
        public float Similarity;
    }

    public sealed class VendorCluster
    {
        public string Key;
        public readonly List<VendorClusterMember> Members = new List<VendorClusterMember>();
        public float MinSimilarity;
        public string TrueSupplierId;
        public float Purity;
        public int EntityCount => Members.Select(m => m.Record.CompanyCode).Distinct().Count();
        public int SpellingCount => Members.Select(m => m.Record.RawName).Distinct().Count();
    }

    public sealed class SupplierMatch
    {
        public Supplier Supplier;
        public float Similarity;
        public string MatchedName;
        public bool IsConfident;
    }

    /// <summary>Resolves free-text supplier names against the supplier master and every known alias.</summary>
    public sealed class SupplierMatcher
    {
        readonly List<(string norm, string raw, Supplier supplier)> index = new List<(string, string, Supplier)>();
        readonly float threshold;

        public SupplierMatcher(HubDatabase db)
        {
            threshold = db.Policy.SupplierMatchThreshold;
            var seen = new HashSet<string>();
            foreach (var s in db.Suppliers)
                if (seen.Add(s.Id + "|" + VendorNormalizer.Normalize(s.Name)))
                    index.Add((VendorNormalizer.Normalize(s.Name), s.Name, s));
            foreach (var v in db.VendorRecords)
            {
                string n = VendorNormalizer.Normalize(v.RawName);
                if (seen.Add(v.SupplierId + "|" + n)) index.Add((n, v.RawName, db.SupplierById[v.SupplierId]));
            }
        }

        public SupplierMatch Match(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string n = VendorNormalizer.Normalize(text);
            if (n.Length == 0) return null;
            SupplierMatch best = null;
            foreach (var (norm, raw, supplier) in index)
            {
                float s = VendorNormalizer.SimilarityNormalized(n, norm);
                // Typed prefixes ("pacific crane") should still resolve while the user is typing.
                if (norm.StartsWith(n, StringComparison.Ordinal) && n.Length >= 6) s = Math.Max(s, 0.9f);
                if (best == null || s > best.Similarity)
                    best = new SupplierMatch { Supplier = supplier, Similarity = s, MatchedName = raw };
            }
            if (best != null) best.IsConfident = best.Similarity >= threshold;
            return best;
        }
    }
}
