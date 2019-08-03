﻿using OSharp.Beatmap.MetaData;

namespace Milky.OsuPlayer.Common
{
    public struct MapIdentity : IMapIdentifiable
    {
        public MapIdentity(string folderName, string version) : this()
        {
            FolderName = folderName;
            Version = version;
        }

        public string FolderName { get; set; }
        public string Version { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var trueObj = (MapIdentity)obj;
            return trueObj.FolderName == FolderName && trueObj.Version == Version;
        }

        public override int GetHashCode() => base.GetHashCode();
    }
}
