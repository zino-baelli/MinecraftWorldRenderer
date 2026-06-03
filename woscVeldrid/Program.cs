using Meshoptimizer;
using woscVeldrid;
using woscVeldrid.Shader;

var windowRenderer = new AppWindow(1920, 1080, "WoSc");
string[] files = Directory.GetFiles(@"C:\Users\zino\Downloads\Greenfield v0.5.4\Greenfield v0.5.4\region", "*.mca", SearchOption.TopDirectoryOnly);


//for creating meshes
// var fileLoadAmount =files.Length;
// var loadMeshFromFiles = false;
// var renderMeshes = false;

//for rendering
var fileLoadAmount = 0;
var loadMeshFromFiles = true;
var renderMeshes = true;


// files = [@"C:\Users\zino\Downloads\Greenfield v0.5.4\Greenfield v0.5.4\region\r.-1.-1.mca"];


var lodBuilder = new LodBuilder(
    error: 0.1f,
    ratio: 0.15f,
    maxError: 2.0f,
    maxRatio: 0.85f
    );

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
        FileProcessor.ProcessFile(files[j], blockNameId, j, meshLocation, ref lodBuilder);
        Console.WriteLine($"File {j} of {fileLoadAmount} has been processed.");
    }
}




// windowRenderer.BlockIds = blockNameId;
windowRenderer.ColorPalette = blockPalette;
if (renderMeshes)
{
    // windowRenderer.Render(meshes.ToArray());
    meshLocation = meshLocation + "/lod0";
    windowRenderer.RenderStreamed(meshLocation);
}
// windowRenderer.Render(lods.ToArray());


