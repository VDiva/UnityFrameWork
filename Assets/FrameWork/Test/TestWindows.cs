using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace FrameWork.Test
{
    public class TestWindows : OdinEditorWindow
    {
        [MenuItem("FrameWork/打开")]
        private static void OpenWindow()
        {
            GetWindow<TestWindows>().Show();
        }
        
        [HorizontalGroup, InlineEditor(InlineEditorModes.LargePreview)]
        public VideoClip Preview;
    }
}