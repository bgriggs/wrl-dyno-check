namespace BigMission.WrlDynoCheck.ReplayCsv;

internal class CsvFile
{
    public string Path { get; }
    public string[] Headers { get; private set; }
    public List<string[]> Rows { get; }

    public CsvFile(string path)
    {
        Path = path;
        Headers = [];
        Rows = [];
    }

    public void Load()
    {
        using var reader = new StreamReader(Path);
        Headers = reader.ReadLine()?.Split(',') ?? [];
        while (!reader.EndOfStream)
        {
            Rows.Add(reader.ReadLine()?.Split(',') ?? []);
        }
    }

    public string[] GetColumnValues(int index) => Rows.Select(row => row[index]).ToArray();
}
