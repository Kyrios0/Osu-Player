﻿using Milky.OsuPlayer.Common.Player;
using Milky.OsuPlayer.Media.Audio.Music;
using OSharp.Beatmap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Milky.OsuPlayer.Common.Configuration;

namespace Milky.OsuPlayer.Media.Audio
{
    public sealed class ComponentPlayer : Player, IDisposable
    {
        private string _filePath;
        private int _stopCount;

        public override int ProgressRefreshInterval { get; set; } = 500;

        public OsuFile OsuFile { get; private set; }
        internal HitsoundPlayer HitsoundPlayer { get; private set; }
        internal SampleTrackPlayer SampleTrackPlayer { get; private set; }
        internal MusicPlayer MusicPlayer { get; private set; }

        private CancellationTokenSource _cts = new CancellationTokenSource();

        public override PlayerStatus PlayerStatus
        {
            get => HitsoundPlayer?.PlayerStatus ?? PlayerStatus.Stopped;
            protected set => throw new InvalidOperationException();
        }

        public override int Duration
        {
            get => HitsoundPlayer?.Duration ?? 0;
            protected set => throw new InvalidOperationException();
        }

        public override int PlayTime
        {
            get => HitsoundPlayer?.PlayTime ?? 0;
            protected set => throw new InvalidOperationException();
        }

        public int HitsoundOffset
        {
            get => HitsoundPlayer.SingleOffset;
            set => HitsoundPlayer.SingleOffset = value;
        }

        public static ComponentPlayer Current { get; set; }

        public ComponentPlayer(string filePath, OsuFile osuFile)
        {
            _filePath = filePath;
            OsuFile = osuFile;
        }

        public override async Task InitializeAsync()
        {
            Current?.Dispose();
            Current = this;

            FileInfo fileInfo = new FileInfo(_filePath);
            DirectoryInfo dirInfo = fileInfo.Directory;
            FileInfo musicInfo = new FileInfo(Path.Combine(dirInfo.FullName, OsuFile.General.AudioFilename));
            HitsoundPlayer = new HitsoundPlayer(_filePath, OsuFile);
            SampleTrackPlayer = new SampleTrackPlayer(_filePath, OsuFile);
            MusicPlayer = new MusicPlayer(musicInfo.FullName);

            await HitsoundPlayer.InitializeAsync();
            await SampleTrackPlayer.InitializeAsync();
            await MusicPlayer.InitializeAsync();

            HitsoundPlayer.SetDuration(MusicPlayer.Duration);
            HitsoundPlayer.PlayerFinished += Players_OnFinished;
            SampleTrackPlayer.SetDuration(MusicPlayer.Duration);
            SampleTrackPlayer.PlayerFinished += Players_OnFinished;
            MusicPlayer.PlayerFinished += Players_OnFinished;

            NotifyProgress(_cts.Token);

            RaisePlayerLoadedEvent(this, new EventArgs());
        }

        private void Players_OnFinished(object sender, EventArgs e)
        {
            _stopCount++;
            if (_stopCount < 3) return;

            ResetWithoutNotify();

            RaisePlayerFinishedEvent(this, new EventArgs());
        }

        public override void Play()
        {
            if (HitsoundPlayer.IsPlaying)
                return;

            _stopCount = 0;
            MusicPlayer.Play();
            HitsoundPlayer.Play();
            SampleTrackPlayer.Play();
            RaisePlayerStartedEvent(this, new ProgressEventArgs(PlayTime, Duration));
        }

        public override void Pause()
        {
            MusicPlayer.Pause();
            HitsoundPlayer.Pause();
            SampleTrackPlayer.Pause();

            RaisePlayerPausedEvent(this, new ProgressEventArgs(PlayTime, Duration));
        }

        public override void Stop()
        {
            ResetWithoutNotify();
            RaisePlayerStoppedEvent(this, new EventArgs());
        }

        private void ResetWithoutNotify()
        {
            HitsoundPlayer?.ResetWithoutNotify();
            SampleTrackPlayer?.ResetWithoutNotify();
            MusicPlayer?.ResetWithoutNotify();
        }

        public override void Replay()
        {
            Stop();
            Play();
        }

        public override void SetTime(int ms, bool play = true)
        {
            HitsoundPlayer.SetTime(ms, play);
            SampleTrackPlayer.SetTime(ms, play);
            if (play)
            {
                MusicPlayer.SetTime(ms);
                HitsoundPlayer.Play();
                SampleTrackPlayer.Play();
            }
            else
                MusicPlayer.SetTime(ms, false);
        }

        public void SetPlayMod(PlayMod mod, bool play)
        {
            //MusicPlayer.SetPlayMod(mod);
            HitsoundPlayer.SetPlayMod(mod, play);
            SampleTrackPlayer.SetPlayMod(mod, play);
        }

        public override void Dispose()
        {
            base.Dispose();

            _cts?.Cancel();
            _cts?.Dispose();
            HitsoundPlayer?.Dispose();
            SampleTrackPlayer?.Dispose();
            MusicPlayer?.Dispose();
            Current = null;
        }
    }

    internal class SampleTrackPlayer : HitsoundPlayer
    {
        public SampleTrackPlayer(string filePath, OsuFile osuFile) : base(filePath, osuFile)
        {
        }
        protected override void InitVolume()
        {
            Engine.Volume = 1f * PlayerConfig.Current.Volume.Sample * PlayerConfig.Current.Volume.Main;
        }

        protected override void Volume_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Engine.Volume = 1f * PlayerConfig.Current.Volume.Sample * PlayerConfig.Current.Volume.Main;
        }

        protected override List<HitsoundElement> FillHitsoundList(OsuFile osuFile, DirectoryInfo dirInfo)
        {
            List<HitsoundElement> hitsoundList = new List<HitsoundElement>();
            var sampleList = osuFile.Events.SampleInfo;
            if (sampleList == null)
                return hitsoundList;
            foreach (var sampleData in sampleList)
            {
                var element = new HitsoundElement(
                    mapFolderName: dirInfo.FullName,
                    mapWaveFiles: new string [0],
                    gameMode: osuFile.General.Mode,
                    offset: sampleData.Offset,
                    track: -1,
                    lineSample: OSharp.Beatmap.Sections.Timing.TimingSamplesetType.None,
                    hitsound: OSharp.Beatmap.Sections.HitObject.HitsoundType.Normal,
                    sample: OSharp.Beatmap.Sections.HitObject.ObjectSamplesetType.Auto,
                    addition: OSharp.Beatmap.Sections.HitObject.ObjectSamplesetType.Auto,
                    customFile: sampleData.Filename,
                    volume: sampleData.Volume / 100f
                );

                hitsoundList.Add(element);
            }

            return hitsoundList;
        }
    }
}
