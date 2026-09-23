using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ProcurementHub.UI
{
    /// <summary>
    /// Optional natural-language explanations from Claude via the Messages API (raw HTTP; the C# SDK is not
    /// a practical dependency inside Unity). Enabled only when ANTHROPIC_API_KEY is set. The assistant only
    /// explains - it never changes a request; every decision still needs a human click.
    /// </summary>
    public static class ClaudeAssistant
    {
        const string Endpoint = "https://api.anthropic.com/v1/messages";
        const string Model = "claude-opus-5";

        const string SystemPrompt =
            "You are the procurement assistant inside a Global Procurement Operations Hub for a network of marine terminals, " +
            "rail/intermodal operations and a terminal-software company. You receive a structured summary of one purchase request " +
            "and the hub's deterministic evaluation (engagement rules, routing, contracts, price benchmark). Answer the user's " +
            "question in plain business English in at most 150 words. Ground every statement in the data provided; if the data " +
            "does not answer the question, say so. You do not approve or change anything - a human decides.";

        [Serializable] class Msg { public string role; public string content; }
        [Serializable] class OutputConfig { public string effort; }
        [Serializable] class Req
        {
            public string model;
            public int max_tokens;
            public string system;
            public string fallbacks;
            public OutputConfig output_config;
            public List<Msg> messages;
        }
        [Serializable] class Block { public string type; public string text; }
        [Serializable] class Resp { public List<Block> content; public string stop_reason; }
        [Serializable] class ErrBody { public string type; public string message; }
        [Serializable] class Err { public ErrBody error; }

        static string ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        public static bool IsConfigured => !string.IsNullOrEmpty(ApiKey);

        public static IEnumerator Ask(string context, string question, Action<string> onAnswer, Action<string> onError)
        {
            var body = new Req
            {
                model = Model,
                max_tokens = 16000,
                system = SystemPrompt,
                fallbacks = "default",
                output_config = new OutputConfig { effort = "low" },
                messages = new List<Msg> { new Msg { role = "user", content = "Request data:\n" + context + "\n\nQuestion: " + question } },
            };
            var json = JsonUtility.ToJson(body);
            using (var req = new UnityWebRequest(Endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("content-type", "application/json");
                req.SetRequestHeader("x-api-key", ApiKey);
                req.SetRequestHeader("anthropic-version", "2023-06-01");
                req.SetRequestHeader("anthropic-beta", "server-side-fallback-2026-07-01");
                req.timeout = 120;
                yield return req.SendWebRequest();

                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string msg = req.error;
                    try
                    {
                        var err = string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<Err>(text);
                        if (err?.error != null && !string.IsNullOrEmpty(err.error.message)) msg = err.error.type + ": " + err.error.message;
                    }
                    catch (Exception) { }
                    onError?.Invoke(msg);
                    yield break;
                }

                Resp resp = null;
                try { resp = JsonUtility.FromJson<Resp>(text); }
                catch (Exception ex) { onError?.Invoke("Could not parse the response: " + ex.Message); yield break; }

                if (resp == null) { onError?.Invoke("Empty response."); yield break; }
                if (resp.stop_reason == "refusal") { onError?.Invoke("The model declined to answer this question."); yield break; }
                var sb = new StringBuilder();
                if (resp.content != null)
                    foreach (var b in resp.content)
                        if (b.type == "text" && !string.IsNullOrEmpty(b.text)) sb.Append(b.text);
                if (resp.stop_reason == "max_tokens") sb.Append(" [truncated]");
                onAnswer?.Invoke(sb.Length > 0 ? sb.ToString().Trim() : "(No text returned.)");
            }
        }

        /// <summary>Compact, factual summary of an evaluation for the model to reason over.</summary>
        public static string Describe(HubDatabase db, RequestDraft d, Evaluation ev)
        {
            var sb = new StringBuilder();
            var loc = db.Loc(d.LocationId);
            sb.AppendLine("Site: " + (loc != null ? loc.Name + " (" + loc.Company.Name + ", " + Labels.Site(loc.Type) + ")" : "unknown"));
            sb.AppendLine("Description: " + d.Description);
            sb.AppendLine("Quantity x unit price: " + d.Quantity + " x " + Fmt.Unit(d.UnitPrice) + " = " + Fmt.MoneyExact(d.Total));
            sb.AppendLine("Urgency: " + d.Urgency + "; requires contract: " + d.RequiresContract + "; supplier requested: " + (string.IsNullOrWhiteSpace(d.SupplierText) ? "none" : d.SupplierText));
            if (ev.Sub != null) sb.AppendLine("Classification: " + ev.Sub.Category.Name + " / " + ev.Sub.Name + " (" + Fmt.Pct(ev.Confidence) + ")");
            sb.AppendLine("Engagement decision: " + ev.Engagement.Decision + ". " + ev.Engagement.Summary);
            foreach (var r in ev.Engagement.Rules) sb.AppendLine("  " + r.Title + ": " + (r.Triggered ? "TRIGGERED" : "not triggered") + " - " + r.Detail);
            if (ev.Routing.Manager != null) sb.AppendLine("Routed to: " + ev.Routing.Manager.Name + " (" + ev.Routing.Manager.Title + ")");
            foreach (var a in ev.Routing.Approvals) sb.AppendLine("  Approval: " + a.Role + " - " + a.Name + " (" + a.Why + ")");
            foreach (var c in ev.Contracts) sb.AppendLine("Contract option: " + c.Contract.Id + " " + c.Supplier.Name + ", " + c.Coverage + (c.UnitPrice.HasValue ? ", " + Fmt.Unit(c.UnitPrice.Value) : "") + ", " + c.DaysLeft + " days left");
            if (ev.Benchmark != null) sb.AppendLine("Price benchmark (" + ev.Benchmark.Samples + " purchases): median " + Fmt.Unit(ev.Benchmark.Median) + ", p25 " + Fmt.Unit(ev.Benchmark.P25) + ", p75 " + Fmt.Unit(ev.Benchmark.P75) + (ev.Benchmark.ContractPrice.HasValue ? ", contract " + Fmt.Unit(ev.Benchmark.ContractPrice.Value) : ""));
            sb.AppendLine("Hub recommendation: " + ev.Recommendation.Headline);
            foreach (var r in ev.Recommendation.Risks) sb.AppendLine("  Risk: " + r);
            return sb.ToString();
        }
    }
}
