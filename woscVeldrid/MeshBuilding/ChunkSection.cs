namespace woscVeldrid;

public class ChunkSection
{
    public int Y; // section Y (not world Y)
    public Block[,,] Blocks = new Block[16, 16, 16];
}
