namespace BlazorSemanticCompare.Models;

public sealed record FirewallAddressContext(
    string PropertyName, string RuleName, string Direction, string Side,
    string GroupName, string ResourceAddress, IReadOnlyList<string> Cidrs,
    bool CidrsUnknown, bool FromConfiguration);
