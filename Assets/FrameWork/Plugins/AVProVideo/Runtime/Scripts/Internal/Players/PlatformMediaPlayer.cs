//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

#if UNITY_2017_2_OR_NEWER && (UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS || UNITY_ANDROID)))

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace RenderHeads.Media.AVProVideo
{
	public sealed partial class PlatformMediaPlayer : BaseMediaPlayer
	{
		private static DateTime Epoch = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		private static IntPtr PluginRenderEventFunction;

		static PlatformMediaPlayer()
		{
			#if !UNITY_EDITOR && (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS || UNITY_ANDROID)
				Native.AVPPluginBootstrap();
			#endif

			PluginRenderEventFunction = Native.AVPPluginGetRenderEventFunction();
		}

		private IntPtr _player;
		Native.AVPPlayerSettings _playerSettings;

		private MediaPlayer.PlatformOptions _options;

		public PlatformMediaPlayer(MediaPlayer.PlatformOptions options)
		{
			// Keep a handle on the options
			_options = options;

			// Configure the video output settings
			_playerSettings = new Native.AVPPlayerSettings();

			if (options is MediaPlayer.OptionsApple)
			{
				PlatformMediaPlayerInitWithOptions(options as MediaPlayer.OptionsApple);
			}
			else
			if (options is MediaPlayer.OptionsAndroid)
			{
				MediaPlayer.OptionsAndroid androidOptions = options as MediaPlayer.OptionsAndroid;
				PlatformMediaPlayerInitWithOptions(androidOptions);
			}

			// Make the player
			_player = Native.AVPPluginMakePlayer(_playerSettings);

			// Create the command buffers
			CreateCommandBuffers();

			// And execute the setup buffer
			Graphics.ExecuteCommandBuffer(_setupCommandBuffer);

			// Force an update to get our state in sync with the native
			Update();
		}

		/// <summary>
		/// Creates the command buffers
		/// </summary>
		private void CreateCommandBuffers()
		{
#if !UNITY_EDITOR && UNITY_ANDROID
			// We pass setup flags alongside the player id on Android
			long playerID = _player.ToInt64();
			Debug.Log($"playerID: {playerID:X8}");

			long flags = 0;
			if ((_options as MediaPlayer.OptionsAndroid).textureFormat == MediaPlayer.PlatformOptions.TextureFormat.YCbCr420_OES)
				flags |= (long)Native.AVPPlayerRenderEventPlayerSetupFlags.AndroidUseOESFastPath;
			if (QualitySettings.activeColorSpace == ColorSpace.Linear)
				flags |= (long)Native.AVPPlayerRenderEventPlayerSetupFlags.LinearColourSpace;
			Debug.Log($"flags: {flags:X8}");

			long param = (playerID & Native.kAVPPlayerRenderEventDataPlayerIDMask) << Native.kAVPPlayerRenderEventDataPlayerIDShift;
			param |= (flags & Native.kAVPPlayerRenderEventDataOptionsMask) << Native.kAVPPlayerRenderEventDataOptionsShift;
			Debug.Log($"flags: {param:X8}");

			IntPtr setupData = new IntPtr(param);
#else
			// Other platforms just take the player id directly
			IntPtr setupData = _player;
#endif
			int eventId = Native.kAVPPlayerRenderEventId | ((int)Native.AVPPluginRenderEvent.PlayerSetup << Native.kAVPPlayerRenderEventTypeShift);
			_setupCommandBuffer = new CommandBuffer();
			_setupCommandBuffer.name = "AVPPluginRenderEvent.PlayerSetup";
			_setupCommandBuffer.IssuePluginEventAndData(PluginRenderEventFunction, eventId, setupData);

			// Render resources
			eventId = Native.kAVPPlayerRenderEventId | ((int)Native.AVPPluginRenderEvent.PlayerRender << Native.kAVPPlayerRenderEventTypeShift);
			_renderCommandBuffer = new CommandBuffer();
			_renderCommandBuffer.name = "AVPPluginRenderEvent.PlayerRender";
			_renderCommandBuffer.IssuePluginEventAndData(PluginRenderEventFunction, eventId, _player);

			// Free resources
			eventId = Native.kAVPPlayerRenderEventId | ((int)Native.AVPPluginRenderEvent.PlayerFreeResources << Native.kAVPPlayerRenderEventTypeShift);
			_freeResourcesCommandBuffer = new CommandBuffer();
			_freeResourcesCommandBuffer.name = "AVPPluginRenderEvent.PlayerFreeResources";
			_freeResourcesCommandBuffer.IssuePluginEventAndData(PluginRenderEventFunction, eventId, _player);
		}

		// Configure the player with Apple options
		private void PlatformMediaPlayerInitWithOptions(MediaPlayer.OptionsApple options)
		{
			switch (options.textureFormat)
			{
				case MediaPlayer.OptionsApple.TextureFormat.BGRA:
				default:
					_playerSettings.pixelFormat = Native.AVPPlayerVideoPixelFormat.Bgra;
					break;
				case MediaPlayer.OptionsApple.TextureFormat.YCbCr420_OES:
					_playerSettings.pixelFormat = Native.AVPPlayerVideoPixelFormat.YCbCr420;
					break;
			}

			if (options.flags.GenerateMipmaps())
				_playerSettings.videoFlags |= Native.AVPPlayerVideoOutputSettingsFlags.GenerateMipmaps;
			if (QualitySettings.activeColorSpace == ColorSpace.Linear)
				_playerSettings.videoFlags |= Native.AVPPlayerVideoOutputSettingsFlags.LinearColorSpace;

			GetWidthHeightFromResolution(
				options.preferredMaximumResolution,
				options.customPreferredMaximumResolution,
				out _playerSettings.preferredMaximumResolution_width,
				out _playerSettings.preferredMaximumResolution_height
			);

			_playerSettings.maximumPlaybackRate = options.maximumPlaybackRate;

			// Configure the audio output settings
			_playerSettings.audioOutputMode = (Native.AVPPlayerAudioOutputMode)options.audioMode;
			if (options.audioMode == MediaPlayer.OptionsApple.AudioMode.Unity)
			{
				_playerSettings.sampleRate = AudioSettings.outputSampleRate;
				int numBuffers;
				AudioSettings.GetDSPBufferSize(out _playerSettings.bufferLength, out numBuffers);
			}

			// Configure any network settings
			_playerSettings.preferredPeakBitRate = options.GetPreferredPeakBitRateInBitsPerSecond();
			_playerSettings.preferredForwardBufferDuration = options.preferredForwardBufferDuration;
			if (options.flags.PlayWithoutBuffering())
				_playerSettings.networkFlags |= Native.AVPPlayerNetworkSettingsFlags.PlayWithoutBuffering;
			if (options.flags.UseSinglePlayerItem())
				_playerSettings.networkFlags |= Native.AVPPlayerNetworkSettingsFlags.UseSinglePlayerItem;

			// Setup any other flags from the options
			_flags = _flags.SetAllowExternalPlayback(options.flags.AllowExternalPlayback());
			_flags = _flags.SetResumePlayback(options.flags.ResumePlaybackAfterAudioSessionRouteChange());
		}

		// Configure the player with Android options
		private void PlatformMediaPlayerInitWithOptions(MediaPlayer.OptionsAndroid options)
		{
			_playerSettings.videoApi = options.videoApi == Android.VideoApi.MediaPlayer ? Native.AVPPlayerVideoAPI.MediaPlayer : Native.AVPPlayerVideoAPI.ExoPlayer;
			if (options.preferSoftwareDecoder)
				_playerSettings.videoFlags |= Native.AVPPlayerVideoOutputSettingsFlags.PreferSoftwareDecoder;
			if (options.forceRtpTCP)
				_playerSettings.networkFlags |= Native.AVPPlayerNetworkSettingsFlags.ForceRtpTCP;
			if (options.forceEnableMediaCodecAsynchronousQueueing)
				_playerSettings.videoFlags |= Native.AVPPlayerVideoOutputSettingsFlags.ForceEnableMediaCodecAsynchronousQueueing;

			switch (options.textureFormat)
			{
				case MediaPlayer.OptionsAndroid.TextureFormat.BGRA:
				default:
					_playerSettings.pixelFormat = Native.AVPPlayerVideoPixelFormat.Bgra;
					break;
				case MediaPlayer.OptionsAndroid.TextureFormat.YCbCr420_OES:
					_playerSettings.pixelFormat = Native.AVPPlayerVideoPixelFormat.YCbCr420;
					break;
			}

			if (QualitySettings.activeColorSpace == ColorSpace.Linear)
				_playerSettings.videoFlags |= Native.AVPPlayerVideoOutputSettingsFlags.LinearColorSpace;

			GetWidthHeightFromResolution(
				options.preferredMaximumResolution,
				options.customPreferredMaximumResolution,
				out _playerSettings.preferredMaximumResolution_width,
				out _playerSettings.preferredMaximumResolution_height
			);

			// Configure the audio output settings
			_playerSettings.audioOutputMode = (Native.AVPPlayerAudioOutputMode)options.audioMode;
			switch( options.audioMode )
			{
				case MediaPlayer.OptionsAndroid.AudioMode.Unity:
					{
						_playerSettings.sampleRate = AudioSettings.outputSampleRate;
						int numBuffers;
						AudioSettings.GetDSPBufferSize(out _playerSettings.bufferLength, out numBuffers);
					}
					break;

				case MediaPlayer.OptionsAndroid.AudioMode.SystemDirectWithCapture:
					{
						// SystemDirectWithCapture does not really exist (or supported) on Android
						_playerSettings.audioOutputMode = (Native.AVPPlayerAudioOutputMode)(MediaPlayer.OptionsAndroid.AudioMode.FacebookAudio360);
					}
					break;
			}

			_playerSettings.audio360Channels = options.audio360ChannelMode;
			_playerSettings.audio360LatencyMS = options.audio360LatencyMS;

            // Configure any network settings
            _playerSettings.preferredPeakBitRate = options.GetPreferredPeakBitRateInBitsPerSecond();
			if (options.startWithHighestBitrate)
				_playerSettings.networkFlags |= Native.AVPPlayerNetworkSettingsFlags.ForceStartHighestBitrate;

			_playerSettings.minBufferMs = options.minBufferMs;
			_playerSettings.maxBufferMs = options.maxBufferMs;
			_playerSettings.bufferForPlaybackMs = options.bufferForPlaybackMs;
			_playerSettings.bufferForPlaybackAfterRebufferMs = options.bufferForPlaybackAfterRebufferMs;
		}

		private static void GetWidthHeightFromResolution(MediaPlayer.OptionsApple.Resolution resolution, Vector2Int custom, out float width, out float height)
		{
			switch (resolution)
			{
				case MediaPlayer.OptionsApple.Resolution.NoPreference:
				default:
					width = 0;
					height = 0;
					break;
				case MediaPlayer.OptionsApple.Resolution._480p:
					width = 640;
					height = 480;
					break;
				case MediaPlayer.OptionsApple.Resolution._720p:
					width = 1280;
					height = 720;
					break;
				case MediaPlayer.OptionsApple.Resolution._1080p:
					width = 1920;
					height = 1080;
					break;
				case MediaPlayer.OptionsApple.Resolution._1440p:
					width = 2560;
					height = 1440;
					break;
				case MediaPlayer.OptionsApple.Resolution._2160p:
					width = 3840;
					height = 2160;
					break;
				case MediaPlayer.OptionsApple.Resolution.Custom:
					width = custom.x;
					height = custom.y;
					break;
			}
		}
	}

	// IMediaPlayer
	public sealed partial class PlatformMediaPlayer
	{
		private const int MaxTexturePlanes = 2;
		private Native.AVPPlayerState _state = new Native.AVPPlayerState();
		private Native.AVPPlayerFlags _flags = Native.AVPPlayerFlags.None;
		private Native.AVPPlayerAssetInfo _assetInfo = new Native.AVPPlayerAssetInfo();
		private Native.AVPPlayerVideoTrackInfo[] _videoTrackInfo = new Native.AVPPlayerVideoTrackInfo[0];
		private Native.AVPPlayerAudioTrackInfo[] _audioTrackInfo = new Native.AVPPlayerAudioTrackInfo[0];
		private Native.AVPPlayerTextTrackInfo[] _textTrackInfo = new Native.AVPPlayerTextTrackInfo[0];
		private Native.AVPPlayerTexture _playerTexture;
		private Native.AVPPlayerText _playerText;
		private Texture2D[] _texturePlanes = new Texture2D[MaxTexturePlanes];
		private float _volume = 1.0f;
		private float _rate = 1.0f;
		private CommandBuffer _setupCommandBuffer;
		private CommandBuffer _renderCommandBuffer;
		private CommandBuffer _freeResourcesCommandBuffer;

		public override void OnEnable()
		{

		}

		public override void Update()
		{
			Native.AVPPlayerUpdate(_player);

			Native.AVPPlayerStatus prevStatus = _state.status;
			Native.AVPPlayerGetState(_player, ref _state);

			Native.AVPPlayerStatus changedStatus = prevStatus ^ _state.status;

			// Need to make sure that lastError is set when status is failed so that the Error event is triggered
			if (/*BaseMediaPlayer.*/_lastError == ErrorCode.None && changedStatus.HasFailed() && _state.status.HasFailed())
			{
				/*BaseMediaPlayer.*/_lastError = ErrorCode.LoadFailed;
			}

			if (_state.status.HasUpdatedAssetInfo())
			{
				Native.AVPPlayerGetAssetInfo(_player, ref _assetInfo);

				if (_state.status.HasVideo())
				{
					_videoTrackInfo = new Native.AVPPlayerVideoTrackInfo[_assetInfo.videoTrackCount];
					for (int i = 0; i < _assetInfo.videoTrackCount; ++i)
					{
						_videoTrackInfo[i] = new Native.AVPPlayerVideoTrackInfo();
						Native.AVPPlayerGetVideoTrackInfo(_player, i, ref _videoTrackInfo[i]);
					}
				}

				if (_state.status.HasAudio())
				{
					_audioTrackInfo = new Native.AVPPlayerAudioTrackInfo[_assetInfo.audioTrackCount];
					for (int i = 0; i < _assetInfo.audioTrackCount; ++i)
					{
						_audioTrackInfo[i] = new Native.AVPPlayerAudioTrackInfo();
						Native.AVPPlayerGetAudioTrackInfo(_player, i, ref _audioTrackInfo[i]);
					}
				}

				if (_state.status.HasText())
				{
					_textTrackInfo = new Native.AVPPlayerTextTrackInfo[_assetInfo.textTrackCount];
					for (int i = 0; i < _assetInfo.textTrackCount; ++i)
					{
						_textTrackInfo[i] = new Native.AVPPlayerTextTrackInfo();
						Native.AVPPlayerGetTextTrackInfo(_player, i, ref _textTrackInfo[i]);
					}
				}

				/*BaseMediaPlayer.*/UpdateTracks();
			}

			if (_state.status.HasUpdatedBufferedTimeRanges())
			{
				if (_state.bufferedTimeRangesCount > 0)
				{
					Native.AVPPlayerTimeRange[] timeRanges = new Native.AVPPlayerTimeRange[_state.bufferedTimeRangesCount];
					Native.AVPPlayerGetBufferedTimeRanges(_player, timeRanges, timeRanges.Length);
					_bufferedTimes = ConvertNativeTimeRangesToTimeRanges(timeRanges);
				}
				else
				{
					_bufferedTimes = new TimeRanges();
				}
			}

			if (_state.status.HasUpdatedSeekableTimeRanges())
			{
				if (_state.seekableTimeRangesCount > 0)
				{
					Native.AVPPlayerTimeRange[] timeRanges = new Native.AVPPlayerTimeRange[_state.seekableTimeRangesCount];
					Native.AVPPlayerGetSeekableTimeRanges(_player, timeRanges, timeRanges.Length);
					_seekableTimes = ConvertNativeTimeRangesToTimeRanges(timeRanges);
				}
				else
				{
					_seekableTimes = new TimeRanges();
				}
			}

			if (_state.status.HasUpdatedTexture())
			{
				Native.AVPPlayerGetTexture(_player, ref _playerTexture);
				for (int i = 0; i < _playerTexture.planeCount; ++i)
				{
					TextureFormat textureFormat = TextureFormat.BGRA32;
					switch (_playerTexture.planes[i].textureFormat)
					{
						case Native.AVPPlayerTextureFormat.R8:
							textureFormat = TextureFormat.R8;
							break;

						case Native.AVPPlayerTextureFormat.R16:
							textureFormat = TextureFormat.R16;
							break;

						case Native.AVPPlayerTextureFormat.RG8:
							textureFormat = TextureFormat.RG16;
							break;

						case Native.AVPPlayerTextureFormat.RG16:
							textureFormat = TextureFormat.RG32;
							break;

						case Native.AVPPlayerTextureFormat.BC1:
							textureFormat = TextureFormat.DXT1;
							break;

						case Native.AVPPlayerTextureFormat.BC3:
							textureFormat = TextureFormat.DXT5;
							break;

						case Native.AVPPlayerTextureFormat.BC4:
							textureFormat = TextureFormat.BC4;
							break;

						case Native.AVPPlayerTextureFormat.BC5:
							textureFormat = TextureFormat.BC5;
							break;

						case Native.AVPPlayerTextureFormat.BC7:
							textureFormat = TextureFormat.BC7;
							break;

						case Native.AVPPlayerTextureFormat.RGBA16Float:
							textureFormat = TextureFormat.RGBAHalf;
							break;

						case Native.AVPPlayerTextureFormat.BGRA8:
						default:
							break;
					}

					if (_texturePlanes[i] == null ||
						_texturePlanes[i].width != _playerTexture.planes[i].width ||
						_texturePlanes[i].height != _playerTexture.planes[i].height ||
						_texturePlanes[i].format != textureFormat)
					{
#if !UNITY_ANDROID
						// Ensure any existing texture is released.
						if (_texturePlanes[i] != null)
						{
							_texturePlanes[i].UpdateExternalTexture(IntPtr.Zero);
							_texturePlanes[i] = null;
						}
#endif
						_texturePlanes[i] = Texture2D.CreateExternalTexture(
							_playerTexture.planes[i].width,
							_playerTexture.planes[i].height,
							textureFormat,
							_playerTexture.flags.IsMipmapped(),
							_playerTexture.flags.IsLinear(),
							_playerTexture.planes[i].plane
						);
						base.ApplyTextureProperties(_texturePlanes[i]);
					}
					else
					{
						_texturePlanes[i].UpdateExternalTexture(_playerTexture.planes[i].plane);
					}
				}
			}

			if (_state.status.HasUpdatedTextureTransform())
			{
				// Directly grab the video track info as path of least resistance
				if (_state.selectedVideoTrack >= 0)
				{
					Native.AVPPlayerGetVideoTrackInfo(_player, _state.selectedVideoTrack, ref _videoTrackInfo[_state.selectedVideoTrack]);
				}
			}

			if (_state.status.HasUpdatedText())
			{
				Native.AVPPlayerGetText(_player, ref _playerText);
				/*BaseMediaPlayer.*/UpdateTextCue();
			}

			if (_flags.IsDirty())
			{
				_flags = _flags.SetDirty(false);
				Native.AVPPlayerSetFlags(_player, (int)_flags);
			}

			if (_options.HasChanged())
			{
				if (_options is MediaPlayer.OptionsApple)
				{
					UpdatePlayerSettingsFromOptions(_options as MediaPlayer.OptionsApple);
				}
				else
				if (_options is MediaPlayer.OptionsAndroid)
				{
					UpdatePlayerSettingsFromOptions(_options as MediaPlayer.OptionsAndroid);
				}

				Native.AVPPlayerSetPlayerSettings(_player, _playerSettings);

				_options.ClearChanges();
			}

			/*BaseMediaPlayer.*/UpdateDisplayFrameRate();
			/*BaseMediaPlayer.*/UpdateSubtitles();
		}

		private void UpdatePlayerSettingsFromOptions(MediaPlayer.OptionsApple options)
		{
			if (options.HasChanged(MediaPlayer.OptionsApple.ChangeFlags.PreferredPeakBitRate))
			{
				_playerSettings.preferredPeakBitRate = options.GetPreferredPeakBitRateInBitsPerSecond();
			}

			if (options.HasChanged(MediaPlayer.OptionsApple.ChangeFlags.PreferredForwardBufferDuration))
			{
				_playerSettings.preferredForwardBufferDuration = options.preferredForwardBufferDuration;
			}

			if (options.HasChanged(MediaPlayer.OptionsApple.ChangeFlags.PlayWithoutBuffering))
			{
				bool enabled = (options.flags & MediaPlayer.OptionsApple.Flags.PlayWithoutBuffering) == MediaPlayer.OptionsApple.Flags.PlayWithoutBuffering;
				_playerSettings.networkFlags = enabled ? _playerSettings.networkFlags |  Native.AVPPlayerNetworkSettingsFlags.PlayWithoutBuffering
														: _playerSettings.networkFlags & ~Native.AVPPlayerNetworkSettingsFlags.PlayWithoutBuffering;
			}

			if (options.HasChanged(MediaPlayer.OptionsApple.ChangeFlags.PreferredMaximumResolution))
			{
				GetWidthHeightFromResolution(
					options.preferredMaximumResolution,
					options.customPreferredMaximumResolution,
					out _playerSettings.preferredMaximumResolution_width,
					out _playerSettings.preferredMaximumResolution_height);
			}

			if (options.HasChanged(MediaPlayer.OptionsApple.ChangeFlags.AudioMode))
			{
				if (_state.status.IsReadyToPlay() == false)
				{
					_playerSettings.audioOutputMode = (Native.AVPPlayerAudioOutputMode)options.audioMode;
					if (options.audioMode == MediaPlayer.OptionsApple.AudioMode.Unity)
					{
						_playerSettings.sampleRate = AudioSettings.outputSampleRate;
						int numBuffers;
						AudioSettings.GetDSPBufferSize(out _playerSettings.bufferLength, out numBuffers);
					}
				}
				else
				{
					Debug.LogWarning("[AVProVideo] Unable to change audio mode after media has been loaded and is ready to play");
					options.audioMode = options.previousAudioMode;
				}
			}
		}

		private void UpdatePlayerSettingsFromOptions(MediaPlayer.OptionsAndroid options)
		{
			if (options.HasChanged(MediaPlayer.OptionsAndroid.ChangeFlags.PreferredPeakBitRate))
			{
				_playerSettings.preferredPeakBitRate = options.GetPreferredPeakBitRateInBitsPerSecond();
			}

			if (options.HasChanged(MediaPlayer.OptionsAndroid.ChangeFlags.PreferredMaximumResolution))
			{
				GetWidthHeightFromResolution(
					options.preferredMaximumResolution,
					options.customPreferredMaximumResolution,
					out _playerSettings.preferredMaximumResolution_width,
					out _playerSettings.preferredMaximumResolution_height);
			}

			if (options.HasChanged(MediaPlayer.OptionsAndroid.ChangeFlags.AudioMode))
			{
				if (_state.status.IsReadyToPlay() == false)
				{
					_playerSettings.audioOutputMode = (Native.AVPPlayerAudioOutputMode)options.audioMode;
					if (options.audioMode == MediaPlayer.OptionsAndroid.AudioMode.Unity)
					{
						_playerSettings.sampleRate = AudioSettings.outputSampleRate;
						int numBuffers;
						AudioSettings.GetDSPBufferSize(out _playerSettings.bufferLength, out numBuffers);
					}
				}
				else
				{
					Debug.LogWarning("[AVProVideo] Unable to change audio mode after media has been loaded and is ready to play");
					options.audioMode = options.previousAudioMode;
				}
			}
		}

		public override void Render()
		{
			Graphics.ExecuteCommandBuffer(_renderCommandBuffer);
		}

		public override IntPtr GetNativePlayerHandle()
		{
			return _player;
		}

		private static TimeRanges ConvertNativeTimeRangesToTimeRanges(Native.AVPPlayerTimeRange[] ranges)
		{
			TimeRange[] targetRanges = new TimeRange[ranges.Length];
			for (int i = 0; i < ranges.Length; i++)
			{
				targetRanges[i].startTime = ranges[i].start;
				targetRanges[i].duration = ranges[i].duration;
			}
			return new TimeRanges(targetRanges);
		}
	}

	// IMediaControl
	public sealed partial class PlatformMediaPlayer
	{
		public override bool OpenMedia(string path, long offset, string headers, MediaHints mediaHints, int forceFileFormat, bool startWithHighestBitrate)
		{
			_mediaHints = mediaHints;

			bool b = Native.AVPPlayerOpenURL(_player, path, headers);
			if (b)
			{
				Update();
			}

			return b;
		}

		public override bool OpenMediaFromBuffer(byte[] buffer)
		{
			// Unsupported
			return false;
		}

		public override bool StartOpenMediaFromBuffer(ulong length)
		{
			// Unsupported
			return false;
		}

		public override bool AddChunkToMediaBuffer(byte[] chunk, ulong offset, ulong length)
		{
			// Unsupported
			return false;
		}

		public override bool EndOpenMediaFromBuffer()
		{
			// Unsupported
			return false;
		}

		public override void CloseMedia()
		{
			Native.AVPPlayerClose(_player);
			Update();
#if !UNITY_ANDROID
			// Clean up the textures
			for (int i = 0; i < MaxTexturePlanes; ++i)
			{
				if (_texturePlanes[i] != null)
				{
					_texturePlanes[i].UpdateExternalTexture(IntPtr.Zero);
					_texturePlanes[i] = null;
				}
			}
			_playerTexture.frameCount = 0;
#endif
		}

		public override void SetLooping(bool b)
		{
			_flags = _flags.SetLooping(b);
		}

		public override bool IsLooping()
		{
			return _flags.IsLooping();
		}

		public override bool HasMetaData()
		{
			return _state.status.HasMetadata();
		}

		public override bool CanPlay()
		{
			return _state.status.IsReadyToPlay();
		}

		public override bool IsPlaying()
		{
			return _state.status.IsPlaying();
		}

		public override bool IsSeeking()
		{
			return _state.status.IsSeeking() || _state.status.HasFinishedSeeking();
		}

		public override bool IsPaused()
		{
			return _state.status.IsPaused();
		}

		public override bool IsFinished()
		{
			return _state.status.IsFinished();
		}

		public override bool IsBuffering()
		{
			return _state.status.IsBuffering();
		}

		public override void Play()
		{
			Native.AVPPlayerSetRate(_player, _rate);
			Update();
		}

		public override void Pause()
		{
			Native.AVPPlayerSetRate(_player, 0.0f);
			Update();
		}

		public override void Stop()
		{
			Pause();
		}

		public override void Rewind()
		{
			SeekWithTolerance(0.0, 0.0, 0.0);
		}

		public override void Seek(double toTime)
		{
			SeekWithTolerance(toTime, 0.0, 0.0);
		}

		public override void SeekFast(double toTime)
		{
			SeekWithTolerance(toTime, double.PositiveInfinity, double.PositiveInfinity);
		}

		public override void SeekWithTolerance(double toTime, double toleranceBefore, double toleranceAfter)
		{
			Native.AVPPlayerSeek(_player, toTime, toleranceBefore, toleranceAfter);
			Update();
		}

		public override double GetCurrentTime()
		{
			return _state.currentTime;
		}

		public override DateTime GetProgramDateTime()
		{
			return Epoch.AddSeconds(_state.currentDate);
		}

		public override float GetPlaybackRate()
		{
			return _rate;
		}

		public override void SetPlaybackRate(float rate)
		{
			if (rate != _rate)
			{
				_rate = rate;
				Native.AVPPlayerSetRate(_player, rate);
				Update();
			}
		}

		public override void MuteAudio(bool mute)
		{
			_flags = _flags.SetMuted(mute);
		}

		public override bool IsMuted()
		{
			return _flags.IsMuted();
		}

		public override void SetVolume(float volume)
		{
			if (volume != _volume)
			{
				_volume = volume;
				Native.AVPPlayerSetVolume(_player, volume);
			}
		}

		public override void SetBalance(float balance)
		{
			// Unsupported
		}

		public override float GetVolume()
		{
			return _volume;
		}

		public override float GetBalance()
		{
			// Unsupported
			return 0.0f;
		}

		public override long GetLastExtendedErrorCode()
		{
			return 0;
		}

		public override int GetAudioChannelCount()
		{
			int channelCount = -1;
			if (_state.selectedAudioTrack > -1 && _state.selectedAudioTrack < _audioTrackInfo.Length)
			{
				channelCount = (int)_audioTrackInfo[_state.selectedAudioTrack].channelCount;
				#if !UNITY_EDITOR && UNITY_IOS
					MediaPlayer.OptionsApple options = _options as MediaPlayer.OptionsApple;
					if (options.audioMode == MediaPlayer.OptionsApple.AudioMode.Unity)
					{
						// iOS audio capture will convert down to two channel stereo
						channelCount = Math.Min(channelCount, 2);
					}
				#endif
			}
			return channelCount;
		}

		public override AudioChannelMaskFlags GetAudioChannelMask()
		{
			if (_state.selectedAudioTrack != -1 && _state.selectedAudioTrack < _audioTrackInfo.Length)
			{
				return _audioTrackInfo[_state.selectedAudioTrack].channelBitmap;
			}
			return AudioChannelMaskFlags.Unspecified;
		}

		public override void AudioConfigurationChanged(bool deviceChanged)
		{
			if (_playerSettings.audioOutputMode == Native.AVPPlayerAudioOutputMode.SystemDirect)
				return;
			_playerSettings.sampleRate = AudioSettings.outputSampleRate;
			int numBuffers;
			AudioSettings.GetDSPBufferSize(out _playerSettings.bufferLength, out numBuffers);
			Native.AVPPlayerSetPlayerSettings(_player, _playerSettings);
		}

		public override int GrabAudio(float[] buffer, int sampleCount, int channelCount)
		{
			return Native.AVPPlayerGetAudio(_player, buffer, buffer.Length);
		}

		public override int GetAudioBufferedSampleCount()
		{
			return _state.audioCaptureBufferedSamplesCount;
		}

		public override void SetAudioHeadRotation(Quaternion q)
		{
			float[] aRotation = new float[] { q.x, q.y, q.z, q.w };
			Native.AVPPlayerSetAudioHeadRotation(_player, aRotation);
		}

		public override void ResetAudioHeadRotation()
		{
			Native.AVPPlayerSetPositionTrackingEnabled(_player, false);
		}

		public override void SetAudioChannelMode(Audio360ChannelMode channelMode)
		{
			// Unsupported
		}

		public override void SetAudioFocusEnabled(bool enabled)
		{
			Native.AVPPlayerSetAudioFocusEnabled(_player, enabled);
		}

		public override void SetAudioFocusProperties(float offFocusLevel, float widthDegrees)
		{
			Native.AVPPlayerSetAudioFocusProperties(_player, offFocusLevel, widthDegrees);
		}

		public override void SetAudioFocusRotation(Quaternion q)
		{
			float[] aRotation = new float[] { q.x, q.y, q.z, q.w };
			Native.AVPPlayerSetAudioFocusRotation(_player, aRotation);
		}

		public override void ResetAudioFocus()
		{
			Native.AVPPlayerResetAudioFocus(_player);
		}

		public override bool WaitForNextFrame(Camera camera, int previousFrameCount)
		{
			return false;
		}

		public override void SetKeyServerAuthToken(string token)
		{
			Native.AVPPlayerSetKeyServerAuthToken(_player, token);
		}

		public override void SetOverrideDecryptionKey(byte[] key)
		{
			int length = key != null ? key.Length : 0;
			Native.AVPPlayerSetDecryptionKey(_player, key, length);
		}

		public override bool IsExternalPlaybackActive()
		{
			return _state.status.IsExternalPlaybackActive();
		}

		public override void SetAllowsExternalPlayback(bool enable)
		{
			_flags.SetAllowExternalPlayback(enable);
		}

		public override void SetExternalPlaybackVideoGravity(ExternalPlaybackVideoGravity gravity_)
		{
			Native.AVPPlayerExternalPlaybackVideoGravity gravity;
			switch (gravity_)
			{
				case ExternalPlaybackVideoGravity.Resize:
				default:
					gravity = Native.AVPPlayerExternalPlaybackVideoGravity.Resize;
					break;
				case ExternalPlaybackVideoGravity.ResizeAspect:
					gravity = Native.AVPPlayerExternalPlaybackVideoGravity.ResizeAspect;
					break;
				case ExternalPlaybackVideoGravity.ResizeAspectFill:
					gravity = Native.AVPPlayerExternalPlaybackVideoGravity.ResizeAspectFill;
					break;
			}
			Native.AVPPlayerSetExternalPlaybackVideoGravity(_player, gravity);
		}
	}

	// IMediaInfo
	public sealed partial class PlatformMediaPlayer
	{
		public override double GetDuration()
		{
			return _assetInfo.duration;
		}

		public override int GetVideoWidth()
		{
			int width = 0;
			if (_state.selectedVideoTrack >= 0)
			{
				width = (int)_videoTrackInfo[_state.selectedVideoTrack].dimensions.width;
			}
			return width;
		}

		public override int GetVideoHeight()
		{
			int height = 0;
			if (_state.selectedVideoTrack >= 0)
			{
				height = (int)_videoTrackInfo[_state.selectedVideoTrack].dimensions.height;
			}
			return height;
		}

		public override float GetVideoFrameRate()
		{
			float framerate = 0.0f;
			if (_state.selectedVideoTrack >= 0)
			{
				framerate = _videoTrackInfo[_state.selectedVideoTrack].frameRate;
			}
			return framerate;
		}

		public override bool HasVideo()
		{
			return _state.status.HasVideo();
		}

		public override bool HasAudio()
		{
			return _state.status.HasAudio();
		}

		public override bool PlayerSupportsLinearColorSpace()
		{
			return _playerTexture.flags.IsLinear();
		}

		public override bool IsPlaybackStalled()
		{
			return _state.status.IsStalled();
		}

		public override float[] GetAffineTransform()
		{
			if (_state.selectedVideoTrack >= 0)
			{
				Native.AVPPlayerVideoTrackInfo videoTrackInfo = _videoTrackInfo[_state.selectedVideoTrack];
				Native.AVPAffineTransform transform = videoTrackInfo.transform;
				return new float[] { transform.a, transform.b, transform.c, transform.d, transform.tx, transform.ty };
			}
			else
			{
				return new float[] { 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f };
			}
		}

		public override long GetEstimatedTotalBandwidthUsed()
		{
			return 0;
		}

		public override bool IsExternalPlaybackSupported()
		{
			return _assetInfo.flags.IsCompatibleWithAirPlay();
		}
	}

	// IMediaProducer
	public sealed partial class PlatformMediaPlayer
	{
		public override int GetTextureCount()
		{
			return _playerTexture.planeCount;
		}

		public override Texture GetTexture(int index)
		{
			return _texturePlanes[index];
		}

		public override int GetTextureFrameCount()
		{
			return _playerTexture.frameCount;
		}

		public override bool SupportsTextureFrameCount()
		{
			return true;
		}

		public override long GetTextureTimeStamp()
		{
			return _playerTexture.itemTime;
		}

		public override bool RequiresVerticalFlip()
		{
			return _playerTexture.flags.IsFlipped();
		}

		public override TransparencyMode GetTextureTransparency()
		{
			if (_state.selectedVideoTrack >= 0)
			{
				Native.AVPPlayerVideoTrackInfo info = _videoTrackInfo[_state.selectedVideoTrack];
				if ((info.videoTrackFlags & Native.AVPPlayerVideoTrackFlags.HasAlpha) == Native.AVPPlayerVideoTrackFlags.HasAlpha)
				{
					return TransparencyMode.Transparent;
				}
			}
			return base.GetTextureTransparency();
		}

		public override Matrix4x4 GetYpCbCrTransform()
		{
			if (_videoTrackInfo.Length > 0 && _state.selectedVideoTrack >= 0)
				return _videoTrackInfo[_state.selectedVideoTrack].yCbCrTransform;
			else
				return Matrix4x4.identity;
		}

		internal override StereoPacking InternalGetTextureStereoPacking()
		{
			if (_state.selectedVideoTrack >= 0)
			{
				switch (_videoTrackInfo[_state.selectedVideoTrack].stereoMode)
				{
					case Native.AVPPlayerVideoTrackStereoMode.Unknown:
						return StereoPacking.Unknown;
					case Native.AVPPlayerVideoTrackStereoMode.Monoscopic:
						return StereoPacking.None;
					case Native.AVPPlayerVideoTrackStereoMode.StereoscopicLeftRight:
						return StereoPacking.LeftRight;
					case Native.AVPPlayerVideoTrackStereoMode.StereoscopicTopBottom:
						return StereoPacking.TopBottom;
					case Native.AVPPlayerVideoTrackStereoMode.StereoscopicRightLeft:
						return StereoPacking.Unknown;
					case Native.AVPPlayerVideoTrackStereoMode.StereoscopicCustom:
						return StereoPacking.CustomUV;
				}
			}
			return StereoPacking.Unknown;
		}
	}

	// IDispose
	public sealed partial class PlatformMediaPlayer
	{
		public override void Dispose()
		{
			Graphics.ExecuteCommandBuffer(_freeResourcesCommandBuffer);
			Native.AVPPlayerRelease(_player);
			_player = IntPtr.Zero;
		}
	}

	// Version
	public sealed partial class PlatformMediaPlayer
	{
		public override string GetVersion()
		{
			return Native.GetPluginVersion();
		}

		public override string GetExpectedVersion()
		{
			return Helper.ExpectedPluginVersion.Apple;
		}
	}

	// Media selection
	public sealed partial class PlatformMediaPlayer
	{
		internal override bool InternalIsChangedTracks(TrackType trackType)
		{
			return _state.status.HasUpdatedAssetInfo();
		}

		internal override int InternalGetTrackCount(TrackType trackType)
		{
			switch (trackType)
			{
				case TrackType.Video:
					return _videoTrackInfo.Length;
				case TrackType.Audio:
					return _audioTrackInfo.Length;
				case TrackType.Text:
					return _textTrackInfo.Length;
				default:
					return 0;
			}
		}

		internal override bool InternalSetActiveTrack(TrackType trackType, int index)
		{
			switch (trackType)
			{
				case TrackType.Video:
					return Native.AVPPlayerSetTrack(_player, Native.AVPPlayerTrackType.Video, index);

				case TrackType.Audio:
					return Native.AVPPlayerSetTrack(_player, Native.AVPPlayerTrackType.Audio, index);

				case TrackType.Text:
					return Native.AVPPlayerSetTrack(_player, Native.AVPPlayerTrackType.Text, index);

				default:
					return false;
			}
		}

		internal override TrackBase InternalGetTrackInfo(TrackType type, int index, ref bool isActiveTrack)
		{
			TrackBase track = null;
			switch (type)
			{
				case TrackType.Video:
					if (index >= 0 && index < _videoTrackInfo.Length)
					{
						Native.AVPPlayerVideoTrackInfo trackInfo = _videoTrackInfo[index];
						track = new VideoTrack(index, trackInfo.name, trackInfo.language, trackInfo.flags.IsDefault());
						isActiveTrack = _state.selectedVideoTrack == index;
					}
					break;

				case TrackType.Audio:
					if (index >= 0 && index < _audioTrackInfo.Length)
					{
						Native.AVPPlayerAudioTrackInfo trackInfo = _audioTrackInfo[index];
						track = new AudioTrack(index, trackInfo.name, trackInfo.language, trackInfo.flags.IsDefault());
						isActiveTrack = _state.selectedAudioTrack == index;
					}
					break;

				case TrackType.Text:
					if (index >= 0 && index < _textTrackInfo.Length)
					{
						Native.AVPPlayerTextTrackInfo trackInfo = _textTrackInfo[index];
						track = new TextTrack(index, trackInfo.name, trackInfo.language, trackInfo.flags.IsDefault());
						isActiveTrack = _state.selectedTextTrack == index;
					}
					break;

				default:
					break;
			}
			return track;
		}

		internal override bool InternalIsChangedTextCue()
		{
			return _state.status.HasUpdatedText();
		}

		internal override string InternalGetCurrentTextCue()
		{
			if (_playerText.buffer != IntPtr.Zero)
				return Marshal.PtrToStringUni(_playerText.buffer, _playerText.length);
			else
				return null;
		}
	}

#if !UNITY_EDITOR && UNITY_IOS
	// Media Caching
	public sealed partial class PlatformMediaPlayer
	{
        public override bool IsMediaCachingSupported()
        {
            return true;
        }

		public override void AddMediaToCache(string url, string headers, MediaCachingOptions options)
		{
			Native.MediaCachingOptions nativeOptions = new Native.MediaCachingOptions();
			GCHandle artworkHandle = new GCHandle();

			if (options != null)
			{
				nativeOptions.minimumRequiredBitRate = options.minimumRequiredBitRate;
				nativeOptions.minimumRequiredResolution_width = options.minimumRequiredResolution.x;
				nativeOptions.minimumRequiredResolution_height = options.minimumRequiredResolution.y;
				nativeOptions.title = options.title;
				if (options.artwork != null && options.artwork.Length > 0)
				{
					artworkHandle = GCHandle.Alloc(options.artwork, GCHandleType.Pinned);
					nativeOptions.artwork = artworkHandle.AddrOfPinnedObject();
					nativeOptions.artworkLength = options.artwork.Length;
				}
			}

			Native.AVPPluginCacheMediaForURL(url, headers, nativeOptions);

			if (artworkHandle.IsAllocated)
			{
				artworkHandle.Free();
			}
		}

		public override void CancelDownloadOfMediaToCache(string url)
		{
			Native.AVPPluginCancelDownloadOfMediaForURL(url);
		}

		public override void RemoveMediaFromCache(string url)
		{
			Native.AVPPluginRemoveCachedMediaForURL(url);
		}

        public override CachedMediaStatus GetCachedMediaStatus(string url, ref float progress)
        {
			return (CachedMediaStatus)Native.AVPPluginGetCachedMediaStatusForURL(url, ref progress);
        }
	}
#endif
}

#endif
