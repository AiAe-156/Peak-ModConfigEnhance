#nullable enable
using System;
using PEAKLib.UI.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zorro.UI.Animation;

namespace ModConfigEnhance;

/// <summary>
/// 「纸面」样式：游戏原版 UI 的底框是一张手绘的、带细微圆角与褶皱质感的 sprite，并且大约每秒三帧地换着画
/// （描边在抖，像手绘动画的「沸腾线」）。本项在运行时从原版对象上把这套东西抄下来，给左栏的行、表头、
/// 工具行、复选框、说明面板用，让它们和原版一个手感。
///
/// 来源按顺序找：① 选项单元格预制体（PEAKLib.UI 的 <c>Templates.SettingsCellPrefab</c>）里不在选项内容容器下、
/// 面积最大的 Image；② 页面上的搜索框（原版 FloatSettingCell 输入框）。
/// 抖动的来源有两种可能：Image 同对象上的 <see cref="ImageSpriteAnimator"/>（Sprite[] + delay，直接抄帧），
/// 或 Animator 用动画片段换 sprite（把它的 RuntimeAnimatorController 复制到我们的对象上；片段里的绑定路径
/// 只有为空时才对得上，路径非空就只记日志）。哪种都没有就只抄 sprite，不抖。
/// 全部找不到时退回自绘的圆角 sprite（<see cref="ModConfigSidebarWidgets.ApplyRounded"/>）。
/// 首次解析把来源、sprite 名、帧数 / Animator 写进日志，便于对照实机。
/// </summary>
internal static class ModConfigPaperStyle
{
    private static bool _resolved;
    private static Sprite? _sprite;
    private static Sprite[]? _frames;
    private static float _delay;
    private static Material? _material;
    private static Image.Type _type = Image.Type.Sliced;
    private static float _ppu = 1f;
    private static RuntimeAnimatorController? _controller;

    internal static bool Available => _sprite != null;

    /// <summary>所有纸面元素共用一个帧时钟，抖动同步；delay 抄自来源，没有就 0.33 秒。</summary>
    internal static float Delay => _delay > 0f ? _delay : 0.33f;

