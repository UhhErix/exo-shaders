using System.Runtime.InteropServices;
using SCSKiller.Core.Warming;

namespace SCSKiller.Core.App;

/// <summary>
/// Whether the PC is really idle, for work that waits for it ("when idle" items, precompiled-database rebuilds): nobody
/// is using it, not only "no input for a while" (a browser, a build or a video with no key pressed is someone using it).
/// Idle only when every one of these holds:
/// - no keyboard or mouse input for <see cref="Away"/>, or for <see cref="AwayLocked"/> while the session is locked or
///   the display is off;
/// - no app presents or runs full screen (SHQueryUserNotificationState: busy, Direct3D full screen, presentation mode);
/// - no sound played in the last <see cref="SoundWindow"/> (an active output device's peak above <see cref="Silence"/>);
/// - no microphone is in use (an active capture session: a call);
/// - other processes used under <see cref="CpuShare"/> of the processor over the last <see cref="CpuWindow"/> (this
///   process and its children, the compile among them, don't count);
/// - no game runs, and the PC isn't on battery.
/// <see cref="Now"/> is cheap to ask often: input is read at each ask, and only once the user has been away long enough
/// does a sampler read sound, the microphone and the processor, every <see cref="SampleEvery"/> for as long as it is
/// asked (it stops <see cref="StopAfter"/> after the last ask, or at the first input). Exo Launcher has its own copy of
/// this rule (Helpers/PcIdle.cs) for the work it does while the PC is idle.
/// </summary>
public static class PcIdle
{
    public static readonly TimeSpan Away = TimeSpan.FromMinutes(10), AwayLocked = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan SoundWindow = TimeSpan.FromSeconds(10), CpuWindow = TimeSpan.FromMinutes(1);
    /// <summary>About one and a half busy threads' worth of the processor, between 5% and 40%, so the rule means the same on
    /// 4 threads as on 32 (Exo Launcher's PcIdle keeps the same rule).</summary>
    public static double CpuShareFor(int threads) => Math.Clamp(1.5 / Math.Max(1, threads), 0.05, 0.40);

    public static readonly double CpuShare = CpuShareFor(Environment.ProcessorCount);
    /// <summary>A peak above this (about -60 dB) is sound.</summary>
    public const float Silence = 0.001f;
    static readonly TimeSpan SampleEvery = TimeSpan.FromSeconds(2), StopAfter = TimeSpan.FromMinutes(2);

    /// <param name="Input">how long since the last keyboard or mouse input</param>
    /// <param name="Quiet">the session is locked or the display is off</param>
    /// <param name="Presenting">an app is full screen or presenting</param>
    /// <param name="Sound">sound played within <see cref="SoundWindow"/></param>
    /// <param name="Microphone">a microphone is in use</param>
    /// <param name="OthersCpu">other processes' share of the processor over <see cref="CpuWindow"/>, 0 to 1; null: not
    /// measured that long yet</param>
    public sealed record Readings(TimeSpan Input, bool Quiet, bool Presenting, bool Sound, bool Microphone, double? OthersCpu, bool Game, bool Battery);

    /// <summary>The rule: every condition holds.</summary>
    public static bool Idle(Readings r) =>
        r.Input >= (r.Quiet ? AwayLocked : Away) && !r.Presenting && !r.Sound && !r.Microphone
        && r.OthersCpu is { } cpu && cpu < CpuShare && !r.Game && !r.Battery;

    /// <summary>One sampling interval: its length, the processor time every process used (busy) out of all there was,
    /// and what this process and its children used of it, in 100 ns units.</summary>
    public readonly record struct Interval(long Ms, long Busy, long All, long Own);

    /// <summary>Other processes' share of the processor over the newest intervals that cover <paramref name="window"/>;
    /// null when they cover less than that less one sample (the sampler hasn't run that long).</summary>
    public static double? OthersShare(IReadOnlyList<Interval> intervals, TimeSpan window)
    {
        long ms = 0, busy = 0, all = 0, own = 0;
        for (var i = intervals.Count - 1; i >= 0 && ms < window.TotalMilliseconds; i--)
            (ms, busy, all, own) = (ms + intervals[i].Ms, busy + intervals[i].Busy, all + intervals[i].All, own + intervals[i].Own);
        if (ms < (window - SampleEvery).TotalMilliseconds || all <= 0) return null;
        return Math.Clamp((double)(busy - own) / all, 0, 1);
    }

