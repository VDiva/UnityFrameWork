using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FrameWork;
using FrameWork.ChatTool;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using XNode;
using Object = System.Object;

public enum PropertyType
{
	[LabelText("一个值")]Value,
	[LabelText("道具")]Item,
	[LabelText("属性")]Property,
	[LabelText("其他属性")]Other
}

public enum ItemType
{
	
}

public enum PropertyTypeValue
{
	
}

public enum OtherType
{
	[LabelText("回合")]Round
}

[Serializable]
public class VideoAudioTextData
{
	public TextAsset textAsset;
	public bool isRoleText;
}

[Serializable]
public class EventData
{
	public float evenTime;
	public string eventName;
	public string eventValue;
	[HideInInspector]
	public bool isEx;

	public bool isReturn;
}


public enum QteType
{
	[LabelText("空的")]NoneGo,
	[LabelText("缩圈")]CondensationGo,
	[LabelText("按键")]KeyGo,
	[LabelText("滑动")]SlideGo
}

[Serializable]
public class QteData
{
	[LabelText("qte类型")]
	public QteType qteType;
	[LabelText("qte开始时间")]
	public float qteStartTime;
	[LabelText("qte结束时间")]
	public float qteEndTime;
	[HideInInspector]
	public bool isSpawnEx;
	
	[LabelText("qte坐标")]
	public Vector2 qteLoc;
	
	[LabelText("编辑器显示的按钮名称")]
	public string qteName;
	
	
	[LabelText("成功添加属性")]
	public List<PropertyData>  sucPropertyData=new List<PropertyData>();
	
	[LabelText("失败添加属性")]
	public List<PropertyData>  losePropertyData=new List<PropertyData>();

	[HideInInspector]
	public List<PropertyData> sumProperty=new List<PropertyData>();

	[LabelWidth(200)]
	[LabelText("是否执行掉血")]
	public bool isExAdd;
	
	[HideInInspector]
	public bool isSucEx;
}

[Serializable]
public class PropertyData
{
	[LabelText("判断类型")]
	public VideoIfType videoIfType;
	
	[LabelText("属性类型")]
	public PropertyType PropertyType;
	
	[ShowIf("PropertyType",PropertyType.Value)]
	[LabelText("属性值名称")]
	public string TypeName;
	
	[ShowIf("PropertyType",PropertyType.Item)]
	[LabelText("道具类型")]
	public ItemType itemType;
	
	[ShowIf("PropertyType",PropertyType.Property)]
	[LabelText("属性类型")]
	public PropertyTypeValue propertyTypeValue;

	[ShowIf("PropertyType",PropertyType.Other)]
	[LabelText("其他属性值")]
	public OtherType otherType;
	
	[LabelText("属性值")]
	public int PropertyValue;

	[LabelText("添加属性的时间")]
	public float addPropertyTime;

	[LabelText("是否显示属性弹窗")] 
	public bool isShowTips=true;
	[HideInInspector]
	public bool isEx; //是否执行了这个

	[LabelText("是否是回合属性")]
	public bool isRound;

	[LabelText("是否一直会触发")] 
	public bool isAlwaysEx;
}



public enum TaskType
{
	[LabelText("空")]None,
	[LabelText("开启")]Open,
	[LabelText("完成")]Suc,
	[LabelText("失败")]Lose
}

[Serializable]
public class TaskData
{
	[LabelText("任务id")]
	public string xlsxTaskKey;
	[LabelText("任务状态")]
	public TaskType taskType;
}

[CreateNodeMenu("Video/VideoNode")]
[NodeWidth(300)]
public class VideoNode : BaseNode
{

	[LabelText("DEBUG标记")]
	public string tips;


	[LabelText("显示结尾底图")]
	public bool isShowLastImg=true;
	// [ReadOnly]
	// [LabelText("唯一id")]
	// public string uid;
	// [LabelText("视频名称")]
	// public Xlsx_Language_Key videoName;
	[LabelText("视频对应的按钮")]
	[Input] 
	public ButtonNode buttonNode;
	[LabelText("上一个输入过来的视频")]
	[Input]
	public VideoNode inputVideoNode;
	
	[LabelText("输出到的视频")]
	[Output]
	public VideoNode outVideoNode;



#if UNITY_EDITOR
	[LabelText("视频源")]
	[InlineEditor(InlineEditorModes.LargePreview)]
	[OnValueChanged("VideoChange")]
	public VideoClip  videoClip;
#endif

	[LabelText("中文字幕文件")]
	public VideoAudioTextData[] subtitleChinese;
	
	[LabelText("英文字幕文件")]
	public VideoAudioTextData[] subtitleEnglish;
	
	[ReadOnly]
	[LabelText("视频路径")]
	public string videoPath;
	[ReadOnly]
	[LabelText("视频名字")]
	public string videName;
	
	[LabelText("音频源")]
	[InlineEditor(InlineEditorModes.LargePreview)]
	public AudioClip audioClip;
	
