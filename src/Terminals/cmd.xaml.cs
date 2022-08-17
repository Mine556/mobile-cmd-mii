﻿using MobileTerminal.Classes;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace MobileTerminal.Terminals
{
    public sealed partial class cmd : Page
    {
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        string LocalPath = ApplicationData.Current.LocalFolder.Path;
        static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        TelnetClient client = new TelnetClient(TimeSpan.FromSeconds(1), cancellationTokenSource.Token);
        public cmd()
        {
            this.InitializeComponent();
            try
            {
                GetSettings();
                ApplicationData.Current.LocalFolder.CreateFileAsync("cmdstring.txt", CreationCollisionOption.ReplaceExisting);
                string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
                ulong version = ulong.Parse(deviceFamilyVersion);
                ulong major = (version & 0xFFFF000000000000L) >> 48;
                ulong minor = (version & 0x0000FFFF00000000L) >> 32;
                ulong build = (version & 0x00000000FFFF0000L) >> 16;
                ulong revision = (version & 0x000000000000FFFFL);
                var osVersion = $"{major}.{minor}.{build}.{revision}";

                Globals.ReportedBuildVersion = build;
                Globals.FullBuildNumber = osVersion;
                try
                {
                    Connect();
                }
                catch (Exception ex)
                {
                    Exceptions.ThrowFullError(ex);

                }
            }
            catch (Exception ex)
            {
                Exceptions.ThrowFullError(ex);
            }
        }

        public async void Connect()
        {
            try
            {
                BlockTextInput(true);
                CMDtestText.Text = $"Connecting to device...";
                await client.Connect();
                await client.Send($"echo %CD%^> > \"{LocalPath}\\cmdstring.txt\" 2>&1");
                string results = File.ReadAllText($"{LocalPath}\\cmdstring.txt");
                CMDtestText.Text =
                    $"mobile-cmd v0.3-beta\n" +
                    $"Microsoft Windows [{Globals.FullBuildNumber}]\n" +
                    $"(c) Microsoft Corporation. All rights reserved." +
                    $"\n\n{results}";
                BlockTextInput(false);

            }
            catch (Exception ex)
            {
                Exceptions.ThrowFullError(ex);
            }
        }

        private void GetSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values["Font"] is string Font)
            {
                CMDtestText.FontFamily = new FontFamily(Font);
                SendCommandText.FontFamily = new FontFamily(Font);
            }
        }
        private void SendCommandBtn_Click(object sender, RoutedEventArgs e)
        {
            // Prevent two commands running at the same time in different tabs
            string CommandRunning = localSettings.Values["CommandRunning"] as string;
            if (CommandRunning == "true")
            {
                Exceptions.CustomMessage("You cannot run two commands at the same time");
            }
            else
            {
                // Change "CommandRunning" to "true" to prevent another command from running
                localSettings.Values["CommandRunning"] = "true";
                // Block text input
                BlockTextInput(true);
                var text = CMDtestText.Text;
                CMDtestText.Text = text.Remove(text.Length - 2);
                // Write command to UI
                CMDtestText.Text += $"{SendCommandText.Text}\n";
                // Check if command is malicious and if it isnt run the command
                bool IsCommandMalicious = Tools.CheckForMaliciousCommand(SendCommandText.Text);
                if (!IsCommandMalicious)
                {
                    SendCommand();
                }
                else
                {
                    CMDtestText.Text += "Command aborted. The command you tried to run is malicious";
                }
                // Clear Text input
                SendCommandText.Text = "";
                // Change "CommandRunning" to false, since the command now finish running
                localSettings.Values["CommandRunning"] = "false";
            }
        }

        private async void SendCommand()
        {
            string command = SendCommandText.Text;
            if (command.Length != 0)
            {
                if (command == "cls")
                {
                    CMDtestText.Text = "";
                    await ShowCurrentPath();
                }
                else
                {
                    // send command
                    await client.Send($"{command} > \"{LocalPath}\\cmdstring.txt\" 2>&1");
                    TestIfFileAccessable();
                }
            }
        }

        private async void TestIfFileAccessable()
        {
            bool IsFileUsed = Tools.IsFileAccessable($"{LocalPath}\\cmdstring.txt");
            if (IsFileUsed == false)
            {
                GetOutput();
            }
            else
            {
                await Task.Delay(500);
                TestIfFileAccessable();
            }
        }

        private async void GetOutput()
        {
            try
            {
                // Append command output to TextBox
                CMDtestText.Text += "\n" + File.ReadAllText($"{LocalPath}\\cmdstring.txt");
                await ShowCurrentPath();
                // Scroll to bottom to show results
                var grid = (Grid)VisualTreeHelper.GetChild(CMDtestText, 0);
                for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
                {
                    object obj = VisualTreeHelper.GetChild(grid, i);
                    if (!(obj is ScrollViewer)) continue;
                    ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                    break;
                }
                // Remove text input block
                BlockTextInput(false);
            }
            catch (Exception ex)
            {
                Exceptions.ThrowFullError(ex);
            }
        }

        private async Task ShowCurrentPath()
        {
            // Show current path
            await client.Send($"echo %CD%^> > \"{LocalPath}\\cmdstring.txt\" 2>&1");
            string currentpath = File.ReadAllText($"{LocalPath}\\cmdstring.txt");
            CMDtestText.Text += "\n\n" + $"{currentpath}";
        }

        private void BlockTextInput(bool enable)
        {
            try
            {
                if (enable == true)
                {
                    SendCommandText.IsEnabled = false;
                    CMDtestText.IsHitTestVisible = false;
                    SendCommandBtn.IsEnabled = false;
                }
                else
                {
                    SendCommandText.IsEnabled = true;
                    CMDtestText.IsHitTestVisible = true;
                    SendCommandBtn.IsEnabled = true;
                    SendCommandText.Focus(FocusState.Programmatic);
                }

            }
            catch (Exception ex)
            {
                Exceptions.ThrowFullError(ex);
            }
        }
    }
}
