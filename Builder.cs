using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity_Analyzer_Structs;
using YamlDotNet.Serialization;

public class Builder
{
    private static List<string> BuildHierarchy(string fileId, ref Dictionary<string, string> data, int level = 0)
    {
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var transform = deserializer.Deserialize<TransformWrapper>(data[fileId]);
        var gameObject = deserializer.Deserialize<GameObjectWrapper>(data[transform.Transform.m_GameObject.fileID]);
        List<string> hierarchy =
        [
            string.Concat(Enumerable.Repeat("--", level)) + gameObject.GameObject.m_Name
        ];
        foreach (var child in transform.Transform.m_Children)
        {
            var childHierarchy = BuildHierarchy(child.fileID, ref data, level + 1);
            hierarchy.AddRange(childHierarchy);
        }

        return hierarchy;
    }

    public static (Dictionary<string, string>, string) SplitBlocks(string filePath)
    {
        const string startOfBlockTag = "--- !u!";

        using var reader = new StreamReader(filePath);

        var data = new Dictionary<string, string>();
        string block = "", id = "";
        while (!reader.EndOfStream)
        {
            var now = reader.ReadLine();
            if (now == null) break;
            if (now.StartsWith('%')) continue;
            if (now.StartsWith(startOfBlockTag))
            {
                data[id] = block;
                id = now.Substring(now.IndexOf('&') + 1);
                block = "";
            }
            else
            {
                block += now + "\n";
            }

            if (reader.EndOfStream)
            {
                data[id] = block;
                id = now.Substring(now.IndexOf('&') + 1);
            }
        }

        return (data, block);
    }

    public static List<string> PrepareAndBuild(ref Dictionary<string, string> data, ref string block)
    {
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var sceneRoots = deserializer.Deserialize<SceneRootsWrapper>(block);
        var hierarchy = new List<string>();
        foreach (var root in sceneRoots.SceneRoots.m_Roots)
        {
            var rootHierarchy = BuildHierarchy(root.fileID, ref data);
            hierarchy.AddRange(rootHierarchy);
        }

        return hierarchy;
    }

    public string CsFileId(string filePath)
    {
        using var reader = new StreamReader(filePath);
        var meta = reader.ReadToEnd();
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var csFile = deserializer.Deserialize<CsFileWrapper>(meta);
        return csFile.guid;
    }

    private static T DeserializeYaml<T>(string yamlData)
    {
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<T>(yamlData);
    }

    public bool FindInScript(string guid, ref List<Dictionary<string, string>> blockData,
        ref Dictionary<string, string> guidToPath)
    {
        foreach (var oneBlockData in blockData)
        foreach (var block in oneBlockData)
        {
            if (!block.Value.StartsWith("MonoBehaviour")) continue;

            var data = DeserializeYaml<Dictionary<string, dynamic>>(block.Value);
            if (data["MonoBehaviour"]["m_Script"]["guid"] == guid) return true;
            foreach (var scriptPair in data["MonoBehaviour"])
            {
                if (scriptPair.Key == "m_Script" || scriptPair.Value == null ||
                    scriptPair.Value is not Dictionary<object, object> ||
                    data["MonoBehaviour"]["m_Script"]["guid"] == null)
                    continue;
                if (!scriptPair.Value.ContainsKey("guid") || scriptPair.Value["guid"] != guid)
                    continue;
                string filepath = guidToPath[data["MonoBehaviour"]["m_Script"]["guid"]];
                var type = guidToPath[guid].Substring(guidToPath[guid].LastIndexOf('/') + 1);
                type = type.Substring(0, type.Length - 3);
                using var reader = new StreamReader(filepath);
                var csCode = reader.ReadToEnd();
                var syntaxTree = CSharpSyntaxTree.ParseText(csCode);
                var root = syntaxTree.GetCompilationUnitRoot();
                var variableDeclarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>();

                if ((from declaration in variableDeclarations
                        from variable in declaration.Variables
                        where variable.Identifier.Text == scriptPair.Key &&
                              declaration.Type.ToString() == type
                        select declaration).Any())
                    return true;
            }
        }

        return false;
    }
}