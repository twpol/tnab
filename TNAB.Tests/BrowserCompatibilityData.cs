using System.Collections.Immutable;
using System.Text.Json;
using TNAB.Browser;
using TNAB.Layout;
using TNAB.Network;
using TNAB.Renderer.Skia;
using Xunit.Internal;

namespace TNAB.Tests;

public class BrowserCompatibilityData(BrowserCompatibilityDataFixture fixture) : IClassFixture<BrowserCompatibilityDataFixture>
{
    BrowserCompatibilityDataFixture Fixture { get; init; } = fixture;

    [Theory]
    [MemberData(nameof(GetWebPlatformTests))]
    public async ValueTask WebPlatformTests(Uri testUri)
    {
        var usedFeatures = new HashSet<string>();
        var network = new NetworkManager();
        var navigable = new Navigable(network);
        navigable.FeatureUsed += (sender, e) => usedFeatures.Add(e.Feature);
        await navigable.Navigate(testUri);
        var layout = new BoxParser(navigable.ActiveDocument);
        layout.Parse();
        var renderer = new SkiaRenderer(layout.Root);
        renderer.Render();
        foreach (var feature in usedFeatures)
        {
            var featureParts = feature.Split('.');
            if (feature.StartsWith("html.elements.") && featureParts.Length >= 4 && featureParts[3].StartsWith("on")) Assert.Skip($"Skipped due to unsupported HTML ({feature})");
            if (feature.StartsWith("css.properties.") && featureParts.Length >= 3 && featureParts[2].StartsWith('-')) Assert.Skip($"Skipped due to prefixed CSS ({feature})");
            Fixture.TestsRun++;
            if (Fixture.ExtraFeatures.Contains(feature))
            {
                Fixture.ExtraFeaturesUsed.Add(feature);
            }
            else
            {
                if (feature.StartsWith("http.")) Assert.Contains(feature, Fixture.KnownFeatures);
                if (feature.StartsWith("html.")) Assert.Contains(feature, Fixture.KnownFeatures);
                if (feature.StartsWith("css.")) Assert.Contains(feature, Fixture.KnownFeatures);
            }
            Fixture.TestsPassed++;
        }
    }

    public static IEnumerable<TheoryDataRow<Uri>> GetWebPlatformTests()
    {
        var root = Path.GetFullPath("../../../..");
        var skip = new List<string>();
        // Top five folders in Web Platform Tests (WPT) containing ref-tests
        var paths = new string[] {
            // Path.Join(root, "tests", "wpt", "mathml"),
            Path.Join(root, "tests", "wpt", "webvtt"),
            // Path.Join(root, "tests", "wpt", "svg"),
            Path.Join(root, "tests", "wpt", "html"),
            Path.Join(root, "tests", "wpt", "css"),
        };
        var fileExtensions = new string[] {
            ".htm",
            ".html",
            ".xht",
            ".xhtml",
        };
        foreach (var path in paths)
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!fileExtensions.Contains(Path.GetExtension(file))) continue;
                var html = File.ReadAllText(file);
                skip.Clear();
                if (Path.GetFileName(file) == "mask-image-cors-001-frame.sub.html") skip.Add("???");
                if (html.Contains("<script")) skip.Add("Skipped due to unsupported HTML (scripting)");
                if (html.Contains("<svg")) skip.Add("Skipped due to unsupported HTML (SVG)");
                if (html.Contains("xml:lang")) skip.Add("Skipped due to unsupported HTML (xml:lang)");
                yield return new TheoryDataRow<Uri>(new Uri(file, UriKind.Absolute))
                    .WithTrait("path", new Uri(file, UriKind.Absolute).ToString())
                    .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
            }
        }
    }
}

public class BrowserCompatibilityDataFixture : IDisposable
{
    public ImmutableHashSet<string> KnownFeatures { get; init; }
    public ImmutableHashSet<string> ExtraFeatures { get; init; }
    public HashSet<string> ExtraFeaturesUsed { get; } = [];
    public int TestsRun = 0;
    public int TestsPassed = 0;

