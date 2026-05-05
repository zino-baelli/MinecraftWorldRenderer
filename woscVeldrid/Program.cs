using woscVeldrid;
using woscVeldrid.Shader;

var windowRenderer = new AppWindow(1920, 1080, "WoSc");

// windowRenderer.Render();

var path = "../../../TestData/r.12.3.mca";

byte[] chunkData = null;
Chunk[] data = new Chunk[1024]; //amount of chunks per .mca file are 32X32
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
            Console.WriteLine($"Using chunk {x},{z}");
            var nbtData = NbtParser.LoadChunkNBT(chunkData);
            data[z + 32  * x] = NbtDecoder.DecodeChunk(nbtData);
            // data[z + 32  * x] = new Chunk(foundX, foundZ, NbtDecoder.DecodeChunk(nbtData).ToArray());
            // data[chunkCounter] = new Chunk(foundX, foundZ, NbtDecoder.DecodeChunk(nbtData).ToArray());
            chunkCounter++;

            // break;
        }
    }
    // if (chunkData != null) break;
}

if (chunkData == null)
{
    Console.WriteLine("No chunks found in this region!");
    return;
}

// var nbtData = NbtParser.LoadChunkNBT(chunkData);
// NbtDebugger.PrintTag(nbtData);
// var decodedChunk = NbtDecoder.DecodeChunk(nbtData);

var mesh = MeshBuilder.BuildMesh(data[0]);

windowRenderer.Render(mesh);

var i = 1;