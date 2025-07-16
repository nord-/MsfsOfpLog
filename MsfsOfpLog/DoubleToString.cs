using static System.Globalization.CultureInfo;
public static class DoubleToString
{
    /// <summary>
    /// Converts a double to a string with a specified number of decimal places.
    /// </summary>
    /// <param name="value">The double value to convert.</param>
    /// <param name="decimalPlaces">The number of decimal places to include in the string.</param>
    /// <returns>A string representation of the double value with the specified number of decimal places.</returns>
    public static string ToDecimalString(this double value, int decimalPlaces) => value.ToString($"F{decimalPlaces}", InvariantCulture);
}