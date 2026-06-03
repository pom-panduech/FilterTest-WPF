using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ModbusChart.Models;

namespace ModbusChart.Views;

public partial class SettingsWindow : Window
{
    public AppSettings? ResultSettings { get; private set; }
    private readonly AppSettings _original;
    private readonly Dictionary<LftChannelId, ChannelRow> _rows = new();

    // Column widths: CH | Description | Address | FC | DataType | Scale | Offset | Color | Show
    private static readonly double[] ColW = { 52, 155, 80, 175, 128, 72, 72, 60, 44 };

    public SettingsWindow(AppSettings original)
    {
        InitializeComponent();
        _original = original;

        LoadConnectionTab(original);
        LoadAxisScaleTab(original);
        LoadUnitTab(original);
        BuildChannelRows(original);

    }

    // ─── Tab 1 ───────────────────────────────────────────────────────────────
    private void LoadConnectionTab(AppSettings s)
    {
        TbHost.Text         = s.ModbusHost;
        TbPort.Text         = s.ModbusPort.ToString();
        TbUnitId.Text       = s.UnitId.ToString();
        TbTimeout.Text      = s.ConnectionTimeoutMs.ToString();
        TbPollInterval.Text = s.PollingIntervalMs.ToString();
    }

    // ─── Tab 3 ───────────────────────────────────────────────────────────────
    private void LoadAxisScaleTab(AppSettings s)
    {
        TbPresMax.Text = s.PressureYMax.ToString("F0");
        TbTsMax.Text   = s.TempSpeedYMax.ToString("F0");

        var darkFg = new SolidColorBrush(Color.FromRgb(0x1A, 0x2B, 0x3C));
        for (int i = 5; i <= 20; i++)
        {
            CbPresDivisions.Items.Add(new ComboBoxItem { Content = i.ToString(), Foreground = darkFg, Background = Brushes.White, FontSize = 11, Padding = new Thickness(8, 5, 8, 5) });
            CbTsDivisions.Items.Add(new ComboBoxItem   { Content = i.ToString(), Foreground = darkFg, Background = Brushes.White, FontSize = 11, Padding = new Thickness(8, 5, 8, 5) });
        }
        int presIdx = s.PressureAxisDivisions - 5;
        int tsIdx   = s.TempSpeedAxisDivisions - 5;
        CbPresDivisions.SelectedIndex = presIdx is >= 0 and <= 15 ? presIdx : 5;
        CbTsDivisions.SelectedIndex   = tsIdx   is >= 0 and <= 15 ? tsIdx   : 5;
    }

    // ─── Tab 4 ───────────────────────────────────────────────────────────────
    private void LoadUnitTab(AppSettings s)
    {
        RbBar.IsChecked        = s.PressureUnit    == "bar";
        RbPsi.IsChecked        = s.PressureUnit    == "psi";
        RbCelsius.IsChecked    = s.TemperatureUnit == "°C";
        RbFahrenheit.IsChecked = s.TemperatureUnit == "°F";
        RbRpm.IsChecked        = s.SpeedUnit       == "RPM";
        RbCcMin.IsChecked      = s.SpeedUnit       == "cc/min";
    }

