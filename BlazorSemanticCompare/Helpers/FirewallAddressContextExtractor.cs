using System.Text.Json;
using BlazorSemanticCompare.Models;

namespace BlazorSemanticCompare.Helpers;

/// <summary>Explains IP-group references without replacing Terraform's unknown values.</summary>
public static class FirewallAddressContextExtractor
{
    public static void Populate(JsonElement plan, List<ResourceChange> changes)
    {
        if (!plan.TryGetProperty("resource_changes", out var resources)) return;
        var groups = resources.EnumerateArray()
            .Where(r => Text(r, "type") == "azurerm_ip_group")
            .ToList(); // Includes no-op groups: their ranges are still relevant to changed rules.
        var configurations = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (plan.TryGetProperty("configuration", out var configuration) &&
            configuration.TryGetProperty("root_module", out var root))
            ReadConfiguration(root, "", configurations);

        foreach (var change in changes.Where(c => c.Type == "azurerm_firewall_policy_rule_collection_group"))
        {
            configurations.TryGetValue(change.Address, out var config);
            var expressions = Property(config, "expressions");
            foreach (var side in new[] { "Before", "After" })
            {
                var values = side == "Before" ? change.Before : change.After;
                if (values is not { ValueKind: JsonValueKind.Object }) continue;
                foreach (var collectionProperty in new[] { "network_rule_collection", "application_rule_collection", "nat_rule_collection" })
                {
                    var collections = Property(values.Value, collectionProperty);
                    if (collections.ValueKind != JsonValueKind.Array) continue;
                    var configCollections = Property(expressions, collectionProperty);
                    var collectionIndex = 0;
                    foreach (var collection in collections.EnumerateArray())
                    {
                        var configCollection = FindNamed(configCollections, Text(collection, "name"));
                        var rules = Property(collection, "rule");
                        if (rules.ValueKind != JsonValueKind.Array) { collectionIndex++; continue; }
                        var ruleIndex = 0;
                        foreach (var rule in rules.EnumerateArray())
                        {
                            var configRule = FindNamed(Property(configCollection, "rule"), Text(rule, "name"));
                            foreach (var direction in new[] { "source", "destination" })
                            {
                                var field = direction + "_ip_groups";
                                var ids = Property(rule, field);
                                var unknown = side == "After" && ContainsTrue(Property(
                                    At(Property(At(Property(change.AfterUnknown ?? default, collectionProperty), collectionIndex), "rule"), ruleIndex), field));
                                var matched = new List<JsonElement>();
                                if (ids.ValueKind == JsonValueKind.Array)
                                    foreach (var id in ids.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.String))
                                    {
                                        var group = groups.FirstOrDefault(g => string.Equals(
                                            Text(Property(Property(g, "change"), side.ToLowerInvariant()), "id"), id.GetString(), StringComparison.OrdinalIgnoreCase));
                                        if (group.ValueKind == JsonValueKind.Object) matched.Add(group);
                                        else change.AddressContexts.Add(new(collectionProperty, Text(rule, "name"), direction, side,
                                            id.GetString()!.Split('/').Last(), id.GetString()!, [], true, false));
                                    }
                                if (unknown)
                                {
                                    var references = Property(Property(configRule, field), "references");
                                    if (references.ValueKind == JsonValueKind.Array)
                                        foreach (var reference in references.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.String))
                                        {
                                            var text = reference.GetString()!;
                                            if (!text.EndsWith(".id", StringComparison.Ordinal)) continue;
                                            var modulePrefix = change.Address.Contains(".azurerm_", StringComparison.Ordinal)
                                                ? change.Address[..(change.Address.LastIndexOf(".azurerm_", StringComparison.Ordinal) + 1)] : "";
                                            var group = groups.FirstOrDefault(g => Text(g, "address") == modulePrefix + text[..^3]);
                                            if (group.ValueKind == JsonValueKind.Object) matched.Add(group);
                                        }
                                }
                                foreach (var group in matched.DistinctBy(g => Text(g, "address")))
                                {
                                    var groupChange = Property(group, "change");
                                    var groupValues = Property(groupChange, side.ToLowerInvariant());
                                    var cidrs = Property(groupValues, "cidrs");
                                    var cidrsUnknown = cidrs.ValueKind != JsonValueKind.Array ||
                                        (side == "After" && ContainsTrue(Property(Property(groupChange, "after_unknown"), "cidrs")));
                                    change.AddressContexts.Add(new(collectionProperty, Text(rule, "name"), direction, side,
                                        Text(groupValues, "name"), Text(group, "address"),
                                        cidrs.ValueKind == JsonValueKind.Array ? cidrs.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()!).ToList() : [],
                                        cidrsUnknown, unknown));
                                }
                            }
                            ruleIndex++;
                        }
                        collectionIndex++;
                    }
                }
            }
        }
    }

    private static void ReadConfiguration(JsonElement module, string prefix, Dictionary<string, JsonElement> result)
    {
        var resources = Property(module, "resources");
        if (resources.ValueKind == JsonValueKind.Array)
            foreach (var resource in resources.EnumerateArray()) result[prefix + Text(resource, "address")] = resource;
        var calls = Property(module, "module_calls");
        if (calls.ValueKind == JsonValueKind.Object)
            foreach (var call in calls.EnumerateObject())
                ReadConfiguration(Property(call.Value, "module"), prefix + "module." + call.Name + ".", result);
    }

    // Match blocks by constant names, never by configuration order (Terraform sets may be reordered).
    private static JsonElement FindNamed(JsonElement array, string name) => array.ValueKind == JsonValueKind.Array
        ? array.EnumerateArray().FirstOrDefault(v => Text(Property(v, "name"), "constant_value") == name && name.Length > 0) : default;
    private static JsonElement Property(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) ? property : default;
    private static JsonElement At(JsonElement value, int index) => value.ValueKind == JsonValueKind.Array && index < value.GetArrayLength() ? value[index] : default;
    private static string Text(JsonElement value, string name) => Property(value, name) is { ValueKind: JsonValueKind.String } text ? text.GetString()! : "";
    private static bool ContainsTrue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Array => value.EnumerateArray().Any(ContainsTrue),
        JsonValueKind.Object => value.EnumerateObject().Any(p => ContainsTrue(p.Value)),
        _ => false
    };
}
