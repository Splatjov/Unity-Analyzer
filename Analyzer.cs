using System.Collections.Concurrent;

public class Analyzer
{
    private Dictionary<string, string> _guidToPath = new();

    private List<Dictionary<string, string>> _blockData = [];

    public void Dumper(string filePath, string endPath)
    {
        var directories = Directory.GetDirectories(filePath);
        var files = Directory.GetFiles(filePath);
    
        Parallel.ForEach(files, file =>
        {
            if (!file.EndsWith(".unity")) return;

            var (data, block) = Builder.SplitBlocks(file);
            lock (_blockData)
            {
                _blockData.Add(data);
            }

            var fileDump = Builder.PrepareAndBuild(ref data, ref block);
            lock (_blockData)
            {
                File.WriteAllLines(endPath + "/" + file.Substring(file.LastIndexOf('/') + 1) + ".dump", fileDump);
            }
        });
        Parallel.ForEach(directories, directory =>
        {
            Dumper(directory, endPath);
        });
    }

    public void FillGuid(string filePath)
    {
        var directories = Directory.GetDirectories(filePath);
        var files = Directory.GetFiles(filePath);
        var builder = new Builder();
        
        var concurrentGuidToPath = new ConcurrentDictionary<string, string>();

        Parallel.ForEach(files, file =>
        {
            if (!file.EndsWith(".cs")) return;
            
            var fileId = builder.CsFileId(file + ".meta");
            
            concurrentGuidToPath.AddOrUpdate(fileId, file, (_, _) => file);
        });
        
        foreach (var kvp in concurrentGuidToPath)
        {
            lock (_guidToPath)
            {
                _guidToPath[kvp.Key] = kvp.Value;
            }
        }
        Parallel.ForEach(directories, FillGuid);
    }

    public void ScriptFinder(string filePath, string endPath, string startPath)
    {
        var directories = Directory.GetDirectories(filePath);
        var files = Directory.GetFiles(filePath);
        var builder = new Builder();
        if (startPath == filePath)
        {
            lock (_guidToPath)
            {
                File.WriteAllText(endPath + "/UnusedScripts.csv", "Relative Path,GUID\n");
            }
        }
        Parallel.ForEach(files, file =>
        {
            if (!file.EndsWith(".cs")) return;
            var localScripts = new StringWriter();
            if (!builder.FindInScript(builder.CsFileId(file + ".meta"), ref _blockData, ref _guidToPath))
            {
                localScripts.Write(file.Substring(startPath.Length) + "," + builder.CsFileId(file + ".meta") + "\n");
            }
            lock (_guidToPath)
            {
                File.AppendAllText(endPath + "/UnusedScripts.csv", localScripts.ToString());
            }
        });
        foreach (var directory in directories) 
        {
            ScriptFinder(directory, endPath, startPath);
        }
    }
}