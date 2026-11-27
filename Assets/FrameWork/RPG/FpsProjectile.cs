using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsProjectile : MonoBehaviour
    {
        private const int MaxPooledPerPrefab = 64;
        private static readonly Dictionary<FpsProjectile, Stack<FpsProjectile>> Pools = new Dictionary<FpsProjectile, Stack<FpsProjectile>>();

        private FpsProjectile sourcePrefab;
        private float speed;
        private float remainingDistance;
        private float damage;
        private LayerMask hitLayers;
        private GameObject instigator;
        private FpsWeapon weapon;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPools() => Pools.Clear();

        internal static FpsProjectile Spawn(FpsProjectile prefab, Vector3 position, Quaternion rotation)
        {
            if (Pools.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled == null) continue;
                    pooled.transform.SetPositionAndRotation(position, rotation);
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var created = Instantiate(prefab, position, rotation);
            created.sourcePrefab = prefab;
            return created;
        }

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

        private void Recycle()
        {
            instigator = null;
            weapon = null;
            if (sourcePrefab != null)
            {
                if (!Pools.TryGetValue(sourcePrefab, out var stack))
                {
                    stack = new Stack<FpsProjectile>();
                    Pools[sourcePrefab] = stack;
                }
                if (stack.Count < MaxPooledPerPrefab)
                {
                    gameObject.SetActive(false);
                    stack.Push(this);
                    return;
                }
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            if (remainingDistance <= 0f)
            {
                Recycle();
                return;
            }

            float distance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
            if (distance <= 0f) return;
            Vector3 direction = transform.forward;
            Transform ignoredRoot = instigator != null ? instigator.transform : null;
            if (FpsWeapon.TryRaycastExcluding(new Ray(transform.position, direction), distance,
                hitLayers, ignoredRoot, out var hit))
            {
                GameObject hitInstigator = instigator;
                FpsWeapon hitWeapon = weapon;
                float hitDamage = damage;
                Recycle();
                FpsWeapon.ResolveHit(hit, hitDamage, hitInstigator, hitWeapon);
                return;
            }

            transform.position += direction * distance;
            remainingDistance -= distance;
            if (remainingDistance <= 0f) Recycle();
        }
    }
}
