using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{

public class Object { }
public class Component : Object
{
    public GameObject gameObject { get; internal set; } = null!;
    public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    public T AddComponent<T>() where T : Component, new() => gameObject.AddComponent<T>();
}
public class GameObject : Object
{
    private readonly Dictionary<Type, Component> components = new();
    public string name;
    public bool activeSelf = true;
    public bool activeInHierarchy => activeSelf;
    public Transform transform { get; }
    public GameObject(string name) { this.name = name; transform = AddComponent<Transform>(); }
    public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; components[typeof(T)] = c; return c; }
    public T? GetComponent<T>() where T : Component => components.TryGetValue(typeof(T), out var c) ? (T)c : null;
    public void SetActive(bool value) => activeSelf = value;
}
public class Transform : Component, IEnumerable<Transform>
{
    private readonly List<Transform> children = new();
    public void AddChild(GameObject child) => children.Add(child.transform);
    public IEnumerator<Transform> GetEnumerator() => children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
public class MonoBehaviour : Component
{
    protected Coroutine StartCoroutine(IEnumerator routine)
    {
        var coroutine = new Coroutine(routine);
        routine.MoveNext(); // Unity starts a coroutine immediately, up to its first yield.
        return coroutine;
    }
    protected void StopCoroutine(Coroutine routine) => routine.Stopped = true;
    public bool isActiveAndEnabled => gameObject.activeInHierarchy;
}
public sealed class Coroutine
{
    public bool Stopped;
    public IEnumerator Routine { get; }
    public Coroutine(IEnumerator routine) { Routine = routine; }
}
public sealed class CanvasGroup : Component { public float alpha; }
public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
public static class Mathf
{
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
public static class Time { public static float unscaledDeltaTime = 0.016f; }
}

namespace UnityEngine.UI
{
public sealed class ScrollRect : UnityEngine.Component { public UnityEngine.Transform? content; }
}

namespace PEAKLib.ModConfig.Components
{
public sealed class ModdedTABSButton : UnityEngine.Component { public bool Selected; }
}

namespace PEAKLib.UI
{
public sealed class PeakHorizontalTabs : UnityEngine.Component
{
}
public sealed class SFX_Instance { public int Plays; public void Play() => Plays++; }
public static class Templates { public static SettingsUICell? SettingsCellPrefab; }
public sealed class SettingsUICell : UnityEngine.Component { public SFX_Instance? fadeInSFX; }
}

namespace PEAKLib.UI.Elements
{
}
