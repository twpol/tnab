namespace TNAB.Parsers;

public abstract record MarkupNode(string Name, string? Value, CustomList<MarkupNode> Children);
public record MarkupDocument(string? DocType) : MarkupNode("#document", null, []);
public record MarkupText(string Value) : MarkupNode("#text", Value, []);
public record MarkupElement(string Name, CustomDictionary<string, string> Attributes) : MarkupNode(Name, null, []);
public record MarkupComment(string Value) : MarkupNode("#comment", Value, []);
