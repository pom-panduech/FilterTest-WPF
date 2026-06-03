using System.Windows;

namespace ModbusChart.Views;

public partial class ScaleDialog : Window
{
    public double YMin { get; private set; }
    public double YMax { get; private set; }

    public ScaleDialog(string title, double currentMin, double currentMax)
    {
        InitializeComponent();
        TbTitle.Text = title;
        TbMin.Text = currentMin.ToString("F0");
        TbMax.Text = currentMax.ToString("F0");
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(TbMin.Text, out var min) || !double.TryParse(TbMax.Text, out var max))
        {
            MessageBox.Show("Please enter valid numbers.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (min >= max)
        {
            MessageBox.Show("Min must be less than Max.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        YMin = min;
        YMax = max;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
