using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BlazorSemanticCompare.Services;

/// <summary>
/// Ties the agent code to the circuit lifetime. OnCircuitClosed only fires after
/// DisconnectedCircuitRetentionPeriod, which is the grace period after a connection drop.
/// </summary>
public sealed class AgentCircuitHandler(AgentSession session, AgentSessionRegistry registry) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        registry.Add(session);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        session.PlanHandler = null;
        registry.Remove(session);
        return Task.CompletedTask;
    }
}
