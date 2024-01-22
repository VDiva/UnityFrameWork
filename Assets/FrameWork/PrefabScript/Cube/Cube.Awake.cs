using UnityEngine;
using FrameWork;
namespace FrameWork
{
	public partial class Cube : MonoBehaviour
	{
		private void Awake()
		{
			TransformCube = transform.GetComponent<Transform>();
			MeshFilterCube = transform.GetComponent<MeshFilter>();
			MeshRendererCube = transform.GetComponent<MeshRenderer>();
			BoxColliderCube = transform.GetComponent<BoxCollider>();
			IdentityCube = transform.GetComponent<Identity>();
			PlayerCube = transform.GetComponent<Player>();
		}
	}
}
