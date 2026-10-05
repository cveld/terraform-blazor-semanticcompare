using System.Text;
using System.Text.Json;
using BlazorSemanticCompare.Models;

namespace BlazorSemanticCompare.Helpers
{
    public static class ResourceChangeExtractor
    {
        /// <summary>Extracts all non-"no-op" resource changes from a Terraform plan JSON document.</summary>
        public static List<ResourceChange> Extract(JsonDocument? doc)
        {
            var result = new List<ResourceChange>();
            if (doc == null) return result;
            if (!doc.RootElement.TryGetProperty("resource_changes", out var changesArray))
                return result;
            foreach (var item in changesArray.EnumerateArray())
            {
                if (!item.TryGetProperty("change", out var changeObj)) continue;
                if (!changeObj.TryGetProperty("actions", out var actionsArray)) continue;
                if (actionsArray.GetArrayLength() == 0) continue;

                var actionsList = actionsArray.EnumerateArray().Select(a => a.GetString()).ToList();
                var action = (actionsList.Contains("delete") && actionsList.Contains("create"))
                    ? "replace"
                    : actionsArray[0].GetString();

                if (string.Equals(action, "no-op", StringComparison.OrdinalIgnoreCase)) continue;

                var type = item.TryGetProperty("type", out var t) ? t.GetString() : "?";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : "?";
                var address = item.TryGetProperty("address", out var addressProp)
                    ? addressProp.GetString() ?? string.Empty
                    : string.Empty;

                JsonElement? before = null, after = null, afterUnknown = null;
                if (changeObj.TryGetProperty("before", out var beforeVal)) before = beforeVal;
                if (changeObj.TryGetProperty("after", out var afterVal)) after = afterVal;
                if (changeObj.TryGetProperty("after_unknown", out var afterUnknownVal)) afterUnknown = afterUnknownVal;

                var actionReason = item.TryGetProperty("action_reason", out var ar) && ar.ValueKind == JsonValueKind.String
                    ? ar.GetString()
                    : null;

                result.Add(new ResourceChange
                {
                    Address = address,
                    Type = type ?? "?",
                    Name = name ?? "?",
                    Action = action ?? "?",
                    Before = before,
                    After = after,
                    AfterUnknown = afterUnknown,
                    ActionReason = actionReason,
                    ReplacePaths = ReadReplacePaths(changeObj)
                });
            }
            return result;
        }

        /// <summary>Formats <c>change.replace_paths</c> as dotted paths, e.g. <c>network_interface[0].subnet_id</c>.</summary>
        private static List<string> ReadReplacePaths(JsonElement changeObj)
        {
            var paths = new List<string>();
            if (!changeObj.TryGetProperty("replace_paths", out var rp) || rp.ValueKind != JsonValueKind.Array)
                return paths;

            foreach (var path in rp.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Array))
            {
                var sb = new StringBuilder();
                foreach (var seg in path.EnumerateArray())
                {
                    if (seg.ValueKind == JsonValueKind.Number) sb.Append('[').Append(seg.GetRawText()).Append(']');
                    else sb.Append(sb.Length > 0 ? "." : "").Append(seg.GetString());
                }
                paths.Add(sb.ToString());
            }
            return paths;
        }
    }
}
