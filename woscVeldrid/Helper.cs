using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace woscVeldrid;

public class Helper
{
    public static void WriteBlockTypesToFile(List<string> blockTypes)
    {
        StringBuilder builder = new();
        foreach (var blockType in blockTypes)
        {
            builder.Append(blockType);
            builder.Append(",\n");
        }
        builder.Remove(builder.Length - 2, 2);
        File.WriteAllText(@"C:\Users\zino\Desktop\renderDocCaptures\blockTypes.csv", builder.ToString());
    }

    public static Dictionary<string, int> GetBlockToIdFromFile(string path)
    {
        Dictionary<string, int> palette = new();
        var input = File.ReadAllLines(path);
        foreach (var s in input)
        {
            var block = s.Split(';');
            palette.Add(block[0],int.Parse(block[1]));
        }
        return palette;
    }
    
    public static Dictionary<int, Vector3> GetColorPaletteFromFile(string path)
    {
        Dictionary<int, Vector3> palette = new();
        var input = File.ReadAllLines(path);
        foreach (var s in input)
        {
            var block = s.Split(';');
            var hex = block[2];
            if (int.TryParse(block[2], out var result) && result  == -1)
            {
                palette.Add(int.Parse(block[1]), new Vector3(100/255f, 1000/255f, 100/255f));
            }
            else
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                palette.Add(int.Parse(block[1]), new Vector3(r/255f, g/255f, b/255f));
            }
        }
        return palette;
    }
    
    

    public static float[] Flatten(Vector3[] v)
    {
        float[] result = new float[v.Length * 3];

        for (int i = 0; i < v.Length; i++)
        {
            result[i * 3 + 0] = v[i].X;
            result[i * 3 + 1] = v[i].Y;
            result[i * 3 + 2] = v[i].Z;
        }

        return result;
    }
}