using System;
using Cysharp.Threading.Tasks;
using FrameWork.Script.WebNet;
using UnityEngine;

namespace FrameWork.Script.Tool
{
    enum MoveType
    {
        MoveToPos,//位移到指定位置
        MoveToDis,//向指定方向移动
        MoveToTransform,//持续追踪一个可移动目标
        MoveParabola,//沿抛物线移动到指定位置
        MoveAccelerated,//加速或减速直线移动
        MoveBezier,//二次贝塞尔曲线移动
        MoveOrbit,//围绕目标旋转
        MoveSine,//正弦波浪移动
        MoveIcathianRain,//先向外散开，再弯曲追踪指定目标
        MoveAttract,//在指定时间内持续向固定位置吸附
        DetectCollision,//保持原位，仅在指定时间内检测碰撞
    }
    public class MoveTool : WebNetworkBehaviour
    {
        private MoveType _moveType;
        private float _moveSpeed;
        private bool _isCanEx;
        private Func<UniTask> _moveAction;
        private Vector3 _targetPos;
        private float _maxDistance;
        private Func<Collider2D, UniTask> _coll;
        private Vector3 _dis;
        private Transform _targetTransform;
        private WebNetworkIdentity _targetIdentity;
        private float _stopDistance;
        private Vector3 _startPos;
        private float _moveDuration;
        private float _moveElapsed;
        private float _parabolaHeight;
        private float _acceleration;
        private float _minSpeed;
        private float _maxSpeed;
        private float _travelledDistance;
        private Vector3 _controlPos;
        private Transform _orbitCenter;
        private float _orbitRadius;
        private float _orbitAngle;
        private float _angularSpeed;
        private Vector3 _forward;
        private Vector3 _side;
        private float _sineAmplitude;
        private float _sineFrequency;
        private Vector3 _scatterEndPos;
        private float _scatterDuration;
        private float _homingDuration;
        private float _homingCurve;
        private bool _homingPhase;
        private bool _loggedFirstMoveFrame;
        private LayerMask _layerMask;


        public void SetMask(LayerMask mask)
        {
            _layerMask = mask;
        }

        public void SetNotMask()
        {
            _layerMask = 0;
        }
        
        public void MoveTo(Vector3 target, float moveSpeed, Func<UniTask> end = null)
        {
            ResetMoveState();
            _targetPos=target;
            _isCanEx = true;
            _moveType = MoveType.MoveToPos;
            _moveSpeed=moveSpeed;
            _moveAction = end;
        }

        /// <summary>
        /// 保持当前位置不移动，在 duration 秒内持续检测碰撞。
        /// 每次有效碰撞都会调用 coll，时间结束后停止检测并调用 end。
        /// duration 受 Time.timeScale 影响；小于或等于 0 时立即调用 end。
        /// 可用 StopMove 提前停止。
        /// </summary>
        public void DetectCollision(float duration, Func<Collider2D, UniTask> coll,
            Func<UniTask> end = null)
        {
            ResetMoveState();
            _moveDuration = Mathf.Max(0f, duration);
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.DetectCollision;
            _isCanEx = true;
            if (_moveDuration <= 0f)
                CompleteMove();
        }

        /// <summary>
        /// 持续向相对父物体的局部坐标 target 吸附，moveSpeed 为每秒移动的局部距离。
        /// duration 为从调用开始的总持续秒数（受 Time.timeScale 影响），
        /// 到达目标后仍保持吸附，时间结束才调用 end；时间到时未到达也会停止。
        /// duration 小于或等于 0 时立即结束，不移动。可用 StopMove 提前停止。
        /// </summary>
        public void MoveAttract(Vector3 target, float moveSpeed, float duration,
            Func<UniTask> end = null)
        {
            ResetMoveState();
            _targetPos = target;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            _moveDuration = Mathf.Max(0f, duration);
            _moveAction = end;
            _moveType = MoveType.MoveAttract;
            _isCanEx = true;
            if (_moveDuration <= 0f)
                CompleteMove();
        }

