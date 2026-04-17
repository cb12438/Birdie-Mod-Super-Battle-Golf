using BepInEx;
using BepInEx.Logging;

// BepInEx entry point — compiled only in the BepInEx build.
// Wires BirdieLog / BirdieCoroutine and bridges Unity lifecycle methods.
[BepInPlugin("com.cb12438.birdiemod", "Birdie Mod", "1.1.0")]
public partial class BirdieMod : BaseUnityPlugin
{
    private new ManualLogSource Logger;

    private void Awake()
    {
        Logger = base.Logger;
        BirdieLog.MsgImpl  = s => Logger.LogInfo(s);
        BirdieLog.WarnImpl = s => Logger.LogWarning(s);
        BirdieCoroutine.StartImpl = e => StartCoroutine(e);
        BirdieInit();
    }

    private void Update()     => BirdieUpdate();
    private void LateUpdate() => BirdieLateUpdate();
    private void OnGUI()      => BirdieOnGUI();
}
