using GameData;
namespace WebSocketDemo;

/// <summary>共享入口校验，防止异常数值进入转发及初始快照缓存。</summary>
public static class NetworkStateValidation
{
    public const int MaxChildren = 128;
    public const int MaxAnimationTracks = 32;
    static bool Vector(float x, float y, float z) => float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z);
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
