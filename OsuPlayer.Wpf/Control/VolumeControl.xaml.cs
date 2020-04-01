﻿using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Common.Data;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Media.Audio;
using Milky.WpfApi;
using NAudio.Wave;
using OsuPlayer.Devices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Milky.OsuPlayer.Control
{
    public class VolumeControlVm : ViewModelBase
    {
        public SharedVm Shared { get; } = SharedVm.Default;
    }

    /// <summary>
    /// VolumeControl.xaml 的交互逻辑
    /// </summary>
    public partial class VolumeControl : UserControl
    {
        private readonly ObservablePlayController _controller = Services.Get<ObservablePlayController>();

        private readonly AppDbOperator _dbOperator = new AppDbOperator();
        private IWavePlayer _device;

        public VolumeControl()
        {
            InitializeComponent();
        }

        private void VolumeControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            _device = DeviceProvider.GetCurrentDevice();
            if (_device is AsioOut asio)
            {
                BtnAsio.Visibility = Visibility.Visible;
            }
            else
            {
                BtnAsio.Visibility = Visibility.Collapsed;
            }


            Offset.Value = _controller.PlayList.CurrentInfo?.BeatmapSettings?.Offset ?? 0;
            _controller.LoadFinished += Controller_LoadFinished;
        }

        private void Controller_LoadFinished(BeatmapContext bc, System.Threading.CancellationToken arg2)
        {
            Offset.Value = bc.BeatmapSettings.Offset;
        }

        private void MasterVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            AppSettings.SaveDefault();
        }

        private void MusicVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            AppSettings.SaveDefault();
        }

        private void HitsoundVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            AppSettings.SaveDefault();
        }

        private void SampleVolume_DragComplete(object sender, DragCompletedEventArgs e)
        {
            AppSettings.SaveDefault();
        }

        private void Balance_DragComplete(object sender, DragCompletedEventArgs e)
        {
            AppSettings.SaveDefault();
        }

        private void Offset_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_controller.Player == null)
                return;
            _controller.Player.ManualOffset = (int)Offset.Value;
        }

        private void Offset_DragComplete(object sender, DragCompletedEventArgs e)
        {
            _dbOperator.UpdateMap(_controller.PlayList.CurrentInfo.Beatmap, _controller.Player.ManualOffset);
        }

        private void BtnAsio_OnClick(object sender, RoutedEventArgs e)
        {
            if (_device is AsioOut asio)
            {
                asio.ShowControlPanel();
            }
        }

        private async void BtnPlayMod_OnClick(object sender, RoutedEventArgs e)
        {
            await _controller.Player.SetPlayMod((PlayModifier)((Button)sender).Tag);
        }
    }
}
