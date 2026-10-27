namespace DTech.Entrypoint.Lifecycle;

internal sealed record ExitStateResponse(string State)
{
    public static ExitStateResponse From(ApplicationExitState state)
    {
        var name = ExitStateNames.Of(state);
        return new ExitStateResponse(name);
    }
}
