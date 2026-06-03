namespace ModbusChart.ViewModels;

public class ChannelState
{
    public double Current { get; private set; } = double.NaN;
    public double StartValue { get; private set; } = double.NaN;
    public double MinValue { get; private set; } = double.NaN;
    public double MaxValue { get; private set; } = double.NaN;
    private double _sum;
    private int _count;
    public bool HasError { get; set; }

    public double Average => _count > 0 ? _sum / _count : double.NaN;

    public void Reset()
    {
        Current = double.NaN;
        StartValue = double.NaN;
        MinValue = double.NaN;
        MaxValue = double.NaN;
        _sum = 0;
        _count = 0;
        HasError = false;
    }

    public void Record(double value)
    {
        if (double.IsNaN(StartValue)) StartValue = value;
        Current = value;
        MinValue = double.IsNaN(MinValue) ? value : Math.Min(MinValue, value);
        MaxValue = double.IsNaN(MaxValue) ? value : Math.Max(MaxValue, value);
        _sum += value;
        _count++;
        HasError = false;
    }
}
