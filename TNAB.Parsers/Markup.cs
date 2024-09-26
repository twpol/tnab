using System.Text;

namespace TNAB.Parsers;

public enum NodeType
{
    Element,
    Attribute,
    Text,
    CDataSection,
    ProcessingInstruction,
    Comment,
    Document,
    DocumentType,
    DocumentFragment,
    TNABStyleSheet,
}

public abstract record NodeBase(NodeType NodeType, string NodeName, string? NodeValue, CustomList<Node> Children);
public abstract record Node(NodeType NodeType, string NodeName, string? NodeValue, CustomList<Node> Children) : NodeBase(NodeType, NodeName, NodeValue, Children)
{
    public Node? ParentNode { get; set; } = null;
    public List<T> OfType<T>() where T : Node => OfType<T>([]);
    public List<T> OfType<T>(List<T> list) where T : Node
    {
        for (var i = 0; i < Children.Count; i++) Children[i].OfType(list);
        if (this is T match) list.Add(match);
        return list;
    }
    protected override bool PrintMembers(StringBuilder builder) => base.PrintMembers(builder);
}
public record Element(string NodeName, CustomDictionary<string, string> Attributes) : Node(NodeType.Element, NodeName, null, []);
// Not implemented: public record Attribute(string NodeName, string NodeValue) : Node(NodeType.Attribute, NodeName, NodeValue, []);
public record Text(string NodeValue) : Node(NodeType.Text, "#text", NodeValue, []);
// Not implemented: public record CDataSection(string NodeValue) : Node(NodeType.CDataSection, "#cdata-section", NodeValue, []);
// Not implemented: public record ProcessingInstruction(string NodeName, string NodeValue) : Node(NodeType.PRocessingInstruction, NodeName, NodeValue, []);
public record Comment(string NodeValue) : Node(NodeType.Comment, "#comment", NodeValue, []);
public record Document(Uri BaseUri) : Node(NodeType.Document, "#document", null, []);
// Not implemented: public record DocumentType(string NodeName) : Node(NodeType.DocumentType, NodeName, null, []);
// Not implemented: public record DocumentFragment() : Node(NodeType.DocumentFragment, "#document-fragment", null, []);

// NOTE: TNAB extension
public record TNABStyleSheet(StyleSheet StyleSheet) : Node(NodeType.TNABStyleSheet, "#style-sheet", null, []);
