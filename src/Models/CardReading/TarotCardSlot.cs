namespace Toolbox.Models.CardReading;

/// <summary>
/// Describes a card slot within a tarot spread layout.
/// </summary>
/// <param name="Id">Unique identifier of the slot.</param>
/// <param name="CenterX">The horizontal center position in layout units.</param>
/// <param name="CenterY">The vertical center position in layout units.</param>
/// <param name="Orientation">The orientation in which the card is placed.</param>
/// <param name="Label">Human readable label shown for accessibility.</param>
/// <param name="ZIndex">Explicit z-index ordering for overlapping scenarios.</param>
public sealed record TarotCardSlot(
    string Id,
    double CenterX,
    double CenterY,
    CardOrientation Orientation = CardOrientation.Vertical,
    string? Label = null,
    int ZIndex = 1);
