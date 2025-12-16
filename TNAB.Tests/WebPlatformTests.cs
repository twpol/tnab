using System.Text.Json;
using System.Text.Json.Nodes;
using Codeuctivity.SkiaSharpCompare;
using SkiaSharp;
using TNAB.Browser;
using TNAB.Layout;
using TNAB.Network;
using TNAB.Renderer.Skia;

namespace TNAB.Tests;

public class WebPlatformTests
{
    [Theory]
    [MemberData(nameof(GetCrashTests))]
    public async ValueTask CrashTests(Uri testUri)
    {
        var network = new NetworkManager();
        var navigable = new Navigable(network);
        await navigable.Navigate(testUri);
        var layout = new BoxParser(navigable.ActiveDocument);
        layout.Parse();
        var renderer = new SkiaRenderer(layout.Root);
        renderer.Render();
    }

    public static IEnumerable<TheoryDataRow<Uri>> GetCrashTests() => GetTests("crashtest", GetCrashTestsHandler);

    static IEnumerable<TheoryDataRow<Uri>> GetCrashTestsHandler(string path, Uri testUri, JsonNode json)
    {
        var testCases = json as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {json}");
        if (testCases.Count < 2) throw new InvalidDataException($"Invalid test data; expected 2+ items, got {testCases.Count}");

        var hash = (string?)json[0] ?? throw new InvalidDataException($"Invalid test data; expected string, got {json[0]}");

        for (var i = 1; i < testCases.Count; i++)
        {
            var testCase = testCases[i] as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {testCases[i]}");
            if (testCase.Count != 2) throw new InvalidDataException($"Invalid test data; expected 2 items, got {testCase.Count}");
            if (testCase[0] != null) throw new InvalidDataException($"Invalid test data; expected null, got {testCase[0]}");
            var properties = testCase[1] as JsonObject ?? throw new InvalidDataException($"Invalid test data; expected object, got {testCase[1]}");

            var skip = new List<string>();
            if (properties.Count > 0) skip.Add("Skipped due to unsupported tests (key/value properties)");
            yield return new TheoryDataRow<Uri>(testUri)
                .WithTrait("path", path)
                .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
        }
    }

    [Theory]
    [MemberData(nameof(GetRefTests))]
    public async ValueTask RefTests(string file, int index, Uri testUrl, Uri referenceUrl, RefTestRelation relation)
    {
        SKImage testImage;
        SKImage referenceImage;
        {
            var network = new NetworkManager();
            var navigable = new Navigable(network);
            await navigable.Navigate(testUrl);
            var layout = new BoxParser(navigable.ActiveDocument);
            layout.Parse();
            var renderer = new SkiaRenderer(layout.Root);
            testImage = renderer.Render();
        }
        {
            var network = new NetworkManager();
            var navigable = new Navigable(network);
            await navigable.Navigate(referenceUrl);
            var layout = new BoxParser(navigable.ActiveDocument);
            layout.Parse();
            var renderer = new SkiaRenderer(layout.Root);
            referenceImage = renderer.Render();
        }

        var testBitmap = SKBitmap.FromImage(testImage);
        var referenceBitmap = SKBitmap.FromImage(referenceImage);
        var differences = Compare.CalcDiff(testBitmap, referenceBitmap, new SKBitmap(testImage.Info));
        var pass = relation switch
        {
            RefTestRelation.Equal => differences.PixelErrorCount == 0,
            RefTestRelation.NotEqual => differences.PixelErrorCount != 0,
            _ => throw new InvalidDataException($"Unknown reftest relation {relation}"),
        };

        // if (!pass)
        // {
        //     var resultPath = Path.GetFullPath(Path.Join("..", "..", "..", "..", "test-results", "wpt", "reftest", file));
        //     Directory.CreateDirectory(resultPath);

        //     var testStream = File.OpenWrite(Path.Join(resultPath, index + "-test.png"));
        //     testBitmap.Encode(testStream, SKEncodedImageFormat.Png, 100);

        //     var referenceStream = File.OpenWrite(Path.Join(resultPath, index + "-reference.png"));
        //     referenceBitmap.Encode(referenceStream, SKEncodedImageFormat.Png, 100);

        //     var differencesBitmap = Compare.CalcDiffMaskImage(testBitmap, referenceBitmap);
        //     using var differencesStream = File.OpenWrite(Path.Join(resultPath, index + "-differences.png"));
        //     differencesBitmap.Encode(differencesStream, SKEncodedImageFormat.Png, 100);
        // }

        Assert.True(pass);
    }

