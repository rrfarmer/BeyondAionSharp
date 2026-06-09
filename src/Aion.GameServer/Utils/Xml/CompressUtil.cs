using System.IO;
using System.IO.Compression;

namespace Aion.GameServer.Utils.Xml;

/// <summary>
/// Java parity: utils/xml/CompressUtil (Rolandas). Java java.util.zip.Deflater/Inflater produce/consume zlib (RFC1950)
/// format → C# System.IO.Compression.ZLibStream (zlib-compatible, unlike raw DeflateStream).
/// </summary>
public static class CompressUtil
{
    public static byte[] Decompress(byte[] bytes)
    {
        using MemoryStream input = new(bytes);
        using ZLibStream decompressor = new(input, CompressionMode.Decompress);
        using MemoryStream bos = new(bytes.Length);
        decompressor.CopyTo(bos);
        return bos.ToArray();
    }

    public static byte[] Compress(byte[] bytes)
    {
        using MemoryStream bos = new();
        using (ZLibStream compressor = new(bos, CompressionMode.Compress, true))
        {
            compressor.Write(bytes, 0, bytes.Length);
        }
        return bos.ToArray();
    }
}
