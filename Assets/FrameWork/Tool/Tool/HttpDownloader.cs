using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public enum DownloadStatus { Idle, Waiting, Downloading, Paused, Completed, Failed, Cancelled }

/// <summary>先下载独立分段，校验响应范围后才合并，防止不支持 Range 的服务器损坏续传文件。</summary>
public class Downloader
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Url { get; }
    public string SavePath { get; }
    public string TempPath { get; }
    private string PartPath => TempPath + ".part";
    public DownloadStatus Status { get; private set; }
    public long TotalSize { get; private set; }
    public long DownloadedSize { get; private set; }
    public float Progress { get; private set; }
    public float DownloadSpeed { get; private set; }
    public int MaxRetryCount { get; set; } = 3;
    public float RetryDelay { get; set; } = 2f;
    public int BufferSize { get; set; } = 1024 * 1024;
    public string ErrorMessage { get; private set; }
    public event Action<Downloader> OnStart, OnProgress, OnComplete, OnPaused, OnResumed, OnCancelled;
    public event Action<Downloader, string> OnError;
    internal Action<Downloader> ScheduleResume;
    private UnityWebRequest _request;
    private Coroutine _downloadCoroutine;
    private MonoBehaviour _runner;
    private int _runVersion;
    private long _attemptStart;
    private float _lastUpdateTime;
    private long _lastDownloadedSize;

    public Downloader(string url, string savePath)
    {
        Url = url;
        SavePath = savePath;
        TempPath = savePath + ".tmp";
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    public void Start(MonoBehaviour coroutineRunner)
    {
        if (_downloadCoroutine != null || Status == DownloadStatus.Downloading || Status == DownloadStatus.Completed)
            return;
        if (coroutineRunner == null) throw new ArgumentNullException(nameof(coroutineRunner));
        _runner = coroutineRunner;
        Status = DownloadStatus.Downloading;
        ErrorMessage = null;
        _downloadCoroutine = _runner.StartCoroutine(DownloadCoroutine(++_runVersion));
    }

    public void Pause()
    {
        if (Status != DownloadStatus.Downloading && Status != DownloadStatus.Waiting) return;
        Status = DownloadStatus.Paused;
        StopAttempt(true);
        Notify(OnPaused);
    }

    public void Resume()
    {
        if (Status != DownloadStatus.Paused) return;
        Status = DownloadStatus.Waiting;
        Notify(OnResumed);
        if (Status != DownloadStatus.Waiting) return;
        if (ScheduleResume != null) ScheduleResume(this);
        else if (_runner != null) Start(_runner);
    }

    public void Cancel()
    {
        if (Status == DownloadStatus.Cancelled || Status == DownloadStatus.Completed) return;
        Status = DownloadStatus.Cancelled;
        StopAttempt(false);
        TryDelete(TempPath);
        TryDelete(PartPath);
        Notify(OnCancelled);
    }

    public void Reset()
    {
        Cancel();
        Status = DownloadStatus.Idle;
        Progress = 0;
        TotalSize = DownloadedSize = 0;
        DownloadSpeed = 0;
        ErrorMessage = null;
        TryDelete(TempPath);
        TryDelete(PartPath);
    }

    private IEnumerator DownloadCoroutine(int version)
    {
        // 避免协程同步结束后 Start 把已结束的句柄重新写回。
        yield return null;
        Notify(OnStart);
        int retries = 0;
        try
        {
            while (Status == DownloadStatus.Downloading && version == _runVersion)
            {
                UnityWebRequestAsyncOperation operation;
                string error = BeginAttempt(out operation);
                if (error == null)
                {
                    while (!operation.isDone)
                    {
                        UpdateProgress();
                        Notify(OnProgress);
                        if (Status != DownloadStatus.Downloading || version != _runVersion) yield break;
                        yield return null;
                    }
                    bool success = _request.result == UnityWebRequest.Result.Success;
                    error = success ? null : (_request.error ?? "下载请求失败。");
                    string commitError = FinishAttempt(true, success);
                    if (success) error = commitError;
                    if (error == null)
                    {
                        error = PublishFile();
                        if (error == null)
                        {
                            Status = DownloadStatus.Completed;
                            Progress = 1f;
                            TotalSize = DownloadedSize = new FileInfo(SavePath).Length;
                            _downloadCoroutine = null;
                            Notify(OnComplete);
                            yield break;
                        }
                    }
                }
                if (retries++ >= MaxRetryCount)
                {
                    Status = DownloadStatus.Failed;
                    ErrorMessage = error;
                    _downloadCoroutine = null;
                    NotifyError(error);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, RetryDelay));
            }
        }
        finally
        {
            if (version == _runVersion)
            {
                if (_request != null) FinishAttempt(false, false);
                _downloadCoroutine = null;
            }
        }
    }

    private string BeginAttempt(out UnityWebRequestAsyncOperation operation)
    {
        operation = null;
        try
        {
            _attemptStart = File.Exists(TempPath) ? new FileInfo(TempPath).Length : 0;
            DownloadedSize = _lastDownloadedSize = _attemptStart;
            TotalSize = 0;
            _lastUpdateTime = Time.realtimeSinceStartup;
            DownloadSpeed = 0;
            _request = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbGET);
            if (_attemptStart > 0) _request.SetRequestHeader("Range", $"bytes={_attemptStart}-");
            _request.downloadHandler = new DownloadHandlerFile(PartPath, false);
            operation = _request.SendWebRequest();
            return null;
        }
        catch (Exception error)
        {
            if (_request != null) FinishAttempt(false, false);
            return error.Message;
        }
    }

    private void UpdateProgress()
    {
        bool fullResponse = _request.responseCode == 200;
        long prefix = fullResponse ? 0 : _attemptStart;
        if (long.TryParse(_request.GetResponseHeader("Content-Length"), out var length) && length >= 0)
            TotalSize = prefix + length;
        DownloadedSize = prefix + (long)_request.downloadedBytes;
        Progress = TotalSize > 0 ? Mathf.Clamp01((float)DownloadedSize / TotalSize) : 0f;
        float now = Time.realtimeSinceStartup;
        if (now - _lastUpdateTime >= 0.5f)
        {
            DownloadSpeed = Math.Max(0L, DownloadedSize - _lastDownloadedSize) / 1024f / (now - _lastUpdateTime);
            _lastUpdateTime = now;
            _lastDownloadedSize = DownloadedSize;
        }
    }

    private void StopAttempt(bool preserve)
    {
        var coroutine = _downloadCoroutine;
        _downloadCoroutine = null;
        _runVersion++;
        // 先处理分段和释放文件，协程 finally 只会看到空请求。
        if (_request != null)
        {
            _request.Abort();
            FinishAttempt(preserve, false);
        }
        if (coroutine != null && _runner != null) _runner.StopCoroutine(coroutine);
    }

    private string FinishAttempt(bool preserve, bool completed)
    {
        var request = _request;
        _request = null;
        if (request == null) return null;
        long code = request.responseCode;
        string range = request.GetResponseHeader("Content-Range");
        string contentLength = request.GetResponseHeader("Content-Length");
        request.Dispose(); // DownloadHandlerFile 必须先关闭才能读取、移动分段。
        try
        {
            if (!preserve || !File.Exists(PartPath)) return "下载分段不存在。";
            long received = new FileInfo(PartPath).Length;
            if (completed && long.TryParse(contentLength, out var expected) && received != expected)
                return "下载分段长度不匹配。";
            if (code == 200)
            {
                // 服务器忽略 Range：本次是完整响应，替换旧临时文件。
                if (File.Exists(TempPath)) File.Delete(TempPath);
                File.Move(PartPath, TempPath);
            }
            else if (code == 206 && TryParseRange(range, out var start, out var end, out var total) &&
                start == _attemptStart && received <= end - start + 1 &&
                (!completed || (received == end - start + 1 && end + 1 == total)))
            {
                long existing = File.Exists(TempPath) ? new FileInfo(TempPath).Length : 0;
                if (existing != start) return "续传文件长度已变更。";
                using (var output = new FileStream(TempPath, FileMode.Append, FileAccess.Write))
                using (var input = File.OpenRead(PartPath))
                {
                    try { input.CopyTo(output, Math.Max(4096, BufferSize)); }
                    catch { output.SetLength(existing); throw; }
                }
            }
            else
            {
                // 范围无效时下次从头请求，绝不把响应拼到旧文件后。
                if (code == 206 || code == 416) TryDelete(TempPath);
                return "服务器返回无效的续传范围或响应状态：" + code;
            }
            return null;
        }
        catch (Exception error) { return error.Message; }
        finally { TryDelete(PartPath); }
    }

    private static bool TryParseRange(string value, out long start, out long end, out long total)
    {
        start = end = total = 0;
        if (string.IsNullOrEmpty(value) || !value.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = value.Substring(6).Split('-', '/');
        return parts.Length == 3 && long.TryParse(parts[0], out start) &&
            long.TryParse(parts[1], out end) && long.TryParse(parts[2], out total) &&
            start >= 0 && end >= start && total > end;
    }

    private string PublishFile()
    {
        try
        {
            if (!File.Exists(TempPath)) return "下载临时文件不存在。";
            if (File.Exists(SavePath)) File.Replace(TempPath, SavePath, null);
            else File.Move(TempPath, SavePath);
            return null;
        }
        catch (Exception error) { return error.Message; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception error) { Debug.LogException(error); }
    }

    private void Notify(Action<Downloader> handlers)
    {
        if (handlers == null) return;
        foreach (Action<Downloader> handler in handlers.GetInvocationList())
            try { handler(this); } catch (Exception error) { Debug.LogException(error); }
    }

    private void NotifyError(string message)
    {
        if (OnError == null) return;
        foreach (Action<Downloader, string> handler in OnError.GetInvocationList())
            try { handler(this, message); } catch (Exception error) { Debug.LogException(error); }
    }
}

