using System.Text;
using TNAB.Parsers;

namespace TNAB.Tests;

public class Html5LibTests
{
    [Theory]
    [MemberData(nameof(GetTreeConstructionTests))]
    public void TreeConstruction(string input, string expected)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", input)));
        var htmlParser = new HtmlParser(stream);
        htmlParser.Parse();
        var actual = new StringBuilder();
        PrintNode(ref actual, 0, htmlParser.Root);
        Assert.Equal(expected, actual.ToString());
    }

    public static IEnumerable<TheoryDataRow<string, string>> GetTreeConstructionTests()
    {
        var root = Path.GetFullPath("../../../..");
        var inputBuffer = new StringBuilder();
        var outputBuffer = new StringBuilder();
        var skip = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Join(root, "tests", "html5lib-tests", "tree-construction"), "*.dat"))
        {
            var data = File.ReadAllText(file);
            var index = 0;
            var mode = "";
            foreach (var line in data.Split('\n'))
            {
                if (line.StartsWith('#'))
                {
                    mode = line;
                    if (mode == "#data")
                    {
                        index++;
                        inputBuffer.Clear();
                        outputBuffer.Clear();
                    }
                    else if (mode != "#errors")
                    {
                        outputBuffer.Append(line);
                        outputBuffer.Append('\n');
                    }
                }
                else if (mode == "#data")
                {
                    inputBuffer.Append(line);
                    inputBuffer.Append('\n');
                }
                else if (mode == "#errors")
                {
                }
                else if (mode == "#document" && line.Length == 0)
                {
                    // Remove trailing newline from source
                    if (inputBuffer.Length > 0) inputBuffer.Length -= 1;
                    var input = inputBuffer.ToString();
                    var output = outputBuffer.ToString();
                    skip.Clear();
                    if (!input.Contains("<html") || !input.Contains("<head") || !input.Contains("</head") || !input.Contains("<body")) skip.Add("Skipped due to unsupported auto-html/head/body elements");
                    if (input.Contains("<div><p><li>")) skip.Add("Skipped due to unsupported HTML (<ul><li>)");
                    if (input.Contains("<pre")) skip.Add("Skipped due to unsupported HTML (<pre>)");
                    yield return new TheoryDataRow<string, string>(input, output)
                        .WithTrait("path", new Uri(file, UriKind.Absolute) + "?index=" + index)
                        .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
                }
                else
                {
                    outputBuffer.Append(line);
                    outputBuffer.Append('\n');
                }
            }
        }
    }

    static void PrintNode(ref StringBuilder output, int level, MarkupNode node)
    {
        switch (node)
        {
            case MarkupDocument document:
                output.AppendFormat("{0}{1}\n", GetIndent(level), node.Name);
                if (document.DocType != null)
                {
                    output.AppendFormat("{0}<!DOCTYPE {1}", GetIndent(level + 1), document.DocType.RootElement);
                    if (document.DocType.SystemId != null)
                    {
                        if (document.DocType.PublicId != null)
                        {
                            output.Append("PUBLIC");
                            output.Append(document.DocType.PublicId);
                        }
                        else
                        {
                            output.Append("SYSTEM");
                        }
                        output.Append(document.DocType.SystemId);
                    }
                    output.Append(">\n");
                }
                foreach (var child in node.Children) PrintNode(ref output, level + 1, child);
                break;
            case MarkupText:
                output.AppendFormat("{0}\"{1}\"\n", GetIndent(level), node.Value);
                foreach (var child in node.Children) PrintNode(ref output, level + 1, child);
                break;
            case MarkupElement element:
                output.AppendFormat("{0}<{1}>\n", GetIndent(level), element.Name);
                foreach (var child in element.Attributes) output.AppendFormat("{0}{1}=\"{2}\"\n", GetIndent(level + 1), child.Key, child.Value);
                foreach (var child in node.Children) PrintNode(ref output, level + 1, child);
                break;
            case MarkupComment:
                output.AppendFormat("{0}<!-- {1} -->\n", GetIndent(level), node.Value);
                foreach (var child in node.Children) PrintNode(ref output, level + 1, child);
                break;
        }
    }

    static string GetIndent(int level) => level == 0 ? string.Empty : '|' + new string(' ', level * 2 - 1);
}
