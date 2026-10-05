using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.Application.Services
{
    // Limit rocznika kategorii — ta sama reguła co w AssignPlayerToTournamentTeam / CreatePlayerInTournamentTeam.
    internal static class CategoryAgeRule
    {
        public const string TooOld = "TooOld";
        public const string TooYoung = "TooYoung";

        /// <summary>null = zawodnik mieści się w kategorii (albo kategoria nie ma limitu).</summary>
        public static string? Check(TournamentCategoryDto? category, DateTime birthDate)
        {
            if (category == null)
                return null;

            if (category.MinBirthDate.HasValue && birthDate < category.MinBirthDate.Value)
                return TooOld;

            if (category.MaxBirthDate.HasValue && birthDate > category.MaxBirthDate.Value)
                return TooYoung;

            return null;
        }

        public static string Message(string violation) => violation == TooOld
            ? "Player is too old for this category"
            : "Player is too young for this category";
    }
}
