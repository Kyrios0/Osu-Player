﻿using Milky.OsuPlayer.Media.Audio.Player;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Milky.OsuPlayer.Media.Audio.Wave
{
    internal class CachedSound
    {
        public string SourcePath { get; }
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }
        public TimeSpan Duration { get; private set; }
        public static MyAudioFileReader.WaveStreamType WaveStreamType { get; set; } =
            MyAudioFileReader.WaveStreamType.Wav;

        private CachedSound(string filePath)
        {
            SourcePath = filePath;
        }

        public override bool Equals(object obj)
        {
            if (obj is CachedSound other)
                return Equals(other);
            return ReferenceEquals(this, obj);
        }

        protected bool Equals(CachedSound other)
        {
            return SourcePath == other.SourcePath;
        }

        public override int GetHashCode()
        {
            return (SourcePath != null ? SourcePath.GetHashCode() : 0);
        }

        public static IReadOnlyDictionary<string, CachedSound> CachedSounds { get; } =
            new ReadOnlyDictionary<string, CachedSound>(CachedDictionary);
        public static IReadOnlyDictionary<string, CachedSound> DefaultSounds { get; } =
            new ReadOnlyDictionary<string, CachedSound>(DefaultDictionary);

        public static async Task CreateCacheSounds(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                await CreateCacheSound(path, false).ConfigureAwait(false); // Cache each file once before play.
            }
        }

        /// <summary>
        /// Default skin hitsounds
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        public static async Task CreateDefaultCacheSounds(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                await CreateCacheSound(path, true).ConfigureAwait(false);
            }
        }

        public static async Task<CachedSound> GetOrCreateCacheSound(string path)
        {
            if (DefaultDictionary.ContainsKey(path))
                return DefaultDictionary[path];

            //if (!CachedDictionary.ContainsKey(path))
            return await CreateCacheSound(path, false).ConfigureAwait(false);

            //return CachedDictionary[path];
        }

        public static void ClearCacheSounds()
        {
            CachedDictionary.Clear();
        }

        private static readonly ConcurrentDictionary<string, CachedSound> CachedDictionary =
            new ConcurrentDictionary<string, CachedSound>();
        private static readonly ConcurrentDictionary<string, CachedSound> DefaultDictionary =
            new ConcurrentDictionary<string, CachedSound>();

        private static async Task<CachedSound> CreateFromFile(string filePath)
        {
            using (var audioFileReader = await WaveFormatFactory.GetResampledAudioFileReader(filePath,
                WaveStreamType).ConfigureAwait(false))
            {
                var wholeData = new List<float>((int)(audioFileReader.Length / 4));

                var readBuffer =
                    new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                int samplesRead;
                while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeData.AddRange(readBuffer.Take(samplesRead));
                }

                var cachedSound = new CachedSound(filePath)
                {
                    AudioData = wholeData.ToArray(),
                    Duration = audioFileReader.TotalTime,
                    WaveFormat = audioFileReader.WaveFormat
                };
                return cachedSound;
            }
        }

        private static async Task<CachedSound> CreateCacheSound(string path, bool isDefault)
        {
            string newPath = path;
            if (!File.Exists(newPath))
            {
                newPath = TryGetPath(newPath);
            }

            if (!File.Exists(newPath))
            {
                newPath = TryGetPath(Path.Combine(Path.GetDirectoryName(newPath), Path.GetFileNameWithoutExtension(newPath)));
            }

            if (!File.Exists(newPath))
            {
                if (!isDefault) CachedDictionary.TryAdd(path, null);
                else DefaultDictionary.TryAdd(path, null);
                return null;
            }

            if (!isDefault && CachedDictionary.TryGetValue(path, out var value) ||
                isDefault && DefaultDictionary.TryGetValue(path, out value))
            {
                return value;
            }

            CachedSound cachedSound;
            try
            {
                cachedSound = await CreateFromFile(newPath).ConfigureAwait(false);
            }
            catch
            {
                if (!isDefault) CachedDictionary.TryAdd(path, null);
                else DefaultDictionary.TryAdd(path, null);
                return null;
            }

            // Cache each file once before play.
            var sound = isDefault
                ? DefaultDictionary.GetOrAdd(path, cachedSound)
                : CachedDictionary.GetOrAdd(path, cachedSound);

            //Console.WriteLine(CountSize(CachedDictionary.Values.Sum(k => k.AudioData.Length)));

            return sound;
        }

        private static string TryGetPath(string path)
        {
            foreach (var ext in AudioPlaybackEngine.SupportExtensions)
            {
                var autoAudioFile = path + ext;
                if (!File.Exists(autoAudioFile))
                    continue;

                path = autoAudioFile;
                break;
            }

            return path;
        }
    }
}