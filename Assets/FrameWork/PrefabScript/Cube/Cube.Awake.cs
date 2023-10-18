using UnityEngine;
namespace Prefab.Script
{
	public partial class Cube : MonoBehaviour
	{
		private void Awake()
		{
			TransformCube = transform.GetComponent<Transform>();
			MeshFilterCube = transform.GetComponent<MeshFilter>();
			MeshRendererCube = transform.GetComponent<MeshRenderer>();
			BoxColliderCube = transform.GetComponent<BoxCollider>();
		}
	}
}