    // relation
    public enum RefTestRelation
    {
        Equal,
        NotEqual,
    }

    public static IEnumerable<TheoryDataRow<string, int, Uri, Uri, RefTestRelation>> GetRefTests() => GetTests("reftest", GetRefTestsHandler);

    static IEnumerable<TheoryDataRow<string, int, Uri, Uri, RefTestRelation>> GetRefTestsHandler(string path, Uri testUri, JsonNode json)
    {
        // Console.WriteLine();
        // Console.WriteLine(basePath);
        // Console.WriteLine(string.Join("/", path));
        // Console.WriteLine(System.Text.Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(json)));

        var testCases = json as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {json}");
        if (testCases.Count < 2) throw new InvalidDataException($"Invalid test data; expected 2+ items, got {testCases.Count}");

        var hash = (string?)json[0] ?? throw new InvalidDataException($"Invalid test data; expected string, got {json[0]}");

        for (var i = 1; i < testCases.Count; i++)
        {
            var testCase = testCases[i] as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {testCases[i]}");
            if (testCase.Count != 3) throw new InvalidDataException($"Invalid test data; expected 3 items, got {testCase.Count}");
            var testPath = (string?)testCase[0] ?? throw new InvalidDataException($"Invalid test data; expected string, got {testCase[0]}");
            var comparisons = testCase[1] as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {testCase[1]}");
            var properties = testCase[2] as JsonObject ?? throw new InvalidDataException($"Invalid test data; expected object, got {testCase[2]}");

            foreach (var comparison in comparisons)
            {
                var components = comparison as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {comparison}");
                if (components.Count != 2) throw new InvalidDataException($"Invalid test data; expected 2 items, got {components.Count}");

                var referencePath = (string?)components[0] ?? throw new InvalidDataException($"Invalid test data; expected string, got {components[0]}");
                if (referencePath[0] == '/') referencePath = referencePath[1..];
                var relation = (string?)components[1] switch
                {
                    "==" => RefTestRelation.Equal,
                    "!=" => RefTestRelation.NotEqual,
                    _ => throw new InvalidDataException($"Invalid test data; expected '==' or '!=', got {comparison}"),
                };

                var testUrl = new Uri(testUri, testPath);
                var referenceUrl = new Uri(testUri, referencePath);
                var skip = new List<string>();
                if (testUrl.Scheme == "about" || referenceUrl.Scheme == "about") skip.Add("Skipped due to unsupported tests (about scheme)");
                if (testUrl.Query.Length > 0 || referenceUrl.Query.Length > 0) skip.Add("Skipped due to unsupported tests (query)");
                if (testUrl.Fragment.Length > 0 || referenceUrl.Fragment.Length > 0) skip.Add("Skipped due to unsupported tests (fragment)");
                if (properties.Count > 0) skip.Add("Skipped due to unsupported tests (key/value properties)");
                yield return new TheoryDataRow<string, int, Uri, Uri, RefTestRelation>(path, i, new Uri(testUri, testPath), new Uri(testUri, referencePath), relation)
                    .WithTrait("path", path)
                    .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
            }
        }
    }

    static IEnumerable<T> GetTests<T>(string type, Func<string, Uri, JsonNode, IEnumerable<T>> handler)
    {
        var root = Path.GetFullPath("../../../../");
        var rootUri = new Uri(root);
        var baseUri = new Uri(root + "tests/wpt/");
        using var stream = File.OpenRead(Path.Join(root, "tests", "wpt", "MANIFEST.json"));
        CreateJsonReader(stream, out var state);
        var reader = new Utf8JsonReader();
        while (AdvanceJsonReader(ref state, ref reader))
        {
            if (state.Path.Count < 3) continue;
            if (state.Path[0] != "items") continue;
            if (state.Path[1] != type) continue;
            if (reader.TokenType != JsonTokenType.StartArray) continue;
            var uri = new Uri(baseUri, string.Join('/', state.Path.Skip(2)));
            foreach (var test in handler(rootUri.MakeRelativeUri(uri).OriginalString, uri, ReadDocumentFromJsonReader(ref state, ref reader)))
            {
                yield return test;
                reader = new();
            }
        }
    }

