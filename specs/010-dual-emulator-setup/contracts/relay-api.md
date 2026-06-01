# Contract: TCP Relay Server API

**Version**: 1.0 | **Stability**: BETA | **Transport**: In-process (PowerShell function calls, TCP loopback health checks)

---

## Overview

Defines the interface for starting, configuring, monitoring, and stopping a TCP relay server that forwards NDI streams between emulators.

---

## Core Operations

### 1. Start-RelayServer

**Summary**: Start a new TCP relay server with configured routes and health monitoring.

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "listeningPort": 15000,
  "routes": [
    {
      "sourceEmulatorId": "emulator-5554",
      "destEmulatorId": "emulator-5556",
      "sourcePort": 15001,
      "destPort": 15003,
      "protocol": "TCP"
    },
    {
      "sourceEmulatorId": "emulator-5556",
      "destEmulatorId": "emulator-5554",
      "sourcePort": 15003,
      "destPort": 15001,
      "protocol": "TCP"
    }
  ],
  "healthCheckIntervalMs": 5000,
  "maxLatencyMs": 100,
  "autoRestartOnFailure": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relay": {
    "id": "relay-session-abc123",
    "state": "RUNNING",
    "listeningPort": 15000,
    "pidOrProcessHandle": 12345,
    "startTime": "2026-03-23T14:32:00Z",
    "routes": [
      {"id": "route-5554-to-5556", "isActive": true},
      {"id": "route-5556-to-5554", "isActive": true}
    ]
  },
  "startupDurationMs": 500
}
```

**Error Cases**:
- `PORT_IN_USE`: Relay listening port already bound
- `INVALID_ROUTES`: Routes reference non-existent emulators or invalid ports
- `RELAY_PROCESS_FAILED`: Process creation failed
- `HEALTH_CHECK_FAILED`: Initial health check failed

**Idempotency**: NO - calling twice creates two relay processes (use Get-RelayServer to check first)

---

### 2. Get-RelayServer

**Summary**: Query current state of a running relay without modification.

**Input**:
```json
{
  "relayId": "relay-session-abc123"
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relay": {
    "id": "relay-session-abc123",
    "state": "RUNNING",
    "listeningPort": 15000,
    "pidOrProcessHandle": 12345,
    "uptime": {
      "startEpochMs": 1711270320000,
      "lastHealthCheckMs": 1711270380000,
      "restartCount": 0
    },
    "metrics": {
      "totalBytesForwarded": 52428800,
      "peakLatencyMs": 85,
      "avgLatencyMs": 42,
      "packetLossPercent": 0.0
    }
  }
}
```

**Read-Only**: YES - no side effects

---

### 3. Get-RelayMetrics

**Summary**: Fetch detailed performance metrics for a relay server.

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "routeId": "route-5554-to-5556",
  "includeSamples": true,
  "latencySamplesLimit": 100
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relayId": "relay-session-abc123",
  "metrics": {
    "totalBytesForwarded": 52428800,
    "peakLatencyMs": 85,
    "avgLatencyMs": 42,
    "p99LatencyMs": 78,
    "packetLossPercent": 0.0,
    "routeMetrics": [
      {
        "routeId": "route-5554-to-5556",
        "packetsSent": 1024,
        "bytesForwarded": 26214400,
        "latencySamples": [40, 42, 41, 43, 39, ...]
      }
    ]
  }
}
```

---

### 4. Update-RelayRoutes

**Summary**: Add or modify relay routes dynamically without restarting.

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "routes": [
    {
      "sourceEmulatorId": "emulator-5554",
      "destEmulatorId": "emulator-5556",
      "sourcePort": 15001,
      "destPort": 15003,
      "protocol": "TCP",
      "operation": "ADD"
    }
  ]
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relayId": "relay-session-abc123",
  "addedRoutes": [{"id": "route-5554-to-5556", "isActive": true}],
  "removedRoutes": [],
  "errors": []
}
```

**Error Cases**:
- `ROUTE_ALREADY_EXISTS`: Route with same source/dest already active
- `INVALID_ROUTE`: Malformed route definition
- `RELAY_NOT_RUNNING`: Relay must be running to modify routes

---

### 5. Check-RelayHealth

**Summary**: Perform immediate health check (latency, connectivity, packet loss).

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "routeIds": ["route-5554-to-5556"],
  "testPacketSizeBytes": 1024,
  "echoCount": 10
}
```

