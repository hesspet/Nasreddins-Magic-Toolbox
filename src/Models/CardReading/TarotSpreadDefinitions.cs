namespace Toolbox.Models.CardReading;

/// <summary>
///     Provides predefined tarot spread layouts used by the application.
/// </summary>
public static class TarotSpreadDefinitions
{
    public static readonly TarotSpreadLayout CelticCross = new(
        id: "celtic-cross",
        slots: new[]
        {
            new TarotCardSlot("center", 0, 0.5, Label: "Karte 1"),
            new TarotCardSlot("cross", 0.1, -0.7, CardOrientation.Horizontal, Label: "Karte 2", ZIndex: 2),
            new TarotCardSlot("above", 0, -2.8, Label: "Karte 3"),
            new TarotCardSlot("below", 0, 3.2, Label: "Karte 4"),
            new TarotCardSlot("left", -2.4, 0.5, Label: "Karte 5"),
            new TarotCardSlot("right", 2.4, 0.5, Label: "Karte 6")
        })
    {
        HorizontalPadding = 1.2,
        VerticalPadding = 1.6,
        DefaultCardWidth = 150,
        MinCardWidth = 100,
        MaxCardWidth = 280
    };

    public static readonly TarotSpreadLayout FourCorners = new(
        id: "four-corners",
        slots: new[]
        {
            new TarotCardSlot("top-left", -1.6, -1.8, Label: "Karte 1"),
            new TarotCardSlot("top-right", 1.6, -1.8, Label: "Karte 2"),
            new TarotCardSlot("bottom-right", 1.6, 1.8, Label: "Karte 3"),
            new TarotCardSlot("bottom-left", -1.6, 1.8, Label: "Karte 4")
        })
    {
        HorizontalPadding = 1.1,
        VerticalPadding = 1.1,
        DefaultCardWidth = 170,
        MinCardWidth = 110,
        MaxCardWidth = 320
    };

    public static readonly TarotSpreadLayout Path = new(
        id: "path",
        slots: new[]
        {
            new TarotCardSlot("one", -3.2, 0, Label: "Karte 1"),
            new TarotCardSlot("two", -1.6, 0, Label: "Karte 2"),
            new TarotCardSlot("three", 0, 0, Label: "Karte 3"),
            new TarotCardSlot("four", 1.6, 0, Label: "Karte 4"),
            new TarotCardSlot("five", 3.2, 0, Label: "Karte 5")
        })
    {
        HorizontalPadding = 1.2,
        VerticalPadding = 0.8,
        DefaultCardWidth = 150,
        MinCardWidth = 100,
        MaxCardWidth = 260
    };

    public static readonly TarotSpreadLayout SingleCard = new(
                    id: "single-card",
        slots: new[]
        {
            new TarotCardSlot("single", 0, 0, Label: "Karte 1")
        })
    {
        HorizontalPadding = 0.8,
        VerticalPadding = 0.8,
        DefaultCardWidth = 200,
        MinCardWidth = 120,
        MaxCardWidth = 360
    };

    public static readonly TarotSpreadLayout ThreeCards = new(
        id: "three-cards",
        slots: new[]
        {
            new TarotCardSlot("left", -1.6, 0, Label: "Karte 1"),
            new TarotCardSlot("center", 0, 0, Label: "Karte 2"),
            new TarotCardSlot("right", 1.6, 0, Label: "Karte 3")
        })
    {
        HorizontalPadding = 1.0,
        VerticalPadding = 0.8,
        DefaultCardWidth = 170,
        MinCardWidth = 110,
        MaxCardWidth = 300
    };

    /// <summary>
    ///     Enumerates all known spreads.
    /// </summary>
    public static IReadOnlyList<TarotSpreadLayout> All
    {
        get;
    } = new[]
    {
        SingleCard,
        ThreeCards,
        Path,
        FourCorners,
        CelticCross
    };
}
