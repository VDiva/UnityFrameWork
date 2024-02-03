using UnityEngine;
using FrameWork;
namespace FrameWork
{
	public partial class Cube
	{
		protected virtual void Awake()
		{
			TransformCube = transform.GetComponent<Transform>();
			MeshFilterCube = transform.GetComponent<MeshFilter>();
			MeshRendererCube = transform.GetComponent<MeshRenderer>();
			BoxColliderCube = transform.GetComponent<BoxCollider>();
		}
	}
}
