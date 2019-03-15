﻿using Microsoft.Win32;
using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Common.Data;
using Milky.OsuPlayer.Common.Data.EF.Model;
using Milky.OsuPlayer.Common.Instances;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Control;
using Milky.OsuPlayer.Data;
using Milky.OsuPlayer.Instances;
using Milky.OsuPlayer.Media.Audio;
using Milky.OsuPlayer.Models;
using Milky.OsuPlayer.Pages;
using Milky.OsuPlayer.ViewModels;
using Milky.WpfApi;
using OSharp.Beatmap;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Milky.OsuPlayer.Windows
{
    partial class MainWindow
    {
        /// <summary>
        /// Call a file dialog to open custom file.
        /// </summary>
        public string LoadFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = @"请选择一个.osu文件",
                Filter = @"Osu Files(*.osu)|*.osu"
            };
            var result = openFileDialog.ShowDialog();
            return (result.HasValue && result.Value) ? openFileDialog.FileName : null;
        }

        public async Task PlayNewFile(string path)
        {
            await PlayNewFile(path, true);
        }

        private Action _waitAction;
        private MyCancellationTokenSource _waitActionCts;
        private TimeSpan _position;
        private bool _forcePaused;
        private bool _videoPlay;

        /// <summary>
        /// Play a new file by file path.
        /// </summary>
        private async Task PlayNewFile(string path, bool play)
        {
            var sw = Stopwatch.StartNew();

            var playerInst = InstanceManage.GetInstance<PlayersInst>();
            ComponentPlayer audioPlayer = null;
            var dbInst = InstanceManage.GetInstance<OsuDbInst>();
            if (path == null) return;
            if (File.Exists(path))
            {
                try
                {
                    var osuFile = await OsuFile.ReadFromFileAsync(path); //50 ms
                    var fi = new FileInfo(path);
                    if (!fi.Exists)
                        throw new FileNotFoundException("Cannot locate.", fi.FullName);
                    var dir = fi.Directory.FullName;

                    /* Clear */
                    ClearHitsoundPlayer();

                    /* Set new hitsound player*/
                    playerInst.SetAudioPlayer(path, osuFile); //todo: 700 ms
                    audioPlayer = playerInst.AudioPlayer;
                    await audioPlayer.InitializeAsync();
                    audioPlayer.ProgressRefreshInterval = 500;
                    SignUpPlayerEvent(audioPlayer);

                    /* Set Meta */
                    var nowIdentity = new MapIdentity(fi.Directory.Name, osuFile.Metadata.Version);

                    MapInfo mapInfo = DbOperate.GetMapFromDb(nowIdentity);
                    //BeatmapEntry entry = App.PlayerList.Entries.GetBeatmapByIdentity(nowIdentity);
                    BeatmapEntry entry = dbInst.Beatmaps.FilterByIdentity(nowIdentity);

                    LblTitle.Content = osuFile.Metadata.TitleMeta.ToUnicodeString();
                    LblArtist.Content = osuFile.Metadata.ArtistMeta.ToUnicodeString();
                    ((ToolTip)NotifyIcon.TrayToolTip).Content =
                        (string)LblArtist.Content + " - " + (string)LblTitle.Content;
                    bool isFaved = SetFaved(nowIdentity); //50 ms
                    audioPlayer.HitsoundOffset = mapInfo.Offset;
                    Offset.Value = audioPlayer.HitsoundOffset;

                    InstanceManage.GetInstance<PlayerList>().CurrentInfo =
                        new CurrentInfo(osuFile.Metadata.Artist,
                            osuFile.Metadata.ArtistUnicode,
                            osuFile.Metadata.Title,
                            osuFile.Metadata.TitleUnicode,
                            osuFile.Metadata.Creator,
                            osuFile.Metadata.Source,
                            osuFile.Metadata.TagList,
                            osuFile.Metadata.BeatmapId,
                            osuFile.Metadata.BeatmapSetId,
                            entry != null
                                ? (entry.DiffStarRatingStandard.ContainsKey(Mods.None)
                                    ? entry.DiffStarRatingStandard[Mods.None]
                                    : 0)
                                : 0,
                            osuFile.Difficulty.HpDrainRate,
                            osuFile.Difficulty.CircleSize,
                            osuFile.Difficulty.ApproachRate,
                            osuFile.Difficulty.OverallDifficulty,
                            audioPlayer?.Duration ?? 0,
                            nowIdentity,
                            mapInfo,
                            entry,
                            isFaved); // 20 ms

                    /* Set Lyric */
                    SetLyricSynchronously(); //todo: 900ms

                    /* Set Progress */
                    //PlayProgress.Value = App.HitsoundPlayer.SingleOffset;
                    PlayProgress.Maximum = audioPlayer.Duration;
                    PlayProgress.Value = 0;

                    PlayerViewModel.Current.Duration = InstanceManage.GetInstance<PlayersInst>().AudioPlayer.Duration;
                    //LblTotal.Content = new TimeSpan(0, 0, 0, 0, audioPlayer.Duration).ToString(@"mm\:ss");
                    //LblNow.Content = new TimeSpan(0, 0, 0, 0, audioPlayer.PlayTime).ToString(@"mm\:ss");

                    /* Set Storyboard */
                    if (true)
                    {
                        // Todo: Set Storyboard
                    }

                    /* Set Video */
                    bool showVideo = PlayerViewModel.Current.EnableVideo && !ViewModel.IsMiniMode;
                    if (VideoElement != null)
                    {
                        await ClearVideoElement(showVideo);

                        if (showVideo)
                        {
                            var videoName = osuFile.Events.VideoInfo?.Filename;
                            if (videoName == null)
                            {
                                VideoElement.Source = null;
                                VideoElementBorder.Visibility = System.Windows.Visibility.Hidden;
                            }
                            else
                            {
                                var vPath = Path.Combine(dir, videoName);
                                if (File.Exists(vPath))
                                {
                                    VideoElement.Source = new Uri(vPath);
                                    _videoOffset = -(osuFile.Events.VideoInfo.Offset);
                                    if (_videoOffset >= 0)
                                    {
                                        _waitAction = () => { };
                                        _position = TimeSpan.FromMilliseconds(_videoOffset);
                                    }
                                    else
                                    {
                                        _waitAction = () => { Thread.Sleep(TimeSpan.FromMilliseconds(-_videoOffset)); };
                                    }
                                }
                                else
                                {
                                    VideoElement.Source = null;
                                    VideoElementBorder.Visibility = System.Windows.Visibility.Hidden;
                                }
                            }
                        }
                    }

                    /* Set Background */
                    if (osuFile.Events.BackgroundInfo != null)
                    {
                        var bgPath = Path.Combine(dir, osuFile.Events.BackgroundInfo.Filename);
                        BlurScene.Source = File.Exists(bgPath) ? new BitmapImage(new Uri(bgPath)) : null;
                        Thumb.Source = File.Exists(bgPath) ? new BitmapImage(new Uri(bgPath)) : null;
                    }
                    else
                        BlurScene.Source = null;

                    /* Start Play */
                    switch (MainFrame.Content)
                    {
                        case RecentPlayPage recentPlayPage:
                            var item = recentPlayPage.DataModels.FirstOrDefault(k =>
                                k.GetIdentity().Equals(nowIdentity));
                            recentPlayPage.RecentList.SelectedItem = item;
                            break;
                        case CollectionPage collectionPage:
                            collectionPage.MapList.SelectedItem =
                                collectionPage.ViewModel.Beatmaps.FirstOrDefault(k =>
                                    k.GetIdentity().Equals(nowIdentity));
                            break;
                    }

                    _videoPlay = play;
                    if (play)
                    {
                        if (showVideo && VideoElement?.Source != null)
                        {
                            // use event to control here.
                            //VideoPlay();
                            //App.HitsoundPlayer.Play();
                        }
                        else
                            audioPlayer.Play();
                    }

                    //if (!App.PlayerList.Entries.Any(k => k.GetIdentity().Equals(nowIdentity)))
                    //    App.PlayerList.Entries.Add(entry);
                    PlayerConfig.Current.CurrentPath = path;
                    PlayerConfig.SaveCurrent();

                    //RunSurfaceUpdate();
                    DbOperate.UpdateMap(nowIdentity);
                }
                catch (RepeatTimingSectionException ex)
                {
                    var result = MsgBox.Show(this, @"铺面读取时发生问题：" + ex.Message, Title, MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.OK)
                    {
                        if (audioPlayer == null) return;
                        if (audioPlayer.PlayerStatus != PlayerStatus.Playing) await PlayNextAsync(false, true);
                    }
                }
                catch (BadOsuFormatException ex)
                {
                    var result = MsgBox.Show(this, @"铺面读取时发生问题：" + ex.Message, Title, MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.OK)
                    {
                        if (audioPlayer == null) return;
                        if (audioPlayer.PlayerStatus != PlayerStatus.Playing) await PlayNextAsync(false, true);
                    }
                }
                catch (VersionNotSupportedException ex)
                {
                    var result = MsgBox.Show(this, @"铺面读取时发生问题：" + ex.Message, Title, MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    if (result == MessageBoxResult.OK)
                    {
                        if (audioPlayer == null) return;
                        if (audioPlayer.PlayerStatus != PlayerStatus.Playing) await PlayNextAsync(false, true);
                    }
                }
                catch (Exception ex)
                {
                    var result = MsgBox.Show(this, @"发生未处理的异常问题：" + (ex.InnerException ?? ex), Title,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    if (result == MessageBoxResult.OK)
                    {
                        if (audioPlayer == null) return;
                        if (audioPlayer.PlayerStatus != PlayerStatus.Playing) await PlayNextAsync(false, true);
                    }

                    Console.WriteLine(ex);
                }
            }
            else
            {
                MsgBox.Show(this, string.Format(@"所选文件不存在{0}。", InstanceManage.GetInstance<OsuDbInst>().Beatmaps == null
                        ? ""
                        : " ，可能是db没有及时更新。请关闭此播放器或osu后重试"),
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        private void SignUpPlayerEvent(ComponentPlayer audioPlayer)
        {
            audioPlayer.PlayerLoaded += (sender, e) =>
            {
                var player = (ComponentPlayer)sender;
                Console.WriteLine(player.OsuFile.ToString() + @" PlayerLoaded.");
            };
            audioPlayer.PlayerFinished += async (sender, e) =>
            {
                await PlayNextAsync(false, true);
            };
            audioPlayer.PlayerPaused += (sender, e) =>
            {
                PlayerViewModel.Current.IsPlaying = false;
                PlayerViewModel.Current.Position = e.Position;
            };
            audioPlayer.PositionSet += (sender, e) =>
            {

            };
            audioPlayer.PlayerStarted += (sender, e) =>
            {
                PlayerViewModel.Current.IsPlaying = true;
                PlayerViewModel.Current.Position = e.Position;
            };
            audioPlayer.PlayerStopped += (sender, e) =>
            {

            };
            audioPlayer.PositionChanged += (sender, e) =>
            {
                if (!_scrollLock)
                {
                    PlayerViewModel.Current.Position = e.Position;
                    PlayProgress.Value = e.Position;
                }
            };
        }

        //private async void VideoPlay()
        //{
        //    //await VideoElement.Pause();

        //    //await VideoElement.Play();
        //}

        private async Task VideoJumpTo(int milliseconds)
        {
            _waitActionCts = new MyCancellationTokenSource();
            Guid? guid = _waitActionCts?.Guid;
            var trueOffset = milliseconds + _videoOffset;
            if (trueOffset < 0)
            {
                await VideoElement.Pause();
                VideoElement.Position = TimeSpan.FromMilliseconds(0);

                await Task.Run(() => { Thread.Sleep(TimeSpan.FromMilliseconds(-trueOffset)); });
                if (_waitActionCts?.Guid != guid || _waitActionCts?.IsCancellationRequested == true)
                    return;
                if (!_forcePaused)
                    await VideoElement.Play();
                else
                    await VideoElement.Pause();
            }
            else
            {
                //if (_mediaEnded)
                //{
                //    VideoElement.Position = TimeSpan.FromMilliseconds(0);
                //    await Task.Run(() => { Thread.Sleep(10); });
                //}
                if (!_forcePaused)
                    await VideoElement.Play();
                else
                    await VideoElement.Pause();
                VideoElement.Position = TimeSpan.FromMilliseconds(trueOffset);
            }
        }

        private async Task ClearVideoElement(bool seek)
        {
            await Task.Run(async () =>
            {
                await VideoElement.Stop();
                Dispatcher.Invoke(() =>
                {
                    VideoElement.Position = new TimeSpan(0);
                    VideoElement.Source = null;
                    VideoElement.Dispose();
                    VideoElement = null;
                    VideoElementBorder.Visibility = Visibility.Hidden;
                    VideoElement = new Unosquare.FFME.MediaElement
                    {
                        IsMuted = true,
                        LoadedBehavior = MediaState.Manual,
                        Visibility = System.Windows.Visibility.Visible,
                    };
                    VideoElement.MediaOpened += VideoElement_MediaOpened;
                    VideoElement.MediaFailed += VideoElement_MediaFailed;
                    VideoElement.MediaEnded += (sender, e) => { VideoElement.Position = TimeSpan.FromSeconds(0); };
                    if (seek)
                    {
                        VideoElement.SeekingStarted += (sender, e) => { };
                        VideoElement.SeekingEnded += (sender, e) =>
                        {
                            if (!_videoPlay) return;
                            PlayMedia();
                        };
                    }
                    VideoElementBorder.Children.Add(VideoElement);
                });
            });
        }

        private void PlayMedia()
        {
            if (_forcePaused) return;
            InstanceManage.GetInstance<PlayersInst>().AudioPlayer.Play();
            // Todo: Set Storyboard
        }

        private void PauseMedia()
        {
            InstanceManage.GetInstance<PlayersInst>().AudioPlayer.Pause();
            // Todo: Set Storyboard
        }

        /// <summary>
        /// Play next song in list if list exist.
        /// </summary>
        /// <param name="isManual">Whether it is called by user (Click next button manually)
        /// or called by application (A song finshed).</param>
        /// <param name="isNext"></param>
        private async Task PlayNextAsync(bool isManual, bool isNext)
        {
            if (InstanceManage.GetInstance<PlayersInst>().AudioPlayer == null) return;
            (PlayerList.ChangeType result, BeatmapEntry entry) = await InstanceManage.GetInstance<PlayerList>().PlayToAsync(isNext, isManual);
            switch (result)
            {
                //case PlayerList.ChangeType.Keep:
                //    await VideoJumpTo(0);
                //    App.StoryboardProvider?.StoryboardTiming.SetTiming(0, false);
                //    PlayMedia();
                //    break;

                case PlayerList.ChangeType.Stop:
                    PlayerViewModel.Current.IsPlaying = false;
                    PlayerViewModel.Current.Position = 0;
                    _videoPlay = false;
                    _forcePaused = true;
                    break;
                case PlayerList.ChangeType.Change:
                default:
                    var path = Path.Combine(new FileInfo(PlayerConfig.Current.General.DbPath).Directory.FullName, "Songs",
                        entry.FolderName, entry.BeatmapFileName);
                    await PlayNewFile(path);
                    break;
            }
        }

        private void ClearHitsoundPlayer()
        {
            InstanceManage.GetInstance<PlayersInst>().ClearAudioPlayer();
        }
    }

    internal class MyCancellationTokenSource : CancellationTokenSource
    {
        public Guid Guid { get; }

        public MyCancellationTokenSource()
        {
            Guid = Guid.NewGuid();
        }
    }
}
