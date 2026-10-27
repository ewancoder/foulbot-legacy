using FoulBot.Domain.Features;
using FoulBot.Domain.Storage;

namespace FoulBot.Domain.Tests;

public class SpecialModeFeatureTests
{
    private const string Command = "$pidormode";
    private readonly FoulBotConfiguration _config = new FoulBotConfiguration(
        "botId", "botName", "default directive", [], [])
        .WithSpecialMode(Command, "special directive", ["special hint"], ["special mood"]);
    private readonly FoulChatId _chatId = new("chatId");
    private readonly Mock<IFoulBot> _bot = new();
    private readonly InMemoryBotModeStore _store = new();

    [Fact]
    public void Personality_ShouldUseDefaultMode_ByDefault()
    {
        var personality = new BotPersonality(_config);

        Assert.Equal("default directive", personality.Directive);
    }

    [Fact]
    public void Personality_ShouldUseSpecialMode_WhenOn()
    {
        var personality = new BotPersonality(_config, isSpecialModeOn: true);

        Assert.Equal("special directive", personality.Directive);
        Assert.Equal(["special hint"], personality.ReplyStyleHints);
        Assert.Equal(["special mood"], personality.Moods);
    }

    [Fact]
    public void Personality_ShouldNotTurnOnSpecialMode_WhenNotConfigured()
    {
        var personality = new BotPersonality(
            new FoulBotConfiguration("botId", "botName", "default directive", [], []),
            isSpecialModeOn: true);

        Assert.False(personality.IsSpecialModeOn);
        Assert.Equal("default directive", personality.Directive);
    }

    [Fact]
    public async Task ShouldToggleSpecialMode_AndPersistIt()
    {
        var personality = new BotPersonality(_config);
        var sut = new SpecialModeFeature(personality, _store, _bot.Object, _config, _chatId);

        Assert.True(await sut.ProcessMessageAsync(CreateMessage(Command)));
        Assert.True(personality.IsSpecialModeOn);
        Assert.True(await _store.IsSpecialModeOnAsync(_config.FoulBotId, _chatId));
        _bot.Verify(x => x.ResetContext());

        Assert.True(await sut.ProcessMessageAsync(CreateMessage($" {Command} ")));
        Assert.False(personality.IsSpecialModeOn);
        Assert.False(await _store.IsSpecialModeOnAsync(_config.FoulBotId, _chatId));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("$pidormode please")]
    public async Task ShouldNotProcessOtherMessages(string text)
    {
        var personality = new BotPersonality(_config);
        var sut = new SpecialModeFeature(personality, _store, _bot.Object, _config, _chatId);

        Assert.False(await sut.ProcessMessageAsync(CreateMessage(text)));
        Assert.False(personality.IsSpecialModeOn);
    }

    [Fact]
    public async Task ShouldNotProcessCommand_FromBots()
    {
        var personality = new BotPersonality(_config);
        var sut = new SpecialModeFeature(personality, _store, _bot.Object, _config, _chatId);

        Assert.False(await sut.ProcessMessageAsync(CreateMessage(Command, isOriginallyBotMessage: true)));
        Assert.False(personality.IsSpecialModeOn);
    }

    private static FoulMessage CreateMessage(string text, bool isOriginallyBotMessage = false)
        => FoulMessage.CreateText(
            Guid.NewGuid().ToString(), FoulMessageSenderType.User, new("user", "user"),
            text, DateTime.UtcNow, isOriginallyBotMessage, null);
}
