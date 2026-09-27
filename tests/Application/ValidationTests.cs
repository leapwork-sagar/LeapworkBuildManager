using System;
using LeapworkBuildManager;
using System.Windows.Forms;

static class ValidationTests
{
    static int count;
    static void Check(string input, bool submitted, bool valid, bool error, string message)
    {
        var result = BuildInputFeedback.Evaluate(input, submitted);
        if (result.IsValid != valid || result.IsError != error || result.Message != message)
            throw new Exception("Feedback: " + input);
        count++;
        Console.WriteLine("PASS feedback " + input + " submitted=" + submitted);
    }

    [STAThread]
    static void Main()
    {
        Check("", false, false, false, "");
        Check("", true, false, false, "");
        Check("2", false, false, false, "Enter the four-digit release year.");
        Check("202", true, false, true, "Enter the four-digit release year.");
        Check("2026.", false, false, false, "Next, enter the release quarter: 1–4.");
        Check("2026.", true, false, true, "Enter the release quarter: 1–4.");
        Check("2026.7.", false, false, true, "Release quarter must be between 1 and 4.");
        Check("2026.0.257", true, false, true, "Release quarter must be between 1 and 4.");
        Check("2026.2.", false, false, false, "Next, enter the build number.");
        Check("2026.2.", true, false, true, "Enter the build number after the quarter.");
        Check("2026.2.2", false, true, false, "2026 · Q2");
        Check("2026.2.257", true, true, false, "2026 · Q2");
        Check("2025.4.0", false, true, false, "2025 · Q4");
        using (var box = new BuildNumberBox())
        {
            if (!box.InsertNumericText(" 2026.2.257 ") || box.Text != "2026.2.257")
                throw new Exception("Formatted paste");
            count++;
            Console.WriteLine("PASS formatted paste unchanged");
            box.SelectAll();
            if (!box.InsertNumericText("20262257") || box.Text != "2026.2.257")
                throw new Exception("Numeric paste");
            count++;
            Console.WriteLine("PASS numeric paste unchanged");
            if (box.InsertNumericText("bad text") || box.Text != "2026.2.257")
                throw new Exception("Rejected paste");
            count++;
            Console.WriteLine("PASS rejected paste unchanged");
        }

        Console.WriteLine(count + " validation checks passed");
    }
}
