# Contract: View Screen Developer Log Address Rendering

## Scope
Defines user-visible contract for configured-address content in View screen logs when developer mode is enabled.

## Inputs
- Developer mode state (`enabled` true/false)
- Active configured address list (ordered strings)
- Viewer log event that includes configured-address context

## Rules
1. If developer mode is disabled:
- Configured-address developer log output MUST NOT be rendered.

2. If developer mode is enabled:
- Render actual configured addresses instead of redacted placeholders.
- Accept valid address formats:
  - IPv4 literal
  - IPv6 literal
  - Hostname
- Exclude malformed entries from rendered output.
- Avoid duplicate address repetition within a single event output.
- Preserve active configuration order for rendered valid addresses.

3. Fallback behavior:
- If no valid addresses remain after validation, render explicit "not configured"-style message.

## Observable Output Examples
- Single address: "Configured address: 192.168.1.10"
- Multi-address: "Configured addresses: 192.168.1.10, ff02::1, ndi-host.local"
- No valid entries: "Configured addresses: not configured"

## Test Assertions
- Developer mode ON + valid address set => rendered output contains actual configured values.
- Developer mode OFF => configured-address developer output absent.
- Multi-address set => all expected valid values present in order.
- Invalid-only set => fallback message rendered.
