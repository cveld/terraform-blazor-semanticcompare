---
name: semantic-compare-plan
description: Load a Terraform plan JSON (for example from an Azure DevOps pipeline build artifact) into the Semantic Compare web app (local http://localhost:5206 or https://semanticcompare.carlintveld.nl) without the clipboard and without the plan passing through the model context. Use when asked to open, view or inspect a build's plan json in semantic compare, or to "shoot" a plan json into the webapp.
---

# Load a plan JSON into Semantic Compare

The app (source: `C:\work\git\github\cveld\terraform-blazor-semanticcompare`) takes a plan
from the clipboard, from a file input, or from the WebMCP tool `load-terraform-plan`.
Plans can be large, so never read or paste their contents into model context or a tool
argument. Move bytes file-to-file or let the app fetch them from a temporary URL.

## Preferred: push via the agent API (hosted and local)

The app shows an **agent code** (element `#agent-code`) once its page is interactive. The plan
is POSTed to `/api/plan` with that code as a bearer token; it goes file-to-server, so it never
enters model context, and it works against the hosted site (no `files.upload`, no local server).
The plan lives only as long as that page's connection; a refresh gives a new code.

1. Get the plan JSON on disk (section 1).
2. Open the app in a new tab (hosted `https://semanticcompare.carlintveld.nl/`, or the local app)
   and read the code with a bounded evaluation, after waiting a few seconds for the circuit:
   `document.querySelector('#agent-code')?.textContent`. If the element is missing, the deployed
   version predates the API: use A, B or C below.
3. POST the file (PowerShell tool, one call):
   ```powershell
   Invoke-RestMethod -Method Post -Uri https://semanticcompare.carlintveld.nl/api/plan `
       -Headers @{ Authorization = "Bearer $code" } -ContentType application/json -InFile terraform.plan.json
   ```
   `200` returns `{"resourceChanges": n}`. `400` = invalid JSON (message in `error`), `401` = unknown
   or expired code (re-read it from the page), `413` = over 20 MB, `429` = too many failed attempts.
4. The `200` response is the verification; do not read the page body. Keep the tab open: closing
   it (or a refresh) releases the plan after a ~2 minute grace period.

Details for humans are on the app's `/agent-setup` page. Never log or repeat the code outside
the command that uses it.

## 0. Discover capabilities first

After opening or selecting the Semantic Compare tab, query the current WebMCP/page-tool
catalog first, specifically for `load-terraform-plan`. Then query the browser catalog for
`files.upload`. Runtime capability discovery is the source of truth; do not rely on stale
statements in this skill about what the browser supports.

After discovery, choose the shortest available byte path:

1. Registered WebMCP/page tool `load-terraform-plan` when a fetchable URL already exists.
2. Browser `files.upload` for a local file; this is cheaper than creating a local server.
3. One-shot local HTTP server only if neither capability is available.

Do not start with DOM mutation, synthetic drop zones, clipboard access, or a local server.

### Per-harness expectation (verified)

| Harness | Path |
|---|---|
| opencode | Browser `files.upload` works: upload the file directly (section 2B). No server. |
| Claude Code (desktop browser pane or Chrome) | No `files.upload`, no WebMCP in the pane: go straight to the one-shot server (section 2C). |
| GitHub Copilot | Same as Claude Code: one-shot server (section 2C). |

For Claude Code and Copilot, skip the discovery probing beyond a single check that the
page has one `input[type=file]`, then use 2C. Starting the one-shot server is already
documented here and the user expects it: still state file, origin and port in one line
before starting, but do not use an interactive question tool; just proceed unless the
user objects. Copy the plan to a temp file first (names with spaces/parentheses, e.g.
`terraform.plan (25).json`, are awkward) and delete that copy afterwards.

## 1. Get the plan JSON on disk (no content in context)

Azure DevOps build artifact via the AzDO MCP tool when available:

1. `pipelines_artifact` action `list` (project, buildId) - the artifact is usually the
   Terraform working directory (for example `terraformlrn`).
2. `pipelines_artifact` action `download` with `destinationPath` as a **relative** path
   inside the working directory (absolute paths are rejected), for example `tmp-build-<id>`.
3. The download is a ZIP. Extract it and find `terraform.plan.json`
   (`Expand-Archive` in PowerShell; copy the JSON next to the zip, delete the zip).

If the artifact is an Azure DevOps `Container` artifact and the normal pipeline-artifact
download rejects it, use its authenticated download URL with `azps rest --output-file
<zip>`, then extract it. Do not print or parse the JSON.

The built-in browser pane has no AzDO login (clean profile) - do not try to open the
build page there. If the pipeline has no JSON artifact, run `terraform show -json <plan>`.

## 2. Load it into the app

Default to the hosted site, `https://semanticcompare.carlintveld.nl/`; the local app does
not need to run. Exception: the one-shot server fallback (C) only works against the local
app, because the hosted page cannot fetch from `127.0.0.1` (see "Things that do not work").
For C, check the local app is up first (`http://localhost:5206/`); with `dotnet run` code
changes need a restart, `dotnet watch` hot-reloads. If port 5206 is already in use, the app
is probably running already: do not start a second instance (the build fails on the locked
`obj` DLL).

When opening the app in the pane, use a new tab (`tabs_create`) rather than navigating a tab
that shows something else, unless the user asks otherwise.

### A. WebMCP/page tool

If discovery returns `load-terraform-plan` and the plan already has a URL the app can fetch,
invoke it with its `url` argument, never its `plan` argument. Pass only the small URL:

```json
{"url":"https://example.invalid/terraform.plan.json"}
```

The app fetches and processes the bytes without putting the plan in model context. An
authenticated Azure DevOps artifact URL usually cannot be fetched by the clean browser
profile, so download it to disk and use browser upload instead of building a proxy.

### B. Browser file upload

If `files.upload` is available, use browser `find` with the exact accessible name
`Upload Terraform plan JSON` to obtain a fresh ref for the existing file input, then upload
the absolute path to `terraform.plan.json`. Do not take a full-page snapshot first. The
browser transfers bytes directly; do not read the file. Refs such as `e36` are ephemeral
and change after a snapshot or Blazor re-render, so never store or guess them.

This is the normal fallback in both the built-in browser pane and Chrome when the WebMCP
page tool is unavailable.

### C. One-shot URL fallback

Use this only when neither the WebMCP/page tool nor browser `files.upload` is available.
The fallback is a one-shot, origin-locked local server plus browser-side JavaScript.

1. **Announce it** - this opens a localhost port. State the file, origin and port in one
   line, then proceed (no question tool; the skill itself is the user's standing approval).
   The auto-mode classifier refuses an open-CORS server (`Access-Control-Allow-Origin: *`);
   this script is strict on purpose, do not loosen it.
2. Start the server in the background (`run_in_background`):
   `python ~/.claude/skills/semantic-compare-plan/scripts/serve_once.py <plan.json> http://localhost:5206 18765 120`
   It binds to 127.0.0.1, serves only `/plan.json`, only for that Origin, exits after one
   GET or after the timeout.
3. In the pane tab at the app origin, use browser JavaScript evaluation:
   ```js
   const blob = await (await fetch('http://127.0.0.1:18765/plan.json')).blob();
   const f = new File([blob], 'terraform.plan.json', {type: 'application/json'});
   const dt = new DataTransfer(); dt.items.add(f);
   const input = document.querySelector('input[type=file]');
   input.files = dt.files;
   input.dispatchEvent(new Event('change', {bubbles: true}));
   blob.size
   ```
   The origin in step 2 must equal the page origin exactly, so this route uses the local
   app (`http://localhost:5206`). It does not work from the hosted site, see below. After
   navigating to the app, wait a few seconds before injecting: if the Blazor circuit is not
   connected yet, the `change` event is dropped silently and nothing loads (the page then
   shows no file name; re-navigate and retry).
4. Verify with a small bounded result: check that the displayed file name is
   `terraform.plan.json`, that the resource-change section exists, and that no error alert
   is present. Do not return `document.body.innerText` or plan details to model context.
   The first render of a large plan can freeze the renderer for ~30 s.
5. Confirm the port is free (`Get-NetTCPConnection -LocalPort 18765 -State Listen`).

### Things that do not work (do not retry)

- One-shot server from the hosted site: with `https://semanticcompare.carlintveld.nl` as the
  page origin, `fetch('http://127.0.0.1:<port>/plan.json')` fails immediately with
  `TypeError: Failed to fetch` and never reaches the server (re-verified 2026-10-05, also
  blocked in the earlier session). Cause: Chrome's Local Network Access check. In the pane
  `navigator.permissions.query({name:'local-network-access'})` (and `'loopback-network'`)
  returns `denied`, and the console shows `net::ERR_BLOCKED_BY_CLIENT`. Every variant is
  blocked before any network traffic: plain, `no-cors`, `targetAddressSpace: 'loopback'` and
  `'local'`, even with `Access-Control-Allow-Private-Network` on the server. Nothing in the
  page or server can change that; the pane has no way to grant the permission. Use the
  local app for route C (an `http://localhost` page is not subject to the check), or
  `files.upload` / the WebMCP tool on the hosted site.
- Reading the clipboard in the pane: `NotAllowedError: Read permission denied`.
- Putting the plan in the app's `wwwroot` so the page can fetch it: the user rejected it,
  and it makes `dotnet watch` restart.
- Clicking the upload label in the pane: opens an invisible native dialog.
- Assuming WebMCP is unavailable without querying the current page-tool catalog first.
- Reading the page's complete body text to verify loading; use bounded booleans/counts.

## 3. Read the result honestly

- The app lists only `resource_changes` whose action is not `no-op`. With none, it shows
  "This plan does not contain any changes."
- If the user asks for analysis, compute only bounded summaries locally (for example action
  counts) without returning the JSON or resource details to model context. `resource_drift`
  can be non-empty even when there are no changes: Terraform reports resources that changed
  outside Terraform. Say so; "no changes" is not "no drift".

## 4. Clean up

Delete the temporary folder with the downloaded artifact and the extracted JSON
(`tmp-build-<id>` in the working directory) once the user is done. Leave the user's
dev server and any terminals alone.
