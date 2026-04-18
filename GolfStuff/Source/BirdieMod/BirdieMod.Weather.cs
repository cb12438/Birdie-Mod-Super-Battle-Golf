using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

internal enum WeatherType : byte
{
    None            = 0,
    RainLight       = 1, RainMedium      = 2, RainHeavy     = 3,
    WindGustsLight  = 4, WindGustsMedium = 5, WindGustsHeavy = 6,
    Thunderstorm    = 7, Tornado         = 8
}

public partial class BirdieMod
{
    // ── Weather fields ────────────────────────────────────────────────────────
    private byte     _currentWeatherType;
    private bool     _weatherPhysicsApplied;
    private float    _originalLinearAirDrag;
    private bool     _originalAirDragCached;
    private float    _originalWindForceScale;
    private bool     _originalWindForceScaleCached;
    private float    _nextLightningTime;
    private float    _nextGustTime;
    private float    _tornadoMoveTimer;
    private Vector3  _tornadoWorldPosition;
    private bool     _tornadoPositionSet;
    private float    _lightningFlashEndTime;
    private bool     _lightningStrikePending;
    private Vector3  _pendingLightningStrikePosition;

    // ── Weather reflection cache ──────────────────────────────────────────────
    private bool         _wAirDragReady;
    private FieldInfo    _wAirDragField;
    private object       _wBallSettings;
    private bool         _wWindReady;
    private object       _wWindSettings;
    private FieldInfo    _wWindScaleField;
    private bool         _wWmReady;
    private Type         _wWmType;
    private PropertyInfo _wWmSpeedProp;
    private PropertyInfo _wWmAngleProp;
    private bool         _gustRunning;
    private bool         _lightningRunning;

    // ─────────────────────────────────────────────────────────────────────────

    internal void WeatherSystemInit()
    {
        _currentWeatherType = 0;
        _weatherPhysicsApplied = false;
        _originalAirDragCached = false;
        _originalWindForceScaleCached = false;
        _lightningStrikePending = false;
        _tornadoPositionSet = false;
        _gustRunning = false;
        _lightningRunning = false;
        BirdieWeatherBridge.EnsureHandlersRegistered();
    }

    internal void WeatherSystemUpdate()
    {
        if (BirdieWeatherBridge.ReceivedNewWeather)
        {
            BirdieWeatherBridge.ReceivedNewWeather = false;
            ApplyWeather(BirdieWeatherBridge.ReceivedWeatherType);
        }

        if (IsNetworkHost() && _currentWeatherType != 0)
        {
            float now = Time.time;

            if (now >= _nextGustTime && !_gustRunning)
            {
                _gustRunning = true;
                BirdieCoroutine.Start(HostGustCoroutine());
            }

            if (_currentWeatherType == (byte)WeatherType.Thunderstorm
                && now >= _nextLightningTime && !_lightningRunning)
            {
                _lightningRunning = true;
                BirdieCoroutine.Start(HostLightningCoroutine());
            }

            if (_currentWeatherType == (byte)WeatherType.Tornado)
            {
                _tornadoMoveTimer -= Time.deltaTime;
                if (_tornadoMoveTimer <= 0f)
                {
                    _tornadoMoveTimer = 5f;
                    Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                    float step = UnityEngine.Random.Range(4f, 12f);
                    _tornadoWorldPosition += new Vector3(dir.x * step, 0f, dir.y * step);
                }
            }
        }

        WeatherVFXUpdate();
    }

