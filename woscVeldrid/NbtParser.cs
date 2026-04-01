using fNbt;

namespace woscVeldrid.Shader;

public class NbtParser
{
    public static NbtCompound LoadChunkNBT(byte[] data)
    {
        var file = new NbtFile();
        file.LoadFromBuffer(data, 0, data.Length, NbtCompression.None);
        return file.RootTag;
    }
    
}