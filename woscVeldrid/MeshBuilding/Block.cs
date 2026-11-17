namespace woscVeldrid;

public class Block(string blockName = "")
{
    public string Name = blockName;
    public Dictionary<string, string>? Properties;
}