    // ─── Tab 2: Channels ─────────────────────────────────────────────────────
    private void BuildChannelRows(AppSettings settings)
    {
        // Header
        var hdr = MakeGrid();
        hdr.Margin = new Thickness(0, 7, 0, 7);
        string[] labels = { "CH", "Description", "Address", "Function Code", "Data Type", "Scale", "Offset", "Color", "Show" };
        for (int i = 0; i < labels.Length; i++)
        {
            var tb = new TextBlock
            {
                Text       = labels[i],
                Foreground = Brushes.White,
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = i == 8 ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            };
            Grid.SetColumn(tb, i);
            hdr.Children.Add(tb);
        }
        ChHeaderBorder.Child = hdr;

        // Rows
        ChannelsPanel.Children.Clear();
        _rows.Clear();
        bool alt = false;
        foreach (var id in Enum.GetValues<LftChannelId>())
        {
            var cfg = settings.GetChannel(id);
            var row = new ChannelRow(id, cfg);
            _rows[id] = row;

            var wrap = new Border
            {
                Child           = row.RowGrid,
                Background      = new SolidColorBrush(Color.FromRgb(0x0A, 0x16, 0x28)),
                Padding         = new Thickness(0, 4, 0, 4),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            ChannelsPanel.Children.Add(wrap);
            alt = !alt;
        }
    }

    internal static Grid MakeGrid()
    {
        var g = new Grid();
        foreach (var w in ColW)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
        return g;
    }

    // ─── OK / Cancel ─────────────────────────────────────────────────────────
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseSettings(out var s)) return;
        ResultSettings = s;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private async void TestConnect_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TbPort.Text, out var port)) { TbTestStatus.Text = "Invalid port"; TbTestStatus.Foreground = Brushes.OrangeRed; return; }
        if (!int.TryParse(TbUnitId.Text, out var uid))  { TbTestStatus.Text = "Invalid Slave ID"; TbTestStatus.Foreground = Brushes.OrangeRed; return; }
        if (!int.TryParse(TbTimeout.Text, out var to))  { TbTestStatus.Text = "Invalid timeout"; TbTestStatus.Foreground = Brushes.OrangeRed; return; }

        BtnTestConnect.IsEnabled = false;
        TbTestStatus.Text = "Connecting...";
        TbTestStatus.Foreground = Brushes.White;

        var host = TbHost.Text.Trim();
        var svc = new ModbusChart.Services.ModbusService();
        try
        {
            await Task.Run(() => svc.Connect(host, port, uid, to));
            TbTestStatus.Text = $"Connected  ✓  ({host}:{port})";
            TbTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }
        catch (Exception ex)
        {
            TbTestStatus.Text = $"Failed  ✗  {ex.Message}";
            TbTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        }
        finally
        {
            svc.Disconnect();
            svc.Dispose();
            BtnTestConnect.IsEnabled = true;
        }
    }

    private bool TryParseSettings(out AppSettings settings)
    {
        settings = null!;

        if (!int.TryParse(TbPort.Text, out var port) || port < 1 || port > 65535)
        { Err("Port must be 1–65535."); return false; }
        if (!int.TryParse(TbUnitId.Text, out var uid) || uid < 0 || uid > 247)
        { Err("Unit ID must be 0–247."); return false; }
        if (!int.TryParse(TbTimeout.Text, out var to) || to < 100)
        { Err("Timeout must be ≥ 100 ms."); return false; }
        if (!int.TryParse(TbPollInterval.Text, out var poll) || poll < 100)
        { Err("Poll interval must be ≥ 100 ms."); return false; }
if (!double.TryParse(TbPresMax.Text, out var presMax) || presMax <= 0)
        { Err("Pressure Max must be greater than 0."); return false; }
        if (!double.TryParse(TbTsMax.Text, out var tsMax) || tsMax <= 0)
        { Err("Temp/Speed Max must be greater than 0."); return false; }
        const double presMin = 0;
        const double tsMin   = 0;

        // Validate channel rows
        var channels = new List<ChannelConfig>();
        foreach (var (id, row) in _rows)
        {
            if (!row.TryGetConfig(out var cfg, out var errMsg))
            { Err($"Channel {id}: {errMsg}"); return false; }
            channels.Add(cfg);
        }

        settings = new AppSettings
        {
            ModbusHost             = TbHost.Text.Trim(),
            ModbusPort             = port,
            UnitId                 = uid,
            ConnectionTimeoutMs    = to,
            PollingIntervalMs      = poll,
PressureYMin           = presMin,
            PressureYMax           = presMax,
            PressureAxisDivisions  = CbPresDivisions.SelectedIndex >= 0 ? CbPresDivisions.SelectedIndex + 5 : 10,
            TempSpeedYMin          = tsMin,
            TempSpeedYMax          = tsMax,
            TempSpeedAxisDivisions = CbTsDivisions.SelectedIndex >= 0 ? CbTsDivisions.SelectedIndex + 5 : 10,
            PressureUnit           = RbPsi.IsChecked == true ? "psi" : "bar",
            TemperatureUnit        = RbFahrenheit.IsChecked == true ? "°F" : "°C",
            SpeedUnit              = RbCcMin.IsChecked == true ? "cc/min" : "RPM",
            Channels               = channels,
        };
        return true;
    }

    private void Err(string msg)
        => MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
}

// ─── ChannelRow ───────────────────────────────────────────────────────────────
internal class ChannelRow
{
    // 40 preset colors (5 rows × 8 cols)
    private static readonly string[] Palette =
    {
        "#F44336","#E91E63","#9C27B0","#673AB7","#3F51B5","#2196F3","#03A9F4","#00BCD4",
        "#009688","#4CAF50","#8BC34A","#CDDC39","#FFEB3B","#FFC107","#FF9800","#FF5722",
        "#795548","#9E9E9E","#607D8B","#37474F","#0D47A1","#1A237E","#006064","#1B5E20",
        "#FFCDD2","#F8BBD0","#E1BEE7","#C5CAE9","#BBDEFB","#B2EBF2","#B2DFDB","#DCEDC8",
        "#000000","#263238","#546E7A","#B0BEC5","#ECEFF1","#FFFFFF","#1565C0","#C62828",
    };

