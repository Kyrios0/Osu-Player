﻿using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Common.Data;
using Milky.OsuPlayer.Common.Instances;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Common.Scanning;
using Milky.OsuPlayer.Control;
using Milky.OsuPlayer.Media.Audio;
using Milky.OsuPlayer.Pages;
using Milky.OsuPlayer.Utils;
using Milky.OsuPlayer.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace Milky.OsuPlayer.Windows
{
    partial class MainWindow
    {
        #region Window events

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //// todo: This should be kept since the application exit last time.
            //BtnRecent_Click(sender, e);
            if (PlayerConfig.Current.General.FirstOpen)
            {
                WelcomeViewModel.ShowWelcome = true;
                await LoadLocalDbAsync();
            }
            else
            {
                await InstanceManage.GetInstance<OsuFileScanner>().NewScanAndAddAsync();
                await InstanceManage.GetInstance<OsuDbInst>().SyncOsuDbAsync(PlayerConfig.Current.General.DbPath, true);
                await InstanceManage.GetInstance<OsuDbInst>().LoadLocalDbAsync();
                //ScanSynchronously();
                //SyncSynchronously();
            }

            UpdateCollections();
            //LoadSurfaceSettings();

            if (PlayerConfig.Current.CurrentPath != null && PlayerConfig.Current.Play.Memory)
            {
                var entries = BeatmapQuery.FilterByIdentities(PlayerConfig.Current.CurrentList);
                await InstanceManage.GetInstance<PlayerList>()
                    .RefreshPlayListAsync(PlayerList.FreshType.All, beatmaps: entries);

                bool play = PlayerConfig.Current.Play.AutoPlay;
                await PlayNewFile(PlayerConfig.Current.CurrentPath, play);
            }

            await SetPlayMode(PlayerConfig.Current.Play.PlayListMode);

            var helper = new WindowInteropHelper(this);
            var source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndMessageHook);

            //if (PlayerConfig.Current.General.FirstOpen)
            //{
            //    ViewModel.ShowWelcome = true;
            //}

            var updater = InstanceManage.GetInstance<Updater>();
            bool? hasUpdate = await updater.CheckUpdateAsync();
            if (hasUpdate == true && updater.NewRelease.NewVerString != PlayerConfig.Current.IgnoredVer)
            {
                var newVersionWindow = new NewVersionWindow(updater.NewRelease, this);
                newVersionWindow.ShowDialog();
            }
        }

        private static void ScanSynchronously()
        {
            Task.Run(() => InstanceManage.GetInstance<OsuFileScanner>().NewScanAndAddAsync());
        }

        private static async Task LoadLocalDbAsync()
        {
            await InstanceManage.GetInstance<OsuDbInst>().LoadLocalDbAsync();
        }

        private static void SyncSynchronously()
        {
            Task.Run(() => InstanceManage.GetInstance<OsuDbInst>().SyncOsuDbAsync(PlayerConfig.Current.General.DbPath, true));
        }

        /// <summary>
        /// Clear things.
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (PlayerConfig.Current.General.ExitWhenClosed == null && !ForceExit)
            {
                e.Cancel = true;
                FramePop.Navigate(new ClosingPage(this));
                return;
            }
            else if (PlayerConfig.Current.General.ExitWhenClosed == false && !ForceExit)
            {
                WindowState = WindowState.Minimized;
                Hide();
                e.Cancel = true;
                return;
            }

            ClearHitsoundPlayer();
            ComponentPlayer.DisposeAll();
            LyricWindow.Dispose();
            NotifyIcon.Dispose();
            if (ConfigWindow == null || ConfigWindow.IsClosed)
                return;
            if (ConfigWindow.IsInitialized)
                ConfigWindow.Close();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                return;
            _lastState = WindowState;
        }

        #endregion Window events

        #region Navigation events

        private bool _ischanging = false;

        /// <summary>
        /// Navigate search page.
        /// </summary>
        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content?.GetType() != typeof(SearchPage))
                MainFrame.Navigate(Pages.SearchPage);
            //MainFrame.Navigate(new Uri("Pages/SearchPage.xaml", UriKind.Relative), this);
        }

        /// <summary>
        /// Navigate find page.
        /// </summary>
        private void BtnFind_Click(object sender, RoutedEventArgs e)
        {
            //MainFrame.Navigate(Pages.FindPage);
            MsgBox.Show(this, "功能完善中，敬请期待~", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            //bool? b = await PageBox.ShowDialog(Title, "功能完善中，敬请期待~");
        }

        /// <summary>
        /// Navigate storyboard page.
        /// </summary>
        private void Storyboard_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(Pages.StoryboardPage);
        }

        /// <summary>
        /// Navigate recent page.
        /// </summary>
        private void BtnRecent_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content?.GetType() != typeof(RecentPlayPage))
                MainFrame.Navigate(Pages.RecentPlayPage);
        }

        /// <summary>
        /// Navigate export page.
        /// </summary>
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content?.GetType() != typeof(ExportPage))
                MainFrame.Navigate(Pages.ExportPage);
        }

        private void BtnAddCollection_Click(object sender, RoutedEventArgs e)
        {
            FramePop.Navigate(new AddCollectionPage(this));
        }

        /// <summary>
        /// Navigate collection page.
        /// </summary>
        private void BtnCollection_Click(object sender, RoutedEventArgs e)
        {
            var btn = (ToggleButton)sender;
            var colId = (string)btn.Tag;
            if (MainFrame.Content?.GetType() != typeof(CollectionPage))
                MainFrame.Navigate(new CollectionPage(this, DbOperate.GetCollectionById(colId)));
            if (MainFrame.Content?.GetType() == typeof(CollectionPage))
            {
                var sb = (CollectionPage)MainFrame.Content;
                if (sb.Id != colId)
                    MainFrame.Navigate(new CollectionPage(this, DbOperate.GetCollectionById(colId)));
            }
        }

        private void BtnNavigate_Checked(object sender, RoutedEventArgs e)
        {
            if (_ischanging)
                return;
            _ischanging = true;
            var btn = (ToggleButton)sender;
            _optionContainer.Switch(btn);
            _ischanging = false;
        }

        private void BtnNavigate_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_ischanging)
                return;
            _ischanging = true;
            ((ToggleButton)sender).IsChecked = true;
            _ischanging = false;
        }

        #endregion Navigation events

        #region Title events

        /// <summary>
        /// Popup a dialog for settings.
        /// </summary>
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigWindow == null || ConfigWindow.IsClosed)
            {
                ConfigWindow = new ConfigWindow(this);
                ConfigWindow.Show();
            }
            else
            {
                if (ConfigWindow.IsInitialized)
                    ConfigWindow.Focus();
            }
        }

        private void BtnMini_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsMiniMode = true;
            ToMiniMode();
        }

        #endregion Title events

        #region Play control events

        private void ThumbButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PlayerViewModel.Current.EnableVideo)
                PlayerViewModel.Current.EnableVideo = true;
            else if (PlayerViewModel.Current.EnableVideo)
            {
                if (ResizableArea.Margin == new Thickness(5))
                    SetFullScr();
                else
                    SetFullScrMini();
            }
        }

        private void SetFullScrMini()
        {
            ResizableArea.BorderBrush = new SolidColorBrush(Color.FromArgb(64, 0, 0, 0));
            ResizableArea.BorderThickness = new Thickness(1);
            ResizableArea.HorizontalAlignment = HorizontalAlignment.Right;
            ResizableArea.VerticalAlignment = VerticalAlignment.Bottom;
            ResizableArea.Width = 318;
            ResizableArea.Height = 180;
            ResizableArea.Margin = new Thickness(5);
        }

        private void BtnHideFullScr_Click(object sender, RoutedEventArgs e)
        {
            //if (FullModeArea.Visibility == Visibility.Visible)
            //{
            //    SetFullScr();
            //    FullModeArea.Visibility = Visibility.Hidden;
            //}
            if (PlayerViewModel.Current.EnableVideo)
            {
                SetFullScr();
                PlayerViewModel.Current.EnableVideo = false;
            }
        }

        private void SetFullScr()
        {
            ResizableArea.ClearValue(Border.BorderBrushProperty);
            ResizableArea.ClearValue(Border.BorderThicknessProperty);
            ResizableArea.ClearValue(Border.HorizontalAlignmentProperty);
            ResizableArea.ClearValue(Border.VerticalAlignmentProperty);
            ResizableArea.ClearValue(Border.WidthProperty);
            ResizableArea.ClearValue(Border.HeightProperty);
            ResizableArea.ClearValue(Border.MarginProperty);
        }

        /// <summary>
        /// Play next song in playlist.
        /// </summary>
        public async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            _videoPlay = true;
            _forcePaused = false;
            if (ComponentPlayer.Current == null)
            {
                BtnOpen_Click(sender, e);
                return;
            }

            switch (ComponentPlayer.Current.PlayerStatus)
            {
                case PlayerStatus.Playing:
                    if (VideoElement?.Source != null)
                        await VideoElement.Pause();
                    PauseMedia();
                    break;
                case PlayerStatus.Ready:
                case PlayerStatus.Stopped:
                case PlayerStatus.Paused:
                    if (VideoElement?.Source != null)
                        await VideoElement.Play();
                    PlayMedia();
                    break;
            }
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var path = LoadFile();
            if (path != null)
            {
                await PlayNewFile(path);
                await InstanceManage.GetInstance<PlayerList>().RefreshPlayListAsync(PlayerList.FreshType.None);
            }
        }

        public async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            await PlayNextAsync(true, false);
        }

        public async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            await PlayNextAsync(true, true);
        }

        private void BtnMode_Click(object sender, RoutedEventArgs e)
        {
            PopMode.IsOpen = true;
        }

        /// <summary>
        /// Popup a dialog for adding music to a collection.
        /// </summary>
        private async void BtnLike_Click(object sender, RoutedEventArgs e)
        {
            var entry = BeatmapQuery.FilterByIdentity(InstanceManage.GetInstance<PlayerList>().CurrentIdentity);
            //var entry = App.PlayerList?.CurrentInfo.Beatmap;
            if (entry == null)
            {
                MsgBox.Show(this, "该图不存在于该osu!db中。", Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (!ViewModel.IsMiniMode)
                FramePop.Navigate(new SelectCollectionPage(this, entry));
            else
            {
                var collection = DbOperate.GetCollections().First(k => k.Locked);
                if (InstanceManage.GetInstance<PlayerList>().CurrentInfo.IsFavorite)
                {
                    DbOperate.RemoveMapFromCollection(entry, collection);
                    InstanceManage.GetInstance<PlayerList>().CurrentInfo.IsFavorite = false;
                }
                else
                {
                    await SelectCollectionPage.AddToCollectionAsync(collection, entry);
                    InstanceManage.GetInstance<PlayerList>().CurrentInfo.IsFavorite = true;
                }
            }

            IsMapFavorite(InstanceManage.GetInstance<PlayerList>().CurrentInfo.Identity);
        }

        private void BtnVolume_Click(object sender, RoutedEventArgs e)
        {
            Pop.IsOpen = true;
        }

        private void PlayMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_ischanging)
                return;
            _ischanging = true;
            var btn = (ToggleButton)sender;
            _modeOptionContainer.Switch(btn);
            _ischanging = false;
        }

        private void PlayMode_UnChecked(object sender, RoutedEventArgs e)
        {
            if (_ischanging)
                return;
            _ischanging = true;
            ((ToggleButton)sender).IsChecked = true;
            _ischanging = false;
        }
        private async void PlayMode_Click(object sender, RoutedEventArgs e)
        {
            var btn = (ToggleButton)sender;
            PlayerMode playmode;
            switch (btn.Name)
            {
                case "Single":
                    //BtnMode.Content = "单曲播放";
                    playmode = PlayerMode.Single;
                    break;
                case "SingleLoop":
                    //BtnMode.Content = "单曲循环";
                    playmode = PlayerMode.SingleLoop;
                    break;
                case "Normal":
                    //BtnMode.Content = "顺序播放";
                    playmode = PlayerMode.Normal;
                    break;
                case "Random":
                    //BtnMode.Content = "随机播放";
                    playmode = PlayerMode.Random;
                    break;
                case "Loop":
                    //BtnMode.Content = "循环列表";
                    playmode = PlayerMode.Loop;
                    break;
                default:
                case "LoopRandom":
                    //BtnMode.Content = "随机循环";
                    playmode = PlayerMode.LoopRandom;
                    break;
            }

            await SetPlayMode(playmode);
            PopMode.IsOpen = false;
        }

        private async Task SetPlayMode(PlayerMode playMode)
        {
            switch (playMode)
            {
                case PlayerMode.Normal:
                    Normal.IsChecked = true;
                    break;
                case PlayerMode.Random:
                    Random.IsChecked = true;
                    break;
                case PlayerMode.Loop:
                    Loop.IsChecked = true;
                    break;
                case PlayerMode.LoopRandom:
                    LoopRandom.IsChecked = true;
                    break;
                case PlayerMode.Single:
                    Single.IsChecked = true;
                    break;
                case PlayerMode.SingleLoop:
                    SingleLoop.IsChecked = true;
                    break;
            }

            string flag = ViewModel.IsMiniMode ? "S" : "";
            ModeButton.Background = (ImageBrush)ToolControl.FindResource(playMode + flag);
            if (playMode == InstanceManage.GetInstance<PlayerList>().PlayerMode)
                return;
            InstanceManage.GetInstance<PlayerList>().PlayerMode = playMode;
            await InstanceManage.GetInstance<PlayerList>().RefreshPlayListAsync(PlayerList.FreshType.IndexOnly);
            PlayerConfig.Current.Play.PlayListMode = playMode;
            PlayerConfig.SaveCurrent();
        }

        private void BtnMax_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsMiniMode = false;
            ToNormalMode();
        }

        private void PlayProgress_DragStarted(object sender, DragStartedEventArgs e)
        {
            _scrollLock = true;
        }

        /// <summary>
        /// Play progress control.
        /// While drag started, slider's updating should be recoverd.
        /// </summary>
        private async void PlayProgress_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (ComponentPlayer.Current != null)
            {
                switch (ComponentPlayer.Current.PlayerStatus)
                {
                    case PlayerStatus.Playing:
                        if (VideoElement.Source != null)
                        {
                            ComponentPlayer.Current.SetTime((int)PlayProgress.Value, false);
                            // Todo: Set Storyboard Progress
                            _forcePaused = false;
                            await VideoJumpTo((int)PlayProgress.Value);
                        }
                        else
                        {
                            ComponentPlayer.Current.SetTime((int)PlayProgress.Value);
                            // Todo: Set Storyboard Progress
                        }
                        break;
                    case PlayerStatus.Paused:
                    case PlayerStatus.Stopped:
                        _forcePaused = true;
                        if (VideoElement.Source != null)
                            await VideoJumpTo((int)PlayProgress.Value);
                        ComponentPlayer.Current.SetTime((int)PlayProgress.Value, false);
                        // Todo: Set Storyboard Progress
                        break;
                }
            }

            _scrollLock = false;
        }

        private void Mod_CheckChanged(object sender, RoutedEventArgs e)
        {
            PlayMod mod;
            if (ModNone.IsChecked == true)
                mod = PlayMod.None;
            else if (ModDt.IsChecked == true)
                mod = PlayMod.DoubleTime;
            else if (ModHt.IsChecked == true)
                mod = PlayMod.HalfTime;
            else if (ModNc.IsChecked == true)
                mod = PlayMod.NightCore;
            else if (ModDc.IsChecked == true)
                mod = PlayMod.DayCore;
            else
                throw new ArgumentOutOfRangeException();

            PlayerConfig.Current.Play.PlayMod = mod;
            ComponentPlayer.Current.SetPlayMod(mod, ComponentPlayer.Current.PlayerStatus == PlayerStatus.Playing);
        }

        #endregion Play control events

        #region Popup events

        /// <summary>
        /// Play progress control.
        /// While drag started, slider's updating should be paused.
        /// </summary>
        /// <summary>
        /// Master Volume Settings
        /// </summary>
        private void MasterVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            //PlayerConfig.Current.Volume.Main = (float)(MasterVolume.Value / 100);
            PlayerConfig.SaveCurrent();
        }

        /// <summary>
        /// Music Volume Settings
        /// </summary>
        private void MusicVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            //PlayerConfig.Current.Volume.Music = (float)(MusicVolume.Value / 100);
            PlayerConfig.SaveCurrent();
        }

        /// <summary>
        /// Effect Volume Settings
        /// </summary>
        private void HitsoundVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            PlayerConfig.SaveCurrent();
        }

        /// <summary>
        /// Offset Settings
        /// </summary>
        private void Offset_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ComponentPlayer.Current == null)
                return;
            ComponentPlayer.Current.HitsoundOffset = (int)Offset.Value;
            DbOperate.UpdateMap(InstanceManage.GetInstance<PlayerList>().CurrentInfo.Identity, ComponentPlayer.Current.HitsoundOffset);
        }

        #endregion Popup events

        #region Notification events

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Topmost = true;
            Topmost = false;
            Show();
            WindowState = _lastState;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            ForceExit = true;
            this.Close();
        }

        private void MenuConfig_Click(object sender, RoutedEventArgs e)
        {
            BtnSettings_Click(sender, e);
        }

        private void MenuOpenHideLyric_Click(object sender, RoutedEventArgs e)
        {
            if (LyricWindow.IsShown)
            {
                LyricWindow.Hide();
            }
            else
            {
                LyricWindow.Show();
            }
        }

        private void MenuLockLyric_Click(object sender, RoutedEventArgs e)
        {
            LyricWindow.IsLocked = !LyricWindow.IsLocked;
        }

        #endregion Notification events
    }
}
