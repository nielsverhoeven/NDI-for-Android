// NdiTestSender — a PC-side NDI test source for the tablet gate (#415/#416/#423/#403).
//
// Sends a 1080p60 BGRA full-bandwidth (SpeedHQ) stream through the installed NDI 6 Runtime
// (Processing.NDI.Lib.x64.dll). Every frame carries:
//   * a scrolling colour field + a per-frame noise band (so SpeedHQ bitrate stays realistic, like
//     a moving screen capture — not the ~1 fps static Test Patterns source);
//   * the PC wall clock as big digits  HH:MM:SS.mmm  (UTC epoch ms also as a 13-digit line) so a
//     tablet screenshot / screenrecord frame can be compared against the PC clock;
//   * a frame counter;
//   * a white flash bar on every .000 second, with an audio click in the same frame (A/V sync).
// Audio: 48 kHz stereo, 800 samples per frame, −20 dBFS 440 Hz tone + click on the second.
//
// Usage: NdiTestSender [--name NAME] [--width W] [--height H] [--fps N] [--seconds S] [--no-audio]
//                      [--noise ROWS] [--lib-dir DIR]
//
// The NDI runtime directory is taken from --lib-dir, else the NDI_RUNTIME_DIR environment
// variable, else the NDI 6 Runtime's default install location. The runtime is not part of this
// repository: install it from https://ndi.video/tools/.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const string Lib = "Processing.NDI.Lib.x64.dll";
    private const string LibDirEnvVar = "NDI_RUNTIME_DIR";
    private const string DefaultLibDir = @"C:\Program Files\NDI\NDI 6 Runtime\v6\";

    [StructLayout(LayoutKind.Sequential)]
    private struct SendCreate
    {
        public IntPtr p_ndi_name;
        public IntPtr p_groups;
        [MarshalAs(UnmanagedType.I1)] public bool clock_video;
        [MarshalAs(UnmanagedType.I1)] public bool clock_audio;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VideoFrameV2
    {
        public int xres, yres;
        public uint FourCC;
        public int frame_rate_N, frame_rate_D;
        public float picture_aspect_ratio;
        public int frame_format_type;
        public long timecode;
        public IntPtr p_data;
        public int line_stride_in_bytes;
        public IntPtr p_metadata;
        public long timestamp;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioFrameInterleaved32f
    {
        public int sample_rate, no_channels, no_samples;
        public long timecode;
        public IntPtr p_data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Tally
    {
        [MarshalAs(UnmanagedType.I1)] public bool on_program;
        [MarshalAs(UnmanagedType.I1)] public bool on_preview;
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool NDIlib_initialize();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void NDIlib_destroy();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr NDIlib_version();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr NDIlib_send_create(ref SendCreate create);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void NDIlib_send_destroy(IntPtr inst);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void NDIlib_send_send_video_v2(IntPtr inst, ref VideoFrameV2 frame);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void NDIlib_util_send_send_audio_interleaved_32f(IntPtr inst, ref AudioFrameInterleaved32f frame);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int NDIlib_send_get_no_connections(IntPtr inst, uint timeoutMs);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool NDIlib_send_get_tally(IntPtr inst, ref Tally tally, uint timeoutMs);

    private const long TimecodeSynthesize = long.MaxValue;
    private const uint FourCC_BGRA = 0x41524742;

    // 5x7 bitmap font: digits, ':', '.', '-', 'F', 'P', 'C', ' ' (row-major, MSB = leftmost column).
    private static readonly Dictionary<char, byte[]> Font = new()
    {
        ['0'] = [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
        ['1'] = [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
        ['2'] = [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
        ['3'] = [0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E],
        ['4'] = [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
        ['5'] = [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
        ['6'] = [0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E],
        ['7'] = [0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08],
        ['8'] = [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
        ['9'] = [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C],
        [':'] = [0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00],
        ['.'] = [0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C],
        ['-'] = [0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00],
        ['F'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10],
        ['P'] = [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
        ['C'] = [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
        ['U'] = [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
        ['T'] = [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
        ['E'] = [0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F],
        ['M'] = [0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11],
        ['S'] = [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E],
        [' '] = [0, 0, 0, 0, 0, 0, 0],
    };

    private static unsafe void DrawText(uint* px, int stride, int width, int height, string text, int x0, int y0, int scale, uint color, uint bg)
    {
        int cx = x0;
        foreach (var ch in text)
        {
            var glyph = Font.TryGetValue(ch, out var g) ? g : Font[' '];
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    bool on = (glyph[row] & (0x10 >> col)) != 0;
                    uint c = on ? color : bg;
                    int px0 = cx + col * scale, py0 = y0 + row * scale;
                    for (int dy = 0; dy < scale; dy++)
                    {
                        int y = py0 + dy; if (y < 0 || y >= height) continue;
                        uint* rowp = px + (long)y * stride;
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int x = px0 + dx; if (x < 0 || x >= width) continue;
                            rowp[x] = c;
                        }
                    }
                }
            }
            cx += 6 * scale;
        }
    }

    private static int Main(string[] args)
    {
        string name = "PC-Test-1080p60";
        int width = 1920, height = 1080, fps = 60;
        double seconds = double.PositiveInfinity;
        bool audio = true;
        int noiseRows = 200; // height of the incompressible noise band (0 = none; lower = lower SpeedHQ bitrate)
        string? libDir = Environment.GetEnvironmentVariable(LibDirEnvVar);
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--noise": noiseRows = int.Parse(args[++i]); break;
                case "--name": name = args[++i]; break;
                case "--width": width = int.Parse(args[++i]); break;
                case "--height": height = int.Parse(args[++i]); break;
                case "--fps": fps = int.Parse(args[++i]); break;
                case "--seconds": seconds = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--no-audio": audio = false; break;
                case "--lib-dir": libDir = args[++i]; break;
            }
        }

        // ── NDI runtime location: --lib-dir > NDI_RUNTIME_DIR > the NDI 6 Runtime default ──────
        libDir = Path.GetFullPath(string.IsNullOrWhiteSpace(libDir) ? DefaultLibDir : libDir);
        string libPath = Path.Combine(libDir, Lib);
        Console.WriteLine($"NDI runtime directory: {libDir}");
        if (!File.Exists(libPath))
        {
            Console.Error.WriteLine($"ERROR: {Lib} not found in {libDir}");
            Console.Error.WriteLine($"Install the NDI 6 Runtime (https://ndi.video/tools/), or point --lib-dir <path> or the {LibDirEnvVar} environment variable at the directory that contains {Lib}.");
            return 1;
        }

        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (lib, asm, path) =>
            lib == Lib ? NativeLibrary.Load(libPath) : IntPtr.Zero);

        if (!NDIlib_initialize()) { Console.Error.WriteLine("NDIlib_initialize failed"); return 2; }
        Console.WriteLine($"NDI runtime: {Marshal.PtrToStringAnsi(NDIlib_version())}");

        var namePtr = Marshal.StringToHGlobalAnsi(name);
        var create = new SendCreate { p_ndi_name = namePtr, p_groups = IntPtr.Zero, clock_video = true, clock_audio = false };
        var send = NDIlib_send_create(ref create);
        if (send == IntPtr.Zero) { Console.Error.WriteLine("NDIlib_send_create failed"); return 3; }
        Console.WriteLine($"Sending \"{Environment.MachineName} ({name})\" {width}x{height}@{fps} BGRA, audio={audio}. Ctrl+C to stop.");

        int stride = width * 4;
        var buffer = Marshal.AllocHGlobal(stride * height);
        var frame = new VideoFrameV2
        {
            xres = width, yres = height, FourCC = FourCC_BGRA,
            frame_rate_N = fps, frame_rate_D = 1,
            picture_aspect_ratio = (float)width / height,
            frame_format_type = 1, // progressive
            timecode = TimecodeSynthesize,
            p_data = buffer, line_stride_in_bytes = stride,
            p_metadata = IntPtr.Zero, timestamp = 0,
        };

        const int sampleRate = 48000, channels = 2;
        int samplesPerFrame = sampleRate / fps;
        var audioBuf = Marshal.AllocHGlobal(samplesPerFrame * channels * sizeof(float));
        var aframe = new AudioFrameInterleaved32f
        {
            sample_rate = sampleRate, no_channels = channels, no_samples = samplesPerFrame,
            timecode = TimecodeSynthesize, p_data = audioBuf,
        };

        var running = true;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; running = false; };

        var sw = Stopwatch.StartNew();
        long frameNo = 0;
        uint rng = 0x9E3779B9;
        double phase = 0;
        int lastSecond = -1;
        var lastReport = Stopwatch.StartNew();
        int digitScale = Math.Max(4, width / 140);          // 1920 → 13 px per font pixel → 65x91 px digits
        int bigScale = Math.Max(6, width / 100);            // clock: 19 → 95x133 px digits

        // Row templates: 8 hue bands x 2 checker phases, unshifted; rotated per frame below.
        var templates = new uint[16][];
        for (int band = 0; band < 8; band++)
            for (int ph = 0; ph < 2; ph++)
            {
                var t = new uint[width];
                for (int x = 0; x < width; x++)
                {
                    uint r = (uint)(x * 255 / width);
                    uint g = 255 - r;
                    uint b = (uint)((band * 37 + x / 4) & 0xFF);
                    uint v = (((x / 60) + ph) & 1) == 0 ? 0u : 0x30u; // soft checker
                    t[x] = 0xFF000000u | (((r + v) & 0xFF) << 16) | (((g + v) & 0xFF) << 8) | ((b + v) & 0xFF);
                }
                templates[band * 2 + ph] = t;
            }

        unsafe
        {
            uint* px = (uint*)buffer;
            float* ab = (float*)audioBuf;
            while (running && sw.Elapsed.TotalSeconds < seconds)
            {
                var now = DateTime.UtcNow;
                var local = now.ToLocalTime();
                long epochMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                int ms = local.Millisecond;
                bool flash = ms < 1000 / fps; // first frame of every second

                // ── background: scrolling hue bands + per-frame noise band ─────────────────
                // Precomputed row templates (one per band/checker phase) are copied with a rotation
                // (two memcpy per row), and rows are rendered in parallel so the generator keeps
                // 60 fps on a laptop — a slow generator would masquerade as receiver-side drops.
                int shift = (int)(frameNo * 6) % width;
                uint frameSeed = rng; rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                int w = width, h = height, strideU = stride / 4;
                uint* pxBase = px;
                Parallel.For(0, h, y =>
                {
                    uint* row = pxBase + (long)y * strideU;
                    if (y >= h - 60 - noiseRows && y < h - 60)
                    {
                        // noise band — new random texture every frame (keeps SpeedHQ bitrate high)
                        uint r = frameSeed ^ (uint)(y * 2654435761u);
                        for (int x = 0; x < w; x++)
                        {
                            r ^= r << 13; r ^= r >> 17; r ^= r << 5;
                            row[x] = 0xFF000000u | (r & 0x00FFFFFFu);
                        }
                    }
                    else
                    {
                        int band = y * 8 / h;
                        int checkerPhase = (y / 60) & 1;
                        var tpl = templates[band * 2 + checkerPhase];
                        fixed (uint* t = tpl)
                        {
                            // row[x] = tpl[(x + shift) % w]
                            int first = w - shift;
                            Buffer.MemoryCopy(t + shift, row, (long)first * 4, (long)first * 4);
                            Buffer.MemoryCopy(t, row + first, (long)shift * 4, (long)shift * 4);
                        }
                    }
                });

                // ── flash bar at the top on every .000 second ─────────────────────────────
                if (flash)
                {
                    for (int y = 0; y < 60; y++)
                    {
                        uint* row = px + (long)y * stride / 4;
                        for (int x = 0; x < width; x++) row[x] = 0xFFFFFFFFu;
                    }
                }

                // ── text: local clock (big), UTC epoch ms, frame counter ───────────────────
                string clock = local.ToString("HH:mm:ss.fff");
                int clockW = clock.Length * 6 * bigScale;
                DrawText(px, stride / 4, width, height, clock, (width - clockW) / 2, 140, bigScale, 0xFFFFFFFFu, 0xFF000000u);
                string epochLine = $"UTC {epochMs}";
                DrawText(px, stride / 4, width, height, epochLine, (width - epochLine.Length * 6 * digitScale) / 2, 140 + 7 * bigScale + 40, digitScale, 0xFFFFFF00u, 0xFF000000u);
                string frameLine = $"F {frameNo:D8}  {width}P{fps}";
                DrawText(px, stride / 4, width, height, frameLine, (width - frameLine.Length * 6 * digitScale) / 2, 140 + 7 * bigScale + 40 + 7 * digitScale + 30, digitScale, 0xFF00FFFFu, 0xFF000000u);

                // moving marker: a block that sweeps left→right once per second (motion cue)
                int mx = (int)((long)ms * (width - 120) / 1000);
                for (int y = height - 50; y < height - 10; y++)
                {
                    uint* row = px + (long)y * stride / 4;
                    for (int x = mx; x < mx + 120 && x < width; x++) row[x] = flash ? 0xFFFF0000u : 0xFF00FF00u;
                }

                // ── audio: 440 Hz at −20 dBFS, click on the flash frame ────────────────────
                if (audio)
                {
                    double step = 2 * Math.PI * 440 / sampleRate;
                    for (int s = 0; s < samplesPerFrame; s++)
                    {
                        float v = (float)(0.1 * Math.Sin(phase));
                        phase += step; if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
                        if (flash && s < 96) v = (s & 8) == 0 ? 0.8f : -0.8f; // 2 ms click burst
                        ab[s * 2] = v; ab[s * 2 + 1] = v;
                    }
                    NDIlib_util_send_send_audio_interleaved_32f(send, ref aframe);
                }

                // ── send (clock_video=true: this call paces to fps) ───────────────────────
                // A file named pause.flag next to the exe suspends video (connection stays up,
                // audio keeps flowing) — simulates "sender connected but sends no video" (#412 tier 3).
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, "pause.flag")))
                {
                    Thread.Sleep(1000 / fps);
                    if (lastReport.Elapsed.TotalSeconds >= 1) { lastReport.Restart(); Console.WriteLine($"[{local:HH:mm:ss.fff}] PAUSED (pause.flag present) connections={NDIlib_send_get_no_connections(send, 0)}"); }
                    continue;
                }
                NDIlib_send_send_video_v2(send, ref frame);
                frameNo++;

                if (lastReport.Elapsed.TotalSeconds >= 1)
                {
                    lastReport.Restart();
                    int conns = NDIlib_send_get_no_connections(send, 0);
                    var tally = new Tally();
                    NDIlib_send_get_tally(send, ref tally, 0);
                    double actualFps = frameNo / sw.Elapsed.TotalSeconds;
                    Console.WriteLine($"[{local:HH:mm:ss.fff}] frames={frameNo} avgFps={actualFps:F1} connections={conns} tally(pgm={tally.on_program},pvw={tally.on_preview})");
                }
                _ = lastSecond;
            }
        }

        NDIlib_send_destroy(send);
        Marshal.FreeHGlobal(buffer);
        Marshal.FreeHGlobal(audioBuf);
        Marshal.FreeHGlobal(namePtr);
        NDIlib_destroy();
        Console.WriteLine($"Stopped after {frameNo} frames.");
        return 0;
    }
}