/// <summary>
/// 下载管理器 - 管理多个Downloader实例
/// </summary>
public class DownloadManager : MonoBehaviour
{
    private static DownloadManager _instance;
    public static DownloadManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("DownloadManager");
                _instance = go.AddComponent<DownloadManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 配置
    [SerializeField] private int _maxConcurrent = 3;
    
    // 所有下载器
    private Dictionary<string, Downloader> _allDownloaders = new Dictionary<string, Downloader>();
    private Queue<Downloader> _waitingQueue = new Queue<Downloader>();
    private List<Downloader> _runningDownloaders = new List<Downloader>();
    
    // 属性
    public int MaxConcurrent 
    { 
        get => _maxConcurrent; 
        set => _maxConcurrent = Mathf.Max(1, value); 
    }
    
    public int RunningCount => _runningDownloaders.Count;
    public int WaitingCount => _waitingQueue.Count;
    public int TotalCount => _allDownloaders.Count;

    void Update()
    {
        ProcessQueue();
    }

    #region 公共方法 - 创建和管理下载器

    /// <summary>
    /// 创建下载器（自动开始）
    /// </summary>
    public Downloader CreateDownloader(string url, string savePath)
    {
        Downloader downloader = new Downloader(url, savePath);
        
        // 订阅事件
        downloader.OnStart += OnDownloaderStart;
        downloader.OnProgress += OnDownloaderProgress;
        downloader.OnComplete += OnDownloaderComplete;
        downloader.OnError += OnDownloaderError;
        downloader.OnPaused += OnDownloaderPaused;
        downloader.ScheduleResume = OnDownloaderResumed;
        downloader.OnCancelled += OnDownloaderCancelled;
        
        _allDownloaders[downloader.Id] = downloader;
        _waitingQueue.Enqueue(downloader);
        
        ProcessQueue();
        
        return downloader;
    }

