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

    public static IEnumerable<TheoryDataRow<CrashTest>> GetCrashTests()
    {
        var skip = new List<string>();
        var basePath = Path.GetFullPath("../../../../tests/wpt");
        var path = new List<string>();
        using var stream = File.OpenRead(Path.GetFullPath("MANIFEST.json", basePath));
        var bufferArray = new byte[1024 * 16];
        var buffer = bufferArray.AsMemory(0, stream.Read(bufferArray));
        var reader = new Utf8JsonReader(buffer.Span, false, default);
        while (AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader))
        {
            while (path.Count > reader.CurrentDepth) path.RemoveAt(path.Count - 1);
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    if (path.Count == reader.CurrentDepth) path.RemoveAt(path.Count - 1);
                    path.Add(reader.GetString() ?? "");
                    break;
                case JsonTokenType.StartArray:
                    if (path.Count > 2 && path[0] == "items" && path[1] == "crashtest")
                    {
                        AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader);
                        var hash = reader.GetString();

                        AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader);
                        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

                        AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader);
                        var url = reader.GetString();
                        Assert.Null(url);

                        AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader);
                        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

                        var test = new CrashTest(Path.GetFullPath(string.Join('/', path.Skip(2)), basePath), []);

                        while (AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader))
                        {
                            if (reader.TokenType == JsonTokenType.EndObject) break;
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                var key = reader.GetString();
                                Assert.NotNull(key);

                                AdvanceJsonReader(stream, bufferArray, ref buffer, ref reader);
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

                        buffer = buffer[(int)reader.BytesConsumed..];
                        var state = reader.CurrentState;

                        skip.Clear();
                        if (test.Properties.Count > 0) skip.Add("Skipped due to unsupported tests (key/value properties)");
                        yield return new TheoryDataRow<CrashTest>(test)
                            .WithSkip(skip.Count > 0 ? string.Join(", ", skip) : null);

                        reader = new(buffer.Span, false, state);
                    }
                    break;
            }
        }
    }

    public record CrashTest(string File, CustomDictionary<string, string> Properties);

    static bool AdvanceJsonReader(Stream stream, byte[] bufferArray, ref Memory<byte> buffer, ref Utf8JsonReader reader)
    {
        if (!reader.Read())
        {
            buffer = buffer[(int)reader.BytesConsumed..];
            buffer.CopyTo(bufferArray);
            buffer = bufferArray.AsMemory(0, buffer.Length + stream.Read(bufferArray.AsSpan(buffer.Length)));
            reader = new(buffer.Span, false, reader.CurrentState);
            reader.Read();
        }
        return buffer.Length > 0;
    }
}
