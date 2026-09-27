using System;

namespace LeapworkBuildManager
{
    public sealed class RecentSpeed
    {
        double previousTime, rate;
        long previousBytes;
        bool initialized;
        public void Reset()
        {
            previousTime = rate = 0;
            previousBytes = 0;
            initialized = false;
        }

        internal void ResetAt(long bytes, double seconds)
        {
            Reset();
            previousBytes = bytes;
            previousTime = seconds;
        }

        public double Update(long bytes, double seconds)
        {
            double elapsed = seconds - previousTime;
            if (elapsed < OperationalSettings.ProgressIntervalMilliseconds / 1000.0)
                return rate;
            if (bytes < previousBytes || seconds < previousTime)
            {
                Reset();
                return 0;
            }

            double sample = (bytes - previousBytes) / elapsed;
            double alpha = 1 - Math.Exp(-elapsed / OperationalSettings.SpeedSmoothingSeconds);
            rate = initialized ? rate + alpha * (sample - rate) : sample;
            initialized = true;
            previousTime = seconds;
            previousBytes = bytes;
            return rate;
        }
    }
}
