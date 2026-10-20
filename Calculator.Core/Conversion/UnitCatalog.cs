using PeterO.Numbers;

namespace Calculator.Core;

public sealed record UnitDefinition(string Id, string Label, EDecimal Scale)
{
    public override string ToString() => Label;
}

public sealed record ConversionCategory(ModeId Id, string Title, IReadOnlyList<UnitDefinition> Units)
{
    public EDecimal Convert(EDecimal amount, string fromId, string toId)
    {
        var from = Units.FirstOrDefault(unit => unit.Id == fromId) ?? throw new ArgumentException("Unknown source unit.");
        var to = Units.FirstOrDefault(unit => unit.Id == toId) ?? throw new ArgumentException("Unknown destination unit.");
        Numbers.Checked(amount);
        if (Id != ModeId.TEMPERATURE)
            return Numbers.Checked(amount.Multiply(from.Scale, Numbers.MC).Divide(to.Scale, Numbers.MC));

        var celsius = from.Id switch
        {
            "fahrenheit" => amount.Subtract(D("32")).Multiply(D("5")).Divide(D("9"), Numbers.MC),
            "kelvin" => amount.Subtract(D("273.15")),
            _ => amount
        };
        if (celsius.CompareTo(D("-273.15")) < 0)
            throw new CalculationException("Temperature cannot be below absolute zero", CalculationError.Domain);
        var result = to.Id switch
        {
            "fahrenheit" => celsius.Multiply(D("9")).Divide(D("5"), Numbers.MC).Add(D("32")),
            "kelvin" => celsius.Add(D("273.15")),
            _ => celsius
        };
        return Numbers.Checked(result.RoundToPrecision(Numbers.MC));
    }

    private static EDecimal D(string value) => EDecimal.FromString(value);
}

public static class UnitCatalog
{
    private static UnitDefinition U(string id, string label, string scale) => new(id, label, EDecimal.FromString(scale));
    private static UnitDefinition R(string id, string label, string numerator, string denominator) =>
        new(id, label, EDecimal.FromString(numerator).Divide(EDecimal.FromString(denominator), Numbers.MC));
    private static ConversionCategory C(ModeId id, string title, params UnitDefinition[] units) => new(id, title, Array.AsReadOnly(units));

    public static IReadOnlyList<ConversionCategory> Categories { get; } = Array.AsReadOnly(new[]
    {
        C(ModeId.LENGTH, "Length", U("mm", "Millimeters (mm)", ".001"), U("cm", "Centimeters (cm)", ".01"), U("m", "Meters (m)", "1"), U("km", "Kilometers (km)", "1000"), U("in", "Inches (in)", ".0254"), U("ft", "Feet (ft)", ".3048"), U("yd", "Yards (yd)", ".9144"), U("mi", "Miles (mi)", "1609.344")),
        C(ModeId.AREA, "Area", U("mm2", "Square millimeters", ".000001"), U("cm2", "Square centimeters", ".0001"), U("m2", "Square meters", "1"), U("km2", "Square kilometers", "1000000"), U("in2", "Square inches", ".00064516"), U("ft2", "Square feet", ".09290304"), U("yd2", "Square yards", ".83612736"), U("mi2", "Square miles", "2589988.110336"), U("ha", "Hectares", "10000"), U("acre", "Acres", "4046.8564224")),
        C(ModeId.VOLUME, "Volume", U("ml", "Milliliters (mL)", ".001"), U("l", "Liters (L)", "1"), U("m3", "Cubic meters (m³)", "1000"), U("usgal", "US gallons", "3.785411784")),
        C(ModeId.MASS, "Weight and Mass", U("mg", "Milligrams (mg)", ".000001"), U("g", "Grams (g)", ".001"), U("kg", "Kilograms (kg)", "1"), U("oz", "Ounces (oz)", ".028349523125"), U("lb", "Pounds (lb)", ".45359237")),
        C(ModeId.TEMPERATURE, "Temperature", U("celsius", "Celsius (°C)", "1"), U("fahrenheit", "Fahrenheit (°F)", "1"), U("kelvin", "Kelvin (K)", "1")),
        C(ModeId.SPEED, "Speed", U("mps", "Meters / second", "3.6"), U("kph", "Kilometers / hour", "1"), U("mph", "Miles / hour", "1.609344"), U("knot", "Knots", "1.852")),
        C(ModeId.TIME, "Time", U("ms", "Milliseconds", ".001"), U("s", "Seconds", "1"), U("min", "Minutes", "60"), U("h", "Hours", "3600"), U("day", "Days", "86400"), U("week", "Weeks", "604800")),
        C(ModeId.STORAGE, "Data", U("bit", "Bits (bit)", ".125"), U("b", "Bytes (B)", "1"), U("kb", "Kilobytes (kB)", "1000"), U("mb", "Megabytes (MB)", "1000000"), U("gb", "Gigabytes (GB)", "1000000000"), U("tb", "Terabytes (TB)", "1000000000000"), U("kib", "Kibibytes (KiB)", "1024"), U("mib", "Mebibytes (MiB)", "1048576"), U("gib", "Gibibytes (GiB)", "1073741824"), U("tib", "Tebibytes (TiB)", "1099511627776")),
        C(ModeId.ENERGY, "Energy", U("j", "Joules (J)", "1"), U("kj", "Kilojoules (kJ)", "1000"), U("cal", "Thermochemical calories (cal)", "4.184"), U("kcal", "Thermochemical kilocalories (kcal)", "4184"), U("wh", "Watt-hours (Wh)", "3600"), U("kwh", "Kilowatt-hours (kWh)", "3600000"), U("ftlbf", "Foot-pound-force (ft lbf)", "1.3558179483314004"), U("btu", "BTU (IT)", "1055.05585262")),
        C(ModeId.POWER, "Power", U("w", "Watts (W)", "1"), U("kw", "Kilowatts (kW)", "1000"), U("hp", "Mechanical horsepower (hp)", "745.69987158227022"), U("ps", "Metric horsepower (PS)", "735.49875"), R("ftlbfmin", "Foot-pound-force / minute", "1.3558179483314004", "60"), R("btumin", "BTU (IT) / minute", "1055.05585262", "60")),
        C(ModeId.PRESSURE, "Pressure", U("pa", "Pascals (Pa)", "1"), U("kpa", "Kilopascals (kPa)", "1000"), U("mpa", "Megapascals (MPa)", "1000000"), U("bar", "Bars (bar)", "100000"), U("mbar", "Millibars (mbar)", "100"), U("atm", "Standard atmospheres (atm)", "101325"), R("psi", "Pounds-force / square inch (PSI)", "4.4482216152605", ".00064516"), R("torr", "Torr", "101325", "760")),
        C(ModeId.ANGLE, "Angle", U("deg", "Degrees", "1"), R("rad", "Radians (rad)", "180", "3.14159265358979323846264338327950288419716939937510"), U("gon", "Gradians (gon)", ".9"))
    });

    public static ConversionCategory Get(ModeId mode) => Categories.FirstOrDefault(category => category.Id == mode)
        ?? throw new ArgumentException("The mode is not a static converter.", nameof(mode));
}
