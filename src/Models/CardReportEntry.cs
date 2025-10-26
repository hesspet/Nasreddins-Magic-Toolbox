namespace Toolbox.Models;

/// <summary>
///     Represents the report data for a single card within a deck.
/// </summary>
/// <param name="CardName">          Name of the card. </param>
/// <param name="ImageLength">       Size of the card image in bytes. </param>
/// <param name="DescriptionLength"> Length of the card description in characters. </param>
public sealed record CardReportEntry(string CardName, int ImageLength, int DescriptionLength);