    public  Grid     RowGrid    { get; }
    private TextBox  _tbAddr;
    private ComboBox _cbFc;
    private ComboBox _cbDt;
    private TextBox  _tbScale;
    private TextBox  _tbOffset;
    private CheckBox _chkShow;
    private Border   _colorBtn;
    private string   _color;
    private readonly LftChannelId _id;

    private static readonly Dictionary<LftChannelId, string> _descriptions = new()
    {
        { LftChannelId.P1, "Pressure Inlet" },
        { LftChannelId.P2, "Pressure Screen" },
        { LftChannelId.T1, "Temperature Inlet" },
        { LftChannelId.T2, "Temperature Screen" },
        { LftChannelId.V1, "Speed Extruder" },
        { LftChannelId.V2, "Speed Gear Pump" },
        { LftChannelId.I1, "% Ampere Extruder" },
        { LftChannelId.I2, "% Ampere Gear Pump" },
    };

    public ChannelRow(LftChannelId id, ChannelConfig cfg)
    {
        _id    = id;
        _color = cfg.Color;

        RowGrid = SettingsWindow.MakeGrid();

        // Col 0 – Channel label
        var chLabel = new TextBlock
        {
            Text      = id.ToString(),
            Foreground = Brushes.White,
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(4, 0, 0, 0),
        };
        Add(chLabel, 0);

        // Col 1 – Description
        var desc = new TextBlock
        {
            Text       = _descriptions.TryGetValue(id, out var d) ? d : "",
            Foreground = Brushes.White,
            FontSize   = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(4, 0, 8, 0),
        };
        Add(desc, 1);

        // Col 2 – Address
        _tbAddr = Box(cfg.Address.ToString(), 8);
        Add(_tbAddr, 2);

        // Col 3 – Function Code
        _cbFc = Combo(new[] { "FC03 Holding Register", "FC04 Input Register" },
            cfg.FunctionCode == ModbusFunctionCode.InputRegister ? 1 : 0, 8);
        Add(_cbFc, 3);

        // Col 4 – Data Type
        _cbDt = Combo(new[] { "Int16 Unsigned", "Int16 Signed", "Float32" },
            cfg.DataType switch { ModbusDataType.Int16Signed => 1, ModbusDataType.Float32 => 2, _ => 0 }, 8);
        Add(_cbDt, 4);

        // Col 5 – Scale
        _tbScale = Box(cfg.ScaleFactor.ToString("G"), 8);
        Add(_tbScale, 5);

        // Col 6 – Offset
        _tbOffset = Box(cfg.Offset.ToString("G"), 8);
        Add(_tbOffset, 6);

        // Col 7 – Color button
        _colorBtn = new Border
        {
            Width = 40, Height = 24,
            Background      = HexBrush(cfg.Color),
            CornerRadius    = new CornerRadius(5),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Click to choose color",
        };
        _colorBtn.MouseLeftButtonUp += OpenColorPicker;
        Add(_colorBtn, 7);

        // Col 8 – Show
        _chkShow = new CheckBox
        {
            IsChecked = cfg.ShowInGraph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        Add(_chkShow, 8);
    }

    // ── Validation + read ────────────────────────────────────────────────────
    public bool TryGetConfig(out ChannelConfig cfg, out string errMsg)
    {
        cfg    = null!;
        errMsg = "";

        if (!int.TryParse(_tbAddr.Text.Trim(), out var addr) || addr < 0 || addr > 65535)
        { errMsg = "Address must be a number (0–65535)."; return false; }

        if (!double.TryParse(_tbScale.Text.Trim(), out var scale))
        { errMsg = "Scale must be a number."; return false; }

        if (!double.TryParse(_tbOffset.Text.Trim(), out var offset))
        { errMsg = "Offset must be a number."; return false; }

        cfg = new ChannelConfig
        {
            Id           = _id,
            Address      = addr,
            FunctionCode = _cbFc.SelectedIndex == 1 ? ModbusFunctionCode.InputRegister : ModbusFunctionCode.HoldingRegister,
            DataType     = _cbDt.SelectedIndex switch { 1 => ModbusDataType.Int16Signed, 2 => ModbusDataType.Float32, _ => ModbusDataType.Int16Unsigned },
            ScaleFactor  = scale,
            Offset       = offset,
            Color        = _color,
            ShowInGraph  = _chkShow.IsChecked == true,
        };
        return true;
    }

    // ── Color picker popup ───────────────────────────────────────────────────
    private void OpenColorPicker(object sender, MouseButtonEventArgs e)
    {
        var popup = new Popup
        {
            PlacementTarget    = _colorBtn,
            Placement          = PlacementMode.Bottom,
            StaysOpen          = false,
            AllowsTransparency = true,
        };

        var root = new StackPanel { Width = 228 };

        // Palette grid (5 rows × 8 cols)
        var swatchGrid = new UniformGrid { Columns = 8, Margin = new Thickness(6, 6, 6, 4) };
        foreach (var hex in Palette)
        {
            var sw = new Border
            {
                Width = 22, Height = 18, Margin = new Thickness(2),
                Background      = HexBrush(hex),
                CornerRadius    = new CornerRadius(3),
                Cursor          = Cursors.Hand,
                BorderBrush     = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                ToolTip         = hex,
            };
            var capturedHex = hex;
            sw.MouseLeftButtonUp += (s2, e2) =>
            {
                ApplyColor(capturedHex);
                popup.IsOpen = false;
                e2.Handled   = true;
            };
            sw.MouseEnter += (s2, e2) => ((Border)s2).BorderBrush = Brushes.Black;
            sw.MouseLeave += (s2, e2) => ((Border)s2).BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            swatchGrid.Children.Add(sw);
        }
        root.Children.Add(swatchGrid);

        // Separator
        root.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(220, 228, 240)), Margin = new Thickness(6, 0, 6, 0) });

        // Hex input row
        var hexRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 6, 6, 6) };
        var hexLbl = new TextBlock { Text = "Hex:", Foreground = Brushes.Black, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var hexBox = new TextBox
        {
            Text            = _color,
            Width           = 90, Height = 26,
            FontSize        = 11,
            Foreground      = Brushes.Black,
            Background      = Brushes.White,
            BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 195, 220)),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(6, 3, 6, 3),
            VerticalContentAlignment = VerticalAlignment.Center,
            MaxLength       = 9,
        };
        var preview = new Border
        {
            Width = 26, Height = 26,
            Background      = HexBrush(_color),
            CornerRadius    = new CornerRadius(3),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Margin          = new Thickness(6, 0, 6, 0),
        };
        hexBox.TextChanged += (s2, e2) =>
        {
            var txt = hexBox.Text.Trim();
            if (!txt.StartsWith("#")) txt = "#" + txt;
            try { preview.Background = HexBrush(txt); } catch { }
        };
        var applyBtn = new Button
        {
            Content         = "Apply",
            Width           = 52, Height = 26,
            FontSize        = 11,
            Background      = new SolidColorBrush(Color.FromRgb(30, 100, 200)),
            Foreground      = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        applyBtn.Click += (s2, e2) =>
        {
            var txt = hexBox.Text.Trim();
            if (!txt.StartsWith("#")) txt = "#" + txt;
            try { HexBrush(txt); ApplyColor(txt); popup.IsOpen = false; }
            catch { hexBox.BorderBrush = Brushes.Red; }
        };

        hexRow.Children.Add(hexLbl);
        hexRow.Children.Add(hexBox);
        hexRow.Children.Add(preview);
        hexRow.Children.Add(applyBtn);
        root.Children.Add(hexRow);

        popup.Child = new Border
        {
            Child           = root,
            Background      = Brushes.White,
            BorderBrush     = new SolidColorBrush(Color.FromRgb(180, 195, 220)),
            BorderThickness = new Thickness(1),
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
                { BlurRadius = 12, Opacity = 0.2, ShadowDepth = 3 },
        };
        popup.IsOpen = true;
        e.Handled = true;
    }

    private void ApplyColor(string hex)
    {
        _color                = hex;
        _colorBtn.Background  = HexBrush(hex);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private void Add(UIElement el, int col) { Grid.SetColumn(el, col); RowGrid.Children.Add(el); }

    private static TextBox Box(string text, int rightMargin) => new()
    {
        Text                     = text,
        Height                   = 26,
        Margin                   = new Thickness(0, 0, rightMargin, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        Background               = new SolidColorBrush(Color.FromRgb(0xED, 0xF2, 0xF8)),
        Foreground               = new SolidColorBrush(Color.FromRgb(0x1A, 0x2B, 0x3C)),
        BorderBrush              = new SolidColorBrush(Color.FromRgb(0x7A, 0x9E, 0xC0)),
        BorderThickness          = new Thickness(1),
        Padding                  = new Thickness(6, 3, 6, 3),
        FontSize                 = 11,
    };

    private static ComboBox Combo(string[] items, int sel, int rightMargin)
    {
        var cb = new ComboBox
        {
            Height   = 26,
            Margin   = new Thickness(0, 0, rightMargin, 0),
            FontSize = 11,
        };
        foreach (var item in items)
            cb.Items.Add(new ComboBoxItem
            {
                Content    = item,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x2B, 0x3C)),
                Background = Brushes.White,
                FontSize   = 11,
                Padding    = new Thickness(8, 5, 8, 5),
            });
        cb.SelectedIndex = sel;
        return cb;
    }

    internal static SolidColorBrush HexBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(color);
    }
}
