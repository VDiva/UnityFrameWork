using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 下载状态枚举
/// </summary>
public enum DownloadStatus
{
    Idle,           // 空闲
    Waiting,        // 等待中
    Downloading,    // 下载中
    Paused,         // 已暂停
    Completed,      // 已完成
    Failed,         // 失败
    Cancelled       // 已取消
}

/// <summary>
/// 独立的下载类 - 负责单个文件的下载
/// </summary>
public class Downloader
{
    // 基础信息
    public string Id { get; private set; }
    public string Url { get; private set; }
    public string SavePath { get; private set; }
    public string TempPath { get; private set; }
    
    // 下载状态
    public DownloadStatus Status { get; private set; }
    public long TotalSize { get; private set; }
    public long DownloadedSize { get; private set; }
    public float Progress { get; private set; }
    public float DownloadSpeed { get; private set; } // KB/s
    public string ErrorMessage { get; private set; }
    
    // 配置
    public int MaxRetryCount { get; set; } = 3;
    public float RetryDelay { get; set; } = 2f;
    public int BufferSize { get; set; } = 1024 * 1024; // 1MB
    
    // 回调事件
    public event Action<Downloader> OnStart;
    public event Action<Downloader> OnProgress;
    public event Action<Downloader> OnComplete;
    public event Action<Downloader, string> OnError;
    public event Action<Downloader> OnPaused;
    public event Action<Downloader> OnResumed;
    public event Action<Downloader> OnCancelled;
    
    // 内部变量
    private UnityWebRequest _request;
    private FileStream _fileStream;
    private Coroutine _downloadCoroutine;
    private MonoBehaviour _coroutineRunner;
    private bool _isPaused;
    private int _retryCount;
    private float _lastUpdateTime;
    private long _lastDownloadedSize;
    private float _speedUpdateInterval = 0.5f;

