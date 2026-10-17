using System.IO;
using System.Media;
using Brady.App.Settings;

namespace Brady.App.Platform;

/// <summary>
/// The optional "soft sound" (off by default): a short, quiet wooden tick when a reminder arrives,
/// never louder than that (Toast.md). It is synthesized once in memory, so there's no sound file to
/// ship, and it plays only when the setting is on.
/// </summary>
public sealed class SoftSound(SettingsService settings) : IDisposable
{
    private const int SampleRate = 44100;
    private SoundPlayer? _player;

    public void PlayIfEnabled()
    {
        if (!settings.Current.Sound) return;
        _player ??= new SoundPlayer(new MemoryStream(Tick()));
        _player.Play(); // asynchronous; never blocks the toast
    }

    public void Dispose() => _player?.Dispose();

    // 90 ms: a 660 Hz tone with a hint of its octave, a 4 ms attack and a fast exponential decay,
    // peaking at about 13% of full scale. 16-bit mono PCM in a WAV container.
    private static byte[] Tick()
    {
        var samples = SampleRate * 90 / 1000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);       // PCM
        writer.Write((short)1);       // mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2); // byte rate
        writer.Write((short)2);       // block align
        writer.Write((short)16);      // bits per sample
        writer.Write("data"u8);
        writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / SampleRate;
            var attack = Math.Min(1, t / 0.004);
            var envelope = attack * Math.Exp(-t * 45);
            var wave = Math.Sin(2 * Math.PI * 660 * t) + 0.3 * Math.Sin(2 * Math.PI * 1320 * t);
            writer.Write((short)(wave / 1.3 * envelope * 0.18 * short.MaxValue));
        }
        writer.Flush();
        return stream.ToArray();
    }
}
