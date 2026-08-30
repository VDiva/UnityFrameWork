using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork
{
    [CustomEditor(typeof(AnimationController))]
    public class AnimationControllerEditor : UnityEditor.Editor
    {
        private Object unityAsset;
        private UnityEditor.Editor assetEditor;
        private bool isAnimationClip;
        private List<string> _keys;
        private List<AnimationClip> _values;
        private AnimationController _animationController;
        public int index = 0;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            CheckAsset();
            ShowPreview();
        }

        private void OnEnable()
        {

            if (_animationController==null)
            {
                _animationController=target as AnimationController;
            }

            if (_keys==null)
            {
                _keys = new List<string>();
            }

            if (_values==null)
            {
                _values = new List<AnimationClip>();
            }
            
            _keys.Clear();
            _values.Clear();
            
            if (_animationController!=null&& _animationController.animLayer!=null)
            {
                foreach (var item in _animationController.animLayer)
                {
                    foreach (var anim in item.AnimData)
                    {
                        if (anim!=null)
                        {
                            _keys.Add(anim.name);
                            _values.Add(anim);
                        }
                    }
                }
            }
            
        }
        
        private void CheckAsset()
        {
            
            EditorGUI.BeginChangeCheck(); //开始检查编辑器资源是否发生变动
            
            //unityAsset = EditorGUILayout.ObjectField(unityAsset, typeof(Object), true); //显示Object
            //EditorGUILayout.TextField(m_itemString);
            
            index = EditorGUILayout.Popup("动画:",index, _keys.ToArray());
            
            if (GUI.changed) //如果发生变动（比如拖拽新的资源）
            {
                unityAsset = _values[index];
                //MyLog.Log("打印");
                isAnimationClip = false;
                // var go = unityAsset as GameObject; //如果是物体
                // if (go != null)
                // {
                //     assetEditor = Editor.CreateEditor(go); //创建新的Editor
                //     assetEditor.OnInspectorGUI(); //Editor初始化
                //     return;
                // }
                DestroyImmediate(assetEditor);
                var clip = unityAsset as AnimationClip; //如果是动画片
                if (clip != null)
                {
                    assetEditor = UnityEditor.Editor.CreateEditor(clip);
                    assetEditor.OnInspectorGUI();
                    isAnimationClip = true;
                    return;
                }
                // var mesh = unityAsset as Mesh; //如果是网格
                // if (mesh != null)
                // {
                //     assetEditor = Editor.CreateEditor(mesh);
                //     assetEditor.OnInspectorGUI();
                //     return;
                // }
                // var tex = unityAsset as Texture; //如果是纹理资源
                // if (tex != null)
                // {
                //     assetEditor = Editor.CreateEditor(tex);
                //     assetEditor.OnInspectorGUI();
                //     return;
                // }
                // var mat = unityAsset as Material;
                // if (mat != null)
                // {
                //     assetEditor = Editor.CreateEditor(mat);
                //     assetEditor.OnInspectorGUI();
                //     return;
                // }
            }
        }
        private void ShowPreview()
        {
            if (assetEditor != null) //如果资源Editor非空
            {
                using (new EditorGUILayout.HorizontalScope()) //水平布局
                {
                    GUILayout.FlexibleSpace();//填充间隔
                    assetEditor.OnPreviewSettings(); //显示预览设置项
                }
                if (isAnimationClip)
                {
                    AnimationMode.StartAnimationMode(); //为了播放正常播放预览动画而进行的设置
                    AnimationMode.BeginSampling();
                    assetEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(150, 250), EditorStyles.whiteLabel);
                    AnimationMode.EndSampling();
                    AnimationMode.StopAnimationMode();
                }
                else
                {
                    assetEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(150, 250), EditorStyles.whiteLabel);
                }
            }
        }
    }
}