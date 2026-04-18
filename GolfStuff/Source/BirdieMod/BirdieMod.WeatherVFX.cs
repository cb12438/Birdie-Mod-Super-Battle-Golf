using System;
using UnityEngine;

public partial class BirdieMod
{
    // ── VFX object references ─────────────────────────────────────────────────
    private GameObject     _weatherVfxRoot;
    private ParticleSystem _rainPs;
    private ParticleSystem _tornadoPs;
    private Light          _lightningLight;
    private AudioSource    _weatherAudio;

    // ── Lightning screen flash ────────────────────────────────────────────────
    private bool  _lightningScreenFlash;
    private float _lightningScreenFlashEnd;

    // ─────────────────────────────────────────────────────────────────────────
    // Start / Stop
    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherVFXStart(WeatherType type)
    {
        WeatherVFXStop();

        _weatherVfxRoot = new GameObject("BirdieWeatherVFX_Root");

        // Audio source infrastructure (no clip assigned — user may assign via Resources)
        // Attach an AudioClip named "rain_loop" etc. via Resources or leave silent
        _weatherAudio = _weatherVfxRoot.AddComponent<AudioSource>();
        _weatherAudio.loop   = true;
        _weatherAudio.volume = 0.4f;
        _weatherAudio.clip   = null; // no bundled audio assets

        switch (type)
        {
            case WeatherType.RainLight:
                _rainPs = CreateRainParticles(0.25f, 12f, 0.03f);
                ParentToRoot(_rainPs.gameObject);
                break;

            case WeatherType.RainMedium:
                _rainPs = CreateRainParticles(0.55f, 16f, 0.045f);
                ParentToRoot(_rainPs.gameObject);
                break;

            case WeatherType.RainHeavy:
                _rainPs = CreateRainParticles(1.0f, 22f, 0.06f);
                ParentToRoot(_rainPs.gameObject);
                break;

            case WeatherType.Thunderstorm:
                _rainPs = CreateRainParticles(1.0f, 22f, 0.06f);
                ParentToRoot(_rainPs.gameObject);
                // Lightning light infrastructure created on demand in DoLightningFlash
                break;

            case WeatherType.Tornado:
                _rainPs = CreateRainParticles(0.4f, 18f, 0.05f);
                ParentToRoot(_rainPs.gameObject);
                if (_tornadoPositionSet)
                {
                    _tornadoPs = CreateTornadoParticles(_tornadoWorldPosition);
                }
                else
                {
                    Vector3 fallbackPos = Camera.main != null
                        ? Camera.main.transform.position + Camera.main.transform.forward * 30f
                        : Vector3.zero;
                    _tornadoPs = CreateTornadoParticles(fallbackPos);
                }
                ParentToRoot(_tornadoPs.gameObject);
                break;

            case WeatherType.WindGustsLight:
            case WeatherType.WindGustsMedium:
            case WeatherType.WindGustsHeavy:
                // Wind is felt, not seen — no VFX spawned
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
        {
            UnityEngine.Object.Destroy(_lightningLight.gameObject);
        }

        _rainPs             = null;
        _tornadoPs          = null;
        _lightningLight     = null;
        _weatherAudio       = null;
        _lightningScreenFlash = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-frame update
    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherVFXUpdate()
    {
        float now = Time.time;

        // Move tornado particle system to the current world position
        if (_tornadoPs != null && _tornadoPositionSet)
        {
            _tornadoPs.transform.position = _tornadoWorldPosition;
        }

        // Trigger lightning flash if host signalled a strike
        if (_lightningStrikePending)
        {
            _lightningStrikePending = false;
            DoLightningFlash(_pendingLightningStrikePosition);
        }

        // Fade lightning light intensity to zero
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
    // OnGUI (called from BirdieOnGUI during thunderstorm)
    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherOnGUI()
    {
        if (_currentWeatherType != (byte)WeatherType.Thunderstorm)
        {
            return;
        }

        if (_lightningScreenFlash && Time.time < _lightningScreenFlashEnd)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(
                new Rect(0, 0, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        else
        {
            _lightningScreenFlash = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rain particle system factory
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem CreateRainParticles(float density, float speed, float size)
    {
        GameObject go = new GameObject("BirdieRain");

        Transform anchor = Camera.main != null ? Camera.main.transform : null;
        if (anchor != null)
        {
            go.transform.SetParent(anchor);
        }

        go.transform.localPosition = new Vector3(0f, 8f, 6f);
        go.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime  = 0.6f;
        main.startSpeed     = speed;
        main.startSize      = size;
        main.startColor     = new Color(0.75f, 0.85f, 1f, 0.55f);
        main.maxParticles   = (int)(density * 3000f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(40f, 1f, 40f);

        var emission = ps.emission;
        emission.rateOverTime = density * 800f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.y       = new ParticleSystem.MinMaxCurve(-speed * 1.2f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode    = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale   = 2.5f;
        renderer.material      = new Material(
            Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"));

        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tornado particle system factory
    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem CreateTornadoParticles(Vector3 worldPosition)
    {
        GameObject go = new GameObject("BirdieTornado");
        go.transform.position = worldPosition;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2f, 8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startColor      = new Color(0.55f, 0.48f, 0.38f, 0.6f);
        main.maxParticles    = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 15f;
        shape.radius    = 2f;

        var emission = ps.emission;
        emission.rateOverTime = 120f;

        var vel = ps.velocityOverLifetime;
        vel.enabled   = true;
        vel.orbitalY  = new ParticleSystem.MinMaxCurve(8f);
        vel.radial    = new ParticleSystem.MinMaxCurve(-0.3f);
        vel.y         = new ParticleSystem.MinMaxCurve(1f, 4f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(
            Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"));

        return ps;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lightning flash
    // ─────────────────────────────────────────────────────────────────────────

    private void DoLightningFlash(Vector3 strikePosition)
    {
        // Destroy any previous lightning light
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
        _lightningLight.intensity = 8f;
        _lightningLight.range     = 40f;
        _lightningFlashEndTime    = Time.time + 0.15f;

        // Screen flash
        _lightningScreenFlash    = true;
        _lightningScreenFlashEnd = Time.time + 0.08f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal helper
    // ─────────────────────────────────────────────────────────────────────────

    private void ParentToRoot(GameObject child)
    {
        if (child == null || _weatherVfxRoot == null)
        {
            return;
        }

        // Keep world position — only reparent for organisational grouping
        child.transform.SetParent(_weatherVfxRoot.transform, true);
    }
}
