# Dual-Emulator Network Preflight

## Topology Checks

- Emulator A and emulator B are running simultaneously.
- Both emulators are reachable with adb (`adb devices`).
- Both emulators are attached to the same multicast-capable host network.
- No host firewall rule blocks emulator-to-emulator UDP multicast or unicast discovery traffic.

## Readiness Checks

- App debug build is installed on both emulators.
- Publisher source is discoverable on emulator A before output start.
- Receiver source list refresh is functional on emulator B.

## Failure Handling

- If discovery fails on emulator B, capture logcat for both emulators and rerun after topology correction.
- If publisher cannot reach ACTIVE, capture output-state telemetry and abort the run.

## Preflight Execution Record

| Date | Engineer | Emulator A Serial | Emulator B Serial | Result | Notes |
|---|---|---|---|---|---|
| TBD | TBD | TBD | TBD | NOT_RUN | Pending first execution |
