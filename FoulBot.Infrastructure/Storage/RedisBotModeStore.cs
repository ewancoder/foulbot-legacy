using FoulBot.Domain.Storage;
using StackExchange.Redis;

namespace FoulBot.Infrastructure.Storage;

public sealed class RedisBotModeStore : IBotModeStore
{
    private readonly ILogger<RedisBotModeStore> _logger;
    private readonly IConnectionMultiplexer _redis;

    public RedisBotModeStore(
        ILogger<RedisBotModeStore> logger,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    public async ValueTask<bool> IsSpecialModeOnAsync(FoulBotId botId, FoulChatId chatId)
    {
        try
        {
            var db = _redis.GetDatabase();

            return await db.KeyExistsAsync(GetKey(botId, chatId));
        }
        catch (Exception exception)
        {
            // Bot should still work in the default mode if Redis is unavailable.
            _logger.LogError(exception, "Could not get bot special mode, using the default mode.");
            return false;
        }
    }

    public async ValueTask SetSpecialModeAsync(FoulBotId botId, FoulChatId chatId, bool isOn)
    {
        var db = _redis.GetDatabase();

        if (isOn)
            await db.StringSetAsync(GetKey(botId, chatId), "on");
        else
            await db.KeyDeleteAsync(GetKey(botId, chatId));
    }

    private static string GetKey(FoulBotId botId, FoulChatId chatId)
        => $"special_mode_{botId.BotId}_{chatId.Value}";
}
