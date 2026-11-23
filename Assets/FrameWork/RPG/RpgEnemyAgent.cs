using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.FPS
{
    public enum RpgEnemyState { Idle, Pursuing, Attacking, Returning, Defeated }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(FpsHealth))]
    public sealed class RpgEnemyAgent : MonoBehaviour
    {
        [SerializeField] private RpgEnemyDefinition definition;
        [SerializeField] private RpgBehaviorTree behaviorTree;
        [SerializeField] private FpsWeapon weapon;
        [SerializeField] private Transform target;
        [SerializeField] private bool findPlayerOnStart = true;
        [SerializeField] private LayerMask sightLayers = Physics.DefaultRaycastLayers;

        private CharacterController motor;
        private FpsHealth health;
        private FpsHealth targetHealth;
        private CharacterController targetMotor;
        private Vector3 home;
        private float verticalSpeed;
        private RpgEnemyState state;
        private Vector3 movement;
        private bool? canAttackThisTick;
        private bool? canPursueThisTick;
        private bool? lineOfSightThisTick;
        private string lastTreeError;
        private List<RpgBehaviorTraceStep> trace;
        private Dictionary<string, string> traceParameters;

        public RpgEnemyState State => health != null && health.IsDepleted ? RpgEnemyState.Defeated : state;
        public RpgBehaviorTree BehaviorTree => behaviorTree;
        public RpgEnemyDefinition Definition => definition;
        public FpsWeapon Weapon => weapon;
        public Transform Target => target;
        public float TargetDistance => target != null ? HorizontalOffset(transform.position, target.position).magnitude : float.NaN;
        public float HomeDistance => HorizontalOffset(transform.position, home).magnitude;
        public bool? LastCanPursue => canPursueThisTick;
        public bool? LastCanAttack => canAttackThisTick;
        public bool? LastHasLineOfSight => lineOfSightThisTick;
        public string LastBehaviorError => lastTreeError;
        public bool IsTracingBehavior => trace != null;
        public int BehaviorTraceFrame { get; private set; } = -1;
        public IReadOnlyList<RpgBehaviorTraceStep> BehaviorTrace => trace;
        public IReadOnlyDictionary<string, string> BehaviorParameters => traceParameters;

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            health = GetComponent<FpsHealth>();
            home = transform.position;
        }

        private void Start()
        {
            if (target == null && findPlayerOnStart)
            {
                var player = FindObjectOfType<FpsPlayer>();
                if (player != null) target = player.transform;
            }
            SetTarget(target);
        }

        public void SetTarget(Transform next)
        {
            target = next;
            targetHealth = next != null ? next.GetComponentInParent<FpsHealth>() : null;
            targetMotor = next != null ? next.GetComponentInParent<CharacterController>() : null;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (health.IsDepleted || definition == null || !motor.enabled || deltaTime <= 0f) return;

            movement = Vector3.zero;
            canAttackThisTick = null;
            canPursueThisTick = null;
            lineOfSightThisTick = null;
            if (trace != null)
            {
                trace.Clear();
                traceParameters.Clear();
                BehaviorTraceFrame = Time.frameCount;
            }
            if (behaviorTree == null)
            {
                if (AttackTarget(deltaTime) == RpgBehaviorStatus.Failure &&
                    ChaseTarget(deltaTime) == RpgBehaviorStatus.Failure)
                    ReturnHome(deltaTime);
            }
            else if (!behaviorTree.TryTick(this, deltaTime, out var error))
            {
                state = RpgEnemyState.Idle;
                if (lastTreeError != error) Debug.LogWarning($"{behaviorTree.name}：{error}", this);
                lastTreeError = error;
            }
            else lastTreeError = null;

            if (motor.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed -= 20f * deltaTime;
            movement.y = verticalSpeed;
            motor.Move(movement * deltaTime);
        }

        public void SetBehaviorTracing(bool enabled)
        {
            if (enabled == IsTracingBehavior) return;
            trace = enabled ? new List<RpgBehaviorTraceStep>() : null;
            traceParameters = enabled ? new Dictionary<string, string>() : null;
            BehaviorTraceFrame = -1;
        }

        /// <summary>发布自定义动作这一帧的调试参数。</summary>
        public void ReportBehaviorParameter(string name, object value)
        {
            if (traceParameters == null || string.IsNullOrWhiteSpace(name)) return;
            traceParameters[name] = value?.ToString() ?? "null";
        }

        internal int BeginBehaviorTraceStep(RpgBehaviorNode node, RpgBehaviorNode parent)
        {
            if (trace == null) return -1;
            int index = trace.Count;
            trace.Add(new RpgBehaviorTraceStep(node, parent, RpgBehaviorStatus.Running));
            return index;
        }

        internal void EndBehaviorTraceStep(int index, RpgBehaviorStatus result)
        {
            if (trace == null || index < 0 || index >= trace.Count) return;
            var step = trace[index];
            trace[index] = new RpgBehaviorTraceStep(step.Node, step.Parent, result);
        }

        internal bool CheckCondition(RpgEnemyCondition condition)
        {
            return condition switch
            {
                RpgEnemyCondition.CanPursue => CanPursue(),
                RpgEnemyCondition.CanAttack => CanAttack(),
                RpgEnemyCondition.AwayFromHome => HorizontalOffset(transform.position, home).sqrMagnitude > 0.04f,
                _ => false
            };
        }

        internal RpgBehaviorStatus ExecuteAction(RpgEnemyAction action, float deltaTime)
        {
            switch (action)
            {
                case RpgEnemyAction.Chase: return ChaseTarget(deltaTime);
                case RpgEnemyAction.Attack: return AttackTarget(deltaTime);
                case RpgEnemyAction.ReturnHome: return ReturnHome(deltaTime);
                case RpgEnemyAction.Idle:
                    state = RpgEnemyState.Idle;
                    movement = Vector3.zero;
                    return RpgBehaviorStatus.Success;
                default: return RpgBehaviorStatus.Failure;
            }
        }

        private bool CanAttack()
        {
            if (canAttackThisTick.HasValue) return canAttackThisTick.Value;

            float range = definition.AttackRange;
            bool inRange = weapon != null && weapon.isActiveAndEnabled && CanPursue() &&
                HorizontalOffset(transform.position, target.position).sqrMagnitude <= range * range;
            if (!inRange)
            {
                canAttackThisTick = false;
                return false;
            }

            Vector3 origin = weapon.transform.position;
            Vector3 aim = TargetAimPoint() - origin;
            bool clear = false;
            if (aim.sqrMagnitude > 0.0001f)
            {
                clear = HasLineOfSight(origin, aim);
                lineOfSightThisTick = clear;
            }
            canAttackThisTick = clear;
            return clear;
        }

        private RpgBehaviorStatus AttackTarget(float deltaTime)
        {
            if (!CanAttack()) return RpgBehaviorStatus.Failure;

            Vector3 offset = HorizontalOffset(transform.position, target.position);
            TurnTowards(offset, deltaTime);
            state = RpgEnemyState.Attacking;
            if (Vector3.Dot(transform.forward, offset.normalized) >= 0.94f)
            {
                Vector3 origin = weapon.transform.position;
                weapon.TryFire(new Ray(origin, (TargetAimPoint() - origin).normalized), gameObject);
            }
            return RpgBehaviorStatus.Running;
        }

        private RpgBehaviorStatus ChaseTarget(float deltaTime)
        {
            if (!CanPursue()) return RpgBehaviorStatus.Failure;

            Vector3 offset = HorizontalOffset(transform.position, target.position);
            TurnTowards(offset, deltaTime);
            state = RpgEnemyState.Pursuing;
            movement = offset.normalized * definition.MoveSpeed;
            return RpgBehaviorStatus.Running;
        }

        private RpgBehaviorStatus ReturnHome(float deltaTime)
        {
            Vector3 offset = HorizontalOffset(transform.position, home);
            if (offset.sqrMagnitude <= 0.04f)
            {
                state = RpgEnemyState.Idle;
                return RpgBehaviorStatus.Success;
            }

            TurnTowards(offset, deltaTime);
            state = RpgEnemyState.Returning;
            movement = Vector3.ClampMagnitude(offset / deltaTime, definition.MoveSpeed);
            return RpgBehaviorStatus.Running;
        }

        private Vector3 TargetAimPoint() => targetMotor != null ? targetMotor.bounds.center : target.position;

        private bool CanPursue()
        {
            bool result = CanPursueCore();
            canPursueThisTick = result;
            return result;
        }

        private bool CanPursueCore()
        {
            if (target == null || !target.gameObject.activeInHierarchy ||
                (targetHealth != null && targetHealth.IsDepleted)) return false;

            float leash = definition.LeashRange;
            if (HorizontalOffset(home, target.position).sqrMagnitude > leash * leash ||
                HorizontalOffset(home, transform.position).sqrMagnitude > leash * leash)
                return false;

            if (state == RpgEnemyState.Pursuing || state == RpgEnemyState.Attacking) return true;

            float detection = definition.DetectionRange;
            return HorizontalOffset(transform.position, target.position).sqrMagnitude <= detection * detection;
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 aim)
        {
            float distance = aim.magnitude;
            if (!Physics.Raycast(origin, aim / distance, out var hit, distance, sightLayers,
                QueryTriggerInteraction.Ignore))
                return true;

            return hit.transform.IsChildOf(target) ||
                (targetMotor != null && hit.transform.IsChildOf(targetMotor.transform)) ||
                (targetHealth != null && hit.transform.IsChildOf(targetHealth.transform));
        }

        private void TurnTowards(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude <= 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                definition.TurnSpeed * deltaTime);
        }

        private static Vector3 HorizontalOffset(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset;
        }
    }
}
