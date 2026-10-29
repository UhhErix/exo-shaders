namespace SCSKiller.Core;

/// <summary>The driver-update picker's four choices over <see cref="Settings.OnDriverUpdate"/> and
/// <see cref="Settings.DriverRightAway"/>: 0 ask first, 1 when the PC is idle, 2 automatically right away (when no game runs),
/// 3 off.</summary>
public static class DriverUpdateChoice
{
    public static int Of(Settings s) => s.OnDriverUpdate switch
    {
        DriverUpdateMode.Ask => 0,
        DriverUpdateMode.WhenIdle => s.DriverRightAway ? 2 : 1,
        _ => 3,
    };

    public static Settings With(Settings s, int choice) => s with
    {
        OnDriverUpdate = choice switch
        {
            0 => DriverUpdateMode.Ask,
            1 or 2 => DriverUpdateMode.WhenIdle,
            _ => DriverUpdateMode.Off,
        },
        DriverRightAway = choice == 2,
    };
}
