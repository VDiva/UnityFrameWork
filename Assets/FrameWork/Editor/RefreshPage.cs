using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    public class RefreshPage: UnityEditor.Editor
    {
        [MenuItem("FrameWork/Refresh")]
        public static void Refresh()
        {
            AssetDatabase.Refresh();
        }
    }
}