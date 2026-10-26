using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class UiRoot : UiActor
	{
		public override void Awake()
		{
			base.Awake();
			RectTransformCamera = GetGameObject().transform.Find("Camera/").GetComponent<RectTransform>();
			CameraCamera = GetGameObject().transform.Find("Camera/").GetComponent<Camera>();
			AddScriptsCamera = GetGameObject().transform.Find("Camera/").GetComponent<AddScripts>();
		}
	}
}
