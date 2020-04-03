﻿using Milky.OsuPlayer.Models.Github;
using Milky.OsuPlayer.Presentation;
using System.Diagnostics;
using Milky.OsuPlayer.Common.Configuration;

namespace Milky.OsuPlayer.Windows
{
    /// <summary>
    /// NewVersionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewVersionWindow : WindowEx
    {
        private readonly Release _release;
        private readonly MainWindow _mainWindow;

        public NewVersionWindow(Release release, MainWindow mainWindow)
        {
            _release = release;
            _mainWindow = mainWindow;
            InitializeComponent();
            MainGrid.DataContext = _release;
        }

        private void OpenHyperlink(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            var p = e.Parameter.ToString();
            if (p == "later")
            {
                Close();
            }
            else if (p == "ignore")
            {
                AppSettings.Default.IgnoredVer = _release.NewVerString;
                AppSettings.SaveDefault();
                Close();
            }
            else if (p == "update")
            {
                UpdateWindow updateWindow = new UpdateWindow(_release, _mainWindow);
                updateWindow.Show();
                Close();
            }
            else
                Process.Start(p);
        }
    }
}
