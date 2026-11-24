using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] private RpgEncounterGraph encounterGraph;
        [SerializeField] private EncounterEvent[] encounterEvents = Array.Empty<EncounterEvent>();

        [Serializable]
        private sealed class EncounterEvent
        {
            public string key;
            public UnityEvent action;
        }

        private readonly List<RpgEncounterNode> route = new List<RpgEncounterNode>();
        private readonly Dictionary<string, int> signals = new Dictionary<string, int>(StringComparer.Ordinal);
        private int stepIndex;

        public RpgEncounterGraph EncounterGraph => encounterGraph;
        public RpgEncounterNode CurrentNode => stepIndex < route.Count ? route[stepIndex] : null;

        public void Signal(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            signals.TryGetValue(key, out int count);
            signals[key] = count + 1;
        }

        private enum MissionState { Combat, Extract, Complete, Defeated }

        private MissionState state;
        private GUIStyle titleStyle;
        private GUIStyle objectiveStyle;
        private GUIStyle resultStyle;

        private void Start()
        {
            if (encounterGraph == null) return;
            string error = encounterGraph.BuildRoute(route);
            if (error == null)
            {
                foreach (var node in route)
                {
                    if (!(node is RpgEncounterStep step)) continue;
                    if (step.operation == RpgEncounterOperation.WaitForSignal && string.IsNullOrWhiteSpace(step.key))
                        error = "等待信号的步骤必须填写 Key。";
                    if (step.operation == RpgEncounterOperation.InvokeEvent &&
                        (string.IsNullOrWhiteSpace(step.key) ||
                         Array.FindAll(encounterEvents, item => item != null && item.key == step.key).Length != 1))
                        error = $"事件 {step.key} 必须在场景中绑定一次。";
                    if (error != null) break;
                }
            }
            if (error == null) return;
            route.Clear();
            Debug.LogError(error, this);
            Finish(MissionState.Defeated);
        }

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

            if (encounterGraph != null)
            {
                AdvanceEncounter();
                return;
            }

            if (!gate.IsOpen) return;
            state = MissionState.Extract;
            if (IsAtExtraction()) Finish(MissionState.Complete);
        }

        private bool IsAtExtraction() =>
            (player.transform.position - extraction.position).sqrMagnitude <= extractionRadius * extractionRadius;

        private void AdvanceEncounter()
        {
            if (CurrentNode is RpgEncounterEnd)
            {
                Finish(MissionState.Complete);
                return;
            }
            if (CurrentNode is RpgEncounterStep step)
            {
                switch (step.operation)
                {
                    case RpgEncounterOperation.WaitForGuards:
                        state = MissionState.Combat;
                        if (gate.Remaining > 0) return;
                        break;
                    case RpgEncounterOperation.OpenGate:
                        gate.Open();
                        break;
                    case RpgEncounterOperation.WaitForExtraction:
                        state = MissionState.Extract;
                        if (!IsAtExtraction()) return;
                        break;
                    case RpgEncounterOperation.WaitForSignal:
                        if (!signals.TryGetValue(step.key, out int count)) return;
                        if (count == 1) signals.Remove(step.key);
                        else signals[step.key] = count - 1;
                        break;
                    case RpgEncounterOperation.InvokeEvent:
                        Array.Find(encounterEvents, item => item != null && item.key == step.key).action?.Invoke();
                        break;
                }
            }
            stepIndex++;
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
            var skillBar = player.SkillBar;
            var header = new Rect(16f, 16f, width, skillBar != null ? 215f : 166f);
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

            if (skillBar != null)
            {
                int index = 0;
                foreach (var slot in skillBar.Slots)
                {
                    if (slot?.skill == null) continue;
                    float remaining = skillBar.GetCooldown(slot.skill);
                    string status = !skillBar.MeetsLevel(slot.skill) ? $"Lv{slot.skill.RequiredLevel}"
                        : remaining > 0f ? $"{remaining:0.0}s" : "Ready";
                    float column = available / 3f;
                    GUI.Label(new Rect(header.x + 12f + index % 3 * column,
                        header.y + 167f + index / 3 * 19f, column, 19f),
                        $"{slot.key} {slot.skill.DisplayName} {status}");
                    if (++index == 6) break;
                }
            }

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