	[LabelText("视频默认从多少秒开始播放")]
	public float videoStartTime;

	[LabelWidth(200)]
	[LabelText("是否在播放到多少秒直接播放下一个视频")]
	public bool isPlayTimeToNext;
	
	[LabelWidth(200)]
	[ShowIf("isPlayTimeToNext")]
	[LabelText("到下一个视频的时间")]
	public float toNextTime;
	[LabelWidth(200)]
	[ShowIf("isPlayTimeToNext")]
	[LabelText("输出到的视频")]
	[Output]
	public VideoNode playTimeToNextVideoNode;

	[LabelWidth(200)]
	[LabelText("点击选项直接结束")] 
	public bool isNowToNext;
	[LabelWidth(200)]
	[LabelText("跳到下个视频是否直接跳到显示按钮")]
	public bool isToNextVideoToLoopStart;
	// [LabelWidth(200)]
	// [ShowIf("isToNextVideoToLoopStart")]
	// [LabelText("下一个视频的循环时间点")]
	// public float nextLoopStartTime;
	
	[LabelWidth(200)]
	[LabelText("是否是循环播放视频")]
	public bool isLoopVideo;
	
	[LabelWidth(200)]
	[LabelText("是否视频含有按钮显示")]
	public bool isHasBtn;
	
	[ShowIf("isHasBtn")]
	[LabelWidth(200)]
	[LabelText("按钮出现时间")]
	public float videoBtnShowTime;
	[ShowIf("isHasBtn")]
	[LabelWidth(200)]
	[LabelText("按钮消失时间")]
	public float videoBtnHideTime;

	[LabelWidth(200)]
	[LabelText("是否跳转到下一个事件")]
	public bool isToNextGroup;
	
	[ShowIf("isToNextGroup")]
	[LabelWidth(200)]
	[LabelText("视频事件")]
	public VideoGraph videoGraph;
	
	
	[LabelWidth(200)]
	[LabelText("是否是全景视频")]
	public bool is360Video;

	[ShowIf("is360Video")]
	[LabelText("旋转角度")]
	[LabelWidth(200)]
	public float rotationAngle;
	
	[LabelWidth(200)]
	[ShowIf("is360Video")]
	[LabelText("全景图是否就是平面图片")]
	public bool imgIsNor;
	[LabelText("全景图片")]
	[ShowIf("is360Video")]
	public Texture2D img360;
	
	[LabelWidth(200)]
	[LabelText("是否播放完视频后结束")]
	public bool isPlayerEndOver;
	[LabelWidth(200)]
	[LabelText("是否播放完视频后直接衔接下一个视频")]
	public bool isPlayerEndPlayerNext;
	[LabelWidth(200)]
	[ShowIf("isPlayerEndPlayerNext")]
	[LabelText("下个视频是否随机")]
	public bool isPlayerEndPlayerNextRandom;
	
	[LabelWidth(200)]
	[ShowIf("isPlayerEndPlayerNext")]
	[LabelText("下个视频是否顺序播放")]
	public bool isPlayerEndPlayerNextSequence;
	[HideInInspector]
	public int sequenceIndex = 0;
	[LabelWidth(200)]
	[ShowIf("isPlayerEndPlayerNext")]
	[Output]
	[LabelText("下一个视频")]
	public VideoNode nextVideoNode;

	// [LabelText("添加属性是否以回合计算")]
	// [LabelWidth(200)]
	// public bool addIsRound;
	[LabelText("视频需要添加的属性")]
	public List<PropertyData> PropertyData = new List<PropertyData>();
	
	[LabelWidth(200)]
	[LabelText("是否点击按钮只播放一段音频")]
	public bool isClickBtnPlayAudio;
	[LabelWidth(200)]
	[ShowIf("isClickBtnPlayAudio")]
	[LabelText("播放音频是否还要播放视频")]
	public bool isClickBtnPlayVideo;
	[LabelWidth(200)]
	[ShowIf("isClickBtnPlayAudio")]
	[LabelText("按钮点击播放的音频")]
	public AudioClip clickAudio;

	[LabelWidth(200)]
	[LabelText("是否是触发成功的视频")]
	public bool isSucVideo;
	
	
	[LabelWidth(200)]
	[LabelText("是否视频结束跳转到地图")]
	public bool isToMap;

	[LabelWidth(200)]
	[LabelText("是否结束跳转工作小游戏")]
	public bool isToGame;
	
	[LabelText("视频触发的事件值")]
	public List<EventData> EventValue=new List<EventData>();
	
	[LabelText("视频结束事件值")]
	public List<EventData> VideoEndEventValue=new List<EventData>();

	[LabelText("视频结束需要添加的属性")]
	public List<PropertyData> VideoEndPropertyData = new List<PropertyData>();


	[LabelText("喝酒系统")] 
	public bool isHasWineList;

