﻿using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Data;
using Milky.OsuPlayer.Common.Data.EF.Model;
using Milky.OsuPlayer.Common.Instances;
using Milky.OsuPlayer.Common.Metadata;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Control;
using Milky.OsuPlayer.Windows;
using Milky.WpfApi.Collections;
using OSharp.Beatmap;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Milky.OsuPlayer.Pages
{
    /// <summary>
    /// RecentPlayPage.xaml 的交互逻辑
    /// </summary>
    public partial class RecentPlayPage : Page
    {
        private IEnumerable<Beatmap> _entries;
        public NumberableObservableCollection<BeatmapDataModel> DataModels;
        private readonly MainWindow _mainWindow;

        public RecentPlayPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeComponent();
        }

        public void UpdateList()
        {
            _entries = BeatmapQuery.GetRecentListFromDb();
            DataModels = new NumberableObservableCollection<BeatmapDataModel>(_entries.ToDataModels(false));
            RecentList.DataContext = DataModels.ToList();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateList();
            var item = DataModels.FirstOrDefault(k =>
                k.GetIdentity().Equals(InstanceManage.GetInstance<PlayerList>().CurrentInfo.Identity));
            RecentList.SelectedItem = item;
        }

        private void Recent_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PlaySelected();
        }

        private void ItemPlay_Click(object sender, RoutedEventArgs e)
        {
            PlaySelected();
        }

        private async void ItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if (RecentList.SelectedItem == null)
                return;
            var selected = RecentList.SelectedItems;
            var entries = ConvertToEntries(selected.Cast<BeatmapDataModel>());
            //var searchInfo = (BeatmapDataModel)RecentList.SelectedItem;
            foreach (var entry in entries)
            {
                DbOperate.RemoveFromRecent(entry.GetIdentity());
            }
            UpdateList();
            await InstanceManage.GetInstance<PlayerList>().RefreshPlayListAsync(PlayerList.FreshType.All, PlayListMode.RecentList);
        }

        private void BtnDelAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MsgBox.Show(_mainWindow, "真的要删除全部吗？", _mainWindow.Title, MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                DbOperate.ClearRecent();
                UpdateList();
            }
        }

        private void BtnPlayAll_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ItemCollect_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.FramePop.Navigate(new SelectCollectionPage(_mainWindow, GetSelected()));
        }

        private void ItemExport_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            if (map == null) return;
            ExportPage.QueueEntry(map);
        }

        private void ItemSearchSource_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            if (map == null) return;
            _mainWindow.MainFrame.Navigate(new SearchPage(_mainWindow, map.SongSource));
        }

        private void ItemSearchMapper_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            if (map == null) return;
            _mainWindow.MainFrame.Navigate(new SearchPage(_mainWindow, map.Creator));
        }

        private void ItemSearchArtist_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            if (map == null) return;
            _mainWindow.MainFrame.Navigate(new SearchPage(_mainWindow,
                MetaString.GetUnicode(map.Artist, map.ArtistUnicode)));
        }

        private void ItemSearchTitle_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            if (map == null) return;
            _mainWindow.MainFrame.Navigate(new SearchPage(_mainWindow,
                MetaString.GetUnicode(map.Title, map.TitleUnicode)));
        }

        private void ItemSet_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            Process.Start($"https://osu.ppy.sh/b/{map.BeatmapId}");
        }

        private void ItemFolder_Click(object sender, RoutedEventArgs e)
        {
            var map = GetSelected();
            Process.Start(Path.Combine(Domain.OsuSongPath, map.FolderName));
        }

        private async void PlaySelected()
        {
            var map = GetSelected();
            if (map == null) return;

            await _mainWindow.PlayNewFile(Path.Combine(Domain.OsuSongPath, map.FolderName,
                   map.BeatmapFileName));
            await InstanceManage.GetInstance<PlayerList>().RefreshPlayListAsync(PlayerList.FreshType.None, PlayListMode.RecentList);
        }

        private Beatmap GetSelected()
        {
            if (RecentList.SelectedItem == null)
                return null;
            var selectedItem = (BeatmapDataModel)RecentList.SelectedItem;
            return BeatmapQuery.FilterByFolder(selectedItem.FolderName)
                .FirstOrDefault(k => k.Version == selectedItem.Version);
        }

        private Beatmap ConvertToEntry(BeatmapDataModel dataModel)
        {
            return BeatmapQuery.FilterByFolder(dataModel.FolderName)
                .FirstOrDefault(k => k.Version == dataModel.Version);
        }

        private IEnumerable<Beatmap> ConvertToEntries(IEnumerable<BeatmapDataModel> dataModels)
        {
            return dataModels.Select(ConvertToEntry);
        }
    }
}
