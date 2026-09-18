#nullable enable
using System.Collections;
using System.Collections.Generic;
using PEAKLib.ModConfig.Components;
using PEAKLib.UI;
using PEAKLib.UI.Elements;
using UnityEngine;

namespace ModConfigEnhance;

/// <summary>
/// 左栏的逐行出现：与原版设置页 / ModConfig 右侧单元格同一套做法——每行一个 CanvasGroup，alpha 从 0 Lerp 到 1，
/// 每行间隔 <see cref="Interval"/> 秒，出现时播单元格预制体上的 <c>fadeInSFX</c>（借 <c>Templates.SettingsCellPrefab</c> 的那份）。
/// 页面每次打开时模组列表跑一遍；选中模组、分区行重建时分区行跑一遍。
/// </summary>
internal sealed class SidebarCascade : MonoBehaviour
{
    private const float Interval = 0.05f;
    private const float LerpSpeed = 10f;

    internal PeakHorizontalTabs? ModTabs;
    internal GameObject? Header;

    private static SFX_Instance? _sfx;
    private static bool _sfxResolved;

    private Coroutine? _modRun;
    private Coroutine? _sectionRun;
    private readonly List<CanvasGroup> _fading = new List<CanvasGroup>();

    private static SFX_Instance? Sfx
    {
        get
        {
            if (!_sfxResolved)
            {
                _sfxResolved = true;
                try
                {
                    SettingsUICell? cell = Templates.SettingsCellPrefab != null ? Templates.SettingsCellPrefab.GetComponent<SettingsUICell>() : null;
                    _sfx = cell != null ? cell.fadeInSFX : null;
                }
                catch
                {
                    _sfx = null;
                }
            }

            return _sfx;
        }
    }

    private void OnEnable()
    {
        List<GameObject> rows = new List<GameObject>();
        if (Header != null)
        {
            rows.Add(Header);
        }

        if (ModTabs != null)
        {
            // 按当前显示顺序（置顶行在前）
            foreach (Transform child in ModTabs.GetComponent<UnityEngine.UI.ScrollRect>().content)
            {
                if (child.gameObject.activeSelf && child.GetComponent<ModdedTABSButton>() != null)
                {
                    rows.Add(child.gameObject);
                }
            }
        }

        _modRun = Restart(_modRun, rows);
    }

    private void OnDisable()
    {
        _modRun = null;
        _sectionRun = null;
        foreach (CanvasGroup group in _fading)
        {
            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        _fading.Clear();
    }

    /// <summary>分区行重建 / 展开后调用（UpdateSectionTabs 的 postfix）。</summary>
    internal void PlaySections(IEnumerable<GameObject> rows)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _sectionRun = Restart(_sectionRun, new List<GameObject>(rows));
    }

    private Coroutine? Restart(Coroutine? previous, List<GameObject> rows)
    {
        if (previous != null)
        {
            StopCoroutine(previous);
        }

        if (rows.Count == 0)
        {
            return null;
        }

        List<CanvasGroup> groups = new List<CanvasGroup>(rows.Count);
        foreach (GameObject row in rows)
        {
            if (row == null || !row.activeInHierarchy)
            {
                continue;
            }

            CanvasGroup group = row.GetComponent<CanvasGroup>() ?? row.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            groups.Add(group);
        }

        return StartCoroutine(Run(groups));
    }

    private IEnumerator Run(List<CanvasGroup> groups)
    {
        foreach (CanvasGroup group in groups)
        {
            if (group == null)
            {
                continue;
            }

            _fading.Add(group);
            SFX_Instance? sfx = Sfx;
            if (sfx != null)
            {
                sfx.Play();
            }

            yield return new WaitForSecondsRealtime(Interval);
        }
    }

    private void Update()
    {
        if (_fading.Count == 0)
        {
            return;
        }

        for (int i = _fading.Count - 1; i >= 0; i--)
        {
            CanvasGroup group = _fading[i];
            if (group == null)
            {
                _fading.RemoveAt(i);
                continue;
            }

            group.alpha = Mathf.Lerp(group.alpha, 1f, Time.unscaledDeltaTime * LerpSpeed);
            if (group.alpha > 0.995f)
            {
                group.alpha = 1f;
                _fading.RemoveAt(i);
            }
        }
    }
}
