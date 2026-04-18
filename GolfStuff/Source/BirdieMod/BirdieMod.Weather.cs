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

    // ── Auto weather ──────────────────────────────────────────────────────────
    internal bool   autoWeatherEnabled;
    internal int    autoWeatherChance  = 40;   // 0-100 %
    // per-type enabled flags (index 0 = RainLight … 7 = Tornado)
    internal int[]  autoWeatherChances = { 50, 35, 20, 40, 25, 10, 15, 5 };
    private  bool   _holeEventSubscribed;
    private  float  _tornadoFlingNextTime;

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
        _wAirDragReady = false;  // reset so drag reflection retries on next weather start
        BirdieWeatherBridge.EnsureHandlersRegistered();
        BirdieCoroutine.Start(LoadWeatherSounds());
        SubscribeHoleChangeEvent();
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
        if (dist >= 12f) return;
        if (Time.time < _tornadoFlingNextTime) return;

        _tornadoFlingNextTime = Time.time + 1.2f;
        try
        {
            Vector3 outward = (playerMovement.transform.position - _tornadoWorldPosition).normalized;
            Vector3 fling   = Vector3.up * 18f + outward * 14f;
            FlingPlayer(playerMovement, fling);
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
            case WeatherType.RainLight:        wind = 1.5f; break;
            case WeatherType.RainMedium:       wind = 2.2f; break;
            case WeatherType.RainHeavy:        wind = 3.0f; break;
            case WeatherType.WindGustsLight:   wind = 2.5f; break;
            case WeatherType.WindGustsMedium:  wind = 5.0f; break;
            case WeatherType.WindGustsHeavy:   wind = 9.0f; break;
            case WeatherType.Thunderstorm:     wind = 4.0f; break;
            case WeatherType.Tornado:          wind = 11.0f; break;
        }
        ApplyWindScale(wind);

        // Set actual WindManager speed so the ball is affected in flight
        if (IsNetworkHost()) SetBaseWind(t);

        _weatherPhysicsApplied = true;

        if (t == WeatherType.Tornado && playerMovement != null)
        {
            float spawnDist = UnityEngine.Random.Range(15f, 22f);
            Vector2 off2D = UnityEngine.Random.insideUnitCircle.normalized * spawnDist;
            _tornadoWorldPosition = playerMovement.transform.position
                + new Vector3(off2D.x, 0f, off2D.y);
            _tornadoPositionSet   = true;
            _tornadoMoveTimer     = 5f;
            _tornadoFlingNextTime = 0f;
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
        try
        {
            object bs = GetGolfBallSettingsObject();
            if (bs == null) return;  // don't flag ready — will retry next call
            Type t = bs.GetType();
            _wAirDragField = t.GetField("<LinearAirDragFactor>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (_wAirDragField == null || _wAirDragField.FieldType != typeof(float))
            { BirdieLog.Warning("[Birdie] Weather drag: field not found on " + t.Name); _wAirDragField = null; return; }
            _wBallSettings         = bs;
            _originalLinearAirDrag = (float)_wAirDragField.GetValue(bs);
            _originalAirDragCached = true;
            _wAirDragReady         = true;  // only set on full success
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
            yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 13f));
            if (_currentWeatherType == (byte)WeatherType.None) break;

            float baseI = GustIntensity((WeatherType)_currentWeatherType);
            if (baseI <= 0f) break;

            // Burst of 1-3 rapid direction+speed changes
            int bursts = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < bursts; i++)
            {
                // SyncVars are int — cast explicitly so the hook fires and WindUpdated event triggers
                int gustSpeed = (int)(baseI * UnityEngine.Random.Range(0.7f, 1.0f));
                int gustAngle = UnityEngine.Random.Range(0, 360);
                try
                {
                    if (_wWmType != null && _wWmSpeedProp != null && _wWmAngleProp != null)
                    {
                        UnityEngine.Object wm = UnityEngine.Object.FindObjectOfType(_wWmType);
                        if (wm != null)
                        {
                            _wWmSpeedProp.SetValue(wm, gustSpeed, null);
                            _wWmAngleProp.SetValue(wm, gustAngle, null);
                        }
                    }
                }
                catch (Exception ex) { BirdieLog.Warning("[Birdie] Gust set: " + ex.Message); }

                PlayGustSound();

                // Hold each gust pulse for a moment before next shift
                if (i < bursts - 1)
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.8f, 2.5f));
            }
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

                Vector3 fling = Vector3.up * 28f + UnityEngine.Random.insideUnitSphere.normalized * 16f;
                FlingPlayer(target, fling);
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
            case WeatherType.WindGustsLight:   return 20f;
            case WeatherType.WindGustsMedium:  return 45f;
            case WeatherType.WindGustsHeavy:   return 75f;
            case WeatherType.Thunderstorm:     return 50f;
            case WeatherType.Tornado:          return 90f;
            case WeatherType.RainLight:        return 8f;
            case WeatherType.RainMedium:       return 12f;
            case WeatherType.RainHeavy:        return 18f;
            default:                           return 0f;
        }
    }

    // FlingPlayer — sets linearVelocity directly on the PlayerMovement's private Rigidbody.
    // Unity 6 renamed Rigidbody.velocity → linearVelocity; we try both via reflection.
    private void FlingPlayer(Component target, Vector3 vel)
    {
        if (target == null) return;
        try
        {
            // Prefer the private rigidbody field on PlayerMovement for direct access
            FieldInfo rbField = target.GetType().GetField("rigidbody",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Rigidbody rb = rbField != null ? rbField.GetValue(target) as Rigidbody : null;
            if (rb == null) rb = target.GetComponent<Rigidbody>()
                             ?? target.GetComponentInChildren<Rigidbody>()
                             ?? target.GetComponentInParent<Rigidbody>();
            if (rb == null) return;

            rb.isKinematic = false;

            // Try Unity 6 API first, fall back to legacy
            PropertyInfo lvProp = typeof(Rigidbody).GetProperty("linearVelocity")
                               ?? typeof(Rigidbody).GetProperty("velocity");
            if (lvProp != null && lvProp.CanWrite)
                lvProp.SetValue(rb, vel);
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] FlingPlayer: " + ex.Message); }
    }

    private void SetBaseWind(WeatherType t)
    {
        int speed;
        switch (t)
        {
            case WeatherType.RainLight:       speed = 8;  break;
            case WeatherType.RainMedium:      speed = 14; break;
            case WeatherType.RainHeavy:       speed = 20; break;
            case WeatherType.WindGustsLight:  speed = 15; break;
            case WeatherType.WindGustsMedium: speed = 30; break;
            case WeatherType.WindGustsHeavy:  speed = 50; break;
            case WeatherType.Thunderstorm:    speed = 35; break;
            case WeatherType.Tornado:         speed = 60; break;
            default:                          return;
        }
        EnsureWmSyncReflection();
        try
        {
            if (_wWmType == null || _wWmSpeedProp == null || _wWmAngleProp == null) return;
            UnityEngine.Object wm = UnityEngine.Object.FindObjectOfType(_wWmType);
            if (wm == null) return;
            _wWmSpeedProp.SetValue(wm, speed, null);
            _wWmAngleProp.SetValue(wm, UnityEngine.Random.Range(0, 360), null);
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] SetBaseWind: " + ex.Message); }
    }

    // ── Auto weather ──────────────────────────────────────────────────────────

    private void SubscribeHoleChangeEvent()
    {
        if (_holeEventSubscribed) return;
        try
        {
            Type cmType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            { cmType = asm.GetType("CourseManager"); if (cmType != null) break; }
            if (cmType == null) return;

            EventInfo ei = cmType.GetEvent("CurrentHoleGlobalIndexChanged",
                BindingFlags.Public | BindingFlags.Static);
            if (ei == null) return;

            MethodInfo mi = typeof(BirdieMod).GetMethod("OnHoleChanged",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null) return;

            Delegate d = Delegate.CreateDelegate(typeof(Action), this, mi);
            ei.AddEventHandler(null, d);
            _holeEventSubscribed = true;
        }
        catch (Exception ex) { BirdieLog.Warning("[Birdie] Weather hole-event sub: " + ex.Message); }
    }

    private void OnHoleChanged()
    {
        if (!autoWeatherEnabled || !IsNetworkHost()) return;

        // Clear weather between holes
        if (_hostWeatherRunning)
        {
            _hostWeatherRunning = false;
            BirdieWeatherBridge.BroadcastWeather(0, true);
            ApplyWeather(0);
        }

        // Roll against spawn chance
        if (UnityEngine.Random.Range(0, 100) < autoWeatherChance)
            BirdieCoroutine.Start(AutoSpawnWeatherCoroutine());
    }

    private IEnumerator AutoSpawnWeatherCoroutine()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 10f));

        // Weighted random selection — higher % = more likely to be chosen
        int totalWeight = 0;
        for (int i = 0; i < autoWeatherChances.Length; i++)
            if (autoWeatherChances[i] > 0) totalWeight += autoWeatherChances[i];
        if (totalWeight == 0) yield break;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        byte pick = 0;
        int cumulative = 0;
        for (int i = 0; i < autoWeatherChances.Length; i++)
        {
            if (autoWeatherChances[i] <= 0) continue;
            cumulative += autoWeatherChances[i];
            if (roll < cumulative) { pick = (byte)(i + 1); break; }
        }
        if (pick == 0) yield break;

        bool weatherAllowed = (hostAllowedFeatureMask & (1UL << 15)) != 0;
        _hostSelectedWeather = pick;
        _hostWeatherRunning  = true;
        BirdieWeatherBridge.BroadcastWeather(pick, weatherAllowed);
        ApplyWeather(pick);
        BirdieLog.Msg("[Birdie] Auto-weather spawned: " + (WeatherType)pick);
    }

}
