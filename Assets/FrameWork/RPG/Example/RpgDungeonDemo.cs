using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    public sealed class RpgDungeonDemo : MonoBehaviour
    {
        [SerializeField] private FpsPlayer player;
        [SerializeField] private FpsHealth playerHealth;
        [SerializeField] private FpsInventory inventory;
        [SerializeField] private RpgProgression progression;
        [SerializeField] private FpsItemDefinition ammoPack;
        [SerializeField] private FpsItemDefinition medkit;
        [SerializeField] private RpgEncounterGate gate;
        [SerializeField] private Transform extraction;
        [SerializeField, Min(0.5f)] private float extractionRadius = 1.6f;

        private enum MissionState { Combat, Extract, Complete, Defeated }

        private MissionState state;
        private GUIStyle titleStyle;
        private GUIStyle objectiveStyle;
        private GUIStyle resultStyle;

        private void Update()
        {
            if (state == MissionState.Complete || state == MissionState.Defeated)
            {
                if (Input.GetKeyDown(KeyCode.Return)) Restart();
                return;
            }

            if (playerHealth.IsDepleted)
            {
                Finish(MissionState.Defeated);
                return;
            }

            if (Input.GetKeyDown(KeyCode.H) && (player.IsTopDown || Cursor.lockState == CursorLockMode.Locked))
                inventory.TryUse(medkit);

            if (!gate.IsOpen) return;

            state = MissionState.Extract;
            Vector3 offset = player.transform.position - extraction.position;
            if (offset.sqrMagnitude <= extractionRadius * extractionRadius)
                Finish(MissionState.Complete);
        }

        private void Finish(MissionState result)
        {
            state = result;
            player.enabled = false;
        }

        private static void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }

        private void OnGUI()
        {
            InitializeStyles();
            float width = Mathf.Min(450f, Screen.width - 32f);
            var header = new Rect(16f, 16f, width, 166f);
            GUI.Box(header, GUIContent.none);
            GUI.Label(new Rect(header.x + 12f, header.y + 9f, width - 24f, 29f), "EMBER VAULT", titleStyle);

            string objective = state switch
            {
                MissionState.Combat => $"Defeat the vault guards  ·  {gate.Remaining} remaining",
                MissionState.Extract => "Gate open  ·  Reach the exit beyond the gate",
                MissionState.Complete => "Extraction complete",
                _ => "You were defeated"
            };
            GUI.Label(new Rect(header.x + 12f, header.y + 44f, width - 24f, 22f), objective, objectiveStyle);

            var weapon = player.EquippedWeapon;
            string weaponInfo = "Unarmed";
            if (weapon != null)
            {
                string weaponName = weapon.Definition != null ? weapon.Definition.DisplayName : weapon.name;
                string rounds = weapon.IsMelee ? "Melee" : $"{weapon.MagazineAmmo} / {weapon.ReserveAmmo}";
                if (weapon.IsReloading) rounds += "  Reloading";
                weaponInfo = weaponName + "  ·  " + rounds;
            }

            float available = width - 24f;
            GUI.Label(new Rect(header.x + 12f, header.y + 72f, available, 21f),
                $"HP {playerHealth.CurrentHealth:0}/{playerHealth.MaxHealth:0}     LV {progression.Level}     {weaponInfo}");
            GUI.Label(new Rect(header.x + 12f, header.y + 95f, available, 19f),
                $"Ammo packs {inventory.GetCount(ammoPack)}    Medkits {inventory.GetCount(medkit)}");
            GUI.Label(new Rect(header.x + 12f, header.y + 120f, available, 18f),
                "WASD Move   LMB Attack   1–3 Equip   R Reload");
            GUI.Label(new Rect(header.x + 12f, header.y + 140f, available, 18f),
                "F Ammo   H Heal   V View   Esc Release Cursor");

            if (state != MissionState.Complete && state != MissionState.Defeated)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    float x = Screen.width * 0.5f;
                    float y = Screen.height * 0.5f;
                    GUI.DrawTexture(new Rect(x - 6f, y - 1f, 12f, 2f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(x - 1f, y - 6f, 2f, 12f), Texture2D.whiteTexture);
                }
                return;
            }

            float resultWidth = Mathf.Min(420f, Screen.width - 32f);
            var panel = new Rect((Screen.width - resultWidth) * 0.5f, Screen.height * 0.48f - 55f, resultWidth, 110f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 15f, panel.width, 36f),
                state == MissionState.Complete ? "VAULT CLEARED" : "MISSION FAILED", resultStyle);
            if (GUI.Button(new Rect(panel.center.x - 78f, panel.y + 62f, 156f, 29f), "Restart  [Enter]"))
                Restart();
        }

        private void InitializeStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
            objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            objectiveStyle.normal.textColor = new Color(0.98f, 0.78f, 0.41f);
            resultStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
