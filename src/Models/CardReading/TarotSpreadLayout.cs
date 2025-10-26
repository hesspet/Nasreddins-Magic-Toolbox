namespace Toolbox.Models.CardReading;

/// <summary>
///     Describes the geometry of a tarot spread.
/// </summary>
public sealed record TarotSpreadLayout
{
    public TarotSpreadLayout(string id, IReadOnlyList<TarotCardSlot> slots)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Layout id must not be null or whitespace.", nameof(id));
        }

        Id = id;
        Slots = slots ?? throw new ArgumentNullException(nameof(slots));
    }

    /// <summary>
    ///     Unique identifier for the spread layout.
    /// </summary>
    public string Id
    {
        get;
    }

    /// <summary>
    ///     Collection of card slots included in this spread.
    /// </summary>
    public IReadOnlyList<TarotCardSlot> Slots
    {
        get;
    }

    /// <summary>
    ///     Aspect ratio (height / width) of a single card.
    /// </summary>
    public double CardAspectRatio { get; init; } = 1.62;

    /// <summary>
    ///     Number of logical units representing the width of a card.
    /// </summary>
    public double CardWidthUnits { get; init; } = 1.0;

    /// <summary>
    ///     Horizontal padding around the spread measured in card width units.
    /// </summary>
    public double HorizontalPadding { get; init; } = 0.7;

    /// <summary>
    ///     Vertical padding around the spread measured in card width units.
    /// </summary>
    public double VerticalPadding { get; init; } = 0.7;

    /// <summary>
    ///     Suggested default card width in pixels for initial rendering.
    /// </summary>
    public double DefaultCardWidth { get; init; } = 160.0;

    /// <summary>
    ///     Minimum width in pixels to which cards may shrink.
    /// </summary>
    public double MinCardWidth { get; init; } = 96.0;

    /// <summary>
    ///     Maximum width in pixels that cards may occupy.
    /// </summary>
    public double MaxCardWidth { get; init; } = 360.0;
}
