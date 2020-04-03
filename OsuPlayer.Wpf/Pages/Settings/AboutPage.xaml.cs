﻿using Milky.OsuPlayer.Presentation;
using Milky.OsuPlayer.Shared;
using Milky.OsuPlayer.Utils;
using Milky.OsuPlayer.Windows;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Shared.Dependency;

namespace Milky.OsuPlayer.Pages.Settings
{
    /// <summary>
    /// AboutPage.xaml 的交互逻辑
    /// </summary>
    public partial class AboutPage : Page
    {
        private readonly MainWindow _mainWindow;
        private readonly ConfigWindow _configWindow;
        private readonly string _dtFormat = "g";
        private NewVersionWindow _newVersionWindow;

        public AboutPage()
        {
            _mainWindow = WindowEx.GetCurrentFirst<MainWindow>();
            _configWindow = WindowEx.GetCurrentFirst<ConfigWindow>();
            InitializeComponent();
        }

        private void LinkGithub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Milkitic/Osu-Player");
        }

        private void LinkFeedback_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Milkitic/Osu-Player/issues/new");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentVer.Content = Service.Get<Updater>().CurrentVersion;
            if (Service.Get<Updater>().NewRelease != null)
                NewVersion.Visibility = Visibility.Visible;
            GetLastUpdate();
        }

        private void GetLastUpdate()
        {
            LastUpdate.Content = AppSettings.Default.LastUpdateCheck == null
                ? "从未"
                : AppSettings.Default.LastUpdateCheck.Value.ToString(_dtFormat);
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            //todo: action
            CheckUpdate.IsEnabled = false;
            var hasNew = await Service.Get<Updater>().CheckUpdateAsync();
            CheckUpdate.IsEnabled = true;
            if (hasNew == null)
            {
                MessageBox.Show(_configWindow, "检查更新时出错。", _configWindow.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AppSettings.Default.LastUpdateCheck = DateTime.Now;
            GetLastUpdate();
            AppSettings.SaveDefault();
            if (hasNew == true)
            {
                NewVersion.Visibility = Visibility.Visible;
                NewVersion_Click(sender, e);
            }
            else
            {
                MessageBox.Show(_configWindow, "已是最新版本。", _configWindow.Title, MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void NewVersion_Click(object sender, RoutedEventArgs e)
        {
            if (_newVersionWindow != null && !_newVersionWindow.IsClosed)
                _newVersionWindow.Close();
            _newVersionWindow = new NewVersionWindow(Service.Get<Updater>().NewRelease, _mainWindow);
            _newVersionWindow.ShowDialog();
        }

        private void LinkLicense_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Milkitic/Osu-Player/blob/master/LICENSE");
        }

        private void LinkPrivacy_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This software will NOT collect any user information.");
        }
    }
}
