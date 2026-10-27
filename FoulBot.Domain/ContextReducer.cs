namespace FoulBot.Domain;

public interface IContextReducer
{
    IList<FoulMessage> Reduce(IList<FoulMessage> context);

    bool ShouldTrigger(FoulMessage message);
    bool ShouldAlwaysTrigger(FoulMessage message);
}

public sealed class ContextReducer : IContextReducer
{
    public static readonly TimeSpan MoodPeriod = TimeSpan.FromMinutes(30);

    private readonly FoulBotConfiguration _config;
    private readonly ISharedRandomGenerator? _random;
    private readonly BotPersonality _personality;
    private readonly Lock _moodLock = new();
    private IReadOnlyList<string>? _moodPickedFrom;
    private string? _currentMood;
    private DateTime _moodPickedAt;

    public ContextReducer(
        FoulBotConfiguration config,
        ISharedRandomGenerator? random = null,
        BotPersonality? personality = null)
    {
        _config = config;
        _random = random;
        _personality = personality ?? new(config);
    }

    public IList<FoulMessage> Reduce(IList<FoulMessage> context)
    {
        var onlyAddressedToMe = new List<FoulMessage>();
        var onlyAddressedToMeCharactersCount = 0;
        var allMessages = new List<FoulMessage>();
        var allMessagesCharactersCount = 0;

        // TODO: Consider removing attachment messages (binary data) if not removed yet.
        // TODO: Consider storing context in reverse order too, to avoid copying it on every message.
        foreach (var message in context.Reverse())
        {
            if (onlyAddressedToMe.Count < _config.ContextSize
                && onlyAddressedToMeCharactersCount < _config.MaxContextSizeInCharacters / 2
                && (ShouldTrigger(message) || IsMyOwnMessage(message)))
            {
                if (!IsMyOwnMessage(message) && message.SenderType == FoulMessageSenderType.Bot)
                    onlyAddressedToMe.Add(message.AsUser());
                else
                    onlyAddressedToMe.Add(message);

                onlyAddressedToMeCharactersCount += message.Text.Length;
            }

            if (allMessages.Count < _config.ContextSize / 2
                && allMessagesCharactersCount < _config.MaxContextSizeInCharacters / 2
                && !ShouldTrigger(message) && !IsMyOwnMessage(message))
            {
                if (message.SenderType == FoulMessageSenderType.Bot)
                    allMessages.Add(message.AsUser());
                else
                    allMessages.Add(message);

                allMessagesCharactersCount += message.Text.Length;
            }
        }

        List<FoulMessage> reduced =
        [
            FoulMessage.CreateText("Directive", FoulMessageSenderType.System, new("System", null), _personality.Directive, DateTime.MinValue, false, null),
            .. onlyAddressedToMe.Concat(allMessages)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Date)
            .TakeLast(_config.ContextSize)
        ];

        var moods = _personality.Moods;
        var styleHints = _personality.ReplyStyleHints;

        var hints = new List<string>();
        if (moods.Count > 0)
            hints.Add($"Your mood right now: {GetCurrentMood(moods)}");

        if (_random != null && styleHints.Count > 0)
        {
            var hint = styleHints[_random.Generate(0, styleHints.Count - 1)];
            hints.Add($"Style of your next reply, unless someone asked you to write or explain something: {hint}");
        }

        if (hints.Count > 0)
        {
            reduced.Add(FoulMessage.CreateText(
                "StyleHint", FoulMessageSenderType.System, new("System", null),
                string.Join(' ', hints), DateTime.MaxValue, false, null));
        }

        return reduced;
    }

    // Random mood that changes every MoodPeriod. Picked again when the moods change (bot mode was switched).
    // Not persisted: a restart gives a new random mood.
    private string GetCurrentMood(IReadOnlyList<string> moods)
    {
        lock (_moodLock)
        {
            if (_currentMood == null
                || !ReferenceEquals(_moodPickedFrom, moods)
                || DateTime.UtcNow - _moodPickedAt > MoodPeriod)
            {
                _currentMood = moods[_random?.Generate(0, moods.Count - 1) ?? 0];
                _moodPickedFrom = moods;
                _moodPickedAt = DateTime.UtcNow;
            }

            return _currentMood;
        }
    }

    public bool ShouldTrigger(FoulMessage message)
    {
        return _config.KeyWords.Any(keyword => message.Text.Contains(keyword, StringComparison.InvariantCultureIgnoreCase))
            || ShouldAlwaysTrigger(message);
    }

    public bool ShouldAlwaysTrigger(FoulMessage message)
    {
        return !message.IsOriginallyBotMessage && _config.Triggers.Any(trigger =>
            message.Text.Equals(trigger, StringComparison.InvariantCultureIgnoreCase)
            || message.Text.StartsWith($"{trigger} ", StringComparison.InvariantCultureIgnoreCase)
            || message.Text.EndsWith($" {trigger}", StringComparison.InvariantCultureIgnoreCase)
            || message.Text.Contains($" {trigger} ", StringComparison.InvariantCultureIgnoreCase));
    }

    private bool IsMyOwnMessage(FoulMessage message)
    {
        return message.SenderType == FoulMessageSenderType.Bot
            && message.SenderName == _config.BotName;
    }
}
