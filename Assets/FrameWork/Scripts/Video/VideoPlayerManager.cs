using System;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FrameWork
{
    public class VideoPlayerManager : SingletonAsMono<VideoPlayerManager>
    {
        private MyVideoPlayer _myVideoPlayer;
        private RenderTexture _renderTexture;
        private VideoPlayer _videoPlayer;
        private RawImage _rawImage;
        private void Awake()
        {
            _myVideoPlayer=UiManager.Instance.ShowUi<MyVideoPlayer>();
            _renderTexture = new RenderTexture(1920, 1080, 24);
            _renderTexture.enableRandomWrite = true;
            _renderTexture.Create();
            _rawImage = _myVideoPlayer.RawImageMyVideoPlayer;
            _videoPlayer = _myVideoPlayer.VideoPlayerMyVideoPlayer;
            _rawImage.texture = _renderTexture;
            _videoPlayer.targetTexture = _renderTexture;
        }
        
        public void PlayVideo(VideoClip videoClip,float duration=0.3f)
        {
            if (_videoPlayer.clip!=null)
            {
                
                DOTween.To(() => Color.white, x => _rawImage.color = x, Color.black, duration).onComplete += (() =>
                {
                    _videoPlayer.clip = videoClip;
                    _videoPlayer.Play();

                    Task.Run(async () =>
                    {
                        await Task.Delay(150);
                        DOTween.To(() => Color.black, x => _rawImage.color = x, Color.white, duration);
                    });
                    
                });
            }
            else
            {
                _videoPlayer.clip = videoClip;
                _videoPlayer.Play();
            }
        }
        
        public void PlayVideo()
        {
            _videoPlayer.Play();
        }

        public void Pause()
        {
            _videoPlayer.Pause();
        }
        
        public void StopVideo()
        {
            _videoPlayer.Stop();
        }


        public void SetSpeed(float speed)
        {
            _videoPlayer.playbackSpeed = speed;
        }

        public void SetSecond(float s)
        {
            DOTween.To(() => Color.white, x => _rawImage.color = x, Color.black, 0.1f).onComplete += (() =>
            {
                _videoPlayer.frame = (long)(s / _videoPlayer.length * _videoPlayer.frameCount);
                Task.Run(async () =>
                {
                    await Task.Delay(150);
                    DOTween.To(() => Color.black, x => _rawImage.color = x, Color.white, 0.1f);
                });
            });
        }


        private void Update()
        {
            
        }
    }
}