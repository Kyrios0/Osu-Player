﻿using Milky.OsuPlayer.Common.Player;

namespace Milky.OsuPlayer.Media.Audio
{
    public interface IPlayer
    {
        PlayerStatus PlayerStatus { get; }
        int Duration { get; }
        int PlayTime { get; }
        float PlaybackRate { get; set; }
        float Volume { get; set; }

        void Play();
        void Pause();
        void Stop();
        void Replay();
        void SetTime(int ms, bool play = true);
    }
}
