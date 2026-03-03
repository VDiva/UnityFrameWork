using System.Collections;
using System.Collections.Generic;
using FrameWork;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using XNode;

[CreateNodeMenu("Video/ButtonNode")]
[NodeWidth(300)]
public class ButtonNode : Node {

	[LabelText("输出的按钮")]
	[Output]
	public ButtonNode button;
	[LabelText("按钮名字(仅编辑器使用)")]
	public string buttonName;

	[LabelText("按钮名字语言表")]
	public string btnName;
	
	
	[LabelText("按钮坐标")]
	public Vector3 buttonPosition;
	[LabelText("按钮大小")]
	public float buttonSize=80;

	[LabelWidth(200)]
	[LabelText("是否点击按钮需要确认")]
	public bool isClickSureTips;
	[ShowIf("isClickSureTips")]
	[LabelText("确认弹窗的内容")]
	public string btnSureTips;
	
	[LabelWidth(200)]
	[LabelText("按钮点击是否是打开ui")]
	public bool isOpenUiBtn;
	[ShowIf("isOpenUiBtn")]
	[LabelText("ui名字")]
	public string uiName;
	
	[LabelWidth(200)]
	[LabelText("是否点击触发事件")]
	public bool isClickExEvent;
	[LabelText("事件")]
	[ShowIf("isClickExEvent")] 
	public List<EventData> eventDatas = new List<EventData>();
	// Use this for initialization


	[LabelWidth(200)]
	[LabelText("是否含有前置点击条件")]
	public bool isHasUseProperty;
	[ShowIf("isHasUseProperty")]
	[LabelText("使用条件")]
	public List<PropertyData> usePropertyDatas = new List<PropertyData>();

	[ShowIf("isHasUseProperty")]
	[LabelText("前置条件不足显示文字")]
	public string usePropertyName;
	
	
	[LabelWidth(200)]
	[LabelText("前置条件满足其中一个")]
	public bool isHasUsePropertyOr;
	[FormerlySerializedAs("usePropertyDatas")]
	[ShowIf("isHasUsePropertyOr")]
	[LabelText("条件其中一个满足")]
	public List<PropertyData> usePropertyDatasOr = new List<PropertyData>();
	
	[ShowIf("isHasUsePropertyOr")]
	[LabelText("前置条件不足显示文字")]
	public string usePropertyNameOr;
	
	[LabelWidth(200)]
	[LabelText("是否点击触发属性修改")]
	public bool isSetProperty;
	[ShowIf("isSetProperty")]
	[LabelText("属性修改")]
	public List<PropertyData>  propertyDatas = new List<PropertyData>();

	[LabelWidth(200)]
	[LabelText("是否不修改颜色")]
	public bool btnIsNor;
	
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