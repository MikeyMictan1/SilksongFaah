using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

[BepInPlugin("com.mikey.silksongfaah", "Silksong Faah", "1.0.0")]
public class SilksongFaah : BaseUnityPlugin
{
    internal static ManualLogSource _log;
    internal static AudioClip _faahClip;
    internal static AudioSource _audioSource;

    private readonly Harmony _harmony = new Harmony("com.mikey.silksongfaah");

    private void Awake()
    {
        _log = Logger;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
        _audioSource.playOnAwake = false;
        DontDestroyOnLoad(gameObject);

        _harmony.PatchAll();
        _log.LogInfo("[Silksong Faah] Plugin loaded and initialised");

        StartCoroutine(LoadFaahCoroutine());
    }

    private IEnumerator LoadFaahCoroutine()
    {
        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string fileName = "faah.wav";
        string fullPath = Path.Combine(pluginDir, fileName);

        if (!File.Exists(fullPath))
        {
            _log.LogWarning($"[Silksong Faah] {fileName} not found in {pluginDir}. Place your faah.wav there.");
            yield break;
        }

        string uri = "file:///" + fullPath.Replace("\\", "/");

        using (var uwr = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
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
                _log.LogError($"[Silksong Faah] Exception processing faah.wav: {ex}");
            }
        }
    }

    internal static void PlayFaah()
    {
        try
        {
            if (_faahClip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(_faahClip, 1f);
            _log?.LogDebug("[Silksong Faah] Played faah");
        }
        catch (Exception ex)
        {
            _log?.LogError($"[Silksong Faah] Error playing faah: {ex}");
        }
    }

    /* Debug: press F7 to test the faah sound
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
                _log.LogWarning("[Silksong Faah] F7 pressed but faah.wav not loaded yet.");
            }
        }
    }
   */
}


// Patch class lives outside SilksongFaah — standard Harmony convention
[HarmonyPatch(typeof(GameManager))]
internal class GameManagerPatch
{
    [HarmonyPatch("PlayerDead")]
    [HarmonyPostfix]
    static void PlayerDeadPatch(ref GameManager __instance)
    {
        SilksongFaah.PlayFaah();
    }
}