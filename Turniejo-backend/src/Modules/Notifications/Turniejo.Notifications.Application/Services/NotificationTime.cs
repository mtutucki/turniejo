using System.Globalization;

namespace Turniejo.Notifications.Application.Services
{
    // Terminy meczów są zapisane w UTC, a treść powiadomienia czyta człowiek w Polsce. Bez przeliczenia
    // powiadomienie podawało godzinę o 1-2 h wcześniejszą niż w terminarzu.
    public static class NotificationTime
    {
        private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
        private static readonly TimeZoneInfo PolandZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

        private static DateTimeOffset Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, PolandZone);

        /// <summary>Np. „14 listopada 2026, 09:00”.</summary>
        public static string DateAndTime(DateTimeOffset value) => Local(value).ToString("d MMMM yyyy, HH:mm", Polish);

        /// <summary>Np. „09:00”.</summary>
        public static string Time(DateTimeOffset value) => Local(value).ToString("HH:mm", Polish);
    }
}