        public void MoveToDis(Vector3 dis, float moveSpeed, float maxDistance,
            Func<Collider2D, UniTask> coll, Func<UniTask> end = null)
        {
            ResetMoveState();
            _dis=dis.normalized;
            _isCanEx = true;
            _moveType = MoveType.MoveToDis;
            _moveSpeed=moveSpeed;
            _moveAction = end;
            _coll = coll;
            _maxDistance = maxDistance;
            _targetPos = transform.position + _dis * _maxDistance;
        }

        /// <summary>
        /// 持续追踪一个会移动的目标。到达 stopDistance 时调用 end，
        /// 若移动途中触发目标碰撞，则调用 coll。
        /// </summary>
        public void MoveToTran(Transform target, float moveSpeed,
            Func<Collider2D, UniTask> coll = null, Func<UniTask> end = null,
            float stopDistance = 0.1f)
        {
            ResetMoveState();
            if (target == null)
            {
                InvokeAsyncCallback(end);
                return;
            }

            _targetTransform = target;
            _targetIdentity = target.GetComponentInParent<WebNetworkIdentity>();
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            _stopDistance = Mathf.Max(0f, stopDistance);
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveToTransform;
            _isCanEx = true;
        }

        /// <summary>
        /// 从当前世界坐标沿抛物线移动到 target。
        /// duration 为飞行时间，height 为相对于直线路径的最大抬高。
        /// </summary>
        public void MoveParabola(Vector3 target, float duration, float height,
            Func<Collider2D, UniTask> coll = null, Func<UniTask> end = null)
        {
            ResetMoveState();
            _startPos = transform.position;
            _targetPos = target;
            _moveDuration = Mathf.Max(0.01f, duration);
            _parabolaHeight = height;
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveParabola;
            _isCanEx = true;
        }

        /// <summary>
        /// 沿 direction 做加速或减速直线移动。acceleration 可为负数，
        /// 速度会被限制在 minSpeed 和 maxSpeed 之间。
        /// </summary>
        public void MoveAccelerated(Vector3 direction, float initialSpeed, float acceleration,
            float maxDistance, Func<Collider2D, UniTask> coll = null,
            Func<UniTask> end = null,
            float minSpeed = 0f, float maxSpeed = float.MaxValue)
        {
            ResetMoveState();
            _dis = direction.normalized;
            _moveSpeed = Mathf.Clamp(initialSpeed, minSpeed, maxSpeed);
            _acceleration = acceleration;
            _maxDistance = Mathf.Max(0f, maxDistance);
            _minSpeed = Mathf.Max(0f, minSpeed);
            _maxSpeed = Mathf.Max(_minSpeed, maxSpeed);
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveAccelerated;
            _isCanEx = _dis.sqrMagnitude > 0f;
            if (!_isCanEx)
                CompleteMove();
        }

        /// <summary>从当前位置经过 control 沿二次贝塞尔曲线移动到 target。</summary>
        public void MoveBezier(Vector3 control, Vector3 target, float duration,
            Func<Collider2D, UniTask> coll = null, Func<UniTask> end = null)
        {
            ResetMoveState();
            _startPos = transform.position;
            _controlPos = control;
            _targetPos = target;
            _moveDuration = Mathf.Max(0.01f, duration);
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveBezier;
            _isCanEx = true;
        }

        /// <summary>
        /// 围绕 center 旋转。angularSpeed 单位为度/秒，duration 小于或等于 0 时一直旋转，
        /// 直到调用 StopMove 或发生碰撞。
        /// </summary>
        public void MoveOrbit(Transform center, float radius, float angularSpeed,
            float duration = 0f, Func<Collider2D, UniTask> coll = null,
            Func<UniTask> end = null)
        {
            ResetMoveState();
            if (center == null)
            {
                InvokeAsyncCallback(end);
                return;
            }

            _orbitCenter = center;
            _orbitRadius = Mathf.Max(0f, radius);
            _angularSpeed = angularSpeed;
            _moveDuration = duration;
            Vector3 offset = transform.position - center.position;
            _orbitAngle = offset.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg
                : 0f;
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveOrbit;
            _isCanEx = true;
        }

