using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using System.Reflection;

[BepInPlugin("com.mikey.silksongfaah", "Silksong Faah", "1.0.0")]
public class SilksongFaah : BaseUnityPlugin
{
    private static ManualLogSource _log;
    private static AudioClip _faahClip;
    private static AudioSource _audioSource;
    private static double _lastFaahTime = -999;
    private const double FaahCooldown = 0.5;

    private void Awake()
    {
        _log = Logger;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
        _audioSource.playOnAwake = false;
        DontDestroyOnLoad(gameObject);

        var harmony = new Harmony("com.mikey.silksongfaah");

        // Only patch SendHeroDamagedEvent — it's the most specific "hero took damage" hook
        // The other two (HeroController.TakeDamage, HeroBox.TakeDamageFromDamager) also fire
        // for nail swings and other non-damage events, causing false triggers
        TryPatch(harmony, "DamageHero", "SendHeroDamagedEvent");

        var patched = harmony.GetPatchedMethods().ToList();
        _log.LogInfo($"[Silksong Faah] Total patched methods: {patched.Count}");
        foreach (var m in patched)
            _log.LogInfo($"[Silksong Faah] Patched: {m.DeclaringType?.Name}.{m.Name}");

        _log.LogInfo("[Silksong Faah] Plugin loaded and initialised");
        StartCoroutine(LoadFaahCoroutine());
    }

    private IEnumerator LoadFaahCoroutine()
    {
        string pluginDir = Path.Combine(Paths.PluginPath, "SilksongFaah");
        string fileName = "faah.mp3";
        string fullPath = Path.Combine(pluginDir, fileName);

        if (!File.Exists(fullPath))
        {
            _log.LogWarning($"[Silksong Faah] {fileName} not found in {pluginDir}. Place your faah.mp3 there.");
            yield break;
        }

        string uri = "file:///" + fullPath.Replace("\\", "/");

        using (var uwr = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
        {
            yield return uwr.SendWebRequest();

            try
            {
                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    _log.LogError($"[Silksong Faah] Failed to load {fileName}: {uwr.error}");
                    yield break;
                }

                _faahClip = DownloadHandlerAudioClip.GetContent(uwr);

                if (_faahClip == null)
                {
                    _log.LogError("[Silksong Faah] Loaded file but could not create AudioClip.");
                    yield break;
                }

                _faahClip.name = Path.GetFileNameWithoutExtension(fullPath);
                _log.LogInfo($"[Silksong Faah] Successfully loaded {fileName}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[Silksong Faah] Exception processing faah.mp3: {ex}");
            }
        }
    }

    private static void PlayFaah()
    {
        try
        {
            if (_faahClip == null || _audioSource == null) return;

            // Cooldown guard — even if somehow called multiple times for one hit, only plays once
            double now = Time.timeAsDouble;
            if (now - _lastFaahTime < FaahCooldown) return;
            _lastFaahTime = now;

            _audioSource.PlayOneShot(_faahClip, 1f);
            _log?.LogDebug("[Silksong Faah] Played faah");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Silksong Faah] Error playing faah: {ex}");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7))
        {
            if (_faahClip != null)
            {
                PlayFaah();
                _log.LogInfo("[Silksong Faah] F7 played faah (debug).");
            }
            else
            {
                _log.LogWarning("[Silksong Faah] F7 pressed but faah.mp3 not loaded yet.");
            }
        }
    }

    private void TryPatch(Harmony harmony, string typeName, string methodName)
    {
        try
        {
            // Search all loaded assemblies for the type at runtime
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .FirstOrDefault(t => t.Name == typeName);

            if (type == null)
            {
                _log.LogWarning($"[Silksong Faah] Could not find type: {typeName}");
                return;
            }

            var method = type.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (method == null)
            {
                _log.LogWarning($"[Silksong Faah] Could not find method: {typeName}.{methodName}");
                return;
            }

            var postfix = new HarmonyMethod(typeof(SilksongFaah)
                .GetMethod(nameof(UniversalDamagePostfix),
                    BindingFlags.Static | BindingFlags.NonPublic));

            harmony.Patch(method, postfix: postfix);
            _log.LogInfo($"[Silksong Faah] Successfully patched {typeName}.{methodName}");
        }
        catch (Exception ex)
        {
            _log.LogError($"[Silksong Faah] Failed to patch {typeName}.{methodName}: {ex}");
        }
    }

    private static void UniversalDamagePostfix()
    {
        PlayFaah();
    }
}