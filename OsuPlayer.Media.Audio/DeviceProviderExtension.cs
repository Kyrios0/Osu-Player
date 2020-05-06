﻿using Milky.OsuPlayer.Common.Configuration;
using NAudio.Wave;
using OsuPlayer.Devices;

namespace Milky.OsuPlayer.Media.Audio
{
    public static class DeviceProviderExtension
    {
        public static IWavePlayer CreateOrGetDefaultDevice(out IDeviceInfo actualDeviceInfo)
        {
            var play = AppSettings.Default?.Play;
            if (play != null)
                return DeviceProvider.CreateDevice(out actualDeviceInfo, play.DeviceInfo, play.DesiredLatency, play.IsExclusive);
            return DeviceProvider.CreateDevice(out actualDeviceInfo);
        }
    }
}