using System.Numerics;
using K4os.Compression.LZ4;

namespace woscVeldrid;

public class RegionMeshIOCompressed
{
    const int REGION_SIZE = 32;
    const int CHUNK_COUNT = REGION_SIZE * REGION_SIZE;

    private string _path;
    private ChunkEntry[] _entries;

    struct ChunkEntry
    {
        public bool Exists;
        public int Offset;
        public int Size;          // compressed size
        public int Uncompressed;  // NEW
    }

    public static void Write(string path, Dictionary<(int x, int z), Mesh> chunks)
    {
        var entries = new ChunkEntry[CHUNK_COUNT];

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // HEADER
        bw.Write(CHUNK_COUNT);

        long tableStart = fs.Position;
        for (int i = 0; i < CHUNK_COUNT; i++)
        {
            bw.Write((byte)0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0); // uncompressed size
        }

        // DATA
        for (int cz = 0; cz < REGION_SIZE; cz++)
        for (int cx = 0; cx < REGION_SIZE; cx++)
        {
            int index = cx + cz * REGION_SIZE;

            if (!chunks.TryGetValue((cx, cz), out var mesh))
                continue;

            long offset = fs.Position;

            // --- serialize mesh into raw buffer ---
            byte[] rawData;
            using (var ms = new MemoryStream())
            using (var tempWriter = new BinaryWriter(ms))
            {
                WriteMesh(tempWriter, mesh);
                rawData = ms.ToArray();
            }

            // --- LZ4 compress ---
            int maxSize = LZ4Codec.MaximumOutputSize(rawData.Length);
            byte[] compressed = new byte[maxSize];

            int compressedSize = LZ4Codec.Encode(
                rawData, 0, rawData.Length,
                compressed, 0, maxSize
            );

            // write only actual compressed bytes
            bw.Write(compressed, 0, compressedSize);

            long end = fs.Position;

            entries[index] = new ChunkEntry
            {
                Exists = true,
                Offset = (int)offset,
                Size = compressedSize,
                Uncompressed = rawData.Length
            };
        }

        // WRITE TABLE
        fs.Seek(tableStart, SeekOrigin.Begin);

        for (int i = 0; i < CHUNK_COUNT; i++)
        {
            bw.Write((byte)(entries[i].Exists ? 1 : 0));
            bw.Write(entries[i].Offset);
            bw.Write(entries[i].Size);
            bw.Write(entries[i].Uncompressed);
        }
    }

    private static void WriteMesh(BinaryWriter bw, Mesh mesh)
    {
        bw.Write(mesh.Vertices.Length);
        bw.Write(mesh.Indices.Length);

        foreach (var v in mesh.Vertices)
        {
            bw.Write(v.Position.X);
            bw.Write(v.Position.Y);
            bw.Write(v.Position.Z);

            bw.Write(v.Normal.X);
            bw.Write(v.Normal.Y);
            bw.Write(v.Normal.Z);
        }

        foreach (var i in mesh.Indices)
            bw.Write(i);

        foreach (var c in mesh.ColorIds)
            bw.Write(c);
    }

    public void LoadFile(string path)
    {
        _path = path;
        LoadHeader();
    }

    private void LoadHeader()
    {
        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        int count = br.ReadInt32();
        _entries = new ChunkEntry[count];

        for (int i = 0; i < count; i++)
        {
            _entries[i] = new ChunkEntry
            {
                Exists = br.ReadByte() == 1,
                Offset = br.ReadInt32(),
                Size = br.ReadInt32(),
                Uncompressed = br.ReadInt32()
            };
        }
    }

    public Mesh? LoadChunk(int cx, int cz)
    {
        int index = cx + cz * REGION_SIZE;
        var entry = _entries[index];

        if (!entry.Exists)
            return null;

        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read);
        fs.Seek(entry.Offset, SeekOrigin.Begin);

        byte[] compressed = new byte[entry.Size];
        fs.Read(compressed, 0, entry.Size);

        byte[] decompressed = new byte[entry.Uncompressed];

        LZ4Codec.Decode(
            compressed, 0, entry.Size,
            decompressed, 0, entry.Uncompressed
        );

        using var ms = new MemoryStream(decompressed);
        using var br = new BinaryReader(ms);

        return ReadMesh(br);
    }

    private Mesh ReadMesh(BinaryReader br)
    {
        int vertexCount = br.ReadInt32();
        int indexCount = br.ReadInt32();

        var vertices = new Vertex[vertexCount];
        var indices = new uint[indexCount];
        var colors = new int[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            var pos = new Vector3(
                br.ReadSingle(),
                br.ReadSingle(),
                br.ReadSingle()
            );

            var normal = new Vector3(
                br.ReadSingle(),
                br.ReadSingle(),
                br.ReadSingle()
            );

            vertices[i] = new Vertex(pos, normal);
        }

        for (int i = 0; i < indexCount; i++)
            indices[i] = br.ReadUInt32();

        for (int i = 0; i < vertexCount; i++)
            colors[i] = br.ReadInt32();

        return new Mesh
        {
            Vertices = vertices,
            Indices = indices,
            ColorIds = colors
        };
    }
}