    static readonly Lock Gate = new();
    static Timer? _sampler;
    static long _askedAt, _soundAt = long.MinValue / 2;
    static bool _microphone;
    static readonly List<Interval> Intervals = [];
    static (long At, long Busy, long All, Dictionary<int, long> Own)? _last;

    /// <summary>The PC is idle now (<see cref="Idle"/>). <paramref name="game"/>: a game runs, as the caller knows it.</summary>
    public static bool Now(Func<bool> game)
    {
        if (!OperatingSystem.IsWindows()) return false;
        WatchDisplay();
        var input = InputIdle();
        if (input < AwayLocked) return false;
        var quiet = _displayOff || Locked();
        if (input < (quiet ? AwayLocked : Away)) return false;
        double? cpu;
        bool sound, microphone;
        lock (Gate)
        {
            _askedAt = Environment.TickCount64;
            _sampler ??= new Timer(_ => Sample(), null, TimeSpan.Zero, SampleEvery);
            cpu = OthersShare(Intervals, CpuWindow);
            sound = Environment.TickCount64 - _soundAt < SoundWindow.TotalMilliseconds;
            microphone = _microphone;
        }
        return Idle(new Readings(input, quiet, Presenting(), sound, microphone, cpu, game(), OnBattery()));
    }

    static void Sample()
    {
        try
        {
            bool stop;
            lock (Gate) stop = Environment.TickCount64 - _askedAt > StopAfter.TotalMilliseconds;
            if (stop || InputIdle() < AwayLocked)
            {
                lock (Gate)
                {
                    _sampler?.Dispose();
                    _sampler = null;
                    Intervals.Clear();
                    _last = null;
                }
                return;
            }
            var (sound, microphone) = Audio();
            var now = Environment.TickCount64;
            GetSystemTimes(out var idle, out var kernel, out var user);
            long all = kernel + user, busy = all - idle;
            var own = new Dictionary<int, long>();
            foreach (var pid in ProcessTree.WithDescendants(Environment.ProcessId))
                if (CpuOf(pid) is { } used) own[pid] = used;
            lock (Gate)
            {
                if (sound) _soundAt = now;
                _microphone = microphone;
                if (_last is { } last)
                {
                    var ownDelta = own.Sum(p => Math.Max(0, p.Value - last.Own.GetValueOrDefault(p.Key)));
                    Intervals.Add(new Interval(now - last.At, busy - last.Busy, all - last.All, ownDelta));
                    while (Intervals.Count > 2 * CpuWindow / SampleEvery) Intervals.RemoveAt(0);
                }
                _last = (now, busy, all, own);
            }
        }
        catch (Exception) { }   // a reading that fails leaves the last ones; a timer thread never throws
    }

    // ------------------------------------------------------------------ readings

