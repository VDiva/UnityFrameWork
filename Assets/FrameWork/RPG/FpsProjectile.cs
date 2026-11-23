using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsProjectile : MonoBehaviour
    {
        private float speed;
        private float remainingDistance;
        private float damage;
        private LayerMask hitLayers;
        private GameObject instigator;
        private FpsWeapon weapon;

        internal void Launch(float travelSpeed, float maxDistance, float hitDamage,
            LayerMask layers, GameObject owner, FpsWeapon source)
        {
            speed = travelSpeed;
            remainingDistance = maxDistance;
            damage = hitDamage;
            hitLayers = layers;
            instigator = owner;
            weapon = source;
        }

        private void Update()
        {
            if (remainingDistance <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float distance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
            if (distance <= 0f) return;
            Vector3 direction = transform.forward;
            if (Physics.Raycast(transform.position, direction, out var hit, distance,
                hitLayers, QueryTriggerInteraction.Ignore))
            {
                Destroy(gameObject);
                FpsWeapon.ResolveHit(hit, damage, instigator, weapon);
                return;
            }

            transform.position += direction * distance;
            remainingDistance -= distance;
            if (remainingDistance <= 0f) Destroy(gameObject);
        }

    }
}
