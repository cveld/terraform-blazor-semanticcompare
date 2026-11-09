using System.Text;
using System.Text.Json;
using BlazorSemanticCompare.Models;

namespace BlazorSemanticCompare.Helpers;

/// <summary>Validates a new import before the viewer releases its previous document.</summary>
public static class TerraformPlanParser
{
    public const int MaxPlanBytes = 20_000_000;

    public static (JsonDocument Document, List<ResourceChange> Changes) Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new JsonException("Input is empty.");
        if (Encoding.UTF8.GetByteCount(text) > MaxPlanBytes) throw new JsonException("Plan exceeds 20 MB.");
        var document = JsonDocument.Parse(text);
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Terraform plan must be a JSON object.");
            // Output-only plans can omit resource_changes.
            if (!document.RootElement.TryGetProperty("resource_changes", out _) &&
                !document.RootElement.TryGetProperty("format_version", out _))
                throw new JsonException("Expected Terraform plan JSON with resource_changes or format_version.");
            return (document, ResourceChangeExtractor.Extract(document));
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            document.Dispose();
            throw new JsonException("Invalid Terraform resource_changes structure.", ex);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }
}