        /// <summary>
        /// 沿 direction 前进，同时沿垂直方向做正弦摆动。frequency 为每秒完整波形数。
        /// </summary>
        public void MoveSine(Vector3 direction, float speed, float distance,
            float amplitude, float frequency, Func<Collider2D, UniTask> coll = null,
            Func<UniTask> end = null)
        {
            ResetMoveState();
            _startPos = transform.position;
            _forward = direction.normalized;
            _side = new Vector3(-_forward.y, _forward.x, 0f);
            _moveSpeed = Mathf.Max(0f, speed);
            _maxDistance = Mathf.Max(0f, distance);
            _sineAmplitude = amplitude;
            _sineFrequency = frequency;
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveSine;
            _isCanEx = _forward.sqrMagnitude > 0f;
            if (!_isCanEx)
                CompleteMove();
        }

        /// <summary>
        /// 爱卡西亚暴雨式导弹：先沿 scatterDirection 向外散开，然后沿曲线追踪 target。
        /// </summary>
        /// <param name="target">飞向哪个玩家。</param>
        /// <param name="scatterDirection">最开始向外散开的方向，需要方向向量。</param>
        /// <param name="scatterDistance">先散开多远，建议 0.3f～0.8f。</param>
        /// <param name="scatterTime">散开耗时，建议 0.1f～0.25f。</param>
        /// <param name="homingTime">飞到玩家身上的时间，建议 0.25f～0.5f。</param>
        /// <param name="homingCurve">弯曲幅度，正负决定左右方向，建议 -0.5f～0.5f。</param>
        /// <param name="coll"></param>
        /// <param name="end"></param>
        public void MoveIcathianRain(Transform target, Vector3 scatterDirection,
            float scatterDistance, float scatterTime, float homingTime, float homingCurve,
            Func<Collider2D, UniTask> coll = null, Func<UniTask> end = null)
        {
            ResetMoveState();
            if (target == null)
            {
                InvokeAsyncCallback(end);
                return;
            }

            Vector3 direction = scatterDirection.normalized;
            if (direction.sqrMagnitude <= 0f)
                direction = Vector3.up;

            _startPos = transform.position;
            _scatterEndPos = _startPos + direction * Mathf.Max(0f, scatterDistance);
            _scatterDuration = Mathf.Max(0.01f, scatterTime);
            _homingDuration = Mathf.Max(0.01f, homingTime);
            _homingCurve = homingCurve;
            _targetTransform = target;
            _targetIdentity = target.GetComponentInParent<WebNetworkIdentity>();
            _coll = coll;
            _moveAction = end;
            _moveType = MoveType.MoveIcathianRain;
            _isCanEx = true;
        }

        /// <summary>停止当前移动，默认不触发结束回调。</summary>
        public void StopMove(bool invokeEnd = false)
        {
            if (!_isCanEx)
                return;

            Func<UniTask> end = _moveAction;
            ResetMoveState();
            if (invokeEnd)
                InvokeAsyncCallback(end);
        }


        private void FixedUpdate()
        {
            if (!HasAuthority)return;
            if (!_isCanEx)return;
            if (!_loggedFirstMoveFrame)
            {
                _loggedFirstMoveFrame = true;
                //Debug.Log($"[MoveTool] 开始移动 objectId={NetId}, type={_moveType}, position={transform.position}", this);
            }
            switch (_moveType)
            {
                case MoveType.MoveToPos:
                    UpdateMoveToPos();
                break;
                case MoveType.MoveToDis:
                    UpdateMoveToDis();
                    break;
                case MoveType.MoveToTransform:
                    UpdateMoveToTransform();
                    break;
                case MoveType.MoveParabola:
                    UpdateMoveParabola();
                    break;
                case MoveType.MoveAccelerated:
                    UpdateMoveAccelerated();
                    break;
                case MoveType.MoveBezier:
                    UpdateMoveBezier();
                    break;
                case MoveType.MoveOrbit:
                    UpdateMoveOrbit();
                    break;
                case MoveType.MoveSine:
                    UpdateMoveSine();
                    break;
                case MoveType.MoveIcathianRain:
                    UpdateMoveIcathianRain();
                    break;
                case MoveType.MoveAttract:
                    UpdateMoveAttract();
                    break;
                case MoveType.DetectCollision:
                    UpdateDetectCollision();
                    break;
            } 
        }

