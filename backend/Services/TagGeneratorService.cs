using System.Text.RegularExpressions;

namespace backend.Services;

public class TagGeneratorService : ITagGeneratorService
{
    private static readonly Regex TicketRegex = new(
        @"(?:[A-Za-z]+\d+-\d+|K\d+-\d+|C\d+-\d+|\b\d{4,}\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly object LockObj = new();
    private static string _lastTimestamp = string.Empty;
    private static int _sequence = 0;

    public string GenerateTag(string? branchWeb, string? branchServer = null)
    {
        var ticket = ExtractTicket(branchWeb) ?? ExtractTicket(branchServer) ?? "BUILD";

        string timestamp;
        int currentSeq;

        lock (LockObj)
        {
            var nowStr = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            if (nowStr == _lastTimestamp)
            {
                _sequence++;
                currentSeq = _sequence;
            }
            else
            {
                _lastTimestamp = nowStr;
                _sequence = 0;
                currentSeq = 0;
            }

            timestamp = nowStr;
        }

        if (currentSeq > 0)
        {
            return $"{ticket}-{timestamp}-{currentSeq:D2}";
        }

        return $"{ticket}-{timestamp}";
    }

    private static string? ExtractTicket(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName)) return null;

        var match = TicketRegex.Match(branchName);
        if (match.Success)
        {
            return match.Value.ToUpperInvariant();
        }

        return null;
    }
}
