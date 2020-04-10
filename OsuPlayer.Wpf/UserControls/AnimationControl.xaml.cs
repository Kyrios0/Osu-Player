﻿using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Common.Instances;
using Milky.OsuPlayer.Media.Audio;
using Milky.OsuPlayer.Media.Audio.Player;
using Milky.OsuPlayer.Media.Audio.Playlist;
using Milky.OsuPlayer.Presentation.Interaction;
using Milky.OsuPlayer.Shared;
using Milky.OsuPlayer.Shared.Dependency;
using Unosquare.FFME.Common;

namespace Milky.OsuPlayer.UserControls
{
    public class AnimationControlVm : VmBase
    {
        public SharedVm Player { get; } = SharedVm.Default;
    }

    /// <summary>
    /// AnimationControl.xaml 的交互逻辑
    /// </summary>
    public partial class AnimationControl : UserControl
    {
        private OsuDbInst _dbInst = Service.Get<OsuDbInst>();
        private readonly ObservablePlayController _controller = Service.Get<ObservablePlayController>();

        private bool _playAfterSeek;
        private Task _waitTask;
        private TimeSpan _initialVideoPosition;
        private MyCancellationTokenSource _waitActionCts;

        private double _videoOffset;

        private AnimationControlVm _viewModel;

        public AnimationControl()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            _viewModel = (AnimationControlVm)DataContext;

            var path = Path.Combine(Domain.ResourcePath, "default.jpg");
            if (File.Exists(path))
            {
                BackImage.Source = new BitmapImage(new Uri(path));
                BackImage.Opacity = 1;
            }

            if (_controller == null) return;
            _controller.LoadStarted += Controller_LoadStarted;
            _controller.BackgroundInfoLoaded += Controller_BackgroundInfoLoaded;
            _controller.VideoLoadRequested += Controller_VideoLoadRequested;

            _controller.PlayStatusChanged += Player_PlayStatusChanged;
        }

        private void Player_PlayStatusChanged(PlayStatus obj)
        {
            Execute.OnUiThread(() =>
            {
                if (VideoElement.Source is null) return;

                if (obj == PlayStatus.Playing)
                    VideoElement.Pause();
                else if (obj == PlayStatus.Finished || obj == PlayStatus.Paused)
                    VideoElement.Play();
            });
        }

        private async void Controller_LoadStarted(BeatmapContext arg1, CancellationToken arg2)
        {
            await SafelyRecreateVideoElement(_viewModel.Player.EnableVideo);

            Execute.OnUiThread(() =>
            {
                BackImage.Opacity = 1;
                BlendBorder.Visibility = Visibility.Collapsed;
            });

            AppSettings.Default.Play.PropertyChanged -= Play_PropertyChanged;
        }

        private void Controller_BackgroundInfoLoaded(BeatmapContext beatmapCtx, CancellationToken ct)
        {
            BackImage.Source = beatmapCtx.BeatmapDetail.BackgroundPath == null
                ? null
                : new BitmapImage(new Uri(beatmapCtx.BeatmapDetail.BackgroundPath));
        }

        private void Controller_VideoLoadRequested(BeatmapContext beatmapCtx, CancellationToken ct)
        {
            if (VideoElement == null) return;

            if (!SharedVm.Default.EnableVideo) return;

            _playAfterSeek = true;
            VideoElement.Source = new Uri(beatmapCtx.BeatmapDetail.VideoPath);

            _videoOffset = -(beatmapCtx.OsuFile.Events.VideoInfo.Offset);
            if (_videoOffset >= 0)
            {
                _waitTask = Task.Delay(0);
                _initialVideoPosition = TimeSpan.FromMilliseconds(_videoOffset);
            }
            else
            {
                _waitTask = Task.Delay(TimeSpan.FromMilliseconds(-_videoOffset));
            }

            Execute.OnUiThread(() =>
            {
                BackImage.Opacity = 0.15;
                BlendBorder.Visibility = Visibility.Visible;
            });

            beatmapCtx.PlayHandle = async () =>
            {
                await _controller.Player.Play();
                PlayVideo();
            };

            beatmapCtx.PauseHandle = async () =>
            {
                await _controller.Player.Pause();
                PauseVideo();
            };

            beatmapCtx.StopHandle = async () =>
            {
                await _controller.Player.Stop();
                ResetVideo(false);
            };

            beatmapCtx.SetTimeHandle = async (time, play) =>
            {
                await _controller.Player.Pause();
                _playAfterSeek = play;
                _waitActionCts = new MyCancellationTokenSource();
                Guid? guid = _waitActionCts?.Guid;
                var trueOffset = time + _videoOffset;
                if (trueOffset < 0)
                {
                    await VideoElement.Pause();
                    VideoElement.Position = TimeSpan.FromMilliseconds(0);

                    await Task.Run(() => { Thread.Sleep(TimeSpan.FromMilliseconds(-trueOffset)); });
                    if (_waitActionCts?.Guid != guid || _waitActionCts?.IsCancellationRequested == true)
                        return;
                }


                if (trueOffset >= 0)
                {
                    VideoElement.Position = TimeSpan.FromMilliseconds(trueOffset);
                }
            };

            AppSettings.Default.Play.PropertyChanged += Play_PropertyChanged;
        }

