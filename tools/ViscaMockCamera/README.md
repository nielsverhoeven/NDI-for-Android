# ViscaMockCamera

A standalone .NET console tool that emulates a PTZ camera speaking raw VISCA over IP the
PTZOptics/Avonic way: plain VISCA packets terminated by `0xFF`, no Sony VISCA-over-IP header.
It exists to let the NDI-for-Android app's PTZ endpoint be exercised end-to-end without a
physical camera on the network.

This is a developer tool, not part of the shipped app. It is **not** included in
`NdiForAndroid.sln` and has no external NuGet dependencies.

## What it does

- Listens on one or more TCP ports (default `5678`), and optionally UDP on the same ports.
- Reassembles incoming bytes into VISCA frames by splitting on `0xFF` (frames may be
  concatenated or split across reads/datagrams).
- Logs a timestamped hex dump and a human-readable decode of every frame, for:
  - Pan/tilt drive (`81 01 06 01 VV WW pp tt FF`)
  - Pan/tilt home (`81 01 06 04 FF`)
  - Zoom stop/tele/wide (`81 01 04 07 00|2p|3p FF`)
  - Focus one-push (`81 01 04 18 01 FF`)
  - Focus mode auto/manual (`81 01 04 38 02|03 FF`)
  - Preset reset/set/recall (`81 01 04 3F 00|01|02 pp FF`, 16 slots)
  - Power inquiry (`81 09 04 00 FF`)
  - Pan/tilt position inquiry (`81 09 06 12 FF`)
  - Zoom position inquiry (`81 09 04 47 FF`)
  - Anything else is logged as "unknown".
- Replies like a real camera on the same connection: ACK (`90 41 FF`) then Completion
  (`90 51 FF`) for commands, `90 50 <payload> FF` for inquiries, and a syntax error
  (`90 60 02 FF`) for malformed frames.
- Tracks an in-memory pan/tilt/zoom position that moves while a drive command is active
  (until stop), so inquiries return plausible values, and stores/recalls up to 16 presets.
- Handles multiple concurrent clients and client disconnects, and stops cleanly on Ctrl+C.

## Running it

```powershell
dotnet run --project tools/ViscaMockCamera -- --port 5678 --verbose
```

Options:

| Option | Description |
|---|---|
| `--port <n>` | Port to listen on. Repeatable to listen on more than one port. Defaults to `5678` if omitted. |
| `--udp` | Also listen on UDP for every configured port (TCP is always enabled). |
| `--verbose` | Log connection lifecycle events and every reply frame sent, in addition to the always-on received-frame decode log. |
| `--log <file>` | Append the same log lines to this file as well as the console. |

At startup the tool prints its local IPv4 addresses and the ports it is listening on.

## Testing from the tablet over USB (no firewall changes needed)

With the device connected via USB and `adb` on your PATH:

```powershell
adb reverse tcp:5678 tcp:5678
```

Then configure the app's PTZ endpoint as host `127.0.0.1`, port `5678`. Traffic the app
sends to `127.0.0.1:5678` on the device is forwarded over the USB connection to this tool
running on your workstation.

## Quick manual test from PowerShell

This sends a "pan right" drive command over TCP and reads back the ACK + Completion reply:

```powershell
$c=[Net.Sockets.TcpClient]::new('127.0.0.1',5678); $s=$c.GetStream(); $s.Write([byte[]](0x81,0x01,0x06,0x01,0x08,0x08,0x02,0x03,0xFF),0,9); Start-Sleep -Milliseconds 150; $buf=New-Object byte[] 64; $read=$s.Read($buf,0,$buf.Length); [BitConverter]::ToString($buf,0,$read); $c.Close()
```

Expected output: `90-41-FF-90-51-FF` (ACK, then Completion), and the tool's console/log shows
the matching decode line, e.g.:

```
[hh:mm:ss.fff] TCP:5678 127.0.0.1:xxxxx recv 8101060108080203FF -> pan/tilt drive: pan=right speed=8 tilt=none speed=8
```
