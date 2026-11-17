namespace woscVeldrid;

public static class ChunkCoords
{
    public const int REGION_SIZE = 32;

    // world chunk -> region coordinate
    public static (int rx, int rz) ToRegion(int cx, int cz)
    {
        return (
            FloorDiv(cx, REGION_SIZE),
            FloorDiv(cz, REGION_SIZE)
        );
    }

    // world chunk -> local in region (0..31)
    public static (int lx, int lz) ToLocal(int cx, int cz)
    {
        return (
            Mod(cx, REGION_SIZE),
            Mod(cz, REGION_SIZE)
        );
    }

    private static int FloorDiv(int v, int size)
    {
        return (int)Math.Floor((double)v / size);
    }

    private static int Mod(int v, int size)
    {
        int r = v % size;
        return r < 0 ? r + size : r;
    }
}