# NdiTestSender

A standalone .NET console tool that puts a 1080p60 full-bandwidth (SpeedHQ) NDI source on the
network from a Windows PC. It exists so the NDI-for-Android app's receiver can be put through
the device gate (#415, #416) with a known, measurable picture: the frame carries the PC's clock,
a frame counter, an A/V sync flash + click and a bitrate knob, so a tablet screenshot or screen
recording can be read against what the sender actually put out. It replaces the NDI Tools
"Screen Capture" and "Test Patterns" sources on a PC where NDI Tools is not installed; unlike
the ~1 fps static Test Patterns it moves every frame, so the bitrate is that of a real screen
capture.

This is a developer tool, not part of the shipped app. It is **not** included in
`NdiForAndroid.sln`, is not built by CI and has no NuGet dependencies.

## Requirements

- **Windows x64 only.** The tool P/Invokes `Processing.NDI.Lib.x64.dll` and is built with
  `PlatformTarget=x64`.
- **The NDI 6 Runtime, installed by you.** No NDI DLL is committed to this repository: the
  runtime is licensed separately by Vizrt/NDI and is installed from <https://ndi.video/tools/>
  (the runtime is also installed by NDI Tools). The default location the tool looks in is
  `C:\Program Files\NDI\NDI 6 Runtime\v6\`; see `--lib-dir` below if yours differs.
- .NET 10 SDK.

At startup the tool prints the runtime directory it resolved and the runtime version it
loaded. If `Processing.NDI.Lib.x64.dll` is not in that directory it prints an error naming
the directory and exits with code 1.

## Running it

From the repository root:

```powershell
dotnet run -c Release --project tools/NdiTestSender -- --name PC-Test-1080p60
```

or from `tools/NdiTestSender`:

```powershell
dotnet run -c Release -- --name PC-Test-1080p60
```

Use a Release build: the frame generator is written to hold 60 fps on a laptop, and a Debug
build that cannot keep up would show on the tablet as receiver-side frame drops that are not.
The source appears on the network as `<PC-NAME> (PC-Test-1080p60)`. Stop it with Ctrl+C.

Options:

| Option | Description |
|---|---|
| `--name <text>` | NDI source name. Defaults to `PC-Test-1080p60`. |
| `--width <px>` / `--height <px>` | Frame size. Defaults to `1920` x `1080`. |
| `--fps <n>` | Frame rate; the send call paces to it (`clock_video`). Defaults to `60`. |
| `--seconds <s>` | Stop after this many seconds. Runs until Ctrl+C if omitted. |
| `--no-audio` | Send video only. Audio (48 kHz stereo) is on by default. |
| `--noise <rows>` | Height in pixels of the incompressible noise band, `0` for none. Defaults to `200`. See "Bitrate" below. |
| `--lib-dir <path>` | Directory that contains `Processing.NDI.Lib.x64.dll`. Overrides the `NDI_RUNTIME_DIR` environment variable, which overrides the default `C:\Program Files\NDI\NDI 6 Runtime\v6\`. |

Once a second the console prints the frame count, the average fps achieved, the number of
connected receivers and the tally state the receivers report.

### Pausing the video (`pause.flag`)

Create a file named `pause.flag` next to the executable (`bin/Release/net10.0/` when run
with `dotnet run`) and the tool stops sending video frames while keeping the NDI connection
open and the audio flowing; delete the file and video resumes. The console shows `PAUSED`
once a second while the file exists. This simulates a source that is connected but sends no
video, which is what the receiver's stall handling (#412) has to cope with.

```powershell
New-Item tools/NdiTestSender/bin/Release/net10.0/pause.flag   # pause
Remove-Item tools/NdiTestSender/bin/Release/net10.0/pause.flag # resume
```

## What the picture shows

- **Background**: a colour field of eight hue bands with a soft checker that scrolls six
  pixels per frame, so every frame differs from the previous one.
- **Noise band**: `--noise N` rows of per-frame random pixels just above the bottom edge.
  Random pixels do not compress, so this is the bitrate knob: at 1080p60, about 194 Mbit/s
  with the default `N=200` and about 156 Mbit/s with `N=0`.
- **Local clock** (large white digits, top centre): the PC's local time as `HH:mm:ss.fff`,
  stamped when the frame was generated.
- **UTC epoch** (yellow): the same instant as Unix milliseconds, `UTC 1760000000000`, for
  comparing against `adb shell date +%s%3N` or the app's logcat timestamps.
- **Frame counter** (cyan): `F 00001234  1920P60`, the sequence number of the frame and the
  format being sent. Any gap in consecutive screenshots is a dropped or skipped frame.
- **Flash bar**: on the first frame of every second (`.000`) the top 60 rows go white, and the
  audio for that same frame carries a 2 ms click on top of the continuous 440 Hz tone at
  -20 dBFS. Seeing the flash and hearing the click together is the A/V sync check.
- **Sweep marker**: a green block along the bottom that travels from the left edge to the
  right edge once per second (red on the flash frame). It makes motion judder visible and
  gives a sub-second position reading when the digits are blurred in a recording.

## Reading latency with it

- **In the app**: the sender's NDI runtime stamps every frame with its own UTC send time, so
  the app's 1 Hz `NDI-Lat` logcat line (`adb logcat -s NDI-Lat`, developer mode) reports a
  real `senderToDrawMs` instead of `-1`. That value is receiver clock minus sender clock, so it
  includes the tablet-to-PC clock offset: measure the offset (for example `adb shell date
  +%s%3N` against the PC clock, or NTP-sync both) and subtract it. `recvToDrawMs` on the same
  line is single-clock and needs no correction.
- **Glass to glass**: record the tablet with `adb shell screenrecord --bugreport ...`, which
  overlays the tablet's own clock on every recorded frame, and compare that overlay with the
  sender clock drawn inside the picture in the same recorded frame. The difference, minus the
  clock offset, is the end-to-end latency including the Android display pipeline; the frame
  counter tells you whether frames went missing on the way, and the flash/click pair tells you
  whether audio arrived with its video.

## Windows Firewall

NDI receivers connect to the sender, not the other way round: the tablet opens inbound TCP
connections to this PC on 5960 (discovery/messaging) and 5961 and up (the stream). The first
run normally shows the Windows Defender Firewall prompt; allow the executable on the
**Private** profile. If no prompt appeared and the tablet lists the source but never connects,
add an inbound rule for `NdiTestSender.exe` (or for `dotnet.exe` when run with `dotnet run`)
on the Private profile.
