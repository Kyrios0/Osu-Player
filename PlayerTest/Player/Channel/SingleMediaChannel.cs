﻿using NAudio.Wave;
using PlayerTest.SoundTouch;
using PlayerTest.Wave;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlayerTest.Player.Channel
{
    public class SingleMediaChannel : Subchannel
    {
        private readonly string _path;
        private readonly float _playbackRate;
        private readonly bool _useTempo;

        private MyAudioFileReader _fileReader;
        private VarispeedSampleProvider _speedProvider;
        private ISampleProvider _actualRoot;

        public override TimeSpan Duration { get; protected set; }

        public TimeSpan ReferenceDuration =>
            Duration.Add(TimeSpan.FromMilliseconds(AppSettings.Default.Play.GeneralActualOffset));
        
        public TimeSpan ReferencePosition =>
            Position.Add(TimeSpan.FromMilliseconds(AppSettings.Default.Play.GeneralActualOffset));

        public override float PlaybackRate { get; protected set; }
        public override bool UseTempo { get; protected set; }

        public SingleMediaChannel(AudioPlaybackEngine engine, string path, float playbackRate, bool useTempo) :
            base(engine)
        {
            _path = path;
            _playbackRate = playbackRate;
            _useTempo = useTempo;
        }

        public override async Task Initialize()
        {
            var type = StreamType.Wav;
            _fileReader = await WaveFormatFactory.GetResampledAudioFileReader(_path, type);

            _speedProvider = new VarispeedSampleProvider(_fileReader,
                10,
                new SoundTouchProfile(_useTempo, false)
            )
            {
                PlaybackRate = _playbackRate
            };

            await CachedSound.CreateCacheSounds(new[] { _path });

            Duration = _fileReader.TotalTime;

            SampleControl.Volume = 1;
            SampleControl.Balance = 0;
            PlayStatus = PlayStatus.Ready;
            await Task.CompletedTask;
        }

        public override async Task Play()
        {
            if (!Engine.RootMixer.MixerInputs.Contains(_speedProvider))
                Engine.RootMixer.AddMixerInput(_speedProvider, SampleControl, out _actualRoot);
            PlayStatus = PlayStatus.Playing;
            await Task.CompletedTask;
        }

        public override async Task Pause()
        {
            Engine.RootMixer.RemoveMixerInput(_actualRoot);
            PlayStatus = PlayStatus.Paused;
            await Task.CompletedTask;
        }

        public override async Task Stop()
        {
            Engine.RootMixer.RemoveMixerInput(_actualRoot);
            await SkipTo(TimeSpan.Zero);
            PlayStatus = PlayStatus.Paused;
            await Task.CompletedTask;
        }

        public override async Task Restart()
        {
            await SkipTo(TimeSpan.Zero);
            await Play();
            await Task.CompletedTask;
        }

        public override async Task SkipTo(TimeSpan time)
        {
            var status = PlayStatus;
            PlayStatus = PlayStatus.Reposition;

            _fileReader.CurrentTime = time >= _fileReader.TotalTime
                ? _fileReader.TotalTime - TimeSpan.FromMilliseconds(1)
                : time;
            _speedProvider.Reposition();

            PlayStatus = status;
            await Task.CompletedTask;
        }

        public override async Task SetPlaybackRate(float rate, bool useTempo)
        {
            if (!PlaybackRate.Equals(rate))
            {
                PlaybackRate = rate;
                _speedProvider.PlaybackRate = rate;
            }

            if (UseTempo != useTempo)
            {
                _speedProvider.SetSoundTouchProfile(new SoundTouchProfile(_useTempo, false));
            }

            await Task.CompletedTask;
        }
    }
}