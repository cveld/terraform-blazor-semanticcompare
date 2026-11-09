# Terraform Semantic Compare

Blazor Web App (.NET 10, Interactive Server rendering) that visualizes and semantically compares Terraform plan JSON. The app reads `resource_changes` from a plan and shows, per resource, the differences between `before` and `after`, including values that are only known after apply (`after_unknown`, "known after apply").

## Try it online

The web app is available at [https://semanticcompare.carlintveld.nl/](https://semanticcompare.carlintveld.nl/). Open it in your browser, import your Terraform plan JSON and start comparing right away; there is nothing to install.

## Key feature: semantic compare for Azure Firewall and Application Gateway

Terraform's own plan output for resources such as `azurerm_firewall_policy_rule_collection_group` and `azurerm_application_gateway` is notoriously hard to review. These resources contain large nested lists (rule collections, rules, listeners, routing rules, backend pools, ...), and Terraform compares lists by index. Reordering or inserting a single item makes the plan show a large number of changes, even when the configuration is semantically identical.

Semantic Compare solves this by comparing the *meaning* of a change instead of its position:

- **Match by key** – arrays of objects are matched on their unique `name` (or else `id`), so a reordered rule or rule collection is not reported as changed; only real additions, removals and modifications are.
- **Order-insensitive lists** – arrays of strings and other primitives (e.g. source addresses, destination ports, FQDNs) are sorted and compared as sets, showing exactly which values were added or removed.
- **"Known after apply" values** – `after` is merged with `after_unknown`, so computed values are visibly marked instead of appearing as missing.
- **Empty equals null** – `null` and `""` are treated as equal (configurable), which removes noise.
- **Focused diff** – pick a property (for example `application_rule_collection`) of a resource change and see before, after and a colour-coded diff tree side by side.

Workflow:

1. `terraform plan -out=terraform.plan`
2. `terraform show -json terraform.plan | clip` (Windows) or `pbcopy` (macOS)
3. In the app, choose **Import json plan from clipboard** (or upload the JSON file) and open the semantic compare for the changed property.

## Agent API

A coding agent can push a plan into the page you have open, without the clipboard:

1. The page shows an **agent code** (`#agent-code`).
2. `POST /api/plan` with `Authorization: Bearer <code>` and the plan JSON as body (max 20 MB).
3. The plan lives only as long as that page's Blazor connection. After a connection drop it is released once the circuit expires (`DisconnectedCircuitRetentionPeriod`, 2 minutes). A page refresh gives a new code.

Failed attempts are rate limited per client address. The code registry is in-memory, so this requires **a single replica** (the Container App must keep `maxReplicas: 1`). See `/agent-setup` in the app for examples and the skill onboarding.

## WebMCP (optional browser integration)

On the interactive plan viewer (`/` or `/json-viewer`), browsers exposing
`navigator.modelContext.registerTool` and `unregisterTool` discover:

- **`load-terraform-plan`** — `{ "plan": "<terraform show -json output>" }` imports a plan (max 20 MB UTF-8), replacing the current plan only after validation.
- **`get-plan-status`** — `{}` returns whether a plan is loaded and its changed resource addresses/actions, without raw attribute values.
- **`open-semantic-compare`** — `{ "address": "azurerm_application_gateway.example", "property": "backend_address_pool" }` opens the existing comparison panel.

Tools return text containing JSON, with `isError: true` for failures. Imports are
chunked below Blazor Server's incoming message limit. Tools unregister when leaving
the viewer and register again when returning. The page displays availability; on
browsers without WebMCP, clipboard/file import and the agent API still work.
No polyfill is installed: a compatible browser/agent integration must supply the
native API. Tools never fetch a URL or read the clipboard/files implicitly. Obtain
user approval before sending sensitive Terraform plan values to the page.

Developer checks: `node --test scripts/webmcp.test.mjs` and
`dotnet build BlazorSemanticCompare.sln`. Start the local server only via
`pwsh scripts/dev-watch.ps1` and follow `%LOCALAPPDATA%/semanticcompare/dotnet-watch.log`.

## Features

- Paste or load a plan JSON and inspect each resource change
- The browser tab title follows the loaded plan: its filename (or import source for agent API, WebMCP and clipboard imports), resource change count, and app name. Failed imports keep the previous plan title.
- Semantic diff of `before` / `after` as a tree
- Merges `after` with `after_unknown` so "known after apply" values are visible
- Standalone JSON viewer and a debug page for the comparison

## Structure

| Path | Contents |
|------|----------|
| `BlazorSemanticCompare.sln` | Solution |
| `BlazorSemanticCompare/Program.cs` | Startup; registers `JsonProcessingService` |
| `BlazorSemanticCompare/Components/Pages/` | Routable pages (`Home`, `JsonViewer`, `DebugSemanticCompare`) |
| `BlazorSemanticCompare/Components/Shared/` | Reusable components (`DiffJsonTree`, `JsonTreeNode`, `SemanticComparePanel`, ...) |
| `BlazorSemanticCompare/Helpers/JsonComputedMerger.cs` | Merges `after` with `after_unknown` |
| `BlazorSemanticCompare/Models/ResourceChange.cs` | Terraform resource change model |
| `BlazorSemanticCompare/Services/JsonProcessingService.cs` | JSON diff/processing logic |

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build BlazorSemanticCompare.sln
dotnet run --project BlazorSemanticCompare
```

By default the app runs locally at <http://localhost:5206>.

## Publishing

Publish profile: `BlazorSemanticCompare/Properties/PublishProfiles/blazorsemanticcompare.pubxml` (linux-x64).

## Contributing

See [AGENTS.md](AGENTS.md) for conventions (keep nullable warnings clean, use `System.Text.Json`, scoped `*.razor.css`, diff logic in `Services`/`Helpers`). Run the WebMCP JavaScript checks with `node --test scripts/webmcp.test.mjs`; also verify changes by building and running the app.

### Local sandbox compatibility

`appsettings.Development.json` enables `Development:SandboxCompatibility` for
restricted local runtimes: logs go to console/debug instead of Windows Event Log,
and Data Protection uses in-memory keys instead of the user's DPAPI key ring.
Local protected tokens expire on server restart; reload the page after a restart.
Set `Development__SandboxCompatibility=false` to use the normal providers outside
a sandbox. This option is ignored outside the Development environment.
