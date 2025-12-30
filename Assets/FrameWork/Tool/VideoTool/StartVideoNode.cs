using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FrameWork;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

public class StartVideoNode : Node
{

	[LabelText("章节输入")]
	[Input]
	public ChapterNode chapterInput;
	
	[LabelText("章节输出")]
	[Output]
	public ChapterNode chapterOutput;
	
	
	[Button]
	private void LogOutCount()
	{
		var inputNode = GetInputPort("chapterInput").GetInputValues<ChapterNode>();

		for (int i = 0; i < inputNode.Length; i++)
		{
			Debug.Log(inputNode[i].chapterName);
		}
		
		
		var outNode=GetOutputPort("chapterOutput").GetOutNode();
		Debug.Log(outNode.Count);
		for (int i = 0; i < outNode.Count; i++)
		{
			var video=(VideoNode)outNode[i];
			Debug.Log(video.GetInputValue<ButtonNode>("buttonNode").buttonName);
		}
	}
	
	
	// Use this for initialization
	protected override void Init() {
		base.Init();
		
	}

	// Return the correct value of an output port when requested
	public override object GetValue(NodePort port) {
		return null; // Replace this
	}
}