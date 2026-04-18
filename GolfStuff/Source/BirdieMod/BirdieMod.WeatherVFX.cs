using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

public partial class BirdieMod
{
    // ── Tracked materials (destroyed on weather stop to prevent leaks) ────────
    private readonly List<Material> _weatherMaterials = new List<Material>();
    private Material TrackMat(Material m) { _weatherMaterials.Add(m); return m; }

    // ── VFX object references ─────────────────────────────────────────────────
    private GameObject     _weatherVfxRoot;
    private ParticleSystem _rainPs;
    private ParticleSystem _tornadoPs;
    private Light          _lightningLight;
    private AudioSource    _weatherAudio;

    // ── Lightning screen flash ────────────────────────────────────────────────
    private bool  _lightningScreenFlash;
    private float _lightningScreenFlashEnd;
    private bool  _playerStruckFlash;
    private float _playerStruckFlashEnd;

    // ── Audio ─────────────────────────────────────────────────────────────────
    private AudioSource _weatherAudio2;
    private AudioClip   _clipRainLoop;
    private AudioClip   _clipWindLoop;
    private AudioClip   _clipWindGust;
    private AudioClip   _clipThunderCrack;
    private AudioClip   _clipTornadoLoop;
    private bool        _soundsLoaded;

    // ─────────────────────────────────────────────────────────────────────────
    // Start / Stop
    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherVFXStart(WeatherType type)
    {
        WeatherVFXStop();

        _weatherVfxRoot = new GameObject("BirdieWeatherVFX_Root");

        _weatherAudio              = _weatherVfxRoot.AddComponent<AudioSource>();
        _weatherAudio.loop         = true;
        _weatherAudio.spatialBlend = 0f;
        _weatherAudio.priority     = 0;

        switch (type)
        {
            case WeatherType.RainLight:
                _rainPs = CreateRainParticles(0.25f, 12f, 0.03f);
                ParentToRoot(_rainPs.gameObject);
                PlayLoop(_weatherAudio, _clipRainLoop, 0.85f);
                break;

            case WeatherType.RainMedium:
                _rainPs = CreateRainParticles(0.55f, 16f, 0.045f);
                ParentToRoot(_rainPs.gameObject);
                PlayLoop(_weatherAudio, _clipRainLoop, 0.95f);
                break;

            case WeatherType.RainHeavy:
                _rainPs = CreateRainParticles(1.0f, 22f, 0.06f);
                ParentToRoot(_rainPs.gameObject);
                PlayLoop(_weatherAudio, _clipRainLoop, 1.0f);
                break;

            case WeatherType.Thunderstorm:
                _rainPs = CreateRainParticles(1.0f, 22f, 0.06f);
                ParentToRoot(_rainPs.gameObject);
                PlayLoop(_weatherAudio, _clipRainLoop, 1.0f);
                _weatherAudio2 = _weatherVfxRoot.AddComponent<AudioSource>();
                _weatherAudio2.loop = true; _weatherAudio2.spatialBlend = 0f; _weatherAudio2.priority = 0;
                PlayLoop(_weatherAudio2, _clipWindLoop, 0.95f);
                break;

            case WeatherType.Tornado:
                _rainPs = CreateRainParticles(0.4f, 18f, 0.05f);
                ParentToRoot(_rainPs.gameObject);
                Vector3 spawnPos = _tornadoPositionSet ? _tornadoWorldPosition
                    : (Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 30f : Vector3.zero);
                _tornadoPs = CreateTornadoParticles(spawnPos);
                ParentToRoot(_tornadoPs.gameObject);
                PlayLoop(_weatherAudio, _clipTornadoLoop ?? _clipWindLoop, 1.0f);
                _weatherAudio2 = _weatherVfxRoot.AddComponent<AudioSource>();
                _weatherAudio2.loop = true; _weatherAudio2.spatialBlend = 0f; _weatherAudio2.priority = 0;
                PlayLoop(_weatherAudio2, _clipWindLoop, 0.95f);
                break;

            case WeatherType.WindGustsLight:
                PlayLoop(_weatherAudio, _clipWindLoop, 0.85f);
                break;
            case WeatherType.WindGustsMedium:
                PlayLoop(_weatherAudio, _clipWindLoop, 0.95f);
                break;
            case WeatherType.WindGustsHeavy:
                PlayLoop(_weatherAudio, _clipWindLoop, 1.0f);
                break;

            case WeatherType.None:
            default:
                break;
        }
    }

