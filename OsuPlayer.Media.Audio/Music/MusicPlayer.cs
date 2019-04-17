﻿using Milky.OsuPlayer.Common;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Media.Audio.Music.SampleProviders;
using Milky.OsuPlayer.Media.Audio.Music.SoundTouch;
using Milky.OsuPlayer.Media.Audio.Music.WaveProviders;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Milky.OsuPlayer.Media.Audio.Music
{
    internal sealed class MusicPlayer : Player, IDisposable
    {
        private const int Latency = 5;
        private static bool UseSoundTouch => PlayerConfig.Current.Play.UsePlayerV2;
        private static bool WaitingMode => true;

        private TimeStretchProfile TimeStretchProfile
        {
            get
            {
                lock (_propertiesLock) { return _timeStretchProfile; }
            }
            set
            {
                lock (_propertiesLock)
                {
                    _timeStretchProfile = value;
                }
            }
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private PlayerStatus _playerStatus;

        private readonly object _propertiesLock = new object();
        private IWavePlayer _device;
        private MyAudioFileReader _reader;

        #region SoundTouch requirement

        private BlockAlignReductionStream _blockAlignReductionStream;
        private WaveChannel32 _waveChannel;
        private CusBufferedWaveProvider _provider;

        private static SoundTouchApi _soundTouch;
        private TimeStretchProfile _timeStretchProfile;

        private Thread _playThread;
        private bool _stopWorker;

        #endregion

        private bool _tempoChanged;
        private bool _pitchChanged;
        private bool _rateChanged;
        private float _tempoValue = 1f;
        private float _pitchValue = 0f;
        private float _rateValue = 1f;
        private int _progressRefreshInterval;

        private string _filePath;

        public MusicPlayer(string filePath)
        {
            _filePath = filePath;
        }

        public override async Task InitializeAsync()
        {
            FileInfo fi = new FileInfo(_filePath);
            if (!fi.Exists)
            {
                //throw new FileNotFoundException("找不到音乐文件…", fi.FullName);
                _filePath = Path.Combine(Domain.DefaultPath, "blank.wav");
            }

            _reader = new MyAudioFileReader(_filePath);
            //if (UseSoundTouch)
            _device = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, Latency);
            //else
            //    _device = new WaveOutEvent { DesiredLatency = App.Config.Play.DesiredLatency };
            //_device = new WaveOutEvent { DesiredLatency = 70 };
            _device.PlaybackStopped += (sender, args) =>
            {
                PlayerStatus = PlayerStatus.Finished;
                RaisePlayerFinishedEvent(this, new EventArgs());
            };

            if (UseSoundTouch)
            {
                // init provider
                _blockAlignReductionStream = new BlockAlignReductionStream(_reader);
                _waveChannel = new WaveChannel32(_blockAlignReductionStream);
                _provider = new CusBufferedWaveProvider(_waveChannel.WaveFormat);

                InitSoundTouch();
                _device.Init(_provider);
                SetPlayMod(PlayerConfig.Current.Play.PlayMod);
                _playThread = new Thread(ProcessWave) { Name = "PlayThread" };
                _playThread.Start();
            }
            else
            {
                _device.Init(_reader);
            }

            PlayerConfig.Current.Volume.PropertyChanged += Volume_PropertyChanged;
            Task.Factory.StartNew(UpdateProgress, TaskCreationOptions.LongRunning);

            PlayerStatus = PlayerStatus.Ready;
            RaisePlayerLoadedEvent(this, new EventArgs());
        }

        private void UpdateProgress()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_reader != null && PlayerStatus != PlayerStatus.NotInitialized && PlayerStatus != PlayerStatus.Finished)
                {
                    //_reader.Volume = 1f * PlayerConfig.Current.Volume.Music * PlayerConfig.Current.Volume.Main;
                    PlayTime = (int)_reader?.CurrentTime.TotalMilliseconds;
                    //if (PlayTime >= (int)_reader?.TotalTime.TotalMilliseconds)
                    //    PlayerStatus = PlayerStatus.Finished;
                }

                Thread.Sleep(10);
            }
        }

        private void Volume_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _reader.Volume = 1f * PlayerConfig.Current.Volume.Music * PlayerConfig.Current.Volume.Main;
        }

        public void SetPlayMod(PlayMod mod)
        {
            switch (mod)
            {
                case PlayMod.None:
                    ResetMod();
                    break;
                case PlayMod.DoubleTime:
                    ResetMod();
                    RateValue = 1.5f;
                    ResetMod();
                    TempoValue = 1.5f;
                    break;
                case PlayMod.NightCore:
                    ResetMod();
                    RateValue = 1.5f;
                    break;
                case PlayMod.HalfTime:
                    ResetMod();
                    TempoValue = 0.75f;
                    break;
                case PlayMod.DayCore:
                    ResetMod();
                    RateValue = 0.75f;
                    break;
                case PlayMod.Custom:
                    break;
            }
        }

        private void ResetMod()
        {
            RateValue = 1;
            TempoValue = 1;
            PitchValue = 0;
        }

        public override void Play()
        {
            PlayWithoutNotify();

            PlayerStatus = PlayerStatus.Playing;
            RaisePlayerStartedEvent(this, new ProgressEventArgs(PlayTime, Duration));
        }

        private void PlayWithoutNotify()
        {
            _device.Play();
        }

        public override void Pause()
        {
            PauseWithoutNotify();

            PlayerStatus = PlayerStatus.Paused;
            RaisePlayerPausedEvent(this, new ProgressEventArgs(PlayTime, Duration));
        }

        private void PauseWithoutNotify()
        {
            _device?.Pause();
        }

        public override void Replay()
        {
            SetTime(0);
            Play();
        }

        public override void SetTime(int ms, bool play = true)
        {
            if (ms < 0) ms = 0;
            var span = new TimeSpan(0, 0, 0, 0, ms);
            if (_reader != null)
            {
                _reader.CurrentTime = span >= _reader.TotalTime ? _reader.TotalTime - new TimeSpan(0, 0, 0, 0, 1) : span;
            }
            //PlayerStatus = PlayerStatus.Playing;
            if (!play) PauseWithoutNotify();
        }

        public override void Stop()
        {
            ResetWithoutNotify();
            RaisePlayerStoppedEvent(this, new EventArgs());
        }

        internal void ResetWithoutNotify()
        {
            SetTime(0, false);
            PlayerStatus = PlayerStatus.Stopped;
        }

        public override void Dispose()
        {
            base.Dispose();

            _cts.Cancel();
            _playThread?.Abort();
            _soundTouch?.Dispose();
            _blockAlignReductionStream?.Dispose();
            _device?.Dispose();
            _device = null;
            _reader?.Dispose();
            _reader = null;
            _cts?.Dispose();

            PlayerConfig.Current.Volume.PropertyChanged -= Volume_PropertyChanged;
        }

        #region Private methods

        private void InitSoundTouch()
        {
            // 初始化 soundTouch
            _soundTouch = new SoundTouchApi();
            _soundTouch.CreateInstance();

            _soundTouch.SetSampleRate(_waveChannel.WaveFormat.SampleRate);
            _soundTouch.SetChannels(_waveChannel.WaveFormat.Channels);
            _soundTouch.SetTempoChange(0f);
            _soundTouch.SetPitchSemiTones(0f);
            _soundTouch.SetRateChange(0f);

            _soundTouch.SetTempo(TempoValue);
            TimeStretchProfile = new TimeStretchProfile
            {
                AAFilterLength = 128,
                Description = "demo1",
                Id = "demo",
                Overlap = 20,
                SeekWindow = 80,
                Sequence = 20,
                UseAAFilter = true
            };

            _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_USE_AA_FILTER, TimeStretchProfile.UseAAFilter ? 1 : 0);
            _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_AA_FILTER_LENGTH, TimeStretchProfile.AAFilterLength);
            _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_OVERLAP_MS, TimeStretchProfile.Overlap);
            _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEQUENCE_MS, TimeStretchProfile.Sequence);
            _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEEKWINDOW_MS, TimeStretchProfile.SeekWindow);

            _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEQUENCE_MS, 0);
        }

        private void ApplySoundTouchTimeStretchProfile()
        {
            // "Disable" sound touch AA and revert to Automatic settings at regular tempo (to remove side effects)
            if (Math.Abs(TempoValue - 1) < 0.001)
            {
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_USE_AA_FILTER, 0);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_AA_FILTER_LENGTH, 0);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_OVERLAP_MS, 0);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEQUENCE_MS, 0);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEEKWINDOW_MS, 0);
            }
            else
            {
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_USE_AA_FILTER, TimeStretchProfile.UseAAFilter ? 1 : 0);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_AA_FILTER_LENGTH, TimeStretchProfile.AAFilterLength);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_OVERLAP_MS, TimeStretchProfile.Overlap);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEQUENCE_MS, TimeStretchProfile.Sequence);
                _soundTouch.SetSetting(SoundTouchApi.SoundTouchSettings.SETTING_SEEKWINDOW_MS, TimeStretchProfile.SeekWindow);
            }
        }

        private void SetSoundTouchValues()
        {
            if (_tempoChanged)
            {
                float tempo = this.TempoValue;
                _soundTouch.SetTempo(tempo);
                _tempoChanged = false;
                ApplySoundTouchTimeStretchProfile();
            }

            if (_pitchChanged)
            {
                float pitch = this.PitchValue;
                _soundTouch.SetPitchSemiTones(pitch);
                _pitchChanged = false;
            }

            if (_rateChanged)
            {
                float rate = this.RateValue;
                _soundTouch.SetRate(rate);
                _rateChanged = false;
            }
        }

        private void ProcessWave()
        {
            const int bufferSize = 1024 * 10;
            byte[] inputBuffer = new byte[bufferSize * sizeof(float)];
            byte[] soundTouchOutBuffer = new byte[bufferSize * sizeof(float)];

            ByteAndFloatsConverter convertInputBuffer = new ByteAndFloatsConverter { Bytes = inputBuffer };
            ByteAndFloatsConverter convertOutputBuffer = new ByteAndFloatsConverter { Bytes = soundTouchOutBuffer };

            byte[] buffer = new byte[bufferSize];
            _stopWorker = false;
            while (!_stopWorker && _waveChannel.Position < _waveChannel.Length)
            {
                int bytesRead = _waveChannel.Read(convertInputBuffer.Bytes, 0, convertInputBuffer.Bytes.Length);
                //bytesRead = _waveChannel.Read(buffer, 0, BUFFER_SIZE);
                //bytesRead = _reader.Read(convertInputBuffer.Bytes, 0, convertInputBuffer.Bytes.Length);
                
                int floatsRead = bytesRead / ((sizeof(float)) * _waveChannel.WaveFormat.Channels);
                _soundTouch.PutSamples(convertInputBuffer.Floats, (uint)floatsRead);

                uint receivecount;

                do
                {
                    if (WaitingMode) SetSoundTouchValues();

                    uint outBufferSizeFloats = (uint)convertOutputBuffer.Bytes.Length /
                                               (uint)(sizeof(float) * _waveChannel.WaveFormat.Channels);

                    receivecount = _soundTouch.ReceiveSamples(convertOutputBuffer.Floats, outBufferSizeFloats);

                    if (receivecount > 0)
                    {
                        _provider.AddSamples(convertOutputBuffer.Bytes, 0,
                            (int)receivecount * sizeof(float) * _reader.WaveFormat.Channels, _reader.CurrentTime);

                        while (_provider.BuffersCount > 3)
                        {
                            Thread.Sleep(10);
                        }
                    }
                } while (!_stopWorker && receivecount != 0);
            }

            _reader.Close();
        }

        #endregion

        #region Properties

        public override int ProgressRefreshInterval
        {
            get => _progressRefreshInterval;
            set
            {
                if (value < 10)
                    _progressRefreshInterval = 10;
                _progressRefreshInterval = value;
            }
        }

        public override PlayerStatus PlayerStatus
        {
            get => _playerStatus;
            protected set
            {
                Console.WriteLine(@"Music: " + value);
                _playerStatus = value;
            }
        }

        public override int Duration
        {
            get => (int)_reader.TotalTime.TotalMilliseconds;
            protected set => throw new InvalidOperationException();
        }

        public override int PlayTime { get; protected set; }

        public float TempoValue
        {
            get => _tempoValue;
            set
            {
                if (!UseSoundTouch)
                    throw new NotSupportedException();
                if (WaitingMode)
                    _tempoChanged = true;
                else
                {
                    _soundTouch.SetTempo(value);
                    ApplySoundTouchTimeStretchProfile();
                }

                _tempoValue = value;
            }
        }

        public float PitchValue
        {
            get => _pitchValue;
            set
            {
                if (!UseSoundTouch)
                    throw new NotSupportedException();
                if (WaitingMode)
                    _pitchChanged = true;
                else
                {
                    _soundTouch.SetPitch(value);
                }
                _pitchValue = value;
            }
        }

        public float RateValue
        {
            get => _rateValue;
            set
            {
                if (!UseSoundTouch)
                    throw new NotSupportedException();
                if (WaitingMode)
                    _rateChanged = true;
                else
                {
                    _soundTouch.SetRate(value);
                }
                _rateValue = value;
            }
        }

        #endregion
    }
}
