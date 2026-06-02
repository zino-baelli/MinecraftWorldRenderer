using woscVeldrid.Shader;

namespace woscVeldrid;

public static class FileProcessor
{
    public static void ProcessFile(string path, Dictionary<string,int> blockNameId,int fileIndex, string storagePath)
    {
        Dictionary<(int x, int z), Chunk> chunks =  new Dictionary<(int x, int z), Chunk>();
        List<Chunk> data = new List<Chunk>();
        var meshes = new List<Mesh>();


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
        
    
   
        foreach (var kv in chunks)
        {
            meshes.Add(MeshBuilder.BuildMesh(kv.Value, blockNameId));
        }
   
        
        var index = 0;

        while (index < meshes.Count)
        {
            var regionChunks = new Dictionary<(int , int ), Mesh>();
            for (int j = 0; j < 32; j++)
            {
                for (int k = 0; k < 32; k++)
                {
                    if (index == meshes.Count)
                    {
                        break;
                    }
                    regionChunks.Add((k, j), meshes[index++]);
                }
            }

            MeshIOCompressed.Write(storagePath + $"/{Path.GetFileNameWithoutExtension(path)}.mesh", regionChunks);
        }
    }
}