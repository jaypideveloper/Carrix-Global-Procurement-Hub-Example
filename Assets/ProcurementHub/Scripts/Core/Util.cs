using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProcurementHub
{
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Money(double v)
        {
            double a = Math.Abs(v);
            string sign = v < 0 ? "-" : "";
            if (a >= 1e9) return sign + "$" + (a / 1e9).ToString("0.00", Inv) + "B";
            if (a >= 1e6) return sign + "$" + (a / 1e6).ToString(a >= 1e8 ? "0" : "0.0", Inv) + "M";
            if (a >= 1e4) return sign + "$" + (a / 1e3).ToString("0", Inv) + "K";
            if (a >= 1e3) return sign + "$" + (a / 1e3).ToString("0.0", Inv) + "K";
            return sign + "$" + a.ToString("0", Inv);
        }

        public static string MoneyExact(double v) => (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,##0", Inv);

        public static string Unit(double v) => "$" + v.ToString(v >= 100 ? "#,##0" : "#,##0.00", Inv);

        public static string Pct(double fraction, int decimals = 0) =>
            (fraction * 100.0).ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), Inv) + "%";

        public static string SignedPct(double fraction) =>
            (fraction >= 0 ? "+" : "") + (fraction * 100.0).ToString("0", Inv) + "%";

        public static string Num(double v) => v.ToString("#,##0", Inv);

        public static string Date(DateTime d) => d.ToString("MMM d, yyyy", Inv);
        public static string ShortDate(DateTime d) => d.ToString("MMM d", Inv);
        public static string Month(DateTime d) => d.ToString("MMM", Inv);
        public static string MonthYear(DateTime d) => d.ToString("MMM yy", Inv);
        public static string Stamp(DateTime d) => d.ToString("MMM d, HH:mm", Inv);
        public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", Inv);

        public static string Ago(DateTime then, DateTime now)
        {
            var span = now - then;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + "m ago";
            if (span.TotalDays < 1) return (int)span.TotalHours + "h ago";
            if (span.TotalDays < 60) return (int)span.TotalDays + "d ago";
            return (int)(span.TotalDays / 30) + "mo ago";
        }

        public static string Days(double d) => d.ToString("0.0", Inv) + " d";

        public static string Inv0(double v) => v.ToString("0", Inv);
        public static string Inv2(double v) => v.ToString("0.00", Inv);
    }

    /// <summary>Deterministic random source so every launch produces the same synthetic network.</summary>
    public sealed class Rng
    {
        readonly Random r;
        public Rng(int seed) { r = new Random(seed); }

        public double Next() => r.NextDouble();
        public int Range(int minInclusive, int maxInclusive) => r.Next(minInclusive, maxInclusive + 1);
        public double Range(double min, double max) => min + (max - min) * r.NextDouble();
        public bool Chance(double p) => r.NextDouble() < p;
        public T Pick<T>(IList<T> list) => list[r.Next(list.Count)];

        public T Weighted<T>(IList<T> list, Func<T, double> weight)
        {
            double total = 0;
            foreach (var x in list) total += Math.Max(0, weight(x));
            double t = r.NextDouble() * total;
            foreach (var x in list)
            {
                t -= Math.Max(0, weight(x));
                if (t <= 0) return x;
            }
            return list[list.Count - 1];
        }

        /// <summary>Integer sampled toward the low end of the range (most orders are small).</summary>
        public int Skewed(int min, int max, double power = 1.8)
        {
            if (max <= min) return min;
            double u = Math.Pow(r.NextDouble(), power);
            return min + (int)Math.Round(u * (max - min));
        }

        public int Poisson(double lambda)
        {
            if (lambda <= 0) return 0;
            double l = Math.Exp(-lambda), p = 1.0;
            int k = 0;
            do { k++; p *= r.NextDouble(); } while (p > l && k < 60);
            return k - 1;
        }
    }

    public static class Stats
    {
        public static double Median(List<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            values.Sort();
            int n = values.Count;
            return n % 2 == 1 ? values[n / 2] : 0.5 * (values[n / 2 - 1] + values[n / 2]);
        }

        public static double Percentile(List<double> sortedValues, double p)
        {
            if (sortedValues == null || sortedValues.Count == 0) return 0;
            double idx = p * (sortedValues.Count - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            if (lo == hi) return sortedValues[lo];
            return sortedValues[lo] + (sortedValues[hi] - sortedValues[lo]) * (idx - lo);
        }
    }
}
