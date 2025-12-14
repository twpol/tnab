using System.Text.Json;
using TNAB.Browser;
using TNAB.Layout;
using TNAB.Network;
using TNAB.Parsers;
using TNAB.Renderer.Skia;

namespace TNAB.Tests;

public class WebPlatformTests
{
    [Theory]
    [MemberData(nameof(GetCrashTests))]
    public async ValueTask CrashTests(CrashTest test)
    {
        var network = new NetworkManager();
        var navigable = new Navigable(network);
        await navigable.Navigate(new Uri(test.File));
        var layout = new BoxParser(navigable.ActiveDocument);
        layout.Parse();
        var renderer = new SkiaRenderer(layout.Root);
        renderer.Render();
    }

    public record CrashTest(string File, CustomDictionary<string, string> Properties);

    public static IEnumerable<TheoryDataRow<CrashTest>> GetCrashTests()
    {
        var skip = new List<string>();
        var basePath = Path.GetFullPath("../../../../tests/wpt");
        using var stream = File.OpenRead(Path.GetFullPath("MANIFEST.json", basePath));
        CreateJsonReader(stream, out var state);
        while (AdvanceJsonReader(ref state, out var reader))
        {
            if (reader.TokenType != JsonTokenType.StartArray) continue;
            if (state.Path.Count < 3) continue;
            if (state.Path[0] != "items") continue;
            if (state.Path[1] != "crashtest") continue;

            AdvanceJsonReader(ref state, out reader);
            var hash = reader.GetString();

            AdvanceJsonReader(ref state, out reader);
            Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

            AdvanceJsonReader(ref state, out reader);
            var url = reader.GetString();
            Assert.Null(url);

            AdvanceJsonReader(ref state, out reader);
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var test = new CrashTest(Path.GetFullPath(string.Join('/', state.Path.Skip(2)), basePath), []);

            while (AdvanceJsonReader(ref state, out reader))
            {
                if (reader.TokenType == JsonTokenType.EndObject) break;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var key = reader.GetString();
                    Assert.NotNull(key);

                    AdvanceJsonReader(ref state, out reader);
                    var value = reader.TokenType switch
                    {
                        JsonTokenType.String => reader.GetString(),
                        JsonTokenType.True => "true",
                        JsonTokenType.False => "false",
                        _ => throw new InvalidDataException($"Unknown value type for test data properties: {reader.TokenType}"),
                    };
                    Assert.NotNull(value);

                    test.Properties[key] = value;
                }
                else
                {
                    throw new InvalidDataException($"Unknown token in test data: {reader.TokenType}");
                }
            }

            skip.Clear();
            if (test.Properties.Count > 0) skip.Add("Skipped due to unsupported tests (key/value properties)");
            yield return new TheoryDataRow<CrashTest>(test)
                .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);
        }
    }

    record StreamJsonReaderState(Stream Stream, byte[] BufferArray, Memory<byte> Buffer, JsonReaderState JsonReaderState, List<string> Path);

    static void CreateJsonReader(Stream stream, out StreamJsonReaderState state)
    {
        var buffer = new byte[1024 * 16];
        state = new StreamJsonReaderState(stream, buffer, buffer.AsMemory(0, stream.Read(buffer)), default, []);
    }

    static bool AdvanceJsonReader(ref StreamJsonReaderState state, out Utf8JsonReader reader)
    {
        var buffer = state.Buffer;
        reader = new(buffer.Span, false, state.JsonReaderState);

        var read = reader.Read();
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
