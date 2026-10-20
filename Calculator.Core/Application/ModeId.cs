namespace Calculator.Core;

public enum ModeId
{
    STANDARD, SCIENTIFIC, GRAPHING, PROGRAMMER, DATE, CURRENCY,
    VOLUME, LENGTH, MASS, TEMPERATURE, ENERGY, AREA, SPEED, TIME,
    POWER, STORAGE, PRESSURE, ANGLE
}

public static class Modes
{
    public static string Title(this ModeId mode) => mode switch
    {
        ModeId.DATE => "Date Calculation",
        ModeId.MASS => "Weight and Mass",
        ModeId.STORAGE => "Data",
        _ => char.ToUpperInvariant(mode.ToString()[0]) + mode.ToString()[1..].ToLowerInvariant()
    };
    public static bool IsConverter(this ModeId mode) => mode >= ModeId.CURRENCY;
    public static int? WorkspaceLimit(this ModeId mode) => mode switch
    {
        ModeId.STANDARD => 520,
        ModeId.SCIENTIFIC or ModeId.PROGRAMMER => 640,
        ModeId.DATE => 560,
        ModeId.GRAPHING => 960,
        _ => null
    };
}
