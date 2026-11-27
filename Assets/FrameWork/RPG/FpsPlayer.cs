using UnityEngine;

namespace FrameWork.FPS
{
    public enum FpsViewMode { FirstPerson, ThirdPerson, TopDown }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpsPlayer : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private FpsWeapon weapon;
        [SerializeField] private FpsItemDefinition quickItem;
        [SerializeField, Min(0f)] private float walkSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 8f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField, Min(0.01f)] private float gravity = 20f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 2f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        [SerializeField] private bool startInThirdPerson;
        [SerializeField] private bool startInTopDown;
        [SerializeField, Min(0.1f)] private float topDownDistance = 12f;
        [SerializeField, Range(35f, 89f)] private float topDownPitch = 65f;
        [SerializeField] private float topDownYaw;
        [SerializeField, Min(0.1f)] private float thirdPersonDistance = 3.5f;
        [SerializeField] private Vector2 thirdPersonOffset = new Vector2(0.55f, 0.3f);
        [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.2f;
        [SerializeField] private LayerMask cameraCollisionLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private GameObject thirdPersonModel;
        [SerializeField] private Transform weaponAimPivot;

        private CharacterController controller;
        private FpsHealth health;
        private RpgSkillBar skillBar;
        public RpgSkillBar SkillBar => skillBar;
        private float verticalSpeed;
        private float pitch;
        private bool ownsCursor;
        private FpsLoadout loadout;
        private FpsInventory inventory;
        private Vector3 eyePosition;
        private bool viewReady;

        private static readonly RaycastHit[] CameraCollisionBuffer = new RaycastHit[8];
        public static FpsPlayer ActivePlayer { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActivePlayer() => ActivePlayer = null;

        public FpsWeapon EquippedWeapon => loadout != null ? loadout.EquippedWeapon : weapon;
        public FpsViewMode ViewMode { get; private set; }
        public bool IsThirdPerson => ViewMode == FpsViewMode.ThirdPerson;
        public bool IsTopDown => ViewMode == FpsViewMode.TopDown;

        private void OnEnable()
        {
            ActivePlayer = this;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<FpsHealth>();
            skillBar = GetComponent<RpgSkillBar>();
            loadout = GetComponent<FpsLoadout>();
            inventory = GetComponent<FpsInventory>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera == null || viewCamera.transform == transform ||
                !viewCamera.transform.IsChildOf(transform))
            {
                Debug.LogError("FPS 玩家需要绑定子节点上的相机。", this);
                enabled = false;
                return;
            }

            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewCamera.transform.localEulerAngles.x),
                -pitchLimit, pitchLimit);
            eyePosition = transform.InverseTransformPoint(viewCamera.transform.position);
            viewReady = true;
            if (startInTopDown) SetViewMode(FpsViewMode.TopDown);
            else SetThirdPerson(startInThirdPerson);
        }

        private void OnDisable()
        {
            if (ActivePlayer == this) ActivePlayer = null;
            if (ownsCursor) SetCursorCaptured(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && ownsCursor) SetCursorCaptured(false);
        }

        public void SetCursorCaptured(bool captured)
        {
            captured = captured && !IsTopDown;
            ownsCursor = captured;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && ownsCursor)
                SetCursorCaptured(false);

            bool acceptsInput = IsTopDown || (ownsCursor && Cursor.lockState == CursorLockMode.Locked);
            bool click = Input.GetMouseButtonDown(0);
            if (!acceptsInput && click && Time.timeScale > 0f)
            {
                SetCursorCaptured(true);
                // Unity 可能在本帧结束后才应用鼠标锁定，当前点击仍应触发开火。
                acceptsInput = true;
            }

            if (Time.timeScale <= 0f) return;

            Vector2 move = Vector2.zero;
            bool sprint = false;
            bool jump = false;
            if (acceptsInput)
            {
                if (Input.GetKeyDown(KeyCode.V)) CycleView();
                else if (!IsTopDown)
                    Look(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
                move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                sprint = Input.GetKey(KeyCode.LeftShift);
                jump = Input.GetButtonDown("Jump");
            }

            Move(move, sprint, jump, Time.deltaTime);
            UpdateView(aimAtMouse: acceptsInput && IsTopDown);
            if (!acceptsInput) return;
            UpdateEquipment(click);
            if (skillBar != null && (!IsTopDown || viewCamera.pixelRect.Contains(Input.mousePosition)))
                skillBar.ReadInput(GetAimRay());
        }

        public void SetQuickItem(FpsItemDefinition item) => quickItem = item;

        private void UpdateEquipment(bool click)
        {
            if (loadout != null)
            {
                for (int i = 0; i < Mathf.Min(9, loadout.SlotCount); i++)
                {
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    {
                        loadout.TryEquip(i);
                        break;
                    }
                }
            }

            if (inventory != null && Input.GetKeyDown(KeyCode.F)) inventory.TryUse(quickItem);

            var equipped = EquippedWeapon;
            if (equipped == null || !equipped.isActiveAndEnabled) return;
            if (Input.GetKeyDown(KeyCode.R)) equipped.TryReload();
            bool fire = click || (equipped.Automatic
                ? Input.GetMouseButton(0) || Input.GetButton("Fire1")
                : Input.GetButtonDown("Fire1"));
            if (!fire) return;
            if (IsTopDown && !viewCamera.pixelRect.Contains(Input.mousePosition)) return;
            equipped.TryFire(GetAimRay(), gameObject);
        }

        private Ray GetAimRay() => IsTopDown
            ? new Ray(transform.TransformPoint(eyePosition), transform.forward)
            : viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        private void Look(Vector2 look)
        {
            transform.Rotate(0f, look.x * mouseSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, -pitchLimit, pitchLimit);
        }

        public void SetThirdPerson(bool thirdPerson) =>
            SetViewMode(thirdPerson ? FpsViewMode.ThirdPerson : FpsViewMode.FirstPerson);

        public void SetViewMode(FpsViewMode mode)
        {
            ViewMode = mode;
            if (IsTopDown) SetCursorCaptured(false);
            if (thirdPersonModel != null) thirdPersonModel.SetActive(mode != FpsViewMode.FirstPerson);
            if (viewReady) UpdateView();
        }

        private void CycleView()
        {
            if (IsTopDown)
            {
                SetViewMode(FpsViewMode.FirstPerson);
                SetCursorCaptured(true);
            }
            else
            {
                SetViewMode(IsThirdPerson ? FpsViewMode.TopDown : FpsViewMode.ThirdPerson);
            }
        }

        private void UpdateView(bool aimAtMouse = false)
        {
            Vector3 eye = transform.TransformPoint(eyePosition);
            if (IsTopDown)
            {
                UpdateTopDownView(eye, aimAtMouse);
                return;
            }

            Quaternion aim = transform.rotation * Quaternion.Euler(pitch, 0f, 0f);
            Vector3 cameraPosition = eye;
            if (IsThirdPerson)
            {
                float followDistance = Mathf.Max(0.1f, thirdPersonDistance);
                Vector3 offset = aim * new Vector3(thirdPersonOffset.x, thirdPersonOffset.y, -followDistance);
                float distance = offset.magnitude;
                Vector3 direction = offset / distance;
                int hitCount = Physics.SphereCastNonAlloc(eye, cameraCollisionRadius, direction, CameraCollisionBuffer,
                    distance, cameraCollisionLayers, QueryTriggerInteraction.Ignore);
                float closest = distance;
                for (int i = 0; i < hitCount; i++)
                {
                    var hit = CameraCollisionBuffer[i];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                    float candidate = Mathf.Max(0f, hit.distance - 0.05f);
                    if (candidate < closest) closest = candidate;
                }
                cameraPosition += direction * closest;
            }

            viewCamera.transform.SetPositionAndRotation(cameraPosition, aim);
            if (weaponAimPivot != null) weaponAimPivot.rotation = aim;
        }

        private void UpdateTopDownView(Vector3 eye, bool aimAtMouse)
        {
            Quaternion cameraRotation = Quaternion.Euler(topDownPitch, topDownYaw, 0f);
            Vector3 focus = transform.position;
            focus.y = eye.y;
            Vector3 cameraPosition = focus - cameraRotation * Vector3.forward * Mathf.Max(0.1f, topDownDistance);
            viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);

            if (aimAtMouse && viewCamera.pixelRect.Contains(Input.mousePosition))
            {
                Ray mouseRay = viewCamera.ScreenPointToRay(Input.mousePosition);
                var ground = new Plane(Vector3.up, transform.position);
                if (ground.Raycast(mouseRay, out float distance))
                {
                    Vector3 direction = mouseRay.GetPoint(distance) - transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0025f)
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                        // 玩家转身后恢复子相机的世界姿态。
                        viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                    }
                }
            }

            if (weaponAimPivot != null) weaponAimPivot.rotation = transform.rotation;
        }

        private void Move(Vector2 input, bool sprint, bool jump, float deltaTime)
        {
            if (controller.isGrounded && verticalSpeed <= 0f)
                verticalSpeed = jump ? Mathf.Sqrt(2f * gravity * jumpHeight) : -2f;

            verticalSpeed -= gravity * deltaTime;
            input = Vector2.ClampMagnitude(input, 1f);
            Quaternion moveRotation = IsTopDown ? Quaternion.Euler(0f, topDownYaw, 0f) : transform.rotation;
            Vector3 movement = moveRotation * new Vector3(input.x, 0f, input.y) *
                (sprint ? sprintSpeed : walkSpeed) * (health != null ? health.MovementMultiplier : 1f);
            movement.y = verticalSpeed;
            CollisionFlags collision = controller.Move(movement * deltaTime);
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
        }
    }
}
