using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class CsCube : Actor
	{
		public override void Start()
		{
			base.Start();
			TransformCsCube = GetGameObject().transform.GetComponent<Transform>();
			MeshFilterCsCube = GetGameObject().transform.GetComponent<MeshFilter>();
			MeshRendererCsCube = GetGameObject().transform.GetComponent<MeshRenderer>();
			BoxColliderCsCube = GetGameObject().transform.GetComponent<BoxCollider>();
		}
	}
}
