using System.Text.RegularExpressions;

namespace LeapworkBuildManager
{
    public sealed class BuildInputFeedback
    {
        public string Message { get; private set; }
        public bool IsError { get; private set; }
        public bool IsValid { get; private set; }

        public static BuildInputFeedback Evaluate(string input, bool submitted)
        {
            string value = (input ?? "").Trim(), error;
            var result = new BuildInputFeedback();
            result.IsValid = BuildService.Validate(value, out error);
            if (value.Length == 0)
            {
                result.Message = "";
                return result;
            }

            if (result.IsValid)
            {
                result.Message = value.Substring(0, 4) + " · Q" + value.Split('.')[1];
                return result;
            }

            bool incomplete = true;
            if (Regex.IsMatch(value, @"\A[0-9]{1,3}\z"))
                result.Message = "Enter the four-digit release year.";
            else if (Regex.IsMatch(value, @"\A[0-9]{4}\.?\z"))
                result.Message = submitted ? "Enter the release quarter: 1–4." : "Next, enter the release quarter: 1–4.";
            else if (Regex.IsMatch(value, @"\A[0-9]{4}\.[^1-4]"))
            {
                result.Message = "Release quarter must be between 1 and 4.";
                incomplete = false;
            }
            else if (Regex.IsMatch(value, @"\A[0-9]{4}\.[1-4]\.?\z"))
                result.Message = submitted ? "Enter the build number after the quarter." : "Next, enter the build number.";
            else
            {
                result.Message = "Enter a build number, for example 2026.2.257.";
                incomplete = false;
            }

            result.IsError = submitted || !incomplete;
            return result;
        }
    }
}
