using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class Textcs
	{
		protected virtual void Awake()
		{
			RectTransformTextcs = transform.GetComponent<RectTransform>();
			CanvasRendererTextcs = transform.GetComponent<CanvasRenderer>();
			TextTextcs = transform.GetComponent<Text>();
		}
	}
}
