namespace FrameWork
{
    public class Cs
    {
        public static void Run()
        {
            MyLog.Log("进入Run");
            var obj=new CsSphere();
            MyLog.Log("生成了："+obj.ToString());
        }
    }
}