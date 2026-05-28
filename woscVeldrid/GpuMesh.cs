using Veldrid;

namespace woscVeldrid;

public class GpuMesh
{
    public DeviceBuffer VertexBuffer;
    public DeviceBuffer IndexBuffer;
    public uint IndexCount;
    public uint VertexCount;
}