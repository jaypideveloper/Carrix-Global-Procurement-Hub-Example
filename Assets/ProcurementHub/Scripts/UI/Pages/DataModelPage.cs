using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    public sealed class DataModelPage : HubPage
    {
        public override string Id => "datamodel";
        public override string Title => "Data Model & SQL";
        public override string Subtitle => "The relational model behind the hub and the SQL for every metric - exportable to PostgreSQL with the full synthetic dataset";

        VisualElement queryList, preview;
        Label sqlText, questionText, exportPath;
        Button openFolder;
        int selectedQuery;
        string lastExport;

        public DataModelPage(HubApp app) : base(app) { }

        protected override void Build()
        {
            Root.AddToClassList("row");
            var left = UIX.Scroll("side-scroll", "cc-left").AddTo(Root);
            left.style.width = 340;
            var lc = left.contentContainer;
            lc.Add(UIX.Text("SCHEMA · " + SqlExporter.Tables.Length + " TABLES", "section-label"));
            foreach (var t in SqlExporter.Tables)
            {
                var card = UIX.Div("table-card").AddTo(lc);
                var head = UIX.Div("row-center").AddTo(card);
                head.Add(UIX.Text(t.Name, "table-name", "grow"));
                head.Add(UIX.Text(Fmt.Num(t.Rows(Db)) + " rows", "muted", "small"));
                card.Add(UIX.Text(t.Purpose, "muted", "small"));
                card.Add(UIX.Text(string.Join("  ·  ", t.Columns), "table-cols"));
            }

            var scroll = UIX.Scroll("page-scroll", "grow").AddTo(Root);
            var c = scroll.contentContainer;

            var ex = UIX.Card("Export", "Writes schema, seed data (every row in the hub, including your requests), the analytic queries and a CSV of purchase lines", out var eb).AddTo(c);
            ex.AddToClassList("gap-bottom");
            var er = UIX.Div("btn-row").AddTo(eb);
            er.Add(UIX.Btn("Export SQL + CSV", Export, "btn--primary"));
            openFolder = UIX.Btn("Open folder", () => { if (lastExport != null) Application.OpenURL("file:///" + lastExport.Replace('\\', '/')); });
            openFolder.SetEnabled(false);
            er.Add(openFolder);
            exportPath = UIX.Text("Files: 01_schema.sql · 02_seed.sql · 03_analytics_queries.sql · purchase_lines.csv", "field-hint").AddTo(eb);

            var row = UIX.Div("row").AddTo(c);
            var qCard = UIX.Card("Analytic queries", "Each dashboard metric, as SQL", out var qb).AddTo(row);
            qCard.style.width = 300; qCard.style.flexShrink = 0; qCard.AddToClassList("gap-right");
            queryList = qb;

            var right = UIX.Div("grow").AddTo(row);
            right.style.flexBasis = 0;
            var sCard = UIX.Card("SQL", null, out var sb).AddTo(right);
            questionText = UIX.Text("", "body-text").AddTo(sb);
            questionText.style.marginBottom = 10;
            var code = UIX.Div("code-block").AddTo(sb);
            sqlText = UIX.Text("", "code-text").AddTo(code);
            sqlText.selection.isSelectable = true;
            var pCard = UIX.Card("Result preview", "Computed live by the hub's C# equivalent of the query", out var pb).AddTo(right);
            pCard.AddToClassList("mt-16");
            preview = pb;
        }

        public override void OnShow(object arg)
        {
            if (arg is int i && i >= 0 && i < SqlExporter.Queries.Length) selectedQuery = i;
            RenderQueries();
            RenderQuery();
        }

        void RenderQueries()
        {
            queryList.Clear();
            for (int i = 0; i < SqlExporter.Queries.Length; i++)
            {
                var q = SqlExporter.Queries[i];
                var item = UIX.Div("query-item").AddTo(queryList);
                if (i == selectedQuery) item.AddToClassList("query-item--active");
                item.Add(UIX.Text(q.Title, "query-title"));
                item.Add(UIX.Text(q.Question, "query-question"));
                int idx = i;
                item.RegisterCallback<ClickEvent>(_ => { selectedQuery = idx; RenderQueries(); RenderQuery(); });
            }
        }

        void RenderQuery()
        {
            var q = SqlExporter.Queries[selectedQuery];
            questionText.text = q.Question;
            sqlText.text = q.Sql;
            preview.Clear();
            var (headers, rows) = q.Preview(Db, Svc.Analytics);
            var cols = new List<DataTable<string[]>.Col>();
            for (int i = 0; i < headers.Length; i++)
            {
                int idx = i;
                bool numeric = rows.Count > 0 && rows.Take(5).All(r => r[idx].StartsWith("$") || r[idx].EndsWith("%") || double.TryParse(r[idx].Replace(",", ""), out _));
                cols.Add(new DataTable<string[]>.Col
                {
                    Title = headers[i],
                    Grow = numeric ? 0.8f : 1.4f,
                    Right = numeric,
                    Text = r => r[idx],
                    Sort = r => numeric ? (IComparable)ParseNum(r[idx]) : r[idx],
                });
            }
            var table = new DataTable<string[]>(cols, 30).AddTo(preview);
            table.style.height = Math.Min(460, 40 + rows.Count * 30);
            table.style.flexGrow = 0;
            table.SetItems(rows);
            preview.Add(UIX.Text(rows.Count + " rows", "muted", "small", "mt-8"));
        }

        static double ParseNum(string s)
        {
            var clean = s.Replace("$", "").Replace(",", "").Replace("%", "").Trim();
            int slash = clean.IndexOf('/');
            if (slash > 0) clean = clean.Substring(0, slash).Trim();
            return double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        void Export()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "Export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                SqlExporter.ExportAll(Db, folder);
                lastExport = folder;
                exportPath.text = "Exported to " + folder;
                openFolder.SetEnabled(true);
                App.Toast("Exported schema, " + Fmt.Num(Db.Lines.Count) + " purchase lines and " + Db.Requests.Count + " requests.", Severity.Info);
            }
            catch (Exception ex)
            {
                App.Toast("Export failed: " + ex.Message, Severity.Critical);
            }
        }
    }
}
