
namespace NetWork
{
    public class MainServer
    {
        static void Main(string[] args)
        {
            NetWorkSystem.Start(8888, 100);
            Console.ReadKey();
        }
    }
}
