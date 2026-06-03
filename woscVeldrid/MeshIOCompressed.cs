using System.Numerics;
using K4os.Compression.LZ4;

namespace woscVeldrid;

public class MeshIOCompressed
{
    const int REGION_SIZE = 32;
    const int CHUNK_COUNT = REGION_SIZE * REGION_SIZE;

    const float POSITION_SCALE = 1000f;
    const float NORMAL_SCALE = 10000f;

    private string _path;
    private ChunkEntry[] _entries;

    struct ChunkEntry
    {
        public bool Exists;
        public int Offset;
        public int Size;
        public int Uncompressed;
    }

    #region WRITE

    public static void Write(string path, Dictionary<(int x, int z), Mesh> chunks)
    {
        var entries = new ChunkEntry[CHUNK_COUNT];
    
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);
    
        bw.Write(CHUNK_COUNT);
    
        long tableStart = fs.Position;
        for (int i = 0; i < CHUNK_COUNT; i++)
        {
            bw.Write((byte)0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
        }
    
        for (int cz = 0; cz < REGION_SIZE; cz++)
        for (int cx = 0; cx < REGION_SIZE; cx++)
        {
            int index = cx + cz * REGION_SIZE;
    
            if (!chunks.TryGetValue((cx, cz), out var mesh))
                continue;
    
            long offset = fs.Position;
    
            // --- serialize (delta + quantized) ---
            byte[] raw;
            using (var ms = new MemoryStream())
            using (var temp = new BinaryWriter(ms))
            {
                WriteMesh(temp, mesh);
                raw = ms.ToArray();
            }
    
            // --- LZ4 compress ---
            int max = LZ4Codec.MaximumOutputSize(raw.Length);
            byte[] compressed = new byte[max];
    
            int compressedSize = LZ4Codec.Encode(
                raw, 0, raw.Length,
                compressed, 0, max);
    
            bw.Write(compressed, 0, compressedSize);
    
            entries[index] = new ChunkEntry
            {
                Exists = true,
                Offset = (int)offset,
                Size = compressedSize,
                Uncompressed = raw.Length
            };
        }
    
        fs.Seek(tableStart, SeekOrigin.Begin);
    
        for (int i = 0; i < CHUNK_COUNT; i++)
        {
            bw.Write((byte)(entries[i].Exists ? 1 : 0));
            bw.Write(entries[i].Offset);
            bw.Write(entries[i].Size);
            bw.Write(entries[i].Uncompressed);
        }
    }

    // private static void WriteMesh(BinaryWriter bw, Mesh mesh)
    // {
    //     WriteVarInt(bw, mesh.Vertices.Length);
    //     WriteVarInt(bw, mesh.Indices.Length);
    //
    //     int px = 0, py = 0, pz = 0;
    //     int pnx = 0, pny = 0, pnz = 0;
    //
    //     foreach (var v in mesh.Vertices)
    //     {
    //         int x = (int)(v.Position.X * POSITION_SCALE);
    //         int y = (int)(v.Position.Y * POSITION_SCALE);
    //         int z = (int)(v.Position.Z * POSITION_SCALE);
    //
    //         int nx = (int)(v.Normal.X * NORMAL_SCALE);
    //         int ny = (int)(v.Normal.Y * NORMAL_SCALE);
    //         int nz = (int)(v.Normal.Z * NORMAL_SCALE);
    //
    //         WriteVarInt(bw, x - px);
    //         WriteVarInt(bw, y - py);
    //         WriteVarInt(bw, z - pz);
    //
    //         WriteVarInt(bw, nx - pnx);
    //         WriteVarInt(bw, ny - pny);
    //         WriteVarInt(bw, nz - pnz);
    //
    //         px = x; py = y; pz = z;
    //         pnx = nx; pny = ny; pnz = nz;
    //     }
    //
    //     uint prevIndex = 0;
    //     foreach (var i in mesh.Indices)
    //     {
    //         WriteVarInt(bw, (int)(i - prevIndex));
    //         prevIndex = i;
    //     }
    //
    //     int prevColor = 0;
    //     foreach (var c in mesh.ColorIds)
    //     {
    //         WriteVarInt(bw, c - prevColor);
    //         prevColor = c;
    //     }
    // }
    
    private static void WriteMesh(BinaryWriter bw, Mesh mesh)
    {
        WriteVarInt(bw, mesh.Vertices.Length);
        WriteVarInt(bw, mesh.Indices.Length);

        int px = 0, py = 0, pz = 0;
        int pnx = 0, pny = 0, pnz = 0;
        int pc = 0;

        foreach (var v in mesh.Vertices)
        {
            int x = (int)(v.Position.X * POSITION_SCALE);
            int y = (int)(v.Position.Y * POSITION_SCALE);
            int z = (int)(v.Position.Z * POSITION_SCALE);

            int nx = (int)(v.Normal.X * NORMAL_SCALE);
            int ny = (int)(v.Normal.Y * NORMAL_SCALE);
            int nz = (int)(v.Normal.Z * NORMAL_SCALE);

            int c = v.ColorId;

            WriteVarInt(bw, x - px);
            WriteVarInt(bw, y - py);
            WriteVarInt(bw, z - pz);

            WriteVarInt(bw, nx - pnx);
            WriteVarInt(bw, ny - pny);
            WriteVarInt(bw, nz - pnz);

            WriteVarInt(bw, c - pc);

            px = x; py = y; pz = z;
            pnx = nx; pny = ny; pnz = nz;
            pc = c;
        }

        uint prevIndex = 0;
        foreach (var i in mesh.Indices)
        {
            WriteVarInt(bw, (int)(i - prevIndex));
            prevIndex = i;
        }
    }

