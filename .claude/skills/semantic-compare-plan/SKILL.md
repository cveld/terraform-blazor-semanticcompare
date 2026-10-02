---
name: semantic-compare-plan
description: Load a Terraform plan JSON (for example from an Azure DevOps pipeline build artifact) into the Semantic Compare web app (local http://localhost:5206 or https://semanticcompare.carlintveld.nl) without the clipboard and without the plan passing through the model context. Use when asked to open, view or inspect a build's plan json in semantic compare, or to "shoot" a plan json into the webapp.
---

# Load a plan JSON into Semantic Compare

The app (source: `C:\work\git\github\cveld\terraform-blazor-semanticcompare`) takes a plan
from the clipboard, from a file input, or from the WebMCP tool `load-terraform-plan`.
The plan is ~700 KB, so never paste it into a tool call: move it file-to-file.

## 1. Get the plan JSON on disk (no content in context)

Azure DevOps build artifact via the AzDO MCP tool:

1. `pipelines_artifact` action `list` (project, buildId) - the artifact is usually the
   Terraform working directory (for example `terraformlrn`).
2. `pipelines_artifact` action `download` with `destinationPath` as a **relative** path
   inside the working directory (absolute paths are rejected), for example `tmp-build-<id>`.
3. The download is a ZIP. Extract it and find `terraform.plan.json`
   (`Expand-Archive` in PowerShell; copy the JSON next to the zip, delete the zip).

The built-in browser pane has no AzDO login (clean profile) - do not try to open the
build page there. If the pipeline has no JSON artifact, run `terraform show -json <plan>`.

## 2. Pick a way into the app

Always check the app is up first (`http://localhost:5206/`). With `dotnet run` code
changes need a restart; `dotnet watch` hot-reloads.

### A. Claude in Chrome (simplest, if connected)

`find` the file input ("Upload json plan from file"), then `file_upload` with the plan's
absolute path. Refs go stale after a Blazor re-render: `find` again right before
uploading. Works only if the Chrome extension is connected.

### B. Built-in browser pane (desktop app)

The pane has no file-upload tool (anthropics/claude-code#96800) and the native file
dialog is invisible to it. Workaround: a one-shot, origin-locked local server plus
`javascript_tool`.

1. **Ask the user first** - this opens a localhost port. State the file, origin and port.
   The auto-mode classifier refuses an open-CORS server (`Access-Control-Allow-Origin: *`);
   this script is strict on purpose, do not loosen it.
2. Start the server in the background (`run_in_background`):
   `python ~/.claude/skills/semantic-compare-plan/scripts/serve_once.py <plan.json> http://localhost:5206 18765 120`
   It binds to 127.0.0.1, serves only `/plan.json`, only for that Origin, exits after one
   GET or after the timeout.
3. In the pane tab at the app origin, run `javascript_tool`:
   ```js
   const blob = await (await fetch('http://127.0.0.1:18765/plan.json')).blob();
   const f = new File([blob], 'terraform.plan.json', {type: 'application/json'});
   const dt = new DataTransfer(); dt.items.add(f);
   const input = document.querySelector('input[type=file]');
   input.files = dt.files;
   input.dispatchEvent(new Event('change', {bubbles: true}));
   blob.size
   ```
   The origin in step 2 must equal the page origin exactly. For the hosted site use
   `https://semanticcompare.carlintveld.nl` (the pane page is HTTPS, 127.0.0.1 is still
   allowed as a potentially trustworthy origin).
4. Verify: `javascript_tool` reading `.card-header span` (file name) and `.alert` texts, or
   a screenshot. The first render of a large plan can freeze the renderer for ~30 s.
5. Confirm the port is free (`Get-NetTCPConnection -LocalPort 18765 -State Listen`).

### Things that do not work (do not retry)

- Reading the clipboard in the pane: `NotAllowedError: Read permission denied`.
- Putting the plan in the app's `wwwroot` so the page can fetch it: the user rejected it,
  and it makes `dotnet watch` restart.
- Clicking the upload label in the pane: opens an invisible native dialog.
- `navigator.modelContext` (WebMCP) is `undefined` in the pane, so
  `load-terraform-plan` is never registered there, and it needs the JSON inline anyway.

## 3. Read the result honestly

- The app lists only `resource_changes` whose action is not `no-op`. With none, it shows
  "This plan does not contain any changes."
- Check the plan yourself too (count actions in `resource_changes`). `resource_drift`
  can be non-empty even when there are no changes: Terraform reports resources that
  changed outside Terraform. Say so; "no changes" is not "no drift".

## 4. Clean up

Delete the temporary folder with the downloaded artifact and the extracted JSON
(`tmp-build-<id>` in the working directory) once the user is done. Leave the user's
dev server and any terminals alone.
