using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using RoboSharp;
using RoboSharp.EventArgObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MessageBox = System.Windows.MessageBox;

namespace USBBackup
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TaskbarIcon taskbarIcon;
        private USBControl usbHandler;
        private event EventHandler ShutdownEvent;
        private bool CopyExecuting;
        private int RunningCopyOperations;
        private bool BaloonTipDisplayed;
        private static object lockObject;
        private long TotalFilesToCopy;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            WaitForDebugger();
#endif
            lockObject = new object();
            CreateTaskBarIcon();
            CheckIfInstalledAndTriggerInstall();
            CheckUSBMedia();
        }

        private void PerformInstallation()
        {
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationLocation = Application.ResourceAssembly.Location;
            Guid uniqueIdentifyer = Guid.NewGuid();

            if (File.Exists(System.IO.Path.Combine(programDataPath, "USBBackup", Application.ResourceAssembly.GetName().Name + ".exe")))
                File.Delete(System.IO.Path.Combine(programDataPath, "USBBackup", Application.ResourceAssembly.GetName().Name + ".exe"));

            Directory.CreateDirectory(System.IO.Path.Combine(programDataPath, "USBBackup"));
            File.Copy(applicationLocation, System.IO.Path.Combine(programDataPath, "USBBackup", Application.ResourceAssembly.GetName().Name + ".exe"));

            string rootFolder = System.IO.Path.GetPathRoot(System.Reflection.Assembly.GetEntryAssembly().Location);

            RegistryKey softwareRegKey = Registry.CurrentUser.OpenSubKey("Software", true);
            if (!softwareRegKey.GetSubKeyNames().Contains("USBBackup"))
            {
                softwareRegKey.CreateSubKey("USBBackup").SetValue("UniqueIdentifyer", uniqueIdentifyer.ToString(), RegistryValueKind.String);
                softwareRegKey.OpenSubKey("USBBackup", true).SetValue("BackupFolder", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), RegistryValueKind.String);
                softwareRegKey.OpenSubKey("USBBackup", true).SetValue("Exclude", "AppData");
                softwareRegKey.OpenSubKey("Microsoft", true).OpenSubKey("Windows", true).OpenSubKey("CurrentVersion", true).OpenSubKey("Run", true).SetValue("USB-Backup", System.IO.Path.Combine(programDataPath, "USBBackup", Application.ResourceAssembly.GetName().Name + ".exe"));
            }
            else
            {
                softwareRegKey = Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("USBBackup");
                string uniqueIdent = softwareRegKey.GetValue("UniqueIdentifyer", String.Empty).ToString();
                if (uniqueIdent.Length < 1 || uniqueIdent.Equals(String.Empty))
                    uniqueIdent = uniqueIdentifyer.ToString();

                Directory.CreateDirectory(System.IO.Path.Combine(rootFolder, "USBBackup", uniqueIdent.ToString()));
            }

            softwareRegKey.Close();

            Process p = new Process();
            p.StartInfo.FileName = System.IO.Path.Combine(programDataPath, "USBBackup", Application.ResourceAssembly.GetName().Name + ".exe");
            p.Start();

            ShutdownEvent(null, null);
        }

        private void CheckIfInstalledAndTriggerInstall()
        {
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationLocation = Application.ResourceAssembly.Location;

            if (!applicationLocation.Contains(programDataPath))
            {
                // Not yet installed
                if (MessageBox.Show("USB-Backup ist noch nicht installiert.\r\nSoll es nun installiert werden?", "Soll USB-Backup installiert werden?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    PerformInstallation();
                }
                else
                {
                    ShutdownEvent(null, null);
                }
            }
            else
            {
                // Installed
                ReadConfigFromRegistry();
                AttatchToUSBHandler();
            }
        }

        private void AttatchToUSBHandler()
        {
            Action usbAction = new Action(() =>
            {
                CheckUSBMedia();
            });
            usbHandler = new USBControl(usbAction);
        }

        private void CheckUSBMedia()
        {
            lock (lockObject)
            {
                if (CopyExecuting || BaloonTipDisplayed)
                    return;

                DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    if (!drive.IsReady)
                        continue;

                    if (Directory.Exists(System.IO.Path.Combine(drive.RootDirectory.FullName, "USBBackup", ApplicationConfiguration.UniqueIdentifyer)))
                    {
                        // USB-Media has valid Backup-Folder for this computer
                        // Notify user if he whishes to perform a backup

                        BaloonTipDisplayed = true;
                        taskbarIcon.TrayBalloonTipClicked += PerformBackup;
                        System.Timers.Timer timer = new System.Timers.Timer(10000);
                        timer.Elapsed += delegate (object o, System.Timers.ElapsedEventArgs e) { BaloonTipDisplayed = false; taskbarIcon.TrayBalloonTipClicked -= PerformBackup; };
                        timer.AutoReset = false;
                        timer.Start();
                        taskbarIcon.ShowBalloonTip("Backup bereit", "USB-Backup hat ein Backup-Laufwerk erkannt und kann das Backup nun starten.\r\nZum Starten hier klicken.", BalloonIcon.Info);
                        System.Media.SystemSounds.Asterisk.Play();
                    }
                }
            }
        }

        private async void PerformBackup(object sender, EventArgs eventArgs)
        {
            taskbarIcon.TrayBalloonTipClicked -= PerformBackup;
            if (MessageBox.Show("Soll das Backup jetzt wirklich gestartet werden?", "Backup starten?", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                BaloonTipDisplayed = false;
                return;
            }

            string backupRoot = string.Empty;
            string exclusionString = string.Empty;

            CopyExecuting = true;

            foreach (string exclude in ApplicationConfiguration.Exclusions)
                exclusionString = exclusionString + exclude + " ";

            DriveInfo[] drives = System.IO.DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (!drive.IsReady)
                    continue;

                if (Directory.Exists(System.IO.Path.Combine(drive.RootDirectory.FullName, "USBBackup", ApplicationConfiguration.UniqueIdentifyer)))
                {
                    backupRoot = System.IO.Path.Combine(drive.RootDirectory.FullName, "USBBackup", ApplicationConfiguration.UniqueIdentifyer);
                    break;
                }
            }

            if (backupRoot == string.Empty)
            {
                taskbarIcon.ShowBalloonTip("Fehler beim Durchführen des Backups", "Das Laufwerk war nicht bereit zum Schreiben.\r\nBitte versuchen Sie es erneut.", BalloonIcon.Error);

                return;
            }

            string rootBackupRoot = backupRoot;
            RunningCopyOperations = 0;
            TotalFilesToCopy = 0;

            foreach (string backupSourceRoot in ApplicationConfiguration.BackupFolders)
            {
                if (!Directory.Exists(backupSourceRoot))
                    continue;

                DirectoryInfo info = new DirectoryInfo(backupSourceRoot);
                backupRoot = CreateParentDirectory(rootBackupRoot, info);

                RoboCommand robo = new RoboCommand();
                robo.CopyOptions.Source = backupSourceRoot;
                robo.CopyOptions.Destination = backupRoot;
                robo.CopyOptions.CopySubdirectoriesIncludingEmpty = true;
                robo.RetryOptions.RetryCount = 10;
                robo.RetryOptions.RetryWaitTime = 5;
                robo.SelectionOptions.ExcludeDirectories = exclusionString;
                taskbarIcon.ToolTipText = "USB-Backup - Backup wird initialisiert...";
                await robo.Start_ListOnly();
                robo.OnCommandCompleted += Robo_OnCopyCompleted;
                robo.OnCommandError += Robo_OnCopyError;
                robo.OnFileProcessed += delegate (RoboCommand o, FileProcessedEventArgs e) { };
                robo.OnProgressEstimatorCreated += (RoboCommand s, ProgressEstimatorCreatedEventArgs e) => { robo.IProgressEstimator.ValuesUpdated += IProgressEstimator_ValuesUpdated; };

                TotalFilesToCopy += robo.GetResults().BytesStatistic.Total;
                robo.Start();
                taskbarIcon.ToolTipText = "USB-Backup - Backup läuft...";
                RunningCopyOperations++;
            }
        }

        private void IProgressEstimator_ValuesUpdated(RoboSharp.Interfaces.IProgressEstimator sender, RoboSharp.EventArgObjects.IProgressEstimatorUpdateEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                double percentage = ((double)e.BytesStatistic.Copied) / TotalFilesToCopy;
                percentage *= 100;
                percentage = Math.Round(percentage, 0);
                taskbarIcon.ToolTipText = $"USB-Backup - Backup läuft - {percentage}% abgeschlossen.";
            }), System.Windows.Threading.DispatcherPriority.Send);
        }

        private void Robo_OnCopyError(object sender, CommandErrorEventArgs e)
        {
            taskbarIcon.ShowBalloonTip("Backup fehlerhaft", "Die Erstellung des Backups schloss mit Fehlern ab.", BalloonIcon.Error);
        }

        private void Robo_OnCopyCompleted(object sender, RoboCommandCompletedEventArgs e)
        {
            RunningCopyOperations--;
            if (RunningCopyOperations > 0)
                return;

            taskbarIcon.ShowBalloonTip("Backup abgeschlossen", "Die Erstellung des Backups wurde erfolgreich abgeschlossen.", BalloonIcon.Info);
            taskbarIcon.Dispatcher.BeginInvoke(new Action(() => { taskbarIcon.ToolTipText = "USB-Backup"; }), System.Windows.Threading.DispatcherPriority.Send);
            System.Media.SystemSounds.Asterisk.Play();

            CopyExecuting = false;
            BaloonTipDisplayed = false;
        }

        private string CreateParentDirectory(string backupRoot, DirectoryInfo entity)
        {
            if (entity.Parent != null)
                backupRoot = CreateParentDirectory(backupRoot, entity.Parent);

            backupRoot = System.IO.Path.Combine(backupRoot, entity.Name.Replace("\\", "").Replace(":", ""));
            Directory.CreateDirectory(backupRoot);

            return backupRoot;
        }

        private void ReadConfigFromRegistry()
        {
            RegistryKey softwareRegKey = Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("USBBackup");
            string backupString = softwareRegKey.GetValue("BackupFolder", "").ToString();
            string uniqueIdent = softwareRegKey.GetValue("UniqueIdentifyer", "").ToString();
            string exclusionString = softwareRegKey.GetValue("Exclude", "").ToString();
            softwareRegKey.Close();

            ApplicationConfiguration.BackupFolders = backupString.Split(';').ToList();
            ApplicationConfiguration.Exclusions = exclusionString.Split(';').ToList();
            ApplicationConfiguration.UniqueIdentifyer = uniqueIdent;
        }

        private void CreateTaskBarIcon()
        {
            ShutdownEvent += ShutdownExecute;
            taskbarIcon = new TaskbarIcon();
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ResourceAssembly.Location);
            taskbarIcon.Icon = icon;
            ContextMenu cm = new ContextMenu();

            System.Windows.Controls.MenuItem exitMenuItem = new System.Windows.Controls.MenuItem();
            exitMenuItem.Header = "Beenden";
            exitMenuItem.Click += ShutdownExecute;

            System.Windows.Controls.MenuItem scanMenuItem = new System.Windows.Controls.MenuItem();
            scanMenuItem.Header = "Jetzt nach USB-Zielen suchen";
            scanMenuItem.Click += delegate (object e, RoutedEventArgs f) { CheckUSBMedia(); };

            cm.Items.Add(scanMenuItem);
            cm.Items.Add(exitMenuItem);
            taskbarIcon.ContextMenu = cm;
            taskbarIcon.ToolTipText = "USB-Backup";
        }

        private void ShutdownExecute(object sender, EventArgs args)
        {
            taskbarIcon.Visibility = Visibility.Hidden;
            ShutdownEvent -= ShutdownExecute;
            usbHandler?.Dispose();
            Environment.Exit(0);
        }

        private void WaitForDebugger()
        {
            do
            {
                Thread.Sleep(1000);
            } while (!System.Diagnostics.Debugger.IsAttached);
        }
    }
}
