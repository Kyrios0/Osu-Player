﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Milky.OsuPlayer.Shared;
using Milky.OsuPlayer.Shared.Models;
using Newtonsoft.Json;
using OSharp.Beatmap.MetaData;

namespace Milky.OsuPlayer.Common.Configuration
{
    public class AppSettings : IDisposable
    {
        private ThreadLocal<FileStream> FileStream { get; } = new ThreadLocal<FileStream>(() =>
            File.Open(Domain.ConfigFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite), true);

        public AppSettings()
        {
            if (Default != null)
            {
                return;
            }

            Default = this;
        }

        public VolumeControl Volume { get; set; } = new VolumeControl();
        public GeneralControl General { get; set; } = new GeneralControl();
        public InterfaceControl Interface { get; set; } = new InterfaceControl();
        public PlayControl Play { get; set; } = new PlayControl();
        [JsonProperty("hot_keys")]
        public List<HotKey> HotKeys { get; set; } = new List<HotKey>();
        public LyricControl Lyric { get; set; } = new LyricControl();
        public ExportControl Export { get; set; } = new ExportControl();
        public HashSet<MapIdentity> CurrentList { get; set; } = new HashSet<MapIdentity>();
        public MapIdentity? CurrentMap { get; set; }
        public DateTime? LastUpdateCheck { get; set; } = null;
        public string IgnoredVer { get; set; } = null;


        public DateTime LastTimeScanOsuDb { get; set; }

        public void Save()
        {
            lock (FileSaveLock)
            {
                FileStream.Value.SetLength(0);
                var content = JsonConvert.SerializeObject(this, Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    });
                byte[] buffer = Encoding.GetBytes(content);
                FileStream.Value.Write(buffer, 0, buffer.Length);
            }
        }

        public void Dispose()
        {
            foreach (var fs in FileStream.Values) fs?.Dispose();
            FileStream?.Dispose();
        }

        private static readonly Encoding Encoding = Encoding.UTF8;
        private static readonly object FileSaveLock = new object();

        public static AppSettings Default { get; private set; }

        public static void SaveDefault()
        {
            Default?.Save();
        }

        public static void Load(AppSettings config)
        {
            Default = config ?? new AppSettings();
            //Default.FileStream = File.Open(Domain.ConfigFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        private static void LoadNew()
        {
            File.WriteAllText(Domain.ConfigFile, "");
            Load(new AppSettings());
        }

        public static void CreateNewConfig()
        {
            LoadNew();
            SaveDefault();
        }
    }
}
