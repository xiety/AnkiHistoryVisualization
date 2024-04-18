using System.Globalization;

using CsvHelper;
using CsvHelper.Configuration;

namespace AnkiHistoryVisualization;

public static class CsvUtils
{
    public static T[] Load<T>(string filename)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";", };
        using var reader = new StreamReader(filename);
        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<T>().ToArray();
    }
}
