using System.IO;
using System.Management;
using System.Windows;
using Microsoft.Win32;

namespace WinIsoUsbCreator;

public partial class MainWindow : Window
{
    private sealed class UsbDrive { public int Index { get; init; } public string DisplayName { get; init; } = ""; }
    public MainWindow() { InitializeComponent(); RefreshDrives(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDrives();
    private void RefreshDrives()
    {
        DriveCombo.Items.Clear();
        using var searcher = new ManagementObjectSearcher("SELECT Index, Model, Size FROM Win32_DiskDrive WHERE InterfaceType='USB'");
        foreach (ManagementObject d in searcher.Get())
        {
            long size = Convert.ToInt64(d["Size"] ?? 0L); int index = Convert.ToInt32(d["Index"]);
            string model = Convert.ToString(d["Model"])?.Trim() ?? "USB Drive";
            DriveCombo.Items.Add(new UsbDrive { Index = index, DisplayName = $"Disk {index}: {model} ({size / 1024d / 1024d / 1024d:0.0} GB)" });
        }
        if (DriveCombo.Items.Count > 0) DriveCombo.SelectedIndex = 0;
        StatusText.Text = DriveCombo.Items.Count == 0 ? "No USB drives detected." : "USB drives refreshed.";
    }
    private void BrowseIso_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Windows ISO (*.iso)|*.iso|All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true) IsoPathBox.Text = dlg.FileName;
    }
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (DriveCombo.SelectedItem is not UsbDrive drive) { MessageBox.Show("Select a USB drive."); return; }
        if (!File.Exists(IsoPathBox.Text)) { MessageBox.Show("Select a valid ISO file."); return; }
        if (!ConfirmErase.IsChecked.GetValueOrDefault()) { MessageBox.Show("You must confirm erasing the USB drive."); return; }
        if (MessageBox.Show($"ALL DATA ON DISK {drive.Index} WILL BE ERASED.\n\n{drive.DisplayName}\n\nContinue?", "Final confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        CreateButton.IsEnabled = false;
        try { await Task.Run(() => UsbBuilder.Build(drive.Index, IsoPathBox.Text, Update)); Progress.Value = 100; StatusText.Text = "Completed successfully."; MessageBox.Show("USB creation completed.", "Done"); }
        catch (Exception ex) { StatusText.Text = "Failed: " + ex.Message; MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { CreateButton.IsEnabled = true; }
    }
    private void Update(int percent, string text) => Dispatcher.Invoke(() => { Progress.Value = percent; StatusText.Text = text; });
}
