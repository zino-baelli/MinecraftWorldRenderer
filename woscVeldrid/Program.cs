using woscVeldrid;
using woscVeldrid.Shader;

var windowRenderer = new AppWindow(1920, 1080, "WoSc");

// windowRenderer.Render();

var path = "../../../TestData/r.12.3.mca";
string[] files = Directory.GetFiles(@"C:\Users\zino\Downloads\Greenfield v0.5.4\Greenfield v0.5.4\region", "*.mca", SearchOption.TopDirectoryOnly);

var chunkAmount = files.Length * 1024;
var fileRenderAmount = 20;

HashSet<string> blockTypeSet = new HashSet<string>();
var blockNameId = Helper.GetBlockToIdFromFile("../../../TestData/blockPaletteComplete.csv");
var blockPalette = Helper.GetColorPaletteFromFile("../../../TestData/blockPaletteComplete.csv");


// Chunk[] data = new Chunk[1024]; //amount of chunks per .mca file are 32X32
List<Chunk> data = new List<Chunk>();
Dictionary<(int x, int z), Chunk> chunks =  new Dictionary<(int x, int z), Chunk>();

//
// byte[] chunkData = null;
// int foundX = -1, foundZ = -1;
//
// int chunkCounter = 0;
//
// for (int x = 0; x < 32; x++)
// {
//     for (int z = 0; z < 32; z++)
//     {
//         chunkData = MCAReader.ReadChunk(path, x, z);
//
//         if (chunkData != null)
//         {
//             if (x > 32 || z > 32)
//             {
//                 throw new IndexOutOfRangeException();
//             }
//
//             foundX = x;
//             foundZ = z;
//             // Console.WriteLine($"Using chunk {x},{z}");
//             var nbtData = NbtParser.LoadChunkNBT(chunkData);
//             data[z + 32 * x] = NbtDecoder.DecodeChunk(nbtData);
//             // data[z + 32  * x] = new Chunk(foundX, foundZ, NbtDecoder.DecodeChunk(nbtData).ToArray());
//             // data[chunkCounter] = new Chunk(foundX, foundZ, NbtDecoder.DecodeChunk(nbtData).ToArray());
//             chunkCounter++;
//
//             // break;
//         }
//     }
//     // if (chunkData != null) break;
// }
//
// if (chunkData == null)
// {
//     Console.WriteLine("No chunks found in this region!");
//     return;
// }

for (int j = 0; j < fileRenderAmount; j++)
{
    path = files[j];
    Console.WriteLine($"Loading file: {path}");
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

                foundX = x;
                foundZ = z;
                // Console.WriteLine($"Using chunk {x},{z}");
                var nbtData = NbtParser.LoadChunkNBT(chunkData);
                // data.Add(NbtDecoder.DecodeChunk(nbtData/*, out HashSet<string> blockTypes*/));
                var chunk = NbtDecoder.DecodeChunk(nbtData);
                chunks[(chunk.X, chunk.Z)] = chunk;
                
                data.Add(chunk);
                chunkCounter++;
                
                // if (chunk.X != x || chunk.Z != z)
                // {
                //     Console.WriteLine($"Mismatch: file({x},{z}) nbt({chunk.X},{chunk.Z})");
                // }
                
                // foreach (var blockType in blockTypes)
                // {
                //     blockTypeSet.Add(blockType);
                // }
                // break;
            }
        }
        // if (chunkData != null) break;
    }

    if (chunkData == null)
    {
        // Console.WriteLine("No chunks found in this region!");
        // return;
    }
    // Console.WriteLine($"Finished file {files[j]}");
}
Console.WriteLine("All chunks read from files");

// Helper.WriteBlockTypesToFile(blockTypeSet.ToList());

// var nbtData = NbtParser.LoadChunkNBT(chunkData);
// NbtDebugger.PrintTag(nbtData);
// var decodedChunk = NbtDecoder.DecodeChunk(nbtData);

// File.WriteAllText(@"C:\Users\zino\Desktop\renderDocCaptures\chunk0.csv", data[0].toCsv());

// var mesh = MeshBuilder.BuildMesh(data[0]);
// var mesh2 = MeshBuilder.BuildMesh(data[1]);
// var meshes = new[] { mesh, mesh2, MeshBuilder.BuildMesh(data[2]),  MeshBuilder.BuildMesh(data[3]) };
// var meshes = new[] { MeshBuilder.BuildMesh(data[2]) };
//
// var meshes = new Mesh[data.Length];
// for (int j = 0; j < data.Length; j++)


// var meshes = new Mesh[data.Count];
// Console.WriteLine("Total meshes: " + meshes.Length);
// for (int j = 0; j < data.Count; j++)
// {
//     meshes[j] = MeshBuilder.BuildMesh(data[j], blockNameId);
//     // Console.WriteLine($"Mesh {j} built");
// }

var meshes = new List<Mesh>();
Console.WriteLine("Total meshes: " + meshes.Count);
foreach (var kv in chunks)
{
    meshes.Add(MeshBuilder.BuildMesh(kv.Value, blockNameId));
}

//todo write mesh 883 to file
//compare with nbt data for chunk 883
// File.WriteAllText("../../../TestData/mesh883.txt",meshes[883].ToString());
// File.WriteAllText("../../../TestData/nbtData883.csv", data[883].toCsv());
Console.WriteLine("All meshes built");
// windowRenderer.BlockIds = blockNameId;
windowRenderer.ColorPalette = blockPalette;
windowRenderer.Render(meshes.ToArray());

var i = 1;