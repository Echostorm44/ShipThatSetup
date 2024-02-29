using IWshRuntimeLibrary;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace ShipThatSetup;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public SetupSettings MySettings { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            MySettings = JsonSerializer.Deserialize<SetupSettings>(System.IO.File.ReadAllText(settingsPath));
            lblTitle.Text = MySettings.Title;
            txtInstallPath.Text = Path.Combine(@"C:\", MySettings.DefaultInstallFolderName);
            var eulaPath = Path.Combine(AppContext.BaseDirectory, "EULA.txt");
            using TextReader tr = new StreamReader(eulaPath);
            lblEULA.Text = tr.ReadToEnd();
        }
        catch(Exception ex)
        {
            lblEULA.Text = ex.Message;
        }
    }

    private void CreateShortcut(string exePath, string iconPath, string name)
    {
        try
        {
            object shDesktop = (object)"Desktop";
            WshShell shell = new WshShell();
            string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @$"\{name}.lnk";
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutAddress);
            shortcut.TargetPath = exePath;
            shortcut.IconLocation = iconPath;
            shortcut.Save();
        }
        catch(Exception ex)
        {
            lblEULA.Visibility = Visibility.Visible;
            lblEULA.Text = ex.Message;
        }
    }

    private void DockPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        App.Current.MainWindow.DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void btnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var foo = new OpenFolderDialog();
        if(foo.ShowDialog().Value)
        {
            txtInstallPath.Text = foo.FolderName;
        }
    }

    private async void btnInstall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            btnBrowse.Visibility = Visibility.Collapsed;
            btnInstall.Visibility = Visibility.Collapsed;
            txtInstallPath.Visibility = Visibility.Collapsed;
            pbStatus.Visibility = Visibility.Visible;
            imgBackdrop.Visibility = Visibility.Collapsed;
            svEULA.Visibility = Visibility.Visible;

            lblEULA.Text = "Copying files...\r\n";
            Progress<string> resultUpdate = new Progress<string>();
            resultUpdate.ProgressChanged += (a, b) =>
            {
                lblEULA.Text += b + "\r\n";
                scrollViewerEULA.ScrollToVerticalOffset(scrollViewerEULA.ExtentHeight);
            };
            Progress<double> updateProgress = new Progress<double>();
            updateProgress.ProgressChanged += (a, b) =>
            {
                pbStatus.Value = b;
            };
            IProgress<string> results = resultUpdate;
            IProgress<double> progressBar = updateProgress;
            string installPath = txtInstallPath.Text;

            await Task.Factory.StartNew(() => CopyDirectory(AppContext.BaseDirectory, installPath, results, progressBar));

            results.Report("Creating shortcut...");
            pbStatus.IsIndeterminate = true;
            if(MySettings.UseLauncher)
            {
                CreateShortcut(Path.Combine(txtInstallPath.Text, "launcher.exe"), 
                    Path.Combine(txtInstallPath.Text, MySettings.IconFileName), MySettings.Title);
            }
            else
            {
                CreateShortcut(Path.Combine(txtInstallPath.Text, MySettings.ExeFileName), 
                    Path.Combine(txtInstallPath.Text, MySettings.IconFileName), MySettings.Title);
            }
            pbStatus.IsIndeterminate = false;
            pbStatus.Visibility = Visibility.Collapsed;
            results.Report("Shortcut created, launching for first time.");
            if(MySettings.UseLauncher)
            {
                System.Diagnostics.Process.Start(Path.Combine(txtInstallPath.Text, "launcher.exe"));
            }
            else
            {
                System.Diagnostics.Process.Start(Path.Combine(txtInstallPath.Text, MySettings.ExeFileName));
            }

            Application.Current.Shutdown();
        }
        catch(Exception ex)
        {
            imgBackdrop.Visibility = Visibility.Collapsed;
            lblEULA.Text = ex.ToString();
            svEULA.Visibility = Visibility.Visible;
        }
    }

    static void CopyDirectory(string sourceDirPath, string destinationDirPath,
        IProgress<string> results, IProgress<double> progressBar)
    {
        var sourceDir = new DirectoryInfo(sourceDirPath);

        if(!sourceDir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir.FullName}");
        }

        var subDirs = sourceDir.GetDirectories();

        Directory.CreateDirectory(destinationDirPath);

        var files = sourceDir.GetFiles();
        int counter = 0;
        int total = files.Length;
        foreach(FileInfo file in files)
        {
            string targetFilePath = Path.Combine(destinationDirPath, file.Name);
            results.Report($"Copying {targetFilePath}");
            file.CopyTo(targetFilePath, true);
            progressBar.Report((double)counter / (double)total);
            counter++;
            Task.Delay(10).Wait();
        }
        progressBar.Report(1);

        foreach(DirectoryInfo subDir in subDirs)
        {
            string newDestinationDir = Path.Combine(destinationDirPath, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, results, progressBar);
        }
    }

    private void btnAccept_Click(object sender, RoutedEventArgs e)
    {
        imgBackdrop.Visibility = Visibility.Visible;
        txtInstallPath.Visibility = Visibility.Visible;
        btnBrowse.Visibility = Visibility.Visible;
        btnInstall.Visibility = Visibility.Visible;
        btnAccept.Visibility = Visibility.Collapsed;
        svEULA.Visibility = Visibility.Collapsed;
    }
}

public class SetupSettings
{
    public string Title { get; set; }
    public string DefaultInstallFolderName { get; set; }
    public string ExeFileName { get; set; }
    public string IconFileName { get; set; }
    public bool UseLauncher { get; set; }
    public string GithubOwner { get; set; }
    public string GithubRepo { get; set; }
    public string ZipName { get; set; }
}