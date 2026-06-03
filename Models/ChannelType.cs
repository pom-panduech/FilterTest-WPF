namespace ModbusChart.Models;

public enum LftChannelId { P1, P2, T1, T2, V1, V2, I1, I2 }

public enum ModbusDataType
{
    Int16Unsigned,
    Int16Signed,
    Float32
}

public enum ModbusFunctionCode
{
    HoldingRegister = 3,
    InputRegister = 4
}

public enum ChannelSignalType
{
    Pressure,
    Temperature,
    Speed,
    Ampere
}