    internal void WeatherVFXStop()
    {
        if (_weatherVfxRoot != null)
        {
            UnityEngine.Object.Destroy(_weatherVfxRoot);
            _weatherVfxRoot = null;
        }

        if (_lightningLight != null && _lightningLight.gameObject != null)
            UnityEngine.Object.Destroy(_lightningLight.gameObject);

        foreach (Material mat in _weatherMaterials)
            if (mat != null) UnityEngine.Object.Destroy(mat);
        _weatherMaterials.Clear();

        _rainPs               = null;
        _tornadoPs            = null;
        _lightningLight       = null;
        _weatherAudio         = null;
        _weatherAudio2        = null;
        _lightningScreenFlash = false;
        _playerStruckFlash    = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-frame update
    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherVFXUpdate()
    {
        float now = Time.time;

        // Move rain emitter with camera every frame (same pattern as WindManager.UpdateVfxPosition)
        if (_rainPs != null && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            _rainPs.transform.position = cam.position + Vector3.up * 14f + cam.forward * 12f;
            _rainPs.transform.rotation = Quaternion.Euler(12f, cam.eulerAngles.y, 0f);
        }

        if (_tornadoPs != null && _tornadoPositionSet)
            _tornadoPs.transform.position = _tornadoWorldPosition;

        if (_lightningStrikePending)
        {
            _lightningStrikePending = false;
            DoLightningFlash(_pendingLightningStrikePosition);
            PlayThunderSound();
        }

        if (_lightningLight != null)
        {
            if (now < _lightningFlashEndTime)
            {
                float t = 1f - ((_lightningFlashEndTime - now) / 0.15f);
                _lightningLight.intensity = Mathf.Lerp(8f, 0f, t);
            }
            else
            {
                _lightningLight.intensity = 0f;
                UnityEngine.Object.Destroy(_lightningLight.gameObject);
                _lightningLight = null;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OnGUI
    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherOnGUI()
    {
        if (_lightningScreenFlash && Time.time < _lightningScreenFlashEnd)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        else
        {
            _lightningScreenFlash = false;
        }

        if (_playerStruckFlash && Time.time < _playerStruckFlashEnd)
        {
            GUI.color = new Color(1f, 0.08f, 0.08f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        else
        {
            _playerStruckFlash = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rain particle factory — camera-anchored, wide coverage
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem CreateRainParticles(float density, float speed, float size)
    {
        GameObject go = new GameObject("BirdieRain");

        // Position is updated every frame in WeatherVFXUpdate to follow the camera
        go.transform.position = Camera.main != null
            ? Camera.main.transform.position + Vector3.up * 14f + Camera.main.transform.forward * 12f
            : Vector3.up * 14f;
        go.transform.rotation = Quaternion.Euler(12f, Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
        main.startSpeed      = speed;
        main.startSize       = size;
        main.startColor      = new Color(0.75f, 0.85f, 1f, 0.55f);
        main.maxParticles    = (int)(density * 6000f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(100f, 1f, 100f);

        var emission = ps.emission;
        emission.rateOverTime = density * 1500f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.y       = new ParticleSystem.MinMaxCurve(-speed * 1.2f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode    = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale   = 2.5f;
        renderer.material      = TrackMat(new Material(
            Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default")));

        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tornado particle factory — multi-layer funnel + ring + debris
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem CreateTornadoParticles(Vector3 worldPosition)
    {
        GameObject root = new GameObject("BirdieTornado");
        root.transform.position = worldPosition;

        // ── Layer 1: funnel (very thin white ribbon streaks) ──────────────────
        // startSize is the cross-section width in Stretch mode — keep it tiny
        ParticleSystem ps = root.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.5f, 5.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(10f, 24f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.022f, 0.048f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 0.92f),
            new Color(0.80f, 0.82f, 0.88f, 0.48f));
        main.maxParticles    = 2500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 3f;
        shape.radius    = 0.12f;

        var emission = ps.emission;
        emission.rateOverTime = 280f;

        var vel = ps.velocityOverLifetime;
        vel.enabled  = true;
        vel.space    = ParticleSystemSimulationSpace.World;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(24f);
        vel.radial   = new ParticleSystem.MinMaxCurve(-0.55f);
        vel.y        = new ParticleSystem.MinMaxCurve(4f, 12f);

        var rend = root.GetComponent<ParticleSystemRenderer>();
        rend.renderMode    = ParticleSystemRenderMode.Stretch;
        rend.velocityScale = 0.20f;
        rend.lengthScale   = 12f;
        rend.material      = TrackMat(new Material(
            Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default")));
        ps.Play();

        // ── Layer 2: ground dust swirl (Billboard, small) ─────────────────────
        GameObject ringGo = new GameObject("TornadoRing");
        ringGo.transform.SetParent(root.transform, false);
        ringGo.transform.localPosition = Vector3.zero;

        ParticleSystem ringPs = ringGo.AddComponent<ParticleSystem>();
        ringPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var rm = ringPs.main;
        rm.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
        rm.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        rm.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        rm.startColor      = new Color(0.55f, 0.48f, 0.38f, 0.60f);
        rm.maxParticles    = 350;
        rm.simulationSpace = ParticleSystemSimulationSpace.World;

        var rs = ringPs.shape;
        rs.enabled   = true;
        rs.shapeType = ParticleSystemShapeType.Circle;
        rs.radius    = 3.5f;

        var re = ringPs.emission;
        re.rateOverTime = 65f;

        var rv = ringPs.velocityOverLifetime;
        rv.enabled  = true;
        rv.space    = ParticleSystemSimulationSpace.World;
        rv.orbitalY = new ParticleSystem.MinMaxCurve(12f);
        rv.radial   = new ParticleSystem.MinMaxCurve(0.5f);
        rv.y        = new ParticleSystem.MinMaxCurve(0.2f, 1.5f);

        var ringRend = ringGo.GetComponent<ParticleSystemRenderer>();
        ringRend.renderMode = ParticleSystemRenderMode.Billboard;
        ringRend.material   = TrackMat(new Material(
            Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default")));
        ringPs.Play();

        // ── Layer 3: debris (small chunks, Billboard) ─────────────────────────
        GameObject debrisGo = new GameObject("TornadoDebris");
        debrisGo.transform.SetParent(root.transform, false);
        debrisGo.transform.localPosition = Vector3.zero;

        ParticleSystem debrisPs = debrisGo.AddComponent<ParticleSystem>();
        debrisPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var dm = debrisPs.main;
        dm.startLifetime   = new ParticleSystem.MinMaxCurve(2f, 5f);
        dm.startSpeed      = new ParticleSystem.MinMaxCurve(4f, 10f);
        dm.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        dm.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.18f, 0.42f, 0.08f, 0.88f),
            new Color(0.38f, 0.24f, 0.06f, 0.88f));
        dm.maxParticles    = 130;
        dm.simulationSpace = ParticleSystemSimulationSpace.World;

        var ds = debrisPs.shape;
        ds.enabled   = true;
        ds.shapeType = ParticleSystemShapeType.Cone;
        ds.angle     = 25f;
        ds.radius    = 2.5f;

        var de = debrisPs.emission;
        de.rateOverTime = 18f;

        var dv = debrisPs.velocityOverLifetime;
        dv.enabled  = true;
        dv.space    = ParticleSystemSimulationSpace.World;
        dv.orbitalY = new ParticleSystem.MinMaxCurve(10f);
        dv.radial   = new ParticleSystem.MinMaxCurve(-0.35f);
        dv.y        = new ParticleSystem.MinMaxCurve(1.0f, 5.0f);

        var debrisRend = debrisGo.GetComponent<ParticleSystemRenderer>();
        debrisRend.renderMode = ParticleSystemRenderMode.Billboard;
        debrisRend.material   = TrackMat(new Material(
            Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default")));
        debrisPs.Play();

        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lightning flash
    // ─────────────────────────────────────────────────────────────────────────

    private void DoLightningFlash(Vector3 strikePosition)
    {
        if (_lightningLight != null && _lightningLight.gameObject != null)
        {
            UnityEngine.Object.Destroy(_lightningLight.gameObject);
            _lightningLight = null;
        }

        GameObject lightGo = new GameObject("BirdieLightningLight");
        lightGo.transform.position = strikePosition + Vector3.up * 2f;

        _lightningLight           = lightGo.AddComponent<Light>();
        _lightningLight.type      = LightType.Point;
        _lightningLight.color     = new Color(0.9f, 0.95f, 1f);
        _lightningLight.intensity = 10f;
        _lightningLight.range     = 60f;
        _lightningFlashEndTime    = Time.time + 0.18f;

        _lightningScreenFlash    = true;
        _lightningScreenFlashEnd = Time.time + 0.1f;

        // Red struck flash if local player was the target
        if (playerMovement != null)
        {
            float dist = Vector3.Distance(playerMovement.transform.position, strikePosition);
            if (dist < 5f)
            {
                _playerStruckFlash    = true;
                _playerStruckFlashEnd = Time.time + 0.45f;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ParentToRoot(GameObject child)
    {
        if (child == null || _weatherVfxRoot == null) return;
        child.transform.SetParent(_weatherVfxRoot.transform, true);
    }

    private static void PlayLoop(AudioSource src, AudioClip clip, float volume)
    {
        if (src == null) return;
        src.clip   = clip;
        src.volume = volume;
        if (clip != null) src.Play();
    }

    internal void PlayGustSound()
    {
        if (_weatherAudio != null && _clipWindGust != null)
            _weatherAudio.PlayOneShot(_clipWindGust, 4.0f);
    }

    internal void PlayThunderSound()
    {
        if (_weatherAudio != null && _clipThunderCrack != null)
            _weatherAudio.PlayOneShot(_clipThunderCrack, 4.0f);
    }

    // ── Sound loader ──────────────────────────────────────────────────────────

    internal IEnumerator LoadWeatherSounds()
    {
        string dllDir    = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        string soundsDir = Path.Combine(dllDir, "BirdieMod", "sounds");

        yield return LoadClip(soundsDir, "rain_loop.wav",     AudioType.WAV,  c => _clipRainLoop     = c);
        yield return LoadClip(soundsDir, "wind_loop.wav",     AudioType.WAV,  c => _clipWindLoop     = c);
        yield return LoadClip(soundsDir, "wind_gust.mp3",     AudioType.MPEG, c => _clipWindGust     = c);
        yield return LoadClip(soundsDir, "thunder_crack.mp3", AudioType.MPEG, c => _clipThunderCrack = c);
        yield return LoadClip(soundsDir, "tornado_loop.mp3",  AudioType.MPEG, c => _clipTornadoLoop  = c);
        _soundsLoaded = true;
        BirdieLog.Msg("[Birdie] Weather sounds loaded.");
    }

    private IEnumerator LoadClip(string dir, string file, AudioType type, System.Action<AudioClip> setter)
    {
        string path = Path.Combine(dir, file);
        if (!File.Exists(path)) { BirdieLog.Warning("[Birdie] Sound not found: " + path); yield break; }
        string uri = "file:///" + path.Replace('\\', '/');
        using (var req = UnityWebRequestMultimedia.GetAudioClip(uri, type))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                setter(DownloadHandlerAudioClip.GetContent(req));
            else
                BirdieLog.Warning("[Birdie] Failed to load sound: " + file + " — " + req.error);
        }
    }
}