        private void OnDisable()
        {
            ResetMoveState();
        }


        private void UpdateMoveToPos()
        {
            transform.position=Vector3.MoveTowards(transform.position, _targetPos, _moveSpeed * Time.fixedDeltaTime);
            if (Vector3.Distance(transform.position, _targetPos) < 0.1f)
            {
                CompleteMove();
            }
        }


        private void UpdateMoveAttract()
        {
            float stepTime = Mathf.Min(Time.fixedDeltaTime, _moveDuration - _moveElapsed);
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, _targetPos, _moveSpeed * stepTime);
            _moveElapsed += stepTime;

            // 到达目标不提前结束，持续时间内被移开时仍会继续吸附。
            if (_moveElapsed >= _moveDuration)
                CompleteMove();
        }

        private void UpdateDetectCollision()
        {
            _moveElapsed += Time.fixedDeltaTime;
            if (_moveElapsed >= _moveDuration)
                CompleteMove();
        }

        private void UpdateMoveToDis()
        {
            transform.position = Vector3.MoveTowards(
                transform.position, 
                _targetPos,
                _moveSpeed * Time.fixedDeltaTime
            );

            if (Vector3.Distance(transform.position, _targetPos) < 0.1f)
            {
                CompleteMove();
            }
        }

        private void UpdateMoveToTransform()
        {
            if (_targetTransform == null || !_targetTransform.gameObject.activeInHierarchy)
            {
                CompleteMove();
                return;
            }

            Vector3 targetPosition = _targetTransform.position;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                _moveSpeed * Time.fixedDeltaTime);

