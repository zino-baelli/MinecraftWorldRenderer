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
}