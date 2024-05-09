using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BigMission.WrlDynoCheck.Utilities;

internal class CsvFile(string path)
{
    public string Path { get; } = path;
    public string[] Headers { get; private set; } = [];
    public List<string[]> Rows { get; } = [];

    public async Task Load()
    {
        using var reader = new StreamReader(Path);
        Headers = (await reader.ReadLineAsync())?.Split(',') ?? [];
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            Rows.Add(line?.Split(',') ?? []);
        }
    }

    public string[] GetColumnValues(int index) => Rows.Select(row => row[index]).ToArray();
}
