public class Analyzer
{
    private Dictionary<string, string> _guidToPath = new();

    public static List<Dictionary<string, string>> Dumper(string filePath, string endPath)
    {
        var directories = Directory.GetDirectories(filePath);
        var files = Directory.GetFiles(filePath);
        var blockData = new List<Dictionary<string, string>>();
        foreach (var file in files)
        {
            if (!file.EndsWith(".unity")) continue;
            var (data, block) = Builder.SplitBlocks(file);
            blockData.Add(data);
            var fileDump = Builder.PrepareAndBuild(ref data, ref block);
            File.WriteAllLines(endPath + "/" + file.Substring(file.LastIndexOf('/') + 1) + ".dump", fileDump);
        }

        foreach (var directory in directories) blockData.AddRange(Dumper(directory, endPath));
        return blockData;
    }

    public void FillGuid(string filePath, string endPath, ref List<Dictionary<string, string>> blockData,
        string startPath)
    {
        var directories = Directory.GetDirectories(filePath);
        var files = Directory.GetFiles(filePath);
        var builder = new Builder();
        foreach (var file in files)
        {
            if (!file.EndsWith(".cs")) continue;
            _guidToPath[builder.CsFileId(file + ".meta")] = file;
        }

        foreach (var directory in directories) FillGuid(directory, endPath, ref blockData, startPath);
    }

    public void ScriptFinder(string filePath, string endPath, ref List<Dictionary<string, string>> blockData,
        string startPath)
    {
        var directories = Directory.GetDirectories(filePath);
        var files = Directory.GetFiles(filePath);
        var builder = new Builder();
        TextWriter scripts = new StreamWriter(endPath + "/UnusedScripts.csv", true);
        if (startPath == filePath)
        {
            scripts.Close();
            scripts = new StreamWriter(endPath + "/UnusedScripts.csv");
            scripts.Write("Relative Path,GUID\n");
            scripts.Close();
            scripts = new StreamWriter(endPath + "/UnusedScripts.csv", true);
        }

        foreach (var file in files)
        {
            if (!file.EndsWith(".cs")) continue;
            if (!builder.FindInScript(builder.CsFileId(file + ".meta"), ref blockData, ref _guidToPath))
                scripts.Write(file.Substring(startPath.Length) + "," + builder.CsFileId(file + ".meta") + "\n");
        }

        scripts.Close();
        foreach (var directory in directories) ScriptFinder(directory, endPath, ref blockData, startPath);
    }
}