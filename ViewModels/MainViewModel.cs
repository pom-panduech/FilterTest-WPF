using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using ModbusChart.Models;
using ModbusChart.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace ModbusChart.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    public event Action<string>? ShowAlert;

    private readonly ModbusService _modbus = new();
    private readonly DispatcherTimer _timer = new();
    private AppSettings _settings;
    private double _elapsedSeconds;
    private bool _isReading;
    private readonly Dictionary<string, string> _lastNotified = new();
    private StreamWriter? _csvWriter;
    private string _csvPath = "";

    // ─── Crosshair tracker ────────────────────────────────────────────────
    private bool _trackerVisible;
    private string _trackerText = "";
    private double _trackerX, _trackerY;
    public bool   TrackerVisible { get => _trackerVisible; private set => SetField(ref _trackerVisible, value); }
    public string TrackerText    { get => _trackerText;    private set => SetField(ref _trackerText, value); }
    public double TrackerX       { get => _trackerX;       private set => SetField(ref _trackerX, value); }
    public double TrackerY       { get => _trackerY;       private set => SetField(ref _trackerY, value); }
    public OxyRect PlotArea      => PlotModel.PlotArea;

    private LinearAxis _leftAxis = null!;
    private LinearAxis _rightAxis = null!;
    private LinearAxis _timeAxis = null!;
    private readonly Dictionary<LftChannelId, LineSeries> _seriesMap = new();
    private readonly Dictionary<LftChannelId, ChannelState> _state;
    private FilterTestCalculator _calc = null!;

    // ─── Plot ──────────────────────────────────────────────────────────────
    public PlotModel PlotModel { get; } = new();

    // ─── Connection state ──────────────────────────────────────────────────
    private bool _isConnected;
    private bool _isRunning;
    private string _statusText = "Disconnected";

    public bool IsConnected   { get => _isConnected;  private set { SetField(ref _isConnected, value);  OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); } }
    public bool IsRunning     { get => _isRunning;    private set { SetField(ref _isRunning, value);    OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(CanStop)); } }
    public string StatusText  { get => _statusText;   private set => SetField(ref _statusText, value); }
    private bool _canStart = true;
    private bool _isPaused;
    private bool _recordingComplete;
    private bool _isViewingCsv;
    public bool CanStart  => !IsRunning && _canStart && !_isViewingCsv;
    public bool CanPause  => IsRunning && !_isPaused;
    public bool CanResume => IsRunning && _isPaused;
    public void DisableStart()
    {
        _canStart = false;
        _recordingComplete = true;
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(RecordingStatusText));
    }
    public void EnableStart()
    {
        _canStart = true;
        _recordingComplete = false;
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(RecordingStatusText));
    }

    public string RecordingStatusText
    {
        get
        {
            if (_recordingComplete)        return "Recording Complete";
            if (IsRunning && _isPaused)    return "Paused";
            if (IsRunning)                 return "Recording";
            return "Not Recording";
        }
    }
    public bool CanStop       => IsRunning;

    private bool _hasData;
    public bool HasData { get => _hasData; private set => SetField(ref _hasData, value); }


    // ─── Legend items (for chart legend bar) ──────────────────────────────
    public ObservableCollection<LegendItem> LegendItems { get; } = new();

    // ─── Left panel stats (delegated to FilterTestCalculator) ────────────
    public string P2_PStart  => _calc.P2_PStart;
    public string P2_PMax    => _calc.P2_PMax;
    public string P2_DeltaP  => _calc.P2_DeltaP;
    public string P2_FPV     => _calc.ComputeFPV(double.TryParse(TotalWeight, out var w) ? w : 0);
    public string P1_PStart  => _calc.P1_PStart;
    public string P1_PEnd    => _calc.P1_PEnd;
    public string V1_Speed   => _calc.V1_Speed;
    public string I1_Amp     => _calc.I1_Amp;
    public string T2_TStart  => _calc.T2_TStart;
    public string T2_TEnd    => _calc.T2_TEnd;
    public string T2_TMax    => _calc.T2_TMax;
    public string T2_TAvg    => _calc.T2_TAvg;
    public string T1_TStart  => _calc.T1_TStart;
    public string T1_TEnd    => _calc.T1_TEnd;
    public string V2_Speed   => _calc.V2_Speed;
    public string I2_Amp     => _calc.I2_Amp;

    // ─── Unit labels (reactive to settings changes) ───────────────────────
    public string PressureUnitLabel           => _settings.PressureUnit;
    public string TemperatureUnitLabel        => _settings.TemperatureUnit;
    public string SpeedUnitLabel              => _settings.SpeedUnit;
    public string FpvUnitLabel                => $"{_settings.PressureUnit}/g";
    public string PressureGearPumpInletLabel  => $"Pressure Gear Pump Inlet ({_settings.PressureUnit})";
    public string FiltertestUnitTempLabel     => $"Filtertest Unit Temp ({_settings.TemperatureUnit})";

    private void NotifyUnitLabels()
    {
        OnPropertyChanged(nameof(PressureUnitLabel));
        OnPropertyChanged(nameof(TemperatureUnitLabel));
        OnPropertyChanged(nameof(SpeedUnitLabel));
        OnPropertyChanged(nameof(FpvUnitLabel));
        OnPropertyChanged(nameof(PressureGearPumpInletLabel));
        OnPropertyChanged(nameof(FiltertestUnitTempLabel));
    }

    // ─── Melt summary bar ─────────────────────────────────────────────────
    private string _meltPressure = "—";
    private string _meltTemperature = "—";
    public string MeltPressure    { get => _meltPressure;    set => SetField(ref _meltPressure, value); }
    public string MeltTemperature { get => _meltTemperature; set => SetField(ref _meltTemperature, value); }

    // ─── Test form fields ─────────────────────────────────────────────────
    private string _fileName = "";
    private string _sampleId = "";
    private string _basePolymer = "";
    private string _customer = "";
    private string _date = DateTime.Now.ToString("dd/MM/yy");
    private string _operator = "";
    private string _filterScreenSize = "";
    private string _feedRate = "";
    private string _pressureGearPumpInlet = "";
    private string _filtertestUnitTemp = "";
    private string _pigmentWeight = "";
    private string _totalWeight = "";

    public string FileName
    {
        get => _fileName;
        set => SetField(ref _fileName, value);
    }

    public string CsvPath => _csvPath;
    public string SampleId              { get => _sampleId;              set => SetField(ref _sampleId, value); }
    public string BasePolymer           { get => _basePolymer;           set => SetField(ref _basePolymer, value); }
    public string Customer              { get => _customer;              set => SetField(ref _customer, value); }
    public string Date                  { get => _date;                  set => SetField(ref _date, value); }
    public string Operator              { get => _operator;              set => SetField(ref _operator, value); }
    public string FilterScreenSize      { get => _filterScreenSize;      set => SetField(ref _filterScreenSize, value); }
    public string FeedRate              { get => _feedRate;              set => SetField(ref _feedRate, value); }
    public string PressureGearPumpInlet { get => _pressureGearPumpInlet; set => SetField(ref _pressureGearPumpInlet, value); }
    public string FiltertestUnitTemp    { get => _filtertestUnitTemp;    set => SetField(ref _filtertestUnitTemp, value); }
    public string PigmentWeight         { get => _pigmentWeight;         set => SetField(ref _pigmentWeight, value); }
    public string TotalWeight           { get => _totalWeight;           set => SetField(ref _totalWeight, value); }

    public AppSettings Settings => _settings;

    // ─── Channel display info (dynamic units) ─────────────────────────────
    private Dictionary<LftChannelId, (string Label, string Unit, bool IsLeftAxis)> BuildChannelMeta() => new()
    {
        [LftChannelId.P1] = ("P1  Pressure Inlet",     _settings.PressureUnit,    true),
        [LftChannelId.P2] = ("P2  Pressure at Screen", _settings.PressureUnit,    true),
        [LftChannelId.T1] = ("T1  Temp Gear Pump",     _settings.TemperatureUnit, false),
        [LftChannelId.T2] = ("T2  Temp at Screen",     _settings.TemperatureUnit, false),
        [LftChannelId.V1] = ("V1  Extruder",           _settings.SpeedUnit,       false),
        [LftChannelId.V2] = ("V2  Gear Pump",          _settings.SpeedUnit,       false),
        [LftChannelId.I1] = ("I1  %Amp Extruder",      "%",                       true),
        [LftChannelId.I2] = ("I2  %Amp GearPump",      "%",                       true),
    };
    private Dictionary<LftChannelId, (string Label, string Unit, bool IsLeftAxis)> _channelMeta = null!;

    public MainViewModel()
    {
        _settings = SettingsService.Load();
        _channelMeta = BuildChannelMeta();

        _state = Enum.GetValues<LftChannelId>()
            .ToDictionary(id => id, _ => new ChannelState());
        _calc = new FilterTestCalculator(_state);

        InitializePlot();
        RebuildSeries();

        _timer.Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs);
        _timer.Tick += OnTimerTick;
    }

    // ─── Plot setup ───────────────────────────────────────────────────────
    private void InitializePlot()
    {
        var chartBg = OxyColor.FromRgb(255, 255, 255);
        var gridColor = OxyColor.FromArgb(40, 100, 100, 120);
        var axisColor = OxyColor.FromRgb(80, 100, 130);

        PlotModel.Background = chartBg;
        PlotModel.PlotAreaBackground = chartBg;
        PlotModel.PlotAreaBorderColor = OxyColor.FromRgb(200, 210, 220);
        PlotModel.PlotAreaBorderThickness = new OxyThickness(1);
        PlotModel.TextColor = axisColor;
        PlotModel.IsLegendVisible = false;

        _timeAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Time (sec)",
            TitleColor = axisColor,
            TextColor = axisColor,
            TicklineColor = axisColor,
            AxislineColor = axisColor,
            AxislineStyle = LineStyle.Solid,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = gridColor,
            MinorGridlineStyle = LineStyle.None,
            Minimum = 0,
            Maximum = 300,
            AbsoluteMinimum = 0,
            FontSize = 10,
            IsZoomEnabled = false,
            IsPanEnabled = false,
        };

        _leftAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = $"Pressure ({_settings.PressureUnit}) Amperc (%)",
            Key = "Left",
            TitleColor = OxyColor.FromRgb(140, 80, 60),
            TextColor = OxyColor.FromRgb(160, 100, 80),
            TicklineColor = OxyColor.FromRgb(180, 120, 100),
            AxislineColor = OxyColor.FromRgb(180, 120, 100),
            AxislineStyle = LineStyle.Solid,
            MajorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = gridColor,
            MinorGridlineStyle = LineStyle.None,
            Minimum = _settings.PressureYMin,
            Maximum = _settings.PressureYMax,
            MajorStep = (_settings.PressureYMax - _settings.PressureYMin) / _settings.PressureAxisDivisions,
            FontSize = 10,
            IsZoomEnabled = false,
            IsPanEnabled = false,
        };

        _rightAxis = new LinearAxis
        {
            Position = AxisPosition.Right,
            Title = $"Temperature ({_settings.TemperatureUnit}) / Speed ({_settings.SpeedUnit})",
            Key = "Right",
            TitleColor = OxyColor.FromRgb(180, 100, 60),
            TextColor = OxyColor.FromRgb(190, 110, 70),
            TicklineColor = OxyColor.FromRgb(200, 120, 80),
            AxislineColor = OxyColor.FromRgb(200, 120, 80),
            AxislineStyle = LineStyle.Solid,
            MajorGridlineStyle = LineStyle.None,
            MinorGridlineStyle = LineStyle.None,
            Minimum = _settings.TempSpeedYMin,
            Maximum = _settings.TempSpeedYMax,
            MajorStep = (_settings.TempSpeedYMax - _settings.TempSpeedYMin) / _settings.TempSpeedAxisDivisions,
            FontSize = 10,
            IsZoomEnabled = false,
            IsPanEnabled = false,
        };

        PlotModel.Axes.Add(_timeAxis);
        PlotModel.Axes.Add(_leftAxis);
        PlotModel.Axes.Add(_rightAxis);
    }

    public void RebuildSeries()
    {
        // Save existing points before rebuild
        var saved = _seriesMap.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Points.ToList());

        PlotModel.Series.Clear();
        _seriesMap.Clear();
        LegendItems.Clear();

        foreach (var id in Enum.GetValues<LftChannelId>())
        {
            var cfg = _settings.GetChannel(id);
            var meta = _channelMeta[id];
            var color = OxyColor.Parse(cfg.Color);

            var series = new LineSeries
            {
                Title = meta.Label,
                Color = color,
                StrokeThickness = 1.5,
                YAxisKey = meta.IsLeftAxis ? "Left" : "Right",
                MarkerType = MarkerType.None,
                LineStyle = cfg.ShowInGraph ? LineStyle.Solid : LineStyle.None,
                TrackerFormatString = "{0}\nTime: {2:0.0} s\nValue: {4:0.00} " + meta.Unit,
            };

            PlotModel.Series.Add(series);
            _seriesMap[id] = series;

            LegendItems.Add(new LegendItem(meta.Label.Trim(), cfg.Color, cfg.ShowInGraph));
        }

        // Restore existing points
        foreach (var (id, points) in saved)
            if (_seriesMap.TryGetValue(id, out var s))
                foreach (var pt in points)
                    s.Points.Add(pt);

        PlotModel.InvalidatePlot(false);
    }

    // ─── Connect / Disconnect ─────────────────────────────────────────────
    public async Task ConnectAsync()
    {
        StatusText = $"Connecting to {_settings.ModbusHost}:{_settings.ModbusPort}...";
        try
        {
            await Task.Run(() => _modbus.Connect(
                _settings.ModbusHost,
                _settings.ModbusPort,
                _settings.UnitId,
                _settings.ConnectionTimeoutMs));

            IsConnected = true;
            StatusText = $"Connected  |  {_settings.ModbusHost}:{_settings.ModbusPort}  |  Unit: {_settings.UnitId}";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = $"Connection failed: {ex.Message}";
        }
    }

    public void Disconnect()
    {
        StopRecording();
        _modbus.Disconnect();
        IsConnected = false;
        StatusText = "Disconnected";
    }

    public void DisconnectModbus()
    {
        _modbus.Disconnect();
        IsConnected = false;
    }

    // ─── Start / Stop ─────────────────────────────────────────────────────
    public async Task<bool> StartAsync(string csvPath)
    {
        if (!IsConnected)
        {
            await ConnectAsync();
            if (!IsConnected) return false;
        }

        // สร้าง CSV พร้อม header
        try
        {
            _csvPath   = csvPath;
            _csvWriter = new StreamWriter(csvPath, append: false, System.Text.Encoding.UTF8);
            var ids = Enum.GetValues<LftChannelId>();
            var header = "DateTime,Time(s)," + string.Join(",",
                ids.Select(id => $"{id}({_channelMeta[id].Unit})"));
            _csvWriter.WriteLine(header);
            _csvWriter.Flush();
        }
        catch (Exception ex)
        {
            StatusText = $"CSV error: {ex.Message}";
            return false;
        }

        _elapsedSeconds = 0;
        UpdateTimeAxis();

        foreach (var s in _seriesMap.Values) s.Points.Clear();
        foreach (var st in _state.Values) st.Reset();

        MeltPressure = "—";
        MeltTemperature = "—";

        _isPaused = false;
        _recordingComplete = false;
        IsRunning = true;
        HasData   = true;
        _timer.Start();
        StatusText = "Recording...";
        NotifyLeftPanel();
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(RecordingStatusText));
        return true;
    }

    public void StopRecording()
    {
        _timer.Stop();
        _isPaused = false;
        IsRunning = false;

        _csvWriter?.Flush();
        _csvWriter?.Dispose();
        _csvWriter = null;

        PrependMetadataToCsv();

        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(RecordingStatusText));

        StatusText = IsConnected
            ? $"Stopped  |  {_settings.ModbusHost}:{_settings.ModbusPort}"
            : "Stopped";
    }

    private void PrependMetadataToCsv()
    {
        if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath)) return;
        try
        {
            var data = File.ReadAllLines(_csvPath, System.Text.Encoding.UTF8);
            var meta = new[]
            {
                $"# SampleId: {SampleId}",
                $"# BasePolymer: {BasePolymer}",
                $"# Customer: {Customer}",
                $"# Date: {Date}",
                $"# Operator: {Operator}",
                $"# FilterScreenSize: {FilterScreenSize}",
                $"# FeedRate: {FeedRate}",
                $"# PressureGearPumpInlet: {PressureGearPumpInlet}",
                $"# FiltertestUnitTemp: {FiltertestUnitTemp}",
                $"# PigmentWeight: {PigmentWeight}",
                $"# TotalWeight: {TotalWeight}",
                "# ---",
                $"# P2_PStart: {P2_PStart}",
                $"# P2_PMax: {P2_PMax}",
                $"# P2_DeltaP: {P2_DeltaP}",
                $"# P2_FPV: {P2_FPV}",
                $"# P1_PStart: {P1_PStart}",
                $"# P1_PEnd: {P1_PEnd}",
                $"# V1_Speed: {V1_Speed}",
                $"# I1_Amp: {I1_Amp}",
                $"# T2_TStart: {T2_TStart}",
                $"# T2_TEnd: {T2_TEnd}",
                $"# T2_TMax: {T2_TMax}",
                $"# T2_TAvg: {T2_TAvg}",
                $"# T1_TStart: {T1_TStart}",
                $"# T1_TEnd: {T1_TEnd}",
                $"# V2_Speed: {V2_Speed}",
                $"# I2_Amp: {I2_Amp}",
            };
            File.WriteAllLines(_csvPath, meta.Concat(data), System.Text.Encoding.UTF8);
        }
        catch { }
    }

    public void PauseRecording()
    {
        if (!IsRunning || _isPaused) return;
        _timer.Stop();
        _isPaused = true;
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(RecordingStatusText));
        StatusText = "Paused";
    }

    public async Task ResumeRecording()
    {
        if (!IsRunning || !_isPaused) return;
        if (!IsConnected)
        {
            await ConnectAsync();
            if (!IsConnected)
            {
                ShowAlert?.Invoke("Unable to connect to PLC.\nPlease check your Modbus connection and try again.");
                return;
            }
        }
        _isPaused = false;
        _timer.Start();
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(RecordingStatusText));
        StatusText = "Recording...";
    }

    public void ClearGraph()
    {
        _elapsedSeconds = 0;
        HasData = false;
        _isViewingCsv = false;
        EnableStart();
        UpdateTimeAxis();
        foreach (var s in _seriesMap.Values) s.Points.Clear();
        foreach (var st in _state.Values) st.Reset();
        MeltPressure = "—";
        MeltTemperature = "—";
        PlotModel.InvalidatePlot(true);
        NotifyLeftPanel();
    }


    // ─── Load CSV ─────────────────────────────────────────────────────────
    public void LoadFromCsv(string path)
    {
        StopRecording();

        foreach (var s in _seriesMap.Values) s.Points.Clear();
        foreach (var st in _state.Values) st.Reset();
        _elapsedSeconds = 0;

        var lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
        if (lines.Length < 2) { StatusText = "CSV: no data"; return; }

        // ข้ามบรรทัด metadata (#) และหา header row
        var dataLines = lines.Where(l => !l.StartsWith("#")).ToArray();
        if (dataLines.Length < 2) { StatusText = "CSV: no data"; return; }

        // โหลด metadata ลงใน form fields
        foreach (var ml in lines.Where(l => l.StartsWith("#")))
        {
            var kv = ml.TrimStart('#').Trim().Split(':', 2);
            if (kv.Length < 2) continue;
            var val = kv[1].Trim();
            switch (kv[0].Trim())
            {
                case "SampleId":              SampleId              = val; break;
                case "BasePolymer":           BasePolymer           = val; break;
                case "Customer":              Customer              = val; break;
                case "Date":                  Date                  = val; break;
                case "Operator":              Operator              = val; break;
                case "FilterScreenSize":      FilterScreenSize      = val; break;
                case "FeedRate":              FeedRate              = val; break;
                case "PressureGearPumpInlet": PressureGearPumpInlet = val; break;
                case "FiltertestUnitTemp":    FiltertestUnitTemp    = val; break;
                case "PigmentWeight":         PigmentWeight         = val; break;
                case "TotalWeight":           TotalWeight           = val; break;
            }
        }

        // Map column index → LftChannelId
        var headers = dataLines[0].Split(',');
        var colMap = new Dictionary<int, LftChannelId>();
        foreach (var id in Enum.GetValues<LftChannelId>())
            for (int i = 2; i < headers.Length; i++)
                if (headers[i].StartsWith(id.ToString(), StringComparison.OrdinalIgnoreCase))
                { colMap[i] = id; break; }

        // Parse rows
        int loaded = 0;
        foreach (var line in dataLines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double t)) continue;

            _elapsedSeconds = Math.Max(_elapsedSeconds, t);

            foreach (var (col, id) in colMap)
            {
                if (col >= parts.Length) continue;
                if (!double.TryParse(parts[col], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double val)) continue;
                if (_seriesMap.TryGetValue(id, out var series))
                    series.Points.Add(new DataPoint(t, val));
                if (_state.TryGetValue(id, out var st))
                    st.Record(val);
            }
            loaded++;
        }

        UpdateTimeAxis();
        PlotModel.InvalidatePlot(true);
        NotifyLeftPanel();
        HasData = loaded > 0;
        _isViewingCsv = true;
        OnPropertyChanged(nameof(CanStart));
        StatusText = $"Loaded: {Path.GetFileName(path)}  ({loaded:N0} rows)  |  Click New to start a new session";
    }

    public void RenameCsvTo(string newName)
    {
        if (string.IsNullOrEmpty(_csvPath)) return;
        var dir     = Path.GetDirectoryName(_csvPath) ?? "";
        var newPath = Path.Combine(dir, newName + ".csv");
        if (string.Equals(newPath, _csvPath, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            // ถ้ากำลัง recording อยู่ → ปิด writer ก่อน แล้วเปิดใหม่หลัง rename
            bool wasRecording = _csvWriter != null;
            _csvWriter?.Flush();
            _csvWriter?.Dispose();
            _csvWriter = null;

            File.Move(_csvPath, newPath, overwrite: true);
            _csvPath = newPath;

            if (wasRecording)
                _csvWriter = new StreamWriter(_csvPath, append: true, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) { StatusText = $"Rename failed: {ex.Message}"; }
    }

    // ─── Crosshair ────────────────────────────────────────────────────────
    public void MoveCrosshair(double screenX, double screenY, double viewWidth, double viewHeight)
    {
        double dataX = _timeAxis.InverseTransform(screenX);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"t = {dataX:F1} s");
        foreach (var (id, series) in _seriesMap)
        {
            if (series.Points.Count == 0 || series.LineStyle == LineStyle.None) continue;
            double y = InterpolateY(series.Points, dataX);
            var meta = _channelMeta[id];
            sb.AppendLine($"{meta.Label.Trim(),-26} {y,8:F2}  {meta.Unit}");
        }

        TrackerText    = sb.ToString().TrimEnd();
        TrackerX       = screenX + 18 > viewWidth  - 220 ? screenX - 235 : screenX + 18;
        TrackerY       = screenY + 10 > viewHeight - 180 ? screenY - 180  : screenY + 10;
        TrackerVisible = true;
    }

    public void HideCrosshair()
    {
        TrackerVisible = false;
    }

    private static double InterpolateY(IList<DataPoint> pts, double x)
    {
        if (pts.Count == 0) return 0;
        if (x <= pts[0].X) return pts[0].Y;
        if (x >= pts[pts.Count - 1].X) return pts[pts.Count - 1].Y;
        int lo = 0, hi = pts.Count - 1;
        while (lo < hi - 1) { int mid = (lo + hi) / 2; if (pts[mid].X < x) lo = mid; else hi = mid; }
        double t = (x - pts[lo].X) / (pts[hi].X - pts[lo].X);
        return pts[lo].Y + t * (pts[hi].Y - pts[lo].Y);
    }

    // ─── Timer tick ───────────────────────────────────────────────────────
    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isReading) return;
        _isReading = true;
        try
        {
            _elapsedSeconds += _settings.PollingIntervalMs / 1000.0;

            var readings = await Task.Run(() => _modbus.ReadAll(_settings.Channels));

            int errorCount = 0;
            foreach (var (id, (value, hasError)) in readings)
            {
                var st = _state[id];
                if (hasError) { st.HasError = true; errorCount++; continue; }
                st.Record(value);
                if (_seriesMap.TryGetValue(id, out var series) && series.LineStyle != LineStyle.None)
                    series.Points.Add(new DataPoint(_elapsedSeconds, value));
            }

            if (errorCount == readings.Count)
            {
                IsConnected = false;
                PauseRecording();
                ShowAlert?.Invoke("PLC disconnected.\nAll channels failed to read.\nRecording has been paused.\n\nClick Play to retry connection.");
                return;
            }
            else if (errorCount > 0)
                StatusText = $"Recording...  ⚠ {errorCount} channel(s) read error";
            else
                StatusText = "Recording...";

            UpdateTimeAxis();

            // เขียน CSV ทุก tick
            if (_csvWriter != null)
            {
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var cols = Enum.GetValues<LftChannelId>().Select(id =>
                {
                    if (!readings.TryGetValue(id, out var r) || r.HasError) return "";
                    return r.Value.ToString("F4");
                });
                _csvWriter.WriteLine($"{now},{_elapsedSeconds:F1},{string.Join(",", cols)}");
                _csvWriter.Flush();
            }

            PlotModel.InvalidatePlot(false);
            NotifyLeftPanel();

            // Update real-time bottom-form and melt values
            MeltPressure          = _calc.MeltPressure;
            MeltTemperature       = _calc.MeltTemperature;
            FeedRate              = _calc.FeedRate;
            PressureGearPumpInlet = _calc.PressureGearPumpInlet;
            FiltertestUnitTemp    = _calc.FiltertestUnitTemp;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            PauseRecording();
            ShowAlert?.Invoke($"Modbus connection lost.\n{ex.Message}\n\nRecording has been paused.\nClick Play to retry connection.");
        }
        finally
        {
            _isReading = false;
        }
    }

    // ─── Settings ─────────────────────────────────────────────────────────
    public void ApplySettings(AppSettings newSettings)
    {
        bool wasRunning  = IsRunning;
        bool wasPaused   = _isPaused;
        string savedPath = _csvPath;

        if (wasRunning) StopRecording();

        _settings = newSettings;
        _channelMeta = BuildChannelMeta();
        SettingsService.Save(_settings);

        _timer.Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs);

        _leftAxis.Title = $"Pressure ({_settings.PressureUnit}) Amperc (%)";
        _leftAxis.Minimum = _settings.PressureYMin;
        _leftAxis.Maximum = _settings.PressureYMax;
        _leftAxis.MajorStep = (_settings.PressureYMax - _settings.PressureYMin) / _settings.PressureAxisDivisions;

        _rightAxis.Title = $"Temperature ({_settings.TemperatureUnit}) / Speed ({_settings.SpeedUnit})";
        _rightAxis.Minimum = _settings.TempSpeedYMin;
        _rightAxis.Maximum = _settings.TempSpeedYMax;
        _rightAxis.MajorStep = (_settings.TempSpeedYMax - _settings.TempSpeedYMin) / _settings.TempSpeedAxisDivisions;

        RebuildSeries();
        NotifyUnitLabels();

        // Resume recording ถ้ากำลัง run อยู่ก่อน apply
        if (wasRunning && !wasPaused && !string.IsNullOrEmpty(savedPath))
        {
            try
            {
                _csvPath   = savedPath;
                _csvWriter = new StreamWriter(savedPath, append: true, System.Text.Encoding.UTF8);
            }
            catch { }

            _isPaused = false;
            IsRunning = true;
            _timer.Start();
            StatusText = "Recording...";
            OnPropertyChanged(nameof(CanPause));
            OnPropertyChanged(nameof(CanResume));
            OnPropertyChanged(nameof(RecordingStatusText));
        }
    }

    // ─── Export CSV ───────────────────────────────────────────────────────
    public void ExportCsv(string filePath)
    {
        var lines = new List<string>();
        var ids = Enum.GetValues<LftChannelId>().ToList();

        lines.Add("Time(s)," + string.Join(",", ids.Select(id => $"{_channelMeta[id].Label.Trim()}({_channelMeta[id].Unit})")));

        int maxPts = _seriesMap.Values.Max(s => s.Points.Count);
        for (int i = 0; i < maxPts; i++)
        {
            double? time = null;
            var parts = new List<string>();
            foreach (var id in ids)
            {
                var pts = _seriesMap[id].Points;
                if (i < pts.Count)
                {
                    time ??= pts[i].X;
                    parts.Add(pts[i].Y.ToString("F4"));
                }
                else parts.Add("");
            }
            lines.Add($"{time:F2},{string.Join(",", parts)}");
        }

        System.IO.File.WriteAllLines(filePath, lines);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────
    private void NotifyIfChanged(string name, string value)
    {
        if (_lastNotified.TryGetValue(name, out var last) && last == value) return;
        _lastNotified[name] = value;
        OnPropertyChanged(name);
    }

    private void NotifyLeftPanel()
    {
        NotifyIfChanged(nameof(P2_PStart), P2_PStart); NotifyIfChanged(nameof(P2_PMax),   P2_PMax);
        NotifyIfChanged(nameof(P2_DeltaP), P2_DeltaP); NotifyIfChanged(nameof(P2_FPV),    P2_FPV);
        NotifyIfChanged(nameof(P1_PStart), P1_PStart); NotifyIfChanged(nameof(P1_PEnd),   P1_PEnd);
        NotifyIfChanged(nameof(V1_Speed),  V1_Speed);  NotifyIfChanged(nameof(I1_Amp),    I1_Amp);
        NotifyIfChanged(nameof(T2_TStart), T2_TStart); NotifyIfChanged(nameof(T2_TEnd),   T2_TEnd);
        NotifyIfChanged(nameof(T2_TMax),   T2_TMax);   NotifyIfChanged(nameof(T2_TAvg),   T2_TAvg);
        NotifyIfChanged(nameof(T1_TStart), T1_TStart); NotifyIfChanged(nameof(T1_TEnd),   T1_TEnd);
        NotifyIfChanged(nameof(V2_Speed),  V2_Speed);  NotifyIfChanged(nameof(I2_Amp),    I2_Amp);
    }

private void UpdateTimeAxis()
    {
        _timeAxis.Minimum = 0;
        _timeAxis.Maximum = Math.Max(_elapsedSeconds * 1.02, 60);

        // MajorStep adapts to elapsed time
        _timeAxis.MajorStep = _elapsedSeconds switch
        {
            < 120    => 10,
            < 600    => 30,
            < 1800   => 60,
            < 7200   => 300,
            < 21600  => 600,
            < 86400  => 3600,
            _        => 7200
        };

        // Label format adapts: s → mm:ss → hh:mm:ss
        _timeAxis.LabelFormatter = val =>
        {
            if (val < 0) return "";
            var ts = TimeSpan.FromSeconds(val);
            if (_elapsedSeconds >= 3600)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            if (_elapsedSeconds >= 60)
                return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
            return $"{(int)val}s";
        };
    }

    public void Dispose()
    {
        _timer.Stop();
        _modbus.Dispose();
    }
}

public record LegendItem(string Label, string ColorHex, bool Visible);
