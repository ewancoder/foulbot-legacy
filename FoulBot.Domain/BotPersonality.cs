namespace FoulBot.Domain;

/// <summary>
/// Current personality of the bot in a chat: the default one,
/// or the special mode that is switched on by a chat command.
/// </summary>
public sealed class BotPersonality
{
    private readonly FoulBotConfiguration _config;
    private volatile bool _isSpecialModeOn;

    public BotPersonality(FoulBotConfiguration config, bool isSpecialModeOn = false)
    {
        _config = config;
        IsSpecialModeOn = isSpecialModeOn;
    }

    public bool IsSpecialModeOn
    {
        get => _isSpecialModeOn;
        set => _isSpecialModeOn = value && _config.SpecialMode != null;
    }

    private BotSpecialMode? ActiveSpecialMode => IsSpecialModeOn ? _config.SpecialMode : null;

    public string Directive => ActiveSpecialMode?.Directive ?? _config.Directive;
    public IReadOnlyList<string> ReplyStyleHints => ActiveSpecialMode?.ReplyStyleHints ?? _config.ReplyStyleHints;
    public IReadOnlyList<string> Moods => ActiveSpecialMode?.Moods ?? _config.Moods;
}
