using MelonLoader;

[assembly: MelonInfo(typeof(BirdieMod), "Birdie Mod", "1.2.0", "Cb12438")]
[assembly: MelonGame]

// MelonLoader entry point — compiled only in the MelonLoader build.
// Wires BirdieLog / BirdieCoroutine and bridges Unity lifecycle methods.
public partial class BirdieMod : MelonMod
{
    [System.Obsolete]
    public override void OnApplicationStart()
    {
        BirdieLog.MsgImpl  = s => MelonLogger.Msg(s);
        BirdieLog.WarnImpl = s => MelonLogger.Warning(s);
        BirdieCoroutine.StartImpl = e => MelonCoroutines.Start(e);
        BirdieInit();
    }

    public override void OnUpdate()     => BirdieUpdate();
    public override void OnLateUpdate() => BirdieLateUpdate();
    public override void OnGUI()        => BirdieOnGUI();
}
