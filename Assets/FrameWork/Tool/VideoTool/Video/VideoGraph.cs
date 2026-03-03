using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

[CreateAssetMenu]
public class VideoGraph : NodeGraph
{
	[LabelText("事件名字")]
	public string eventName;

	[LabelText("对应事件表里面的key")] 
	public string xlsxEventKey;
	
}