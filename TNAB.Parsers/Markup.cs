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
}

public abstract record Node(NodeType NodeType, string NodeName, string? NodeValue, CustomList<Node> Children);
public record Element(string NodeName, CustomDictionary<string, string> Attributes) : Node(NodeType.Element, NodeName, null, []);
// Not implemented: public record Attribute(string NodeName, string NodeValue) : Node(NodeType.Attribute, NodeName, NodeValue, []);
public record Text(string NodeValue) : Node(NodeType.Text, "#text", NodeValue, []);
// Not implemented: public record CDataSection(string NodeValue) : Node(NodeType.CDataSection, "#cdata-section", NodeValue, []);
// Not implemented: public record ProcessingInstruction(string NodeName, string NodeValue) : Node(NodeType.PRocessingInstruction, NodeName, NodeValue, []);
public record Comment(string NodeValue) : Node(NodeType.Comment, "#comment", NodeValue, []);
public record Document() : Node(NodeType.Document, "#document", null, []);
// Not implemented: public record DocumentType(string NodeName) : Node(NodeType.DocumentType, NodeName, null, []);
// Not implemented: public record DocumentFragment() : Node(NodeType.DocumentFragment, "#document-fragment", null, []);