        private void PlayVideo()
        {
            VideoElement.Play();
        }

        private void PauseVideo()
        {
            VideoElement.Pause();
        }

        private void ResetVideo(bool play)
        {
            VideoElement.Position = TimeSpan.Zero;
        }

        private void Play_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.Play.PlaybackRate))
                VideoElement.SpeedRatio = AppSettings.Default.Play.PlaybackRate;
        }

        public bool IsBlur
        {
            get => (bool)GetValue(IsBlurProperty);
            set => SetValue(IsBlurProperty, value);
        }

        public static readonly DependencyProperty IsBlurProperty =
            DependencyProperty.Register("IsBlur",
                typeof(bool),
                typeof(AnimationControl),
                new PropertyMetadata(false, OnBlurChanged));

        private static void OnBlurChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is AnimationControl @this && e.NewValue is bool useEffect)) return;
            var effect = @this.Effect as BlurEffect;
            if ((AppSettings.Default.Interface.MinimalMode || !useEffect))
            {
                if (effect != null) effect.Radius = 0;
                @this.Effect = null;
            }
            else
            {
                if (effect != null) effect.Radius = 30;
                else @this.Effect = new BlurEffect { Radius = 30 };
            }
        }

        private async Task SafelyRecreateVideoElement(bool showVideo)
        {
            if (Execute.CheckDispatcherAccess())
            {
                VideoElement.Stop();
                BindVideoElement();
            }
            else
            {
                await VideoElement.Stop();
                Execute.OnUiThread(BindVideoElement);
            }

            void BindVideoElement()
            {
                VideoElement.Position = TimeSpan.Zero;
                VideoElement.Source = null;

                VideoElement.MediaOpened -= OnMediaOpened;
                VideoElement.MediaFailed -= OnMediaFailed;
                VideoElement.MediaEnded -= OnMediaEnded;
                VideoElement.SeekingStarted -= OnSeekingStarted;
                VideoElement.SeekingEnded -= OnSeekingEnded;
                VideoElement.Dispose();
                VideoElement = null;
                VideoElementBorder.Child = null;
                //VideoElementBorder.Visibility = Visibility.Hidden;
                VideoElement = new Unosquare.FFME.MediaElement
                {
                    IsMuted = true,
                    LoadedBehavior = MediaPlaybackState.Manual,
                    Visibility = Visibility.Visible,
                    SpeedRatio = AppSettings.Default.Play.PlaybackRate
                };
                VideoElement.MediaOpened += OnMediaOpened;
                VideoElement.MediaFailed += OnMediaFailed;
                VideoElement.MediaEnded += OnMediaEnded;

                if (showVideo)
                {
                    VideoElement.SeekingStarted += OnSeekingStarted;
                    VideoElement.SeekingEnded += OnSeekingEnded;
                }

                VideoElementBorder.Child = VideoElement;
            }
        }

        private void OnSeekingStarted(object sender, EventArgs e)
        {
        }

        private async void OnSeekingEnded(object sender, EventArgs e)
        {
            if (!SharedVm.Default.EnableVideo) return;
            await _controller.Player.SkipTo(VideoElement.Position - TimeSpan.FromMilliseconds(_videoOffset));
            if (_playAfterSeek)
            {
                await _controller.Player.Play();
                await VideoElement.Play();
            }
            else
            {
                await _controller.Player.Pause();
                await VideoElement.Pause();
            }
        }

        private async void OnMediaOpened(object sender, MediaOpenedEventArgs e)
        {
            VideoElementBorder.Visibility = Visibility.Visible;
            if (!SharedVm.Default.EnableVideo) return;
            await _waitTask;

            if (VideoElement == null /* || VideoElement.IsDisposed*/) return;
            if (_controller.PlayList.CurrentInfo.PlayInstantly)
            {
                await VideoElement.Play();
                VideoElement.Position = _initialVideoPosition;
            }
        }

        private async void OnMediaFailed(object sender, MediaFailedEventArgs e)
        {
            VideoElementBorder.Visibility = Visibility.Hidden;
            //MsgBox.Show(this, e.ErrorException.ToString(), "不支持的视频格式", MessageBoxButton.OK, MessageBoxImage.Error);
            if (!SharedVm.Default.EnableVideo) return;
            await SafelyRecreateVideoElement(false);
            _controller.Player.TogglePlay();
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            if (VideoElement == null /*|| VideoElement.IsDisposed*/) return;
            //VideoElement.Position = TimeSpan.Zero;
        }
    }
}
