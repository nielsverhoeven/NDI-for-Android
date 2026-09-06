# Technical Plan — PTZ Control over VISCA-over-IP (TCP) for NDI Sources

**Issue:** #339 · **Branch:** `feature/339-visca-ptz-endpoint`
**Companion spec:** `docs/features/ptz-visca-endpoint/spec.md`
**Audience:** Sonnet developer(s) — every step below is exact (file path, member names, signatures). If something here doesn't compile as written, treat it as a plan bug and ask rather than improvising.

---

## 1. Architecture Notes (for `solution-architect` sign-off, per issue D6)

- **New Core slice**: `src/Core/Features/Ptz/` (Models/Services/ViewModels). No new repository — persistence reuses `ISourceRepository.SaveSourceAsync(NdiSource)` (already takes the whole record).
- **New seam**: `IPtzController` (Core), two impls — `NdiPtzController` (wraps `INdiViewerBridge`, **zero behavior change** to NDI PTZ) and `ViscaPtzController` (new raw VISCA/TCP client). Selected per active `NdiSource` by `IPtzControllerFactory`.
- **Socket lives in Core, not MauiApp** — deviates from the `NetworkReachability.cs` precedent (sits in `src/MauiApp/NdiBridge/` despite being plain `TcpClient`). Per issue #339's own analysis: Core is plain `net10.0`, `System.Net.Sockets` is already available there, and keeping the whole VISCA stack MAUI-free keeps it unit-testable with a fake transport. One-off exception — VISCA isn't part of the NDI bridge boundary (rule 2 is about NDI SDK types, not all networking).
- **Shared-file impact (minimal, isolated)**: `ViewerViewModel.cs` / `ViewerView.xaml(.cs)` are shared with a parallel in-flight feature, integrated last. Diff confined to: 2 new ctor params, 3 new fields, 1 new public property (nested form VM), 3 new private helpers, edited bodies of 3 existing PTZ command methods, 2 new preset commands, 1 new "open editor" command, 1 new event handler, and one XAML section. No other method signatures change except `PtzAutoFocus` (`void` → `async Task`, still `ICommand`-safe).
- **No new NuGet packages** — `TcpClient`/`NetworkStream` are BCL, already used in this repo.

---

## 2. New Files — Core (`src/Core/Features/Ptz/`)

### 2.1 `Models/PtzModels.cs`

```csharp
namespace NdiForAndroid.Features.Ptz.Models;

public enum PtzConnectionStatus { Unconfigured, Connecting, Connected, Error }

/// <summary>VISCA-over-TCP endpoint. VISCA device address is fixed at 1.</summary>
public sealed record PtzEndpoint(string Host, int Port)
{
    public const int DefaultViscaPort = 5678;
}

/// <summary>Raised on Save; null Host clears the override (falls back to NDI PTZ).</summary>
public sealed record PtzEndpointSavedEventArgs(string? Host, int? Port);
```

### 2.2 `Services/IViscaTransport.cs`

Mockable raw-socket seam for VISCA-over-TCP; one instance == one TCP connection.

```csharp
namespace NdiForAndroid.Features.Ptz.Services;

public interface IViscaTransport
{
    bool IsConnected { get; }
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);
    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);
    Task<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken = default); // one reply frame, up to and including terminating 0xFF
    Task DisconnectAsync();
}

public interface IViscaTransportFactory { IViscaTransport Create(); }
```

### 2.3 `Services/ViscaTcpTransport.cs`

`public sealed class ViscaTcpTransport : IViscaTransport` using `System.Net.Sockets.TcpClient` (raw VISCA over TCP — PTZOptics-style, **no** Sony VISCA-over-IP UDP header). Fields: `TcpClient? _client`, `NetworkStream? _stream`.

- `IsConnected => _client?.Connected == true && _stream is not null`
- `ConnectAsync(host, port, ct)`: call `DisconnectAsync()` first (idempotent reconnect), then `new TcpClient()`, `await client.ConnectAsync(host, port, ct)`, store client + `client.GetStream()`.
- `SendAsync(payload, ct)`: throw `InvalidOperationException` if `_stream is null`; else `await _stream.WriteAsync(payload, ct)`.
- `ReceiveFrameAsync(ct)`: throw if `_stream is null`; else read **one byte at a time** via `_stream.ReadAsync` into a `List<byte>` buffer, appending each byte, until a read returns `0` (throw `IOException`, "connection closed") or the byte read is `0xFF` (frame complete — VISCA replies always terminate with `0xFF`); return `buffer.ToArray()`.
- `DisconnectAsync()`: dispose `_stream` and close+dispose `_client`, null both fields; return `Task.CompletedTask`.

`public sealed class ViscaTransportFactory : IViscaTransportFactory { public IViscaTransport Create() => new ViscaTcpTransport(); }`

### 2.4 `Services/ViscaCommandEncoder.cs`

Pure functions, address fixed at `0x81` (`0x80 | 1`, per D3 "VISCA address 1").

