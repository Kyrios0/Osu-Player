﻿using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Data;
using Milky.OsuPlayer.Common.Data.EF.Model;
using Milky.OsuPlayer.Common.Data.EF.Model.V1;
using Milky.OsuPlayer.Common.Metadata;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Control;
using Milky.OsuPlayer.Control.FrontDialog;
using Milky.WpfApi;
using Milky.WpfApi.Collections;
using Milky.OsuPlayer.Models;
using Milky.OsuPlayer.Pages;
using Milky.OsuPlayer.Windows;
using Milky.WpfApi.Commands;

namespace Milky.OsuPlayer.ViewModels
{
    public class CollectionPageViewModel : ViewModelBase
    {
        private BeatmapDbOperator _beatmapDbOperator = new BeatmapDbOperator();
        private AppDbOperator _appDbOperator = new AppDbOperator();

        private NumberableObservableCollection<BeatmapDataModel> _beatmaps;
        private NumberableObservableCollection<BeatmapDataModel> _displayedBeatmaps;
        private Collection _collectionInfo;

        public NumberableObservableCollection<BeatmapDataModel> Beatmaps
        {
            get => _beatmaps;
            set
            {
                _beatmaps = value;
                OnPropertyChanged();
            }
        }

        public NumberableObservableCollection<BeatmapDataModel> DisplayedBeatmaps
        {
            get => _displayedBeatmaps;
            set
            {
                _displayedBeatmaps = value;
                OnPropertyChanged();
            }
        }

        public Collection CollectionInfo
        {
            get => _collectionInfo;
            set
            {
                _collectionInfo = value;
                OnPropertyChanged();
            }
        }

        public ICommand SearchByConditionCommand
        {
            get
            {
                return new DelegateCommand(param =>
                {
                    WindowBase.GetCurrentFirst<MainWindow>()
                        .SwitchSearch
                        .CheckAndAction(page => ((SearchPage)page).Search((string)param));
                });
            }
        }

        public ICommand OpenSourceFolderCommand
        {
            get
            {
                return new DelegateCommand(param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    var map = _beatmapDbOperator.GetBeatmapByIdentifiable(beatmap);
                    if (map == null) return;
                    var fileName = beatmap.InOwnDb
                        ? Path.Combine(Domain.CustomSongPath, map.FolderName)
                        : Path.Combine(Domain.OsuSongPath, map.FolderName);
                    if (!Directory.Exists(fileName))
                    {
                        Notification.Show(@"所选文件不存在，可能没有及时同步。请尝试手动同步osuDB后重试。");
                        return;
                    }

                    Process.Start(fileName);
                });
            }
        }

        public ICommand OpenScorePageCommand
        {
            get
            {
                return new DelegateCommand(param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    var map = _beatmapDbOperator.GetBeatmapByIdentifiable(beatmap);
                    if (map == null) return;
                    Process.Start($"https://osu.ppy.sh/s/{map.BeatmapSetId}");
                });
            }
        }

        public ICommand SaveCollectionCommand
        {
            get
            {
                return new DelegateCommand(param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    var map = _beatmapDbOperator.GetBeatmapByIdentifiable(beatmap);
                    FrontDialogOverlay.Default.ShowContent(new SelectCollectionControl(map),
                        DialogOptionFactory.SelectCollectionOptions);
                });
            }
        }

        public ICommand ExportCommand
        {
            get
            {
                return new DelegateCommand(param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    var map = _beatmapDbOperator.GetBeatmapByIdentifiable(beatmap);
                    if (map == null) return;
                    ExportPage.QueueEntry(map);
                });
            }
        }

        public ICommand DirectPlayCommand
        {
            get
            {
                return new DelegateCommand(async param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    var map = _beatmapDbOperator.GetBeatmapByIdentifiable(beatmap);
                    if (map == null) return;
                    await PlayController.Default.PlayNewFile(map);
                    await Services.Get<PlayerList>()
                        .RefreshPlayListAsync(PlayerList.FreshType.All, PlayListMode.RecentList);
                });
            }
        }

        public ICommand PlayCommand
        {
            get
            {
                return new DelegateCommand(async param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    var map = _beatmapDbOperator.GetBeatmapByIdentifiable(beatmap);
                    await PlayController.Default.PlayNewFile(map);
                    await Services.Get<PlayerList>()
                        .RefreshPlayListAsync(PlayerList.FreshType.All, PlayListMode.RecentList);

                });
            }
        }

        public ICommand RemoveCommand
        {
            get
            {
                return new DelegateCommand(param =>
                {
                    var beatmap = (BeatmapDataModel)param;
                    _appDbOperator.RemoveMapFromCollection(beatmap.GetIdentity(), CollectionInfo);
                    Beatmaps.Remove(beatmap);
                    DisplayedBeatmaps.Remove(beatmap);
                    //await Services.Get<PlayerList>().RefreshPlayListAsync(PlayerList.FreshType.All, PlayListMode.Collection, _entries);
                });
            }
        }
    }
}
