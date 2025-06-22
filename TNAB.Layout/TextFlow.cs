using System.Globalization;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace TNAB.Layout;

public class TextFlow
{
    public string Text { get; init; }
    public List<TextFlowRun> Runs { get; init; } = [];

    public TextFlow(string text)
    {
        Text = text;
        AnalyseText();
        // FIXME: new SKShaper();
    }

    void AnalyseText()
    {
        // TODO: https://www.unicode.org/reports/tr14/
        // Let's start with simple Western text using whitespace
        var state = AnalyseTextState.Run;
        var run = new List<string>();
        var space = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(Text);
        while (enumerator.MoveNext())
        {
            var text = enumerator.GetTextElement();
            if (state == AnalyseTextState.Run && string.IsNullOrWhiteSpace(text))
            {
                state = AnalyseTextState.Break;
            }
            else if (state == AnalyseTextState.Break && !string.IsNullOrWhiteSpace(text))
            {
                Runs.Add(new(string.Join("", run), string.Join("", space)));
                run.Clear();
                space.Clear();
                state = AnalyseTextState.Run;
            }
            if (state == AnalyseTextState.Run) run.Add(text);
            else space.Add(text);
        }
        Runs.Add(new(string.Join("", run), string.Join("", space)));
    }

    enum AnalyseTextState
    {
        Run,
        Break,
    }
}

public record TextFlowRun(string Run, string Space)
{
    public float RunWidth;
    public float TotalWidth;
    public SKRect Rectangle;
}
