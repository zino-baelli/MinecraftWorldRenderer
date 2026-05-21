using System.Text;

namespace woscVeldrid;

public class Chunk
{
    public Chunk(){}
    public Chunk(int x, int z, ChunkSection[] section)
    {
        X = x;
        Z = z;
        Sections = section;
    }
    
    public int X;
    public int Z;
    public ChunkSection[] Sections = new ChunkSection[24];

    public String toCsv()
    {
        var builder = new StringBuilder();
        builder.Append("X,Z,");
        for (int i = 0; i < Sections.Length; i++)
        {
            builder.Append($"Section{i} Y,");
            for (int j = 0; j < Sections[i].Blocks.Length; j++)
            {
                builder.Append($"Section{i} Block{j} Name,");
            }
        }
        builder.Append("\n");
        
        foreach (var section in Sections)
        {
            builder.Append($"{X},{Z},");
            builder.Append($"{section.Y}");
            foreach (var block in section.Blocks)
            {
                builder.Append(block.Name+",");
            }
            builder.Append("\n");
        }
        
        return builder.ToString();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        
        return base.ToString();
    }
}