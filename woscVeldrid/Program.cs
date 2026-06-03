using Meshoptimizer;
using woscVeldrid;
using woscVeldrid.Shader;

var windowRenderer = new AppWindow(1920, 1080, "WoSc");
string[] files = Directory.GetFiles(@"C:\Users\zino\Downloads\Greenfield v0.5.4\Greenfield v0.5.4\region", "*.mca", SearchOption.TopDirectoryOnly);


// var fileLoadAmount =files.Length;
var fileLoadAmount = 0;
var loadMeshFromFiles = true;
var renderMeshes = true;


// files = [@"C:\Users\zino\Downloads\Greenfield v0.5.4\Greenfield v0.5.4\region\r.-1.-1.mca"];

// var test = Path.GetFileNameWithoutExtension(files[0]);
// windowRenderer.Render();

var path = "../../../TestData/r.12.3.mca";
// string[] files = Directory.GetFiles(@"C:\Users\zino\Downloads\Greenfield v0.5.4\Greenfield v0.5.4\region", "*.mca", SearchOption.TopDirectoryOnly);

// var chunkAmount = files.Length * 1024;

HashSet<string> blockTypeSet = new HashSet<string>();
var blockNameId = Helper.GetBlockToIdFromFile("../../../TestData/blockPaletteComplete.csv");
var blockPalette = Helper.GetColorPaletteFromFile("../../../TestData/blockPaletteComplete.csv");

var meshLocation = @"J:\wosc\meshes";
string[] mFiles = Directory.GetFiles(meshLocation, "*.mesh", SearchOption.TopDirectoryOnly);

var setA = new HashSet<string>(
    files.Select(Path.GetFileNameWithoutExtension),
    StringComparer.OrdinalIgnoreCase);

var setB = new HashSet<string>(
    mFiles.Select(Path.GetFileNameWithoutExtension),
    StringComparer.OrdinalIgnoreCase);

var same =  setA.SetEquals(setB);

var missing = setA.Where(path => !setB.Contains(Path.GetFileName(path)))
    .ToArray();

// Chunk[] data = new Chunk[1024]; //amount of chunks per .mca file are 32X32
List<Chunk> data = new List<Chunk>();
Dictionary<(int x, int z), Chunk> chunks =  new Dictionary<(int x, int z), Chunk>();

var meshes = new List<Mesh>();
// var meshReader = new RegionMeshIO();
// var meshReader = new RegionMeshIOCompressed();
// var meshReader = new RegionMeshIOGzip();
var meshReader = new MeshIOCompressed();