**Output**:
```json
{
  "status": "HEALTHY",
  "relayId": "relay-session-abc123",
  "checkTime": "2026-03-23T14:35:00Z",
  "routeChecks": [
    {
      "routeId": "route-5554-to-5556",
      "status": "HEALTHY",
      "latencyMs": 42,
      "packetsLost": 0,
      "packetLossPercent": 0.0
    }
  ],
  "relayState": "RUNNING",
  "metricsSnapshot": {
    "avgLatencyMs": 42,
    "peakLatencyMs": 85,
    "packetLossPercent": 0.0
  }
}
```

**Status Values**:
- `HEALTHY`: All metrics within thresholds (latency < 100ms, packet loss = 0%)
- `DEGRADED`: Latency elevated (50-100ms) or minor packet loss (<1%)
- `UNHEALTHY`: Latency > 100ms or packet loss > 1%

---

### 6. Stop-RelayServer

**Summary**: Stop a relay server gracefully.

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "gracefulShutdownTimeoutSeconds": 5,
  "force": false
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relayId": "relay-session-abc123",
  "previousState": "RUNNING",
  "newState": "STOPPED",
  "finalMetrics": {
    "totalBytesForwarded": 52428800,
    "totalDurationMs": 300000,
    "avgLatencyMs": 42
  },
  "gracefulShutdownMs": 1000
}
```

---

### 7. Restart-RelayServer

**Summary**: Stop and restart a relay (preserves routes and ID).

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "force": false,
  "healthCheckAfterRestart": true
}
```

**Output**:
```json
{
  "status": "SUCCESS",
  "relayId": "relay-session-abc123",
  "restartDurationMs": 2000,
  "newPidOrProcessHandle": 12346,
  "healthCheckResult": {
    "status": "HEALTHY",
    "avgLatencyMs": 41
  }
}
```

---

### 8. Monitor-RelayHealth

**Summary**: Start background health monitoring with automatic restarts on failure.

**Input**:
```json
{
  "relayId": "relay-session-abc123",
  "healthCheckIntervalMs": 5000,
  "unhealthyThresholdChecks": 3,
  "autoRestartEnabled": true,
  "maxRestarts": 5
}
```

**Output**:
```json
{
  "status": "MONITORING_STARTED",
  "monitorPid": 54321,
  "configuration": {
    "healthCheckIntervalMs": 5000,
    "unhealthyThresholdChecks": 3,
    "autoRestartEnabled": true,
    "maxRestarts": 5
  }
}
```

**Monitoring Workflow**:
1. Every 5 seconds, perform health check
2. If unhealthy, increment failure counter
3. After 3 consecutive failures, trigger automatic restart
4. Stop after 5 restarts (max-restarts limit)
5. Log all state transitions to artifact collection

---

## Health Check Thresholds

**Healthy**:
- Latency: 0-50ms
- Packet Loss: 0%
- Routes: All active

**Degraded**:
- Latency: 50-100ms
- Packet Loss: 0-1%

**Unhealthy**:
- Latency: > 100ms (SC-002 violation)
- Packet Loss: > 1%
- Any route inactive

---

## Error Handling Contract

All operations return structured error responses:

```json
{
  "status": "FAILURE",
  "errorCode": "RELAY_PROCESS_FAILED",
  "errorMessage": "Relay server process exited with code 1",
  "details": {
    "relayId": "relay-session-abc123",
    "pidOrProcessHandle": 12345,
    "lastErrorLog": "Socket bind failed on port 15000",
    "exitCode": 1
  },
  "retryable": true,
  "suggestedAction": "Check port availability; try alternate port 15001"
}
```

---

## Constraints

**Performance Targets** (from SC-002):
- Relay round-trip latency: < 100ms (98th percentile)
- Packet loss: 0% (ideal), < 1% acceptable
- Throughput: No explicit limit, NDI streaming default

**Relay Limits**:
- Maximum routes per relay: 4 (MVP: 2 bidirectional = 2 routes)
- Maximum concurrent relays per host: 1 (MVP)
- Relay port range: 15000-15010

**Monitoring**:
- Health check interval: 1000-60000 ms (default 5000ms)
- Unhealthy threshold: 1-10 consecutive failures (default 3)
- Maximum auto-restarts: 1-10 per session (default 5)

---

## Dependencies

**Dependent On**:
- Provisioning API: Relay requires running emulators
- TCP/IP networking: Localhost loopback interface must be available

**Depended On By**:
- Feature 009 (Latency Measurement): Uses relay for inter-device NDI streaming

---

**Status**: ✅ **COMPLETE**
