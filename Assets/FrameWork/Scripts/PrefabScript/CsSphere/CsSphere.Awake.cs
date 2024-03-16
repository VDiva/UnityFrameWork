using UnityEngine;
using FrameWork;
using UnityEngine.UI;
namespace FrameWork
{
	public partial class CsSphere : Actor
	{
		public override void Awake()
		{
			base.Awake();
			TransformCsSphere = GetGameObject().transform.GetComponent<Transform>();
			MeshFilterCsSphere = GetGameObject().transform.GetComponent<MeshFilter>();
			MeshRendererCsSphere = GetGameObject().transform.GetComponent<MeshRenderer>();
			SphereColliderCsSphere = GetGameObject().transform.GetComponent<SphereCollider>();
		}
	}
}
