using UnityEngine;

namespace FrameWork
{
    public class Main
    {
        public static void Run()
        {
            MyLog.Log("你好啊啊 仄仄仄仄仄仄仄仄仄仄仄仄仄仄仄仄仄仄中");
            var cube = new CsCube();
            cube.TransformCsCube.position = new Vector3(1, 0, 0);
            var sphere = new CsSphere();
            sphere.TransformCsSphere.position = new Vector3(-1, 0, 0);

            for (int i = 0; i < 10; i++)
            {
                var s = new CsSphere();
                s.TransformCsSphere.position = new Vector3(3, (float)i, 0);
            }
            
        }
        
    }
}