using System.Numerics;

namespace woscVeldrid;

public class Mesh
{
    public List<Vertex> Vertices = new();
    public List<uint> Indices = new();
    public List<int> colorIds = new();
}