```csharp
namespace NdiForAndroid.Features.Ptz.Services;

public static class ViscaCommandEncoder
{
    private const byte Address = 0x81;

    // 81 01 06 01 VV WW 0p 0t FF. Pan dir: 1=left,2=right. Tilt dir: 1=up,2=down.
    public static byte[] PanTiltDrive(float panSpeed, float tiltSpeed)
    {
        var (panDir, panByte) = EncodeAxis(panSpeed, positiveDirection: 0x02, negativeDirection: 0x01);
        var (tiltDir, tiltByte) = EncodeAxis(tiltSpeed, positiveDirection: 0x01, negativeDirection: 0x02);
        return new byte[] { Address, 0x01, 0x06, 0x01, panByte, tiltByte, panDir, tiltDir, 0xFF };
    }

    // value==0 => stop (dir=3, speed=0x00); else speed = clamp(round(|value|*23)+1, 1, 24).
    private static (byte direction, byte speed) EncodeAxis(float value, byte positiveDirection, byte negativeDirection)
    {
        if (value == 0f) return (0x03, 0x00);
        var direction = value > 0 ? positiveDirection : negativeDirection;
        var magnitude = Math.Clamp(Math.Abs(value), 0f, 1f);
        var speed = (byte)Math.Clamp((int)Math.Round(magnitude * 23) + 1, 1, 24);
        return (direction, speed);
    }

    // 81 01 04 07 2p FF (tele/in) | 3p FF (wide/out) | 00 FF (stop). p = speed 1-7.
    public static byte[] ZoomSpeed(float zoomSpeed)
    {
        if (zoomSpeed == 0f) return new byte[] { Address, 0x01, 0x04, 0x07, 0x00, 0xFF };
        var magnitude = Math.Clamp(Math.Abs(zoomSpeed), 0f, 1f);
        var speed = (byte)Math.Clamp((int)Math.Round(magnitude * 7), 1, 7);
        byte prefix = zoomSpeed > 0 ? (byte)0x20 : (byte)0x30;
        return new byte[] { Address, 0x01, 0x04, 0x07, (byte)(prefix | speed), 0xFF };
    }

    public static byte[] AutoFocus() => new byte[] { Address, 0x01, 0x04, 0x18, 0x01, 0xFF }; // one-push auto-focus

    public static byte[] StorePreset(int presetNo) => // 81 01 04 3F 01 pp FF, pp = 0-99 clamped
        new byte[] { Address, 0x01, 0x04, 0x3F, 0x01, ClampPreset(presetNo), 0xFF };

    public static byte[] RecallPreset(int presetNo) => // 81 01 04 3F 02 pp FF, pp = 0-99 clamped
        new byte[] { Address, 0x01, 0x04, 0x3F, 0x02, ClampPreset(presetNo), 0xFF };

    private static byte ClampPreset(int presetNo) => (byte)Math.Clamp(presetNo, 0, 99);
}
```

### 2.5 `Services/ViscaResponseParser.cs`

ACK `90 4Y FF`, Completion `90 5Y FF`, Error `90 6Y ZZ FF` (Y=socket, ZZ=error code).

```csharp
namespace NdiForAndroid.Features.Ptz.Services;

public enum ViscaResponseKind { Ack, Completion, Error, Unknown }
public sealed record ViscaResponse(ViscaResponseKind Kind, int Socket, byte? ErrorCode);

public static class ViscaResponseParser
{
    public static ViscaResponse Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3 || frame[frame.Length - 1] != 0xFF)
            return new ViscaResponse(ViscaResponseKind.Unknown, 0, null);

        var b1 = frame[1];
        var socket = b1 & 0x0F;
        return (byte)(b1 & 0xF0) switch
        {
            0x40 => new ViscaResponse(ViscaResponseKind.Ack, socket, null),
            0x50 => new ViscaResponse(ViscaResponseKind.Completion, socket, null),
            0x60 when frame.Length >= 4 => new ViscaResponse(ViscaResponseKind.Error, socket, frame[2]),
            _ => new ViscaResponse(ViscaResponseKind.Unknown, socket, null),
        };
    }
}
```

### 2.6 `Services/IPtzController.cs`

ViewModel-facing PTZ seam — one instance per active source's backend.

```csharp
using NdiForAndroid.Features.Ptz.Models;

namespace NdiForAndroid.Features.Ptz.Services;

public interface IPtzController
{
    PtzConnectionStatus ConnectionStatus { get; }
    event EventHandler<PtzConnectionStatus>? ConnectionStatusChanged;

    Task<bool> PanTiltAsync(float panSpeed, float tiltSpeed, CancellationToken ct = default);
    Task<bool> ZoomAsync(float zoomSpeed, CancellationToken ct = default);
    Task<bool> AutoFocusAsync(CancellationToken ct = default);
    Task<bool> StorePresetAsync(int presetNo, CancellationToken ct = default);
    Task<bool> RecallPresetAsync(int presetNo, CancellationToken ct = default);

    Task DisposeAsync(); // plain Task (not IAsyncDisposable) so callers use TaskExtensions.FireAndForget (src/Core/Services/TaskExtensions.cs)
}
```

### 2.7 `Services/NdiPtzController.cs`

Thin wrapper — each method is `Task.FromResult(_bridge.<SameMethod>(...))`; no new behavior.

```csharp
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Ptz.Services;

public sealed class NdiPtzController : IPtzController
{
    private readonly INdiViewerBridge _bridge;
    public NdiPtzController(INdiViewerBridge bridge) => _bridge = bridge;

    public PtzConnectionStatus ConnectionStatus =>
        _bridge.IsPtzSupported ? PtzConnectionStatus.Connected : PtzConnectionStatus.Unconfigured;

    public event EventHandler<PtzConnectionStatus>? ConnectionStatusChanged { add { } remove { } } // NDI PTZ support is polled via IsPtzSupported elsewhere — no push events

    public Task<bool> PanTiltAsync(float p, float t, CancellationToken ct = default) => Task.FromResult(_bridge.PtzPanTiltSpeed(p, t));
    public Task<bool> ZoomAsync(float z, CancellationToken ct = default) => Task.FromResult(_bridge.PtzZoomSpeed(z));
    public Task<bool> AutoFocusAsync(CancellationToken ct = default) => Task.FromResult(_bridge.PtzAutoFocus());
    public Task<bool> StorePresetAsync(int n, CancellationToken ct = default) => Task.FromResult(_bridge.PtzStorePreset(n));
    public Task<bool> RecallPresetAsync(int n, CancellationToken ct = default) => Task.FromResult(_bridge.PtzRecallPreset(n));
    public Task DisposeAsync() => Task.CompletedTask;
}
```

