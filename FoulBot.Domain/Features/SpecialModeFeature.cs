using FoulBot.Domain.Storage;

namespace FoulBot.Domain.Features;

/// <summary>
/// Toggles the special mode of the bot when its command is written to the chat.
/// </summary>
public sealed class SpecialModeFeature : BotFeature
{
    private readonly BotPersonality _personality;
    private readonly IBotModeStore _botModeStore;
    private readonly IFoulBot _bot;
    private readonly FoulBotConfiguration _config;
    private readonly FoulChatId _chatId;

    public SpecialModeFeature(
        BotPersonality personality,
        IBotModeStore botModeStore,
        IFoulBot bot,
        FoulBotConfiguration config,
        FoulChatId chatId)
    {
        _personality = personality;
        _botModeStore = botModeStore;
        _bot = bot;
        _config = config;
        _chatId = chatId;
    }

    public override async ValueTask<bool> ProcessMessageAsync(FoulMessage message)
    {
        var command = _config.SpecialMode?.Command;
        if (command == null || message.IsOriginallyBotMessage || message.Text.Trim() != command)
            return false;

        var isOn = !_personality.IsSpecialModeOn;
        await _botModeStore.SetSpecialModeAsync(_config.FoulBotId, _chatId, isOn);
        _personality.IsSpecialModeOn = isOn;

        // Otherwise the bot keeps imitating its own messages from the previous mode.
        _bot.ResetContext();

        await _bot.SendRawAsync(isOn ? $"😈 {command}: ON" : $"😇 {command}: OFF");

        return true;
    }

    public override ValueTask StopFeatureAsync()
    {
        return default;
    }
}
