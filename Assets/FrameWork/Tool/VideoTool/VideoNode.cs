using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FrameWork;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using XNode;

public class VideoNode : Node
{

	[LabelText("视频对应的按钮")]
	[Input] 
	public ButtonNode buttonNode;
	[LabelText("上一个输入过来的视频")]
	[Input]
	public VideoNode inputVideoNode;
	
	[LabelText("输出到的视频")]
	[Output]
	public VideoNode outVideoNode;
	
	
	[DraggableButNotBuild]
	[LabelText("视频源")]
	[InlineEditor(InlineEditorModes.LargePreview)]
	[OnValueChanged("VideoChange")]
	public VideoClip  videoClip;
	
	public string videoPath;
	
	
	[LabelText("视频循环时间(按钮也在这个时间出现)")]
	public float videoLoopTime;

	[LabelText("是否视频播放完就死亡")]
	public bool isEndDie;

	[LabelText("是否是全景视频")]
	public bool is360Video;
	
	[Button]
	public void OpenBtnPosSetWindows()
	{
		var allBtn = GetOutputPort("outVideoNode").GetOutNode();
		var videoNodes=allBtn.Select((node => (VideoNode)node)).ToList();
		var btnNodes=videoNodes.Select((node => node.GetInputValue<ButtonNode>("buttonNode"))).ToList();
		EditorWindow.GetWindow<VideoBtnSetWindows>().Init(btnNodes,videoClip);
	}
	private void VideoChange()
	{
		videoPath=AssetDatabase.GetAssetPath(videoClip);
		videoPath=Path.GetDirectoryName(videoPath)+"\\"+Path.GetFileNameWithoutExtension(videoPath);
		videoPath = videoPath.Split("Video")[1];
	}
	
	// Use this for initialization
	protected override void Init() {
		base.Init();
		
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