using UnityEngine;
namespace Prefab.Script
{
	public partial class Sphere : MonoBehaviour
	{
		private void Awake()
		{
			TransformSphere = transform.GetComponent<Transform>();
			MeshFilterSphere = transform.GetComponent<MeshFilter>();
			MeshRendererSphere = transform.GetComponent<MeshRenderer>();
			SphereColliderSphere = transform.GetComponent<SphereCollider>();
			TransformSphere1 = transform.Find("Sphere1/").GetComponent<Transform>();
			MeshFilterSphere1 = transform.Find("Sphere1/").GetComponent<MeshFilter>();
			MeshRendererSphere1 = transform.Find("Sphere1/").GetComponent<MeshRenderer>();
			SphereColliderSphere1 = transform.Find("Sphere1/").GetComponent<SphereCollider>();
			TransformSphere2 = transform.Find("Sphere1/Sphere2/").GetComponent<Transform>();
			MeshFilterSphere2 = transform.Find("Sphere1/Sphere2/").GetComponent<MeshFilter>();
			MeshRendererSphere2 = transform.Find("Sphere1/Sphere2/").GetComponent<MeshRenderer>();
			SphereColliderSphere2 = transform.Find("Sphere1/Sphere2/").GetComponent<SphereCollider>();
			TransformSphere3 = transform.Find("Sphere1/Sphere3/").GetComponent<Transform>();
			MeshFilterSphere3 = transform.Find("Sphere1/Sphere3/").GetComponent<MeshFilter>();
			MeshRendererSphere3 = transform.Find("Sphere1/Sphere3/").GetComponent<MeshRenderer>();
			SphereColliderSphere3 = transform.Find("Sphere1/Sphere3/").GetComponent<SphereCollider>();
			TransformSphere4 = transform.Find("Sphere4/").GetComponent<Transform>();
			MeshFilterSphere4 = transform.Find("Sphere4/").GetComponent<MeshFilter>();
			MeshRendererSphere4 = transform.Find("Sphere4/").GetComponent<MeshRenderer>();
			SphereColliderSphere4 = transform.Find("Sphere4/").GetComponent<SphereCollider>();
			TransformSphere5 = transform.Find("Sphere4/Sphere5/").GetComponent<Transform>();
			MeshFilterSphere5 = transform.Find("Sphere4/Sphere5/").GetComponent<MeshFilter>();
			MeshRendererSphere5 = transform.Find("Sphere4/Sphere5/").GetComponent<MeshRenderer>();
			SphereColliderSphere5 = transform.Find("Sphere4/Sphere5/").GetComponent<SphereCollider>();
			TransformSphere6 = transform.Find("Sphere4/Sphere5/Sphere6/").GetComponent<Transform>();
			MeshFilterSphere6 = transform.Find("Sphere4/Sphere5/Sphere6/").GetComponent<MeshFilter>();
			MeshRendererSphere6 = transform.Find("Sphere4/Sphere5/Sphere6/").GetComponent<MeshRenderer>();
			SphereColliderSphere6 = transform.Find("Sphere4/Sphere5/Sphere6/").GetComponent<SphereCollider>();
		}
	}
}
