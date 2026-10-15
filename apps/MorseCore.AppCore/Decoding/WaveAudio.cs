using System.Buffers.Binary;

namespace MorseCore.AppCore.Decoding;

/// <summary>A bounded mono analysis signal. Input is reduced to 8 kHz without retaining the source file.</summary>
public sealed record WaveAudio(float[] Samples, int SampleRate)
{
    public const long MaxFileBytes = 64 * 1024 * 1024;
    public const int MaxSeconds = 120;
    public const int AnalysisRate = 8000;
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);

    public static WaveAudio Read(string path, CancellationToken cancellationToken = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream, cancellationToken);
    }

    public static WaveAudio Read(Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanSeek || !stream.CanRead) throw new ArgumentException("A seekable readable WAV stream is required.");
        cancellationToken.ThrowIfCancellationRequested();
        if (stream.Length < 12 || stream.Length > MaxFileBytes) throw new InvalidDataException("decoderFileSize");
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, true);
        if (reader.ReadUInt32() != 0x46464952) throw new InvalidDataException("decoderFormat");
        long riffEnd = 8L + reader.ReadUInt32();
        if (reader.ReadUInt32() != 0x45564157 || riffEnd > stream.Length || riffEnd < 12) throw new InvalidDataException("decoderFormat");
        int format = 0, channels = 0, rate = 0, bits = 0, align = 0;
        long dataStart = 0, dataLength = 0;
        bool hasFormat = false, hasData = false;
        while (stream.Position + 8 <= riffEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint id = reader.ReadUInt32(), size = reader.ReadUInt32();
            long start = stream.Position, end = start + size;
            if (end > riffEnd) throw new InvalidDataException("decoderFormat");
            if (id == 0x20746D66)
            {
                if (hasFormat || size < 16) throw new InvalidDataException("decoderFormat");
                format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                uint bytesPerSecond = reader.ReadUInt32(); align = reader.ReadUInt16(); bits = reader.ReadUInt16();
                if (format == 0xFFFE)
                {
                    if (size < 40 || reader.ReadUInt16() < 22) throw new InvalidDataException("decoderFormat");
                    int validBits = reader.ReadUInt16(); reader.ReadUInt32();
                    byte[] subFormat = reader.ReadBytes(16);
                    if (validBits != bits || !subFormat.AsSpan(4).SequenceEqual(new byte[] { 0, 0, 16, 0, 128, 0, 0, 170, 0, 56, 155, 113 })) throw new InvalidDataException("decoderFormat");
                    format = BinaryPrimitives.ReadInt32LittleEndian(subFormat);
                }
                if (channels is < 1 or > 2 || rate is < 8000 or > 192000 ||
                    !(format == 1 && bits is 8 or 16 or 24 or 32 || format == 3 && bits is 32 or 64) ||
                    align != channels * (bits / 8) || bytesPerSecond != (long)rate * align)
                    throw new InvalidDataException("decoderFormat");
                hasFormat = true;
            }
            else if (id == 0x61746164)
            {
                if (hasData) throw new InvalidDataException("decoderFormat");
                dataStart = start; dataLength = size; hasData = true;
            }
            stream.Position = end + (size & 1);
            if (stream.Position > riffEnd) throw new InvalidDataException("decoderFormat");
        }
        if (stream.Position != riffEnd || !hasFormat || !hasData || dataLength == 0 || dataLength % align != 0) throw new InvalidDataException("decoderFormat");
        long frames = dataLength / align;
        if (frames > (long)rate * MaxSeconds) throw new InvalidDataException("decoderDuration");
        int outputCount = checked((int)((frames * AnalysisRate + rate - 1) / rate));
        var samples = new float[outputCount];
        stream.Position = dataStart;
        int output = 0, accumulated = 0;
        long phase = 0;
        double sum = 0;
        for (long frame = 0; frame < frames; frame++)
        {
            if ((frame & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
            double mono = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                double value;
                if (format == 3) value = bits == 32 ? reader.ReadSingle() : reader.ReadDouble();
                else value = bits switch
                {
                    8 => (reader.ReadByte() - 128) / 128.0,
                    16 => reader.ReadInt16() / 32768.0,
                    24 => Read24(reader) / 8388608.0,
                    _ => reader.ReadInt32() / 2147483648.0
                };
                if (!double.IsFinite(value)) throw new InvalidDataException("decoderFormat");
                mono += Math.Clamp(value, -1, 1);
            }
            sum += mono / channels; accumulated++; phase += AnalysisRate;
            if (phase >= rate)
            {
                samples[output++] = (float)(sum / accumulated);
                sum = 0; accumulated = 0; phase -= rate;
            }
        }
        if (accumulated > 0) samples[output++] = (float)(sum / accumulated);
        if (output != samples.Length) Array.Resize(ref samples, output);
        return new(samples, AnalysisRate);
    }

    private static int Read24(BinaryReader reader)
    {
        int value = reader.ReadByte() | reader.ReadByte() << 8 | reader.ReadByte() << 16;
        return (value << 8) >> 8;
    }
}
