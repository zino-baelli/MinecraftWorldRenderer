using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace woscVeldrid;

public class Mesh
{
    // public List<Vertex> Vertices = new(25000);
    // public List<uint> Indices = new(35000);
    // public List<int> ColorIds = new(25000);
    
    public Vertex[] Vertices;
    public uint[] Indices;
    public int[] ColorIds;


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
    
    public Mesh Clone()
    {
        var mesh = new Mesh();

        mesh.Vertices = new Vertex[Vertices.Length];
        Array.Copy(Vertices, mesh.Vertices, Vertices.Length);

        mesh.Indices = new uint[Indices.Length];
        Array.Copy(Indices, mesh.Indices, Indices.Length);

        mesh.ColorIds = new int[ColorIds.Length];
        Array.Copy(ColorIds, mesh.ColorIds, ColorIds.Length);

        return mesh;
    }
    
    public void Simplify(
        // uint[] indices,
        // Vector3[] positions,
        float ratio,
        float target_error)
    {
        var positions = Vertices.Select(vertex => vertex.Position).ToArray() ;
        // indices = 
        
        int target = (int)(Indices.Length * ratio);

        uint[] destination = new uint[Indices.Length];


        float error;

        int result = (int)MeshoptNative.meshopt_simplify_wrapper(
            destination,
            Indices,
            (UIntPtr)Indices.Length,
            Helper.Flatten(positions),
            (UIntPtr)positions.Length,
            (UIntPtr)(sizeof(float) * 3),
            (UIntPtr)target,
            target_error,
            0,
            out error
        );

        Array.Resize(ref destination, result);
        Indices = destination;
        // return destination;
        
    }
    
    public void SimplifySloppy(float ratio, float target_error)
    {
        var positionsVec = Vertices.Select(v => v.Position).ToArray();
        float[] positions = Helper.Flatten(positionsVec);

        int target = (int)(Indices.Length * ratio);

        uint[] destination = new uint[Indices.Length];

        int result = (int)MeshoptNative.meshopt_simplifySloppy_wrapper(
            destination,
            Indices,
            (UIntPtr)Indices.Length,
            positions,
            (UIntPtr)positionsVec.Length,
            (UIntPtr)(sizeof(float) * 3),
            (UIntPtr)target,
            target_error
        );

        Array.Resize(ref destination, result);
        Indices = destination;
    }
    
    public void Weld(
        // Vertex[] vertices,
        // uint[] indices,
        // out Vertex[] newVertices,
        // out uint[] newIndices
        )
    {
        uint[] remap = new uint[Vertices.Length];

        GCHandle vHandle = GCHandle.Alloc(Vertices, GCHandleType.Pinned);

        try
        {
            int unique = (int)MeshoptNative.meshopt_generateVertexRemap_wrapper(
                remap,
                Indices,
                (UIntPtr)Indices.Length,
                vHandle.AddrOfPinnedObject(),
                (UIntPtr)Vertices.Length,
                (UIntPtr)Marshal.SizeOf<Vertex>()
            );

            var newVertices = new Vertex[unique];
            var newIndices = new uint[Indices.Length];

            GCHandle newVHandle = GCHandle.Alloc(newVertices, GCHandleType.Pinned);

            try
            {
                MeshoptNative.meshopt_remapVertexBuffer_wrapper(
                    newVHandle.AddrOfPinnedObject(),
                    vHandle.AddrOfPinnedObject(),
                    (UIntPtr)Vertices.Length,
                    (UIntPtr)Marshal.SizeOf<Vertex>(),
                    remap);

                MeshoptNative.meshopt_remapIndexBuffer_wrapper(
                    newIndices,
                    Indices,
                    (UIntPtr)Indices.Length,
                    remap);
            }
            finally
            {
                newVHandle.Free();
            }
            Vertices = newVertices;
            Indices = newIndices;
        }
        finally
        {
            vHandle.Free();
        }

       
    }
}