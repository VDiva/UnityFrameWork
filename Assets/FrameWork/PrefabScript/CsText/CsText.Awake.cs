using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class CsText : UiActor
	{
		protected virtual void Awake()
		{
			RectTransformCsText = transform.GetComponent<RectTransform>();
			CanvasRendererCsText = transform.GetComponent<CanvasRenderer>();
			TextCsText = transform.GetComponent<Text>();
			RectTransformddd = transform.Find("ddd/").GetComponent<RectTransform>();
			CanvasRendererddd = transform.Find("ddd/").GetComponent<CanvasRenderer>();
			Imageddd = transform.Find("ddd/").GetComponent<Image>();
			RectTransformfffff = transform.Find("ddd/fffff/").GetComponent<RectTransform>();
			CanvasRendererfffff = transform.Find("ddd/fffff/").GetComponent<CanvasRenderer>();
			Textfffff = transform.Find("ddd/fffff/").GetComponent<Text>();
			RectTransformffa = transform.Find("ddd/fffff/ffa/").GetComponent<RectTransform>();
			CanvasRendererffa = transform.Find("ddd/fffff/ffa/").GetComponent<CanvasRenderer>();
			Textffa = transform.Find("ddd/fffff/ffa/").GetComponent<Text>();
			RectTransformbtn1 = transform.Find("ddd/fffff/ffa/btn1/").GetComponent<RectTransform>();
			CanvasRendererbtn1 = transform.Find("ddd/fffff/ffa/btn1/").GetComponent<CanvasRenderer>();
			Imagebtn1 = transform.Find("ddd/fffff/ffa/btn1/").GetComponent<Image>();
			Buttonbtn1 = transform.Find("ddd/fffff/ffa/btn1/").GetComponent<Button>();
			RectTransformbtn2 = transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<RectTransform>();
			CanvasRendererbtn2 = transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<CanvasRenderer>();
			Imagebtn2 = transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<Image>();
			Buttonbtn2 = transform.Find("ddd/fffff/ffa/btn1/btn2/").GetComponent<Button>();
			RectTransformbtn3333333333 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<RectTransform>();
			CanvasRendererbtn3333333333 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<CanvasRenderer>();
			Imagebtn3333333333 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<Image>();
			Buttonbtn3333333333 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/").GetComponent<Button>();
			RectTransformtext9999999999999 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/text9999999999999/").GetComponent<RectTransform>();
			CanvasRenderertext9999999999999 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/text9999999999999/").GetComponent<CanvasRenderer>();
			Texttext9999999999999 = transform.Find("ddd/fffff/ffa/btn1/btn2/btn3333333333/text9999999999999/").GetComponent<Text>();
		}
	}
}
