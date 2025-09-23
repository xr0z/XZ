namespace XZ;

internal static class Mod
{
    internal static string Input(string text)
    {
        Console.Write(text + " > ");
        return Console.ReadLine();
    }
    internal static int InputInt(string text)
    {
        Console.Write(text + " > ");
        string raw = Console.ReadLine();
        try
        {
            return int.Parse(raw);
        }
        catch
        {
            Console.WriteLine("");
            return 0;
        }
    }
}