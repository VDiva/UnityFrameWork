using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

[CreateNodeMenu("Video/ChapterNode")]
public class ChapterNode : Node {

	
	[LabelText("事件名称")]
	public string chapterName;

	[LabelText("事件触发条件")]
	public List<PropertyData> propertyType;
	
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