    internal static void Resolve(Transform page)
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        try
        {
            string source = "";
            Image? image = null;
            GameObject? prefab = Templates.SettingsCellPrefab;
            if (prefab != null)
            {
                image = FindBackdrop(prefab);
                source = "SettingsCell 预制体";
            }

            if (image == null || image.sprite == null)
            {
                Transform? search = page.Find("SearchInput");
                TMP_InputField? input = search != null ? search.GetComponentInChildren<TMP_InputField>(true) : null;
                image = input != null && input.image != null && input.image.sprite != null ? input.image : (search != null ? FindBackdrop(search.gameObject) : null);
                source = "搜索框";
            }

            if (image == null || image.sprite == null)
            {
                Plugin.Log.LogWarning("[面板布局] 没找到原版纸面底图（预制体与搜索框都没有带 sprite 的 Image），左栏用自绘圆角。");
                return;
            }

            _sprite = image.sprite;
            _material = image.material;
            _type = image.type == Image.Type.Simple ? Image.Type.Sliced : image.type;
            _ppu = image.pixelsPerUnitMultiplier;

            // 抖动在着色器里（Scouts/UI）：SettingsCell 预制体那份材质叫「UI 1」，实机不抖；搜索框 / 返回按钮 /
            // 筛选下拉框用的是「UI」，会抖。优先拿会抖的那份。
            Material? wobbly = FindWobblyMaterial(page);
            if (wobbly != null)
            {
                _material = wobbly;
            }

            ImageSpriteAnimator? animator = image.GetComponent<ImageSpriteAnimator>();
            string wobble;
            if (animator != null && animator.sprites != null && animator.sprites.Length > 1)
            {
                _frames = animator.sprites;
                _delay = animator.delay;
                wobble = $"ImageSpriteAnimator {_frames.Length} 帧 / {_delay:0.###}s";
            }
            else
            {
                Animator? unityAnimator = image.GetComponentInParent<Animator>();
                if (unityAnimator != null && unityAnimator.runtimeAnimatorController != null)
                {
                    string path = RelativePath(unityAnimator.transform, image.transform);
                    if (path.Length == 0)
                    {
                        _controller = unityAnimator.runtimeAnimatorController;
                        wobble = $"Animator {_controller.name}（同对象，已复制）";
                    }
                    else
                    {
                        wobble = $"Animator {unityAnimator.runtimeAnimatorController.name} 在上级，Image 相对路径 \"{path}\"，本版未复制";
                    }
                }
                else
                {
                    wobble = "无（不抖）";
                }
            }

            Plugin.Log.LogInfo($"[面板布局] 纸面样式来源：{source} / {image.name}，sprite {_sprite.name}（{_type}，倍率 {_ppu:0.##}），材质 {(_material != null ? _material.name + "/" + (_material.shader != null ? _material.shader.name : "?") : "null")}，抖动：{wobble}。");
            DumpMaterial("预制体材质", image.material);
            DumpMaterial("采用材质", _material);
            Dump("纸面来源", image.transform);
            Transform? searchInput = page.Find("SearchInput");
            if (searchInput != null)
            {
                Dump("搜索框", searchInput);
            }

            Transform? back = page.Find("UI_MainMenuButton_Back");
            if (back != null)
            {
                Dump("返回按钮", back);
            }
        }
        catch (Exception ex)
        {
            _sprite = null;
            Plugin.Log.LogWarning($"[面板布局] 解析原版纸面样式失败，左栏用自绘圆角: {ex.Message}");
        }
    }

    /// <summary>
    /// 诊断：把一个对象及其两层子孙的组件、Image 的材质 / 着色器写进日志。抖动的来源还没找到
    /// （SettingsCell 的底图既没有 ImageSpriteAnimator 也没有 Animator），先把实机结构打出来对照。
    /// </summary>
    private static void Dump(string tag, Transform root)
    {
        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[面板布局] 结构诊断 ").Append(tag).AppendLine();
            DumpNode(sb, root, 0, 3);
            Plugin.Log.LogDebug(sb.ToString());
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"[面板布局] 结构诊断 {tag} 失败: {ex.Message}");
        }
    }

    private static void DumpNode(System.Text.StringBuilder sb, Transform node, int depth, int maxDepth)
    {
        sb.Append(' ', depth * 2).Append(node.name).Append(node.gameObject.activeSelf ? "" : " (inactive)").Append(": ");
        foreach (Component component in node.GetComponents<Component>())
        {
            if (component == null)
            {
                continue;
            }

            sb.Append(component.GetType().Name);
            if (component is Image img)
            {
                sb.Append('[').Append(img.sprite != null ? img.sprite.name : "无sprite").Append(", mat=").Append(img.material != null ? img.material.name : "null")
                    .Append('/').Append(img.material != null && img.material.shader != null ? img.material.shader.name : "?")
                    .Append(", render=").Append(img.materialForRendering != null && img.materialForRendering.shader != null ? img.materialForRendering.shader.name : "?")
                    .Append(", ").Append(img.type).Append(']');
            }
            else if (component is Animator anim)
            {
                sb.Append('[').Append(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "无控制器").Append(']');
            }

            sb.Append(' ');
        }

        sb.AppendLine();
        if (depth >= maxDepth)
        {
            return;
        }

        foreach (Transform child in node)
        {
            DumpNode(sb, child, depth + 1, maxDepth);
        }
    }

    private const string WobblyShader = "Scouts/UI";

    /// <summary>从搜索框、筛选下拉框、返回按钮的底图里找 Scouts/UI 着色器的材质，优先名字正好叫「UI」的。</summary>
    private static Material? FindWobblyMaterial(Transform page)
    {
        Material? fallback = null;
        foreach (Image candidate in Candidates(page))
        {
            Material? material = candidate != null ? candidate.material : null;
            if (material == null || material.shader == null || material.shader.name != WobblyShader)
            {
                continue;
            }

            if (material.name == "UI")
            {
                return material;
            }

            fallback ??= material;
        }

        return fallback;
    }

    private static System.Collections.Generic.IEnumerable<Image> Candidates(Transform page)
    {
        Transform? search = page.Find("SearchInput");
        if (search != null)
        {
            foreach (Image image in search.GetComponentsInChildren<Image>(true))
            {
                yield return image;
            }
        }

        PEAKLib.ModConfig.Components.ModSettingsMenu? menu = PEAKLib.ModConfig.Components.ModSettingsMenu.Instance;
        if (menu != null && menu.FilterDropdown != null && menu.FilterDropdown.Background != null)
        {
            yield return menu.FilterDropdown.Background;
        }

        Transform? back = page.Find("UI_MainMenuButton_Back");
        if (back != null)
        {
            foreach (Image image in back.GetComponentsInChildren<Image>(true))
            {
                yield return image;
            }
        }
    }

    /// <summary>把材质的全部着色器属性与当前值写进 Debug 日志：对照「UI」与「UI 1」差在哪一格。</summary>
    private static void DumpMaterial(string tag, Material? material)
    {
        if (material == null || material.shader == null)
        {
            return;
        }

        try
        {
            Shader shader = material.shader;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[面板布局] 材质诊断 ").Append(tag).Append(": ").Append(material.name).Append(" / ").Append(shader.name)
                .Append(" keywords=[").Append(string.Join(" ", material.shaderKeywords)).Append("] renderQueue=").Append(material.renderQueue).AppendLine();
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                string name = shader.GetPropertyName(i);
                UnityEngine.Rendering.ShaderPropertyType type = shader.GetPropertyType(i);
                sb.Append("  ").Append(name).Append(" (").Append(type).Append(") = ");
                switch (type)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        sb.Append(material.GetColor(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        sb.Append(material.GetVector(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        sb.Append(material.GetFloat(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        Texture? texture = material.GetTexture(name);
                        sb.Append(texture != null ? texture.name : "null");
                        break;
                    default:
                        sb.Append('?');
                        break;
                }

                sb.AppendLine();
            }

            Plugin.Log.LogDebug(sb.ToString());
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"[面板布局] 材质诊断 {tag} 失败: {ex.Message}");
        }
    }

    private static Image? FindBackdrop(GameObject root)
    {
        SettingsUICell? cell = root.GetComponent<SettingsUICell>();
        Image? best = null;
        float bestArea = 0f;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (cell != null && cell.m_settingsContentParent != null && image.transform.IsChildOf(cell.m_settingsContentParent))
            {
                continue;
            }

            if (image.sprite == null)
            {
                continue;
            }

            Rect r = image.rectTransform.rect;
            float area = Mathf.Abs(r.width * r.height);
            if (area > bestArea)
            {
                bestArea = area;
                best = image;
            }
        }

        return best;
    }

    private static string RelativePath(Transform root, Transform child)
    {
        string path = "";
        Transform? current = child;
        while (current != null && current != root)
        {
            path = path.Length == 0 ? current.name : current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    /// <summary>
    /// 给 Image 套上纸面：sprite / 材质 / 类型 / 倍率，并按来源挂帧动画或 Animator。颜色不动（着色靠 Image.color，
    /// 昼夜匹配也靠它）。没解析到纸面时退回自绘圆角。
    /// </summary>
    internal static void Apply(Image image, float fallbackCorner)
    {
        if (_sprite == null)
        {
            ModConfigSidebarWidgets.ApplyRounded(image, fallbackCorner);
            return;
        }

        image.sprite = _sprite;
        image.material = _material;
        image.type = _type;
        image.pixelsPerUnitMultiplier = _ppu;
        image.preserveAspect = false;
        AttachWobble(image.gameObject);
    }

    private static void AttachWobble(GameObject target)
    {
        if (_frames != null && _frames.Length > 1)
        {
            if (target.GetComponent<PaperWobble>() == null)
            {
                target.AddComponent<PaperWobble>();
            }
        }
        else if (_controller != null && target.GetComponent<Animator>() == null)
        {
            Animator animator = target.AddComponent<Animator>();
            animator.runtimeAnimatorController = _controller;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    /// <summary>会抖的那份材质（Scouts/UI 的「UI」）；没解析到就 null。</summary>
    internal static Material? WobblyMaterial =>
        _material != null && _material.shader != null && _material.shader.name == WobblyShader ? _material : null;

    /// <summary>只换材质不换 sprite：✓ 细条、置顶竖条、滚动条这些自绘圆角图形也一起抖。</summary>
    internal static void ApplyWobble(Image image)
    {
        Material? material = WobblyMaterial;
        if (material != null)
        {
            image.material = material;
        }
    }

    /// <summary>
    /// ModConfig 右侧选项单元格的底图用的是预制体那份「UI 1」（不抖），换成会抖的「UI」，与左栏、说明面板同步。
    /// 只换材质，颜色不动。
    /// </summary>
    internal static void ApplyWobbleToCell(SettingsUICell cell)
    {
        Material? material = WobblyMaterial;
        if (material == null)
        {
            return;
        }

        Image? backdrop = FindBackdrop(cell.gameObject);
        if (backdrop != null && backdrop.material != material)
        {
            backdrop.material = material;
        }
    }

    /// <summary>说明面板从真实单元格抄底图时也走这里：帧 / Animator 与 Resolve 里的一致。</summary>
    internal static void CopyFrom(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.material = _material != null && _material.shader != null && _material.shader.name == WobblyShader ? _material : source.material;
        target.type = source.type;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.preserveAspect = false;
        AttachWobble(target.gameObject);
    }

    // ── 共用帧时钟 ─────────────────────────────────────────────────

    private static int _tickFrame = -1;
    private static int _tick;
    private static float _tickTime;

    /// <summary>当前帧序号（每 Delay 秒加一）；同一渲染帧内多次调用返回同值。</summary>
    internal static int Tick
    {
        get
        {
            int frame = Time.frameCount;
            if (frame != _tickFrame)
            {
                _tickFrame = frame;
                _tickTime += Time.unscaledDeltaTime;
                if (_tickTime >= Delay)
                {
                    _tickTime = 0f;
                    _tick++;
                }
            }

            return _tick;
        }
    }

    /// <summary>照抄 Zorro 的 ImageSpriteAnimator，只是用共用时钟，所有纸面元素同步换帧。</summary>
    internal sealed class PaperWobble : MonoBehaviour
    {
        private Image? _image;
        private int _shown = -1;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void Update()
        {
            if (_image == null || _frames == null || _frames.Length == 0)
            {
                return;
            }

            int index = Tick % _frames.Length;
            if (index != _shown)
            {
                _shown = index;
                _image.sprite = _frames[index];
            }
        }
    }
}
