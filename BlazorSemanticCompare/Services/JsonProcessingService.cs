using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;

namespace BlazorSemanticCompare.Services
{
    public enum DiffKind { Unchanged, Added, Removed, Modified }

    public class JsonDiffNode
    {
        public string? PropertyName { get; set; }
        public DiffKind Kind { get; set; }
        public JsonElement? Before { get; set; }
        public JsonElement? After { get; set; }
        public List<JsonDiffNode>? Children { get; set; }
        public JsonValueKind? OriginalKind { get; set; }  // Track if this was originally an Array or Object
    }

    public class JsonProcessingService
    {
        public JsonDiffNode Compare(JsonElement? before, JsonElement? after, string? propertyName, bool treatEmptyAsNull)
        {
            if (!before.HasValue && !after.HasValue)
                return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Unchanged };
            if (!before.HasValue)
            {
                // Handle Added node: if it's an object or array, fill Children
                if (after.HasValue && (after.Value.ValueKind == JsonValueKind.Object || after.Value.ValueKind == JsonValueKind.Array))
                {
                    return CompareObjectOrArray(null, after, propertyName, DiffKind.Added, treatEmptyAsNull);
                }                
                return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Added, After = after };
            }
            if (!after.HasValue)
            {
                // Handle Removed node: if it's an object or array, fill Children
                if (before.HasValue && (before.Value.ValueKind == JsonValueKind.Object || before.Value.ValueKind == JsonValueKind.Array))
                {
                    return CompareObjectOrArray(before, null, propertyName, DiffKind.Removed, treatEmptyAsNull);
                }                
                return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Removed, Before = before };
            }

            var b = before.Value;
            var a = after.Value;

            // TreatEmptyAsNull logic: check if one is null and the other is an empty string
            if (treatEmptyAsNull)
            {
                bool bIsNullOrEmpty = (b.ValueKind == JsonValueKind.Null) || (b.ValueKind == JsonValueKind.String && b.GetString() == string.Empty);
                bool aIsNullOrEmpty = (a.ValueKind == JsonValueKind.Null) || (a.ValueKind == JsonValueKind.String && a.GetString() == string.Empty);
                
                if (bIsNullOrEmpty && aIsNullOrEmpty)
                {
                    return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Unchanged, Before = before, After = after };
                }
            }

            if (b.ValueKind != a.ValueKind)
            {
                return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Modified, Before = before, After = after };
            }

            switch (b.ValueKind)
            {
                case JsonValueKind.Object:
                    var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p);
                    var aProps = a.EnumerateObject().ToDictionary(p => p.Name, p => p);
                    var allKeys = bProps.Keys.Union(aProps.Keys).OrderBy(x => x).ToList();
                    var objectChildren = new List<JsonDiffNode>();
                    foreach (var key in allKeys)
                    {
                        bProps.TryGetValue(key, out var bVal);
                        aProps.TryGetValue(key, out var aVal);
                        objectChildren.Add(Compare(bProps.ContainsKey(key) ? bVal.Value : (JsonElement?)null, aProps.ContainsKey(key) ? aVal.Value : (JsonElement?)null, key, treatEmptyAsNull));
                    }
                    var objectKind = objectChildren.All(c => c.Kind == DiffKind.Unchanged) ? DiffKind.Unchanged : DiffKind.Modified;
                    return new JsonDiffNode { PropertyName = propertyName, Kind = objectKind, Children = objectChildren, Before = before, After = after, OriginalKind = JsonValueKind.Object };
                case JsonValueKind.Array:
                    if (b.GetArrayLength() == 0 && a.GetArrayLength() == 0)
                        return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Unchanged, Before = before, After = after, OriginalKind = JsonValueKind.Array };
                    // Array of strings: sort and compare, always show all items
                    if (b.GetArrayLength() > 0 && b[0].ValueKind == JsonValueKind.String && a.GetArrayLength() > 0 && a[0].ValueKind == JsonValueKind.String)
                    {
                        var bArr = b.EnumerateArray().Select(x => x.GetString()).ToList();
                        var aArr = a.EnumerateArray().Select(x => x.GetString()).ToList();
                        var allValues = bArr.Union(aArr).OrderBy(x => x).ToList();
                        var stringDiffChildren = new List<JsonDiffNode>();
                        foreach (var val in allValues)
                        {
                            var inBefore = bArr.Contains(val);
                            var inAfter = aArr.Contains(val);
                            DiffKind kindStr = inBefore && inAfter ? DiffKind.Unchanged : inBefore ? DiffKind.Removed : DiffKind.Added;
                            stringDiffChildren.Add(new JsonDiffNode { PropertyName = val, Kind = kindStr });
                        }
                        var stringArrayKind = stringDiffChildren.All(c => c.Kind == DiffKind.Unchanged) ? DiffKind.Unchanged : DiffKind.Modified;
                        return new JsonDiffNode { PropertyName = propertyName, Kind = stringArrayKind, Children = stringDiffChildren, Before = before, After = after, OriginalKind = JsonValueKind.Array };
                    }
                    // Array of primitives (numbers, booleans, etc.): treat like strings, use value as name
                    if (b.GetArrayLength() > 0 && IsPrimitive(b[0].ValueKind) && a.GetArrayLength() > 0 && IsPrimitive(a[0].ValueKind))
                    {
                        var bArr = b.EnumerateArray().Select(x => x.ToString()).ToList();
                        var aArr = a.EnumerateArray().Select(x => x.ToString()).ToList();
                        var allValues = bArr.Union(aArr).OrderBy(x => x).ToList();
                        var primitiveDiffChildren = new List<JsonDiffNode>();
                        foreach (var val in allValues)
                        {
                            var inBefore = bArr.Contains(val);
                            var inAfter = aArr.Contains(val);
                            DiffKind kindPrim = inBefore && inAfter ? DiffKind.Unchanged : inBefore ? DiffKind.Removed : DiffKind.Added;
                            primitiveDiffChildren.Add(new JsonDiffNode { PropertyName = val, Kind = kindPrim });
                        }
                        var primitiveArrayKind = primitiveDiffChildren.All(c => c.Kind == DiffKind.Unchanged) ? DiffKind.Unchanged : DiffKind.Modified;
                        return new JsonDiffNode { PropertyName = propertyName, Kind = primitiveArrayKind, Children = primitiveDiffChildren, Before = before, After = after, OriginalKind = JsonValueKind.Array };
                    }
                    // Array of objects with a unique 'name' (or else 'id') property: match by that key
                    var keyProp = FindArrayKeyProperty(b, a);
                    if (keyProp != null)
                    {
                        var bByKey = b.EnumerateArray().ToDictionary(x => x.GetProperty(keyProp).GetString()!);
                        var aByKey = a.EnumerateArray().ToDictionary(x => x.GetProperty(keyProp).GetString()!);
                        var allKeys2 = bByKey.Keys.Union(aByKey.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
                        var keyDiffChildren = new List<JsonDiffNode>();
                        foreach (var key in allKeys2)
                        {
                            JsonElement? bObj = bByKey.TryGetValue(key, out var bv) ? bv : null;
                            JsonElement? aObj = aByKey.TryGetValue(key, out var av) ? av : null;
                            keyDiffChildren.Add(Compare(bObj, aObj, key, treatEmptyAsNull));
                        }
                        var keyArrayKind = keyDiffChildren.All(c => c.Kind == DiffKind.Unchanged) ? DiffKind.Unchanged : DiffKind.Modified;
                        return new JsonDiffNode { PropertyName = propertyName, Kind = keyArrayKind, Children = keyDiffChildren, Before = before, After = after, OriginalKind = JsonValueKind.Array };
                    }                    // Default: compare by index
                    var bList = b.EnumerateArray().ToList();
                    var aList = a.EnumerateArray().ToList();
                    var maxLen = Math.Max(bList.Count, aList.Count);
                    var arrChildren = new List<JsonDiffNode>();
                    for (int i = 0; i < maxLen; i++)
                    {
                        arrChildren.Add(Compare(i < bList.Count ? bList[i] : (JsonElement?)null, i < aList.Count ? aList[i] : (JsonElement?)null, $"[{i}]", treatEmptyAsNull));
                    }
                    var arrKind = arrChildren.All(c => c.Kind == DiffKind.Unchanged) ? DiffKind.Unchanged : DiffKind.Modified;
                    return new JsonDiffNode { PropertyName = propertyName, Kind = arrKind, Children = arrChildren, Before = before, After = after, OriginalKind = JsonValueKind.Array };
                default:
                    if (b.ToString() == a.ToString())
                        return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Unchanged, Before = before, After = after };
                    return new JsonDiffNode { PropertyName = propertyName, Kind = DiffKind.Modified, Before = before, After = after };
            }
        }

        private static readonly string[] ArrayKeyProperties = { "name", "id" };

        private static string? FindArrayKeyProperty(JsonElement b, JsonElement a)
        {
            foreach (var prop in ArrayKeyProperties)
            {
                if (HasUniqueStringKey(b, prop) && HasUniqueStringKey(a, prop))
                    return prop;
            }
            return null;
        }

        private static bool HasUniqueStringKey(JsonElement array, string prop)
        {
            if (array.GetArrayLength() == 0) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.String)
                    return false;
                if (!seen.Add(v.GetString()!)) return false;
            }
            return true;
        }

        private bool IsPrimitive(JsonValueKind kind) =>
            kind == JsonValueKind.String ||
            kind == JsonValueKind.Number ||
            kind == JsonValueKind.True ||
            kind == JsonValueKind.False ||
            kind == JsonValueKind.Null;

        private JsonDiffNode CompareObjectOrArray(JsonElement? before, JsonElement? after, string? propertyName, DiffKind kind, bool treatEmptyAsNull)
        {
            if (after.HasValue && after.Value.ValueKind == JsonValueKind.Object)
            {
                var a = after.Value;
                var children = a.EnumerateObject().Select(p => Compare(null, p.Value, p.Name, treatEmptyAsNull)).ToList();
                return new JsonDiffNode { PropertyName = propertyName, Kind = kind, After = after, Children = children, OriginalKind = JsonValueKind.Object };
            }
            if (before.HasValue && before.Value.ValueKind == JsonValueKind.Object)
            {
                var b = before.Value;
                var children = b.EnumerateObject().Select(p => Compare(p.Value, null, p.Name, treatEmptyAsNull)).ToList();
                return new JsonDiffNode { PropertyName = propertyName, Kind = kind, Before = before, Children = children, OriginalKind = JsonValueKind.Object };
            }
            if (after.HasValue && after.Value.ValueKind == JsonValueKind.Array)
            {
                var a = after.Value;
                var children = a.EnumerateArray().Select((item, idx) => Compare(null, item, $"[{idx}]", treatEmptyAsNull)).ToList();
                return new JsonDiffNode { PropertyName = propertyName, Kind = kind, After = after, Children = children, OriginalKind = JsonValueKind.Array };
            }
            if (before.HasValue && before.Value.ValueKind == JsonValueKind.Array)
            {
                var b = before.Value;
                var children = b.EnumerateArray().Select((item, idx) => Compare(item, null, $"[{idx}]", treatEmptyAsNull)).ToList();
                return new JsonDiffNode { PropertyName = propertyName, Kind = kind, Before = before, Children = children, OriginalKind = JsonValueKind.Array };
            }
            // fallback
            return new JsonDiffNode { PropertyName = propertyName, Kind = kind, Before = before, After = after };
        }
    }
}
