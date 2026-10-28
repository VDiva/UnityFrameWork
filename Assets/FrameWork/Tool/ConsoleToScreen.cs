using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ConsoleToScreen : MonoBehaviour
{
    const int maxLines = 50;
    const int maxLineLength = 120;
    private readonly Queue<string> _lines = new Queue<string>(maxLines);
    private readonly StringBuilder _builder = new StringBuilder(maxLines * (maxLineLength + 1));
    private string _logStr = "";
    private bool _dirty;
    private Vector2 _scrollPosition;
    private GUIStyle _style;
    private GUISkin _skin;
    public int fontSize = 15;

    private void OnEnable() { Application.logMessageReceived += HandleLog; }
    private void OnDisable() { Application.logMessageReceived -= HandleLog; }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (logString == null) return;
        int start = 0;
        for (int i = 0; i <= logString.Length; i++)
        {
            if (i < logString.Length && logString[i] != '\n') continue;
            int length = i - start;
            if (length > 0 && logString[start + length - 1] == '\r') length--;
            if (length == 0) AddLine("");
            else
            {
                for (int offset = 0; offset < length; offset += maxLineLength)
                    AddLine(logString.Substring(start + offset, System.Math.Min(maxLineLength, length - offset)));
            }
            start = i + 1;
        }
        _dirty = true;
    }

    private void AddLine(string line)
    {
        if (_lines.Count == maxLines) _lines.Dequeue();
        _lines.Enqueue(line);
    }

    private void OnGUI()
    {
        if (_dirty)
        {
            _builder.Clear();
            foreach (var line in _lines) _builder.AppendLine(line);
            _logStr = _builder.ToString();
            _dirty = false;
            _scrollPosition.y = Mathf.Infinity;
        }
        if (_style == null || _skin != GUI.skin)
        {
            _skin = GUI.skin;
            _style = new GUIStyle(_skin.label);
        }
        _style.fontSize = fontSize;
        GUILayout.BeginArea(new Rect(10f, 10f, Screen.width - 20f, Screen.height - 20f));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUIStyle.none, GUIStyle.none);
        GUILayout.Label(_logStr, _style);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
