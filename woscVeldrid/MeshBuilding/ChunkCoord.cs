public struct ChunkCoord : IEquatable<ChunkCoord>
{
    public int X;
    public int Z;

    public ChunkCoord(int x, int z)
    {
        X = x;
        Z = z;
    }

    public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;

    public override int GetHashCode() => HashCode.Combine(X, Z);
}