using System.Numerics;
using System.Runtime.InteropServices;

namespace woscVeldrid;

// [StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public int ColorId;
    // public Vector2 UV;

    public Vertex(Vector3 pos, Vector3 normal, int colorId)
    {
        Position = pos;
        Normal = normal;
        ColorId = colorId;
        // UV = uv;
    }
}