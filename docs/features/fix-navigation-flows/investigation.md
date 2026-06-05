# Investigation: Legacy Kotlin Navigation vs MAUI Shell

Parent issue: #141

## Legacy routes (Kotlin)

Based on existing feature documentation and task references, legacy deep links were:

1. `ndi://sources`
2. `ndi://settings/discovery-servers`
3. `ndi://theme-editor`

## Current MAUI Shell route topology

Current MAUI routing is composed of:

1. Shell tabs:
   - `//sources` (declared as `Route="sources"`)
   - `//settings` (declared as `Route="settings"`)
2. Registered detail routes in shell constructor:
   - `viewer`
   - `output`
3. Source navigation commands:
   - `viewer?sourceId=<escaped>`
   - `output?sourceId=<escaped>`

## Mapping table

| Legacy route | MAUI equivalent | Status |
| --- | --- | --- |
| `ndi://sources` | `//sources` | Mapped |
| `ndi://settings/discovery-servers` | `//settings` (no separate sub-route) | Partial |
| `ndi://theme-editor` | No route/page in MAUI shell | Missing |

## Findings

1. MAUI replaced URI-style deep links with Shell route names, so direct 1:1 URI parity is not currently implemented.
2. The legacy discovery-server sub-route is collapsed into the single Settings page. Functional parity is partial, not structural.
3. No MAUI Shell route currently represents `theme-editor`; this is a known migration gap.
4. Viewer and Output destinations are routed through explicit Shell route registration and query string parameters from `SourceListViewModel`.
