using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModbusChart.Models;

public class ChannelConfig : INotifyPropertyChanged
{
    private string _label = "";
    private int _address;
    private ModbusDataType _dataType = ModbusDataType.Int16Unsigned;
    private ModbusFunctionCode _functionCode = ModbusFunctionCode.HoldingRegister;
    private double _scaleFactor = 1.0;
    private double _offset = 0.0;
    private ChannelSignalType _signalType = ChannelSignalType.Pressure;
    private bool _showInGraph = true;
    private string _color = "#FFFFFF";

    public LftChannelId Id { get; set; }

    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public int Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    public ModbusDataType DataType
    {
        get => _dataType;
        set { _dataType = value; OnPropertyChanged(); }
    }

    public ModbusFunctionCode FunctionCode
    {
        get => _functionCode;
        set { _functionCode = value; OnPropertyChanged(); }
    }

    public double ScaleFactor
    {
        get => _scaleFactor;
        set { _scaleFactor = value; OnPropertyChanged(); }
    }

    public double Offset
    {
        get => _offset;
        set { _offset = value; OnPropertyChanged(); }
    }

    public ChannelSignalType SignalType
    {
        get => _signalType;
        set { _signalType = value; OnPropertyChanged(); }
    }

    public bool ShowInGraph
    {
        get => _showInGraph;
        set { _showInGraph = value; OnPropertyChanged(); }
    }

    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
