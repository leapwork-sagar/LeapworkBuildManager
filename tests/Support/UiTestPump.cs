using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

internal static class UiTestPump
{
    public static void Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            Application.DoEvents();
            Thread.Sleep(5);
            if (watch.ElapsedMilliseconds > 5000)
                throw new TimeoutException("UI wait timed out");
        }
        Application.DoEvents();
    }
}
