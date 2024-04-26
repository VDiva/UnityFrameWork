using UnityEngine;
using FrameWork;
using UnityEngine.UI;
using UnityEngine.Video;
namespace FrameWork
{
	public partial class MyVideoPlayer : UiActor
	{
		public override void Awake()
		{
			base.Awake();
			RectTransformMyVideoPlayer = GetGameObject().transform.GetComponent<RectTransform>();
			VideoPlayerMyVideoPlayer = GetGameObject().transform.GetComponent<VideoPlayer>();
			CanvasRendererMyVideoPlayer = GetGameObject().transform.GetComponent<CanvasRenderer>();
			RawImageMyVideoPlayer = GetGameObject().transform.GetComponent<RawImage>();
		}
	}
}
