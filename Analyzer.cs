using Unity_Analyzer;

public class Analyzer
{
    Dictionary<string, string> GuidToPath = new Dictionary<string, string>();
    public static List<Dictionary<string, string>> Dumper(string filePath, string endPath)
    {
        string[] directories = Directory.GetDirectories(filePath);
        string[] files = Directory.GetFiles(filePath);
        List<Dictionary<string, string>> Datas = new List<Dictionary<string,string>>();
        foreach (var file in files)
        {
            if (!file.EndsWith(".unity")) continue;
            Builder builder = new Builder();
            var tuple = Builder.SplitBlocks(file);
            var Data = tuple.Item1;
            var block = tuple.Item2;
            Datas.Add(Data);
            List<string> fileDump = Builder.PrepareAndBuild(ref Data, ref block);
            File.WriteAllLines(endPath+"/"+file.Substring(file.LastIndexOf('/')+1)+".dump",fileDump);
        }
        foreach (var directory in directories)
        {
            Datas.AddRange(Dumper(directory,endPath));
        }
        return Datas;
    }
    
    public void FillGuid(string filePath, string endPath, ref List<Dictionary<string, string>> Datas, string startPath)
    {
        string[] directories = Directory.GetDirectories(filePath);
        string[] files = Directory.GetFiles(filePath);
        Builder builder = new Builder();
        foreach (var file in files)
        {
            if (!file.EndsWith(".cs")) continue;
            GuidToPath[builder.CsFileId(file + ".meta")] = file;
        }
        foreach (var directory in directories)
        {
            FillGuid(directory, endPath,ref Datas, startPath);
        }
        
    }
    public void ScriptFinder(string filePath, string endPath, ref List<Dictionary<string, string>> Datas, string startPath)
    {
        string[] directories = Directory.GetDirectories(filePath);
        string[] files = Directory.GetFiles(filePath);
        Builder builder = new Builder();
        TextWriter scripts = new StreamWriter(endPath+"/UnusedScripts.csv", true);
        if (startPath == filePath)
        {
            scripts.Close();
            scripts = new StreamWriter(endPath+"/UnusedScripts.csv");
            scripts.Write("Relative Path,GUID\n");
            scripts.Close();
            scripts = new StreamWriter(endPath+"/UnusedScripts.csv", true);
        }
        foreach (var file in files)
        {
            if (!file.EndsWith(".cs")) continue;
            if (!builder.FindInScript(builder.CsFileId(file + ".meta"), ref Datas, ref GuidToPath))
            {
                scripts.Write(file.Substring(startPath.Length)+","+builder.CsFileId(file + ".meta")+"\n");
            }
        }
        scripts.Close();
        foreach (var directory in directories)
        {
            ScriptFinder(directory, endPath,ref Datas, startPath);
        }
        
    }
    
}

public class Program
{
    public static void Main(string[] args)
    {
        var analyzer = new Analyzer();
        string[] files = Directory.GetDirectories(args[0]);
        var Datas = Analyzer.Dumper(filePath: args[0], endPath: args[1]);
        analyzer.FillGuid(args[0],args[1],ref Datas, args[0]);
        analyzer.ScriptFinder(args[0],args[1],ref Datas,args[0]);
        
    }
}