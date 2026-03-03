using System.Collections;
using System.Collections.Generic;
using FrameWork;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

[CreateNodeMenu("Video/ShowIf")]
public class ShowIf : Node
{

	[Input]
	[LabelText("输入")]
	public string input;
	
	[Output]
	[LabelText("输出")]
	public string output;
	
	[LabelText("达成条件才有效")]
	public List<PropertyData> propertyTypes=new List<PropertyData>();
	// Use this for initialization
	protected override void Init() {
		base.Init();
		
	}

	// Return the correct value of an output port when requested
	public override object GetValue(NodePort port) {
		return null; // Replace this
	}


	
	
	public bool IsSuc()
	{
		return propertyTypes.IsSuc();
	}
}