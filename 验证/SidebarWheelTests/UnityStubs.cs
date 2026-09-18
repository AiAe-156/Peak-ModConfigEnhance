namespace UnityEngine
{
public class Object { }

public class Component : Object
{
    public bool isActiveAndEnabled = true;
    public RectTransform transform = null!;
}

public class MonoBehaviour : Component { }

public struct Vector2
{
    public float x;
    public float y;
    public Vector2(float x, float y) { this.x = x; this.y = y; }
}

public struct Rect
{
    public float width;
    public float height;
    public Rect(float width, float height) { this.width = width; this.height = height; }
}

public sealed class RectTransform : Component
{
    public Rect rect;
    public RectTransform(float height) => rect = new Rect(0f, height);
}

public static class Time
{
    public static float unscaledDeltaTime;
}

public static class Mathf
{
    public static bool Approximately(float a, float b) => System.MathF.Abs(a - b) < 0.000001f;
    public static bool Approximately(float a, int b) => Approximately(a, (float)b);
    public static float Abs(float value) => System.MathF.Abs(value);
    public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    public static float SmoothStep(float from, float to, float t)
    {
        t = Clamp01(t);
        t = t * t * (3f - 2f * t);
        return Lerp(from, to, t);
    }
}
}

namespace UnityEngine.EventSystems
{

public sealed class PointerEventData
{
    public UnityEngine.Vector2 scrollDelta;
}

public interface IBeginDragHandler { void OnBeginDrag(PointerEventData eventData); }
public interface IDragHandler { void OnDrag(PointerEventData eventData); }
public interface IEndDragHandler { void OnEndDrag(PointerEventData eventData); }
}

namespace UnityEngine.UI
{

public sealed class ScrollRect : UnityEngine.MonoBehaviour
{
    private object? _component;
    public UnityEngine.RectTransform? content;
    public UnityEngine.RectTransform? viewport;
    public bool vertical = true;
    public float verticalNormalizedPosition;

    public T? GetComponent<T>() where T : class => _component as T;
    public void SetComponent(object component) => _component = component;
}
}
