using System;
using System.Collections.Generic;
using System.Text;

namespace Peeper.Logic.Search
{
    public static class TimeManager
    {

        public static int HardTimeLimit { get; private set; }
        public static int SoftTimeLimit { get; private set; }
        public static bool HasSoftTime => SoftTimeLimit > 0;

        private static Stopwatch TotalSearchTime = new Stopwatch();

        public static void StartTimer() => TotalSearchTime.Start();
        public static void StopTimer() => TotalSearchTime.Stop();
        public static void ResetTimer() => TotalSearchTime.Reset();
        public static void RestartTimer() => TotalSearchTime.Restart();
        public static double GetSearchTime() => TotalSearchTime.ElapsedMilliseconds;


        public static void SetHardLimit(int movetime)
        {
            HardTimeLimit = movetime;
            Assert(!HasSoftTime);
        }

        public static void Reset()
        {
            HardTimeLimit = MaximumSearchTime;
            SoftTimeLimit = -1;
        }

        public static bool CheckHardTime()
        {
            double currentTime = GetSearchTime();
            return (currentTime > (HardTimeLimit - MoveOverhead));
        }

        public static void UpdateTimeLimits(int playerTime, int inc)
        {
            HardTimeLimit = Math.Min(playerTime, inc + (playerTime / 2));

            //  Values from Clarity (who took them from Stormphrax), then slightly adjusted
            SoftTimeLimit = (int)(0.65 * ((playerTime / 20) + (inc * 3 / 4)));
        }
    }
}
