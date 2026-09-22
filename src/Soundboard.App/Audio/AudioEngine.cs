using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Soundboard.Core;

namespace Soundboard.Audio;

internal sealed class AudioEngine : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Playback> _active = new(StringComparer.Ordinal);
    private readonly SlotRampTracker _ramps = new();
    private float _globalVolume = 0.8f;
    private bool _muted;
    private bool _disposed;

    public event Action? Changed;

    public float GlobalVolume
    {
        get { lock (_gate) return _globalVolume; }
        set
        {
            lock (_gate)
            {
                _globalVolume = AppConfig.Clamp01(value);
                ApplyVolumes();
            }
        }
    }

    public bool Muted
    {
        get { lock (_gate) return _muted; }
        set
        {
            lock (_gate)
            {
                _muted = value;
                ApplyVolumes();
            }

            Changed?.Invoke();
        }
    }

    public bool IsPlaying(string slotId)
    {
        lock (_gate)
        {
            return _active.TryGetValue(slotId, out var p)
                   && p.Output.PlaybackState == PlaybackState.Playing;
        }
    }

    public float? PlayingSpeed(string slotId)
    {
        lock (_gate)
        {
            if (_active.TryGetValue(slotId, out var p)
                && p.Output.PlaybackState == PlaybackState.Playing)
            {
                return p.Speed;
            }
        }

        return null;
    }

    public IReadOnlyCollection<string> PlayingIds
    {
        get
        {
            lock (_gate)
            {
                return _active.Where(kv => kv.Value.Output.PlaybackState == PlaybackState.Playing)
                    .Select(kv => kv.Key)
                    .ToArray();
            }
        }
    }

    /// <summary>
    /// Press = play. Press again while playing:
    /// Restart → stop the current stream without resetting the ramp, then play
    /// from the start at the next ramp speed.
    /// ToggleStop → stop and (if ResetOnStop) reset the ramp.
    /// </summary>
    public void ToggleOrRestart(SoundSlot slot, string resolvedPath, HotkeyBehavior behavior)
    {
        lock (_gate)
        {
            var playing = _active.TryGetValue(slot.Id, out var existing)
                          && existing.Output.PlaybackState == PlaybackState.Playing;
            if (playing)
            {
                if (behavior == HotkeyBehavior.ToggleStop)
                {
                    StopLocked(slot.Id, PlaybackStopKind.ExplicitStop);
                    return;
                }

                StopLocked(slot.Id, PlaybackStopKind.Restart);
            }
        }

        var speed = ConsumeRampSpeed(slot);
        Play(slot, resolvedPath, speed);
    }

    public void Play(SoundSlot slot, string resolvedPath) =>
        Play(slot, resolvedPath, ConsumeRampSpeed(slot));

    public void Play(SoundSlot slot, string resolvedPath, float speed)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("找不到音频文件。", resolvedPath);
        }

        speed = Math.Clamp(speed, SpeedRamp.MinSpeed, SpeedRamp.AbsoluteMax);

        WaveStream reader = OpenReader(resolvedPath);
        VolumeSampleProvider? volume = null;
        WaveOutEvent? output = null;
        try
        {
            ISampleProvider samples = reader is ISampleProvider sp ? sp : reader.ToSampleProvider();
            samples = PlaybackSpeed.Apply(samples, speed);
            volume = new VolumeSampleProvider(samples);
            output = new WaveOutEvent { DesiredLatency = 150 };
            output.Init(volume);
            var playback = new Playback(
                slot.Id, slot.Volume, speed, slot.EnableSpeedRamp && slot.ResetOnStop,
                reader, volume, output);

            lock (_gate)
            {
                StopLocked(slot.Id, PlaybackStopKind.Restart);
                playback.Volume.Volume = EffectiveVolume(slot.Volume);
                _active[slot.Id] = playback;
            }

            output.PlaybackStopped += (_, args) => OnStopped(playback, args.Exception);
            output.Play();
        }
        catch
        {
            lock (_gate)
            {
                if (output is not null
                    && _active.TryGetValue(slot.Id, out var existing)
                    && ReferenceEquals(existing.Output, output))
                {
                    _active.Remove(slot.Id);
                }
            }

            output?.Dispose();
            reader.Dispose();
            throw;
        }

        Changed?.Invoke();
    }

    public void Stop(string slotId)
    {
        lock (_gate)
        {
            StopLocked(slotId, PlaybackStopKind.ExplicitStop);
        }

        Changed?.Invoke();
    }

    public void StopAll()
    {
        lock (_gate)
        {
            foreach (var id in _active.Keys.ToArray())
            {
                StopLocked(id, PlaybackStopKind.ExplicitStop);
            }
        }

        Changed?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAll();
        _disposed = true;
    }

    private void OnStopped(Playback playback, Exception? error)
    {
        lock (_gate)
        {
            if (_active.TryGetValue(playback.SlotId, out var current)
                && ReferenceEquals(current, playback))
            {
                _active.Remove(playback.SlotId);
            }

            if (SlotRampTracker.ShouldReset(playback.StopKind, playback.ResetRampOnStop))
            {
                _ramps.Reset(playback.SlotId);
            }
        }

        playback.Dispose();
        Changed?.Invoke();
        if (error is not null)
        {
            PlaybackFailed?.Invoke(playback.SlotId, error);
        }
    }

    public event Action<string, Exception>? PlaybackFailed;

    public void ResetRamp(string slotId) => _ramps.Reset(slotId);

    public void ResetAllRamps() => _ramps.ResetAll();

    public float ConsumeRampSpeed(SoundSlot slot) => _ramps.Consume(slot);

    private void StopLocked(string slotId, PlaybackStopKind kind)
    {
        if (!_active.Remove(slotId, out var playback))
        {
            return;
        }

        playback.StopKind = kind;
        playback.Dispose();
    }

    private void ApplyVolumes()
    {
        foreach (var playback in _active.Values)
        {
            playback.Volume.Volume = EffectiveVolume(playback.SlotVolume);
        }
    }

    private float EffectiveVolume(float slotVolume)
    {
        if (_muted)
        {
            return 0f;
        }

        return AppConfig.Clamp01(slotVolume) * AppConfig.Clamp01(_globalVolume);
    }

    private static WaveStream OpenReader(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".ogg"
            ? new VorbisWaveReader(path)
            : new AudioFileReader(path);
    }

    private sealed class Playback : IDisposable
    {
        public Playback(
            string slotId,
            float slotVolume,
            float speed,
            bool resetRampOnStop,
            WaveStream reader,
            VolumeSampleProvider volume,
            IWavePlayer output)
        {
            SlotId = slotId;
            SlotVolume = slotVolume;
            Speed = speed;
            ResetRampOnStop = resetRampOnStop;
            Reader = reader;
            Volume = volume;
            Output = output;
        }

        public string SlotId { get; }
        public float SlotVolume { get; }
        public float Speed { get; }
        public bool ResetRampOnStop { get; }
        public PlaybackStopKind StopKind { get; set; } = PlaybackStopKind.NaturalEnd;
        public WaveStream Reader { get; }
        public VolumeSampleProvider Volume { get; }
        public IWavePlayer Output { get; }
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try { Output.Stop(); } catch { /* ignore */ }
            try { Output.Dispose(); } catch { /* ignore */ }
            try { Reader.Dispose(); } catch { /* ignore */ }
        }
    }
}
