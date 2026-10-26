using System.Text;

namespace DTech.Core.CommandHistory;

public sealed class ShellIntegrationParser
{
    public const int MaxPayloadBytes = 8 * 1024;

    private const byte Escape = 0x1B;
    private const byte Bell = 0x07;
    private const byte OscIntroducer = (byte)']';
    private const byte StringTerminator = (byte)'\\';

    private readonly List<byte> _payload = [];
    private ParserState _state = ParserState.Ground;
    private bool _overflowed;

    private enum ParserState
    {
        Ground,
        Escape,
        Osc,
        OscEscape,
    }

    public IReadOnlyList<ShellIntegrationEvent> Feed(ReadOnlySpan<byte> data)
    {
        List<ShellIntegrationEvent>? events = null;
        for (var i = 0; i < data.Length; i++)
        {
            var completed = Advance(data[i]);
            if (completed is not null)
            {
                events ??= [];
                events.Add(completed.Value);
            }
        }

        return events ?? (IReadOnlyList<ShellIntegrationEvent>)[];
    }

    private ShellIntegrationEvent? Advance(byte value)
    {
        switch (_state)
        {
            case ParserState.Ground:
                _state = value == Escape ? ParserState.Escape : ParserState.Ground;
                return null;
            case ParserState.Escape:
                StartOscIfIntroduced(value);
                return null;
            case ParserState.Osc:
                return AdvanceInsideOsc(value);
            default:
                return AdvanceAfterEscapeInsideOsc(value);
        }
    }

    private void StartOscIfIntroduced(byte value)
    {
        if (value == OscIntroducer)
        {
            _payload.Clear();
            _overflowed = false;
            _state = ParserState.Osc;
            return;
        }

        _state = value == Escape ? ParserState.Escape : ParserState.Ground;
    }

    private ShellIntegrationEvent? AdvanceInsideOsc(byte value)
    {
        if (value == Bell)
        {
            return Complete();
        }

        if (value == Escape)
        {
            _state = ParserState.OscEscape;
            return null;
        }

        if (_payload.Count >= MaxPayloadBytes)
        {
            _overflowed = true;
            return null;
        }

        _payload.Add(value);
        return null;
    }

    private ShellIntegrationEvent? AdvanceAfterEscapeInsideOsc(byte value)
    {
        if (value == StringTerminator)
        {
            return Complete();
        }

        _state = value == Escape ? ParserState.Escape : ParserState.Ground;
        return null;
    }

    private ShellIntegrationEvent? Complete()
    {
        _state = ParserState.Ground;
        if (_overflowed)
        {
            return null;
        }

        var payload = Encoding.UTF8.GetString(_payload.ToArray());
        return ShellIntegrationPayload.Parse(payload);
    }
}
