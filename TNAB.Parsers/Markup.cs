using System.Text;

namespace TNAB.Parsers;

public abstract record MarkupNodeBase(string Name, string? Value, CustomList<MarkupNode> Children);
public abstract record MarkupNode(string Name, string? Value, CustomList<MarkupNode> Children) : MarkupNodeBase(Name, Value, Children)
{
    public MarkupNode? ParentNode { get; set; } = null;
    protected override bool PrintMembers(StringBuilder builder) => base.PrintMembers(builder);
}
public record MarkupDocument(Uri BaseUri) : MarkupNode("#document", null, [])
{
    public MarkupDocType? DocType { get; set; }
}
public record MarkupDocType(string? RootElement, string? PublicId, string? SystemId);
public record MarkupText(string Value) : MarkupNode("#text", Value, []);
public record MarkupElement(string Name, CustomDictionary<string, string> Attributes) : MarkupNode(Name, null, []);
public record MarkupComment(string Value) : MarkupNode("#comment", Value, []);
public record MarkupStyleSheet(StyleSheet StyleSheet) : MarkupNode("#style-sheet", null, []);
