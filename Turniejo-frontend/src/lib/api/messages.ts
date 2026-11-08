// Backend zwraca komunikaty po angielsku. Tłumaczymy te, na które użytkownik może realnie trafić
// z UI (reguły, których front nie sprawdza sam albo które zależą od stanu serwera).
// Nieznane komunikaty przechodzą bez zmian — lepszy angielski tekst niż żaden.

const EXACT: Record<string, string> = {
  // Auth / konto
  "User with this email already exists.": "Konto z tym adresem email już istnieje",
  "You already have an organization.": "Masz już organizację",
  "Current password is incorrect.": "Obecne hasło jest nieprawidłowe",
  "New password must be different from the current one.": "Nowe hasło musi się różnić od obecnego",
  // Dołączanie kodem
  "Invalid tournament code.": "Nieprawidłowy kod turnieju",
  "This tournament has been cancelled": "Ten turniej został anulowany. Nie można już do niego dołączyć.",
  // Turniej
  "Cannot change format after schedule has been generated.":
    "Formatu nie można zmienić po wygenerowaniu harmonogramu",
  "Cannot generate schedule before setting tournament format.":
    "Ustaw format turnieju przed wygenerowaniem harmonogramu",
  "Tournament status cannot be changed after cancellation":
    "Statusu anulowanego turnieju nie można zmienić",
  "Tournament status cannot be changed after it has finished":
    "Statusu zakończonego turnieju nie można zmienić",
  "Tournament status is already set to the specified value": "Turniej ma już ten status",
  "Finish the matches in progress before finishing the tournament":
    "Najpierw zakończ trwające mecze - potem zakończysz turniej",
  "You cannot edit tournament settings after tournament is in progres":
    "Ustawień sportowych nie można zmieniać, gdy turniej trwa",
  "Tournament end date cannot be before start date": "Zakończenie nie może być przed rozpoczęciem",
  // Kategorie i grupy
  "Name already exists": "Ta nazwa jest już zajęta",
  "Cannot delete a category that still has teams registered to it":
    "Nie można usunąć kategorii z oczekującymi lub zaakceptowanymi drużynami",
  "Cannot delete a category that still has matches scheduled in it":
    "Nie można usunąć kategorii, w której są zaplanowane mecze",
  "Cannot delete a group that still has teams assigned to it":
    "Nie można usunąć grupy, do której są przypisane drużyny",
  "Cannot delete a group that still has matches scheduled in it":
    "Nie można usunąć grupy, w której są zaplanowane mecze",
  "Group does not belong to the team's category": "Grupa należy do innej kategorii niż drużyna",
  // Drużyny
  "Team is already registered for this tournament and category":
    "Ta drużyna jest już zgłoszona do tej kategorii",
  "Tournament has reached its team limit": "Osiągnięto limit drużyn w turnieju",
  "Minute must be between 0 and 300.": "Minuta musi być od 0 do 300",
  "The team is playing a match right now": "Drużyna gra teraz mecz. Wycofasz ją po jego zakończeniu.",
  "Some matches fall outside the new tournament dates":
    "Część meczów wypada poza nowymi datami turnieju. Najpierw przesuń je w terminarzu.",
  "Team limit cannot be lower than the number of accepted teams":
    "Limit nie może być mniejszy niż liczba zaakceptowanych drużyn. Najpierw wycofaj nadmiarowe.",
  "Team does not belong to the tournament's organization":
    "Drużyna nie należy do organizacji tego turnieju",
  "TournamentTeam payment status is already set to the specified value": "Opłata ma już ten status",
  "TournamentTeam registration status is already set to the specified value":
    "Zgłoszenie ma już ten status",
  "User already has this role for this team": "Ta osoba pełni już tę funkcję w drużynie",
  "User is already a coach for this team": "Ta osoba jest już trenerem tej drużyny",
  // Zawodnicy
  "Player is already assigned to this team": "Ten zawodnik jest już w składzie",
  "A player with the same name and birth date is already in this team":
    "W składzie jest już zawodnik o tych samych danych",
  "Player is not part of the source squad": "Tego zawodnika nie ma w wybranym składzie",
  "Player is not on the roster of this team": "Tego zawodnika nie ma w składzie tej drużyny",
  "An own goal has no assist": "Samobój nie ma asysty",
  "Scorer cannot assist their own goal": "Strzelec nie może asystować przy swoim golu",
  "Shirt numbers must be unique.": "Numery na koszulkach nie mogą się powtarzać",
  "Select at least one player.": "Zaznacz co najmniej jednego zawodnika",
  "Player is too old for this category": "Zawodnik jest za stary na tę kategorię",
  "Player is too young for this category": "Zawodnik jest za młody na tę kategorię",
  "Shirt number is already taken in this team": "Ten numer jest już zajęty w drużynie",
  // Boiska i terminarz
  "Cannot delete a pitch that has matches scheduled on it — deactivate it instead":
    "Na tym boisku są zaplanowane mecze - zamiast usuwać, oznacz je jako nieaktywne",
  "Another match is already scheduled on this pitch at this time":
    "Na tym boisku trwa wtedy inny mecz",
  "Another match is already scheduled on this pitch at this time.":
    "Na tym boisku trwa wtedy inny mecz",
  "One of the teams already plays another match at this time":
    "Jedna z drużyn gra w tym czasie inny mecz",
  "Schedule can only be generated when registration is closed":
    "Terminarz można wygenerować dopiero po zamknięciu zapisów",
  "Only a cancelled tournament can be deleted.": "Usunąć można tylko anulowany turniej",
  "Set the tournament format before generating the schedule":
    "Najpierw ustaw format turnieju (Ustawienia → Format)",
  "Generate the knockout bracket first — the schedule places its first-round matches":
    "Najpierw utwórz drabinkę - terminarz rozstawia mecze jej 1. rundy",
  "The tournament stage could not be reverted — try again":
    "Nie udało się cofnąć etapu turnieju - spróbuj ponownie",
  "No valid active pitches belonging to this tournament were provided":
    "Wybierz co najmniej jedno aktywne boisko",
  "Tournament has no groups defined": "Turniej nie ma jeszcze grup",
  "No matches to schedule — each group needs at least 2 accepted teams":
    "Brak meczów do rozegrania - każda grupa potrzebuje co najmniej 2 zaakceptowanych drużyn",
  "Tournament cannot transition to ScheduleGenerated from its current status":
    "Terminarz został już wygenerowany albo turniej jest na innym etapie",
  "Some accepted teams are not assigned to a group":
    "Część zaakceptowanych drużyn nie ma grupy - przypisz je, zanim wygenerujesz terminarz",
  "EndAt must be after StartAt.": "Koniec musi być po rozpoczęciu",
  "At least one pitch is required.": "Wybierz co najmniej jedno boisko",
  "Description must not exceed 500 characters.": "Maksymalnie 500 znaków",
  // Mecze
  "Cannot change the schedule once the match has started, finished or been cancelled":
    "Mecz się rozpoczął, zakończył albo został odwołany - terminu nie można już zmienić",
  "Cannot change the referee once the match has finished or been cancelled":
    "Mecz się zakończył albo został odwołany - sędziego nie można już zmienić",
  "Match already has a referee": "Ten mecz wziął już inny sędzia",
  "You are not the referee of this match": "Nie prowadzisz już tego meczu",
  "User is not a referee in this tournament": "Ta osoba nie jest sędzią w tym turnieju",
  "The referee already has another match at this time":
    "Ten sędzia prowadzi w tym czasie inny mecz",
  "Player has been sent off": "Ten zawodnik ma czerwoną kartkę - nie gra już w tym meczu",
  // Faza pucharowa
  "Penalties can only decide a drawn match": "Karne rozstrzygają tylko remis",
  "Penalty shootout score is required": "Wpisz wynik rzutów karnych",
  "Penalty shootout cannot end in a draw": "Karne muszą wyłonić zwycięzcę",
  "Winner must be the team that won the penalty shootout":
    "Zwycięzcą jest drużyna, która wygrała karne",
  "A knockout match cannot end in a draw — decide it by penalties":
    "Mecz pucharowy nie może zakończyć się remisem - rozstrzygnij go karnymi",
  "Winner must be the team with more goals": "Zwycięzcą jest drużyna z większą liczbą goli",
  "A bracket already exists for this tournament": "Drabinka dla tej kategorii już istnieje",
  "Cannot reset the bracket after one of its matches has started":
    "Nie można ułożyć drabinki od nowa - jeden z jej meczów już się rozpoczął",
  "Cannot change the pairings once a bracket match has started, finished or been cancelled":
    "Nie można już zmienić par - jeden z meczów drabinki się rozpoczął, zakończył albo został odwołany",
  "Only first-round slots can be swapped": "Zamieniać można tylko sloty pierwszej rundy",
  "Choose two different slots to swap": "Wybierz dwa różne sloty",
  "The bracket was removed, but the tournament stage could not be reverted — clear the schedule":
    "Drabinka została usunięta, ale etap turnieju się nie cofnął - wyczyść terminarz",
  "Bracket generation requires Knockout or GroupAndKnockout format":
    "Drabinka wymaga formatu z fazą pucharową",
  "Match events can only be recorded while the match is live, paused, or within the post-match edit window":
    "Czas na poprawki minął - wynik meczu jest zablokowany",
  "The post-match edit window has closed":
    "Czas na poprawki minął. Rozstrzygnięcie może teraz zmienić tylko organizator",
  "Match must be finished to change its result": "Ten mecz nie jest jeszcze zakończony",
  "Walkover requires a winner": "Wybierz drużynę, która wygrywa walkowerem",
  "Logo must be a PNG, JPEG or WebP image": "Herb musi być obrazkiem PNG, JPG albo WebP",
  "Logo file is too large.": "Plik z herbem jest za duży",
  "Logo file is empty.": "Wybierz plik z herbem",
  "Pitch is inactive": "To boisko jest nieaktywne",
  "Pitch does not belong to this tournament": "To boisko nie należy do tego turnieju",
  "Match must take place within the tournament dates": "Mecz musi się odbyć w trakcie turnieju",
  "Match must start after the matches its teams come from":
    "Ten mecz musi się zacząć po meczach, z których przyjdą drużyny",
  "This match already exists, change it in the schedule":
    "Ten mecz już powstał. Zmień go w terminarzu",
  "A pair with a bye has no match": "Para z wolnym losem nie ma meczu",
  "The tournament has finished or been cancelled": "Turniej się zakończył albo został anulowany",
  "Schedule must fit within the tournament dates": "Terminarz musi mieścić się w datach turnieju",
  "Tournament already has group matches — clear the schedule first":
    "Turniej ma już mecze grupowe - najpierw wyczyść terminarz",
  "Schedule can only be cleared before the tournament starts":
    "Terminarz można wyczyścić tylko przed rozpoczęciem turnieju",
  "Cannot clear the schedule after a match has started":
    "Nie można wyczyścić terminarza - któryś mecz już się rozpoczął",
  "Matches were removed, but the tournament stage could not be reverted — try again":
    "Mecze usunięte, ale nie udało się cofnąć etapu turnieju - spróbuj ponownie",
  "Schedule status is set automatically by generating the schedule":
    "Ten etap ustawia się automatycznie po wygenerowaniu terminarza",
}

const PATTERNS: [RegExp, string][] = [
  [/^Tournament category with name '.+' already exists$/, "Kategoria o tej nazwie już istnieje"],
  [/^Pitch with name '.+' already exists in this tournament$/, "Boisko o tej nazwie już istnieje"],
  [
    /^Not enough time available to schedule all matches — (\d+) match\(es\) could not be placed$/,
    "Za mało czasu - nie wszystkie mecze mieszczą się w wybranych godzinach. Dodaj boisko albo wydłuż dzień.",
  ],
  [
    /^Cannot change registration status from \w+ to \w+$/,
    "Tej zmiany statusu zgłoszenia nie można wykonać",
  ],
  [
    /^Tournament status cannot be changed from \w+ to \w+$/,
    "Tej zmiany statusu turnieju nie można wykonać",
  ],
]

export function translateApiMessage(message: string): string
export function translateApiMessage(message: string | undefined): string | undefined
export function translateApiMessage(message: string | undefined) {
  if (!message) return message
  return EXACT[message] ?? PATTERNS.find(([pattern]) => pattern.test(message))?.[1] ?? message
}
