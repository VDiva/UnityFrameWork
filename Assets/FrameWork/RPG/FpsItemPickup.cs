using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class FpsItemPickup : MonoBehaviour
    {
        [SerializeField] private FpsItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        private bool collecting;

        private void Reset()
        {
            GetComponent<SphereCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other.GetComponentInParent<FpsInventory>());
        }

        public bool TryCollect(FpsInventory inventory)
        {
            if (collecting || quantity <= 0 || inventory == null) return false;
            collecting = true;
            try
            {
                int added = inventory.Add(item, quantity);
                if (added == 0) return false;
                quantity -= added;
                if (quantity == 0) Destroy(gameObject);
                return true;
            }
            finally
            {
                collecting = false;
            }
        }
    }
}
