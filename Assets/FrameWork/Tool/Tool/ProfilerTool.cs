using System.Text;
using FrameWork;
using Unity.Profiling;
using UnityEngine;

namespace ScriptCode.Tool
{
    public class ProfilerTool : MonoBehaviour
    {
        string statsText;
        ProfilerRecorder totalReservedMemoryRecorder;
        ProfilerRecorder gcReservedMemoryRecorder;
        ProfilerRecorder systemUsedMemoryRecorder;

        
        float _updateInterval = 1f;//设定更新帧率的时间间隔为1秒  
        float _accum = .0f;//累积时间  
        int _frames = 0;//在_updateInterval时间内运行了多少帧  
        float _timeLeft;
        string fpsFormat;
        
        void OnEnable()
        {
            totalReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            gcReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
            systemUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        }

        void OnDisable()
        {
            totalReservedMemoryRecorder.Dispose();
            gcReservedMemoryRecorder.Dispose();
            systemUsedMemoryRecorder.Dispose();
        }

        void Update()
        {
            
            _timeLeft -= Time.deltaTime;
            //Time.timeScale可以控制Update 和LateUpdate 的执行速度,  
            //Time.deltaTime是以秒计算，完成最后一帧的时间  
            //相除即可得到相应的一帧所用的时间  
            _accum += Time.timeScale / Time.deltaTime;
            ++_frames;//帧数  

            if (_timeLeft <= 0)
            {
                float fps = _accum / _frames;
                //Debug.Log(_accum + "__" + _frames);  
                fpsFormat = System.String.Format("{0:F2}FPS", fps);//保留两位小数  
                _timeLeft = _updateInterval;
                _accum = .0f;
                _frames = 0;
            }
            var sb = new StringBuilder(500);
            if (totalReservedMemoryRecorder.Valid)
                sb.AppendLine($"总预留内存: {DownLoad.GetFileSize(totalReservedMemoryRecorder.LastValue)}");
            if (gcReservedMemoryRecorder.Valid)
                sb.AppendLine($"GC预留内存: {DownLoad.GetFileSize(gcReservedMemoryRecorder.LastValue)}");
            if (systemUsedMemoryRecorder.Valid)
                sb.AppendLine($"系统使用内存: {DownLoad.GetFileSize(systemUsedMemoryRecorder.LastValue)}");
            sb.AppendLine($"FPS:{fpsFormat}");
            statsText = sb.ToString();
        }

        void OnGUI()
        {
            GUIStyle fontStyle = new GUIStyle();
            fontStyle.normal.background = null;
            fontStyle.normal.textColor = new Color(1, 0, 0);
            fontStyle.fontSize = 20;
            GUI.Label(new Rect(10, 30, 250, 50), statsText,fontStyle);
        }
    }
}