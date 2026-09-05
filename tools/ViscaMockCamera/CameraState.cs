namespace ViscaMockCamera;

internal sealed class CameraState : IDisposable
{
    private const int PanMin = -2267;
    private const int PanMax = 2267;
    private const int TiltMin = -792;
    private const int TiltMax = 302;
    private const int ZoomMin = 0;
    private const int ZoomMax = 0x4000;
    private const int PresetCount = 16;

    private readonly object _lock = new();
    private readonly Timer _timer;
    private readonly (int Pan, int Tilt, int Zoom)?[] _presets = new (int, int, int)?[PresetCount];

    private int _pan;
    private int _tilt;
    private int _zoom;
    private int _panDirection;
    private int _tiltDirection;
    private int _panSpeed;
    private int _tiltSpeed;
    private int _zoomDirection;
    private int _zoomSpeed;

    public CameraState()
    {
        _timer = new Timer(Tick, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    public void SetPanTiltDrive(int panSpeed, int tiltSpeed, int panDirection, int tiltDirection)
    {
        lock (_lock)
        {
            _panSpeed = panSpeed;
            _tiltSpeed = tiltSpeed;
            _panDirection = panDirection;
            _tiltDirection = tiltDirection;
        }
    }

    public void StopPanTilt()
    {
        lock (_lock)
        {
            _panDirection = 0;
            _tiltDirection = 0;
        }
    }

    public void GoHome()
    {
        lock (_lock)
        {
            _pan = 0;
            _tilt = 0;
            _panDirection = 0;
            _tiltDirection = 0;
        }
    }

    public void SetZoomDrive(int direction, int speed)
    {
        lock (_lock)
        {
            _zoomDirection = direction;
            _zoomSpeed = speed;
        }
    }

    public void StopZoom()
    {
        lock (_lock)
        {
            _zoomDirection = 0;
        }
    }

    public (int Pan, int Tilt) GetPanTiltPosition()
    {
        lock (_lock)
        {
            return (_pan, _tilt);
        }
    }

    public int GetZoomPosition()
    {
        lock (_lock)
        {
            return _zoom;
        }
    }

    public void ResetPreset(int index)
    {
        lock (_lock)
        {
            _presets[index] = null;
        }
    }

    public void SetPreset(int index)
    {
        lock (_lock)
        {
            _presets[index] = (_pan, _tilt, _zoom);
        }
    }

    public bool RecallPreset(int index)
    {
        lock (_lock)
        {
            var preset = _presets[index];
            if (preset is null)
            {
                return false;
            }

            (_pan, _tilt, _zoom) = preset.Value;
            _panDirection = 0;
            _tiltDirection = 0;
            _zoomDirection = 0;
            return true;
        }
    }

    private void Tick(object? state)
    {
        lock (_lock)
        {
            if (_panDirection != 0)
            {
                _pan = Math.Clamp(_pan + (_panDirection * _panSpeed), PanMin, PanMax);
            }

            if (_tiltDirection != 0)
            {
                _tilt = Math.Clamp(_tilt + (_tiltDirection * _tiltSpeed), TiltMin, TiltMax);
            }

            if (_zoomDirection != 0)
            {
                _zoom = Math.Clamp(_zoom + (_zoomDirection * _zoomSpeed * 8), ZoomMin, ZoomMax);
            }
        }
    }

    public void Dispose() => _timer.Dispose();
}
