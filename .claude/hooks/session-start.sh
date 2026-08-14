#!/bin/bash
#
# SessionStart hook — provisions the .NET SDK for Claude Code on the web.
#
# Web session containers ship without a .NET SDK, so an agent working in one cannot run
# `dotnet build` or `dotnet test` and has to push and wait on CI to learn whether a change
# even compiles. This installs the SDK and warms the restore so the unit-test loop works
# in-session.
#
# Installs from Ubuntu's own archive, NOT from dot.net / dotnet-install.sh.
#   The Microsoft CDN hosts (dot.net, builds.dotnet.microsoft.com, aka.ms,
#   dotnetcli.azureedge.net, download.visualstudio.microsoft.com) are all denied by the
#   environment's egress policy — that is why the install-script approach failed. Ubuntu
#   24.04's noble-updates carries dotnet-sdk-10.0 and archive.ubuntu.com IS reachable.
#
#   One catch: only HTTPS_PROXY is served, so apt's default http:// sources fail. The
#   sources are rewritten to https:// below, which is what makes apt work at all here.
#
# Scope is the base SDK only — NOT the MAUI workload or the Android SDK.
#   src/Core and tests/MauiApp.Tests both target plain net10.0 and the test project
#   references only Core, so `dotnet test tests/MauiApp.Tests` needs nothing more.
#   Building the Android head additionally needs `dotnet workload install maui-android`
#   plus an Android SDK; that is a multi-GB install left to CI deliberately.
#
set -euo pipefail

# Local machines have their own toolchain — this hook is for web sessions only.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

log() { echo "[session-start] $*"; }

# ── Install the SDK (idempotent — a cached container keeps it) ────────────────

if command -v dotnet >/dev/null 2>&1; then
  log "SDK already present ($(dotnet --version 2>/dev/null || echo unknown))"
else
  if [ "$(id -u)" -ne 0 ]; then
    log "WARNING: not root, cannot apt-get install the SDK. Skipping."
    exit 0
  fi

  # apt over plain HTTP does not traverse this environment's HTTPS-only proxy.
  for src in /etc/apt/sources.list.d/ubuntu.sources /etc/apt/sources.list; do
    [ -f "$src" ] && sed -i 's|http://archive.ubuntu.com|https://archive.ubuntu.com|g; s|http://security.ubuntu.com|https://security.ubuntu.com|g' "$src"
  done

  log "Updating package lists ..."
  # Third-party PPAs in this image are blocked by egress policy; their failures are not
  # fatal here, so tolerate a non-zero exit as long as the Ubuntu indexes came through.
  apt-get update -qq 2>/dev/null || log "apt-get update reported errors (blocked PPAs are expected)"

  log "Installing dotnet-sdk-10.0 ..."
  if ! DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0 >/dev/null 2>&1; then
    cat >&2 <<'FAILED'
[session-start] FAILED: could not install dotnet-sdk-10.0 from the Ubuntu archive.

Check that archive.ubuntu.com is reachable over HTTPS from this session:
    curl -sS -o /dev/null -w '%{http_code}\n' https://archive.ubuntu.com/

Do NOT fall back to dot.net / dotnet-install.sh — those hosts are denied by the
environment's egress policy and will fail with a 403 at the proxy.
FAILED
    exit 1
  fi

  log "SDK installed: $(dotnet --version)"
fi

# ── Persist for the session ───────────────────────────────────────────────────

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
    echo 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  } >> "$CLAUDE_ENV_FILE"
fi

# ── Warm the package cache and the build ──────────────────────────────────────
#
# Restoring the test project up front means the agent's first `dotnet test` is fast, and a
# broken restore surfaces here rather than mid-task. Non-fatal: a warm cache is a
# convenience, and failing the hook over it would block the session for something the agent
# can simply retry.

cd "$PROJECT_DIR"

if ! dotnet restore tests/MauiApp.Tests/NdiForAndroid.Tests.csproj --ignore-failed-sources >/dev/null 2>&1; then
  log "WARNING: restore failed — the SDK is installed, so retry 'dotnet restore' in-session."
  exit 0
fi

log "Ready. Unit tests: dotnet test tests/MauiApp.Tests/NdiForAndroid.Tests.csproj"
log "Note: building src/MauiApp (net10.0-android) needs the maui-android workload — CI only."
