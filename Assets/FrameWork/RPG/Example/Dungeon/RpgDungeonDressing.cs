using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    public sealed class RpgDungeonDressing : MonoBehaviour
    {
        [SerializeField] private GameObject[] shellPlaceholders;
        [SerializeField] private GameObject playerVisual;
        [SerializeField] private GameObject[] guards;
        [SerializeField] private Transform sword;
        [SerializeField] private Transform ammoPickup;
        [SerializeField] private Transform medkitPickup;
        [SerializeField] private Transform gate;
        [SerializeField] private Transform extraction;

        private readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        private Material atlasMaterial;
        private Material markerMaterial;

        private void Start()
        {
            var art = new GameObject("Dungeon Art").transform;
            art.SetParent(transform, false);
            var shader = Shader.Find("Standard");
            if (shader != null)
            {
                var atlas = Resources.Load<Texture2D>("EmberVault/Textures/colormap");
                if (atlas != null)
                {
                    atlasMaterial = new Material(shader) { mainTexture = atlas };
                    atlasMaterial.SetFloat("_Glossiness", 0.05f);
                }

                markerMaterial = new Material(shader) { color = new Color(0.12f, 0.58f, 0.53f) };
                markerMaterial.EnableKeyword("_EMISSION");
                markerMaterial.SetColor("_EmissionColor", new Color(0.04f, 0.33f, 0.29f));
            }

            BuildShell(art);
            BuildGate(art);
            DressActors();
            HidePlaceholders(ammoPickup.gameObject);
            AddModel("chest", ammoPickup, new Vector3(0f, -0.46f, 0f), Vector3.one * 1.6f);
            HidePlaceholders(medkitPickup.gameObject);
            AddModel("potion", medkitPickup, new Vector3(0f, -0.46f, 0f), Vector3.one * 1.6f);
            AddModel("barrel", art, new Vector3(-7.4f, 0f, -4.2f), Vector3.one * 1.7f);
            AddModel("barrel", art, new Vector3(-7.0f, 0f, -3.1f), Vector3.one * 1.45f);
            AddModel("chest", art, new Vector3(7.3f, 0f, 7.8f), Vector3.one * 1.6f, 35f);
            AddModel("banner", art, new Vector3(-8.1f, 1.15f, 2.5f), Vector3.one * 1.7f, 90f);
            AddModel("banner", art, new Vector3(8.1f, 1.15f, 2.5f), Vector3.one * 1.7f, -90f);
        }

        private void BuildShell(Transform art)
        {
            foreach (var placeholder in shellPlaceholders) HidePlaceholders(placeholder);
            const float tileWidth = 1.9f;
            const float tileLength = 2f;
            for (int row = 0; row < 14; row++)
            for (int column = 0; column < 10; column++)
            {
                string tile = (row * 13 + column * 7) % 11 == 0 ? "floor-detail" : "floor";
                AddModel(tile, art, new Vector3(-8.55f + column * tileWidth, 0.003f, -9f + row * tileLength),
                    new Vector3(tileWidth, 1f, tileLength));
            }

            for (int i = 0; i < 10; i++)
            {
                float x = -8.55f + i * tileWidth;
                AddModel("wall", art, new Vector3(x, 0f, 17f), new Vector3(tileWidth, 2.2f, 0.6f));
                AddModel("wall", art, new Vector3(x, 0f, -9.7f), new Vector3(tileWidth, 1.4f, 0.6f), 180f);
            }

            for (int i = 0; i < 14; i++)
            {
                float z = -9f + i * tileLength;
                AddModel("wall", art, new Vector3(-9.37f, 0f, z), new Vector3(tileLength, 1.9f, 0.6f), 90f);
                AddModel("wall", art, new Vector3(9.37f, 0f, z), new Vector3(tileLength, 1.9f, 0.6f), -90f);
            }

            AddBlocker("Entrance wall collision", art, new Vector3(0f, 0.8f, -9.7f), new Vector3(19f, 1.6f, 0.5f));
        }

        private void BuildGate(Transform art)
        {
            AddModel("gate", gate, Vector3.zero, new Vector3(6f, 3.1f, 2f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddBlocker("Gate side wall collision", art, new Vector3(side * 6f, 1.2f, gate.position.z),
                    new Vector3(7.2f, 2.4f, 0.5f));
                for (int i = 0; i < 4; i++)
                    AddModel("wall", art, new Vector3(side * (3.3f + i * 1.8f), 0f, gate.position.z),
                        new Vector3(1.8f, 2.2f, 0.55f));

                AddModel("column", art, new Vector3(side * 2.52f, 0f, gate.position.z), Vector3.one * 2.3f);
                AddModel("column", art, new Vector3(side * 1.5f, 0f, extraction.position.z), Vector3.one * 1.4f);
            }

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Extraction marker";
            marker.transform.SetParent(art, false);
            marker.transform.SetPositionAndRotation(extraction.position + Vector3.up * 0.025f, Quaternion.identity);
            marker.transform.localScale = new Vector3(2.6f, 0.025f, 2.6f);
            marker.GetComponent<Collider>().enabled = false;
            if (markerMaterial != null) marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        }

        private void DressActors()
        {
            HidePlaceholders(playerVisual);
            playerVisual.transform.localPosition = Vector3.zero;
            playerVisual.transform.localScale = Vector3.one;
            var human = AddActorModel(playerVisual.transform, "human", 1.65f, out string humanMotion);

            HidePlaceholders(sword.gameObject);
            var swordModel = AddModel("weapon-sword", sword, new Vector3(0f, -0.12f, 0f), Vector3.one * 1.35f, 15f);
            var player = playerVisual.GetComponentInParent<FpsPlayer>(true);
            if (player != null && human != null)
            {
                var motion = player.gameObject.AddComponent<RpgDungeonActorMotion>();
                motion.Initialize(human, humanMotion, false);
                foreach (var weapon in player.GetComponentsInChildren<FpsWeapon>(true))
                {
                    var renderer = weapon.GetComponentInChildren<Renderer>(true);
                    Transform visual = weapon.transform == sword && swordModel != null
                        ? swordModel.transform : renderer != null ? renderer.transform : null;
                    motion.BindWeapon(weapon, visual);
                }
            }

            foreach (var guard in guards)
            {
                HidePlaceholders(guard);
                var orc = AddActorModel(guard.transform, "orc", 1.64f, out string orcMotion);
                if (orc == null) continue;

                var motion = guard.AddComponent<RpgDungeonActorMotion>();
                motion.Initialize(orc, orcMotion, true);
                var weapon = guard.GetComponentInChildren<FpsWeapon>();
                if (weapon == null) continue;
                var blade = AddModel("weapon-sword", weapon.transform, new Vector3(0.41f, -0.18f, 0.47f),
                    Vector3.one * 1.42f, -20f);
                if (blade != null) motion.BindWeapon(weapon, blade.transform);
            }
        }

        private Transform AddActorModel(Transform parent, string kind, float height, out string motionResource)
        {
            var pose = new GameObject("Character Pose").transform;
            pose.SetParent(parent, false);
            string asset = "actor-" + kind;
            var model = AddModel(asset, pose, Vector3.zero, Vector3.one);
            motionResource = model != null ? "EmberVault/" + asset : null;
            if (model == null) model = AddModel("character-" + kind, pose, Vector3.zero, Vector3.one);
            if (model == null)
            {
                Destroy(pose.gameObject);
                return null;
            }

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return pose;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.01f) return pose;
            model.transform.localScale = Vector3.one * (height / bounds.size.y);
            return pose;
        }

        private GameObject AddModel(string assetName, Transform parent, Vector3 position, Vector3 scale,
            float yaw = 0f)
        {
            if (!models.TryGetValue(assetName, out var source))
            {
                source = Resources.Load<GameObject>("EmberVault/" + assetName);
                models.Add(assetName, source);
            }
            if (source == null) return null;

            var model = Instantiate(source, parent, false);
            model.name = assetName;
            model.transform.localPosition = position;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = scale;
            if (atlasMaterial != null)
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = atlasMaterial;
            }
            return model;
        }

        private static void HidePlaceholders(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        private static void AddBlocker(string label, Transform parent, Vector3 position, Vector3 size)
        {
            var blocker = new GameObject(label);
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = position;
            blocker.AddComponent<BoxCollider>().size = size;
        }

        private void OnDestroy()
        {
            if (atlasMaterial != null) Destroy(atlasMaterial);
            if (markerMaterial != null) Destroy(markerMaterial);
        }
    }
}
