using woscVeldrid.Shader;

namespace woscVeldrid;

public static class FileProcessor
{
    public static void ProcessFile(string path, Dictionary<string,int> blockNameId,int fileIndex, string storagePath)
    {
        Dictionary<(int x, int z), Chunk> chunks =  new Dictionary<(int x, int z), Chunk>();
        List<Chunk> data = new List<Chunk>();


        byte[] chunkData = null;
        int foundX = -1, foundZ = -1;

        int chunkCounter = 0;

        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                chunkData = MCAReader.ReadChunk(path, x, z);

                if (chunkData != null)
                {
                    if (x > 32 || z > 32)
                    {
                        throw new IndexOutOfRangeException();
                    }

                  
                    var nbtData = NbtParser.LoadChunkNBT(chunkData);
                    var chunk = NbtDecoder.DecodeChunk(nbtData);
                    chunks[(chunk.X, chunk.Z)] = chunk;
                    
                    data.Add(chunk);
                    chunkCounter++;
                    
                }
            }
        }
        
        // var meshes = new List<Mesh>();

   
        // foreach (var kv in chunks)
        // {
        //     meshes.Add(MeshBuilder.BuildMesh(kv.Value, blockNameId));
        // }
        
        var meshes = new Dictionary<(int x, int z), Mesh>();

        // foreach (var kv in chunks)
        // {
        //     meshes[kv.Key] = MeshBuilder.BuildMesh(kv.Value, blockNameId);
        // }
        
        foreach (var kv in chunks)
        {
            var worldX = kv.Key.x;
            var worldZ = kv.Key.z;

            var (lx, lz) = ChunkCoords.ToLocal(worldX, worldZ);

            meshes[(lx, lz)] = MeshBuilder.BuildMesh(kv.Value, blockNameId);
        }
   
        
        // var index = 0;
        //
        // while (index < meshes.Count)
        // {
        //     var regionChunks = new Dictionary<(int , int ), Mesh>();
        //     for (int j = 0; j < 32; j++)
        //     {
        //         for (int k = 0; k < 32; k++)
        //         {
        //             if (index == meshes.Count)
        //             {
        //                 break;
        //             }
        //             regionChunks.Add((k, j), meshes[index++]);
        //         }
        //     }
        //
        //     MeshIOCompressed.Write(storagePath + $"/{Path.GetFileNameWithoutExtension(path)}.mesh", regionChunks);
        // }
        MeshIOCompressed.Write(storagePath + $"/{Path.GetFileNameWithoutExtension(path)}.mesh", meshes);
        //
        // var io = new MeshIOCompressed();
        // io.LoadFile(storagePath + $"/{Path.GetFileNameWithoutExtension(path)}.mesh");
        //
        // // pick a few random chunks you KNOW exist
        // var testCoords = new List<(int x, int z)>
        // {
        //     // (-1,-1),
        //     // (-1,-2),
        //     // (-2,-1),
        //     // (-5,-5),
        //     // (-10,-3)
        //     (0,0),
        //     (1,0),
        //     (0,1),
        //     (5,5),
        //     (10,3)
        // };
        //
        // foreach (var (x, z) in testCoords)
        // {
        //     var m = meshes[(x, z)];
        //     var m2 = io.LoadChunk(x, z);
        //
        //     Console.WriteLine($"Chunk ({x},{z})");
        //
        //     if (m2 == null)
        //     {
        //         Console.WriteLine(" missing after load");
        //         continue;
        //     }
        //
        //     Console.WriteLine($"  vtx: {m.Vertices.Length} vs {m2.Vertices.Length}");
        //     Console.WriteLine($"  idx: {m.Indices.Length} vs {m2.Indices.Length}");
        //
        //     // compare first vertex position (cheap sanity check)
        //     var a = m.Vertices[0].Position;
        //     var b = m2.Vertices[0].Position;
        //
        //     Console.WriteLine($"  first vtx: {a} vs {b}");
        // }

    }
}