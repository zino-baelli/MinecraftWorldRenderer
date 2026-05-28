using System.Numerics;
using System.Runtime.InteropServices;

namespace woscVeldrid;

// [StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    // public Vector2 UV;

    public Vertex(Vector3 pos, Vector3 normal)
    {
        Position = pos;
        Normal = normal;
        // UV = uv;
    }
}