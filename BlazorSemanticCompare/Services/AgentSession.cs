using System.Security.Cryptography;

namespace BlazorSemanticCompare.Services;

public sealed record AgentPlanResult(bool Success, int ResourceChanges, string? Error);

/// <summary>
/// One per Blazor circuit (scoped). Holds the code an agent uses to push a plan into the
/// page of this circuit. The plan itself lives in the page; it is released with the circuit.
/// </summary>
public sealed class AgentSession
{
    // Crockford base32 without I, L, O, U: unambiguous when read from a page.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    public const int CodeLength = 10;

    public string Code { get; } = NewCode();

    /// <summary>Set by the page while it is alive; loads the plan JSON and reports the outcome.</summary>
    public Func<string, Task<AgentPlanResult>>? PlanHandler { get; set; }

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }

    /// <summary>Uppercases and drops separators so a code can be typed or pasted loosely.</summary>
    public static string Normalize(string code) =>
        new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