    record StreamJsonReaderState(Stream Stream, byte[] BufferArray, Memory<byte> Buffer, JsonReaderState JsonReaderState, List<string> Path);

    static void CreateJsonReader(Stream stream, out StreamJsonReaderState state)
    {
        var buffer = new byte[1024 * 16];
        state = new StreamJsonReaderState(stream, buffer, buffer.AsMemory(0, stream.Read(buffer)), default, []);
    }

    static JsonNode ReadDocumentFromJsonReader(ref StreamJsonReaderState state, ref Utf8JsonReader reader)
    {
        var jsonBuffer = new MemoryStream();
        void readHook(Memory<byte> buffer) => jsonBuffer.Write(buffer.Span);

        jsonBuffer.WriteByte(
            reader.TokenType switch
            {
                JsonTokenType.StartObject => (byte)'{',
                JsonTokenType.StartArray => (byte)'[',
                _ => throw new InvalidOperationException("Expected start of object or array"),
            }
        );

        var depth = reader.CurrentDepth;
        do
        {
            AdvanceJsonReader(ref state, ref reader, readHook);
        } while (depth < reader.CurrentDepth);

        jsonBuffer.Seek(0, SeekOrigin.Begin);
        return JsonNode.Parse(jsonBuffer)!;
    }

    static bool AdvanceJsonReader(ref StreamJsonReaderState state, ref Utf8JsonReader reader, Action<Memory<byte>>? readHook = null)
    {
        var buffer = state.Buffer;
        reader = new(buffer.Span, false, state.JsonReaderState);
        // Console.WriteLine($"[A] Length={buffer.Length} >>" + Encoding.UTF8.GetString(buffer.Span) + "<<");
        var read = reader.Read();
        readHook?.Invoke(buffer[..(int)reader.BytesConsumed]);
        buffer = buffer[(int)reader.BytesConsumed..];
        // Console.WriteLine($"[A] Consumed={reader.BytesConsumed} Remaining={buffer.Length}");

        if (!read)
        {
            buffer.CopyTo(state.BufferArray);
            buffer = state.BufferArray.AsMemory(0, buffer.Length + state.Stream.Read(state.BufferArray.AsSpan(buffer.Length)));
            reader = new(buffer.Span, false, reader.CurrentState);
            // Console.WriteLine("Buffer");
            // Console.WriteLine($"[B] Length={buffer.Length} >>" + Encoding.UTF8.GetString(buffer.Span) + "<<");
            if (!reader.Read())
            {
                if (reader.CurrentDepth > 0) throw new InvalidDataException("Unable to parse JSON; not enough buffer space?");
                return false;
            }
            readHook?.Invoke(buffer[..(int)reader.BytesConsumed]);
            buffer = buffer[(int)reader.BytesConsumed..];
            // Console.WriteLine($"[B] Consumed={reader.BytesConsumed} Remaining={buffer.Length}");
        }

        while (state.Path.Count > reader.CurrentDepth) state.Path.RemoveAt(state.Path.Count - 1);
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            if (state.Path.Count == reader.CurrentDepth) state.Path.RemoveAt(state.Path.Count - 1);
            state.Path.Add(reader.GetString() ?? "");
        }
        // Console.WriteLine($"Read={read}  Line={GetUtf8JsonReaderLineNumber(ref reader)}  Depth={reader.CurrentDepth}  Path={string.Join(" / ", state.Path)}  Token={reader.TokenType}");

        state = state with { Buffer = buffer, JsonReaderState = reader.CurrentState };
        return true;
    }

    // [System.Runtime.CompilerServices.UnsafeAccessor(System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "_lineNumber")]
    // extern static ref long GetUtf8JsonReaderLineNumber(ref Utf8JsonReader @this);
}
