namespace Importer.Models;

/// <summary>
/// HttpArena's classification of a framework, from its <c>meta.json</c>: the <c>type</c> tier
/// (flagship, emerging, experimental, engine, infrastructure) and the <c>mode</c> (standard
/// or tuned). Defaults match the HttpArena leaderboard: a missing type reads as emerging, a
/// missing mode as standard.
/// </summary>
public sealed record Classification(string Type, string Mode)
{
    public const string DefaultType = "emerging";
    public const string DefaultMode = "standard";
    public const string TunedMode = "tuned";

    public static readonly Classification Default = new(DefaultType, DefaultMode);

    public bool Tuned => Mode == TunedMode;
}