### 2.8 `Services/ViscaPtzController.cs`

One persistent connection per endpoint, lazy connect, reconnect-on-failure (D3). Each public method sends one command and awaits exactly one reply frame: Ack/Completion → success; a parsed VISCA-level Error → `false` but link stays `Connected` (healthy link, command rejected); any exception (connect/send/receive/timeout) → `Error` status + force reconnect next call.

```csharp
using NdiForAndroid.Features.Ptz.Models;

namespace NdiForAndroid.Features.Ptz.Services;

public sealed class ViscaPtzController : IPtzController
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    private readonly IViscaTransport _transport;
    private readonly PtzEndpoint _endpoint;
    private readonly object _lock = new();
    private PtzConnectionStatus _status = PtzConnectionStatus.Connecting;

    public ViscaPtzController(IViscaTransport transport, PtzEndpoint endpoint)
    { _transport = transport; _endpoint = endpoint; }

    public PtzConnectionStatus ConnectionStatus { get { lock (_lock) return _status; } }
    public event EventHandler<PtzConnectionStatus>? ConnectionStatusChanged;

    public Task<bool> PanTiltAsync(float p, float t, CancellationToken ct = default) => SendCommandAsync(ViscaCommandEncoder.PanTiltDrive(p, t), ct);
    public Task<bool> ZoomAsync(float z, CancellationToken ct = default) => SendCommandAsync(ViscaCommandEncoder.ZoomSpeed(z), ct);
    public Task<bool> AutoFocusAsync(CancellationToken ct = default) => SendCommandAsync(ViscaCommandEncoder.AutoFocus(), ct);
    public Task<bool> StorePresetAsync(int n, CancellationToken ct = default) => SendCommandAsync(ViscaCommandEncoder.StorePreset(n), ct);
    public Task<bool> RecallPresetAsync(int n, CancellationToken ct = default) => SendCommandAsync(ViscaCommandEncoder.RecallPreset(n), ct);

    private async Task<bool> SendCommandAsync(byte[] command, CancellationToken ct)
    {
        try
        {
            if (!_transport.IsConnected)
            {
                SetStatus(PtzConnectionStatus.Connecting);
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(ConnectTimeout);
                await _transport.ConnectAsync(_endpoint.Host, _endpoint.Port, connectCts.Token);
            }
            await _transport.SendAsync(command, ct);

            using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            receiveCts.CancelAfter(CommandTimeout);
            var response = ViscaResponseParser.Parse(await _transport.ReceiveFrameAsync(receiveCts.Token));

            SetStatus(PtzConnectionStatus.Connected);
            return response.Kind is ViscaResponseKind.Ack or ViscaResponseKind.Completion;
        }
        catch
        {
            await _transport.DisconnectAsync();
            SetStatus(PtzConnectionStatus.Error);
            return false;
        }
    }

    private void SetStatus(PtzConnectionStatus status)
    {
        bool changed;
        lock (_lock) { changed = _status != status; _status = status; }
        if (changed) ConnectionStatusChanged?.Invoke(this, status);
    }

    public async Task DisposeAsync() => await _transport.DisconnectAsync();
}
```

### 2.9 `Services/IPtzControllerFactory.cs` + `PtzControllerFactory.cs`

```csharp
using NdiForAndroid.Features.Sources.Models;

namespace NdiForAndroid.Features.Ptz.Services;

public interface IPtzControllerFactory { IPtzController Create(NdiSource? source); } // null source or blank PtzOverrideHost => NDI receiver PTZ; otherwise VISCA
```

```csharp
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Ptz.Services;

public sealed class PtzControllerFactory : IPtzControllerFactory
{
    private readonly INdiViewerBridge _bridge;
    private readonly IViscaTransportFactory _transportFactory;

    public PtzControllerFactory(INdiViewerBridge bridge, IViscaTransportFactory transportFactory)
    { _bridge = bridge; _transportFactory = transportFactory; }

    public IPtzController Create(NdiSource? source)
    {
        if (!string.IsNullOrWhiteSpace(source?.PtzOverrideHost))
        {
            var endpoint = new PtzEndpoint(source.PtzOverrideHost!.Trim(), source.PtzOverridePort ?? PtzEndpoint.DefaultViscaPort);
            return new ViscaPtzController(_transportFactory.Create(), endpoint);
        }
        return new NdiPtzController(_bridge);
    }
}
```

### 2.10 `ViewModels/PtzEndpointFormViewModel.cs`

Self-contained nested `ObservableObject` — the *only* new state `ViewerViewModel` exposes for the dialog is one property holding this instance (§4.2). Mirrors `SettingsViewModel`'s edit-server dialog pattern, scoped to a single endpoint (no list). Properties (all `[ObservableProperty]`): `bool IsDialogOpen`, `string Host = ""`, `string Port = PtzEndpoint.DefaultViscaPort.ToString()`, `string ValidationMessage = ""`, `string TestResultMessage = ""`. Event: `event EventHandler<PtzEndpointSavedEventArgs>? SaveRequested`. Ctor: `PtzEndpointFormViewModel(IViscaTransportFactory transportFactory)`.

