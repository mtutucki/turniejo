namespace Turniejo.Matches.Application.Matches.AddCard
{
    // CardType: faktycznie zapisana kartka — druga żółta wraca jako "RedCard".
    public sealed record AddCardResponse(Guid MatchEventId, string CardType = "");
}
