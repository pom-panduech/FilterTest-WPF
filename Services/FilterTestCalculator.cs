using ModbusChart.Models;
using ModbusChart.ViewModels;

namespace ModbusChart.Services;

/// <summary>
/// คำนวณค่าสรุปของแต่ละ channel จาก ChannelState เพื่อแสดงใน Left Panel
/// </summary>
public class FilterTestCalculator
{
    private readonly Dictionary<LftChannelId, ChannelState> _state;

    public FilterTestCalculator(Dictionary<LftChannelId, ChannelState> state)
    {
        _state = state;
    }

    // ─── Pressure at Screen (P2) ──────────────────────────────────────────
    public string P2_PStart  => Fmt(S(LftChannelId.P2).StartValue);
    public string P2_PMax    => Fmt(S(LftChannelId.P2).MaxValue);
    public string P2_DeltaP  => FmtDelta(S(LftChannelId.P2).MaxValue, S(LftChannelId.P2).StartValue);

    // ─── Pump Inlet Pressure (P1) ─────────────────────────────────────────
    public string P1_PStart  => Fmt(S(LftChannelId.P1).StartValue);
    public string P1_PEnd    => Fmt(S(LftChannelId.P1).Current);

    // ─── Extruder (V1) ────────────────────────────────────────────────────
    public string V1_Speed   => Fmt(S(LftChannelId.V1).Current);
    public string I1_Amp     => Fmt(S(LftChannelId.I1).Current);

    // ─── Temp at Screen (T2) ─────────────────────────────────────────────
    public string T2_TStart  => Fmt(S(LftChannelId.T2).StartValue);
    public string T2_TEnd    => Fmt(S(LftChannelId.T2).Current);
    public string T2_TMax    => Fmt(S(LftChannelId.T2).MaxValue);
    public string T2_TAvg    => Fmt(S(LftChannelId.T2).Average);

    // ─── Pump Inlet Temp (T1) ────────────────────────────────────────────
    public string T1_TStart  => Fmt(S(LftChannelId.T1).StartValue);
    public string T1_TEnd    => Fmt(S(LftChannelId.T1).Current);

    // ─── Feed Rate (V2) ──────────────────────────────────────────────────
    public string V2_Speed   => Fmt(S(LftChannelId.V2).Current);
    public string I2_Amp     => Fmt(S(LftChannelId.I2).Current);

    // ─── FPV = (P2_Max - P2_Start) / TotalWeight ────────────────────────
    public string ComputeFPV(double totalWeight)
    {
        var delta = DeltaRaw(S(LftChannelId.P2).MaxValue, S(LftChannelId.P2).StartValue);
        if (double.IsNaN(delta)) return "—";
        if (totalWeight <= 0)    return "—";
        return (delta / totalWeight).ToString("F4");
    }

    // ─── Real-time display values ─────────────────────────────────────────
    public string MeltPressure          => Fmt(S(LftChannelId.P2).Current);
    public string MeltTemperature       => double.IsNaN(S(LftChannelId.T2).Current) ? "—" : S(LftChannelId.T2).Current.ToString("F1");
    public string FeedRate              => Fmt(S(LftChannelId.V1).Current);
    public string PressureGearPumpInlet => Fmt(S(LftChannelId.P1).Current);
    public string FiltertestUnitTemp    => Fmt(S(LftChannelId.T1).Current);

    // ─── Raw double values ────────────────────────────────────────────────
    public double P2_MaxRaw    => S(LftChannelId.P2).MaxValue;
    public double P2_StartRaw  => S(LftChannelId.P2).StartValue;
    public double P2_DeltaRaw  => DeltaRaw(S(LftChannelId.P2).MaxValue, S(LftChannelId.P2).StartValue);

    // ─── Helpers ─────────────────────────────────────────────────────────
    private ChannelState S(LftChannelId id) => _state[id];

    internal static string Fmt(double v) =>
        double.IsNaN(v) ? "—" : v.ToString("F2");

    internal static string FmtDelta(double max, double start) =>
        (double.IsNaN(max) || double.IsNaN(start)) ? "—" : (max - start).ToString("F2");

    private static double DeltaRaw(double max, double start) =>
        (double.IsNaN(max) || double.IsNaN(start)) ? double.NaN : max - start;
}
