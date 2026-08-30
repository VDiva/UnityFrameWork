// ProgressBarController_Cutout.cs
// 使用 Update 驱动动画，无协程，零 GC 分配

using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ProgressBarController : MonoBehaviour
{
    [Header("进度设置")]
    [Range(0f, 1f)]
    public float targetProgress = 1f;
    [Range(0f, 1f)]
    public float currentProgress = 0f;
    
    [Header("未进度区域")]
    public bool showInactiveArea = false;
    public Color inactiveColor = new Color(0, 0, 0, 0.3f);
    
    [Header("动画设置")]
    public bool enableAnimation = true;
    public float animationDuration = 0.2f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool autoTest = false;
    public float testSpeed = 0.5f;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private int progressPropertyID;
    private int showInactivePropertyID;
    private int inactiveColorPropertyID;

    // 🔥 动画状态（用变量代替协程）
    private bool isAnimating = false;
    private float animationStartTime = 0f;
    private float animationStartProgress = 0f;
    private float animationEndProgress = 0f;
    private float lastTargetProgress = -1f;

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
        targetProgress = Mathf.Clamp01(targetProgress);
        currentProgress = Mathf.Clamp01(currentProgress);
        lastTargetProgress = targetProgress;
        ApplyProgress();
    }

    void Start()
    {
        currentProgress = targetProgress;
        lastTargetProgress = targetProgress;
        ApplyProgress();
    }

    void Update()
    {
        // 支持通过 Inspector 或其他旧代码直接修改 targetProgress。
        if (!autoTest && !Mathf.Approximately(targetProgress, lastTargetProgress))
        {
            SetProgress(targetProgress);
        }

        // 🔥 驱动动画（无协程）
        if (isAnimating)
        {
            UpdateAnimation();
        }

        // 自动测试
        if (autoTest)
        {
            targetProgress = Mathf.PingPong(Time.time * testSpeed, 1f);
            SetProgress(targetProgress);
        }
    }

    /// <summary>
    /// 设置目标进度（带动画）
    /// </summary>
    public void SetProgress(float value, bool useAnimation = true)
    {
        targetProgress = Mathf.Clamp01(value);
        lastTargetProgress = targetProgress;

        // 如果值相同，不重新动画
        if (Mathf.Approximately(currentProgress, targetProgress))
        {
            isAnimating = false;
            return;
        }

        // 不使用动画 或 动画功能关闭
        if (!useAnimation || !enableAnimation || animationDuration <= 0f)
        {
            currentProgress = targetProgress;
            isAnimating = false;
            ApplyProgress();
            return;
        }

        // 🔥 启动动画（记录状态，不创建协程）
        animationStartTime = Time.time;
        animationStartProgress = currentProgress;
        animationEndProgress = targetProgress;
        isAnimating = true;
    }

    /// <summary>
    /// 立即设置进度（无动画）
    /// </summary>
    public void SetProgressImmediate(float value)
    {
        targetProgress = Mathf.Clamp01(value);
        currentProgress = targetProgress;
        isAnimating = false;
        ApplyProgress();
    }

    /// <summary>
    /// 🔥 Update 中驱动动画
    /// </summary>
    private void UpdateAnimation()
    {
        float elapsed = Time.time - animationStartTime;
        float t = Mathf.Clamp01(elapsed / animationDuration);
        float curveValue = animationCurve.Evaluate(t);
        
        currentProgress = Mathf.Lerp(animationStartProgress, animationEndProgress, curveValue);
        ApplyProgress();

        // 动画结束
        if (t >= 1f)
        {
            currentProgress = animationEndProgress;
            isAnimating = false;
            ApplyProgress();
        }
    }

    private void ApplyProgress()
    {
        Initialize();

        if (spriteRenderer == null)
            return;

        if (spriteRenderer.sharedMaterial == null)
            return;

        Material material = spriteRenderer.sharedMaterial;
        if (material.shader == null || !material.HasProperty(progressPropertyID))
        {
            return;
        }

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(progressPropertyID, currentProgress);
        propertyBlock.SetFloat(showInactivePropertyID, showInactiveArea ? 1f : 0f);
        propertyBlock.SetColor(inactiveColorPropertyID, inactiveColor);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    private void Initialize()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        progressPropertyID = Shader.PropertyToID("_Progress");
        showInactivePropertyID = Shader.PropertyToID("_ShowInactiveArea");
        inactiveColorPropertyID = Shader.PropertyToID("_InactiveColor");
    }

    /// <summary>
    /// 切换未进度区域显示模式
    /// </summary>
    public void SetInactiveAreaMode(bool show, Color? color = null)
    {
        showInactiveArea = show;
        if (color.HasValue)
            inactiveColor = color.Value;
        ApplyProgress();
    }

    /// <summary>
    /// 停止当前动画
    /// </summary>
    public void StopAnimation()
    {
        isAnimating = false;
    }

    public float GetProgress() => currentProgress;
    public float GetTargetProgress() => targetProgress;
}
