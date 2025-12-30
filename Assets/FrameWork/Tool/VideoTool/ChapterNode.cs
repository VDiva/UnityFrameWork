using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

public class ChapterNode : Node {

	
	[LabelText("章节名称")]
	public string chapterName;

	[Output]
	public ChapterNode chapterNode;
	
	// Use this for initialization
	protected override void Init() {
		base.Init();
		
	}

	// Return the correct value of an output port when requested
	public override object GetValue(NodePort port) {

		if (port.fieldName == "chapterNode")
		{
			return this;
		}
		
		return null; // Replace this
	}
}