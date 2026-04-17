// Framework-agnostic logging and coroutine facades.
// Set MsgImpl / WarnImpl / StartImpl once in the entry shim (Awake / OnApplicationStart),
// then call BirdieLog / BirdieCoroutine freely from any partial class file.

internal static class BirdieLog
{
    internal static System.Action<string> MsgImpl;
    internal static System.Action<string> WarnImpl;

    internal static void Msg(string s)     => MsgImpl?.Invoke(s);
    internal static void Warning(string s) => WarnImpl?.Invoke(s);
}

internal static class BirdieCoroutine
{
    internal static System.Action<System.Collections.IEnumerator> StartImpl;

    internal static void Start(System.Collections.IEnumerator e) => StartImpl?.Invoke(e);
}
