using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class UiRoot : UiActor
	{
		public override void Start()
		{
			base.Start();
			RectTransformUiRoot = GetGameObject().transform.GetComponent<RectTransform>();
			CanvasUiRoot = GetGameObject().transform.GetComponent<Canvas>();
			CanvasScalerUiRoot = GetGameObject().transform.GetComponent<CanvasScaler>();
			GraphicRaycasterUiRoot = GetGameObject().transform.GetComponent<GraphicRaycaster>();
			RectTransformPopup = GetGameObject().transform.Find("Popup/").GetComponent<RectTransform>();
			RectTransformNormal = GetGameObject().transform.Find("Normal/").GetComponent<RectTransform>();
			RectTransformControl = GetGameObject().transform.Find("Control/").GetComponent<RectTransform>();
			RectTransformBackground = GetGameObject().transform.Find("Background/").GetComponent<RectTransform>();
		}
	}
}
