using System.Text.RegularExpressions;

namespace SE_Academic_Affairs_Support_System.Helper
{
    public static class GoogleSheetHelper
    {
        public static string? ExtractSheetId(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var match = Regex.Match(url, @"/spreadsheets/d/([a-zA-Z0-9_-]+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
