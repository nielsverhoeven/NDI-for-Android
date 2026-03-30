# 022 Preflight: NDI SDK and Discovery Endpoints

Status: BLOCKED:ENVIRONMENT

- NDI SDK build wiring is present in ndi/sdk-bridge and native library loads at runtime.
- Reachable and unreachable discovery endpoint validation could not be executed in this environment because no live endpoint pair was provisioned for this run.
- Unblock: provide one reachable endpoint (host:port) and one intentionally unreachable endpoint, then rerun endpoint checks during emulator validation.
