using System.Buffers.Binary;

namespace ApartmentManagement.Utilities;

public readonly record struct ImageDimensions(int Width, int Height);

public static class ImageMetadataHelper
{
    private const int MaxHeaderBytes = 1024 * 1024;

    public static ImageDimensions ReadDimensionsAutoDetect(string path)
    {
        using var fs = File.OpenRead(path);
        var toRead = (int)Math.Min(fs.Length, MaxHeaderBytes);
        if (toRead < 24)
            throw new InvalidOperationException("File is too small to read image dimensions.");

        var buffer = new byte[toRead];
        fs.ReadExactly(buffer);

        if (buffer.Length >= 24 &&
            buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
        {
            var w = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(16, 4));
            var h = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(20, 4));
            return new ImageDimensions(w, h);
        }

        if (buffer.Length >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8)
            return ReadJpegDimensions(buffer);

        if (buffer.Length >= 12 &&
            buffer[0] == (byte)'R' && buffer[1] == (byte)'I' && buffer[2] == (byte)'F' && buffer[3] == (byte)'F' &&
            buffer[8] == (byte)'W' && buffer[9] == (byte)'E' && buffer[10] == (byte)'B' && buffer[11] == (byte)'P')
            return ReadWebpDimensions(buffer);

        throw new InvalidOperationException("Unsupported or corrupt image format.");
    }

    private static ImageDimensions ReadJpegDimensions(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length - 9; i++)
        {
            if (data[i] != 0xFF)
                continue;
            var marker = data[i + 1];
            if (marker is not (0xC0 or 0xC1 or 0xC2 or 0xC3))
                continue;
            var segmentLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 2, 2));
            if (segmentLen < 9 || i + 2 + segmentLen > data.Length)
                continue;
            var h = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 5, 2));
            var w = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 7, 2));
            return new ImageDimensions(w, h);
        }

        throw new InvalidOperationException("Could not find JPEG SOF marker in the scanned header.");
    }

    private static ImageDimensions ReadWebpDimensions(ReadOnlySpan<byte> data)
    {
        var pos = 12;
        while (pos + 8 <= data.Length)
        {
            var tag = System.Text.Encoding.ASCII.GetString(data.Slice(pos, 4));
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos + 4, 4));
            var dataStart = pos + 8;
            var padded = (int)chunkSize + (chunkSize % 2);

            if (tag == "VP8X" && chunkSize >= 10 && dataStart + 10 <= data.Length)
            {
                var w = 1 + ReadUInt24LittleEndian(data.Slice(dataStart + 4, 3));
                var h = 1 + ReadUInt24LittleEndian(data.Slice(dataStart + 7, 3));
                return new ImageDimensions(w, h);
            }

            if (tag == "VP8 " && chunkSize >= 10 && dataStart + 10 <= data.Length)
            {
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(dataStart + 3, 4));
                var w = bits & 0x3FFF;
                var h = (bits >> 14) & 0x3FFF;
                return new ImageDimensions((int)w, (int)h);
            }

            if (tag == "VP8L" && chunkSize >= 5 && dataStart + 5 <= data.Length)
            {
                if (data[dataStart] != 0x2F)
                    throw new InvalidOperationException("Unexpected VP8L chunk.");
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(dataStart + 1, 4));
                var w = (int)((bits & 0x3FFF) + 1);
                var h = (int)(((bits >> 14) & 0x3FFF) + 1);
                return new ImageDimensions(w, h);
            }

            pos = dataStart + (int)padded;
        }

        throw new InvalidOperationException("Could not read WebP dimensions.");
    }

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> span) =>
        span[0] | (span[1] << 8) | (span[2] << 16);
}
