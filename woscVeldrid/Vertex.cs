using System.Numerics;

namespace woscVeldrid;

public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 UV;

    public Vertex(Vector3 pos, Vector3 normal, Vector2 uv)
    {
        Position = pos;
        Normal = normal;
        UV = uv;
    }
}