if (loadMeshFromFiles)
{
    Console.WriteLine("Loading meshes...");
    // string[] meshFiles = Directory.GetFiles($"../../../Meshes", "*.mesh", SearchOption.TopDirectoryOnly);
    string[] meshFiles = Directory.GetFiles(meshLocation, "*.mesh", SearchOption.TopDirectoryOnly);

    // meshReader.LoadFile(@"../../../Meshes/r.0.0.mesh");
    //
    // Mesh? mesh = meshReader.LoadChunk(0, 0);
    // if (mesh != null) meshes.Add(mesh);
    for (var f = 0; f < fileLoadAmount && f < meshFiles.Length; f++)
    {
        meshReader.LoadFile(meshFiles[f]);
        // Mesh? mesh = meshReader.LoadChunk(0, 0);
        // if (mesh != null) meshes.Add(mesh);
        for (int i = 0; i < 32; i++)
        {
            for (int j = 0; j < 32; j++)
            {
                Mesh? mesh = meshReader.LoadChunk(i, j);
                if (mesh != null) meshes.Add(mesh);
            }
        }
    }
    Console.WriteLine("Finish loading meshes...");

}
else
{
    for (int j = 0; j < fileLoadAmount && j < files.Length; j++)
    {
        FileProcessor.ProcessFile(files[j], blockNameId,j, meshLocation);
        Console.WriteLine($"File {j} of {fileLoadAmount} has been processed.");
    }
    // Console.WriteLine("Start loading nbt from files");
    // for (int j = 0; j < fileLoadAmount; j++)
    // {
    //     path = files[j];
    //     // Console.WriteLine($"Loading file: {path}");
    //     byte[] chunkData = null;
    //     int foundX = -1, foundZ = -1;
    //
    //     int chunkCounter = 0;
    //
    //     for (int x = 0; x < 32; x++)
    //     {
    //         for (int z = 0; z < 32; z++)
    //         {
    //             chunkData = MCAReader.ReadChunk(path, x, z);
    //
    //             if (chunkData != null)
    //             {
    //                 if (x > 32 || z > 32)
    //                 {
    //                     throw new IndexOutOfRangeException();
    //                 }
    //
    //                 foundX = x;
    //                 foundZ = z;
    //                 // Console.WriteLine($"Using chunk {x},{z}");
    //                 var nbtData = NbtParser.LoadChunkNBT(chunkData);
    //                 // data.Add(NbtDecoder.DecodeChunk(nbtData/*, out HashSet<string> blockTypes*/));
    //                 var chunk = NbtDecoder.DecodeChunk(nbtData);
    //                 chunks[(chunk.X, chunk.Z)] = chunk;
    //                 
    //                 data.Add(chunk);
    //                 chunkCounter++;
    //                 
    //             }
    //         }
    //     }
    //
    //     if (chunkData == null)
    //     {
    //         // Console.WriteLine("No chunks found in this region!");
    //         // return;
    //     }
    //     // Console.WriteLine($"Finished file {files[j]}");
    // }
    // Console.WriteLine("All chunks read from files");
    //
    //
    // Console.WriteLine("Start building meshes");
    // foreach (var kv in chunks)
    // {
    //     meshes.Add(MeshBuilder.BuildMesh(kv.Value, blockNameId));
    // }
    // Console.WriteLine("Total meshes: " + meshes.Count);
    //
    // //todo change lod creation
    // var lods = new Mesh[meshes.Count];
    // // foreach (var mesh in meshes)
    // // {
    // //     // lods[index] = mesh.GenerateLOD(0.5f);
    // //     mesh.Weld();
    // //     // mesh.Simplify(0.01f,1e-2f);
    // //     // mesh.Simplify(0.5f,1f);
    // //     // mesh.SimplifySloppy(0.3f,0.5f);
    // //
    // //
    // //     index++;
    // // }
    //
    // // meshes = null;
    //
    //
    // //todo write mesh 883 to file
    // //compare with nbt data for chunk 883
    // // File.WriteAllText("../../../TestData/mesh883.txt",meshes[883].ToString());
    // // File.WriteAllText("../../../TestData/nbtData883.csv", data[883].toCsv());
    // Console.WriteLine("All meshes built");
    //
    // Console.WriteLine("Save meshes to files");
    // var index = 0;
    // var fileIndex = 0;
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
    //     // RegionMeshIO.Write($"../../../Meshes/r.{fileIndex}.mesh", regionChunks);
    //     // RegionMeshIOCompressed.Write($"../../../Meshes/r.{fileIndex}.mesh", regionChunks);
    //     // RegionMeshIOGzip.Write($"../../../Meshes/r.{fileIndex}.mesh", regionChunks);
    //
    //     MeshIOCompressed.Write(meshLocation + $"/r.{fileIndex}.mesh", regionChunks);
    //
    //
    //     fileIndex++;
    // }
    //
    // // regionChunks.Add((0, 0), meshes[0]);
    // // RegionMeshIO.Write(@"../../../Meshes/r.0.0.mesh", regionChunks);
}





// windowRenderer.BlockIds = blockNameId;
windowRenderer.ColorPalette = blockPalette;
if (renderMeshes)
{
    // windowRenderer.Render(meshes.ToArray());
    windowRenderer.RenderStreamed(meshLocation);
}
// windowRenderer.Render(lods.ToArray());