    public BrowserCompatibilityDataFixture()
    {
        var root = Path.GetFullPath("../../../..");
        KnownFeatures = [
            ..GetKnownFeatures(Path.Join(root, "tests", "bcd", "http")),
            ..GetKnownFeatures(Path.Join(root, "tests", "bcd", "html")),
            ..GetKnownFeatures(Path.Join(root, "tests", "bcd", "css")),
            // Standard features not found in the expected part of Browser Compatibility Data (BCD):
            "html.aria_attributes.role",
            ..from feature in GetKnownFeatures(Path.Join(root, "tests", "bcd", "api", "Element.json"))
                where feature.StartsWith("api.Element.aria")
                select "html.aria_attributes." + feature[16..].ToLowerInvariant() into feature
                select feature.EndsWith("element") ? feature[..^7] : feature.EndsWith("elements") ? feature[..^8] : feature,
            // Standard features not found in the Browser Compatibility Data (BCD):
            "css.properties.anchor-name",
            "css.properties.position-anchor",
            "css.properties.position-visibility",
            "css.types.anchor-size",
            "css.types.anchor",
            "html.elements.input.switch",
            "html.elements.input.type",
            "html.elements.input.value",
            "http.status.200",
        ];
        Console.WriteLine($"Loaded {KnownFeatures.Count} known features from Browser Compatibility Data (BCD)");
        // Additional features Web Platform Tests (WPT) uses which are not found in the Browser Compatibility Data (BCD):
        ExtraFeatures = [
            // HTML Global Attributes
            "html.global_attributes.anchor",
            // HTML Elements & Attributes
            "html.elements.abs",
            "html.elements.atomic",
            "html.elements.bar",
            "html.elements.black",
            "html.elements.blue",
            "html.elements.body.dropzone",
            "html.elements.body.marginheight",
            "html.elements.body.marginwidth",
            "html.elements.bogus",
            "html.elements.box1",
            "html.elements.box2",
            "html.elements.box3",
            "html.elements.box4",
            "html.elements.c",
            "html.elements.columns",
            "html.elements.d",
            "html.elements.dir.start",
            "html.elements.dir.type",
            "html.elements.div.alt",
            "html.elements.div.attr-test-invalid1",
            "html.elements.div.attr-test-invalid2",
            "html.elements.div.attr-test-invalid3",
            "html.elements.div.attr-test-valid1",
            "html.elements.div.bar",
            "html.elements.div.baz",
            "html.elements.div.behavior",
            "html.elements.div.does-exist",
            "html.elements.div.dropzone",
            "html.elements.div.foo",
            "html.elements.div.height",
            "html.elements.div.target",
            "html.elements.div.width",
            "html.elements.div.xml:id", // TODO: Does BCD use `:` for this?
            "html.elements.duv", // TODO: Typo
            "html.elements.e",
            "html.elements.even",
            "html.elements.f",
            "html.elements.fast",
            "html.elements.fl",
            "html.elements.flexbox",
            "html.elements.float",
            "html.elements.font.xmlns",
            "html.elements.foo",
            "html.elements.form.accept",
            "html.elements.frameset.frameborder",
            "html.elements.frameset.name",
            "html.elements.grid",
            "html.elements.halt",
            "html.elements.highlight",
            "html.elements.html.xlink",
            "html.elements.ib",
            "html.elements.ibi",
            "html.elements.ibo",
            "html.elements.id",
            "html.elements.image.height",
            "html.elements.image.src",
            "html.elements.image.width",
            "html.elements.image",
            "html.elements.inner",
            "html.elements.inneritem",
            "html.elements.input.optional",
            "html.elements.input.orient",
            "html.elements.input.required",
            "html.elements.input.size",
            "html.elements.item",
            "html.elements.item1",
            "html.elements.item2",
            "html.elements.left",
            "html.elements.li.dropzone",
            "html.elements.li.start",
            "html.elements.ling.href",
            "html.elements.ling.rel",
            "html.elements.ling", // TODO: Typo
            "html.elements.link.content",
            "html.elements.link.herf",
            "html.elements.link.name",
            "html.elements.link.ref",
            "html.elements.link.z",
            "html.elements.m",
            "html.elements.math.definitionurl",
            "html.elements.math", // TODO: MathML support will change this
            "html.elements.menu.type",
            "html.elements.menuitem",
            "html.elements.meta.assert",
            "html.elements.meta.chartset", // TODO: Typo
            "html.elements.meta.encoding",
            "html.elements.meta.flags",
            "html.elements.meta.href",
            "html.elements.meta.rel",
            "html.elements.meta.value",
            "html.elements.n",
            "html.elements.nocollapse",
            "html.elements.o",
            "html.elements.object.src",
            "html.elements.option.text",
            "html.elements.outer",
            "html.elements.p.color-me",
            "html.elements.p.xml:base", // TODO: Does BCD use `:` for this?
            "html.elements.pad",
            "html.elements.padding",
            "html.elements.permission.type",
            "html.elements.permission",
            "html.elements.progress.min",
            "html.elements.rb.pseudo",
            "html.elements.rbb",
            "html.elements.rbc",
            "html.elements.replaceme_container_tag.replaceme_src_attr",
            "html.elements.replaceme_container_tag",
            "html.elements.rt.pseudo",
            "html.elements.rtc.pseudo",
            "html.elements.selectedoption",
            "html.elements.selectlist",
            "html.elements.sep",
            "html.elements.spacer",
            "html.elements.subgrid",
            "html.elements.table.background",
            "html.elements.table.bordercolor",
            "html.elements.table.height",
            "html.elements.table.layout",
            "html.elements.target",
            "html.elements.target1",
            "html.elements.tbody.background",
            "html.elements.tbody.height",
            "html.elements.td.background",
            "html.elements.td.height",
            "html.elements.td.nowrap",
            "html.elements.template.content",
            "html.elements.tfoot.background",
            "html.elements.tfoot.height",
            "html.elements.th.background",
            "html.elements.thead.background",
            "html.elements.thead.height",
            "html.elements.tr.background",
            "html.elements.tr.height",
            "html.elements.ul.start",
            "html.elements.webkit",
            "html.elements.whatever",
            "html.elements.x",
            "html.elements.xl",
            "html.elements.xr",
            "html.elements.y",
            "html.elements.z",
            // CSS Selectors
            "css.selectors.details-content",
            // CSS At-Rules & Properties
            "css.at-rules.page.margin",
            "css.at-rules.position-try",
            "css.at-rules.position-try.height",
            "css.at-rules.position-try.left",
            "css.at-rules.position-try.top",
            "css.at-rules.position-try.width",
            // CSS Properties
            "css.properties.inset-area",
            "css.properties.outline-bottom",
            "css.properties.position-try-options",
            // TODO: The following all appear to be broken parsing in TNAB:
            // /tests/wpt/html/semantics/forms/the-input-element/range-intrinsic-size-ref.html
            "html.elements.input.<",
            "html.elements.input.body",
            // /tests/wpt/html/syntax/xmldecl/support/meta-inside-xml-charset-before-encoding-trail.htm
            "html.elements.meta.?",
            // /tests/wpt/css/css-backgrounds/box-shadow-029.html
            "html.elements.length",
            "html.elements.color",
            "html.elements.shadow",
            // /tests/wpt/css/css-backgrounds/box-shadow-invalid-001.html
            "html.elements.length.[0,∞]",
            // /tests/wpt/css/css-contain/contain-inline-size-grid-indefinite-height-min-height-flex-row.html
            "html.elements.link.<link",
            // /tests/wpt/css/css-content/element-replacement.html
            "html.elements.meta.content\"this",
            "html.elements.meta.test",
            "html.elements.meta.checks",
            "html.elements.meta.that",
            "html.elements.meta.the",
            "html.elements.meta.css",
            "html.elements.meta.propertly",
            "html.elements.meta.can",
            "html.elements.meta.replace",
            "html.elements.meta.a",
            "html.elements.meta.normal",
            "html.elements.meta.element's",
            "html.elements.meta.contents\"",
            // /tests/wpt/css/css-fonts/language-specific-01.html
            "html.elements.meta.i\".\"",
            // /tests/wpt/css/css-lists/before-after-selectors-on-code-element-crash.html
            "html.elements.html.<head",
            // /tests/wpt/css/css-overflow/scroll-marker-group-005-ref.html
            "html.elements.div.\"",
            // /tests/wpt/css/compositing/svg/mix-blend-mode-in-svg-image.html
            "html.elements.link.\"",
            // /tests/wpt/css/css-align/baseline-rules/synthesized-baseline-table-cell-001-ref.html
            "html.elements.x.<",
            "html.elements.x.td",
            // /tests/wpt/css/css-break/grid/grid-item-008.html
            "html.elements.div.<",
            "html.elements.div.div",
            // /tests/wpt/css/css-gaps/multicol/multicol-gap-decorations-007-ref.html
            "html.elements.div.;",
            // /tests/wpt/css/css-grid/subgrid/line-names-010-ref.html
            "html.elements.x.\"",
            // /tests/wpt/css/css-text/hyphens/hyphens-auto-010.html
            "html.elements.https:",
            "html.elements.https:.codepoints.net",
            "html.elements.https:.u+002d",
            // /tests/wpt/css/css-text/letter-spacing/letter-spacing-bidi-003.xht
            "html.elements.ls",
            "html.elements.space",
            "html.elements.space.stretched",
            "html.elements.space.from",
            "html.elements.space.justification",
            // /tests/wpt/css/css-text/letter-spacing/letter-spacing-bidi-004.xht
            "html.elements.bg",
            "html.elements.gap",
            // /tests/wpt/css/css-text/text-transform/text-transform-fullwidth-005-ref.xht
            "html.elements.wide",
            // /tests/wpt/css/CSS2/syntax/square-brackets-001.xht
            "html.elements.https:.drafts.csswg.org",
            "html.elements.https:.css-syntax",
            "html.elements.https:.#consume-a-list-of-rules",
        ];
    }

    static HashSet<string> GetKnownFeatures(string path)
    {
        var features = new HashSet<string>();
        foreach (var file in Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories) : [path])
        {
            var rootElement = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
            features.AddRange(GetKnownFeatures([], rootElement));
        }
        return features;
    }

    static HashSet<string> GetKnownFeatures(string[] path, JsonElement element)
    {
        var features = new HashSet<string>();
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name == "__compat")
            {
                features.Add(string.Join(".", path));
            }
            else
            {
                features.AddRange(GetKnownFeatures([.. path, prop.Name], prop.Value));
            }
        }
        return features;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (TestsPassed == TestsRun)
        {
            var messages = new List<string>();
            foreach (var feature in ExtraFeatures)
            {
                if (KnownFeatures.Contains(feature)) messages.Add($"Extra feature is known: {feature}");
                else if (!ExtraFeaturesUsed.Contains(feature)) messages.Add($"Unused extra feature: {feature}");
            }
            messages = [.. messages.Order()];
            for (var i = 0; i < messages.Count; i++)
            {
                Console.WriteLine($"{1 + i} of {messages.Count}: {messages[i]}");
            }
            // TODO: Last two lines usually get cut off?
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
