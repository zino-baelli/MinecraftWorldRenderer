namespace woscVeldrid;

using System.Numerics;
using System.Collections.Generic;


public static class MeshBuilder
{
    private static readonly Vector3[] FaceNormals =
    {
        new(1, 0, 0),   // Right
        new(-1, 0, 0),  // Left
        new(0, 1, 0),   // Top
        new(0, -1, 0),  // Bottom
        new(0, 0, 1),   // Front
        new(0, 0, -1)   // Back
    };

    private static readonly Vector3[][] FaceVertices =
    {
        // Right
        // new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
        new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(1,1,0), new Vector3(1,0,0) },
        // Left
        // new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
        new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(0,0,1) },
        // Top
        new[] { new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1) },
        // Bottom
        // new[] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) },
        new[] { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,0,0) },
        // Front
        // new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
        new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,0,1) },
        // Back
        // new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) }
        new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(0,1,0), new Vector3(0,0,0) }
    };

    private static readonly Vector2[] UVs =
    {
        new(0,0), new(0,1), new(1,1), new(1,0)
    };

    public static Mesh BuildMesh(Chunk chunk, Dictionary<string,int> idDict)
    {
        var mesh = new Mesh();

        // Build quick lookup by section Y
        var sectionDict = new Dictionary<int, ChunkSection>();
        foreach (var s in chunk.Sections)
            sectionDict[s.Y] = s;

        foreach (var section in chunk.Sections)
        {
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
            for (int z = 0; z < 16; z++)
            {
                var block = section.Blocks[x, y, z];
                if (IsAir(block)) continue;

                for (int face = 0; face < 6; face++)
                {
                    var neighbor = GetNeighbor(sectionDict, section.Y, x, y, z, face);
                    if (!IsAir(neighbor)) continue;

                    var blockId = 0;
                    if (idDict.TryGetValue(block.Name, out var id))
                    {
                        blockId = id;
                    }
                    AddFace(mesh, chunk.X, chunk.Z, section.Y, x, y, z, face, blockId);
                }
            }
        }

        return mesh;
    }

    private static bool IsAir(Block b)
    {
        return b == null || b.Name == "minecraft:air";
    }

    private static Block GetNeighbor(Dictionary<int, ChunkSection> sections, int sectionY, int x, int y, int z, int face)
    {
        int nx = x, ny = y, nz = z;
        int nSectionY = sectionY;

        switch (face)
        {
            case 0: nx++; break; // right
            case 1: nx--; break; // left
            case 2: ny++; break; // top
            case 3: ny--; break; // bottom
            case 4: nz++; break; // front
            case 5: nz--; break; // back
        }

        // Handle vertical section crossing
        if (ny < 0)
        {
            nSectionY--;
            ny = 15;
        }
        else if (ny > 15)
        {
            nSectionY++;
            ny = 0;
        }

        if (!sections.TryGetValue(nSectionY, out var section))
            return null;

        if (nx < 0 || nx > 15 || nz < 0 || nz > 15)
            return null; // chunk boundary (you'll improve this later)

        return section.Blocks[nx, ny, nz];
    }

    private static void AddFace(Mesh mesh, int chunkX, int chunkZ, int sectionY, int x, int y, int z, int face, int colorId)
    {
        uint startIndex = (uint)mesh.Vertices.Count;

        // float worldX = chunkX * 16 + x;
        // float worldY = sectionY * 16 + y;
        // float worldZ = chunkZ * 16 + z;
        
        float worldX = chunkX * 16 + x;
        float worldY = sectionY * 16 + y;
        float worldZ = chunkZ * 16 + z;
        
        // float worldZ = -(chunkZ * 16 + z);

        for (int i = 0; i < 4; i++)
        {
            var pos = FaceVertices[face][i];
            var vertex = new Vertex(
                new Vector3(worldX, worldY, worldZ) + pos,
                FaceNormals[face],
                UVs[i]
            );

            mesh.Vertices.Add(vertex);
            mesh.colorIds.Add(colorId);
        }

        // two triangles
        mesh.Indices.Add(startIndex + 0);
        mesh.Indices.Add(startIndex + 1);
        mesh.Indices.Add(startIndex + 2);

        mesh.Indices.Add(startIndex + 0);
        mesh.Indices.Add(startIndex + 2);
        mesh.Indices.Add(startIndex + 3);
    }
}