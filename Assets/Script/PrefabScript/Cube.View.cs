using UnityEngine;
	public partial class Cube : MonoBehaviour
	{
		private void Awake()
		{
			TransformCube = transform.GetComponent<Transform>();
			MeshFilterCube = transform.GetComponent<MeshFilter>();
			MeshRendererCube = transform.GetComponent<MeshRenderer>();
			BoxColliderCube = transform.GetComponent<BoxCollider>();
			Transforma = transform.Find("a/").GetComponent<Transform>();
			MeshFiltera = transform.Find("a/").GetComponent<MeshFilter>();
			MeshRenderera = transform.Find("a/").GetComponent<MeshRenderer>();
			BoxCollidera = transform.Find("a/").GetComponent<BoxCollider>();
			Transformc = transform.Find("a/c/").GetComponent<Transform>();
			MeshFilterc = transform.Find("a/c/").GetComponent<MeshFilter>();
			MeshRendererc = transform.Find("a/c/").GetComponent<MeshRenderer>();
			BoxColliderc = transform.Find("a/c/").GetComponent<BoxCollider>();
			Transformd = transform.Find("a/c/d/").GetComponent<Transform>();
			MeshFilterd = transform.Find("a/c/d/").GetComponent<MeshFilter>();
			MeshRendererd = transform.Find("a/c/d/").GetComponent<MeshRenderer>();
			BoxColliderd = transform.Find("a/c/d/").GetComponent<BoxCollider>();
		}
	}
