namespace ModbusChart.Models;

public class AppSettings
{
    // ─── Modbus Connection ─────────────────────────────────────
    public string ModbusHost { get; set; } = "192.168.1.1";
    public int ModbusPort { get; set; } = 502;
    public int UnitId { get; set; } = 1;
    public int PollingIntervalMs { get; set; } = 1000;
    public int ConnectionTimeoutMs { get; set; } = 3000;
public int ViewWindowMinutes { get; set; } = 5;

    // ─── Axis Scale ────────────────────────────────────────────
    public double PressureYMin { get; set; } = 0;
    public double PressureYMax { get; set; } = 1000;
    public int PressureAxisDivisions { get; set; } = 10;

    public double TempSpeedYMin { get; set; } = 0;
    public double TempSpeedYMax { get; set; } = 100;
    public int TempSpeedAxisDivisions { get; set; } = 10;

    // ─── Units ─────────────────────────────────────────────────
    public string PressureUnit { get; set; } = "bar";    // "bar" | "psi"
    public string TemperatureUnit { get; set; } = "°C";  // "°C"  | "°F"
    public string SpeedUnit { get; set; } = "RPM";       // "RPM" | "cc/min"
    public double SpeedCcPerRev { get; set; } = 1.0;     // factor: cc/min = RPM × SpeedCcPerRev

    // ─── Channels ──────────────────────────────────────────────
    public List<ChannelConfig> Channels { get; set; } = CreateDefaultChannels();

    public static List<ChannelConfig> CreateDefaultChannels() => new()
    {
        new() { Id = LftChannelId.P1, Label = "Pressure Inlet",     Address = 0,  Color = "#263238", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Pressure    },
        new() { Id = LftChannelId.P2, Label = "Pressure at Screen", Address = 2,  Color = "#00ACC1", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Pressure    },
        new() { Id = LftChannelId.T1, Label = "Temp Gear Pump",     Address = 4,  Color = "#43A047", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Temperature },
        new() { Id = LftChannelId.T2, Label = "Temp at Screen",     Address = 6,  Color = "#B71C1C", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Temperature },
        new() { Id = LftChannelId.V1, Label = "Extruder",           Address = 8,  Color = "#6A1B9A", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Speed       },
        new() { Id = LftChannelId.V2, Label = "Gear Pump",          Address = 10, Color = "#AD1457", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Speed       },
        new() { Id = LftChannelId.I1, Label = "%Amp Extruder",      Address = 12, Color = "#1565C0", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Ampere      },
        new() { Id = LftChannelId.I2, Label = "%Amp GearPump",      Address = 14, Color = "#C62828", DataType = ModbusDataType.Int16Unsigned, SignalType = ChannelSignalType.Ampere      },
    };

    public ChannelConfig GetChannel(LftChannelId id)
        => Channels.First(c => c.Id == id);

    // ─── Unit conversion helpers ───────────────────────────────
    public double ConvertPressure(double bar)
        => PressureUnit == "psi" ? bar * 14.5038 : bar;

    public double ConvertTemperature(double celsius)
        => TemperatureUnit == "°F" ? celsius * 1.8 + 32 : celsius;

    public double ConvertSpeed(double rpm)
        => SpeedUnit == "cc/min" ? rpm * SpeedCcPerRev : rpm;
}
