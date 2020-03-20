﻿using System.Collections.Generic;
using System.IO;
using Milky.OsuPlayer.Common.Configuration;
using Milky.OsuPlayer.Media.Audio.Sounds;
using Milky.OsuPlayer.Media.Audio.TrackProvider;
using OSharp.Beatmap;

namespace Milky.OsuPlayer.Media.Audio.Core
{
    internal class SampleTrackPlayer : HitsoundPlayer
    {
        protected override string Flag { get; } = nameof(SampleTrackPlayer);

        public SampleTrackPlayer(AudioPlaybackEngine engine, string filePath, OsuFile osuFile) : base(engine, filePath, osuFile)
        {
        }

        protected override void InitVolume()
        {
            Engine.SampleVolume = 1f * AppSettings.Default.Volume.Sample * AppSettings.Default.Volume.Main;
        }

        protected override void Volume_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Engine.SampleVolume = 1f * AppSettings.Default.Volume.Sample * AppSettings.Default.Volume.Main;
        }

        protected override List<SoundElement> FillHitsoundList(OsuFile osuFile, DirectoryInfo dirInfo)
        {
            List<SoundElement> hitsoundList = new List<SoundElement>();
            var sampleList = osuFile.Events.SampleInfo;
            if (sampleList == null)
                return hitsoundList;
            foreach (var sampleData in sampleList)
            {
                var element = new HitsoundElement(
                    mapFolderName: dirInfo.FullName,
                    mapWaveFiles: new HashSet<string>(),
                    gameMode: osuFile.General.Mode,
                    offset: sampleData.Offset,
                    track: -1,
                    lineSample: OSharp.Beatmap.Sections.Timing.TimingSamplesetType.None,
                    hitsound: OSharp.Beatmap.Sections.HitObject.HitsoundType.Normal,
                    sample: OSharp.Beatmap.Sections.HitObject.ObjectSamplesetType.Auto,
                    addition: OSharp.Beatmap.Sections.HitObject.ObjectSamplesetType.Auto,
                    customFile: sampleData.Filename,
                    volume: sampleData.Volume / 100f,
                    balance: 0,
                    forceTrack: 0,
                    fullHitsoundType: null
                );

                hitsoundList.Add(element);
            }

            var sb = new NightcoreTilingTrackProvider(osuFile);
            var eles = sb.GetSoundElements();
            hitsoundList.AddRange(eles);
            return hitsoundList;
        }
    }
}