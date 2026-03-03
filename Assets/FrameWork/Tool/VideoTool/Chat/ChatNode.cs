using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using XNode;

namespace FrameWork.ChatTool
{

    [Serializable]
    public class ChatData
    {
        [LabelText("添加信息的对象")]
        public string targetId;
        [LabelText("需要添加的消息数据")]
        public ChatGraph chatNode;
    }


    [Serializable]
    public class MessageData
    {
        public string assetName;
        public ChatMsgType msgType=ChatMsgType.UnRead;
    }
    
    public enum ChatMsgType
    {
        UnRead,
        Read
    }

    public enum MType
    {
        [LabelText("消息")]Text,
        [LabelText("图片")]Image,
        [LabelText("视频")]Video
    }
    
    [NodeWidth(300)]
    [CreateNodeMenu("Chat/ChatNode")]
    public class ChatNode : Node
    {
        [Input]
        public ChatNode input;
        
        [Output]
        public ChatNode outPut;
        
        // [LabelText("消息对象id")]
        // public Xlsx_Message_Key targetId;
        
        [LabelText("消息类型")]
        public MType mType;


        [LabelText("消息头像(不指定默认使用消息头像)")] 
        public Sprite icon;
        
        [LabelText("是否是自己的消息")]
        public bool isSelfMsg;
        
        [ShowIf("mType",MType.Text)]
        [LabelText("消息备注")]
        public string msgTip="备注";
        [ShowIf("mType",MType.Text)]
        [LabelText("消息内容")]
        public string  msg;

        [ShowIf("mType", MType.Image)] 
        public Sprite img;


        [ShowIf("mType", MType.Video)]
        [LabelText("视频封面")]
        public Sprite fenMian;

#if UNITY_EDITOR
        [OnValueChanged("VideoChange")]
        [ShowIf("mType", MType.Video)]
        [LabelText("视频")]
        public VideoClip videoClip;
#endif
        
        
        [ShowIf("mType", MType.Video)]
        [LabelText("视频地址")]
        [ReadOnly]
        public string videoClipPath;
        
        [HideInInspector] 
        public ChatNode selectNode;
        
        [LabelText("是否含有选项")]
        public bool isHasBtn;
        
        [ShowIf("isHasBtn")]
        [Output]
        public ChatNode selectBtnNode;

        [LabelText("是否是选项")]
        public bool isBtnInfo;
        
        [ShowIf("isBtnInfo")]
        [LabelText("选项名称备注")] 
        public string selectNameTip="选项备注";
        
        [ShowIf("isBtnInfo")]
        [LabelText("选项名称")] 
        public string selectName;

        [ShowIf("isBtnInfo")]
        [LabelText("点击前置条件")]
        public List<PropertyData> fistPropertyDatas=new List<PropertyData>();

        [ShowIf("isBtnInfo")]
        [LabelText("条件失败弹窗文字")]
        public string languageKey;
        
        [ShowIf("isBtnInfo")]
        [LabelText("选项点击后添加属性")]
        public List<PropertyData> propertyDatas=new List<PropertyData>();

        [LabelText("延迟时间")]
        public float delay=0.3f;
        
        
        [LabelText("触发短信后添加任务数据")]
        public List<TaskData> taskData;
        private void VideoChange()
        {
#if UNITY_EDITOR
            if (!videoClip)return;
            videoClipPath=AssetDatabase.GetAssetPath(videoClip);
            videoClipPath=Path.GetDirectoryName(videoClipPath).Replace("\\","/")+"/"+Path.GetFileNameWithoutExtension(videoClipPath);
            videoClipPath = videoClipPath.Split("Video")[1];
            //videName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(videoClip));
#endif
		
        }
    }
    
    
    
    
    
}