    internal void WeatherSystemLateUpdate()
    {
        if (_currentWeatherType != (byte)WeatherType.Tornado
            || !_tornadoPositionSet || playerMovement == null) return;

        float dist = Vector3.Distance(playerMovement.transform.position, _tornadoWorldPosition);
        if (dist >= 8f) return;

        try
        {
            Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
            Vector3 outward = (playerMovement.transform.position - _tornadoWorldPosition).normalized;
            if (rb != null)
                rb.AddForce((Vector3.up * 20f + outward * 12f) * Time.deltaTime * 60f, ForceMode.Force);
            else
                TrySetVelocity(playerMovement, Vector3.up * 8f + outward * 5f);
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Tornado fling: " + ex.Message); }
    }

    internal void ApplyWeather(byte weatherType)
    {
        StopWeatherEffects();
        _currentWeatherType = weatherType;
        if (weatherType == (byte)WeatherType.None) return;

        ApplyWeatherPhysics((WeatherType)weatherType);
        WeatherVFXStart((WeatherType)weatherType);

        if (IsNetworkHost())
        {
            _nextGustTime      = Time.time + UnityEngine.Random.Range(2f, 6f);
            _nextLightningTime = Time.time + UnityEngine.Random.Range(15f, 25f);
            _gustRunning = _lightningRunning = false;
        }

        BirdieLog.Msg("[Birdie] Weather: " + (WeatherType)weatherType);
    }

    private void StopWeatherEffects()
    {
        RestoreWeatherPhysics();
        WeatherVFXStop();
        _gustRunning = _lightningRunning = _lightningStrikePending = _tornadoPositionSet = false;
    }

    // ── Physics ───────────────────────────────────────────────────────────────

    private void ApplyWeatherPhysics(WeatherType t)
    {
        float drag = 1f;
        switch (t)
        {
            case WeatherType.RainLight:                       drag = 1.4f; break;
            case WeatherType.RainMedium:                      drag = 1.8f; break;
            case WeatherType.RainHeavy: case WeatherType.Thunderstorm: drag = 2.5f; break;
            case WeatherType.Tornado:                         drag = 3.0f; break;
        }
        if (drag != 1f) ApplyDragMultiplier(drag);

        float wind = 1f;
        switch (t)
        {
            case WeatherType.RainLight: case WeatherType.RainMedium: case WeatherType.RainHeavy:
                wind = 1.2f; break;
            case WeatherType.WindGustsLight:   wind = 1.5f; break;
            case WeatherType.WindGustsMedium:  wind = 2.5f; break;
            case WeatherType.WindGustsHeavy:   wind = 4.0f; break;
            case WeatherType.Thunderstorm:     wind = 3.0f; break;
            case WeatherType.Tornado:          wind = 5.0f; break;
        }
        ApplyWindScale(wind);
        _weatherPhysicsApplied = true;

        if (t == WeatherType.Tornado && playerMovement != null)
        {
            Vector2 off2D = UnityEngine.Random.insideUnitCircle.normalized * 30f;
            _tornadoWorldPosition = playerMovement.transform.position
                + new Vector3(off2D.x, 0f, off2D.y);
            _tornadoPositionSet = true;
            _tornadoMoveTimer   = 5f;
        }
    }

    private void RestoreWeatherPhysics()
    {
        if (!_weatherPhysicsApplied) return;
        _weatherPhysicsApplied = false;
        if (_originalAirDragCached)        SetDragValue(_originalLinearAirDrag);
        if (_originalWindForceScaleCached) SetWindScaleValue(_originalWindForceScale);
    }

    // ── Air drag reflection ───────────────────────────────────────────────────

    private void EnsureDragReflection()
    {
        if (_wAirDragReady) return;
        _wAirDragReady = true;
        try
        {
            object bs = GetGolfBallSettingsObject();
            if (bs == null) { BirdieLog.Warning("[Birdie] Weather drag: GolfBallSettings null"); return; }
            Type t = bs.GetType();
            _wAirDragField = t.GetField("<LinearAirDragFactor>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (_wAirDragField == null || _wAirDragField.FieldType != typeof(float))
            { BirdieLog.Warning("[Birdie] Weather drag: field not found on " + t.Name); _wAirDragField = null; return; }
            _wBallSettings       = bs;
            _originalLinearAirDrag = (float)_wAirDragField.GetValue(bs);
            _originalAirDragCached = true;
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Weather drag reflection: " + ex.Message); }
    }

    private void ApplyDragMultiplier(float mul)
    {
        EnsureDragReflection();
        if (_originalAirDragCached) SetDragValue(_originalLinearAirDrag * mul);
    }

    private void SetDragValue(float v)
    {
        if (_wAirDragField == null || _wBallSettings == null) return;
        try { _wAirDragField.SetValue(_wBallSettings, v); }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Weather SetDrag: " + ex.Message); }
    }

    // ── Wind scale reflection ─────────────────────────────────────────────────

    private void EnsureWindReflection()
    {
        if (_wWindReady) return;
        _wWindReady = true;
        try
        {
            Type wmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            { wmType = asm.GetType("WindManager"); if (wmType != null) break; }
            if (wmType == null) { BirdieLog.Warning("[Birdie] Weather wind: WindManager not found"); return; }

            UnityEngine.Object wmObj = UnityEngine.Object.FindObjectOfType(wmType);
            if (wmObj == null) { BirdieLog.Warning("[Birdie] Weather wind: WindManager not in scene"); return; }

            FieldInfo wsF = wmType.GetField("windSettings",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (wsF == null) { BirdieLog.Warning("[Birdie] Weather wind: windSettings field missing"); return; }

            object ws = wsF.GetValue(wmObj);
            if (ws == null) { BirdieLog.Warning("[Birdie] Weather wind: windSettings null"); return; }

            FieldInfo fsF = ws.GetType().GetField("forceScale",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fsF == null || fsF.FieldType != typeof(float))
            { BirdieLog.Warning("[Birdie] Weather wind: forceScale not found"); return; }

            _wWindSettings          = ws;
            _wWindScaleField        = fsF;
            _originalWindForceScale = (float)fsF.GetValue(ws);
            _originalWindForceScaleCached = true;
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Weather wind reflection: " + ex.Message); }
    }

    private void ApplyWindScale(float scale) { EnsureWindReflection(); SetWindScaleValue(scale); }

    private void SetWindScaleValue(float v)
    {
        if (_wWindScaleField == null || _wWindSettings == null) return;
        try { _wWindScaleField.SetValue(_wWindSettings, v); }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Weather SetWindScale: " + ex.Message); }
    }

    // ── WindManager SyncVar reflection ────────────────────────────────────────

    private void EnsureWmSyncReflection()
    {
        if (_wWmReady) return;
        _wWmReady = true;
        try
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            { _wWmType = asm.GetType("WindManager"); if (_wWmType != null) break; }
            if (_wWmType == null) { BirdieLog.Warning("[Birdie] Weather WM sync: type not found"); return; }
            _wWmSpeedProp = _wWmType.GetProperty("NetworkcurrentWindSpeed",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _wWmAngleProp = _wWmType.GetProperty("NetworkcurrentWindAngle",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (_wWmSpeedProp == null) BirdieLog.Warning("[Birdie] Weather WM: NetworkcurrentWindSpeed missing");
            if (_wWmAngleProp == null) BirdieLog.Warning("[Birdie] Weather WM: NetworkcurrentWindAngle missing");
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Weather WM sync reflection: " + ex.Message); }
    }

    // ── Host coroutines ───────────────────────────────────────────────────────

    private IEnumerator HostGustCoroutine()
    {
        EnsureWmSyncReflection();
        while (_currentWeatherType != (byte)WeatherType.None)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(4f, 12f));
            if (_currentWeatherType == (byte)WeatherType.None) break;

            float baseI = GustIntensity((WeatherType)_currentWeatherType);
            if (baseI <= 0f) break;

            float speed = baseI * UnityEngine.Random.Range(0.5f, 1.0f);
            float angle = UnityEngine.Random.Range(0f, 360f);
            try
            {
                if (_wWmType != null && _wWmSpeedProp != null && _wWmAngleProp != null)
                {
                    UnityEngine.Object wm = UnityEngine.Object.FindObjectOfType(_wWmType);
                    if (wm != null)
                    {
                        _wWmSpeedProp.SetValue(wm, speed, null);
                        _wWmAngleProp.SetValue(wm, angle, null);
                    }
                }
            }
            catch (Exception ex) { BirdieLog.Warning("[Birdie] Gust set: " + ex.Message); }
        }
        _gustRunning = false;
    }

    private IEnumerator HostLightningCoroutine()
    {
        while (_currentWeatherType == (byte)WeatherType.Thunderstorm)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(25f, 35f));
            if (_currentWeatherType != (byte)WeatherType.Thunderstorm) break;
            try
            {
                Type pmType = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                { pmType = asm.GetType("PlayerMovement"); if (pmType != null) break; }
                if (pmType == null) continue;

                UnityEngine.Object[] players = UnityEngine.Object.FindObjectsOfType(pmType);
                if (players == null || players.Length == 0) continue;

                Component target = players[UnityEngine.Random.Range(0, players.Length)] as Component;
                if (target == null) continue;

                _pendingLightningStrikePosition = target.transform.position;
                _lightningStrikePending         = true;

                Rigidbody rb = target.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddForce(Vector3.up * 25f + UnityEngine.Random.insideUnitSphere * 15f,
                        ForceMode.Impulse);
                else
                    TrySetVelocity(target, Vector3.up * 15f + UnityEngine.Random.insideUnitSphere * 8f);
            }
            catch (Exception ex) { BirdieLog.Warning("[Birdie] Lightning strike: " + ex.Message); }
        }
        _lightningRunning = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private float GustIntensity(WeatherType t)
    {
        switch (t)
        {
            case WeatherType.WindGustsLight:   return 3f;
            case WeatherType.WindGustsMedium:  return 7f;
            case WeatherType.WindGustsHeavy:   return 14f;
            case WeatherType.Thunderstorm:     return 10f;
            case WeatherType.Tornado:          return 18f;
            case WeatherType.RainLight:
            case WeatherType.RainMedium:
            case WeatherType.RainHeavy:        return 3f;
            default:                           return 0f;
        }
    }

    private void TrySetVelocity(Component target, Vector3 vel)
    {
        if (target == null) return;
        try
        {
            Type t = target.GetType();
            PropertyInfo pp = t.GetProperty("velocity",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (pp != null && pp.CanWrite && pp.PropertyType == typeof(Vector3))
            { pp.SetValue(target, vel, null); return; }

            FieldInfo ff = t.GetField("velocity",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (ff != null && ff.FieldType == typeof(Vector3)) ff.SetValue(target, vel);
        }
        catch { }
    }

}
