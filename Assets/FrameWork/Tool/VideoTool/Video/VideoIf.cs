using System.Collections;
using System.Collections.Generic;
using FrameWork;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;


public enum VideoIfType
{
	[LabelText("大于")]Gre,
	[LabelText("小于")]Less,
}

[CreateNodeMenu("Video/VideoIf")]
[NodeWidth(300)]
public class VideoIf : Node
{
	[LabelText("输入")]
	[Input]
	public string input;
	
	[LabelText("需要达到要求才可以进行")]
	public List<PropertyData> properties=new List<PropertyData>();
	// [LabelText("判定类型")]
	// public VideoIfType videoIfType;
	// [LabelText("属性类型")]
	// public string property;
	// [LabelText("需要的值")] 
	// public int value;

	[Output]
	[LabelText("判定成功的视频输出")]
	public VideoNode oneVideoNode;
	[Output]
	[LabelText("判定失败的视频输出")]
	public VideoNode towVideoNode;
	
	// Use this for initialization
	protected override void Init() {
		base.Init();
		
	}

	// Return the correct value of an output port when requested
	public override object GetValue(NodePort port) {
		return null; // Replace this
	}
	
	
	public VideoNode  GetVideoNode()
	{
		bool isSuc = properties.IsSuc();
		if (isSuc)
		{
			var videos = GetOutputPort("oneVideoNode").GetAllVideoNodes();
			return videos[0];
		}
		else
		{
			var videos = GetOutputPort("towVideoNode").GetAllVideoNodes();
			return videos[0];
		}
		
	}
}