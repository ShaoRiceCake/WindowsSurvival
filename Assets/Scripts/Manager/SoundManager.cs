using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    /// <summary>
    /// 单例访问
    /// </summary>
    public static SoundManager Instance => instance;

    // 四个声音通道
    private AudioSource bgmSource;         // 背景音乐通道
    private AudioSource sfxSource;         // 音效通道
    private AudioSource heartbeatSource;   // 心跳通道（无音频效果）
    private Dictionary<string, AudioSource> cardLoopSources = new(); // 卡牌循环音效通道

    private readonly Dictionary<string, float> loopGains = new();
    private float heartbeatGain = 1;
    private Coroutine fadeCoroutine;       // BGM淡入淡出协程
    private float targetVolume;            // BGM目标音量

    // 通道音频效果组件
    private AudioLowPassFilter bgmLowPass;         // BGM低通滤波器
    private AudioDistortionFilter bgmDistortion;   // BGM失真效果
    private AudioLowPassFilter sfxLowPass;         // SFX低通滤波器
    private AudioDistortionFilter sfxDistortion;   // SFX失真效果

    // 危险状态下的音效参数
    private readonly float _defaultCutoffFrequency = 5000f;         // 正常低通截止频率
    private readonly float _dangerCutoffFrequencyLow = 2200f;       // 低危低通截止频率
    private readonly float _dangerCutoffFrequencyHigh = 1000f;      // 高危低通截止频率
    private readonly float _dangerDistortionLevelLow = 0.4f;        // 低危失真
    private readonly float _dangerDistortionLevelHigh = 0.7f;       // 高危失真
    private readonly float _defaultDistortionLevel = 0f;            // 正常失真

    /// <summary>
    /// 初始化所有音频通道
    /// </summary>
    private void Awake()
    {
        instance = this;
        InitAudioChannels();
        GameDataManager.Instance.onBGMVolumeChanged.AddListener(OnBGMVolumeChanged);
        GameSettings.Changed += ApplySettings;
        ApplySettings();
    }

    private void OnDestroy()
    {
        GameDataManager.Instance.onBGMVolumeChanged.RemoveListener(OnBGMVolumeChanged);
        GameSettings.Changed -= ApplySettings;
    }

    /// <summary>
    /// 初始化BGM、SFX、心跳三个主通道
    /// </summary>
    private void InitAudioChannels()
    {
        // BGM通道及其效果
        var bgmObj = new GameObject("BGM_AudioSource");
        bgmObj.transform.parent = this.transform;
        bgmSource = bgmObj.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmLowPass = bgmObj.AddComponent<AudioLowPassFilter>();
        bgmLowPass.cutoffFrequency = _defaultCutoffFrequency;
        bgmLowPass.enabled = false;
        bgmDistortion = bgmObj.AddComponent<AudioDistortionFilter>();
        bgmDistortion.distortionLevel = _defaultDistortionLevel;
        bgmDistortion.enabled = false;

        // SFX通道及其效果
        var sfxObj = new GameObject("SFX_AudioSource");
        sfxObj.transform.parent = this.transform;
        sfxSource = sfxObj.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxLowPass = sfxObj.AddComponent<AudioLowPassFilter>();
        sfxLowPass.cutoffFrequency = _defaultCutoffFrequency;
        sfxLowPass.enabled = false;
        sfxDistortion = sfxObj.AddComponent<AudioDistortionFilter>();
        sfxDistortion.distortionLevel = _defaultDistortionLevel;
        sfxDistortion.enabled = false;

        // 心跳通道（无任何音频效果组件）
        var heartbeatObj = new GameObject("Heartbeat_AudioSource");
        heartbeatObj.transform.parent = this.transform;
        heartbeatSource = heartbeatObj.AddComponent<AudioSource>();
        heartbeatSource.playOnAwake = false;
        heartbeatSource.loop = true;
        heartbeatSource.volume = 1f;
    }

    /// <summary>
    /// 响应BGM音量变化事件
    /// </summary>
    private void ApplySettings()
    {
        OnBGMVolumeChanged();
        sfxSource.volume = GetNormalizedSFXVolume();
        heartbeatSource.volume = heartbeatGain * GetNormalizedSFXVolume();
        foreach (var pair in cardLoopSources)
            if (pair.Value != null) pair.Value.volume = (loopGains.TryGetValue(pair.Key, out var gain) ? gain : .3f) * GetNormalizedSFXVolume();
    }

    private void OnBGMVolumeChanged()
    {
        float newVolume = GetNormalizedBGMVolume();
        if (fadeCoroutine != null)
            targetVolume = newVolume;
        else
            bgmSource.volume = targetVolume = newVolume;
    }

    /// <summary>
    /// 根据危险等级应用音频效果（低通、失真、心跳）
    /// </summary>
    public void ApplyDangerEffects(DangerLevelEnum dangerLevel)
    {
        switch (dangerLevel)
        {
            case DangerLevelEnum.High:
                // 高危：BGM/SFX/卡牌循环都加重失真和低通，心跳更大
                ApplyEffectsToMainChannels(_dangerCutoffFrequencyHigh, _dangerDistortionLevelHigh, true);
                ApplyEffectsToCardLoops(_dangerCutoffFrequencyHigh, _dangerDistortionLevelHigh, true);
                PlayHeartbeat("心跳_01", 1f, 1f);
                break;
            case DangerLevelEnum.Low:
                // 低危：BGM/SFX/卡牌循环略有失真和低通，心跳较小
                ApplyEffectsToMainChannels(_dangerCutoffFrequencyLow, _dangerDistortionLevelLow, true);
                ApplyEffectsToCardLoops(_dangerCutoffFrequencyLow, _dangerDistortionLevelLow, true);
                PlayHeartbeat("心跳_01", 0.7f, 1f);
                break;
            case DangerLevelEnum.None:
                // 正常：关闭所有音频效果，停止心跳
                ApplyEffectsToMainChannels(_defaultCutoffFrequency, _defaultDistortionLevel, false);
                ApplyEffectsToCardLoops(_defaultCutoffFrequency, _defaultDistortionLevel, false);
                StopHeartbeat();
                break;
        }
    }

    /// <summary>
    /// 对BGM和SFX主通道应用音频效果
    /// </summary>
    private void ApplyEffectsToMainChannels(float cutoff, float distortion, bool enable)
    {
        SetChannelEffects(bgmLowPass, bgmDistortion, cutoff, distortion, enable);
        SetChannelEffects(sfxLowPass, sfxDistortion, cutoff, distortion, enable);
    }

    /// <summary>
    /// 对所有卡牌循环音效通道应用音频效果
    /// </summary>
    private void ApplyEffectsToCardLoops(float cutoff, float distortion, bool enable)
    {
        foreach (var source in cardLoopSources.Values)
        {
            var lp = source.GetComponent<AudioLowPassFilter>();
            var ds = source.GetComponent<AudioDistortionFilter>();
            SetChannelEffects(lp, ds, cutoff, distortion, enable);
        }
    }

    /// <summary>
    /// 设置单个通道的低通和失真效果
    /// </summary>
    private void SetChannelEffects(AudioLowPassFilter lp, AudioDistortionFilter ds, float cutoff, float distortion, bool enable)
    {
        if (lp != null)
        {
            lp.enabled = enable;
            lp.cutoffFrequency = cutoff;
        }
        if (ds != null)
        {
            ds.enabled = enable;
            ds.distortionLevel = distortion;
        }
    }

    /// <summary>
    /// 播放心跳声（独立通道，无效果）
    /// </summary>
    public void PlayHeartbeat(string clipName = "心跳_01", float volume = 1f, float pitch = 1f)
    {
        var clip = GetClip(clipName, "Music");
        // 如果没有找到心跳音频资源，则确保心跳停止并返回
        if (clip == null)
        {
            StopHeartbeat();
            return;
        }
        if (heartbeatSource.isPlaying && heartbeatSource.clip == clip)
        {
            heartbeatGain = volume;
            heartbeatSource.volume = volume * GetNormalizedSFXVolume();
            heartbeatSource.pitch = pitch;
            return;
        }
        heartbeatSource.Stop();
        heartbeatSource.clip = clip;
        heartbeatGain = volume;
        heartbeatSource.volume = volume * GetNormalizedSFXVolume();
        heartbeatSource.pitch = pitch;
        heartbeatSource.Play();
    }

    /// <summary>
    /// 停止心跳声
    /// </summary>
    public void StopHeartbeat()
    {
        if (heartbeatSource.isPlaying)
            heartbeatSource.Stop();
    }

    /// <summary>
    /// 播放BGM，支持淡入淡出
    /// </summary>
    public void PlayBGM(string clipName, bool loop = true, float fadeDuration = 1f, float volumeMultiplier = 1f)
    {
        var clip = GetClip(clipName, "Music");
        // 防护：未找到BGM则不尝试播放
        if (clip == null) return;
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeSwitchBGM(clip, loop, fadeDuration, volumeMultiplier));
    }

    /// <summary>
    /// BGM切换淡入淡出协程
    /// </summary>
    private IEnumerator FadeSwitchBGM(AudioClip clip, bool loop, float fadeDuration, float volumeMultiplier)
    {
        targetVolume = GetNormalizedBGMVolume();
        if (!bgmSource.isPlaying)
        {
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.Play();
            yield return FadeVolume(bgmSource, 0f, targetVolume * volumeMultiplier, fadeDuration);
        }
        else
        {
            float startVolume = bgmSource.volume;
            yield return FadeVolume(bgmSource, startVolume, 0f, fadeDuration / 2f);
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.Play();
            yield return FadeVolume(bgmSource, 0f, targetVolume * volumeMultiplier, fadeDuration / 2f);
        }
        fadeCoroutine = null;
    }

    /// <summary>
    /// 停止BGM，支持淡出
    /// </summary>
    public void StopBGM(float fadeDuration = 1f)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutBGM(fadeDuration));
    }

    /// <summary>
    /// BGM淡出协程
    /// </summary>
    private IEnumerator FadeOutBGM(float fadeDuration)
    {
        float startVolume = bgmSource.volume;
        yield return FadeVolume(bgmSource, startVolume, 0f, fadeDuration);
        bgmSource.Stop();
        bgmSource.volume = startVolume;
        fadeCoroutine = null;
    }

    /// <summary>
    /// 通用音量渐变协程
    /// </summary>
    private IEnumerator FadeVolume(AudioSource source, float from, float to, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            source.volume = Mathf.Lerp(from, to, timer / duration);
            yield return null;
        }
        source.volume = to;
    }

    /// <summary>
    /// 播放音效（SFX通道），可选随机音量和音调
    /// </summary>
    public void PlaySound(string clipName, bool isRandom = false, float volumeMultiplier = 1f)
    {
        var clip = GetClip(clipName, "SFX");
        float baseVolume = 1f; // SFX channel gain is applied by ApplySettings.
        if (isRandom)
        {
            float volumeVariation = 1f + UnityEngine.Random.Range(-0.1f, 0.1f);
            float finalVolume = Mathf.Clamp(baseVolume * volumeVariation, 0f, 1f);
            sfxSource.pitch = 1f + UnityEngine.Random.Range(-0.1f, 0.1f);
            sfxSource.PlayOneShot(clip, finalVolume * volumeMultiplier);
        }
        else
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(clip, baseVolume * volumeMultiplier);
        }
    }

    /// <summary>
    /// 加载音频资源
    /// </summary>
    private AudioClip GetClip(string clipName, string type)
    {
        // 构建资源路径
        string path = $"Audio/{type}/{clipName}";
        
        // 尝试加载音效
        AudioClip clip = Resources.Load<AudioClip>(path);

        // 检查是否加载失败
        if (clip == null)
        {
            // 仅输出警告（黄色），不会中断程序
            Debug.LogWarning($"音效文件未找到: {path}");
            return null; // 返回空而不是抛出异常
        }

        return clip;
    }

    /// <summary>
    /// 播放特定卡牌的循环音效（每个卡牌通道都加效果组件）
    /// </summary>
    public void PlayCardLoopSound(string cardId, string clipName, float volume = 0.3f)
    {
        // 如果详情窗口当前正在显示此卡牌，则将初始循环音量设置为更高的值（详情界面打开时应该听到更大的循环音）
        float initialVolume = volume;
        if (WindowsManager.Instance.TryGetOpenedWindow("Details", out var window, true))
        {
            var dw = window as DetailsWindow;
            if (dw.CurrentDisplayedCard != null && dw.CurrentDisplayedCard.CardId == cardId)
            {
                initialVolume = 1.0f;
            }
        }

        var cardObj = new GameObject($"CardLoop_{cardId}");
        cardObj.transform.parent = this.transform;
        var source = cardObj.AddComponent<AudioSource>();
        var clip = GetClip(clipName, "SFX");
        if (clip == null)
        {
            Debug.LogWarning($"未找到音效: {clipName}");
            Destroy(cardObj);
            return;
        }
        source.clip = clip;
        source.loop = true;
        loopGains[cardId] = initialVolume;
        source.volume = initialVolume * GetNormalizedSFXVolume();
        // 独立添加效果组件
        var lp = cardObj.AddComponent<AudioLowPassFilter>();
        lp.cutoffFrequency = _defaultCutoffFrequency;
        lp.enabled = false;
        var ds = cardObj.AddComponent<AudioDistortionFilter>();
        ds.distortionLevel = _defaultDistortionLevel;
        ds.enabled = false;
        source.Play();
        cardLoopSources[cardId] = source;
    }

    /// <summary>
    /// 停止并销毁特定卡牌的循环音效
    /// </summary>
    public void StopCardLoopSound(string cardId)
    {
        if (cardLoopSources.TryGetValue(cardId, out var source))
        {
            source.Stop();
            Destroy(source.gameObject);
            cardLoopSources.Remove(cardId);
            loopGains.Remove(cardId);
        }
    }

    /// <summary>
    /// 设置特定卡牌循环音效的音量
    /// </summary>
    public void SetCardLoopVolume(string cardId, float volume)
    {
        if (cardLoopSources.TryGetValue(cardId, out var source))
        {
            loopGains[cardId] = volume;
            source.volume = volume * GetNormalizedSFXVolume();
        }
    }

    /// <summary>
    /// 获取BGM音量（主音量*BGM音量）
    /// </summary>
    private float GetNormalizedBGMVolume()
    {
        return GameSettings.Current.audio.bgmVolume; // Master volume is applied once by AudioListener.
    }

    /// <summary>
    /// 获取SFX音量（主音量*SFX音量）
    /// </summary>
    private float GetNormalizedSFXVolume()
    {
        return GameSettings.Current.audio.sfxVolume;
    }

    /// <summary>
    /// <summary>
    /// 播放指定地点的环境音乐/环境音。如果传入 null，则使用当前环境或回退到上次地点。
    /// 这是合并后的统一入口，替代原有的 PlayPlaceMusic/PlayCurEnvironmentMusic。
    /// </summary>
    public void PlayEnvironmentMusic(EnvironmentBag nextEnvironmentBag = null)
    {
        //获取当前正在播放的 环境音 的名称，用于判断是否需要中断
        string currentClipName = bgmSource.clip != null ? bgmSource.clip.name : string.Empty;
        // 选择实际要播放的环境音
        EnvironmentBag env = nextEnvironmentBag;
        if (env == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurEnvironmentBag != null)
                env = GameManager.Instance.CurEnvironmentBag;
            else
            {
                // 回退到上次地点
                switch (GameDataManager.Instance.LastPlace)
                {
                    case PlaceEnum.PowerCabin:
                    case PlaceEnum.Cockpit:
                    case PlaceEnum.LifeSupportCabin:
                        PlayBGM("飞船内环境音", true);
                        return;
                    case PlaceEnum.CoralCoast:
                    case PlaceEnum.PhosphorTomb:
                    case PlaceEnum.SpaceshipOuterHull:                        
                        PlayBGM("珊瑚礁海域环境音", true);
                        return;
                    case PlaceEnum.ShallowGrotto:
                    case PlaceEnum.VictimsHall:
                    case PlaceEnum.LastSanctuary:
                        PlayBGM("洞穴环境音", true);
                        return;
                    default:
                        return;
                }
            }
        }

        // 根据目标地点直接播放对应的环境音/BGM
        switch (env.PlaceData.placeType)
        {
            case PlaceEnum.PowerCabin:
            case PlaceEnum.Cockpit:
            case PlaceEnum.LifeSupportCabin:
                const string spaceshipBGM = "飞船内环境音";
                if (currentClipName == spaceshipBGM) return;// 已经在播放，无需切换

                StopBGM();
                PlayBGM("飞船内环境音", true);
                break;
            case PlaceEnum.CoralCoast:
            case PlaceEnum.PhosphorTomb:
            case PlaceEnum.SpaceshipOuterHull:
                const string coralBGM = "珊瑚礁海域环境音";
                if (currentClipName == coralBGM) return;// 已经在播放，无需切换

                StopBGM();
                PlayBGM("珊瑚礁海域环境音", true);
                break;
            case PlaceEnum.ShallowGrotto:
            case PlaceEnum.VictimsHall:
            case PlaceEnum.LastSanctuary:
                const string caveBGM = "洞穴环境音";
                if (currentClipName == caveBGM) return;// 已经在播放，无需切换
                
                StopBGM();
                PlayBGM("洞穴环境音", true);
                break;
            default:
                break;
        }
        return;
    }


}