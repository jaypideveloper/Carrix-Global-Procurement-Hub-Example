using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProcurementHub
{
    public sealed class ClassificationResult
    {
        public Subcategory Sub;
        public float Score;
        public float Confidence;
        public readonly List<string> Evidence = new List<string>();
    }

    /// <summary>
    /// Explainable keyword classifier. Each subcategory carries weighted phrases ("wire rope:3");
    /// multi-word phrases and catalog item overlap add evidence, and a known supplier adds a prior.
    /// Every point of score is traceable to a matched term, so the UI can show why.
    /// </summary>
    public sealed class CategoryClassifier
    {
        readonly List<(Subcategory sub, string phrase, float weight)> keywords = new List<(Subcategory, string, float)>();
        readonly Dictionary<Subcategory, List<HashSet<string>>> itemTokens = new Dictionary<Subcategory, List<HashSet<string>>>();
        readonly List<Subcategory> subs;

        static readonly HashSet<string> Stop = new HashSet<string>
        {
            "the", "and", "for", "with", "new", "per", "unit", "each", "set", "kit", "case", "monthly", "annual", "weekly", "lot", "order", "from", "our",
        };

        public int SubcategoryCount => subs.Count;

        public CategoryClassifier(HubDatabase db)
        {
            subs = db.Subcategories.ToList();
            foreach (var s in subs)
            {
                foreach (var k in s.Keywords)
                {
                    int colon = k.LastIndexOf(':');
                    string phrase = colon > 0 ? k.Substring(0, colon) : k;
                    float w = colon > 0 ? float.Parse(k.Substring(colon + 1), System.Globalization.CultureInfo.InvariantCulture) : 1f;
                    keywords.Add((s, Clean(phrase), w));
                }
                itemTokens[s] = s.Items.Select(i => new HashSet<string>(Tokens(i.Name))).ToList();
            }
        }

        public static string Clean(string text)
        {
            var sb = new StringBuilder(text.Length + 2);
            sb.Append(' ');
            foreach (var ch in text.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '/' ? ch : ' ');
            sb.Append(' ');
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " +", " ");
        }

        public static IEnumerable<string> Tokens(string text) =>
            Clean(text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 3 && !Stop.Contains(t) && !t.All(char.IsDigit));

        public List<ClassificationResult> Classify(string text, Supplier supplierHint = null, int top = 3)
        {
            var results = new List<ClassificationResult>();
            if (string.IsNullOrWhiteSpace(text)) return results;
            string clean = Clean(text);
            var tokens = new HashSet<string>(Tokens(text));
            var bySub = new Dictionary<Subcategory, ClassificationResult>();

            ClassificationResult Get(Subcategory s)
            {
                if (!bySub.TryGetValue(s, out var r)) bySub[s] = r = new ClassificationResult { Sub = s };
                return r;
            }

            foreach (var (sub, phrase, weight) in keywords)
            {
                string p = phrase.Trim();
                if (p.Length == 0) continue;
                bool hit = clean.Contains(" " + p + " ") || clean.Contains(" " + p + "s ") || clean.Contains(" " + p + "es ");
                if (!hit) continue;
                var r = Get(sub);
                r.Score += weight;
                r.Evidence.Add("\"" + p + "\"");
            }

            foreach (var kv in itemTokens)
            {
                float best = 0;
                foreach (var set in kv.Value)
                {
                    int overlap = set.Count(tokens.Contains);
                    best = Math.Max(best, overlap);
                }
                if (best <= 0) continue;
                var r = Get(kv.Key);
                float add = Math.Min(1.5f, best * 0.35f);
                r.Score += add;
                r.Evidence.Add("catalog item match");
            }

            if (supplierHint != null)
            {
                foreach (var code in supplierHint.SubcategoryCodes)
                {
                    var s = subs.FirstOrDefault(x => x.Code == code);
                    if (s == null) continue;
                    var r = Get(s);
                    r.Score += supplierHint.SubcategoryCodes.Count == 1 ? 1.2f : 0.6f;
                    r.Evidence.Add("supplier " + supplierHint.Name + " serves this category");
                }
            }

            results = bySub.Values.Where(r => r.Score > 0).OrderByDescending(r => r.Score).ToList();
            float s1 = results.Count > 0 ? results[0].Score : 0;
            float s2 = results.Count > 1 ? results[1].Score : 0;
            foreach (var r in results)
            {
                float lead = r == results[0] ? s1 / (s1 + 0.5f * s2 + 0.5f) : r.Score / (s1 + r.Score + 0.5f);
                r.Confidence = Math.Max(0.02f, Math.Min(0.98f, lead * Math.Min(1f, r.Score / 2.5f)));
            }
            return results.Take(top).ToList();
        }

        /// <summary>Best catalog item within a subcategory for a free-text description.</summary>
        public CatalogItem MatchItem(Subcategory sub, string text)
        {
            if (sub == null) return null;
            var tokens = new HashSet<string>(Tokens(text ?? ""));
            CatalogItem best = null;
            int bestOverlap = 0;
            var sets = itemTokens[sub];
            for (int i = 0; i < sub.Items.Count; i++)
            {
                int overlap = sets[i].Count(tokens.Contains);
                if (overlap > bestOverlap) { bestOverlap = overlap; best = sub.Items[i]; }
            }
            return best;
        }
    }
}
