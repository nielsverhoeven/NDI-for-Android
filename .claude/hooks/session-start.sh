#!/bin/bash
#
# SessionStart hook — provisions the .NET SDK for Claude Code on the web.
#
# Web session containers ship without a .NET SDK, so an agent working in one cannot
# run `dotnet build` or `dotnet test` and has to push and wait on CI to find out
# whether its change even compiles. This installs the SDK and warms the NuGet cache
# so the unit-test loop works locally.
#
# Scope: the base SDK only — NOT the MAUI workload or the Android SDK.
#   src/Core and tests/MauiApp.Tests both target plain net10.0 and the test project
#   references only Core, so `dotnet test tests/MauiApp.Tests` needs nothing more.
#   Building the Android head (src/MauiApp, net10.0-android) additionally needs
#   `dotnet workload install maui-android` plus an Android SDK; that is a multi-GB
#   install left to CI deliberately.
#
set -euo pipefail

# Local machines have their own toolchain — this hook is for web sessions only.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

# Keep in step with the pin in .github/workflows/ndi-for-android-cicd.yml so local
# results match CI.
DOTNET_VERSION="10.0.301"
DOTNET_INSTALL_DIR="${DOTNET_ROOT:-$HOME/.dotnet}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

log() { echo "[session-start] $*"; }

# ── Install the SDK (idempotent — a cached container keeps it) ────────────────

if [ -x "$DOTNET_INSTALL_DIR/dotnet" ]; then
  log "SDK already present at $DOTNET_INSTALL_DIR ($("$DOTNET_INSTALL_DIR/dotnet" --version 2>/dev/null || echo unknown))"
else
  log "Installing .NET SDK $DOTNET_VERSION into $DOTNET_INSTALL_DIR ..."

  installer="$(mktemp)"
  trap 'rm -f "$installer"' EXIT

  if ! curl -fsSL --retry 3 --retry-delay 2 --max-time 120 \
        https://dot.net/v1/dotnet-install.sh -o "$installer"; then
    cat >&2 <<'BLOCKED'
[session-start] FAILED: could not download the .NET install script.

This is an egress-policy denial, not a transient error: the environment's network
policy does not allow the hosts that serve the .NET SDK. Probed from a web session,
every SDK host returned 403 at the proxy CONNECT while api.nuget.org was reachable.

To make this hook work, allow these hosts in the environment's network policy
(Claude Code on the web → environment settings → network access):

    dot.net
    builds.dotnet.microsoft.com
    aka.ms
    dotnetcli.azureedge.net
    download.visualstudio.microsoft.com

Until then a web session cannot build or test locally and must rely on CI.
See https://code.claude.com/docs/en/claude-code-on-the-web for network policies.
BLOCKED
    exit 1
  fi

  chmod +x "$installer"
  "$installer" --version "$DOTNET_VERSION" --install-dir "$DOTNET_INSTALL_DIR" --no-path
  log "SDK installed: $("$DOTNET_INSTALL_DIR/dotnet" --version)"
fi

export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
export PATH="$DOTNET_INSTALL_DIR:$PATH"

# ── Persist for the session ───────────────────────────────────────────────────

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_INSTALL_DIR\""
    echo "export PATH=\"$DOTNET_INSTALL_DIR:\$PATH\""
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
    echo 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  } >> "$CLAUDE_ENV_FILE"
fi

# ── Warm the package cache and the build ──────────────────────────────────────
#
# Restoring and building the test project up front means the agent's first
# `dotnet test` is fast, and a broken restore surfaces here rather than mid-task.
# Non-fatal: a warm cache is a convenience, and failing the hook over it would
# block the session for something the agent can retry itself.

cd "$PROJECT_DIR"

if ! dotnet restore tests/MauiApp.Tests/NdiForAndroid.Tests.csproj --ignore-failed-sources; then
  log "WARNING: restore failed — the SDK is installed, so retry 'dotnet restore' in-session."
  exit 0
fi

if ! dotnet build tests/MauiApp.Tests/NdiForAndroid.Tests.csproj --no-restore; then
  log "WARNING: warm-up build failed. The SDK is usable; investigate the build error in-session."
  exit 0
fi

log "Ready. Unit tests: dotnet test tests/MauiApp.Tests/NdiForAndroid.Tests.csproj"
log "Note: building src/MauiApp (net10.0-android) needs the maui-android workload — CI only."
