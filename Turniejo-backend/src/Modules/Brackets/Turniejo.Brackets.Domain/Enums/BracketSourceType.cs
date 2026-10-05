namespace Turniejo.Brackets.Domain.Enums
{
    public enum BracketSourceType
    {
        Fixed = 1,
        GroupPosition = 2,
        MatchWinner = 3,
        MatchLoser = 4,

        // Brak przeciwnika w tym slocie Rundy 1 — drużyna z drugiej strony pary awansuje bez meczu.
        // Węzeł z Bye z jednej strony nigdy nie ma obu slotów wypełnionych (IsFullyResolved==false),
        // więc nigdy nie dostaje meczu — awans jest wpychany ręcznie przy generowaniu drabinki.
        Bye = 5
    }
}
