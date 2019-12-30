﻿using System.IO;
using Milky.WpfApi;

namespace Milky.OsuPlayer.Common.Configuration
{
    public class GeneralControl
    {
        public bool RunOnStartup { get; set; } = false;
        public string DbPath { get; set; }
        public string CustomSongsPath { get; set; } = Path.Combine(Domain.CurrentPath, "Songs");
        public bool? ExitWhenClosed { get; set; } = null;
        public bool FirstOpen { get; set; } = true;
        public bool SbScanned { get; set; } = false;
        public double[] MiniPosition { get; set; }
    }

    public class InterfaceControl : ViewModelBase
    {
        private bool _minimalMode = false;

        public bool MinimalMode
        {
            get => _minimalMode;
            set
            {
                _minimalMode = value;
                OnPropertyChanged();
            }
        }

        public string Locale { get; set; }
    }
}