using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class UiManager : SingletonAsMono<UiManager>
    {
        private Dictionary<string, UiBase> UiDic;

        private Transform CanvasTransform = null;
        
        
        public int UiCount
        {
            get { return UiDic.Count; }
        }
        
        private void Awake()
        {
            UiDic = new Dictionary<string, UiBase>();
            CanvasTransform= FindObjectOfType<Canvas>()?.transform;
            
        }

        private void Update()
        {
            if (UiCount>0)
            {
                foreach (var item in UiDic.Keys)
                {
                    if (UiDic[item]!=null)
                    {
                        UiDic[item].Update();
                    }
                }
            }
        }


        public UiBase ShowUi<T>() where T: UiBase
        {
            Type t = typeof(T);
            string fullName = t.FullName;
        
            if (UiDic.ContainsKey(fullName))
            {
                Debug.Log("当前面包已经显示了,名字:"+fullName);
                return UiDic[fullName];
            }

            if (CanvasTransform==null)
            {
                Debug.LogError("场景中没有Canvas组件,无法显示Ui物体");
                return null;
            }
            
            UiBase uiBase = Activator.CreateInstance(t) as UiBase;
            
            GameObject prefab = AssetBundlesLoad.LoadAsset<GameObject>(GlobalVariables.Configure.AbModePrefabName, fullName);
            if (prefab==null)
            {
                Debug.LogError("找不到需要生成的预制体");
                return null;
            }
            
            GameObject go = Instantiate(prefab,CanvasTransform);
            uiBase.UiGameObject = go;
            uiBase.Start();
            UiDic.Add(fullName,uiBase);
            return uiBase;
        }

        public UiBase RemoveUi<T>() where T : UiBase
        {
            string fullName = typeof(T).FullName;
            if (UiDic.TryGetValue(fullName,out UiBase uiBase))
            {
                uiBase.Destroy();
                UiDic.Remove(fullName);
                GameObject.Destroy(uiBase.UiGameObject);
                return uiBase;
            }

            return null;
        }
        
        
        public void ClearAllPanel()
        {
            foreach (var key in UiDic.Keys)
            {
                UiBase uIBase = UiDic[key];
                if (uIBase != null)
                {
                    uIBase.Destroy();
                    GameObject.Destroy(uIBase.UiGameObject);
                }
            }
 
            UiDic.Clear();
        }

    }
}