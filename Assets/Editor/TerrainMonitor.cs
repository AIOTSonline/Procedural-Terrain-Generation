using UnityEngine;
using UnityEditor;
using Unity.Profiling;
using System.Collections.Generic;

public class TerrainMonitor : EditorWindow
{
    private ProfilerRecorder _totalUsedMemory;
    private ProfilerRecorder _gcUsedMemory;
    private ProfilerRecorder _gcAllocPerFrame;
    private ProfilerRecorder _drawCalls;
    private ProfilerRecorder _setPassCalls;

    private readonly Queue<float> _fpsHistory = new Queue<float>();
    private const int GRAPH_SAMPLES = 80;
    private float _fps, _minFps = float.MaxValue, _maxFps, _avgFps;
    private double _lastTime;

    private Vector2 _scroll;

    [MenuItem("Tools/Terrain Monitor")]
    public static void Open() => GetWindow<TerrainMonitor>("Terrain Monitor").minSize = new Vector2(300, 420);

    void OnEnable()
    {
        _totalUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 3);
        _gcUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 3);
        _gcAllocPerFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 3);
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 3);
        _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 3);
        EditorApplication.update += OnUpdate;
    }

    void OnDisable()
    {
        _totalUsedMemory.Dispose();
        _gcUsedMemory.Dispose();
        _gcAllocPerFrame.Dispose();
        _drawCalls.Dispose();
        _setPassCalls.Dispose();
        EditorApplication.update -= OnUpdate;
    }

    void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - _lastTime);
        _lastTime = now;

        if (dt > 0f)
        {
            _fps = 1f / dt;
            _fpsHistory.Enqueue(_fps);
            if (_fpsHistory.Count > GRAPH_SAMPLES) _fpsHistory.Dequeue();

            float sum = 0f;
            _minFps = float.MaxValue;
            _maxFps = 0f;
            foreach (var f in _fpsHistory)
            {
                sum += f;
                if (f < _minFps) _minFps = f;
                if (f > _maxFps) _maxFps = f;
            }
            _avgFps = sum / _fpsHistory.Count;
        }

        Repaint();
    }

    void OnGUI()
    {
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.12f, 0.12f, 0.15f));

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(10);

        if (!EditorApplication.isPlaying)
        {
            GUILayout.Space(40);
            GUILayout.Label("Enter Play Mode to see stats.", CenterLabel());
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawFPS();
        GUILayout.Space(12);
        DrawMemory();
        GUILayout.Space(12);
        DrawRuntime();

        GUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    void DrawFPS()
    {
        SectionLabel("FPS");

        Color c = _fps >= 60 ? Green : _fps >= 30 ? Yellow : Red;
        Row("Current", $"{_fps:F1}", c);
        Row("Average", $"{_avgFps:F1}", White);
        Row("Min / Max", $"{_minFps:F0} / {_maxFps:F0}", White);
        Row("Frame Time", $"{(1000f / Mathf.Max(_fps, 0.01f)):F2} ms", White);

        GUILayout.Space(6);
        DrawGraph(GUILayoutUtility.GetRect(position.width - 24, 56));
    }

    void DrawGraph(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.10f));

        float graphMax = Mathf.Max(_maxFps + 10f, 70f);
        DrawHLine(r, 30f, graphMax, new Color(0.7f, 0.3f, 0.3f, 0.6f));
        DrawHLine(r, 60f, graphMax, new Color(0.3f, 0.7f, 0.3f, 0.6f));

        var history = _fpsHistory.ToArray();
        if (history.Length < 2) return;

        float step = r.width / (GRAPH_SAMPLES - 1);
        int offset = GRAPH_SAMPLES - history.Length;

        for (int i = 1; i < history.Length; i++)
        {
            float x0 = r.x + (offset + i - 1) * step;
            float x1 = r.x + (offset + i) * step;
            float y0 = r.yMax - Mathf.Clamp01(history[i - 1] / graphMax) * r.height;
            float y1 = r.yMax - Mathf.Clamp01(history[i] / graphMax) * r.height;
            DrawSegment(x0, y0, x1, y1, Green, r);
        }
    }

    void DrawHLine(Rect r, float value, float graphMax, Color col)
    {
        float y = r.yMax - (value / graphMax) * r.height;
        EditorGUI.DrawRect(new Rect(r.x, y, r.width, 1), col);
    }

    void DrawSegment(float x0, float y0, float x1, float y1, Color col, Rect clip)
    {
        int steps = Mathf.Max(1, (int)Mathf.Abs(x1 - x0));
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            float px = Mathf.Lerp(x0, x1, t);
            float py = Mathf.Lerp(y0, y1, t);
            if (clip.Contains(new Vector2(px, py)))
                EditorGUI.DrawRect(new Rect(px, py, 2, 2), col);
        }
    }

    void DrawMemory()
    {
        SectionLabel("MEMORY");

        long used = GetValue(_totalUsedMemory);
        long gcUsed = GetValue(_gcUsedMemory);
        long gcAlloc = GetValue(_gcAllocPerFrame);

        Row("Total Used", FormatMB(used), used > 512 * MB ? Red : Green);
        Row("GC Heap Used", FormatMB(gcUsed), gcUsed > 64 * MB ? Yellow : Green);
        Row("GC Alloc/Frame", FormatKB(gcAlloc), gcAlloc > 0 ? Yellow : Green);

        if (gcAlloc > 0)
        {
            GUILayout.Space(4);
            var r = GUILayoutUtility.GetRect(position.width - 24, 18);
            EditorGUI.DrawRect(r, new Color(0.25f, 0.20f, 0.05f));
            GUI.Label(r, "  ⚠  GC allocs this frame — check for allocations in Update loops",
                new GUIStyle(EditorStyles.label) { fontSize = 9, normal = { textColor = Yellow } });
        }
    }

    void DrawRuntime()
    {
        SectionLabel("RENDER");

        long dc = GetValue(_drawCalls);
        long spc = GetValue(_setPassCalls);

        Row("Draw Calls", dc.ToString("N0"), dc > 1500 ? Red : dc > 800 ? Yellow : Green);
        Row("SetPass Calls", spc.ToString("N0"), spc > 400 ? Red : spc > 200 ? Yellow : Green);

        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Row("Active Terrains", terrains.Length.ToString(), White);
    }

    void SectionLabel(string text)
    {
        var r = GUILayoutUtility.GetRect(position.width - 24, 22);
        EditorGUI.DrawRect(r, new Color(0.18f, 0.18f, 0.22f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), Blue);
        GUI.Label(new Rect(r.x + 10, r.y + 3, r.width, r.height), text,
            new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.75f, 0.85f) }
            });
        GUILayout.Space(4);
    }

    void Row(string label, string value, Color valueColor)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.Label(label, new GUIStyle(EditorStyles.label)
        { fontSize = 11, normal = { textColor = new Color(0.65f, 0.65f, 0.65f) } });
        GUILayout.FlexibleSpace();
        GUILayout.Label(value, new GUIStyle(EditorStyles.label)
        { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = valueColor } });
        GUILayout.Space(12);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);
    }

    GUIStyle CenterLabel() => new GUIStyle(EditorStyles.label)
    {
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
    };

    static long GetValue(ProfilerRecorder r) => r.Valid && r.Count > 0 ? r.LastValue : 0;

    const long MB = 1024 * 1024;
    const long KB = 1024;
    static string FormatMB(long bytes) => $"{bytes / (float)MB:F1} MB";
    static string FormatKB(long bytes) => bytes == 0 ? "0 B" : bytes < KB ? $"{bytes} B" : $"{bytes / (float)KB:F1} KB";

    static readonly Color Green = new Color(0.30f, 0.85f, 0.50f);
    static readonly Color Yellow = new Color(1.00f, 0.80f, 0.20f);
    static readonly Color Red = new Color(0.95f, 0.35f, 0.35f);
    static readonly Color Blue = new Color(0.35f, 0.65f, 1.00f);
    static readonly Color White = new Color(0.90f, 0.90f, 0.90f);
}