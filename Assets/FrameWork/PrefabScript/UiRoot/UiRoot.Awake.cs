using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class UiRoot : UiActor
	{
		protected virtual void Awake()
		{
			RectTransformUiRoot = transform.GetComponent<RectTransform>();
			CanvasUiRoot = transform.GetComponent<Canvas>();
			CanvasScalerUiRoot = transform.GetComponent<CanvasScaler>();
			GraphicRaycasterUiRoot = transform.GetComponent<GraphicRaycaster>();
			RectTransformPopup = transform.Find("Popup/").GetComponent<RectTransform>();
			RectTransformNormal = transform.Find("Normal/").GetComponent<RectTransform>();
			RectTransformControl = transform.Find("Control/").GetComponent<RectTransform>();
			RectTransformBackground = transform.Find("Background/").GetComponent<RectTransform>();
		}
	}
}
