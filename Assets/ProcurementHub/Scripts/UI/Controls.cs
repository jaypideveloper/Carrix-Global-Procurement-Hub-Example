using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public enum IconKind { Globe, Plus, List, Chart, Supplier, Shield, Sliders, Database, Check, Warning, Cross, Minus, Sparkle, Refresh, Download, Pin }

    /// <summary>Line icons drawn with Painter2D so the app needs no image assets.</summary>
    public sealed class Icon : VisualElement
    {
        IconKind kind;
        Color color;

        public Icon(IconKind kind, Color color)
        {
            this.kind = kind;
            this.color = color;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(IconKind k, Color c)
        {
            kind = k;
            color = c;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1 || h < 1) return;
            float s = Mathf.Min(w, h) / 16f;
            Vector2 P(float x, float y) => new Vector2(x * s, y * s);
            var p = ctx.painter2D;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineWidth = 1.5f * s;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            void Line(float x1, float y1, float x2, float y2) { p.BeginPath(); p.MoveTo(P(x1, y1)); p.LineTo(P(x2, y2)); p.Stroke(); }
            void Circle(float cx, float cy, float r, bool fill = false)
            {
                p.BeginPath();
                p.Arc(P(cx, cy), r * s, Angle.Degrees(0), Angle.Degrees(360));
                p.ClosePath();
                if (fill) p.Fill(); else p.Stroke();
            }
            void Poly(bool close, params float[] pts)
            {
                p.BeginPath();
                p.MoveTo(P(pts[0], pts[1]));
                for (int i = 2; i < pts.Length; i += 2) p.LineTo(P(pts[i], pts[i + 1]));
                if (close) p.ClosePath();
                p.Stroke();
            }

            switch (kind)
            {
                case IconKind.Globe:
                    Circle(8, 8, 6.2f);
                    Line(1.8f, 8, 14.2f, 8);
                    p.BeginPath(); p.MoveTo(P(8, 1.8f)); p.BezierCurveTo(P(4.5f, 4.5f), P(4.5f, 11.5f), P(8, 14.2f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(8, 1.8f)); p.BezierCurveTo(P(11.5f, 4.5f), P(11.5f, 11.5f), P(8, 14.2f)); p.Stroke();
                    break;
                case IconKind.Plus:
                    Circle(8, 8, 6.2f); Line(8, 5, 8, 11); Line(5, 8, 11, 8);
                    break;
                case IconKind.List:
                    Line(5.5f, 4, 14, 4); Line(5.5f, 8, 14, 8); Line(5.5f, 12, 14, 12);
                    Circle(2.5f, 4, 0.9f, true); Circle(2.5f, 8, 0.9f, true); Circle(2.5f, 12, 0.9f, true);
                    break;
                case IconKind.Chart:
                    Line(2, 14, 14, 14); Line(4.5f, 13, 4.5f, 9); Line(8, 13, 8, 4); Line(11.5f, 13, 11.5f, 7);
                    break;
                case IconKind.Supplier:
                    Poly(true, 2, 14, 2, 6, 6, 3.5f, 6, 6.5f, 10, 4, 10, 7, 14, 5, 14, 14);
                    Line(5, 11, 5, 11.1f); Line(8, 11, 8, 11.1f); Line(11, 11, 11, 11.1f);
                    break;
                case IconKind.Shield:
                    Poly(true, 8, 1.8f, 13.5f, 4, 13, 9, 8, 14.2f, 3, 9, 2.5f, 4);
                    Poly(false, 5.5f, 8, 7.3f, 9.8f, 10.5f, 6.5f);
                    break;
                case IconKind.Sliders:
                    Line(2, 4.5f, 14, 4.5f); Line(2, 11.5f, 14, 11.5f);
                    Circle(10, 4.5f, 1.8f, true); Circle(5.5f, 11.5f, 1.8f, true);
                    break;
                case IconKind.Database:
                    p.BeginPath(); p.Arc(P(8, 4), 5.5f * s, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                    Line(2.5f, 4, 2.5f, 12); Line(13.5f, 4, 13.5f, 12);
                    p.BeginPath(); p.MoveTo(P(2.5f, 8)); p.BezierCurveTo(P(3.5f, 10.5f), P(12.5f, 10.5f), P(13.5f, 8)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(2.5f, 12)); p.BezierCurveTo(P(3.5f, 14.5f), P(12.5f, 14.5f), P(13.5f, 12)); p.Stroke();
                    break;
                case IconKind.Check:
                    Circle(8, 8, 6.4f);
                    Poly(false, 5, 8.2f, 7.2f, 10.4f, 11.2f, 6);
                    break;
                case IconKind.Warning:
                    Poly(true, 8, 2, 14.5f, 13.8f, 1.5f, 13.8f);
                    Line(8, 6.5f, 8, 9.8f);
                    Circle(8, 11.9f, 0.7f, true);
                    break;
                case IconKind.Cross:
                    Circle(8, 8, 6.4f); Line(5.8f, 5.8f, 10.2f, 10.2f); Line(10.2f, 5.8f, 5.8f, 10.2f);
                    break;
                case IconKind.Minus:
                    Circle(8, 8, 6.4f); Line(5.2f, 8, 10.8f, 8);
                    break;
                case IconKind.Sparkle:
                    Poly(true, 8, 1.5f, 9.6f, 6.4f, 14.5f, 8, 9.6f, 9.6f, 8, 14.5f, 6.4f, 9.6f, 1.5f, 8, 6.4f, 6.4f);
                    break;
                case IconKind.Refresh:
                    p.BeginPath(); p.Arc(P(8, 8), 5.5f * s, Angle.Degrees(-60), Angle.Degrees(230)); p.Stroke();
                    Poly(false, 10.8f, 1.8f, 11, 3.4f, 9.3f, 3.9f);
                    break;
                case IconKind.Download:
                    Line(8, 2, 8, 10); Poly(false, 4.8f, 7, 8, 10.2f, 11.2f, 7); Line(2.5f, 13.5f, 13.5f, 13.5f);
                    break;
                case IconKind.Pin:
                    Circle(8, 6.5f, 4.2f);
                    Line(8, 10.7f, 8, 14.5f);
                    break;
            }
        }
    }

    /// <summary>On/off switch styled entirely in USS.</summary>
    public sealed class Switch : VisualElement
    {
        bool value;
        public event Action<bool> Changed;
        public bool Value
        {
            get => value;
            set { this.value = value; EnableInClassList("switch--on", value); }
        }

        public Switch(string label, bool initial)
        {
            AddToClassList("switch");
            var track = UIX.Div("switch-track").AddTo(this);
            track.Add(UIX.Div("switch-knob"));
            if (label != null) Add(UIX.Text(label, "switch-label"));
            Value = initial;
            RegisterCallback<ClickEvent>(_ => { Value = !Value; Changed?.Invoke(Value); });
        }
    }

    /// <summary>Segmented control (radio group) with USS styling.</summary>
    public sealed class Segmented : VisualElement
    {
        readonly List<VisualElement> segs = new List<VisualElement>();
        int index;
        public event Action<int> Changed;
        public int Index
        {
            get => index;
            set { index = value; for (int i = 0; i < segs.Count; i++) segs[i].EnableInClassList("segment--active", i == value); }
        }

        public Segmented(IList<string> labels, int initial, params string[] extraClassPerIndex)
        {
            AddToClassList("segmented");
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var seg = UIX.Div("segment").AddTo(this);
                if (extraClassPerIndex != null && i < extraClassPerIndex.Length && !string.IsNullOrEmpty(extraClassPerIndex[i])) seg.AddToClassList(extraClassPerIndex[i]);
                seg.Add(UIX.Text(labels[i], "segment-label"));
                seg.RegisterCallback<ClickEvent>(_ => { Index = idx; Changed?.Invoke(idx); });
                segs.Add(seg);
            }
            Index = initial;
        }
    }

    /// <summary>Chip group used for filters (single selection).</summary>
    public sealed class ChipGroup : VisualElement
    {
        readonly List<VisualElement> chips = new List<VisualElement>();
        public event Action<int> Changed;
        int index;
        public int Index
        {
            get => index;
            set { index = value; for (int i = 0; i < chips.Count; i++) chips[i].EnableInClassList("chip--active", i == value); }
        }

        public ChipGroup(IList<string> labels, IList<Color?> dots, int initial)
        {
            AddToClassList("chip-row");
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var chip = UIX.Div("chip").AddTo(this);
                if (dots != null && i < dots.Count && dots[i].HasValue) chip.Add(UIX.ColorDot(dots[i].Value, "chip-dot"));
                chip.Add(UIX.Text(labels[i], "chip-label"));
                chip.RegisterCallback<ClickEvent>(_ => { Index = idx; Changed?.Invoke(idx); });
                chips.Add(chip);
            }
            Index = initial;
        }
    }

    /// <summary>Hover tooltips rendered on the app overlay layer.</summary>
    public static class Tooltips
    {
        static Label tip;
        static VisualElement layer;

        public static void Init(VisualElement overlay)
        {
            layer = overlay;
            tip = UIX.Text("", "tooltip");
            tip.pickingMode = PickingMode.Ignore;
            tip.style.display = DisplayStyle.None;
            overlay.Add(tip);
        }

        public static void Attach(VisualElement target, Func<string> text)
        {
            target.RegisterCallback<PointerEnterEvent>(e => ShowAt(text(), e.position));
            target.RegisterCallback<PointerMoveEvent>(e => Move(e.position));
            target.RegisterCallback<PointerLeaveEvent>(_ => Hide());
            target.RegisterCallback<DetachFromPanelEvent>(_ => Hide());
        }

        public static void ShowAt(string text, Vector2 panelPos)
        {
            if (tip == null || string.IsNullOrEmpty(text)) return;
            tip.text = text;
            tip.style.display = DisplayStyle.Flex;
            tip.BringToFront();
            Move(panelPos);
        }

        static void Move(Vector2 panelPos)
        {
            if (tip == null || tip.style.display == DisplayStyle.None) return;
            float w = float.IsNaN(tip.resolvedStyle.width) ? 200 : tip.resolvedStyle.width;
            float h = float.IsNaN(tip.resolvedStyle.height) ? 30 : tip.resolvedStyle.height;
            float lw = layer.layout.width, lh = layer.layout.height;
            float x = panelPos.x + 14, y = panelPos.y + 16;
            if (x + w > lw - 8) x = panelPos.x - w - 12;
            if (y + h > lh - 8) y = panelPos.y - h - 10;
            tip.style.left = Mathf.Max(4, x);
            tip.style.top = Mathf.Max(4, y);
        }

        public static void Hide()
        {
            if (tip != null) tip.style.display = DisplayStyle.None;
        }
    }

    /// <summary>Minimal sparkline for KPI tiles.</summary>
    public sealed class Sparkline : VisualElement
    {
        double[] values = Array.Empty<double>();
        Color color = Palette.Series1;

        public Sparkline()
        {
            AddToClassList("kpi-spark");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetData(IList<double> data, Color c)
        {
            values = data.ToArray();
            color = c;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 2 || h < 2 || values.Length < 2) return;
            double max = values.Max(), min = Math.Min(0, values.Min());
            if (max - min < 1e-9) max = min + 1;
            var pts = new Vector2[values.Length];
            for (int i = 0; i < values.Length; i++)
                pts[i] = new Vector2(w * i / (values.Length - 1), h - 2 - (float)((values[i] - min) / (max - min)) * (h - 4));
            var p = ctx.painter2D;
            var fill = color; fill.a = 0.14f;
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(0, h));
            foreach (var pt in pts) p.LineTo(pt);
            p.LineTo(new Vector2(w, h));
            p.ClosePath();
            p.Fill();
            p.strokeColor = color;
            p.lineWidth = 2f;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.Stroke();
        }
    }

    public sealed class KpiTile : VisualElement
    {
        readonly Label value, note;
        readonly Sparkline spark;

        public KpiTile(string label, bool withSpark = false, bool glass = false, bool last = false)
        {
            AddToClassList("kpi");
            if (glass) AddToClassList("kpi--glass");
            if (last) AddToClassList("kpi--last");
            Add(UIX.Text(label, "kpi-label"));
            value = UIX.Text("-", "kpi-value").AddTo(this);
            note = UIX.Text("", "kpi-note").AddTo(this);
            if (withSpark) spark = new Sparkline().AddTo(this);
        }

        public void Set(string v, string n, IList<double> sparkData = null, Color? sparkColor = null)
        {
            value.text = v;
            note.text = n ?? "";
            note.Show(!string.IsNullOrEmpty(n));
            if (spark != null && sparkData != null) spark.SetData(sparkData, sparkColor ?? Palette.Series1);
        }
    }

    public static class NiceScale
    {
        public static double Max(double v)
        {
            if (v <= 0) return 1;
            double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
            double f = v / exp;
            double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
            return nice * exp;
        }
    }

    /// <summary>Stacked column chart built from VisualElements (crisp text, native hover).</summary>
    public sealed class StackedColumns : VisualElement
    {
        readonly VisualElement plot, yaxis, xaxis, legend;

        public StackedColumns(float height)
        {
            AddToClassList("chart");
            legend = UIX.Div("legend").AddTo(this);
            var row = UIX.Div("chart-plot-row").AddTo(this);
            row.style.height = height;
            yaxis = UIX.Div("chart-yaxis").AddTo(row);
            plot = UIX.Div("chart-plot").AddTo(row);
            xaxis = UIX.Div("chart-xaxis").AddTo(this);
        }

        public void SetData(string[] labels, IList<(string name, Color color, double[] values)> series, Func<int, string> tooltip, int labelEvery = 1)
        {
            plot.Clear(); yaxis.Clear(); xaxis.Clear(); legend.Clear();
            foreach (var s in series)
            {
                var item = UIX.Div("legend-item").AddTo(legend);
                UIX.ColorDot(s.color, "legend-swatch").AddTo(item);
                item.Add(UIX.Text(s.name, "legend-label"));
            }
            int n = labels.Length;
            double max = 0;
            for (int i = 0; i < n; i++) max = Math.Max(max, series.Sum(s => s.values[i]));
            double top = NiceScale.Max(max);
            for (int k = 0; k <= 4; k++)
            {
                float pct = k / 4f;
                var grid = UIX.Div("chart-grid").AddTo(plot);
                grid.style.bottom = Length.Percent(pct * 100);
                var tick = UIX.Text(Fmt.Money(top * pct), "chart-tick").AddTo(yaxis);
                tick.style.bottom = Length.Percent(pct * 100);
                tick.style.marginBottom = -7;
            }
            var cols = UIX.Div("chart-columns").AddTo(plot);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var col = UIX.Div("chart-col").AddTo(cols);
                for (int s = series.Count - 1; s >= 0; s--)
                {
                    double v = series[s].values[i];
                    if (v <= 0) continue;
                    var seg = UIX.Div("chart-seg").AddTo(col);
                    seg.style.height = Length.Percent((float)(v / top * 100));
                    seg.style.backgroundColor = series[s].color;
                    seg.pickingMode = PickingMode.Ignore;
                }
                if (tooltip != null) Tooltips.Attach(col, () => tooltip(idx));
                var xl = UIX.Text(i % labelEvery == 0 ? labels[i] : "", "chart-xlabel").AddTo(xaxis);
            }
        }
    }

    /// <summary>Horizontal bar list (label | bar | value). Single series colour unless a per-row colour is supplied.</summary>
    public sealed class BarList : VisualElement
    {
        public BarList() { AddToClassList("bar-list"); }

        public void SetData<T>(IList<T> rows, Func<T, string> label, Func<T, double> value, Func<T, string> valueText,
            Func<T, string> extra = null, Func<T, Color?> color = null, Func<T, string> tooltip = null, Action<T> onClick = null,
            double? marker = null, float labelWidth = 150)
        {
            Clear();
            double max = rows.Count > 0 ? rows.Max(value) : 1;
            if (marker.HasValue) max = Math.Max(max, marker.Value);
            if (max <= 0) max = 1;
            foreach (var r in rows)
            {
                var row = UIX.Div("bar-row").AddTo(this);
                var l = UIX.Text(label(r), "bar-label").AddTo(row);
                l.style.width = labelWidth;
                var track = UIX.Div("bar-track").AddTo(row);
                var fill = UIX.Div("bar-fill").AddTo(track);
                fill.style.width = Length.Percent((float)Math.Max(0.5, value(r) / max * 100));
                var c = color?.Invoke(r);
                if (c.HasValue) fill.style.backgroundColor = c.Value;
                if (marker.HasValue)
                {
                    var m = UIX.Div("bar-marker").AddTo(track);
                    m.style.left = Length.Percent((float)(marker.Value / max * 100));
                }
                row.Add(UIX.Text(valueText(r), "bar-value"));
                if (extra != null) row.Add(UIX.Text(extra(r), "bar-extra"));
                if (tooltip != null) { var captured = r; Tooltips.Attach(row, () => tooltip(captured)); }
                if (onClick != null)
                {
                    var captured = r;
                    row.AddToClassList("bar-row--clickable");
                    row.RegisterCallback<ClickEvent>(_ => onClick(captured));
                }
            }
            if (rows.Count == 0) Add(UIX.Text("No data for this selection.", "muted", "small"));
        }
    }

    /// <summary>Company x category spend heatmap on a single-hue sequential ramp.</summary>
    public sealed class Heatmap : VisualElement
    {
        public void SetData(IList<string> rowLabels, IList<string> colLabels, IList<string> colTitles, double[,] values, Func<int, int, string> tooltip)
        {
            Clear();
            var header = UIX.Div("heatmap-row").AddTo(this);
            header.Add(UIX.Div("heatmap-rowlabel"));
            for (int j = 0; j < colLabels.Count; j++)
            {
                var cl = UIX.Text(colLabels[j], "heatmap-collabel").AddTo(header);
                int jj = j;
                Tooltips.Attach(cl, () => colTitles[jj]);
            }
            double max = 0;
            foreach (var v in values) max = Math.Max(max, v);
            if (max <= 0) max = 1;
            var ramp = Palette.Sequential;
            for (int i = 0; i < rowLabels.Count; i++)
            {
                var row = UIX.Div("heatmap-row").AddTo(this);
                row.Add(UIX.Text(rowLabels[i], "heatmap-rowlabel"));
                for (int j = 0; j < colLabels.Count; j++)
                {
                    double v = values[i, j];
                    // sqrt scaling keeps mid-sized cells distinguishable next to one dominant category.
                    int bin = v <= 0 ? 0 : Mathf.Clamp(1 + (int)(Math.Sqrt(v / max) * (ramp.Length - 1)), 1, ramp.Length - 1);
                    var cell = UIX.Div("heatmap-cell").AddTo(row);
                    cell.style.backgroundColor = ramp[bin];
                    if (v > 0)
                    {
                        var t = UIX.Text(Fmt.Money(v), "heatmap-value").AddTo(cell);
                        if (bin >= ramp.Length - 1) t.AddToClassList("heatmap-value--dark");
                        t.pickingMode = PickingMode.Ignore;
                    }
                    int ii = i, jj = j;
                    Tooltips.Attach(cell, () => tooltip(ii, jj));
                }
            }
            var scale = UIX.Div("scale-row").AddTo(this);
            scale.Add(UIX.Text("Less", "muted", "small"));
            var sw = UIX.Div("row").AddTo(scale);
            sw.style.marginLeft = 8; sw.style.marginRight = 8;
            foreach (var c in ramp) UIX.ColorDot(c, "scale-swatch").AddTo(sw);
            scale.Add(UIX.Text("More spend", "muted", "small"));
        }
    }

    /// <summary>Price benchmark: p25-p75 band with median, contract and requested markers.</summary>
    public sealed class RangeBar : VisualElement
    {
        public RangeBar() { AddToClassList("range"); }

        public void SetData(PriceBenchmark b)
        {
            Clear();
            if (b == null) return;
            double lo = Math.Min(b.Min, b.Requested > 0 ? b.Requested : b.Min);
            double hi = Math.Max(b.Max, b.Requested);
            if (b.ContractPrice.HasValue) { lo = Math.Min(lo, b.ContractPrice.Value); hi = Math.Max(hi, b.ContractPrice.Value); }
            double pad = (hi - lo) * 0.08 + 1;
            lo = Math.Max(0, lo - pad); hi += pad;
            float Pos(double v) => (float)((v - lo) / (hi - lo) * 100);

            Add(UIX.Div("range-track"));
            var band = UIX.Div("range-band").AddTo(this);
            band.style.left = Length.Percent(Pos(b.P25));
            band.style.width = Length.Percent(Math.Max(0.8f, Pos(b.P75) - Pos(b.P25)));

            void Marker(double v, Color c, string label, bool above)
            {
                var m = UIX.Div("range-marker").AddTo(this);
                m.style.left = Length.Percent(Pos(v));
                m.style.backgroundColor = c;
                var l = UIX.Text(label, "range-marker-label").AddTo(this);
                l.style.left = Length.Percent(Pos(v));
                l.style.top = above ? -12 : 32;
            }
            Marker(b.Median, Palette.Text1, "Median " + Fmt.Unit(b.Median), true);
            if (b.ContractPrice.HasValue) Marker(b.ContractPrice.Value, Palette.Good, "Contract " + Fmt.Unit(b.ContractPrice.Value), false);
            if (b.Requested > 0) Marker(b.Requested, b.Flagged ? Palette.Serious : Palette.Series1, "Yours " + Fmt.Unit(b.Requested), b.ContractPrice.HasValue ? true : false);
        }
    }

    /// <summary>Virtualised, sortable table on top of ListView.</summary>
    public sealed class DataTable<T> : VisualElement
    {
        public sealed class Col
        {
            public string Title;
            public float Width;
            public float Grow;
            public bool Right;
            public Func<T, string> Text;
            public Func<T, IComparable> Sort;
            public Func<T, string> Status;
            public string CellClass;
        }

        readonly List<Col> cols;
        readonly ListView list;
        readonly List<Label> headers = new List<Label>();
        List<T> items = new List<T>();
        int sortCol = -1;
        bool sortDesc = true;
        bool suppress;

        public event Action<T> Selected;
        public IReadOnlyList<T> Items => items;
        public ListView List => list;

        public DataTable(List<Col> columns, float rowHeight = 34, string emptyText = "No rows match the current filters.")
        {
            cols = columns;
            AddToClassList("table");
            var header = UIX.Div("table-header").AddTo(this);
            for (int i = 0; i < cols.Count; i++)
            {
                var c = cols[i];
                var h = UIX.Text(c.Title, "th").AddTo(header);
                ApplyWidth(h, c);
                if (c.Right) h.AddToClassList("th--right");
                if (c.Sort != null)
                {
                    int idx = i;
                    h.AddToClassList("th--sortable");
                    h.RegisterCallback<ClickEvent>(_ => SortBy(idx, sortCol == idx ? !sortDesc : true));
                }
                headers.Add(h);
            }
            list = new ListView
            {
                fixedItemHeight = rowHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = items,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            list.AddToClassList("table-body");
            list.horizontalScrollingEnabled = false;
            // Keep the scrollbar's width constant so header columns always line up with row cells.
            var sv = list.Q<ScrollView>();
            if (sv != null) sv.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            var empty = list.Q<Label>(className: "unity-list-view__empty-label");
            if (empty != null) empty.text = emptyText;
            list.selectionChanged += sel =>
            {
                if (suppress) return;
                foreach (var o in sel)
                {
                    if (o is T t) Selected?.Invoke(t);
                    break;
                }
            };
            Add(list);
        }

        static void ApplyWidth(VisualElement e, Col c)
        {
            if (c.Grow > 0)
            {
                e.style.flexGrow = c.Grow;
                e.style.flexBasis = 0;
                e.style.flexShrink = 1;
                e.style.minWidth = 40;
            }
            else
            {
                e.style.width = c.Width;
                e.style.flexShrink = 0;
            }
        }

        VisualElement MakeRow()
        {
            var row = UIX.Div("tr");
            foreach (var c in cols)
            {
                var cell = UIX.Div("td").AddTo(row);
                ApplyWidth(cell, c);
                if (c.Right) cell.AddToClassList("td--right");
                if (c.CellClass != null) cell.AddToClassList(c.CellClass);
                if (c.Status != null) cell.Add(UIX.Div("dot"));
                cell.Add(UIX.Text("", "td-label"));
            }
            return row;
        }

        void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= items.Count) return;
            var item = items[index];
            for (int i = 0; i < cols.Count; i++)
            {
                var cell = row[i];
                var label = cell.Q<Label>(className: "td-label");
                label.text = cols[i].Text(item) ?? "";
                if (cols[i].Status != null)
                {
                    var dot = cell.Q(className: "dot");
                    if (dot.userData is string prev) dot.RemoveFromClassList(prev);
                    string st = cols[i].Status(item);
                    string cls = st != null ? "dot--" + st : null;
                    if (cls != null) dot.AddToClassList(cls);
                    dot.userData = cls;
                    dot.Show(st != null);
                }
            }
        }

        public void SetItems(IEnumerable<T> source)
        {
            items = source.ToList();
            ApplySort();
            suppress = true;
            list.ClearSelection();
            list.itemsSource = items;
            list.Rebuild();
            suppress = false;
        }

        public void SortBy(int col, bool desc)
        {
            sortCol = col;
            sortDesc = desc;
            for (int i = 0; i < headers.Count; i++)
            {
                headers[i].EnableInClassList("th--sorted", i == col);
                headers[i].text = cols[i].Title + (i == col ? (desc ? "  ↓" : "  ↑") : "");
            }
            var selected = list.selectedItem;
            ApplySort();
            suppress = true;
            list.itemsSource = items;
            list.Rebuild();
            if (selected is T t)
            {
                int idx = items.IndexOf(t);
                if (idx >= 0) list.SetSelectionWithoutNotify(new[] { idx });
            }
            suppress = false;
        }

        void ApplySort()
        {
            if (sortCol < 0 || cols[sortCol].Sort == null) return;
            var key = cols[sortCol].Sort;
            items = sortDesc ? items.OrderByDescending(key).ToList() : items.OrderBy(key).ToList();
        }

        public void Select(T item, bool notify = true)
        {
            int idx = items.IndexOf(item);
            if (idx < 0) return;
            if (notify) list.SetSelection(idx);
            else list.SetSelectionWithoutNotify(new[] { idx });
            list.ScrollToItem(idx);
        }

        public void RefreshRows() => list.RefreshItems();
    }
}
