using System;
using Cysharp.Threading.Tasks;
using GameData;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>常驻显示游戏 WebSocket 往返延迟及本地帧率。</summary>
    public sealed class WebNetworkLatencyOverlay : MonoBehaviour
    {
        const float ProbeInterval = 2f;

        double nextProbeTime;
        double fpsWindowStart;
        int fpsFrames;
        int peakSamples;
        int gapSamples;
        uint trackedObjectId;
        double lastMovingPacketTime;
        double lastTrackedPacketTime;
        float fps;
        float latestRtt = -1f;
        float peakRtt;
        float peakGap;
        int serverQueuePackets;
        int serverQueueWaitMs;
        int serverWriteMs;
        bool probing;
        string pingText = "Ping --";
        string gapText = "Pos --";
        string fpsText = "FPS --";
        GUIStyle labelStyle;

        void OnEnable()
        {
            fpsWindowStart = Time.realtimeSinceStartupAsDouble;
            nextProbeTime = fpsWindowStart;
            WebNetworkManager.TransformReceived += OnTransformReceived;
        }

        void OnDisable()
        {
            WebNetworkManager.TransformReceived -= OnTransformReceived;
        }

        void OnTransformReceived(NetworkTransformData data)
        {
            if (data == null || !data.HasVelocity)
                return;
            if (data.VelocityX * data.VelocityX + data.VelocityY * data.VelocityY < 0.04f)
            {
                if (data.ObjectId == trackedObjectId)
                {
                    lastTrackedPacketTime = 0;
                    gapText = "Pos idle";
                }
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (trackedObjectId == 0 || now - lastMovingPacketTime > 2.0)
            {
                trackedObjectId = data.ObjectId;
                lastTrackedPacketTime = 0;
                gapSamples = 0;
            }
            if (data.ObjectId != trackedObjectId) return;

            if (lastTrackedPacketTime > 0)
            {
                float gap = (float)((now - lastTrackedPacketTime) * 1000.0);
                if (gap > 0f && gap < 1500f)
                {
                    if (gapSamples++ == 0 || gapSamples > 10)
                    {
                        gapSamples = 1;
                        peakGap = gap;
                    }
                    else
                    {
                        peakGap = Mathf.Max(peakGap, gap);
                    }
                    gapText = $"Pos {gap:0}ms  max {peakGap:0}";
                }
            }
            lastTrackedPacketTime = now;
            lastMovingPacketTime = now;
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            fpsFrames++;
            if (now - fpsWindowStart >= 0.5)
            {
                fps = (float)(fpsFrames / (now - fpsWindowStart));
                fpsText = $"FPS {fps:0}  Srv {serverQueueWaitMs}+{serverWriteMs}ms";
                fpsFrames = 0;
                fpsWindowStart = now;
            }

            if (!LobbyWebNet.IsConnected)
            {
                latestRtt = -1f;
                pingText = "Ping offline";
                gapText = "Pos --";
                serverQueuePackets = 0;
                serverQueueWaitMs = 0;
                serverWriteMs = 0;
                trackedObjectId = 0;
                nextProbeTime = now;
                return;
            }

            if (trackedObjectId != 0 && now - lastMovingPacketTime > 2.0)
            {
                trackedObjectId = 0;
                gapText = "Pos idle";
            }

            if (!probing && now >= nextProbeTime)
            {
                nextProbeTime = now + ProbeInterval;
                ProbeAsync().Forget();
            }
        }

        async UniTask ProbeAsync()
        {
            probing = true;
            try
            {
                LobbyWebNet.ServerLatencySample sample = await LobbyWebNet.MeasureServerLatencyAsync();
                if (this == null) return;
                latestRtt = sample.RttMs;
                serverQueuePackets = sample.ServerQueuePackets;
                serverQueueWaitMs = sample.ServerQueueWaitMs;
                serverWriteMs = sample.ServerWriteMs;
                if (peakSamples++ == 0 || peakSamples > 10)
                {
                    peakSamples = 1;
                    peakRtt = sample.RttMs;
                }
                else
                {
                    peakRtt = Mathf.Max(peakRtt, sample.RttMs);
                }
                pingText = $"Ping {latestRtt:0}ms  max {peakRtt:0}";
            }
            catch (Exception)
            {
                if (this == null) return;
                latestRtt = -1f;
                pingText = LobbyWebNet.IsConnected ? "Ping timeout" : "Ping offline";
            }
            finally
            {
                probing = false;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.2f);
            float width = Mathf.Min(205f * scale, Screen.safeArea.width - 12f * scale);
            Rect safe = Screen.safeArea;
            var bounds = new Rect(safe.xMax - width - 6f * scale,
                Screen.height - safe.yMax + 6f * scale, width, 61f * scale);

            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            labelStyle.fontSize = Mathf.RoundToInt(12f * scale);

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -1000;

            GUI.color = latestRtt < 0f ? Color.gray :
                latestRtt < 100f ? new Color(0.65f, 1f, 0.65f) :
                latestRtt < 200f ? Color.yellow : new Color(1f, 0.55f, 0.55f);
            int pendingSends = LobbyWebNet.PendingSendCount;
            string displayedPing = pendingSends > 0 || serverQueuePackets > 0
                ? $"{pingText} Q{pendingSends}/{serverQueuePackets}" : pingText;
            GUI.Label(new Rect(bounds.x + 6f * scale, bounds.y + 2f * scale,
                bounds.width - 12f * scale, 18f * scale), displayedPing, labelStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(bounds.x + 6f * scale, bounds.y + 21f * scale,
                bounds.width - 12f * scale, 18f * scale), gapText, labelStyle);
            GUI.Label(new Rect(bounds.x + 6f * scale, bounds.y + 40f * scale,
                bounds.width - 12f * scale, 18f * scale), fpsText, labelStyle);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }
}
