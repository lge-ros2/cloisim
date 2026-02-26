using UnityEngine;

public class PerformanceLogger : MonoBehaviour
{
    private int _frameCount = 0;
    private int _fixedUpdateCount = 0;
    private float _timeAccumulator = 0f;

    // Expose last-second counters for other systems (e.g. Clock → WorldStatistics)
    public static int LastRenderFPS { get; private set; } = 0;
    public static int LastPhysicsHz { get; private set; } = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Initialize()
    {
        var go = new GameObject("PerformanceLogger");
        go.AddComponent<PerformanceLogger>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        _frameCount++;
        _timeAccumulator += Time.unscaledDeltaTime;

        if (_timeAccumulator >= 1.0f)
        {
            Debug.Log($"[PerformanceSurvey] Render FPS: {_frameCount} | Physics Hz: {_fixedUpdateCount}");
            LastRenderFPS = _frameCount;
            LastPhysicsHz = _fixedUpdateCount;
            _frameCount = 0;
            _fixedUpdateCount = 0;
            _timeAccumulator -= 1.0f;
        }
    }

    void FixedUpdate()
    {
        _fixedUpdateCount++;
    }
}
