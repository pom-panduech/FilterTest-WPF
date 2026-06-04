using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ModbusChart.ViewModels;
using ModbusChart.Views;

namespace ModbusChart;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _csvSaved;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        Closed += (s, e) => _vm.Dispose();
        _vm.ShowAlert += msg => MessageBox.Show(this, msg, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        _csvSaved = false;
        bool started = await _vm.StartAsync();
        if (!started)
            MessageBox.Show(
                "Unable to connect to PLC.\nPlease check IP and Port in Settings.",
                "Connection Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _vm.StopRecording();
        _vm.Disconnect();
        _vm.DisableStart();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _vm.PauseRecording();
        _vm.DisconnectModbus();
    }

    private async void Resume_Click(object sender, RoutedEventArgs e) =>
        await _vm.ResumeRecording();

    private void ClearGraph_Click(object sender, RoutedEventArgs e) => _vm.ClearGraph();
    private void Exit_Click(object sender, RoutedEventArgs e)        => Close();
    private void New_Click(object sender, RoutedEventArgs e) =>
        Process.Start(Environment.ProcessPath!);

    private void OpenCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title      = "Open CSV Log File",
            Filter     = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
        };
        if (dlg.ShowDialog() != true) return;
        try { _vm.LoadFromCsv(dlg.FileName); }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasData)
        {
            MessageBox.Show("No data to save. Please start recording first.",
                "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var suggested = string.IsNullOrWhiteSpace(_vm.FileName)
            ? $"LFT_{DateTime.Now:yyyyMMdd_HHmmss}"
            : _vm.FileName.Trim();

        var dlg = new SaveFileDialog
        {
            Title      = "Save CSV Log File",
            Filter     = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName   = suggested,
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _vm.SaveToCsv(dlg.FileName);
            _vm.FileName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            _csvSaved = true;
            MessageBox.Show($"Saved to:\n{dlg.FileName}", "Save Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var suggested = string.IsNullOrWhiteSpace(_vm.FileName)
            ? $"LFT_{DateTime.Now:yyyyMMdd_HHmmss}"
            : _vm.FileName.Trim();

        var dlg = new SaveFileDialog
        {
            Title      = "Export CSV",
            Filter     = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName   = suggested,
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _vm.ExportCsv(dlg.FileName);
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PlotView_MouseMove(object sender, MouseEventArgs e)
    {
        var pos  = e.GetPosition(PlotView);
        var area = _vm.PlotArea;

        if (area.Width > 0 &&
            pos.X >= area.Left && pos.X <= area.Right &&
            pos.Y >= area.Top  && pos.Y <= area.Bottom)
        {
            CrosshairV.X1 = CrosshairV.X2 = pos.X;
            CrosshairV.Y1 = area.Top;
            CrosshairV.Y2 = area.Bottom;
            CrosshairV.Visibility = Visibility.Visible;

            CrosshairH.Y1 = CrosshairH.Y2 = pos.Y;
            CrosshairH.X1 = area.Left;
            CrosshairH.X2 = area.Right;
            CrosshairH.Visibility = Visibility.Visible;

            _vm.MoveCrosshair(pos.X, pos.Y, PlotView.ActualWidth, PlotView.ActualHeight);
        }
        else
        {
            CrosshairV.Visibility = Visibility.Collapsed;
            CrosshairH.Visibility = Visibility.Collapsed;
            _vm.HideCrosshair();
        }
    }

    private void PlotView_MouseLeave(object sender, MouseEventArgs e)
    {
        CrosshairV.Visibility = Visibility.Collapsed;
        CrosshairH.Visibility = Visibility.Collapsed;
        _vm.HideCrosshair();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        e.Effects = files?.Any(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        var csv = files?.FirstOrDefault(f => f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        if (csv == null) return;
        try { _vm.LoadFromCsv(csv); }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_vm.HasData && !_csvSaved)
        {
            var result = MessageBox.Show(
                "You have unsaved recording data.\nDo you want to save before closing?",
                "Save CSV",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            { e.Cancel = true; return; }

            if (result == MessageBoxResult.Yes)
            {
                var suggested = string.IsNullOrWhiteSpace(_vm.FileName)
                    ? $"LFT_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : _vm.FileName.Trim();

                var dlg = new SaveFileDialog
                {
                    Title      = "Save CSV Log File",
                    Filter     = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName   = suggested,
                };
                if (dlg.ShowDialog() == true)
                {
                    try { _vm.SaveToCsv(dlg.FileName); }
                    catch { }
                }
            }
        }
        base.OnClosing(e);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_vm.Settings) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.ResultSettings != null)
            _vm.ApplySettings(dlg.ResultSettings);
    }
}
