using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AnkiHistoryVisualization;

public static partial class SvgPathParser
{
    private static readonly Dictionary<char, PathCommandType> commands = new()
    {
        ['a'] = PathCommandType.EllipticalArc,
        ['c'] = PathCommandType.CurveTo,
        ['h'] = PathCommandType.HorizontalLineTo,
        ['l'] = PathCommandType.LineTo,
        ['m'] = PathCommandType.MoveTo,
        ['q'] = PathCommandType.QuadraticBézierCurve,
        ['s'] = PathCommandType.SmoothCurveTo,
        ['t'] = PathCommandType.SmoothQuadraticBézierCurveTo,
        ['v'] = PathCommandType.VerticalLineTo,
        ['z'] = PathCommandType.ClosePath,
    };

    private static readonly Dictionary<PathCommandType, int> sizes = new()
    {
        [PathCommandType.EllipticalArc] = 7,
        [PathCommandType.CurveTo] = 6,
        [PathCommandType.HorizontalLineTo] = 1,
        [PathCommandType.LineTo] = 2,
        [PathCommandType.MoveTo] = 2,
        [PathCommandType.QuadraticBézierCurve] = 4,
        [PathCommandType.SmoothCurveTo] = 4,
        [PathCommandType.SmoothQuadraticBézierCurveTo] = 2,
        [PathCommandType.VerticalLineTo] = 1,
        [PathCommandType.ClosePath] = 0,
    };

    public static GraphicsPath Parse(string path, SizeF scale, PointF offset)
    {
        var commands = Tokenize(path);

        var graphicsPath = new GraphicsPath();
        var current = offset;

        foreach (var command in commands)
        {
            switch (command.Type)
            {
                case PathCommandType.MoveTo:
                    current = CalcPoint(command, current, scale, offset);
                    break;

                case PathCommandType.LineTo:
                    var pos = CalcPoint(command, current, scale, offset);
                    graphicsPath.AddLine(current, pos);
                    current = pos;
                    break;

                case PathCommandType.ClosePath:
                    graphicsPath.CloseFigure();
                    break;

                default:
                    throw new NotSupportedException($"{command.Type}");
            }
        }

        return graphicsPath;
    }

    private static PointF CalcPoint(PathCommand command, PointF current, SizeF scale, PointF offset)
    {
        return new PointF(
            command.Values[0] * scale.Width + (command.IsAbsolute ? offset.X : current.X),
            command.Values[1] * scale.Height + (command.IsAbsolute ? offset.Y : current.Y)
        );
    }

    private static IEnumerable<PathCommand> Tokenize(string path)
    {
        var matches = RegexCommands().Matches(path).Cast<Match>();

        foreach (var match in matches)
        {
            var (commandType, isAbsolute) = ParseCommandType(match.Value[0]);
            var args = ParseValues(match.Value[1..]);

            if (commandType == PathCommandType.MoveTo && args.Length > 2)
            {
                yield return new(commandType, isAbsolute, args[0..2]);
                commandType = PathCommandType.LineTo;
                args = args[2..];
            }

            var size = sizes[commandType];

            if (size == 0)
            {
                yield return new(commandType, isAbsolute, []);
            }
            else
            {
                foreach (var subargs in args.Chunk(size))
                    yield return new(commandType, isAbsolute, subargs);
            }
        }
    }

    private static (PathCommandType, bool) ParseCommandType(char c)
    {
        var lower = Char.ToLowerInvariant(c);
        var command = commands[lower];
        var isAbsolute = (lower != c);
        return (command, isAbsolute);
    }

    private static float[] ParseValues(string text)
    {
        var numbers = RegexValues().Matches(text);
        return numbers.Cast<Match>().Select(m => float.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
    }

    [GeneratedRegex("([astvzqmhlc])([^astvzqmhlc]*)", RegexOptions.IgnoreCase)]
    private static partial Regex RegexCommands();

    [GeneratedRegex(@"-?[0-9]*\.?[0-9]+(?:e[-+]?\d+)?")]
    private static partial Regex RegexValues();
}

public record PathCommand(PathCommandType Type, bool IsAbsolute, float[] Values);

public enum PathCommandType
{
    MoveTo,
    LineTo,
    HorizontalLineTo,
    VerticalLineTo,
    CurveTo,
    SmoothCurveTo,
    QuadraticBézierCurve,
    SmoothQuadraticBézierCurveTo,
    EllipticalArc,
    ClosePath,
}
