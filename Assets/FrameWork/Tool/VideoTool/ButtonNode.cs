using System.Collections;
using System.Collections.Generic;
using FrameWork;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using XNode;

public class ButtonNode : Node {

	[LabelText("输出的按钮")]
	[Output]
	public ButtonNode button;
	[LabelText("按钮名字")]
	public string buttonName;
	[LabelText("按钮坐标")]
	public Vector2 buttonPosition;
	[LabelText("按钮大小")]
	public float buttonSize;
	
	// Use this for initialization
	
	protected override void Init() {
		base.Init();
	}

	// Return the correct value of an output port when requested
	public override object GetValue(NodePort port) {
		if (port.fieldName == "button")
		{
			return this;
		}

		return null;
	}
}