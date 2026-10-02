# AGENTS.md

> Lees ook `AGENTS.local.md` (niet in git) als het bestaat; die verwijst naar persoonlijke Azure- en deployment-instructies buiten de repo.

## Project
Blazor Web App (.NET 9, Interactive Server rendering) that visualizes and semantically compares Terraform plan JSON (`resource_changes` with `before` / `after` / `after_unknown`).

## Structure
- `BlazorSemanticCompare.sln` – solution
- `BlazorSemanticCompare/` – the web project
  - `Program.cs` – startup; registers `JsonProcessingService` as singleton
  - `Components/Pages/` – routable pages (`Home`, `JsonViewer`, `DebugSemanticCompare`, plus template pages)
  - `Components/Shared/` – reusable components (`DiffJsonTree`, `JsonTreeNode`, `JsonWithComputedView`, `ResourceChangesComponent`, `SemanticComparePanel`)
  - `Components/Layout/` – `MainLayout`, `NavMenu`
  - `Helpers/JsonComputedMerger.cs` – merges `after` with `after_unknown` ("known after apply") values
  - `Models/ResourceChange.cs` – Terraform resource change model
  - `Services/JsonProcessingService.cs` – JSON diff/processing logic

## Commands
- Build: `dotnet build BlazorSemanticCompare.sln`
- Run: `dotnet run --project BlazorSemanticCompare`
- Publish profile: `BlazorSemanticCompare/Properties/PublishProfiles/blazorsemanticcompare.pubxml` (linux-x64)

There are currently no automated tests; verify changes by building and, for UI changes, running the app.

## Conventions
- C# with `Nullable` and `ImplicitUsings` enabled; keep nullable warnings clean.
- Use `System.Text.Json` (`JsonElement`/`JsonDocument`); do not add Newtonsoft.Json.
- Component styling goes in scoped `*.razor.css` files next to the component.
- Put diff/JSON logic in `Services` or `Helpers`, not in `.razor` code blocks.
- Existing code comments are partly Dutch; either language is fine, stay consistent within a file.
- Don't commit `bin/`, `obj/`, or `*.user` files; keep changes surgical and don't edit `wwwroot/lib` (vendored Bootstrap).
- Do not commit or push unless explicitly asked.
