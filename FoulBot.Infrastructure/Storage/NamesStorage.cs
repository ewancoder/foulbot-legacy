using System.Collections.Concurrent;
using StackExchange.Redis;

namespace FoulBot.Infrastructure.Storage;

public sealed class RedisNamesStorage : INamesStorage
{
    private readonly IConnectionMultiplexer _redis;
    private ConcurrentDictionary<string, string> _names = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized = false;

    public RedisNamesStorage(
        IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async ValueTask<string?> GetNameAsync(string nickname)
    {
        await InitializeAsync();

        if (_names.TryGetValue(nickname, out var name))
            return name;

        return null;
    }

    public async ValueTask SetNameAsync(string nickname, string name)
    {
        await InitializeAsync();

        _names.AddOrUpdate(nickname, name, (_, _) => name);

        var db = _redis.GetDatabase();
        var namesString = JsonSerializer.Serialize(_names);
        await db.StringSetAsync("hard_names", namesString);
    }

    private async ValueTask InitializeAsync()
    {
        if (_initialized)
            return;

        await _lock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            var db = _redis.GetDatabase();
            var namesString = (await db.StringGetAsync("hard_names")).ToString();
            _names = new ConcurrentDictionary<string, string>(ParseNames(namesString));

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseNames(string? namesString)
    {
        if (string.IsNullOrWhiteSpace(namesString))
            return [];

        if (namesString.StartsWith('{'))
            return JsonSerializer.Deserialize<Dictionary<string, string>>(namesString) ?? [];

        // Legacy "nickname=name,nickname=name" format. Skip malformed entries instead of failing.
        return namesString
            .Split(',')
            .Select(keyValue => keyValue.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .DistinctBy(parts => parts[0])
            .Select(parts => new KeyValuePair<string, string>(parts[0], parts[1]));
    }
}
