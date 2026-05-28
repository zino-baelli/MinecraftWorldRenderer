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
    
    // public Mesh GenerateLOD(float ratio)
    // {
    //     int vertexCount = this.Vertices.Count;
    //     int indexCount = this.Indices.Count;
    //
    //     // Flatten positions
    //     float[] positions = new float[vertexCount * 3];
    //     for (int i = 0; i < vertexCount; i++)
    //     {
    //         positions[i * 3 + 0] = this.Vertices[i].Position.X;
    //         positions[i * 3 + 1] = this.Vertices[i].Position.Y;
    //         positions[i * 3 + 2] = this.Vertices[i].Position.Z;
    //     }
    //
    //     uint[] indices = this.Indices.ToArray();
    //
    //     int targetIndexCount = (int)(indexCount * ratio);
    //     uint[] destination = new uint[indices.Length];
    //
    //     float error;
    //
    //     var resultCount = (int)MeshoptNative.meshopt_simplify_wrapper(
    //         destination,
    //         indices,
    //         (UIntPtr)indexCount,
    //         positions,
    //         (UIntPtr)vertexCount,
    //         (UIntPtr)(sizeof(float) * 3),
    //         (UIntPtr)targetIndexCount,
    //         1e-2f,
    //         0,
    //         out error
    //     );
    //
    //     // Trim result
    //     Array.Resize(ref destination, resultCount);
    //
    //     return new Mesh
    //     {
    //         Vertices = this.Vertices, // reuse
    //         Indices = new List<uint>(destination),
    //         colorIds = this.colorIds
    //     };
    // }
    
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