```csharp
public void Open(string? host, int? port) // called by ViewerViewModel.OpenPtzEndpointEditorCommand with the current override
{
    Host = host ?? string.Empty;
    Port = (port ?? PtzEndpoint.DefaultViscaPort).ToString();
    ValidationMessage = string.Empty;
    TestResultMessage = string.Empty;
    IsDialogOpen = true;
}

[RelayCommand]
private void Save()
{
    if (!TryParseInput(out var host, out var port, out var error)) { ValidationMessage = error; return; }
    ValidationMessage = string.Empty;
    IsDialogOpen = false;
    SaveRequested?.Invoke(this, new PtzEndpointSavedEventArgs(host, port));
}

[RelayCommand]
private void Cancel() { IsDialogOpen = false; ValidationMessage = string.Empty; TestResultMessage = string.Empty; }
```

`[RelayCommand] private async Task Test()`: trim `Host`; if blank set `TestResultMessage = "Enter a host to test."` and return. Parse `Port` (fallback `DefaultViscaPort`). Set `TestResultMessage = "Testing..."`, create a transport via `_transportFactory.Create()`, `try` a `ConnectAsync(host, port, cts.Token)` with a 3s `CancellationTokenSource` → `"Connected."`; `catch (OperationCanceledException)` → `"Timed out."`; `catch (Exception ex)` → `$"Failed: {ex.Message}"`; `finally` always `await transport.DisconnectAsync()` (the test connection is never kept — it's separate from the persistent `ViscaPtzController` connection).

`private bool TryParseInput(out string? host, out int? port, out string error)`: trim `Host`; if blank → `(null, null, "")`/`true` (clears the override, falls back to NDI PTZ). Else if `Port` is non-blank, parse it and require `1-65535` (else set `error` and return `false`); if `Port` is blank, default to `PtzEndpoint.DefaultViscaPort`. Return `(trimmedHost, port, "")`/`true`.

---

## 3. Data Layer — `NdiSource` + `NdiDatabase` (additive migration)

**`src/Core/Features/Sources/Models/SourceModels.cs`** — add two trailing nullable params to `NdiSource` (same additive pattern as `DiscoveryMode`/`QualityProfile`):

```csharp
public record NdiSource(
    string SourceId, string DisplayName, string? EndpointAddress, bool IsAvailable,
    long LastSeenAtEpochMillis, bool PreviouslyConnected = false,
    DiscoveryMode DiscoveryMode = DiscoveryMode.Mdns,
    QualityProfile QualityProfile = QualityProfile.Balanced,
    string? PtzOverrideHost = null,
    int? PtzOverridePort = null);
```

**`src/Core/Data/NdiDatabase.cs`**:

- `SourceEntity` (~line 13) — add after `QualityProfile`: `public string? PtzOverrideHost { get; set; }` and `public int? PtzOverridePort { get; set; }`.
- `EnsureSourceColumnsAsync` (~line 270) — add after the `QualityProfile` column check:
  ```csharp
  if (!columnNames.Contains("PtzOverrideHost"))
      await _connection.ExecuteAsync("ALTER TABLE sources ADD COLUMN PtzOverrideHost TEXT");
  if (!columnNames.Contains("PtzOverridePort"))
      await _connection.ExecuteAsync("ALTER TABLE sources ADD COLUMN PtzOverridePort INTEGER");
  ```
- `UpsertSourceAsync` (~line 183) — add to the `SourceEntity` initializer: `PtzOverrideHost = source.PtzOverrideHost, PtzOverridePort = source.PtzOverridePort,`.
- `GetSourcesAsync` (~line 200) — extend the `NdiSource(...)` construction with two trailing args: `..., e.PtzOverrideHost, e.PtzOverridePort` (positional, matching the new param order above).

No other repository changes — `ISourceRepository`/`SourceRepository` pass the whole record through unchanged.

---

## 4. DI Root + ViewModel/View Wiring

### 4.1 `src/MauiApp/MauiProgram.cs`

Add near the other NDI Bridge/repository registrations (after `INdiOutputBridge`):

```csharp
// PTZ (VISCA) — Core-only services, no MAUI dependency (issue #339).
builder.Services.AddSingleton<Features.Ptz.Services.IViscaTransportFactory, Features.Ptz.Services.ViscaTransportFactory>();
builder.Services.AddSingleton<Features.Ptz.Services.IPtzControllerFactory, Features.Ptz.Services.PtzControllerFactory>();
builder.Services.AddTransient<Features.Ptz.ViewModels.PtzEndpointFormViewModel>();
```

(Or add `using NdiForAndroid.Features.Ptz.Services;`/`.ViewModels;` and drop the `Features.Ptz.` prefix, matching this file's existing `using`-block style.)

### 4.2 `ViewerViewModel.cs` — new constructor params, fields, properties

Extend the constructor (currently 7 params) with **two new trailing params**: `IPtzControllerFactory ptzControllerFactory, PtzEndpointFormViewModel ptzEndpointForm`. New usings: `NdiForAndroid.Features.Ptz.Models`, `.Services`, `.ViewModels`.

New fields: `private readonly IPtzControllerFactory _ptzControllerFactory;` · `private IPtzController? _ptzController;` · `private NdiSource? _activeSource;`

New public property (XAML binds `PtzEndpointForm.Host` etc.): `public PtzEndpointFormViewModel PtzEndpointForm { get; }`

New observable properties:
```csharp
[ObservableProperty] private PtzConnectionStatus _ptzConnectionStatus = PtzConnectionStatus.Unconfigured;
[ObservableProperty] private string? _ptzStatusMessage;
[ObservableProperty] private string _ptzPresetNumber = "0";
```

New computed property + change hooks (gates the pad — see §4.4 for why it differs from `IsPtzSupported` alone):
```csharp
public bool IsPtzControlActive => IsPtzSupported || PtzConnectionStatus == PtzConnectionStatus.Connected;
partial void OnIsPtzSupportedChanged(bool value) => OnPropertyChanged(nameof(IsPtzControlActive));
partial void OnPtzConnectionStatusChanged(PtzConnectionStatus value) => OnPropertyChanged(nameof(IsPtzControlActive));
```

Constructor body: `_ptzControllerFactory = ptzControllerFactory;` · `PtzEndpointForm = ptzEndpointForm;` · `PtzEndpointForm.SaveRequested += OnPtzEndpointSaveRequested;` (unsubscribe in `Dispose()`).

### 4.3 `ViewerViewModel.cs` — behavior changes

**`Start()`** — after the existing quality-profile restore `try`/`catch`, before `_bridge.StartReceiver(...)`. Adds a third small `GetCachedSourcesAsync()` fetch, matching the method's existing style of one fetch per concern (it already fetches twice: quality profile, display name) rather than refactoring:
```csharp
NdiSource? ptzSource = null;
try
{
    var sourcesForPtz = await _sourceRepository.GetCachedSourcesAsync();
    ptzSource = sourcesForPtz.FirstOrDefault(s => s.SourceId == SourceId);
}
catch { /* Silent fail – falls back to NDI PTZ */ }
_activeSource = ptzSource;
AttachPtzController(_ptzControllerFactory.Create(ptzSource));
```

**`Stop()`** — alongside the existing `IsPtzSupported = false;` reset:
```csharp
if (_ptzController is not null)
{
    _ptzController.ConnectionStatusChanged -= OnPtzConnectionStatusChanged;
    _ptzController.DisposeAsync().FireAndForget();
    _ptzController = null;
}
PtzConnectionStatus = PtzConnectionStatus.Unconfigured;
PtzStatusMessage = null;
```
(`FireAndForget` — `src/Core/Services/TaskExtensions.cs`, already used elsewhere in this file.)

**Three new private helpers**:
```csharp
private void AttachPtzController(IPtzController controller)
{
    if (_ptzController is not null)
    {
        _ptzController.ConnectionStatusChanged -= OnPtzConnectionStatusChanged;
        _ptzController.DisposeAsync().FireAndForget();
    }
    _ptzController = controller;
    _ptzController.ConnectionStatusChanged += OnPtzConnectionStatusChanged;
    PtzConnectionStatus = _ptzController.ConnectionStatus;
    PtzStatusMessage = DescribePtzStatus(PtzConnectionStatus);
}

private void OnPtzConnectionStatusChanged(object? sender, PtzConnectionStatus status) =>
    _dispatcher.BeginInvokeOnMainThread(() => { PtzConnectionStatus = status; PtzStatusMessage = DescribePtzStatus(status); });

private static string DescribePtzStatus(PtzConnectionStatus status) => status switch
{
    PtzConnectionStatus.Connected => "PTZ: Connected",
    PtzConnectionStatus.Connecting => "PTZ: Connecting...",
    PtzConnectionStatus.Error => "PTZ: Connection error",
    _ => "PTZ: Using NDI",
};
```

**Replace bodies** of the three existing PTZ commands (signatures/`[RelayCommand]` unchanged except `PtzAutoFocus` becomes `async Task` — still `ICommand`-compatible via `IAsyncRelayCommand`):
```csharp
[RelayCommand]
private async Task PtzNudge(string? direction)
{
    var (pan, tilt) = direction switch
    {
        "left" => (-PtzNudgeSpeed, 0f), "right" => (PtzNudgeSpeed, 0f),
        "up" => (0f, PtzNudgeSpeed), "down" => (0f, -PtzNudgeSpeed), _ => (0f, 0f),
    };
    if ((pan == 0f && tilt == 0f) || _ptzController is null) return;
    await _ptzController.PanTiltAsync(pan, tilt);
    await Task.Delay(PtzNudgeDurationMs);
    await _ptzController.PanTiltAsync(0f, 0f);
}

[RelayCommand]
private async Task PtzZoomNudge(string? direction)
{
    var speed = direction switch { "in" => PtzNudgeSpeed, "out" => -PtzNudgeSpeed, _ => 0f };
    if (speed == 0f || _ptzController is null) return;
    await _ptzController.ZoomAsync(speed);
    await Task.Delay(PtzNudgeDurationMs);
    await _ptzController.ZoomAsync(0f);
}

[RelayCommand]
private async Task PtzAutoFocus() { if (_ptzController is not null) await _ptzController.AutoFocusAsync(); }
```

**Two new commands** (preset store/recall — net-new UI per spec D5) and **one new command** (opens the editor with the currently persisted override):
```csharp
[RelayCommand]
private async Task PtzStorePreset()
{
    if (_ptzController is null || !int.TryParse(PtzPresetNumber, out var presetNo)) return;
    await _ptzController.StorePresetAsync(presetNo);
}

[RelayCommand]
private async Task PtzRecallPreset()
{
    if (_ptzController is null || !int.TryParse(PtzPresetNumber, out var presetNo)) return;
    await _ptzController.RecallPresetAsync(presetNo);
}

[RelayCommand]
private void OpenPtzEndpointEditor() => PtzEndpointForm.Open(_activeSource?.PtzOverrideHost, _activeSource?.PtzOverridePort);
```

**One new event handler** (subscribed in the constructor; persists the override + rebuilds the controller):
```csharp
// async void is intentional: event handler, same pattern as OnAppResumed elsewhere in this file.
private async void OnPtzEndpointSaveRequested(object? sender, PtzEndpointSavedEventArgs e)
{
    try
    {
        if (string.IsNullOrEmpty(SourceId)) return;
        var sources = await _sourceRepository.GetCachedSourcesAsync();
        var source = sources.FirstOrDefault(s => s.SourceId == SourceId);
        if (source is null) return;
        var updated = source with { PtzOverrideHost = e.Host, PtzOverridePort = e.Port };
        await _sourceRepository.SaveSourceAsync(updated);
        _activeSource = updated;
        AttachPtzController(_ptzControllerFactory.Create(updated));
    }
    catch { /* Silent fail — endpoint edit is non-critical; status indicator reflects the retry. */ }
}
```

**`Dispose()`**: add `PtzEndpointForm.SaveRequested -= OnPtzEndpointSaveRequested;` and `_ptzController?.DisposeAsync().FireAndForget();` alongside the existing unsubscribes.

### 4.4 `ViewerView.xaml` — PTZ panel changes

Change the outer PTZ container's `IsVisible` from `IsPtzSupported` to `IsPlaying` — the "configure endpoint" affordance must be reachable even when the connected NDI source isn't itself PTZ-capable (spec acceptance criteria). Wrap the pad/zoom controls in an inner container gated on the new `IsPtzControlActive`. Add a header row (label, Endpoint button, status label) and a preset row. Replace lines 49-69 with:

```xml
<VerticalStackLayout HorizontalOptions="Center" Spacing="8" IsVisible="{Binding IsPlaying}">
    <HorizontalStackLayout HorizontalOptions="Center" Spacing="8">
        <Label Text="PTZ" VerticalOptions="Center" TextColor="{DynamicResource TextSecondary}" />
        <Button Text="Endpoint" Command="{Binding OpenPtzEndpointEditorCommand}" />
        <Label Text="{Binding PtzStatusMessage}" VerticalOptions="Center">
            <Label.Style>
                <Style TargetType="Label">
                    <Setter Property="TextColor" Value="{DynamicResource TextSecondary}" />
                    <Style.Triggers>
                        <DataTrigger TargetType="Label" Binding="{Binding PtzConnectionStatus}" Value="Connected">
                            <Setter Property="TextColor" Value="{DynamicResource SuccessGreen}" />
                        </DataTrigger>
                        <DataTrigger TargetType="Label" Binding="{Binding PtzConnectionStatus}" Value="Error">
                            <Setter Property="TextColor" Value="{DynamicResource ErrorRed}" />
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </Label.Style>
        </Label>
    </HorizontalStackLayout>

    <VerticalStackLayout HorizontalOptions="Center" Spacing="8" IsVisible="{Binding IsPtzControlActive}">
        <Grid RowDefinitions="Auto,Auto,Auto" ColumnDefinitions="Auto,Auto,Auto" RowSpacing="8" ColumnSpacing="8" HorizontalOptions="Center">
            <Button Grid.Row="0" Grid.Column="1" Text="▲" Command="{Binding PtzNudgeCommand}" CommandParameter="up" />
            <Button Grid.Row="1" Grid.Column="0" Text="◄" Command="{Binding PtzNudgeCommand}" CommandParameter="left" />
            <Button Grid.Row="1" Grid.Column="1" Text="AF" Command="{Binding PtzAutoFocusCommand}" />
            <Button Grid.Row="1" Grid.Column="2" Text="►" Command="{Binding PtzNudgeCommand}" CommandParameter="right" />
            <Button Grid.Row="2" Grid.Column="1" Text="▼" Command="{Binding PtzNudgeCommand}" CommandParameter="down" />
        </Grid>
        <HorizontalStackLayout HorizontalOptions="Center" Spacing="8">
            <Button Text="Zoom −" Command="{Binding PtzZoomNudgeCommand}" CommandParameter="out" />
            <Button Text="Zoom +" Command="{Binding PtzZoomNudgeCommand}" CommandParameter="in" />
        </HorizontalStackLayout>
        <HorizontalStackLayout HorizontalOptions="Center" Spacing="8">
            <Entry Text="{Binding PtzPresetNumber}" Keyboard="Numeric" WidthRequest="60" />
            <Button Text="Store" Command="{Binding PtzStorePresetCommand}" />
            <Button Text="Recall" Command="{Binding PtzRecallPresetCommand}" />
        </HorizontalStackLayout>
    </VerticalStackLayout>
</VerticalStackLayout>
```

Add the endpoint-edit modal as the **last child of the root `Grid`** (paints on top; give it `Grid.RowSpan="2"` since the root Grid is `RowDefinitions="Auto,*"`), mirroring `SettingsPage.xaml`'s edit-server dialog:

```xml
<Grid Grid.RowSpan="2" IsVisible="{Binding PtzEndpointForm.IsDialogOpen}" BackgroundColor="{DynamicResource ScrimBackground}">
    <Border BackgroundColor="{DynamicResource CardBackground}" Stroke="{DynamicResource BorderColor}"
            StrokeShape="RoundRectangle 12" Padding="20" WidthRequest="320" HorizontalOptions="Center" VerticalOptions="Center">
        <VerticalStackLayout Spacing="10">
            <Label Text="PTZ endpoint" FontAttributes="Bold" FontSize="18" />
            <Entry Placeholder="Host (blank = use NDI PTZ)" Text="{Binding PtzEndpointForm.Host}" Keyboard="Url" />
            <Entry Placeholder="Port (default 5678)" Text="{Binding PtzEndpointForm.Port}" Keyboard="Numeric" />
            <Label Text="{Binding PtzEndpointForm.ValidationMessage}" TextColor="{DynamicResource ErrorRed}" />
            <Label Text="{Binding PtzEndpointForm.TestResultMessage}" TextColor="{DynamicResource TextSecondary}" />
            <HorizontalStackLayout Spacing="8" HorizontalOptions="End">
                <Button Text="Test" Command="{Binding PtzEndpointForm.TestCommand}" />
                <Button Text="Cancel" Command="{Binding PtzEndpointForm.CancelCommand}" />
                <Button Text="Save" Command="{Binding PtzEndpointForm.SaveCommand}" />
            </HorizontalStackLayout>
        </VerticalStackLayout>
    </Border>
</Grid>
```

`ViewerView.xaml.cs` is **unchanged** (rendering plumbing only — no PTZ logic there today or after).

---

## 5. Testing Strategy (`tests/MauiApp.Tests/Features/Ptz/`, new folder)

All new Core types are unit-testable with no native NDI/socket dependency.

| Test class | Key cases |
|---|---|
| `ViscaCommandEncoderTests` | `PanTiltDrive(1,0)`→`81 01 06 01 18 00 02 03 FF`; `PanTiltDrive(-1,1)`→`81 01 06 01 18 18 01 01 FF`; `PanTiltDrive(0,0)`→`81 01 06 01 00 00 03 03 FF`; `ZoomSpeed(1)`→`81 01 04 07 27 FF`; `ZoomSpeed(-1)`→`81 01 04 07 37 FF`; `ZoomSpeed(0)`→`81 01 04 07 00 FF`; `AutoFocus()`→`81 01 04 18 01 FF`; `StorePreset(5)`→`81 01 04 3F 01 05 FF`; `RecallPreset(99)`→`81 01 04 3F 02 63 FF`; preset clamps (150→99, -5→0). Skip exact-byte assertions for fractional speeds (`Math.Round` midpoint behavior is ambiguous) — assert direction nibble + byte range instead. |
| `ViscaResponseParserTests` | `[90,41,FF]`→Ack/socket 1; `[90,51,FF]`→Completion/socket 1; `[90,60,02,FF]`→Error/code `0x02`; too-short or non-`0xFF`-terminated frame→Unknown. |
| `ViscaPtzControllerTests` | Hand-rolled `FakeViscaTransport : IViscaTransport` (records sent bytes; settable next-reply/throw; tracks connect/disconnect counts). `PanTiltAsync` sends the exact encoded bytes and returns `true` on Ack. Send/receive throw → `ConnectionStatus.Error`, returns `false`. Parsed Error frame → returns `false` but status stays `Connected`. Two calls while `IsConnected` stays true → `ConnectAsync` called once (reuse). Failure then success → `ConnectAsync` called twice, `DisconnectAsync` called once after the failure. `ConnectionStatusChanged` fires once per actual transition, not per call. Simulate "timeout" by having the fake throw `TaskCanceledException` directly (no real delay) — mirrors this repo's existing untested-real-timeout pattern in `NetworkReachability.cs`; the real `CancelAfter` timeouts live only in `ViscaTcpTransport`, not unit-tested against a real socket. |
| `NdiPtzControllerTests` | `Mock<INdiViewerBridge>` — each method delegates 1:1 and returns the bridge's result; `ConnectionStatus` reflects `IsPtzSupported`. |
| `PtzControllerFactoryTests` | `Create(source-with-PtzOverrideHost)` → `Assert.IsType<ViscaPtzController>`; `Create(null)` and `Create(source-without-override)` → `Assert.IsType<NdiPtzController>`. Mock `IViscaTransportFactory` (no real socket). |
| `PtzEndpointFormViewModelTests` | `Open(host,port)` populates fields + opens dialog. `Save` with valid input raises `SaveRequested` with parsed values, closes dialog. `Save` with out-of-range port sets `ValidationMessage`, dialog stays open, no event. `Save` with blank host raises `SaveRequested(null,null)`. `Cancel` closes without raising. `Test` with a mocked `IViscaTransportFactory` whose transport succeeds/throws `OperationCanceledException`/throws otherwise → `TestResultMessage` = "Connected."/"Timed out."/"Failed: ...". |

**`tests/MauiApp.Tests/Data/NdiDatabaseSchemaTests.cs`** (extend) — add `UpsertSourceAsync_PersistsPtzOverride_RoundTrips` (save an `NdiSource` with `PtzOverrideHost`/`PtzOverridePort` against a temp SQLite file, read back via `GetSourcesAsync()`, assert equality) and `UpsertSourceAsync_WithoutPtzOverride_DefaultsToNull`.

**`tests/MauiApp.Tests/Features/Viewer/ViewerViewModelTests.cs`** (extend, existing file):

- Update `CreateSut()`: add `Mock<IPtzControllerFactory> _ptzControllerFactoryMock` and `Mock<IPtzController> _ptzControllerMock`, with `_ptzControllerFactoryMock.Setup(f => f.Create(It.IsAny<NdiSource?>())).Returns(_ptzControllerMock.Object)` in the ctor; pass `_ptzControllerFactoryMock.Object` and `new PtzEndpointFormViewModel(Mock.Of<IViscaTransportFactory>())` as the two new args.
- **Rework** `PtzNudge_BurstsThenStops` / `PtzZoomNudge_In_BurstsThenStops` (currently assert `_bridgeMock.Verify(b => b.PtzPanTiltSpeed(...))`/`PtzZoomSpeed(...)`) to instead verify `_ptzControllerMock.Verify(c => c.PanTiltAsync(expectedPan, expectedTilt, It.IsAny<CancellationToken>()))` / `c.ZoomAsync(...)` — commands now route through the controller seam, not the bridge directly. `ConnectionStateChanged_Connected_RefreshesIsPtzSupported` and `StopCommand_ClearsTallyAndPtzState` are unaffected (`IsPtzSupported` is still read straight from the bridge).
- New: `Start_WithPtzOverrideConfigured_ResolvesViscaBackedController` — mock `GetCachedSourcesAsync()` to return a source with `PtzOverrideHost` set; verify `_ptzControllerFactoryMock.Verify(f => f.Create(It.Is<NdiSource>(s => s.PtzOverrideHost == "...")))`.
- New: `Stop_DisposesActivePtzController` — verify `_ptzControllerMock.Verify(c => c.DisposeAsync())`.
- New: `OpenPtzEndpointEditorCommand_PopulatesFormFromActiveSource`.
- New: `PtzEndpointForm_SaveRequested_PersistsOverrideAndRebuildsController` — set `Host`/`Port` on `sut.PtzEndpointForm` then execute `SaveCommand` (same instance held by the SUT); verify `_sourceRepoMock.Verify(r => r.SaveSourceAsync(It.Is<NdiSource>(s => s.PtzOverrideHost == "...")))` and `_ptzControllerFactoryMock.Verify(f => f.Create(...), Times.AtLeastOnce)`.

No NDI hardware or real sockets anywhere — everything routes through `Mock<T>`/`FakeViscaTransport`.

**Manual/E2E verification aid (already present, untracked, in this worktree)**:
`tools/ViscaMockCamera/` is a standalone .NET console tool (not part of `NdiForAndroid.sln`, no
external NuGet deps — see its `README.md`) that emulates a PTZOptics/Avonic-style raw-VISCA-over-TCP
camera on port 5678, decodes/logs every frame, and replies ACK+Completion, matching this plan's byte
layouts exactly (pan/tilt drive `81 01 06 01 VV WW pp tt FF`, zoom `81 01 04 07 00|2p|3p FF`,
focus one-push `81 01 04 18 01 FF`, preset set/recall `81 01 04 3F 01|02 pp FF`) — a useful
cross-check that the encoder in §2.4 is byte-compatible with a real PTZOptics-style device. It also
documents an `adb reverse tcp:5678 tcp:5678` recipe for testing the on-device app against it over
USB with no firewall changes. This tool is untracked (not yet committed) — flag it to the user for
inclusion in this feature's PR (it belongs in the repo permanently as a dev aid, e.g. still under
`tools/ViscaMockCamera/`, excluded from `NdiForAndroid.sln`) rather than treating it as scratch work
to discard. It does not replace real Avonic CM93 verification (§8/spec.md) but de-risks it.

---

## 6. Documentation Updates (after implementation)

- `docs/ndi-sdk-coverage.md` — PTZ row's Notes gain one sentence on the VISCA-over-TCP override path for non-PTZ-capable sources (status stays ✅ Implemented — NDI PTZ itself is unchanged).
- `docs/architecture.md` — add `IPtzController` to the module map next to `INdiViewerBridge`, noting the two backends and per-source selection.

---

## 7. Risks & Edge Cases

| Risk / Edge case | Mitigation |
|---|---|
| VISCA reply split across multiple TCP reads | `ViscaTcpTransport.ReceiveFrameAsync` reads byte-by-byte until `0xFF` — partial reads accumulate naturally. |
| Endpoint edited while a command is in flight | `Create` builds a new `ViscaPtzController`+transport on save; `AttachPtzController` disposes the old one — an in-flight command on the old instance completes/fails independently, doesn't corrupt the new connection. |
| User clears the host field | `TryParseInput` returns `(null,null)`; `PtzControllerFactory.Create` falls back to `NdiPtzController` when `PtzOverrideHost` is null/blank. |
| `Dispose()`/`Stop()` racing a VISCA reconnect | Event unsubscribed before `DisposeAsync()`; disposed instance is discarded, no further callbacks. |
| Avonic CM93 quirks vs. documented VISCA subset (D3) | Flagged in spec.md as a required follow-up, not blocking; encoder/parser are unit-tested against the documented spec bytes only. |
| `PtzAutoFocus` type change (`void`→`async Task`) | `[RelayCommand]` on async yields `IAsyncRelayCommand`, still `ICommand`-compatible — no other XAML changes needed. |
| Shared-file merge conflicts with the parallel Viewer feature | All new logic lives in `src/Core/Features/Ptz/*`; the `ViewerViewModel`/`ViewerView.xaml` diff is confined to §4.3/§4.4's additive blocks. |

---

## 8. Open Questions

None blocking. Verifying exact VISCA command/response behavior against real Avonic CM93 hardware is explicitly out of scope here (spec.md) and tracked as a required follow-up before shipping to that hardware.
