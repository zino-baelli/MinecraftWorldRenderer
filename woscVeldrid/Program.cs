using woscVeldrid;
using woscVeldrid.Shader;

var windowRenderer = new AppWindow(1920, 1080, "WoSc");

// windowRenderer.Render();

var path = "../../../TestData/r.12.3.mca";

byte[] chunkData = null;
int foundX = -1, foundZ = -1;

for (int z = 0; z < 32; z++)
{
    for (int x = 0; x < 32; x++)
    {
        chunkData = MCAReader.ReadChunk(path, x, z);

        if (chunkData != null)
        {
            foundX = x;
            foundZ = z;
            Console.WriteLine($"Using chunk {x},{z}");
            break;
        }
    }
    if (chunkData != null) break;
}

if (chunkData == null)
{
    Console.WriteLine("No chunks found in this region!");
    return;
}

var nbtData = NbtParser.LoadChunkNBT(chunkData);
NbtDebugger.PrintTag(nbtData);
var decodedChunk = NbtDecoder.DecodeChunk(nbtData);
var i = 1;