using UnityEngine;
using FrameWork;
using UnityEngine.UI;
using UnityEngine.Video;
namespace FrameWork
{
	public partial class UiRoot : UiActor
	{
		public override void Awake()
		{
			base.Awake();
			RectTransformUiRoot = GetGameObject().transform.GetComponent<RectTransform>();
			CanvasUiRoot = GetGameObject().transform.GetComponent<Canvas>();
			CanvasScalerUiRoot = GetGameObject().transform.GetComponent<CanvasScaler>();
			GraphicRaycasterUiRoot = GetGameObject().transform.GetComponent<GraphicRaycaster>();
			RectTransformBackground = GetGameObject().transform.Find("Background/").GetComponent<RectTransform>();
			RectTransformNormal = GetGameObject().transform.Find("Normal/").GetComponent<RectTransform>();
			RectTransformControl = GetGameObject().transform.Find("Control/").GetComponent<RectTransform>();
			RectTransformPopup = GetGameObject().transform.Find("Popup/").GetComponent<RectTransform>();
		}
	}
}
