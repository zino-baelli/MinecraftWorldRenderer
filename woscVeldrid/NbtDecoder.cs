using fNbt;

namespace woscVeldrid;

public class NbtDecoder
{
    
    public static Chunk DecodeChunk(NbtCompound chunk/*, out HashSet<string> blockTypes*/)
    {
        // blockTypes = new HashSet<string>();
        var Chunk = new Chunk();
        var level = chunk.Get<NbtCompound>("Level");
        var xPos = level.Get<NbtInt>("xPos");
        var zPos = level.Get<NbtInt>("zPos");
        Chunk.X = xPos.Value;
        Chunk.Z = zPos.Value;
        var sections = level.Get<NbtList>("Sections");

        var result = new List<ChunkSection>();

        foreach (NbtCompound section in sections)
        {
            var decoded = DecodeSection(section/*, out List<string> types*/);
            if (decoded != null)
            {
                result.Add(decoded);
                // foreach (var blockType in types)
                // {
                //     blockTypes.Add(blockType);
                // }
            }
        }
        
        Chunk.Sections = result.ToArray();
        return Chunk;
    }
    
    private static ChunkSection? DecodeSection(NbtCompound section/*, out List<string> */)
    {
        int sectionY = section.Get<NbtByte>("Y").Value;
        // blockTypes = new List<string>();

        // If no palette, it's empty (air)
        if (!section.Contains("Palette"))
        {
            return CreateEmptySection(sectionY);
        }

        var paletteTag = section.Get<NbtList>("Palette");
        var palette = ReadPalette(paletteTag);

        // Single-value palette optimization
        if (!section.Contains("BlockStates"))
        {
            // blockTypes.Add(palette[0].Name);
            return CreateFilledSection(sectionY, palette[0]);
        }

        var blockStates = section.Get<NbtLongArray>("BlockStates").Value;

        int bitsPerBlock = Math.Max(4, (int)Math.Ceiling(Math.Log2(palette.Count)));

        var chunkSection = new ChunkSection { Y = sectionY };

        for (int i = 0; i < 4096; i++)
        {
            int paletteIndex = ExtractIndex(blockStates, i, bitsPerBlock);
            var block = new Block("invalid_Palette_id");
            if (paletteIndex < palette.Count)
            {
                block = palette[paletteIndex];
            }

            int x = i % 16;
            int z = (i / 16) % 16;
            int y = i / 256;

            chunkSection.Blocks[x, y, z] = block;
            // blockTypes.Add(block.Name);
        }

        return chunkSection;
    }
    
    private static List<Block> ReadPalette(NbtList paletteTag)
    {
        var palette = new List<Block>();

        foreach (NbtCompound entry in paletteTag)
        {
            var block = new Block
            {
                Name = entry.Get<NbtString>("Name").Value
            };

            if (entry.Contains("Properties"))
            {
                var props = entry.Get<NbtCompound>("Properties");
                block.Properties = props.Tags
                    .ToDictionary(
                        t => t.Name,
                        t => ((NbtString)t).Value
                    );
            }

            palette.Add(block);
        }

        return palette;
    }
    
    private static int ExtractIndex(long[] data, int index, int bitsPerBlock)
    {
        int bitIndex = index * bitsPerBlock;

        int longIndex = bitIndex / 64;
        int startBit = bitIndex % 64;

        ulong current = (ulong)data[longIndex];

        if (startBit + bitsPerBlock <= 64)
        {
            ulong mask = (1UL << bitsPerBlock) - 1;
            return (int)((current >> startBit) & mask);
        }
        else
        {
            ulong next = (ulong)data[longIndex + 1];

            int bitsInCurrent = 64 - startBit;
            int bitsInNext = bitsPerBlock - bitsInCurrent;

            ulong part1 = current >> startBit;
            ulong part2 = next & ((1UL << bitsInNext) - 1);

            return (int)((part2 << bitsInCurrent) | part1);
        }
    }
    
    private static ChunkSection CreateEmptySection(int y)
    {
        var section = new ChunkSection { Y = y };

        var air = new Block { Name = "minecraft:air" };

        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        for (int yy = 0; yy < 16; yy++)
            section.Blocks[x, yy, z] = air;

        return section;
    }
    
    private static ChunkSection CreateFilledSection(int y, Block block)
    {
        var section = new ChunkSection { Y = y };

        for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
                for (int yy = 0; yy < 16; yy++)
                    section.Blocks[x, yy, z] = block;

        return section;
    }
    


    
    
}