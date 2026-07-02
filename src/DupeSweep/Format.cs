namespace DupeSweep;

public static class Format
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {Units[unit]}" : $"{value:0.##} {Units[unit]}";
    }

    public static string Duration(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1 ? $"{elapsed:mm\\:ss}" : $"{elapsed.TotalSeconds:0.0}s";
}
