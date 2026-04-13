using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VRA.AudioManager.Audio;

public class LevelMonitor : IDisposable
{
    private record DeviceCapture(WasapiCapture Capture, float[] Levels, object Lock);

    private readonly Dictionary<string, DeviceCapture> _captures = new();
    private readonly HashSet<string> _failedDevices = new();

    /// <param name="devices">Clé = Windows Device ID (GUID stable), valeur = MMDevice.</param>
    public LevelMonitor(Dictionary<string, MMDevice> devices)
    {
        foreach (var (id, device) in devices)
        {
            var capture  = new WasapiCapture(device);
            int channels = capture.WaveFormat.Channels;
            var levels   = new float[channels];
            var lck      = new object();

            Array.Fill(levels, -96f);
            capture.DataAvailable += (_, e) => OnDataAvailable(e, capture.WaveFormat, levels, lck);

            _captures[id] = new DeviceCapture(capture, levels, lck);
        }
    }

    public void Start()
    {
        foreach (var (id, dc) in _captures)
        {
            try
            {
                dc.Capture.StartRecording();
            }
            catch (COMException ex)
            {
                _failedDevices.Add(id);
                Console.Error.WriteLine($"  ✘ Périphérique inaccessible (0x{ex.HResult:X8}) — " +
                    "vérifiez qu'aucune autre application n'a le périphérique en mode exclusif.");
            }
        }
    }

    /// <summary>True si le périphérique n'a pas pu être ouvert.</summary>
    public bool IsFailed(string windowsDeviceId) => _failedDevices.Contains(windowsDeviceId);

    public void Stop()
    {
        foreach (var dc in _captures.Values) dc.Capture.StopRecording();
    }

    /// <param name="windowsDeviceId">Windows Device ID (GUID) du périphérique.</param>
    /// <param name="channelIndex">Index 0-based du canal au sein du périphérique.</param>
    public float GetLevel(string windowsDeviceId, int channelIndex)
    {
        if (_failedDevices.Contains(windowsDeviceId)) return -96f;
        if (!_captures.TryGetValue(windowsDeviceId, out var dc)) return -96f;
        lock (dc.Lock)
            return channelIndex < dc.Levels.Length ? dc.Levels[channelIndex] : -96f;
    }

    private static void OnDataAvailable(WaveInEventArgs e, WaveFormat fmt, float[] levels, object lck)
    {
        int channels = fmt.Channels;
        int bps      = fmt.BitsPerSample / 8;
        int frames   = e.BytesRecorded / (bps * channels);

        if (frames == 0) return;

        var sums = new double[channels];

        for (int f = 0; f < frames; f++)
            for (int ch = 0; ch < channels; ch++)
            {
                int offset = (f * channels + ch) * bps;
                float sample = ReadSample(e.Buffer, offset, fmt.BitsPerSample);
                sums[ch] += sample * sample;
            }

        lock (lck)
            for (int ch = 0; ch < channels; ch++)
            {
                double rms = Math.Sqrt(sums[ch] / frames);
                levels[ch] = rms > 1e-10 ? (float)(20.0 * Math.Log10(rms)) : -96f;
            }
    }

    private static float ReadSample(byte[] buffer, int offset, int bitsPerSample) =>
        bitsPerSample switch
        {
            32 => BitConverter.ToSingle(buffer, offset),
            16 => BitConverter.ToInt16(buffer, offset) / 32768f,
            24 => Read24Bit(buffer, offset) / 8388608f,
            _  => 0f
        };

    private static int Read24Bit(byte[] buffer, int offset)
    {
        int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        return (value & 0x800000) != 0 ? value | unchecked((int)0xFF000000) : value;
    }

    public void Dispose()
    {
        foreach (var dc in _captures.Values)
        {
            dc.Capture.StopRecording();
            dc.Capture.Dispose();
        }
    }
}
