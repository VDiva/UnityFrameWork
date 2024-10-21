using Riptide;

namespace Test;

public class Test
{
    static void Main()
    {
        NetWork.SatrtServer(8888,100);
        Console.WriteLine("----------------------------------");
        Console.ReadKey();
    }
}