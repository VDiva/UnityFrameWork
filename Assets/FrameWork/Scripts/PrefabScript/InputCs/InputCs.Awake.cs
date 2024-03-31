using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class InputCs : UiActor
	{
		public override void Awake()
		{
			base.Awake();
			RectTransformInputCs = GetGameObject().transform.GetComponent<RectTransform>();
			RectTransformInputField = GetGameObject().transform.Find("InputField/").GetComponent<RectTransform>();
			CanvasRendererInputField = GetGameObject().transform.Find("InputField/").GetComponent<CanvasRenderer>();
			ImageInputField = GetGameObject().transform.Find("InputField/").GetComponent<Image>();
			InputFieldInputField = GetGameObject().transform.Find("InputField/").GetComponent<InputField>();
			RectTransformPlaceholder = GetGameObject().transform.Find("InputField/Placeholder/").GetComponent<RectTransform>();
			CanvasRendererPlaceholder = GetGameObject().transform.Find("InputField/Placeholder/").GetComponent<CanvasRenderer>();
			TextPlaceholder = GetGameObject().transform.Find("InputField/Placeholder/").GetComponent<Text>();
			RectTransformInputText = GetGameObject().transform.Find("InputField/InputText/").GetComponent<RectTransform>();
			CanvasRendererInputText = GetGameObject().transform.Find("InputField/InputText/").GetComponent<CanvasRenderer>();
			TextInputText = GetGameObject().transform.Find("InputField/InputText/").GetComponent<Text>();
			RectTransformButton = GetGameObject().transform.Find("Button/").GetComponent<RectTransform>();
			CanvasRendererButton = GetGameObject().transform.Find("Button/").GetComponent<CanvasRenderer>();
			ImageButton = GetGameObject().transform.Find("Button/").GetComponent<Image>();
			ButtonButton = GetGameObject().transform.Find("Button/").GetComponent<Button>();
			RectTransformBtnTxt = GetGameObject().transform.Find("Button/BtnTxt/").GetComponent<RectTransform>();
			CanvasRendererBtnTxt = GetGameObject().transform.Find("Button/BtnTxt/").GetComponent<CanvasRenderer>();
			TextBtnTxt = GetGameObject().transform.Find("Button/BtnTxt/").GetComponent<Text>();
			RectTransformleft = GetGameObject().transform.Find("left/").GetComponent<RectTransform>();
			CanvasRendererleft = GetGameObject().transform.Find("left/").GetComponent<CanvasRenderer>();
			Imageleft = GetGameObject().transform.Find("left/").GetComponent<Image>();
			Buttonleft = GetGameObject().transform.Find("left/").GetComponent<Button>();
			RectTransformleftText = GetGameObject().transform.Find("left/leftText/").GetComponent<RectTransform>();
			CanvasRendererleftText = GetGameObject().transform.Find("left/leftText/").GetComponent<CanvasRenderer>();
			TextleftText = GetGameObject().transform.Find("left/leftText/").GetComponent<Text>();
			RectTransformright = GetGameObject().transform.Find("right/").GetComponent<RectTransform>();
			CanvasRendererright = GetGameObject().transform.Find("right/").GetComponent<CanvasRenderer>();
			Imageright = GetGameObject().transform.Find("right/").GetComponent<Image>();
			Buttonright = GetGameObject().transform.Find("right/").GetComponent<Button>();
			RectTransformrightText = GetGameObject().transform.Find("right/rightText/").GetComponent<RectTransform>();
			CanvasRendererrightText = GetGameObject().transform.Find("right/rightText/").GetComponent<CanvasRenderer>();
			TextrightText = GetGameObject().transform.Find("right/rightText/").GetComponent<Text>();
			RectTransformting = GetGameObject().transform.Find("ting/").GetComponent<RectTransform>();
			CanvasRendererting = GetGameObject().transform.Find("ting/").GetComponent<CanvasRenderer>();
			Imageting = GetGameObject().transform.Find("ting/").GetComponent<Image>();
			Buttonting = GetGameObject().transform.Find("ting/").GetComponent<Button>();
			RectTransformtingText = GetGameObject().transform.Find("ting/tingText/").GetComponent<RectTransform>();
			CanvasRenderertingText = GetGameObject().transform.Find("ting/tingText/").GetComponent<CanvasRenderer>();
			TexttingText = GetGameObject().transform.Find("ting/tingText/").GetComponent<Text>();
			RectTransformmatc = GetGameObject().transform.Find("matc/").GetComponent<RectTransform>();
			CanvasRenderermatc = GetGameObject().transform.Find("matc/").GetComponent<CanvasRenderer>();
			Imagematc = GetGameObject().transform.Find("matc/").GetComponent<Image>();
			Buttonmatc = GetGameObject().transform.Find("matc/").GetComponent<Button>();
			RectTransformmatcText = GetGameObject().transform.Find("matc/matcText/").GetComponent<RectTransform>();
			CanvasRenderermatcText = GetGameObject().transform.Find("matc/matcText/").GetComponent<CanvasRenderer>();
			TextmatcText = GetGameObject().transform.Find("matc/matcText/").GetComponent<Text>();
			RectTransformspanw = GetGameObject().transform.Find("spanw/").GetComponent<RectTransform>();
			CanvasRendererspanw = GetGameObject().transform.Find("spanw/").GetComponent<CanvasRenderer>();
			Imagespanw = GetGameObject().transform.Find("spanw/").GetComponent<Image>();
			Buttonspanw = GetGameObject().transform.Find("spanw/").GetComponent<Button>();
			RectTransformspanwText = GetGameObject().transform.Find("spanw/spanwText/").GetComponent<RectTransform>();
			CanvasRendererspanwText = GetGameObject().transform.Find("spanw/spanwText/").GetComponent<CanvasRenderer>();
			TextspanwText = GetGameObject().transform.Find("spanw/spanwText/").GetComponent<Text>();
		}
	}
}
