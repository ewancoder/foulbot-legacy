using System.Collections.Concurrent;

namespace FoulBot.Domain.Storage;

/// <summary>
/// Stores whether the special mode of the bot is switched on in a chat.
/// </summary>
public interface IBotModeStore
{
    ValueTask<bool> IsSpecialModeOnAsync(FoulBotId botId, FoulChatId chatId);
    ValueTask SetSpecialModeAsync(FoulBotId botId, FoulChatId chatId, bool isOn);
}

public sealed class InMemoryBotModeStore : IBotModeStore
{
    private readonly ConcurrentDictionary<(string BotId, string ChatId), bool> _modes = new();

    public ValueTask<bool> IsSpecialModeOnAsync(FoulBotId botId, FoulChatId chatId)
        => new(_modes.TryGetValue((botId.BotId, chatId.Value), out var isOn) && isOn);

    public ValueTask SetSpecialModeAsync(FoulBotId botId, FoulChatId chatId, bool isOn)
    {
        _modes[(botId.BotId, chatId.Value)] = isOn;
        return default;
    }
}