    /// <summary>
    /// 批量创建下载器
    /// </summary>
    public List<Downloader> CreateDownloaders(string[] urls, string[] savePaths)
    {
        if (urls == null || savePaths == null || urls.Length != savePaths.Length)
        {
            Debug.LogError("参数错误：URLs和保存路径数量不匹配");
            return null;
        }
        
        List<Downloader> downloaders = new List<Downloader>();
        for (int i = 0; i < urls.Length; i++)
        {
            Downloader downloader = CreateDownloader(urls[i], savePaths[i]);
            downloaders.Add(downloader);
        }
        return downloaders;
    }

    /// <summary>
    /// 获取下载器
    /// </summary>
    public Downloader GetDownloader(string id)
    {
        _allDownloaders.TryGetValue(id, out Downloader downloader);
        return downloader;
    }

    /// <summary>
    /// 获取所有下载器
    /// </summary>
    public List<Downloader> GetAllDownloaders()
    {
        return new List<Downloader>(_allDownloaders.Values);
    }

    /// <summary>
    /// 获取运行中的下载器
    /// </summary>
    public List<Downloader> GetRunningDownloaders()
    {
        return new List<Downloader>(_runningDownloaders);
    }

    /// <summary>
    /// 暂停所有下载
    /// </summary>
    public void PauseAll()
    {
        foreach (var downloader in _allDownloaders.Values)
        {
            if (downloader.Status == DownloadStatus.Downloading || 
                downloader.Status == DownloadStatus.Waiting)
            {
                downloader.Pause();
            }
        }
    }

