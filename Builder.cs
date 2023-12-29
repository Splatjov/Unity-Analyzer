using Unity_Analyzer;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

public class Builder
{
    public static List<string> BuildHierarchy(string fileID, ref Dictionary<string, string> Data, int level = 0)
    {
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var transform  = deserializer.Deserialize<TransformWrapper>(Data[fileID]);
        var gameObject = deserializer.Deserialize<GameObjectWrapper>(Data[transform.Transform.m_GameObject.fileID]);
        List<string> Hierarchy = new List<string>();
        Hierarchy.Add(String.Concat(Enumerable.Repeat("--", level))+gameObject.GameObject.m_Name);
        foreach (var child in transform.Transform.m_Children)
        {
            List<string> childHierarchy = BuildHierarchy(child.fileID, ref Data, level + 1);
            Hierarchy.AddRange(childHierarchy);
        }
        return Hierarchy;
    }

    public static (Dictionary<string, string>, string) SplitBlocks(string filePath)
    {
        var startOfBlockTag = "--- !u!";

        using StreamReader reader = new StreamReader(filePath);

        Dictionary<string, string> Data = new Dictionary<string, string>();
        string now = "", block = "", id = "";
        while (!reader.EndOfStream)
        {
            now = reader.ReadLine();
            if (now.StartsWith("%")) continue;
            if (now.StartsWith(startOfBlockTag))
            {
                Data[id] = block;
                id = now.Substring(now.IndexOf("&")+1);
                block = "";
            }
            else
            {
                block += now+"\n";
            }

            if (reader.EndOfStream)
            {
                Data[id] = block;
                id = now.Substring(now.IndexOf("&")+1);
            }
        }

        return (Data, block);
    }
    public static List<string> PrepareAndBuild(ref Dictionary<string, string> Data, ref string block)
    {
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var sceneRoots  = deserializer.Deserialize<SceneRootsWrapper>(block);
        var hierarchy = new List<string>();
        foreach (var root in sceneRoots.SceneRoots.m_Roots)
        {
            var rootHierarchy = BuildHierarchy(root.fileID, ref Data);
            hierarchy.AddRange(rootHierarchy);
        }

        return hierarchy;
    }
    
    public string CsFileId(string filePath)
    {
        using StreamReader reader = new StreamReader(filePath);
        var meta = reader.ReadToEnd();
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var csfile  = deserializer.Deserialize<CsFileWrapper>(meta);
        return csfile.guid;

    }
    
    static T DeserializeYaml<T>(string yamlData)
    {
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<T>(yamlData);
    }

    public bool FindInScript(string guid, ref List<Dictionary<string, string>> Datas, ref Dictionary<string, string> GuidToPath)
    {
        foreach (var Data in Datas)
        {
            foreach (var block in Data)
            {
                if (!block.Value.StartsWith("MonoBehaviour")) continue;
                
                var data = DeserializeYaml<Dictionary<string, dynamic>>(block.Value);
                if (data["MonoBehaviour"]["m_Script"]["guid"] == guid)
                {
                    return true;
                }
                foreach (var scriptPair in data["MonoBehaviour"])
                {
                    if (scriptPair.Key == "m_Script"|| scriptPair.Value == null || scriptPair.Value is not Dictionary<object, object> || data["MonoBehaviour"]["m_Script"]["guid"] == null)
                        continue;
                    if (!scriptPair.Value.ContainsKey("guid") || scriptPair.Value["guid"]!=guid)
                        continue;
                    string filepath = GuidToPath[data["MonoBehaviour"]["m_Script"]["guid"]];
                    string type = GuidToPath[guid].Substring(GuidToPath[guid].LastIndexOf('/') + 1);
                    type = type.Substring(0, type.Length - 3);
                    using StreamReader reader = new StreamReader(filepath);
                    var cscode = reader.ReadToEnd();
                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(cscode);
                    CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();
                    var variableDeclarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>();

                    foreach (var declaration in variableDeclarations)
                    {
                        foreach (var variable in declaration.Variables)
                        {
                            if (variable.Identifier.Text == scriptPair.Key &&
                                declaration.Type.ToString() == type)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }
}

