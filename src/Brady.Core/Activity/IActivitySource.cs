namespace Brady.Core.Activity;

/// <summary>When the user last touched the keyboard or mouse (GetLastInputInfo on Windows).</summary>
public interface IActivitySource
{
    DateTimeOffset LastInputAt { get; }
}
