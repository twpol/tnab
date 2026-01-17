using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TNAB.Parsers.Generators;

[Generator(LanguageNames.CSharp)]
public class CssParserGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext initContext)
    {
        initContext.RegisterPostInitializationOutput(context => context.AddSource("CssParserGenerator.init.g.cs", "public partial class CssParserGeneratorInit {}"));

        initContext.RegisterSourceOutput(
            initContext.SyntaxProvider.CreateSyntaxProvider(
                static (n, _) => n is ClassDeclarationSyntax,
                static (n, _) => n.Node.ToString()
            ),
            (context, data) =>
            {
                context.AddSource("CssParserGenerator.g.cs", @$"
                    public partial class CssParserGenerator {{
                        public static string {data} = "";
                    }}
                ");
            }
        );
    }
}
