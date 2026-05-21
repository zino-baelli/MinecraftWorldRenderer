using System.Numerics;
using System.Text;

namespace woscVeldrid;

public class Mesh
{
    public List<Vertex> Vertices = new();
    public List<uint> Indices = new();
    public List<int> colorIds = new();

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("vertices:");
        foreach (var vertex in Vertices)
        {
            builder.Append($"{vertex.Position.X},{vertex.Position.Y},{vertex.Position.Z};");
        }
        builder.Append("indices:");
        foreach (var index in Indices)
        {
            builder.Append($"{index},");
        }
        return builder.ToString();
    }
}