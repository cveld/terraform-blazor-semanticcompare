const toolNames = ["load-terraform-plan", "get-plan-status", "open-semantic-compare"];
const maxPlanBytes = 20_000_000;
let activeRegistration;

function result(value) {
    return { content: [{ type: "text", text: JSON.stringify(value) }], isError: value.success === false };
}

export function unregisterTools() {
    if (!activeRegistration) return;
    const registration = activeRegistration;
    activeRegistration = undefined;
    registration.active = false;
    for (const name of registration.names) {
        try { registration.context.unregisterTool(name); }
        catch (error) { console.warn(`Could not unregister ${name}`, error); }
    }
}

export function registerTools(dotNetRef) {
    unregisterTools();
    const context = navigator.modelContext;
    if (!context?.registerTool || !context?.unregisterTool) return false;
    const registration = { context, names: [], active: true, importing: false };
    activeRegistration = registration;
    const execute = (callback) => async (args) => {
        try {
            if (!registration.active) throw new Error("The viewer is no longer open.");
            return result(await callback(args ?? {}));
        } catch (error) {
            return result({ success: false, error: error.message ?? String(error) });
        }
    };
    const definitions = [
        {
            name: toolNames[0],
            description: "Load Terraform plan JSON (terraform show -json) into this page, replacing the current plan. Maximum 20 MB. Send the plan explicitly; this tool does not read the clipboard, local files, or fetch URLs.",
            inputSchema: {
                type: "object",
                properties: { plan: { type: "string", description: "Complete Terraform plan JSON as a string." } },
                required: ["plan"], additionalProperties: false
            },
            execute: execute(async ({ plan }) => {
                if (typeof plan !== "string" || !plan.trim()) throw new Error("Provide a non-empty plan string.");
                if (plan.length > maxPlanBytes || new TextEncoder().encode(plan).length > maxPlanBytes)
                    throw new Error("Plan exceeds 20 MB.");
                const parsed = JSON.parse(plan);
                if (!parsed || Array.isArray(parsed) || typeof parsed !== "object")
                    throw new Error("Terraform plan must be a JSON object.");
                if (registration.importing) throw new Error("Another plan import is in progress.");
                registration.importing = true;
                try {
                    await dotNetRef.invokeMethodAsync("BeginWebMcpPlan");
                    // Below Blazor Server's default message limit, even with escaping/multibyte text.
                    for (let offset = 0; offset < plan.length;) {
                        if (!registration.active) throw new Error("The viewer is no longer open.");
                        let end = Math.min(offset + 2000, plan.length);
                        // Never split a UTF-16 surrogate pair: .NET JSON decoding would replace it.
                        const last = plan.charCodeAt(end - 1);
                        if (end < plan.length && last >= 0xD800 && last <= 0xDBFF) end--;
                        await dotNetRef.invokeMethodAsync("AppendWebMcpPlan", plan.slice(offset, end));
                        offset = end;
                    }
                    return await dotNetRef.invokeMethodAsync("CompleteWebMcpPlan");
                } finally {
                    try { await dotNetRef.invokeMethodAsync("CancelWebMcpPlan"); } catch { /* disconnected */ }
                    registration.importing = false;
                }
            })
        },
        {
            name: toolNames[1],
            description: "Read current viewer status and changed resource addresses/actions. Does not return raw plan values or fetch data outside this page.",
            inputSchema: { type: "object", properties: {}, additionalProperties: false },
            annotations: { readOnlyHint: true },
            execute: execute(() => dotNetRef.invokeMethodAsync("GetWebMcpPlanStatus"))
        },
        {
            name: toolNames[2],
            description: "Open the semantic comparison panel for one top-level property of a changed resource in the loaded plan. Changes only this page's view; does not apply Terraform changes.",
            inputSchema: {
                type: "object",
                properties: {
                    address: { type: "string", description: "Exact Terraform resource address from get-plan-status." },
                    property: { type: "string", description: "Top-level attribute name, e.g. application_rule_collection." }
                },
                required: ["address", "property"], additionalProperties: false
            },
            execute: execute(({ address, property }) => {
                if (typeof address !== "string" || !address || typeof property !== "string" || !property)
                    throw new Error("Provide address and property strings.");
                return dotNetRef.invokeMethodAsync("OpenWebMcpSemanticCompare", address, property);
            })
        }
    ];
    try {
        for (const definition of definitions) {
            context.registerTool(definition);
            registration.names.push(definition.name);
        }
        return true;
    } catch (error) {
        unregisterTools();
        console.warn("WebMCP registration failed", error);
        return false;
    }
}
