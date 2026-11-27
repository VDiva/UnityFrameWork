using GameData;
namespace WebSocketDemo;

/// <summary>共享入口校验，防止异常数值进入转发及初始快照缓存。</summary>
public static class NetworkStateValidation
{
    public const int MaxChildren = 128;
    public const int MaxAnimationTracks = 32;
    public const float MaxSustainedSpeed = 60f;
    public const float MaxBurstAllowance = 60f;
    private const float NearLodDistSq = 18f * 18f;
    private const float MidLodDistSq = 40f * 40f;

    static bool Vector(float x, float y, float z) => float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z);

    /// <summary>
    /// 令牌桶位移速率包络校验：仅在携带有效单调 SentTime 时启用，允许瞬时冲刺/闪现/击退预算，拦截持续加速挂与全图瞬移挂。
    /// </summary>
    public static bool IsWithinSpeedEnvelope(
        NetworkTransformData? previous,
        NetworkTransformData current,
        ref float burstAllowance)
    {
        if (previous == null ||
            previous.RoomId != current.RoomId ||
            previous.SceneEpoch != current.SceneEpoch ||
            previous.SentTime <= 0.0 ||
            current.SentTime <= previous.SentTime)
        {
            burstAllowance = MaxBurstAllowance;
            return true;
        }

        double dt = Math.Clamp(current.SentTime - previous.SentTime, 0.005, 5.0);
        float refilled = Math.Min(MaxBurstAllowance, burstAllowance + (float)(dt * 15.0));

        float dx = current.PositionX - previous.PositionX;
        float dy = current.PositionY - previous.PositionY;
        float dz = current.PositionZ - previous.PositionZ;
        float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        float baseBudget = (float)(dt * MaxSustainedSpeed);
        if (dist <= baseBudget)
        {
            burstAllowance = refilled;
            return true;
        }

        float excess = dist - baseBudget;
        if (excess <= refilled)
        {
            burstAllowance = refilled - excess;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 房间内按距离动态 LOD 抽样分频：近距全频（1/1）、中距半频（1/2）、远距四分之一频（1/4）；首帧与静止停步帧始终立即放行。
    /// </summary>
    public static bool ShouldForwardByDistanceLod(
        NetworkTransformData? observerPos,
        NetworkTransformData targetPos)
    {
        if (observerPos == null || targetPos.Sequence <= 2)
            return true;

        float dx = targetPos.PositionX - observerPos.PositionX;
        float dy = targetPos.PositionY - observerPos.PositionY;
        float dz = targetPos.PositionZ - observerPos.PositionZ;
        float distSq = dx * dx + dy * dy + dz * dz;
        if (distSq <= NearLodDistSq)
            return true;

        // 停步关键帧（速度归零）不抽帧，确保远端立即停在准确终点。
        if (targetPos.HasVelocity)
        {
            float velSq = targetPos.VelocityX * targetPos.VelocityX +
                          targetPos.VelocityY * targetPos.VelocityY +
                          targetPos.VelocityZ * targetPos.VelocityZ;
            if (velSq < 0.0025f)
                return true;
        }

        if (distSq <= MidLodDistSq)
            return (targetPos.Sequence & 1UL) == 0;

        return (targetPos.Sequence & 3UL) == 0;
    }
    public static bool IsValid(NetworkTransformData? s) => s != null &&
        Vector(s.PositionX, s.PositionY, s.PositionZ) &&
        Vector(s.RotationX, s.RotationY, s.RotationZ) && float.IsFinite(s.RotationW) &&
        (!s.HasScale || Vector(s.ScaleX, s.ScaleY, s.ScaleZ)) &&
        (!s.HasVelocity || Vector(s.VelocityX, s.VelocityY, s.VelocityZ)) &&
        double.IsFinite(s.SentTime) && s.Children.Count <= MaxChildren &&
        s.Children.All(c => Vector(c.PositionX, c.PositionY, c.PositionZ) &&
            Vector(c.RotationX, c.RotationY, c.RotationZ) && float.IsFinite(c.RotationW) &&
            Vector(c.ScaleX, c.ScaleY, c.ScaleZ));
    public static bool IsValid(NetworkAnimationData s) =>
        s.TrackIndex >= 0 && s.TrackIndex < MaxAnimationTracks &&
        float.IsFinite(s.NormalizedTime) && s.AnimationName.Length <= 256;
}
