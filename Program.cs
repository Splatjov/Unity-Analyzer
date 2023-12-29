public abstract class Program
{
    public static void Main(string[] args)
    {
        var analyzer = new Analyzer();
        var blockData = Analyzer.Dumper(args[0], args[1]);
        analyzer.FillGuid(args[0], args[1], ref blockData, args[0]);
        analyzer.ScriptFinder(args[0], args[1], ref blockData, args[0]);
    }
}