	[ShowIf("isHasWineList")]
	[LabelText("是否初始化酒量")] 
	public bool isInitJiuLian;
	[ShowIf("isInitJiuLian")]
	[LabelText("对方酒量")]
	public float targetJiuLian;
	// [ShowIf("isHasWineList")]
	// [LabelText("选了酒单后事件")]
	// public VideoGraph jiuDanEvent;
	
	// [LabelWidth(200)]
	// [LabelText("是否添加事件次数")] 
	// public bool isAddEventCount;
	
	[LabelWidth(200)]
	[LabelText("是否含有qte")]
	public bool isHasQte;

	[ShowIf("isHasQte")]
	[LabelWidth(200)]
	[LabelText("是否初始化血量")]
	public bool isInitHp;

	[LabelWidth(200)]
	[ShowIf("isInitHp")]
	[LabelText("敌人血量")] 
	public float enemyHp;
	
	[ShowIf("isHasQte")]
	[LabelText("qte数据")]
	public List<QteData>  qteDatas=new List<QteData>();

	[ShowIf("isHasQte")]
	[Output]
	[LabelText("qte成功")]
	public VideoNode qteSuc;
	
	[ShowIf("isHasQte")]
	[Output]
	[LabelText("qte失败")]
	public VideoNode qteFail;

	[ShowIf("isHasQte")]
	[Output] 
	[LabelText("敌方是否中途死亡")] 
	public VideoNode enemyDieVideoNode;
	
	[ShowIf("isHasQte")]
	[Button]
	public void OpenBtnPosSetWindowsAsQte()
	{
#if UNITY_EDITOR
		// 通过字符串获取类。注意：如果类在命名空间里，需要写全称
		// "类名, 程序集名"
		Type windowType = Type.GetType("FrameWork.VideoBtnSetWindows, Assembly-CSharp-Editor");
        
		if (windowType != null) 
		{
			EditorWindow.GetWindow(windowType).Show();
			windowType.GetMethod("InitAsQte").Invoke(null, new Object[] { qteDatas, videoClip, videoBtnShowTime });
		}
		else 
		{
			Debug.LogError("找不到该窗口类！");
		}
#endif
		
		
// #if UNITY_EDITOR
// 		EditorWindow.GetWindow<VideoBtnSetWindows>().Init(btnNodes,videoClip,videoBtnShowTime);
// #endif
		
	}
	
	[LabelText("视频添加任务数据")]
	public List<TaskData> taskData;
	
	
	[LabelText("视频结束后添加消息数据")]
	public List<ChatData>  chatData;
	[Button]
	public void OpenBtnPosSetWindows()
	{
		var allBtn = GetOutputPort("outVideoNode").GetAllButtonNodes(true);
		//var videoNodes=allBtn.Select((node => (VideoNode)node)).ToList();
		var btnNodes=allBtn;
#if UNITY_EDITOR
		// 通过字符串获取类。注意：如果类在命名空间里，需要写全称
		// "类名, 程序集名"
		Type windowType = Type.GetType("FrameWork.VideoBtnSetWindows, Assembly-CSharp-Editor");
        
		if (windowType != null) 
		{
			EditorWindow.GetWindow(windowType).Show();
			windowType.GetMethod("Init").Invoke(null, new Object[] { btnNodes, videoClip, videoBtnShowTime });
		}
		else 
		{
			Debug.LogError("找不到该窗口类！");
		}
#endif
		
// #if UNITY_EDITOR
// 		EditorWindow.GetWindow<VideoBtnSetWindows>().Init(btnNodes,videoClip,videoBtnShowTime);
// #endif
		
	}
	
	private void VideoChange()
	{
#if UNITY_EDITOR
		if (!videoClip)return;
		videoPath=AssetDatabase.GetAssetPath(videoClip);
		videoPath=Path.GetDirectoryName(videoPath).Replace("\\","/")+"/"+Path.GetFileNameWithoutExtension(videoPath);
		videoPath = videoPath.Split("Video")[1];
		videName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(videoClip));
#endif
		
	}
	
// 	private void AudioChange()
// 	{
// #if UNITY_EDITOR
// 		if (!audioClip)return;
// 		audioPath=AssetDatabase.GetAssetPath(audioClip);
// 		audioPath=Path.GetDirectoryName(audioPath)+"/"+Path.GetFileNameWithoutExtension(audioPath);
// 		audioPath = audioPath.Split("Video")[1];
// #endif
// 		
// 	}
	
	// Use this for initialization
	protected override void Init() {
		base.Init();
#if UNITY_EDITOR
		// if (string.IsNullOrEmpty(uid))
		// {
		// 	uid=Guid.NewGuid().ToString();
		// }
		//VideoChange();
		//uid=Guid.NewGuid().ToString();
#endif

	}

	// Return the correct value of an output port when requested
	public override object GetValue(NodePort port) {
		if (port.fieldName is "Video" or "outVideoNode")
		{
			return this;
		}

		return null;
	}
}