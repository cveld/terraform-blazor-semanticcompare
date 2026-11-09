using System.Text.Json;
using BlazorSemanticCompare.Helpers;

const string fixture = """
{
  "resource_changes": [
    {"address":"azurerm_ip_group.groups[\"vms\"]","type":"azurerm_ip_group","change":{"actions":["no-op"],"before":{"id":"/ipGroups/vms","name":"vms","cidrs":["10.0.0.0/24"]},"after":{"id":"/ipGroups/vms","name":"vms","cidrs":["10.0.0.0/24"]}}},
    {"address":"azurerm_ip_group.groups[\"sql\"]","type":"azurerm_ip_group","change":{"actions":["create"],"after":{"name":"sql","cidrs":["10.1.0.0/24"]},"after_unknown":{"id":true,"cidrs":[false]}}},
    {"address":"azurerm_firewall_policy_rule_collection_group.rules","type":"azurerm_firewall_policy_rule_collection_group","change":{"actions":["update"],"before":{"network_rule_collection":[{"name":"collection","rule":[{"name":"allow","source_ip_groups":["/ipGroups/vms"]}]}]},"after":{"network_rule_collection":[{"name":"collection","rule":[{"name":"allow"}]}]},"after_unknown":{"network_rule_collection":[{"rule":[{"source_ip_groups":true}]}]}}}
  ],
  "configuration":{"root_module":{"resources":[{"address":"azurerm_firewall_policy_rule_collection_group.rules","expressions":{"network_rule_collection":[{"name":{"constant_value":"other"},"rule":[]},{"name":{"constant_value":"collection"},"rule":[{"name":{"constant_value":"other"}},{"name":{"constant_value":"allow"},"source_ip_groups":{"references":["azurerm_ip_group.groups[\"vms\"].id","azurerm_ip_group.groups[\"vms\"]","azurerm_ip_group.groups","azurerm_ip_group.groups[\"sql\"].id","azurerm_ip_group.groups[\"sql\"]"]}}]}]}}]}}
}
""";

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

using (var doc = JsonDocument.Parse(fixture))
{
    var changes = ResourceChangeExtractor.Extract(doc);
    var contexts = changes.Single(c => c.Type.Contains("firewall")).AddressContexts;
    Check(changes.Count == 2, "No-op resources must stay out of the change list.");
    Check(contexts.Count == 3, "Must match renamed/reordered configuration blocks by name and deduplicate references.");
    Check(contexts.Single(c => c.Side == "Before").Cidrs.SequenceEqual(["10.0.0.0/24"]), "Resolve existing group IDs.");
    Check(contexts.Single(c => c.GroupName == "sql").Cidrs.SequenceEqual(["10.1.0.0/24"]), "Expose planned ranges for a created group.");
    Check(contexts.Where(c => c.Side == "After").All(c => c.FromConfiguration && !c.CidrsUnknown), "Preserve dependency provenance and known ranges.");
}

using (var doc = JsonDocument.Parse(fixture.Replace("\"cidrs\":[false]", "\"cidrs\":true")))
    Check(ResourceChangeExtractor.Extract(doc).Single(c => c.Type.Contains("firewall")).AddressContexts.Single(c => c.GroupName == "sql").CidrsUnknown,
        "Unknown CIDRs must remain explicitly unknown.");

using (var doc = JsonDocument.Parse(fixture.Replace("\"constant_value\":\"allow\"", "\"references\":[\"each.key\"]")))
    Check(ResourceChangeExtractor.Extract(doc).Single(c => c.Type.Contains("firewall")).AddressContexts.Count == 1,
        "Do not guess references for dynamically named rules.");

if (args.Length > 0)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
    var contexts = ResourceChangeExtractor.Extract(doc)
        .Single(c => c.Address.EndsWith(".Allow-management-to-workload")).AddressContexts;
    var sql = contexts.Single(c => c.Side == "After" && c.Direction == "source" && c.GroupName == "ipg-sql");
    Check(sql.Cidrs.SequenceEqual(["10.200.24.0/24"]) && !sql.CidrsUnknown && sql.FromConfiguration, "Novulo SQL ranges.");
    Check(contexts.Any(c => c.Side == "After" && c.Direction == "source" && c.GroupName == "ipg-vms" && c.Cidrs.Count > 0), "Novulo existing VM ranges.");
    foreach (var c in contexts.Where(c => c.Direction == "source"))
        Console.WriteLine($"{c.Side}: {c.GroupName}: {string.Join(", ", c.Cidrs)}");
}
Console.WriteLine("All address-context checks passed.");
