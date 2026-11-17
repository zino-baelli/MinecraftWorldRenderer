using fNbt;
using System;

public static class NbtDebugger
{
    public static void PrintTag(NbtTag tag, int indent = 0)
    {
        string indentStr = new string(' ', indent * 2);

        Console.WriteLine($"{indentStr}{tag.TagType}: {tag.Name}");

        switch (tag)
        {
            case NbtCompound compound:
                foreach (var child in compound.Tags)
                {
                    PrintTag(child, indent + 1);
                }
                break;

            case NbtList list:
                int i = 0;
                foreach (var item in list)
                {
                    Console.WriteLine($"{indentStr}  [{i}]");
                    PrintTag(item, indent + 2);
                    i++;
                }
                break;
        }
    }
}