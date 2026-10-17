using Brady.Core.Copy;
using Brady.Core.Reminders;

namespace Brady.Core.Tests.Copy;

public sealed class CopyDeckTests
{
    public static TheoryData<ReminderKind> Kinds => [.. Enum.GetValues<ReminderKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Draw_never_repeats_a_line_back_to_back(ReminderKind kind)
    {
        var deck = new CopyDeck();
        var previous = deck.Draw(kind);
        for (var i = 0; i < 10; i++)
        {
            var next = deck.Draw(kind);
            Assert.NotEqual(previous, next);
            previous = next;
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Draw_cycles_through_every_line(ReminderKind kind)
    {
        var deck = new CopyDeck();
        var lines = CopyDeck.LinesFor(kind);
        var drawn = Enumerable.Range(0, lines.Count).Select(_ => deck.Draw(kind)).ToHashSet();
        Assert.Equal(lines.Count, drawn.Count);
    }

    [Fact]
    public void Kinds_rotate_independently()
    {
        var deck = new CopyDeck();
        deck.Draw(ReminderKind.Eyes);
        deck.Draw(ReminderKind.Eyes);
        Assert.Equal(CopyDeck.LinesFor(ReminderKind.Move)[0], deck.Draw(ReminderKind.Move));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_line_follows_the_voice_rules(ReminderKind kind)
    {
        foreach (var line in CopyDeck.LinesFor(kind))
        {
            Assert.True(line.Title.Split(' ').Length <= 5, $"Title over five words: {line.Title}");
            Assert.True(line.Body.Length <= 90, $"Body over 90 characters: {line.Body}");
            Assert.DoesNotContain('!', line.Title + line.Body);
        }
    }

    [Theory]
    [InlineData(30, "Nice. See you in 30.")]
    [InlineData(45, "Nice. See you in 45.")]
    [InlineData(60, "Nice. See you in an hour.")]
    [InlineData(120, "Nice. See you in 2 hours.")]
    public void After_done_names_the_next_interval(int minutes, string expected)
    {
        var copy = CopyDeck.AfterDone(ReminderKind.Posture, TimeSpan.FromMinutes(minutes));
        Assert.Equal(expected, copy.Title);
    }
}
