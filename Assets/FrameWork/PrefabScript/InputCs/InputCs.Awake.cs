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
		}
	}
}
