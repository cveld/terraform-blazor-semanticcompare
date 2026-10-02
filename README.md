# Terraform Semantic Compare

Blazor Web App (.NET 9, Interactive Server rendering) om Terraform plan JSON te visualiseren en semantisch te vergelijken. De app leest `resource_changes` uit een plan en toont per resource de verschillen tussen `before` en `after`, inclusief waarden die pas na apply bekend zijn (`after_unknown`, "known after apply").

## Functionaliteit

- Plan JSON plakken of laden en per resource change bekijken
- Semantische diff van `before` / `after` als boomstructuur
- Samenvoegen van `after` met `after_unknown` zodat "known after apply"-waarden zichtbaar zijn
- Losse JSON viewer en een debugpagina voor de vergelijking

## Structuur

| Pad | Inhoud |
|-----|--------|
| `BlazorSemanticCompare.sln` | Solution |
| `BlazorSemanticCompare/Program.cs` | Startup; registreert `JsonProcessingService` |
| `BlazorSemanticCompare/Components/Pages/` | Routeerbare pagina's (`Home`, `JsonViewer`, `DebugSemanticCompare`) |
| `BlazorSemanticCompare/Components/Shared/` | Herbruikbare componenten (`DiffJsonTree`, `JsonTreeNode`, `SemanticComparePanel`, ...) |
| `BlazorSemanticCompare/Helpers/JsonComputedMerger.cs` | Merge van `after` met `after_unknown` |
| `BlazorSemanticCompare/Models/ResourceChange.cs` | Model voor een Terraform resource change |
| `BlazorSemanticCompare/Services/JsonProcessingService.cs` | JSON diff/verwerking |

## Aan de slag

Vereist de [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
dotnet build BlazorSemanticCompare.sln
dotnet run --project BlazorSemanticCompare
```

Standaard draait de app lokaal op <http://localhost:5206>.

## Publiceren

Publish profile: `BlazorSemanticCompare/Properties/PublishProfiles/blazorsemanticcompare.pubxml` (linux-x64).

## Bijdragen

Zie [AGENTS.md](AGENTS.md) voor conventies (nullable warnings schoon houden, `System.Text.Json`, scoped `*.razor.css`, diff-logica in `Services`/`Helpers`). Er zijn nog geen geautomatiseerde tests; controleer wijzigingen door te builden en de app te draaien.
