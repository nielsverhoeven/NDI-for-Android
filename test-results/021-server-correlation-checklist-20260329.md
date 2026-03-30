# Discovery Server Correlation Checklist (2026-03-29)

## Client Capture

- Artifact: `test-results/021-discovery-server-correlation-20260329-195458.log`
- Device launch epoch (from adb): `1774806898.571`
- First discovery event epoch: `1774806899.352`
- Last event in capture epoch: `1774806917.004`
- First discovery event (UTC): `2026-03-29 17:54:59.352`
- Last event (UTC): `2026-03-29 17:55:17.004`
- First discovery event (local +02:00): `2026-03-29 19:54:59.352`
- Last event (local +02:00): `2026-03-29 19:55:17.004`
- Configured endpoint during run: `10.10.0.53:5959`
- Stale endpoint observed in this run: none (`192.168.2.23` not present)

## What To Match On Discovery Server

Search your Discovery Server logs for this epoch range:

- Start: `1774806899.0`
- End: `1774806917.5`

Equivalent wall-clock ranges:

- UTC: `2026-03-29 17:54:59` to `2026-03-29 17:55:17`
- Local (+02:00): `2026-03-29 19:54:59` to `2026-03-29 19:55:17`

Client-side expected events in that window:

1. Initial discovery triggers against `10.10.0.53:5959`.
2. `send-listener ... connected=false ... senderCount=0`.
3. `find-extra-ip extraIp=10.10.0.53, sourceCount=0`.
4. `discover total=0`.

## Interpretation

- If server logs show **no inbound requests** from tablet/client IP in this window:
  - issue is upstream of server discovery handling (network path, routing, ACL, firewall, or device network policy).
- If server logs show requests and healthy sender inventory:
  - issue is likely Android-side NDI SDK interaction/response parsing path.
- If server logs show requests but **no senders registered**:
  - source registration path to Discovery Server is the primary issue.

## Quick Verification Already Done

- Host TCP to `10.10.0.53:5959`: success.
- Tablet TCP to `10.10.0.53:5959` (adb shell netcat): success.
- On-device DB after reset contains only `10.10.0.53:5959`.
