﻿using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Common.Data;
using Milky.OsuPlayer.Common.Data.EF.Model;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Media.Audio.Player;
using Milky.OsuPlayer.Media.Audio.Wave;
using Milky.OsuPlayer.Presentation.Interaction;
using OSharp.Beatmap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Milky.OsuPlayer.Media.Audio
{

    public sealed class ObservablePlayController : VmBase, IAsyncDisposable
    {
        public event Action<PlayStatus> PlayStatusChanged;
        public event Action<TimeSpan> PositionUpdated;

        public event Action InterfaceClearRequest;

        public event Action<string, CancellationToken> PreLoadStarted;

        public event Action<BeatmapContext, CancellationToken> LoadStarted;

        public event Action<BeatmapContext, CancellationToken> MetaLoaded;
        public event Action<BeatmapContext, CancellationToken> BackgroundInfoLoaded;
        public event Action<BeatmapContext, CancellationToken> MusicLoaded;
        public event Action<BeatmapContext, CancellationToken> VideoLoadRequested;
        public event Action<BeatmapContext, CancellationToken> StoryboardLoadRequested;

        public event Action<BeatmapContext, CancellationToken> LoadFinished;



        public OsuMixPlayer Player
        {
            get => _player;
            private set
            {
                if (Equals(value, _player)) return;
                _player = value;
                OnPropertyChanged();
            }
        }

        public PlayList PlayList { get; } = new PlayList();
        public bool IsPlayerReady => Player != null && Player.PlayStatus != PlayStatus.Unknown;

        private OsuMixPlayer _player;
        private SemaphoreSlim _readLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly AppDbOperator _appDbOperator = new AppDbOperator();

        public ObservablePlayController()
        {
            PlayList.AutoSwitched += PlayList_AutoSwitched;
            PlayList.SongListChanged += PlayList_SongListChanged;
        }

        public async Task PlayNewAsync(Beatmap beatmap, bool playInstantly = true)
        {
            PlayList.AddOrSwitchTo(beatmap);
            InitializeContextHandle(PlayList.CurrentInfo);
            await LoadAsync(false, playInstantly).ConfigureAwait(false);
            if (playInstantly) PlayList.CurrentInfo.PlayHandle.Invoke();
        }

        public async Task PlayNewAsync(string path, bool playInstantly = true)
        {
            try
            {
                await _readLock.WaitAsync(_cts.Token).ConfigureAwait(false);

                if (!File.Exists(path))
                    throw new FileNotFoundException("cannot locate file", path);
                await ClearPlayer();
                Execute.OnUiThread(() => PreLoadStarted?.Invoke(path, _cts.Token));
                var osuFile =
                    await OsuFile.ReadFromFileAsync(path, options => options.ExcludeSection("Editor"))
                        .ConfigureAwait(false); //50 ms
                var beatmap = Beatmap.ParseFromOSharp(osuFile);
                beatmap.IsTemporary = true;
                Beatmap trueBeatmap = _appDbOperator.GetBeatmapByIdentifiable(beatmap) ?? beatmap;

                PlayList.AddOrSwitchTo(trueBeatmap);
                var context = PlayList.CurrentInfo;
                context.OsuFile = osuFile;
                context.BeatmapDetail.MapPath = path;
                context.BeatmapDetail.BaseFolder = Path.GetDirectoryName(path);

                InitializeContextHandle(context);
                await LoadAsync(true, playInstantly).ConfigureAwait(false);
                if (playInstantly) context.PlayHandle.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                _readLock.Release();
            }
        }

        public async Task PlayPrevAsync()
        {
            await PlayByControl(PlayControlType.Previous, false).ConfigureAwait(false);
        }

        public async Task PlayNextAsync()
        {
            await PlayByControl(PlayControlType.Next, false).ConfigureAwait(false);
        }

        private async Task LoadAsync(bool isReading, bool playInstantly)
        {
            var context = PlayList.CurrentInfo;
            context.PlayInstantly = playInstantly;
            try
            {
                if (!isReading)
                {
                    await _readLock.WaitAsync(_cts.Token).ConfigureAwait(false);
                    await ClearPlayer();
                }

                var beatmap = context.Beatmap;
                Execute.OnUiThread(() => LoadStarted?.Invoke(context, _cts.Token));

                // meta
                var osuFile = context.OsuFile;
                var beatmapDetail = context.BeatmapDetail;

                if (osuFile == null)
                {
                    string path = beatmap.InOwnDb
                        ? Path.Combine(Domain.CustomSongPath, beatmap.FolderName, beatmap.BeatmapFileName)
                        : Path.Combine(Domain.OsuSongPath, beatmap.FolderName, beatmap.BeatmapFileName);
                    osuFile = await OsuFile.ReadFromFileAsync(path).ConfigureAwait(false);
                    context.OsuFile = osuFile;
                    beatmapDetail.MapPath = path;
                    beatmapDetail.BaseFolder = Path.GetDirectoryName(path);
                }

                var album = _appDbOperator.GetCollectionsByMap(context.BeatmapSettings);
                bool isFavorite = album != null && album.Any(k => k.LockedBool);

                var metadata = beatmapDetail.Metadata;
                metadata.IsFavorite = isFavorite;

                metadata.Artist = osuFile.Metadata.ArtistMeta;
                metadata.Title = osuFile.Metadata.TitleMeta;
                metadata.BeatmapId = osuFile.Metadata.BeatmapId;
                metadata.BeatmapsetId = osuFile.Metadata.BeatmapSetId;
                metadata.Creator = osuFile.Metadata.Creator;
                metadata.Version = osuFile.Metadata.Version;
                metadata.Source = osuFile.Metadata.Source;
                metadata.Tags = osuFile.Metadata.TagList;

                metadata.HP = osuFile.Difficulty.HpDrainRate;
                metadata.CS = osuFile.Difficulty.CircleSize;
                metadata.AR = osuFile.Difficulty.ApproachRate;
                metadata.OD = osuFile.Difficulty.OverallDifficulty;

                Execute.OnUiThread(() => MetaLoaded?.Invoke(context, _cts.Token));

                // background
                var defaultPath = Path.Combine(Domain.ResourcePath, "default.jpg");

                if (osuFile.Events.BackgroundInfo != null)
                {
                    var bgPath = Path.Combine(beatmapDetail.BaseFolder,
                        osuFile.Events.BackgroundInfo.Filename);
                    beatmapDetail.BackgroundPath = File.Exists(bgPath)
                        ? bgPath
                        : File.Exists(defaultPath) ? defaultPath : null;
                }
                else
                {
                    beatmapDetail.BackgroundPath = File.Exists(defaultPath)
                        ? defaultPath
                        : null;
                }

                Execute.OnUiThread(() => BackgroundInfoLoaded?.Invoke(context, _cts.Token));

                // music
                beatmapDetail.MusicPath = Path.Combine(beatmapDetail.BaseFolder,
                    osuFile.General.AudioFilename);

                if (PlayList.PreInfo?.BeatmapDetail?.BaseFolder != PlayList.CurrentInfo?.BeatmapDetail?.BaseFolder)
                {
                    CachedSound.ClearCacheSounds();
                }

                Player = new OsuMixPlayer(osuFile, beatmapDetail.BaseFolder);
                Player.PlayStatusChanged += Player_PlayStatusChanged;
                Player.PositionUpdated += Player_PositionUpdated;
                await Player.Initialize().ConfigureAwait(false); //700 ms
                Player.ManualOffset = context.BeatmapSettings.Offset;

                Execute.OnUiThread(() => MusicLoaded?.Invoke(context, _cts.Token));

                // video
                var videoName = osuFile.Events.VideoInfo?.Filename;

                if (videoName != null)
                {
                    var videoPath = Path.Combine(beatmapDetail.BaseFolder, videoName);
                    if (File.Exists(videoPath))
                    {
                        beatmapDetail.VideoPath = videoPath;
                        Execute.OnUiThread(() => VideoLoadRequested?.Invoke(context, _cts.Token));
                    }
                }

                // storyboard
                var analyzer = new OsuFileAnalyzer(osuFile);
                if (osuFile.Events.ElementGroup.ElementList.Count > 0)
                    Execute.OnUiThread(() => StoryboardLoadRequested?.Invoke(context, _cts.Token));
                else
                {
                    var osbFile = Path.Combine(beatmapDetail.BaseFolder, analyzer.OsbFileName);
                    if (File.Exists(osbFile) && await OsuFile.OsbFileHasStoryboard(osbFile).ConfigureAwait(false))
                        Execute.OnUiThread(() => StoryboardLoadRequested?.Invoke(context, _cts.Token));
                }

                context.FullLoaded = true;
                // load finished
                Execute.OnUiThread(() => LoadFinished?.Invoke(context, _cts.Token));
                AppSettings.Default.CurrentMap = beatmap.GetIdentity();
                AppSettings.SaveDefault();
                if (!isReading) _readLock.Release();
            }
            catch (Exception ex)
            {
                Notification.Push(@"发生未处理的错误：" + (ex.InnerException?.Message ?? ex?.Message));

                if (!isReading) _readLock.Release();
                if (Player?.PlayStatus != PlayStatus.Playing)
                {
                    await PlayByControl(PlayControlType.Next, false).ConfigureAwait(false);
                }
            }
            finally
            {
                _appDbOperator.UpdateMap(context.Beatmap.GetIdentity());
            }
        }

        private async Task ClearPlayer()
        {
            if (Player == null) return;
            PlayList.CurrentInfo.StopHandle();
            Player.PlayStatusChanged -= Player_PlayStatusChanged;
            Player.PositionUpdated -= Player_PositionUpdated;
            await Player.DisposeAsync();
        }

        private async void Player_PlayStatusChanged(PlayStatus obj)
        {
            Execute.OnUiThread(() => PlayStatusChanged?.Invoke(obj));
            SharedVm.Default.IsPlaying = obj == PlayStatus.Playing;
            if (obj == PlayStatus.Finished)
                await PlayByControl(PlayControlType.Next, true).ConfigureAwait(false);
        }

        private void Player_PositionUpdated(TimeSpan position)
        {
            Execute.OnUiThread(() => PositionUpdated?.Invoke(position));
        }

        private async Task PlayList_AutoSwitched(PlayControlResult controlResult, Beatmap beatmap, bool playInstantly)
        {
            var context = PlayList.CurrentInfo;

            if (controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Keep)
            {
                context.SetTimeHandle(0, playInstantly || controlResult.PlayStatus == PlayControlResult.PlayControlStatus.Play);
            }
            else if (controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Default ||
                     controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Reset)
            {
                InitializeContextHandle(context);
                await LoadAsync(false, true).ConfigureAwait(false);
                switch (controlResult.PlayStatus)
                {
                    case PlayControlResult.PlayControlStatus.Play:
                        if (playInstantly) context.PlayHandle();
                        break;
                    case PlayControlResult.PlayControlStatus.Stop:
                        context.StopHandle();
                        break;
                }
            }
            else if (controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Clear)
            {
                Execute.OnUiThread(() => InterfaceClearRequest?.Invoke());
            }

            await Task.CompletedTask;
        }

        private void PlayList_SongListChanged()
        {
            AppSettings.Default.CurrentList = new HashSet<MapIdentity>(PlayList.SongList.Select(k => k.GetIdentity()));
            AppSettings.SaveDefault();
        }

        private async Task PlayByControl(PlayControlType control, bool auto)
        {
            if (!auto)
            {
                InterruptPrevOperation();
            }

            var preInfo = PlayList.CurrentInfo;
            var controlResult = auto
                    ? await PlayList.InvokeAutoNext().ConfigureAwait(false)
                    : await PlayList.SwitchByControl(control).ConfigureAwait(false);
            if (controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Default &&
                controlResult.PlayStatus == PlayControlResult.PlayControlStatus.Play)
            {
                if (PlayList.CurrentInfo == null)
                {
                    await ClearPlayer();
                    Execute.OnUiThread(() => InterfaceClearRequest?.Invoke());
                    return;
                }

                if (preInfo == PlayList.CurrentInfo)
                {
                    PlayList.CurrentInfo.StopHandle();
                    PlayList.CurrentInfo.PlayHandle();
                    return;
                }

                InitializeContextHandle(PlayList.CurrentInfo);
                await LoadAsync(false, true).ConfigureAwait(false);
                PlayList.CurrentInfo.PlayHandle.Invoke();
            }
            else if (controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Keep)
            {
                switch (controlResult.PlayStatus)
                {
                    case PlayControlResult.PlayControlStatus.Play:
                        PlayList.CurrentInfo.RestartHandle.Invoke();
                        break;
                    case PlayControlResult.PlayControlStatus.Stop:
                        PlayList.CurrentInfo.StopHandle.Invoke();
                        break;
                }
            }
            else if (controlResult.PointerStatus == PlayControlResult.PointerControlStatus.Clear)
            {
                await ClearPlayer();
                Execute.OnUiThread(() => InterfaceClearRequest?.Invoke());
                return;
            }
        }

        private void InitializeContextHandle(BeatmapContext context)
        {
            context.PlayHandle = async () => await Player.Play().ConfigureAwait(false);
            context.PauseHandle = async () => await Player.Pause().ConfigureAwait(false);
            context.StopHandle = async () => await Player.Stop().ConfigureAwait(false);
            context.RestartHandle = () =>
            {
                context.StopHandle();
                context.PlayHandle();
            };
            context.TogglePlayHandle = () =>
            {
                if (Player.PlayStatus == PlayStatus.Ready ||
                    Player.PlayStatus == PlayStatus.Finished ||
                    Player.PlayStatus == PlayStatus.Paused)
                {
                    context.PlayHandle();
                }
                else if (Player.PlayStatus == PlayStatus.Playing) context.PauseHandle();
            };

            context.SetTimeHandle = async (time, play) =>
                await Player.SkipTo(TimeSpan.FromMilliseconds(time)).ConfigureAwait(false);
        }

        private void InterruptPrevOperation()
        {
            _cts.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        public async Task DisposeAsync()
        {
            if (_player != null) await _player?.DisposeAsync();
            _readLock?.Dispose();
            _cts?.Dispose();
        }
    }
}