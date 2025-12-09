using System.Text.Json;
using System.Text.Json.Nodes;
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
        Console.WriteLine($"CrashTests: testUri = {testUri}");
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
        Console.WriteLine($"GetCrashTestsHandler: path = {path}");
        Console.WriteLine($"GetCrashTestsHandler: testUri = {testUri}");
        var testCases = json as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {json}");
        if (testCases.Count < 2) throw new InvalidDataException($"Invalid test data; expected 2+ items, got {testCases.Count}");

        var hash = (string?)json[0] ?? throw new InvalidDataException($"Invalid test data; expected string, got {json[0]}");

        for (var index = 1; index < testCases.Count; index++)
        {
            var testCase = testCases[index] as JsonArray ?? throw new InvalidDataException($"Invalid test data; expected array, got {testCases[index]}");
            if (testCase.Count != 2) throw new InvalidDataException($"Invalid test data; expected 2 items, got {testCase.Count}");
            if (testCase[0] != null) throw new InvalidDataException($"Invalid test data; expected null, got {testCase[0]}");
            var properties = testCase[1] as JsonObject ?? throw new InvalidDataException($"Invalid test data; expected object, got {testCase[1]}");

            var skip = new List<string>();
            if (properties.Count > 0) skip.Add("Skipped due to unsupported tests (key/value properties)");
            yield return new TheoryDataRow<Uri>(testUri)
                .WithTrait("path", testUri + "?index=" + index)
                .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
        }
    }

    static IEnumerable<T> GetTests<T>(string type, Func<string, Uri, JsonNode, IEnumerable<T>> handler)
    {
        var root = Path.GetFullPath("../../../..");
        var rootUri = new Uri(root + "/");
        var baseUri = new Uri(root + "/tests/wpt/");
        Console.WriteLine($"GetTests: root = {root}");
        Console.WriteLine($"GetTests: rootUri = {rootUri}");
        Console.WriteLine($"GetTests: baseUri = {baseUri}");
        using var stream = File.OpenRead(Path.Join(root, "tests", "wpt", "MANIFEST.json"));
        CreateJsonReader(stream, out var state);
        var reader = new Utf8JsonReader();
        while (AdvanceJsonReader(ref state, ref reader))
        {
            if (state.Path.Count < 3) continue;
            if (state.Path[0] != "items") continue;
            if (state.Path[1] != type) continue;
            if (reader.TokenType != JsonTokenType.StartArray) continue;
            Console.WriteLine($"GetTests: state.Path = {state.Path}");
            var uri = new Uri(baseUri, string.Join('/', state.Path.Skip(2)));
            Console.WriteLine($"GetTests: uri = {uri}");
            foreach (var test in handler(rootUri.MakeRelativeUri(uri).OriginalString, uri, ReadDocumentFromJsonReader(ref state, ref reader)))
            {
                yield return test;
                reader = new();
            }
            break;
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
        var read = reader.Read();
        readHook?.Invoke(buffer[..(int)reader.BytesConsumed]);
        buffer = buffer[(int)reader.BytesConsumed..];

        if (!read)
        {
            buffer.CopyTo(state.BufferArray);
            buffer = state.BufferArray.AsMemory(0, buffer.Length + state.Stream.Read(state.BufferArray.AsSpan(buffer.Length)));
            reader = new(buffer.Span, false, reader.CurrentState);
            if (!reader.Read())
            {
                if (reader.CurrentDepth > 0) throw new InvalidDataException("Unable to parse JSON; not enough buffer space?");
                return false;
            }
            readHook?.Invoke(buffer[..(int)reader.BytesConsumed]);
            buffer = buffer[(int)reader.BytesConsumed..];
        }

        while (state.Path.Count > reader.CurrentDepth) state.Path.RemoveAt(state.Path.Count - 1);
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            if (state.Path.Count == reader.CurrentDepth) state.Path.RemoveAt(state.Path.Count - 1);
            state.Path.Add(reader.GetString() ?? "");
        }

        state = state with { Buffer = buffer, JsonReaderState = reader.CurrentState };
        return true;
    }
}