    /// <summary>
    /// 构造函数
    /// </summary>
    public Downloader(string url, string savePath)
    {
        Id = Guid.NewGuid().ToString();
        Url = url;
        SavePath = savePath;
        TempPath = savePath + ".tmp";
        Status = DownloadStatus.Idle;
        Progress = 0;
        DownloadedSize = 0;
        TotalSize = 0;
        DownloadSpeed = 0;
        _lastUpdateTime = Time.time;
        _lastDownloadedSize = 0;
        
        // 创建目录
        string directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 开始下载
    /// </summary>
    public void Start(MonoBehaviour coroutineRunner)
    {
        if (Status == DownloadStatus.Downloading || Status == DownloadStatus.Waiting)
            return;
            
        if (Status == DownloadStatus.Completed)
        {
            Debug.LogWarning("文件已完成下载: " + Url);
            return;
        }
        
        _coroutineRunner = coroutineRunner;
        Status = DownloadStatus.Waiting;
        _downloadCoroutine = _coroutineRunner.StartCoroutine(DownloadCoroutine());
    }

    /// <summary>
    /// 暂停下载
    /// </summary>
    public void Pause()
    {
        if (Status != DownloadStatus.Downloading && Status != DownloadStatus.Waiting)
            return;
            
        _isPaused = true;
        Status = DownloadStatus.Paused;
        
        if (_request != null)
        {
            _request.Abort();
        }
        
        CloseFileStream();
        OnPaused?.Invoke(this);
        Debug.Log($"下载已暂停: {Url}");
    }

    /// <summary>
    /// 恢复下载
    /// </summary>
    public void Resume()
    {
        if (Status != DownloadStatus.Paused)
            return;
            
        _isPaused = false;
        Status = DownloadStatus.Waiting;
        OnResumed?.Invoke(this);
        Debug.Log($"下载已恢复: {Url}");
        
        if (_coroutineRunner != null && _downloadCoroutine == null)
        {
            _downloadCoroutine = _coroutineRunner.StartCoroutine(DownloadCoroutine());
        }
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    public void Cancel()
    {
        if (Status == DownloadStatus.Cancelled || Status == DownloadStatus.Completed)
            return;
            
        Status = DownloadStatus.Cancelled;
        
        if (_request != null)
        {
            _request.Abort();
        }
        
        CloseFileStream();
        
        // 删除临时文件
        if (File.Exists(TempPath))
        {
            try { File.Delete(TempPath); } catch { }
        }
        
        if (_downloadCoroutine != null && _coroutineRunner != null)
        {
            _coroutineRunner.StopCoroutine(_downloadCoroutine);
            _downloadCoroutine = null;
        }
        
        OnCancelled?.Invoke(this);
        Debug.Log($"下载已取消: {Url}");
    }

    /// <summary>
    /// 重置下载（重新开始）
    /// </summary>
    public void Reset()
    {
        Cancel();
        Status = DownloadStatus.Idle;
        Progress = 0;
        DownloadedSize = 0;
        TotalSize = 0;
        DownloadSpeed = 0;
        ErrorMessage = null;
        _retryCount = 0;
        _isPaused = false;
        _lastDownloadedSize = 0;
        _lastUpdateTime = Time.time;
        
        // 删除临时文件
        if (File.Exists(TempPath))
        {
            try { File.Delete(TempPath); } catch { }
        }
    }

    #region 内部方法

    /// <summary>
    /// 下载协程
    /// </summary>
    private IEnumerator DownloadCoroutine()
    {
        Status = DownloadStatus.Downloading;
        OnStart?.Invoke(this);
        
        // 检查已下载部分
        long startByte = 0;
        if (File.Exists(TempPath))
        {
            FileInfo fileInfo = new FileInfo(TempPath);
            startByte = fileInfo.Length;
            DownloadedSize = startByte;
        }

        // 创建请求
        _request = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbGET);
        
        // 设置Range头（断点续传）
        if (startByte > 0)
        {
            _request.SetRequestHeader("Range", $"bytes={startByte}-");
        }
        
        // 使用DownloadHandlerFile
        DownloadHandlerFile fileHandler = new DownloadHandlerFile(TempPath, true);
        _request.downloadHandler = fileHandler;
        
        // 发送请求
        UnityWebRequestAsyncOperation asyncOp = _request.SendWebRequest();
        
        // 等待完成
        while (!asyncOp.isDone)
        {
            // 检查暂停
            if (_isPaused)
            {
                _request.Abort();
                Status = DownloadStatus.Paused;
                _downloadCoroutine = null;
                yield break;
            }
            
            // 获取总大小
            if (TotalSize == 0)
            {
                string contentLength = _request.GetResponseHeader("Content-Length");
                if (!string.IsNullOrEmpty(contentLength))
                {
                    TotalSize = long.Parse(contentLength) + startByte;
                }
                else
                {
                    // 如果无法获取总大小，使用已下载大小
                    TotalSize = -1;
                }
            }
            
            // 更新已下载大小
            if (File.Exists(TempPath))
            {
                FileInfo fileInfo = new FileInfo(TempPath);
                DownloadedSize = fileInfo.Length;
                
                if (TotalSize > 0)
                {
                    Progress = Mathf.Clamp01((float)DownloadedSize / TotalSize);
                }
                else
                {
                    Progress = 0;
                }
            }
            
            // 更新下载速度
            UpdateSpeed();
            
            // 触发进度事件
            OnProgress?.Invoke(this);
            
            yield return null;
        }

        // 处理结果
        if (_request.result == UnityWebRequest.Result.Success)
        {
            // 下载完成
            Status = DownloadStatus.Completed;
            Progress = 1;
            
            if (TotalSize > 0)
            {
                DownloadedSize = TotalSize;
            }
            else if (File.Exists(TempPath))
            {
                FileInfo fileInfo = new FileInfo(TempPath);
                DownloadedSize = fileInfo.Length;
                TotalSize = DownloadedSize;
            }
            
            // 移动文件
            if (File.Exists(TempPath))
            {
                if (File.Exists(SavePath))
                {
                    try { File.Delete(SavePath); } catch { }
                }
                try 
                { 
                    File.Move(TempPath, SavePath);
                    Debug.Log($"文件已保存: {SavePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"文件移动失败: {e.Message}");
                    Status = DownloadStatus.Failed;
                    ErrorMessage = e.Message;
                    OnError?.Invoke(this, ErrorMessage);
                    _request.Dispose();
                    _request = null;
                    _downloadCoroutine = null;
                    yield break;
                }
            }
            
            OnComplete?.Invoke(this);
            Debug.Log($"下载完成: {Url}");
        }
        else
        {
            // 下载失败
            ErrorMessage = _request.error;
            
            // 重试
            if (_retryCount < MaxRetryCount && !_isPaused)
            {
                _retryCount++;
                Status = DownloadStatus.Waiting;
                Debug.LogWarning($"下载失败，正在重试 ({_retryCount}/{MaxRetryCount}): {Url}");
                
                _request.Dispose();
                _request = null;
                
                yield return new WaitForSeconds(RetryDelay);
                
                _downloadCoroutine = _coroutineRunner.StartCoroutine(DownloadCoroutine());
                yield break;
            }
            else
            {
                Status = DownloadStatus.Failed;
                OnError?.Invoke(this, ErrorMessage);
                Debug.LogError($"下载失败: {Url}, 错误: {ErrorMessage}");
            }
        }
        
        _request.Dispose();
        _request = null;
        _downloadCoroutine = null;
    }

    /// <summary>
    /// 更新下载速度
    /// </summary>
    private void UpdateSpeed()
    {
        float currentTime = Time.time;
        float timeDiff = currentTime - _lastUpdateTime;
        
        if (timeDiff >= _speedUpdateInterval)
        {
            long sizeDiff = DownloadedSize - _lastDownloadedSize;
            DownloadSpeed = sizeDiff / 1024f / timeDiff;
            _lastUpdateTime = currentTime;
            _lastDownloadedSize = DownloadedSize;
        }
    }

    /// <summary>
    /// 关闭文件流
    /// </summary>
    private void CloseFileStream()
    {
        if (_fileStream != null)
        {
            _fileStream.Close();
            _fileStream.Dispose();
            _fileStream = null;
        }
    }

    #endregion
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
        downloader.OnResumed += OnDownloaderResumed;
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
        while (_runningDownloaders.Count < _maxConcurrent && _waitingQueue.Count > 0)
        {
            Downloader downloader = _waitingQueue.Dequeue();
            
            if (downloader.Status == DownloadStatus.Cancelled)
                continue;
                
            if (downloader.Status == DownloadStatus.Paused)
            {
                // 暂停的任务放回队列
                _waitingQueue.Enqueue(downloader);
                break;
            }
            
            if (downloader.Status != DownloadStatus.Waiting && 
                downloader.Status != DownloadStatus.Idle)
                continue;
                
            downloader.Start(this);
            _runningDownloaders.Add(downloader);
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
        ProcessQueue();
    }

    private void OnDownloaderError(Downloader downloader, string error)
    {
        Debug.LogError($"下载错误: {downloader.Url}, {error}");
        _runningDownloaders.Remove(downloader);
        ProcessQueue();
    }

    private void OnDownloaderPaused(Downloader downloader)
    {
        Debug.Log($"下载暂停: {downloader.Url}");
        _runningDownloaders.Remove(downloader);
    }

    private void OnDownloaderResumed(Downloader downloader)
    {
        Debug.Log($"下载恢复: {downloader.Url}");
        _waitingQueue.Enqueue(downloader);
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