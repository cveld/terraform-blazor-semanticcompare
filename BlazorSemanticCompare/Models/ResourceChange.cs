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