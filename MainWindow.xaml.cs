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
        // ใช้ชื่อจาก File Name field ถ้ามี ไม่งั้น auto-generate
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

        // sync ชื่อไฟล์ (ไม่รวม extension) กลับไปที่ File Name field
        _vm.FileName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

        bool started = await _vm.StartAsync(dlg.FileName);
        if (!started)
            MessageBox.Show(
                "Unable to connect to PLC.\nPlease check IP and Port in Settings.",
                "Connection Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }
    private void Stop_Click(object sender, RoutedEventArgs e)        { _vm.StopRecording(); _vm.Disconnect(); _vm.DisableStart(); }
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _vm.PauseRecording();
        _vm.DisconnectModbus();
    }

    private async void Resume_Click(object sender, RoutedEventArgs e) =>
        await _vm.ResumeRecording();
    private void ClearGraph_Click(object sender, RoutedEventArgs e)  => _vm.ClearGraph();
    private void Exit_Click(object sender, RoutedEventArgs e)        => Close();
    private void New_Click(object sender, RoutedEventArgs e) =>
        Process.Start(Process.GetCurrentProcess().MainModule!.FileName);


    private void OpenCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
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

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export CSV",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = $"LFT_{DateTime.Now:yyyyMMdd_HHmmss}"
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
            // WPF lines — instant, no chart re-render
            CrosshairV.X1 = CrosshairV.X2 = pos.X;
            CrosshairV.Y1 = area.Top;
            CrosshairV.Y2 = area.Bottom;
            CrosshairV.Visibility = Visibility.Visible;

            CrosshairH.Y1 = CrosshairH.Y2 = pos.Y;
            CrosshairH.X1 = area.Left;
            CrosshairH.X2 = area.Right;
            CrosshairH.Visibility = Visibility.Visible;

            // Tooltip content (data lookup only)
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

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_vm.CsvPath))
        {
            MessageBox.Show("No CSV file found. Please click Start to begin recording first.",
                "No File", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var newName = string.IsNullOrWhiteSpace(_vm.FileName)
            ? System.IO.Path.GetFileNameWithoutExtension(_vm.CsvPath)
            : _vm.FileName.Trim();

        var folder  = System.IO.Path.GetDirectoryName(_vm.CsvPath);
        var oldName = System.IO.Path.GetFileNameWithoutExtension(_vm.CsvPath);

        var result = MessageBox.Show(
            $"Do you want to save all data to this file?\n\n" +
            $"  {folder}\\{newName}.csv",
            "Save CSV",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _vm.RenameCsvTo(newName);
            _vm.FileName = newName;
            _vm.Disconnect();
            _vm.DisableStart();
            _csvSaved = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot rename file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        if (!string.IsNullOrEmpty(_vm.CsvPath) && !_csvSaved)
        {
            var result = MessageBox.Show(
                $"Do you want to save all data to this file?\n\n  {_vm.CsvPath}",
                "Save CSV",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            { e.Cancel = true; return; }

            if (result == MessageBoxResult.Yes)
                _vm.StopRecording(); // flush + close CSV writer
            else // No → ลบไฟล์ทิ้ง
            {
                _vm.StopRecording();
                try { System.IO.File.Delete(_vm.CsvPath); } catch { }
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
