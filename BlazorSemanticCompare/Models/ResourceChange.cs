using System.Text.Json;

namespace BlazorSemanticCompare.Models
{
    public class ResourceChange
    {
        public string Address { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public JsonElement? Before { get; set; }
        public JsonElement? After { get; set; }
        public JsonElement? AfterUnknown { get; set; }
        /// <summary>Terraform's <c>action_reason</c>, e.g. replace_because_cannot_update.</summary>
        public string? ActionReason { get; set; }
        /// <summary>Attribute paths from <c>change.replace_paths</c> that force replacement.</summary>
        public List<string> ReplacePaths { get; set; } = new();

        public bool IsReplace => string.Equals(Action, "replace", StringComparison.OrdinalIgnoreCase);

        public string? GetReplaceReasonText() => ActionReason switch
        {
            null or "" => null,
            "replace_because_cannot_update" => "cannot be updated in place",
            "replace_because_tainted" => "resource is tainted",
            "replace_by_request" => "requested via -replace",
            "replace_by_triggers" or "replace_triggered_by" => "replace_triggered_by",
            var other => other
        };

        /// <summary>Top-level property names touched by <see cref="ReplacePaths"/>.</summary>
        public bool ForcesReplacement(string propertyName) =>
            ReplacePaths.Any(p => p == propertyName || p.StartsWith(propertyName + ".") || p.StartsWith(propertyName + "["));

        public string GetDisplayPath()
        {
            if (string.IsNullOrEmpty(Address)) return string.Empty;
            // Split on '.' maar houd [index] als onderdeel van het voorgaande deel
            var parts = new List<string>();
            var current = "";
            foreach (var c in Address)
            {
                if (c == '.')
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        parts.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }
            if (!string.IsNullOrEmpty(current)) parts.Add(current);
            return string.Join(" > ", parts);
        }
    }
}