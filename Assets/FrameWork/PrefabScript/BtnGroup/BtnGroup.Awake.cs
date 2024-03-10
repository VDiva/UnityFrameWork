using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class BtnGroup : UiActor
	{
		public override void Start()
		{
			base.Start();
			RectTransformBtnGroup = GetGameObject().transform.GetComponent<RectTransform>();
			RectTransformBtn1 = GetGameObject().transform.Find("Btn1/").GetComponent<RectTransform>();
			CanvasRendererBtn1 = GetGameObject().transform.Find("Btn1/").GetComponent<CanvasRenderer>();
			ImageBtn1 = GetGameObject().transform.Find("Btn1/").GetComponent<Image>();
			ButtonBtn1 = GetGameObject().transform.Find("Btn1/").GetComponent<Button>();
			RectTransformBtn1Text = GetGameObject().transform.Find("Btn1/Btn1Text/").GetComponent<RectTransform>();
			CanvasRendererBtn1Text = GetGameObject().transform.Find("Btn1/Btn1Text/").GetComponent<CanvasRenderer>();
			TextBtn1Text = GetGameObject().transform.Find("Btn1/Btn1Text/").GetComponent<Text>();
			RectTransformBtn2 = GetGameObject().transform.Find("Btn2/").GetComponent<RectTransform>();
			CanvasRendererBtn2 = GetGameObject().transform.Find("Btn2/").GetComponent<CanvasRenderer>();
			ImageBtn2 = GetGameObject().transform.Find("Btn2/").GetComponent<Image>();
			ButtonBtn2 = GetGameObject().transform.Find("Btn2/").GetComponent<Button>();
			RectTransformBtn2Text = GetGameObject().transform.Find("Btn2/Btn2Text/").GetComponent<RectTransform>();
			CanvasRendererBtn2Text = GetGameObject().transform.Find("Btn2/Btn2Text/").GetComponent<CanvasRenderer>();
			TextBtn2Text = GetGameObject().transform.Find("Btn2/Btn2Text/").GetComponent<Text>();
			RectTransformBtn3 = GetGameObject().transform.Find("Btn3/").GetComponent<RectTransform>();
			CanvasRendererBtn3 = GetGameObject().transform.Find("Btn3/").GetComponent<CanvasRenderer>();
			ImageBtn3 = GetGameObject().transform.Find("Btn3/").GetComponent<Image>();
			ButtonBtn3 = GetGameObject().transform.Find("Btn3/").GetComponent<Button>();
			RectTransformBtn3Text = GetGameObject().transform.Find("Btn3/Btn3Text/").GetComponent<RectTransform>();
			CanvasRendererBtn3Text = GetGameObject().transform.Find("Btn3/Btn3Text/").GetComponent<CanvasRenderer>();
			TextBtn3Text = GetGameObject().transform.Find("Btn3/Btn3Text/").GetComponent<Text>();
			RectTransformBtn4 = GetGameObject().transform.Find("Btn4/").GetComponent<RectTransform>();
			CanvasRendererBtn4 = GetGameObject().transform.Find("Btn4/").GetComponent<CanvasRenderer>();
			ImageBtn4 = GetGameObject().transform.Find("Btn4/").GetComponent<Image>();
			ButtonBtn4 = GetGameObject().transform.Find("Btn4/").GetComponent<Button>();
			RectTransformBtn4Text = GetGameObject().transform.Find("Btn4/Btn4Text/").GetComponent<RectTransform>();
			CanvasRendererBtn4Text = GetGameObject().transform.Find("Btn4/Btn4Text/").GetComponent<CanvasRenderer>();
			TextBtn4Text = GetGameObject().transform.Find("Btn4/Btn4Text/").GetComponent<Text>();
			RectTransformBtn5 = GetGameObject().transform.Find("Btn5/").GetComponent<RectTransform>();
			CanvasRendererBtn5 = GetGameObject().transform.Find("Btn5/").GetComponent<CanvasRenderer>();
			ImageBtn5 = GetGameObject().transform.Find("Btn5/").GetComponent<Image>();
			ButtonBtn5 = GetGameObject().transform.Find("Btn5/").GetComponent<Button>();
			RectTransformBtn5Text = GetGameObject().transform.Find("Btn5/Btn5Text/").GetComponent<RectTransform>();
			CanvasRendererBtn5Text = GetGameObject().transform.Find("Btn5/Btn5Text/").GetComponent<CanvasRenderer>();
			TextBtn5Text = GetGameObject().transform.Find("Btn5/Btn5Text/").GetComponent<Text>();
		}
	}
}
