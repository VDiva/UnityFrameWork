using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class CsText : UiActor
	{
		public override void Awake()
		{
			base.Awake();
			RectTransformCsText = GetGameObject().transform.GetComponent<RectTransform>();
			CanvasRendererCsText = GetGameObject().transform.GetComponent<CanvasRenderer>();
			TextCsText = GetGameObject().transform.GetComponent<Text>();
			RectTransformddd = GetGameObject().transform.Find("ddd/").GetComponent<RectTransform>();
			CanvasRendererddd = GetGameObject().transform.Find("ddd/").GetComponent<CanvasRenderer>();
			Imageddd = GetGameObject().transform.Find("ddd/").GetComponent<Image>();
			RectTransformfffff = GetGameObject().transform.Find("ddd/fffff/").GetComponent<RectTransform>();
			CanvasRendererfffff = GetGameObject().transform.Find("ddd/fffff/").GetComponent<CanvasRenderer>();
			Textfffff = GetGameObject().transform.Find("ddd/fffff/").GetComponent<Text>();
			RectTransformffa = GetGameObject().transform.Find("ddd/fffff/ffa/").GetComponent<RectTransform>();
			CanvasRendererffa = GetGameObject().transform.Find("ddd/fffff/ffa/").GetComponent<CanvasRenderer>();
			Textffa = GetGameObject().transform.Find("ddd/fffff/ffa/").GetComponent<Text>();
			RectTransformbtn1 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/").GetComponent<RectTransform>();
			CanvasRendererbtn1 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/").GetComponent<CanvasRenderer>();
			Imagebtn1 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/").GetComponent<Image>();
			Buttonbtn1 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/").GetComponent<Button>();
			RectTransformbtn2 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<RectTransform>();
			CanvasRendererbtn2 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<CanvasRenderer>();
			Imagebtn2 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<Image>();
			Buttonbtn2 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<Button>();
			RectTransformbtn3333333333 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<RectTransform>();
			CanvasRendererbtn3333333333 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<CanvasRenderer>();
			Imagebtn3333333333 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<Image>();
			Buttonbtn3333333333 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<Button>();
			RectTransformtext9999999999999 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/text9999999999999/").GetComponent<RectTransform>();
			CanvasRenderertext9999999999999 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/text9999999999999/").GetComponent<CanvasRenderer>();
			Texttext9999999999999 = GetGameObject().transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/text9999999999999/").GetComponent<Text>();
		}
	}
}
