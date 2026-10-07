using System.Media;

namespace Slime.Desktop;

/// <summary>A short, soft rising/falling squeak generated in memory; no audio file is needed.</summary>
internal sealed class SqueakSound : IDisposable
{
    private readonly MemoryStream wave = CreateWave();
    private readonly SoundPlayer player;

    public SqueakSound()
    {
        player = new SoundPlayer(wave);
        player.Load();
    }

    public void Play()
    {
        // Missing audio hardware must not interrupt the pet's mouse handling.
        try { player.Play(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private static MemoryStream CreateWave()
    {
        const int sampleRate = 22050;
        const int samples = (int)(sampleRate * 0.24);
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + samples * 2);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(samples * 2);
            var phase = 0.0;
            for (var i = 0; i < samples; i++)
            {
                var progress = (double)i / samples;
                var frequency = 740 + 430 * Math.Sin(progress * Math.PI) - 130 * progress;
                phase += 2 * Math.PI * frequency / sampleRate;
                var envelope = Math.Sin(progress * Math.PI) * Math.Min(1, progress * 18);
                var tone = Math.Sin(phase) + 0.23 * Math.Sin(phase * 2) + 0.08 * Math.Sin(phase * 3);
                writer.Write((short)(tone * envelope * 5500));
            }
        }
        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        player.Stop();
        player.Dispose();
        wave.Dispose();
    }
}