    static TimeSpan InputIdle()
    {
        var info = new LastInputInfo { Size = 8 };
        // No input info: never idle (the work waits rather than run under someone).
        return GetLastInputInfo(ref info) ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time)) : TimeSpan.Zero;
    }

    /// <summary>The session is locked (WTSSessionInfoEx's SessionFlags: WTS_SESSIONSTATE_LOCK).</summary>
    static bool Locked()
    {
        if (!WTSQuerySessionInformationW(0, unchecked((int)0xFFFFFFFF), 25, out var buffer, out var bytes)) return false;
        try { return bytes >= 20 && Marshal.ReadInt32(buffer, 16) == 0; }
        finally { WTSFreeMemory(buffer); }
    }

    static bool Presenting() => SHQueryUserNotificationState(out var state) == 0 && state is 2 or 3 or 4;

    static bool OnBattery() => GetSystemPowerStatus(out var p) && p.AcLineStatus == 0;

    static long? CpuOf(int pid)
    {
        var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == 0) return null;
        try { return GetProcessTimes(h, out _, out _, out var kernel, out var user) ? kernel + user : null; }
        finally { CloseHandle(h); }
    }

    // The display's state (GUID_CONSOLE_DISPLAY_STATE: 0 off, 1 on, 2 dimmed), told by Windows when it changes.
    static volatile bool _displayOff;
    static int _displayWatched;
    static DeviceNotifyCallback? _onDisplay;

    static void WatchDisplay()
    {
        if (Interlocked.Exchange(ref _displayWatched, 1) == 1) return;
        try
        {
            _onDisplay = (_, type, setting) =>
            {
                if (type == 0x8013 /* PBT_POWERSETTINGCHANGE */ && setting != 0) _displayOff = Marshal.ReadInt32(setting, 20) == 0;
                return 0;
            };
            var recipient = new DeviceNotifySubscribeParameters { Callback = Marshal.GetFunctionPointerForDelegate(_onDisplay) };
            var display = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");
            _ = PowerSettingRegisterNotification(ref display, 2 /* DEVICE_NOTIFY_CALLBACK */, ref recipient, out _);
        }
        catch (Exception) { }   // unknown: the display counts as on
    }

    /// <summary>Sound on any active output device in the last sample, and any active capture session (a microphone in use).</summary>
    static (bool Sound, bool Microphone) Audio()
    {
        object? enumerator = null;
        try
        {
            enumerator = new MMDeviceEnumeratorComObject();
            var devices = (IMMDeviceEnumerator)enumerator;
            return (AnyDevice(devices, 0 /* eRender */, Loud), AnyDevice(devices, 1 /* eCapture */, InUse));
        }
        catch (Exception e) when (e is COMException or InvalidCastException) { return (false, false); }
        finally { Release(enumerator); }
    }

    static bool AnyDevice(IMMDeviceEnumerator devices, int flow, Func<IMMDevice, bool> test)
    {
        if (devices.EnumAudioEndpoints(flow, 1 /* DEVICE_STATE_ACTIVE */, out var list) < 0) return false;
        try
        {
            if (list.GetCount(out var count) < 0) return false;
            for (var i = 0; i < count; i++)
            {
                if (list.Item(i, out var device) < 0) continue;
                try { if (test(device)) return true; }
                finally { Release(device); }
            }
            return false;
        }
        finally { Release(list); }
    }

    static bool Loud(IMMDevice device)
    {
        var iid = typeof(IAudioMeterInformation).GUID;
        if (device.Activate(ref iid, 23 /* CLSCTX_ALL */, 0, out var meter) < 0) return false;
        try { return ((IAudioMeterInformation)meter).GetPeakValue(out var peak) >= 0 && peak > Silence; }
        finally { Release(meter); }
    }

    static bool InUse(IMMDevice device)
    {
        var iid = typeof(IAudioSessionManager2).GUID;
        if (device.Activate(ref iid, 23, 0, out var manager) < 0) return false;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            if (((IAudioSessionManager2)manager).GetSessionEnumerator(out sessions) < 0 || sessions.GetCount(out var count) < 0) return false;
            for (var i = 0; i < count; i++)
            {
                if (sessions.GetSession(i, out var session) < 0) continue;
                try { if (session.GetState(out var state) >= 0 && state == 1 /* AudioSessionStateActive */) return true; }
                finally { Release(session); }
            }
            return false;
        }
        finally
        {
            Release(sessions);
            Release(manager);
        }
    }

    static void Release(object? com)
    {
        if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
    }

    // ------------------------------------------------------------------ Windows

    struct LastInputInfo { public uint Size, Time; }

    [StructLayout(LayoutKind.Sequential)]
    struct SystemPowerStatus { public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [StructLayout(LayoutKind.Sequential)]
    struct DeviceNotifySubscribeParameters { public nint Callback, Context; }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate uint DeviceNotifyCallback(nint context, uint type, nint setting);

    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
    [DllImport("kernel32.dll")] static extern nint OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool GetProcessTimes(nint process, out long creation, out long exit, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
    [DllImport("wtsapi32.dll")] static extern bool WTSQuerySessionInformationW(nint server, int session, int infoClass, out nint buffer, out int bytes);
    [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(nint memory);
    [DllImport("powrprof.dll")] static extern uint PowerSettingRegisterNotification(ref Guid setting, uint flags, ref DeviceNotifySubscribeParameters recipient, out nint handle);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorComObject;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(nint groupingParam, uint streamFlags, out nint control);
        [PreserveSig] int GetSimpleAudioVolume(nint groupingParam, uint streamFlags, out nint volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
    }
}
