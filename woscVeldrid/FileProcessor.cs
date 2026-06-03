using woscVeldrid.Shader;

namespace woscVeldrid;

public static class FileProcessor
{
    public static void ProcessFile(string path, Dictionary<string,int> blockNameId,int fileIndex, string storagePath,ref LodBuilder lodBuilder)
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
        
        var meshes = new Dictionary<(int x, int z), Mesh>();
        
        foreach (var kv in chunks)
        {
            var worldX = kv.Key.x;
            var worldZ = kv.Key.z;

            var (lx, lz) = ChunkCoords.ToLocal(worldX, worldZ);

            meshes[(lx, lz)] = MeshBuilder.BuildMesh(kv.Value, blockNameId);
        }

        var lods = lodBuilder.BuildLods(meshes, 4);
        
        MeshIOCompressed.Write(storagePath + $"/{Path.GetFileNameWithoutExtension(path)}.mesh", meshes);

        for (int i = 0; i < lods.Length; i++)
        {
            if (!Directory.Exists(storagePath + $"/lod{i}"))
            {
                Directory.CreateDirectory(storagePath + $"/lod{i}");
            }
            MeshIOCompressed.Write(storagePath + $"/lod{i}/{Path.GetFileNameWithoutExtension(path)}.mesh", lods[i]);
        }
 
    }
}