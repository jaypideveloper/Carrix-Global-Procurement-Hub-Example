using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProcurementHub.UI
{
    /// <summary>Small builder helpers so pages read as layout rather than boilerplate.</summary>
    public static class UIX
    {
        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) l.AddToClassList(c);
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            return b;
        }

        public static T Cls<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
            return e;
        }

        public static T AddTo<T>(this T e, VisualElement parent) where T : VisualElement
        {
            parent.Add(e);
            return e;
        }

        public static VisualElement Kids(this VisualElement e, params VisualElement[] children)
        {
            foreach (var c in children) if (c != null) e.Add(c);
            return e;
        }

        public static void Show(this VisualElement e, bool show) => e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        public static void Toggle(this VisualElement e, string cls, bool on) => e.EnableInClassList(cls, on);

        public static ScrollView Scroll(params string[] classes)
        {
            var s = new ScrollView(ScrollViewMode.Vertical);
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.verticalScrollerVisibility = ScrollerVisibility.Auto;
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) s.AddToClassList(c);
            return s;
        }

        public static VisualElement Card(string title, string subtitle, out VisualElement body, params string[] classes)
        {
            var card = Div("card");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) card.AddToClassList(c);
            if (title != null)
            {
                var head = Div("card-header").AddTo(card);
                var titles = Div("card-titles").AddTo(head);
                titles.Add(Text(title, "card-title"));
                if (!string.IsNullOrEmpty(subtitle)) titles.Add(Text(subtitle, "card-subtitle"));
            }
            body = Div().AddTo(card);
            return card;
        }

        public static VisualElement CardHeaderRight(VisualElement card)
        {
            var head = card.Q(className: "card-header");
            var right = Div("row-center");
            head?.Add(right);
            return right;
        }

        public static VisualElement Dot(string statusClass)
        {
            var d = Div("dot");
            if (statusClass != null) d.AddToClassList(statusClass);
            return d;
        }

        public static VisualElement ColorDot(Color c, string cls = "dot")
        {
            var d = Div(cls);
            d.style.backgroundColor = c;
            return d;
        }

        public static VisualElement Pill(string text, string status = null)
        {
            var p = Div("pill");
            if (status != null)
            {
                p.AddToClassList("pill--" + status);
                p.Add(Dot("dot--" + status));
            }
            p.Add(Text(text, "pill-label"));
            return p;
        }

        public static VisualElement KV(string key, string value)
        {
            var row = Div("kv");
            row.Add(Text(key, "kv-key"));
            row.Add(Text(value, "kv-value"));
            return row;
        }

        public static VisualElement Bullet(string text, bool risk = false)
        {
            var row = Div("bullet");
            if (risk) row.AddToClassList("bullet--risk");
            row.Add(Text(risk ? "!" : "-", "bullet-mark"));
            row.Add(Text(text, "bullet-text"));
            return row;
        }

        public static VisualElement Field(string label, VisualElement input, string hint = null)
        {
            var f = Div("field");
            f.Add(Text(label, "field-label"));
            f.Add(input);
            if (hint != null) f.Add(Text(hint, "field-hint"));
            return f;
        }

        public static TextField Input(string placeholder = null, bool multiline = false)
        {
            var t = new TextField { multiline = multiline };
            t.AddToClassList("hub-input");
            if (multiline) t.AddToClassList("hub-input--multiline");
            if (placeholder != null)
            {
                t.textEdition.placeholder = placeholder;
                t.textEdition.hidePlaceholderOnFocus = false;
            }
            return t;
        }

        public static DropdownField Dropdown(List<string> choices, int index = 0)
        {
            var d = new DropdownField(choices, Mathf.Clamp(index, 0, Math.Max(0, choices.Count - 1)));
            d.AddToClassList("hub-input");
            return d;
        }

        public static VisualElement EmptyState(string title, string subtitle)
        {
            var e = Div("empty-state");
            e.Add(Text(title, "empty-title"));
            if (subtitle != null) e.Add(Text(subtitle, "empty-sub"));
            return e;
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0].Substring(0, 1).ToUpperInvariant()
                : (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }

        public static VisualElement Avatar(string name, bool accent = false)
        {
            var a = Div("avatar");
            if (accent) a.AddToClassList("avatar--accent");
            a.Add(Text(Initials(name), "avatar-label"));
            return a;
        }

        public static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var clean = text.Replace("$", "").Replace(",", "").Trim();
            double mult = 1;
            if (clean.EndsWith("k", StringComparison.OrdinalIgnoreCase)) { mult = 1e3; clean = clean.Substring(0, clean.Length - 1); }
            else if (clean.EndsWith("m", StringComparison.OrdinalIgnoreCase)) { mult = 1e6; clean = clean.Substring(0, clean.Length - 1); }
            if (!double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            value *= mult;
            return true;
        }

        public static string SeverityClass(Severity s)
        {
            switch (s)
            {
                case Severity.Critical: return "critical";
                case Severity.Serious: return "serious";
                case Severity.Warning: return "warning";
                default: return "info";
            }
        }

        public static string StageStatus(RequestStage s)
        {
            switch (s)
            {
                case RequestStage.Closed: return "good";
                case RequestStage.Rejected: return "critical";
                case RequestStage.Intake:
                case RequestStage.Routed: return "warning";
                default: return "info";
            }
        }
    }

    public static class Palette
    {
        public static readonly Color Series1 = new Color32(0x39, 0x87, 0xE5, 0xFF);
        public static readonly Color Series2 = new Color32(0xD9, 0x59, 0x26, 0xFF);
        public static readonly Color Series3 = new Color32(0x19, 0x9E, 0x70, 0xFF);
        public static readonly Color Series4 = new Color32(0xC9, 0x85, 0x00, 0xFF);
        public static readonly Color Neutral = new Color32(0xC3, 0xC2, 0xB7, 0xFF);
        public static readonly Color Text1 = new Color32(0xF3, 0xF6, 0xFA, 0xFF);
        public static readonly Color Text3 = new Color32(0x7F, 0x8B, 0xA0, 0xFF);
        public static readonly Color Good = new Color32(0x0C, 0xA3, 0x0C, 0xFF);
        public static readonly Color Warning = new Color32(0xFA, 0xB2, 0x19, 0xFF);
        public static readonly Color Serious = new Color32(0xEC, 0x83, 0x5A, 0xFF);
        public static readonly Color Critical = new Color32(0xD0, 0x3B, 0x3B, 0xFF);

        /// <summary>Single-hue blue ramp for magnitude (dark-mode steps; near-zero recedes toward the surface).</summary>
        public static readonly Color[] Sequential =
        {
            new Color32(0x15, 0x20, 0x33, 0xFF), new Color32(0x10, 0x42, 0x81, 0xFF), new Color32(0x18, 0x4F, 0x95, 0xFF),
            new Color32(0x1C, 0x5C, 0xAB, 0xFF), new Color32(0x25, 0x6A, 0xBF, 0xFF), new Color32(0x2A, 0x78, 0xD6, 0xFF),
            new Color32(0x55, 0x98, 0xE7, 0xFF), new Color32(0x86, 0xB6, 0xEF, 0xFF),
        };

        public static Color Unit(BusinessUnit u) => Globe.GlobeController.UnitColor(u);
    }
}