            if (Vector3.Distance(transform.position, targetPosition) <= _stopDistance)
                CompleteMove();
        }

        private void UpdateMoveParabola()
        {
            _moveElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_moveElapsed / _moveDuration);
            Vector3 position = Vector3.Lerp(_startPos, _targetPos, progress);
            position += Vector3.up * (4f * _parabolaHeight * progress * (1f - progress));
            transform.position = position;

            if (progress >= 1f)
                CompleteMove();
        }

        private void UpdateMoveAccelerated()
        {
            _moveSpeed = Mathf.Clamp(
                _moveSpeed + _acceleration * Time.fixedDeltaTime,
                _minSpeed,
                _maxSpeed);
            float step = Mathf.Min(_moveSpeed * Time.fixedDeltaTime,
                _maxDistance - _travelledDistance);
            transform.position += _dis * step;
            _travelledDistance += step;

            if (_travelledDistance >= _maxDistance ||
                _moveSpeed <= 0f && _acceleration <= 0f)
                CompleteMove();
        }

        private void UpdateMoveBezier()
        {
            _moveElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_moveElapsed / _moveDuration);
            float inverse = 1f - progress;
            transform.position = inverse * inverse * _startPos +
                                 2f * inverse * progress * _controlPos +
                                 progress * progress * _targetPos;
            if (progress >= 1f)
                CompleteMove();
        }

        private void UpdateMoveOrbit()
        {
            if (_orbitCenter == null || !_orbitCenter.gameObject.activeInHierarchy)
            {
                CompleteMove();
                return;
            }

            _moveElapsed += Time.fixedDeltaTime;
            _orbitAngle += _angularSpeed * Time.fixedDeltaTime;
            float radians = _orbitAngle * Mathf.Deg2Rad;
            transform.position = _orbitCenter.position + new Vector3(
                Mathf.Cos(radians) * _orbitRadius,
                Mathf.Sin(radians) * _orbitRadius,
                0f);

            if (_moveDuration > 0f && _moveElapsed >= _moveDuration)
                CompleteMove();
        }

        private void UpdateMoveSine()
        {
            _moveElapsed += Time.fixedDeltaTime;
            _travelledDistance = Mathf.Min(
                _travelledDistance + _moveSpeed * Time.fixedDeltaTime,
                _maxDistance);
            float wave = Mathf.Sin(_moveElapsed * _sineFrequency * Mathf.PI * 2f) *
                         _sineAmplitude;
            transform.position = _startPos + _forward * _travelledDistance + _side * wave;

            if (_travelledDistance >= _maxDistance)
                CompleteMove();
        }

        private void UpdateMoveIcathianRain()
        {
            if (_targetTransform == null || !_targetTransform.gameObject.activeInHierarchy)
            {
                CompleteMove();
                return;
            }

            _moveElapsed += Time.fixedDeltaTime;
            if (!_homingPhase)
            {
                float scatterProgress = Mathf.Clamp01(_moveElapsed / _scatterDuration);
                // 散开阶段使用缓出：初速快，转向追踪时速度放缓。
                float eased = 1f - (1f - scatterProgress) * (1f - scatterProgress);
                transform.position = Vector3.LerpUnclamped(_startPos, _scatterEndPos, eased);

                if (scatterProgress < 1f)
                    return;

                _homingPhase = true;
                _moveElapsed = 0f;
                _startPos = transform.position;

                Vector3 toTarget = (_targetTransform.position - _startPos).normalized;
                Vector3 side = new Vector3(-toTarget.y, toTarget.x, 0f);
                _controlPos = _startPos +
                              toTarget * Vector3.Distance(_startPos, _targetTransform.position) * 0.35f +
                              side * _homingCurve;
                return;
            }

            float homingProgress = Mathf.Clamp01(_moveElapsed / _homingDuration);
            float inverse = 1f - homingProgress;
            // 终点每帧读取，因此敌人在导弹飞行期间移动仍会被追踪。
            Vector3 targetPosition = _targetTransform.position;
            transform.position = inverse * inverse * _startPos +
                                 2f * inverse * homingProgress * _controlPos +
                                 homingProgress * homingProgress * targetPosition;

            if (homingProgress >= 1f)
                CompleteMove();
        }


        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasAuthority || !_isCanEx || _coll == null)return;
            var id = other.GetComponentInParent<WebNetworkIdentity>();
            if (id==null)return;
            // var roleId = other.GetComponentInParent<RoleId>();
            // if(roleId==null)return;
            if (_moveType == MoveType.MoveToTransform && _targetIdentity != null)
            {
                if (id != _targetIdentity)
                    return;
            }
            else if (_moveType == MoveType.MoveIcathianRain && _targetIdentity != null)
            {
                if (id != _targetIdentity)
                    return;
            }
            int layerMaskForObject = 1 << other.gameObject.layer;
            // 2. 按位与(&)操作，判断是否有重合
            // 如果结果不为0，说明这个层在LayerMask里
            if ((_layerMask.value & layerMaskForObject) != 0)
            {
                Func<Collider2D, UniTask> collision = _coll;
                // 原地碰撞检测需要持续到 duration 结束，其余移动仍在首次命中时结束。
                if (_moveType != MoveType.DetectCollision)
                    ResetMoveState();
                InvokeAsyncCallback(collision, other);
            }
        }

        private void CompleteMove()
        {
            Func<UniTask> end = _moveAction;
            ResetMoveState();
            InvokeAsyncCallback(end);
        }

        private static void InvokeAsyncCallback(Func<UniTask> callback)
        {
            if (callback == null)
                return;

            callback().Forget(Debug.LogException);
        }

        private static void InvokeAsyncCallback(
            Func<Collider2D, UniTask> callback, Collider2D collider)
        {
            if (callback == null)
                return;

            callback(collider).Forget(Debug.LogException);
        }

        private void ResetMoveState()
        {
            _isCanEx = false;
            _moveAction = null;
            _coll = null;
            _targetTransform = null;
            _targetIdentity = null;
            _orbitCenter = null;
            _moveElapsed = 0f;
            _travelledDistance = 0f;
            _homingPhase = false;
            _loggedFirstMoveFrame = false;
        }
    }
}