    /// <summary>
    /// 恢复所有下载
    /// </summary>
    public void ResumeAll()
    {
        foreach (var downloader in _allDownloaders.Values)
        {
            if (downloader.Status == DownloadStatus.Paused)
            {
                downloader.Resume();
            }
        }
    }

    /// <summary>
    /// 取消所有下载
    /// </summary>
    public void CancelAll()
    {
        List<string> ids = new List<string>(_allDownloaders.Keys);
        foreach (string id in ids)
        {
            Downloader downloader = _allDownloaders[id];
            downloader.Cancel();
            _allDownloaders.Remove(id);
        }
        _waitingQueue.Clear();
        _runningDownloaders.Clear();
    }

    /// <summary>
    /// 移除完成的下载器
    /// </summary>
    public void RemoveCompleted()
    {
        List<string> idsToRemove = new List<string>();
        foreach (var kvp in _allDownloaders)
        {
            if (kvp.Value.Status == DownloadStatus.Completed || 
                kvp.Value.Status == DownloadStatus.Cancelled ||
                kvp.Value.Status == DownloadStatus.Failed)
            {
                idsToRemove.Add(kvp.Key);
            }
        }
        
        foreach (string id in idsToRemove)
        {
            _allDownloaders.Remove(id);
        }
    }

    /// <summary>
    /// 获取总进度
    /// </summary>
    public float GetTotalProgress()
    {
        if (_allDownloaders.Count == 0)
            return 0;
            
        float total = 0;
        foreach (var downloader in _allDownloaders.Values)
        {
            total += downloader.Progress;
        }
        return total / _allDownloaders.Count;
    }

    #endregion

    #region 内部方法

    /// <summary>
    /// 处理下载队列
    /// </summary>
    private void ProcessQueue()
    {
        int remaining = _waitingQueue.Count;
        while (_runningDownloaders.Count < _maxConcurrent && _waitingQueue.Count > 0 && remaining-- > 0)
        {
            Downloader downloader = _waitingQueue.Dequeue();
            
            if (downloader.Status == DownloadStatus.Cancelled)
                continue;
                
            if (downloader.Status == DownloadStatus.Paused)
            {
                // 暂停的任务放回队列
                _waitingQueue.Enqueue(downloader);
                continue;
            }
            
            if (downloader.Status != DownloadStatus.Waiting && 
                downloader.Status != DownloadStatus.Idle)
                continue;
                
            if (_runningDownloaders.Contains(downloader)) continue;
            _runningDownloaders.Add(downloader);
            try { downloader.Start(this); }
            catch { _runningDownloaders.Remove(downloader); throw; }
        }
    }

    #endregion

    #region 事件处理

    private void OnDownloaderStart(Downloader downloader)
    {
        Debug.Log($"开始下载: {downloader.Url}");
    }

    private void OnDownloaderProgress(Downloader downloader)
    {
        // 可以在这里做全局进度处理
    }

    private void OnDownloaderComplete(Downloader downloader)
    {
        Debug.Log($"下载完成: {downloader.Url}");
        _runningDownloaders.Remove(downloader);
    }

    private void OnDownloaderError(Downloader downloader, string error)
    {
        Debug.LogError($"下载错误: {downloader.Url}, {error}");
        _runningDownloaders.Remove(downloader);
    }

    private void OnDownloaderPaused(Downloader downloader)
    {
        Debug.Log($"下载暂停: {downloader.Url}");
        _runningDownloaders.Remove(downloader);
    }

    private void OnDownloaderResumed(Downloader downloader)
    {
        Debug.Log($"下载恢复: {downloader.Url}");
        if (!_waitingQueue.Contains(downloader)) _waitingQueue.Enqueue(downloader);
        ProcessQueue();
    }

    private void OnDownloaderCancelled(Downloader downloader)
    {
        Debug.Log($"下载取消: {downloader.Url}");
        _runningDownloaders.Remove(downloader);
    }

    #endregion

    void OnDestroy()
    {
        CancelAll();
    }
}