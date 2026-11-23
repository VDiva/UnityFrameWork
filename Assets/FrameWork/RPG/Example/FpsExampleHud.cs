using UnityEngine;

namespace FrameWork.FPS
{
    public sealed class FpsExampleHud : MonoBehaviour
    {
        [SerializeField] private FpsPlayer player;
        [SerializeField] private FpsHealth playerHealth;
        [SerializeField] private FpsInventory inventory;
        [SerializeField] private FpsItemDefinition ammoPack;
        [SerializeField] private FpsItemDefinition medkit;
        [SerializeField] private RpgProgression progression;
        private FpsWeapon[] weapons;
        private float flashUntil;

        private void Start()
        {
            // Leave room to demonstrate healing as soon as the player picks up a medkit.
            playerHealth.ApplyDamage(40f, null);
            weapons = player.GetComponentsInChildren<FpsWeapon>(true);
            foreach (var weapon in weapons)
            {
                weapon.Fired += ShowShot;
                weapon.MeleeSwung += ShowShot;
            }
        }

        private void OnDestroy()
        {
            if (weapons == null) return;
            foreach (var weapon in weapons)
            {
                if (weapon == null) continue;
                weapon.Fired -= ShowShot;
                weapon.MeleeSwung -= ShowShot;
            }
        }

        private void ShowShot() => flashUntil = Time.unscaledTime + 0.12f;

        private void Update()
        {
            if ((player.IsTopDown || Cursor.lockState == CursorLockMode.Locked) && Input.GetKeyDown(KeyCode.H))
                inventory.TryUse(medkit);
        }

        private void OnGUI()
        {
            var weapon = player.EquippedWeapon;
            string weaponName = weapon != null ? weapon.Definition.DisplayName : "Unarmed";
            string ammo = weapon == null ? "-" : weapon.IsMelee ? "Melee" : $"{weapon.MagazineAmmo} / {weapon.ReserveAmmo}";
            string reload = weapon != null && weapon.IsReloading ? "  Reloading" : "";

            GUILayout.BeginArea(new Rect(16f, 16f, 540f, 164f), GUI.skin.box);
            string perspective = player.IsTopDown ? "Top down" : player.IsThirdPerson ? "Third person" : "First person";
            GUILayout.Label($"TRAINING RANGE    HP {playerHealth.CurrentHealth:0} / {playerHealth.MaxHealth:0}    {perspective}");
            GUILayout.Label($"{weaponName}    {ammo}{reload}");
            if (progression != null)
            {
                string experience = progression.IsMaxLevel ? "MAX" :
                    $"{progression.CurrentExperience} / {progression.RequiredExperience}";
                GUILayout.Label($"Level {progression.Level}    XP {experience}");
            }
            GUILayout.Label($"Ammo packs: {inventory.GetCount(ammoPack)}   Medkits: {inventory.GetCount(medkit)}");
            string aimHint = player.IsTopDown ? "Mouse aim" : "Click to capture";
            GUILayout.Label($"{aimHint}  |  WASD move  |  LMB attack  |  R reload  |  1–3 equip");
            GUILayout.Label("V camera  |  F ammo  |  H heal  |  Space jump  |  Shift sprint  |  Esc release");
            GUILayout.EndArea();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float x = Screen.width * 0.5f;
                float y = Screen.height * 0.5f;
                var color = GUI.color;
                if (Time.unscaledTime < flashUntil) GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(x - 7f, y - 1f, 14f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - 1f, y - 7f, 2f, 14f), Texture2D.whiteTexture);
                GUI.color = color;
            }
        }
    }
}
