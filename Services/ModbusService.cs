using System.Buffers.Binary;
using System.Net;
using FluentModbus;
using ModbusChart.Models;
using FC = ModbusChart.Models.ModbusFunctionCode;

namespace ModbusChart.Services;

public class ModbusService : IDisposable
{
    private ModbusTcpClient _client;
    private readonly object _lock = new();
    private bool _disposed;
    private byte _unitId = 1;

    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                try { return _client.IsConnected; }
                catch { return false; }
            }
        }
    }

    public ModbusService()
    {
        _client = new ModbusTcpClient();
    }

    public void Connect(string host, int port, int unitId = 1, int timeoutMs = 3000)
    {
        lock (_lock)
        {
            _unitId = (byte)unitId;
            try { _client.Disconnect(); } catch { }

            _client = new ModbusTcpClient
            {
                ReadTimeout = timeoutMs,
                WriteTimeout = timeoutMs
            };

            IPAddress ip;
            if (!IPAddress.TryParse(host, out ip!))
                ip = Dns.GetHostAddresses(host).First();

            _client.Connect(new IPEndPoint(ip, port));
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            try { _client.Disconnect(); } catch { }
        }
    }

    public double ReadChannel(ChannelConfig ch)
    {
        lock (_lock)
        {
            double raw;

            if (ch.DataType == ModbusDataType.Float32)
            {
                Span<ushort> words = ch.FunctionCode == FC.InputRegister
                    ? _client.ReadInputRegisters<ushort>(_unitId, ch.Address, 2)
                    : _client.ReadHoldingRegisters<ushort>(_unitId, ch.Address, 2);
                uint hi = BinaryPrimitives.ReverseEndianness(words[0]);
                uint lo = BinaryPrimitives.ReverseEndianness(words[1]);
                raw = BitConverter.Int32BitsToSingle((int)((hi << 16) | lo));
            }
            else if (ch.DataType == ModbusDataType.Int16Signed)
            {
                Span<short> data = ch.FunctionCode == FC.InputRegister
                    ? _client.ReadInputRegisters<short>(_unitId, ch.Address, 1)
                    : _client.ReadHoldingRegisters<short>(_unitId, ch.Address, 1);
                raw = BinaryPrimitives.ReverseEndianness(data[0]);
            }
            else
            {
                Span<ushort> data = ch.FunctionCode == FC.InputRegister
                    ? _client.ReadInputRegisters<ushort>(_unitId, ch.Address, 1)
                    : _client.ReadHoldingRegisters<ushort>(_unitId, ch.Address, 1);
                raw = BinaryPrimitives.ReverseEndianness(data[0]);
            }

            return raw * ch.ScaleFactor + ch.Offset;
        }
    }

    public Dictionary<LftChannelId, (double Value, bool HasError)> ReadAll(List<ChannelConfig> channels)
    {
        var results = new Dictionary<LftChannelId, (double, bool)>();
        foreach (var ch in channels)
        {
            try
            {
                results[ch.Id] = (ReadChannel(ch), false);
            }
            catch
            {
                results[ch.Id] = (double.NaN, true);
            }
        }
        return results;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Disconnect();
            _client.Dispose();
        }
    }
}
