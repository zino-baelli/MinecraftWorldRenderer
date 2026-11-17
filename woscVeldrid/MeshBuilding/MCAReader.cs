using System.IO.Compression;

namespace woscVeldrid;

public class MCAReader
{
    
    public static byte[] ReadChunk(string path, int chunkX, int chunkZ)
    {
        using var fs = File.OpenRead(path);

        int index = (chunkX & 31) + (chunkZ & 31) * 32;
        fs.Seek(index * 4, SeekOrigin.Begin);

        byte[] entry = new byte[4];
        fs.Read(entry, 0, 4);

        int offset = (entry[0] << 16) | (entry[1] << 8) | entry[2];
        int sectorCount = entry[3];

        if (offset == 0) return null;

        fs.Seek(offset * 4096, SeekOrigin.Begin);

        byte[] lengthBytes = new byte[4];
        fs.Read(lengthBytes, 0, 4);
        int length = System.BitConverter.ToInt32(lengthBytes.Reverse().ToArray(), 0);

        int compression = fs.ReadByte();

        byte[] compressed = new byte[length - 1];
        fs.Read(compressed, 0, compressed.Length);

        using var ms = new MemoryStream(compressed);
        Stream stream = compression switch
        {
            1 => new GZipStream(ms, CompressionMode.Decompress),
            2 => new ZLibStream(ms, CompressionMode.Decompress),
            _ => throw new Exception("Unknown compression")
        };
        using var result = new MemoryStream();
        stream.CopyTo(result);

        return result.ToArray();
    }
}