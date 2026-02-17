using System.Text.Json;

namespace BlazorSemanticCompare.Helpers;

public static class JsonComputedMerger
{
    // Use a special marker that won't be escaped
    private const string COMPUTED_MARKER = "<<<COMPUTED>>>";
    
    /// <summary>
    /// Combineert after en afterUnknown, waarbij properties waarvoor afterUnknown true is, 
    /// vervangen worden door de string "&lt;computed&gt;".
    /// </summary>
    public static object? MergeAfterWithComputed(JsonElement? after, JsonElement? afterUnknown)
    {
        if (!after.HasValue)
        {
            // Als er geen 'after' is maar wel 'afterUnknown', toon dan de computed properties
            if (afterUnknown.HasValue && afterUnknown.Value.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in afterUnknown.Value.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True)
                    {
                        dict[prop.Name] = COMPUTED_MARKER;
                    }
                }
                return dict;
            }
            return null;
        }
        
        if (after.Value.ValueKind == JsonValueKind.Array)
        {
            // Special handling for arrays at the top level
            if (afterUnknown.HasValue && afterUnknown.Value.ValueKind == JsonValueKind.Array)
            {
                var arr = new List<object?>();
                int idx = 0;
                foreach (var item in after.Value.EnumerateArray())
                {
                    if (afterUnknown.Value.GetArrayLength() > idx && afterUnknown.Value[idx].ValueKind == JsonValueKind.True)
                        arr.Add(COMPUTED_MARKER);
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        JsonElement? itemUnknown = null;
                        if (afterUnknown.Value.GetArrayLength() > idx && afterUnknown.Value[idx].ValueKind == JsonValueKind.Object)
                        {
                            itemUnknown = afterUnknown.Value[idx];
                        }
                        arr.Add(MergeAfterWithComputed(item, itemUnknown));
                    }
                    else
                        arr.Add(item);
                    idx++;
                }
                return arr;
            }
            return after.Value;
        }
        
        if (after.Value.ValueKind != JsonValueKind.Object)
            return after.Value;
            
        var resultDict = new Dictionary<string, object?>();
        
        // First, collect all property names from both 'after' and 'afterUnknown'
        var allPropertyNames = new HashSet<string>();
        foreach (var prop in after.Value.EnumerateObject())
        {
            allPropertyNames.Add(prop.Name);
        }
        if (afterUnknown.HasValue && afterUnknown.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in afterUnknown.Value.EnumerateObject())
            {
                allPropertyNames.Add(prop.Name);
            }
        }
        
        foreach (var propName in allPropertyNames)
        {
            bool hasAfter = after.Value.TryGetProperty(propName, out var afterProp);
            
            JsonElement unknownProp = default;
            bool hasUnknown = afterUnknown.HasValue && 
                             afterUnknown.Value.ValueKind == JsonValueKind.Object && 
                             afterUnknown.Value.TryGetProperty(propName, out unknownProp);
            
            // If property only exists in afterUnknown and is true, it's computed
            if (!hasAfter && hasUnknown && unknownProp.ValueKind == JsonValueKind.True)
            {
                resultDict[propName] = COMPUTED_MARKER;
                continue;
            }
            
            // If property doesn't exist in after, skip it (unless handled above)
            if (!hasAfter)
                continue;
            
            // Check if this property is marked as computed
            bool isComputed = hasUnknown && unknownProp.ValueKind == JsonValueKind.True;
            
            if (isComputed)
            {
                resultDict[propName] = COMPUTED_MARKER;
            }
            else if (afterProp.ValueKind == JsonValueKind.Object)
            {
                // Recursief voor nested objecten
                JsonElement? nestedUnknown = hasUnknown ? unknownProp : null;
                resultDict[propName] = MergeAfterWithComputed(afterProp, nestedUnknown);
            }
            else if (afterProp.ValueKind == JsonValueKind.Array)
            {
                // Arrays: recursief per item, als after_unknown een array is
                if (hasUnknown && unknownProp.ValueKind == JsonValueKind.Array)
                {
                    var arr = new List<object?>();
                    int idx = 0;
                    foreach (var item in afterProp.EnumerateArray())
                    {
                        if (unknownProp.GetArrayLength() > idx && unknownProp[idx].ValueKind == JsonValueKind.True)
                            arr.Add(COMPUTED_MARKER);
                        else if (item.ValueKind == JsonValueKind.Object)
                        {
                            // Voor objecten in arrays, kijk of er een corresponderende after_unknown entry is
                            JsonElement? itemUnknown = null;
                            if (unknownProp.GetArrayLength() > idx && unknownProp[idx].ValueKind == JsonValueKind.Object)
                            {
                                itemUnknown = unknownProp[idx];
                            }
                            arr.Add(MergeAfterWithComputed(item, itemUnknown));
                        }
                        else
                            arr.Add(item);
                        idx++;
                    }
                    resultDict[propName] = arr;
                }
                else
                {
                    resultDict[propName] = afterProp;
                }
            }
            else
            {
                resultDict[propName] = afterProp;
            }
        }
        return resultDict;
    }
    
    /// <summary>
    /// Vervangt de COMPUTED_MARKER in een JSON string met de juiste &lt;computed&gt; weergave
    /// </summary>
    public static string ReplaceComputedMarkers(string json)
    {
        // Replace the marker with <computed> in italic markup
        return json.Replace($"\"{COMPUTED_MARKER}\"", "<em>&lt;computed&gt;</em>");
    }
}