    #endregion

    #region READ

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

        byte[] raw = new byte[entry.Uncompressed];

        LZ4Codec.Decode(
            compressed, 0, entry.Size,
            raw, 0, entry.Uncompressed);

        using var ms = new MemoryStream(raw);
        using var br = new BinaryReader(ms);

        return ReadMesh(br);
    }

    // private Mesh ReadMesh(BinaryReader br)
    // {
    //     int vertexCount = ReadVarInt(br);
    //     int indexCount = ReadVarInt(br);
    //
    //     var vertices = new Vertex[vertexCount];
    //     var indices = new uint[indexCount];
    //     var colors = new int[vertexCount];
    //
    //     int x = 0, y = 0, z = 0;
    //     int nx = 0, ny = 0, nz = 0;
    //
    //     for (int i = 0; i < vertexCount; i++)
    //     {
    //         x += ReadVarInt(br);
    //         y += ReadVarInt(br);
    //         z += ReadVarInt(br);
    //
    //         nx += ReadVarInt(br);
    //         ny += ReadVarInt(br);
    //         nz += ReadVarInt(br);
    //
    //         vertices[i] = new Vertex(
    //             new Vector3(
    //                 x / POSITION_SCALE,
    //                 y / POSITION_SCALE,
    //                 z / POSITION_SCALE),
    //             new Vector3(
    //                 nx / NORMAL_SCALE,
    //                 ny / NORMAL_SCALE,
    //                 nz / NORMAL_SCALE)
    //         );
    //     }
    //
    //     uint prevIndex = 0;
    //     for (int i = 0; i < indexCount; i++)
    //     {
    //         prevIndex += (uint)ReadVarInt(br);
    //         indices[i] = prevIndex;
    //     }
    //
    //     int prevColor = 0;
    //     for (int i = 0; i < vertexCount; i++)
    //     {
    //         prevColor += ReadVarInt(br);
    //         colors[i] = prevColor;
    //     }
    //
    //     return new Mesh
    //     {
    //         Vertices = vertices,
    //         Indices = indices,
    //         ColorIds = colors
    //     };
    // }
    private Mesh ReadMesh(BinaryReader br)
    {
        int vertexCount = ReadVarInt(br);
        int indexCount = ReadVarInt(br);

        var vertices = new Vertex[vertexCount];
        var indices = new uint[indexCount];

        int x = 0, y = 0, z = 0;
        int nx = 0, ny = 0, nz = 0;
        int c = 0;

        for (int i = 0; i < vertexCount; i++)
        {
            x += ReadVarInt(br);
            y += ReadVarInt(br);
            z += ReadVarInt(br);

            nx += ReadVarInt(br);
            ny += ReadVarInt(br);
            nz += ReadVarInt(br);

            c += ReadVarInt(br);

            vertices[i] = new Vertex(
                new Vector3(
                    x / POSITION_SCALE,
                    y / POSITION_SCALE,
                    z / POSITION_SCALE),
                new Vector3(
                    nx / NORMAL_SCALE,
                    ny / NORMAL_SCALE,
                    nz / NORMAL_SCALE),
                c
            );
        }
        // for (int i = 0; i < vertices.Length; i++)
        // {
        //     var p = vertices[i].Position;
        //
        //     if (MathF.Abs(p.X) > 10000 ||
        //         MathF.Abs(p.Y) > 10000 ||
        //         MathF.Abs(p.Z) > 10000)
        //     {
        //         Console.WriteLine($"BAD VERTEX {i}: {p}");
        //         break;
        //     }
        // }

        uint prevIndex = 0;
        for (int i = 0; i < indexCount; i++)
        {
            prevIndex += (uint)ReadVarInt(br);
            indices[i] = prevIndex;
        }

        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] >= vertices.Length)
            {
                Console.WriteLine($"BAD INDEX {i}: {indices[i]} >= {vertices.Length}");
                break;
            }
        }
        
        return new Mesh
        {
            Vertices = vertices,
            Indices = indices
        };
    }

    #endregion

    #region VARINT

    private static void WriteVarInt(BinaryWriter bw, int value)
    {
        uint v = (uint)((value << 1) ^ (value >> 31)); // zigzag

        while (v >= 0x80)
        {
            bw.Write((byte)(v | 0x80));
            v >>= 7;
        }

        bw.Write((byte)v);
    }

    private static int ReadVarInt(BinaryReader br)
    {
        uint result = 0;
        int shift = 0;

        while (true)
        {
            byte b = br.ReadByte();
            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                break;

            shift += 7;
        }

        int value = (int)((result >> 1) ^ -(int)(result & 1)); // zigzag decode
        return value;
    }

    #endregion
}