using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TNAB.Parsers;

namespace TNAB.Tests;

public class Html5LibTests
{
    // TODO: encoding suite

    // TODO: serializer suite

    [Theory]
    [MemberData(nameof(GetTokeniserTests))]
    public void Tokeniser(TokeniserTest test)
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", test.Input)));
        var htmlTokeniser = new HtmlTokeniser(input);
        var output = new JsonArray();
        var tagName = "";
        var attributes = new JsonObject();
        var attributeName = "";
        foreach (var token in htmlTokeniser.GetTokens(test.InitialState, test.LastStartTag))
        {
            switch (token.Type)
            {
                case HtmlTokeniser.TokenType.Data:
                    output.Add(new JsonArray("Character", token.Value));
                    break;
                case HtmlTokeniser.TokenType.TagOpen:
                    tagName = token.Value;
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeName:
                    attributeName = token.Value;
                    break;
                case HtmlTokeniser.TokenType.TagOpenAttributeValue:
                    if (!attributes.ContainsKey(attributeName)) attributes.Add(attributeName, token.Value);
                    break;
                case HtmlTokeniser.TokenType.TagOpenEnd:
                    output.Add(new JsonArray("StartTag", tagName, attributes));
                    attributes = [];
                    break;
                case HtmlTokeniser.TokenType.TagClose:
                    if (token.Value.Length == 0)
                    {
                        if (output.Count > 0 && output.Last() is JsonArray array) array.Add(true);
                    }
                    else
                    {
                        output.Add(new JsonArray("EndTag", token.Value));
                    }
                    break;
                case HtmlTokeniser.TokenType.Comment:
                    output.Add(new JsonArray("Comment", token.Value));
                    break;
                case HtmlTokeniser.TokenType.DocType:
                    var parts = token.Value.Split('\0').Select(s => s switch
                    {
                        "" => null,
                        "True" => true,
                        "False" => false,
                        _ => (JsonNode)s,
                    });
                    output.Add(new JsonArray(["DOCTYPE", .. parts]));
                    break;

            }
        }
        Assert.Equal(test.Output, output.ToJsonString(CompressedJson));
    }

    public static IEnumerable<TheoryDataRow<TokeniserTest>> GetTokeniserTests()
    {
        var skip = new List<string>();
        foreach (var file in Directory.GetFiles("../../../../tests/html5lib-tests/tokenizer", "*.test"))
        {
            var root = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
            var index = 0;
            if (root.TryGetProperty("tests", out var tests))
            {
                foreach (var test in tests.EnumerateArray())
                {
                    index++;
                    foreach (var initialState in GetInitialStates(test))
                    {
                        var input = test.GetProperty("input").GetString() ?? "";
                        var output = JsonSerializer.Serialize(test.GetProperty("output"), CompressedJson);
                        var lastTag = test.TryGetProperty("lastStartTag", out var lastStartTag) ? lastStartTag.ToString() : "";
                        skip.Clear();
                        if (Path.GetFileName(file) == "namedEntities.test") skip.Add("Skipped due to unsupported tests (named entities)");
                        if (Path.GetFileName(file) == "numericEntities.test") skip.Add("Skipped due to unsupported tests (numeric entities)");
                        if (input.Contains('&', StringComparison.OrdinalIgnoreCase)) skip.Add("Skipped due to unsupported HTML (entities)");
                        if (test.TryGetProperty("errors", out var _)) skip.Add("Skipped due to unsupported error reporting");
                        yield return new TheoryDataRow<TokeniserTest>(new(Path.GetFileName(file), input, initialState, lastTag, output))
                            .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
                    }
                }
            }
        }
    }

    public record TokeniserTest(string File, string Input, HtmlTokeniser.State InitialState, string LastStartTag, string Output);

    static readonly JsonSerializerOptions CompressedJson = new()
    {
        WriteIndented = false,
    };

    static IEnumerable<HtmlTokeniser.State> GetInitialStates(JsonElement test)
    {
        if (test.TryGetProperty("initialStates", out var initialStates))
        {
            foreach (var initialState in initialStates.EnumerateArray())
            {
                yield return initialState.GetString() switch
                {
                    "Data state" => HtmlTokeniser.State.Data,
                    "PLAINTEXT state" => HtmlTokeniser.State.PlaintText,
                    "RCDATA state" => HtmlTokeniser.State.RCData,
                    "RAWTEXT state" => HtmlTokeniser.State.RawText,
                    "Script data state" => HtmlTokeniser.State.ScriptData,
                    "CDATA section state" => HtmlTokeniser.State.CData,
                    _ => throw new InvalidDataException($"Unknown initial state <{initialState}>"),
                };
            }
        }
        else
        {
            yield return HtmlTokeniser.State.Data;
        }
    }

    [Theory]
    [MemberData(nameof(GetTreeConstructionTests))]
    public void TreeConstruction(TreeConstructionTest test)
    {
        var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", test.Input)));
        var htmlParser = new HtmlParser(input);
        htmlParser.Parse();
        var actual = new StringBuilder();
        PrintNode(ref actual, 0, htmlParser.Root);
        Assert.Equal(test.Output, actual.ToString());
    }

    public static IEnumerable<TheoryDataRow<TreeConstructionTest>> GetTreeConstructionTests()
    {
        var inputBuffer = new StringBuilder();
        var outputBuffer = new StringBuilder();
        var skip = new List<string>();
        foreach (var file in Directory.GetFiles("../../../../tests/html5lib-tests/tree-construction", "*.dat"))
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
                    yield return new TheoryDataRow<TreeConstructionTest>(new(Path.GetFileName(file), input, output))
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

    public record TreeConstructionTest(string File, string Input, string Output);

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
