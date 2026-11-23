using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class FpsLoadout : MonoBehaviour
    {
        [SerializeField] private Transform weaponMount;
        [SerializeField, Min(1)] private int slotLimit = 3;
        [SerializeField] private List<FpsWeapon> weapons = new List<FpsWeapon>();

        public FpsWeapon EquippedWeapon { get; private set; }
        public int SlotCount => weapons.Count;
        public event Action EquippedChanged;

        private void Awake()
        {
            for (int i = weapons.Count - 1; i >= 0; i--)
            {
                var weapon = weapons[i];
                if (weapon == null || weapon.transform == transform ||
                    !weapon.transform.IsChildOf(transform) || weapons.IndexOf(weapon) != i)
                    weapons.RemoveAt(i);
            }

            for (int i = 0; i < weapons.Count; i++) weapons[i].gameObject.SetActive(false);
            if (weapons.Count > 0) TryEquip(0);
        }

        public FpsWeapon GetWeapon(int slot) => slot >= 0 && slot < weapons.Count ? weapons[slot] : null;

        public bool TryEquip(int slot)
        {
            var next = GetWeapon(slot);
            if (next == null || next == EquippedWeapon) return false;

            var previous = EquippedWeapon;
            EquippedWeapon = next;
            if (previous != null) previous.gameObject.SetActive(false);
            next.gameObject.SetActive(true);
            EquippedChanged?.Invoke();
            return true;
        }

        public bool TryAcquire(FpsWeapon prefab)
        {
            if (prefab == null || prefab.Definition == null || weapons.Count >= Mathf.Max(1, slotLimit))
                return false;

            foreach (var weapon in weapons)
                if (weapon != null && weapon.Definition == prefab.Definition) return false;

            var instance = Instantiate(prefab, weaponMount != null ? weaponMount : transform, false);
            weapons.Add(instance);
            if (EquippedWeapon == null) TryEquip(weapons.Count - 1);
            else instance.gameObject.SetActive(false);
            return true;
        }
    }
}
