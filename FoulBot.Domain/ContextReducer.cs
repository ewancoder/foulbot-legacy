namespace FoulBot.Domain;

public interface IContextReducer
{
    IList<FoulMessage> Reduce(IList<FoulMessage> context);

    bool ShouldTrigger(FoulMessage message);
    bool ShouldAlwaysTrigger(FoulMessage message);
}

public sealed class ContextReducer : IContextReducer
{
    public static readonly TimeSpan MoodPeriod = TimeSpan.FromHours(6);

    private readonly FoulBotConfiguration _config;
    private readonly ISharedRandomGenerator? _random;

    public ContextReducer(FoulBotConfiguration config, ISharedRandomGenerator? random = null)
    {
        _config = config;
        _random = random;
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
            FoulMessage.CreateText("Directive", FoulMessageSenderType.System, new("System", null), _config.Directive, DateTime.MinValue, false, null),
            .. onlyAddressedToMe.Concat(allMessages)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.Date)
            .TakeLast(_config.ContextSize)
        ];

        var hints = new List<string>();
        if (_config.Moods.Count > 0)
            hints.Add($"Your mood right now: {GetCurrentMood()}");

        if (_random != null && _config.ReplyStyleHints.Count > 0)
        {
            var hint = _config.ReplyStyleHints[_random.Generate(0, _config.ReplyStyleHints.Count - 1)];
            hints.Add($"Style of your next reply, unless someone asked you to write or explain something: {hint} Swear heavily anyway.");
        }

        if (hints.Count > 0)
        {
            reduced.Add(FoulMessage.CreateText(
                "StyleHint", FoulMessageSenderType.System, new("System", null),
                string.Join(' ', hints), DateTime.MaxValue, false, null));
        }

        return reduced;
    }

    // Same mood for everyone during the period, randomly shuffled between periods.
    private string GetCurrentMood()
    {
        var period = (int)(DateTime.UtcNow.Ticks / MoodPeriod.Ticks % int.MaxValue);
#pragma warning disable CA5394 // Do not use insecure randomness
        var index = new Random(period).Next(_config.Moods.Count);
#pragma warning restore CA5394 // Do not use insecure randomness

        return _config.Moods[index];
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
