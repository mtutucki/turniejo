import type { Metadata } from "next"
import Link from "next/link"

import { Fill, LegalDocument, LegalSection } from "@/components/legal/legal-document"
import { LEGAL } from "@/lib/legal"

export const metadata: Metadata = {
  title: "Polityka prywatności",
  description: "Jakie dane przetwarza Turniejo, w jakim celu, jak długo i jakie masz prawa.",
}

const linkClass = "text-primary font-medium hover:underline"

export default function PrivacyPage() {
  return (
    <LegalDocument title="Polityka prywatności">
      <LegalSection title="1. Administrator danych">
        <p>
          Administratorem danych osobowych użytkowników serwisu Turniejo jest{" "}
          <strong>
            <Fill value={LEGAL.controllerName} />
          </strong>
          , <Fill value={LEGAL.controllerAddress} />, <Fill value={LEGAL.controllerRegistry} />.
        </p>
        <p>
          W sprawach danych osobowych napisz na <Fill value={LEGAL.contactEmail} />.
        </p>
        <p>
          Dane zawodników (w tym dzieci) wprowadza organizator turnieju i to on jest ich
          administratorem — szczegóły w punkcie 5.
        </p>
      </LegalSection>

      <LegalSection title="2. Jakie dane przetwarzamy">
        <ul>
          <li>
            <strong>Dane konta:</strong> imię, nazwisko, adres e-mail, numer telefonu (opcjonalnie),
            hasło (przechowywane wyłącznie w postaci skrótu kryptograficznego), data i wersja
            zaakceptowanego regulaminu.
          </li>
          <li>
            <strong>Dane o udziale w turniejach:</strong> role (organizator, trener, sędzia, rodzic,
            kibic) i to, do jakiego turnieju, drużyny lub zawodnika są przypisane, zdarzenia meczowe
            wprowadzone przez sędziego, powiadomienia.
          </li>
          <li>
            <strong>Dane zawodników:</strong> imię, nazwisko, data urodzenia, numer, pozycja,
            zdjęcie (jeśli zostało dodane) i statystyki meczowe — wprowadzane przez organizatora
            albo trenera.
          </li>
          <li>
            <strong>Dane techniczne:</strong> adres IP, czas i adres żądania w logach serwera.
          </li>
          <li>
            <strong>Statystyki odwiedzin:</strong> zagregowane, anonimowe dane o odwiedzanych
            stronach — patrz punkt 8.
          </li>
        </ul>
      </LegalSection>

      <LegalSection title="3. Cele i podstawy prawne">
        <ul>
          <li>
            Założenie i prowadzenie konta, obsługa turniejów, wyników i powiadomień — wykonanie
            umowy o świadczenie usług drogą elektroniczną (art. 6 ust. 1 lit. b RODO).
          </li>
          <li>
            Bezpieczeństwo serwisu, zapobieganie nadużyciom, dochodzenie i obrona roszczeń oraz
            anonimowe statystyki odwiedzin — prawnie uzasadniony interes administratora (art. 6 ust.
            1 lit. f RODO).
          </li>
          <li>
            Wykazanie zgodności z przepisami, w tym zapis akceptacji regulaminu — obowiązek prawny
            (art. 6 ust. 1 lit. c w zw. z art. 5 ust. 2 RODO).
          </li>
        </ul>
        <p>
          Podanie danych konta jest dobrowolne, ale bez nich nie da się korzystać z serwisu. Nie
          podejmujemy decyzji opartych wyłącznie na zautomatyzowanym przetwarzaniu i nie profilujemy
          użytkowników.
        </p>
      </LegalSection>

      <LegalSection title="4. Komu przekazujemy dane">
        <ul>
          <li>
            Dostawcom usług, którzy przetwarzają dane w naszym imieniu: hosting —{" "}
            <Fill value={LEGAL.hostingProvider} />, wysyłka e-maili —{" "}
            <Fill value={LEGAL.emailProvider} />.
          </li>
          <li>
            Innym uczestnikom turnieju w zakresie potrzebnym do jego prowadzenia — np. organizator
            widzi listę osób, które dołączyły do jego turnieju, a trener skład swojej drużyny.
          </li>
          <li>
            Kibicom na portalu publicznym — tylko to, co organizator zdecydował się udostępnić
            (punkt 5).
          </li>
        </ul>
        <p>Nie sprzedajemy danych i nie przekazujemy ich poza Europejski Obszar Gospodarczy.</p>
      </LegalSection>

      <LegalSection id="zawodnicy" title="5. Dane zawodników i portal publiczny">
        <p>
          Organizator turnieju decyduje, jakie dane zawodników wprowadza i czy publikuje je na
          portalu kibica — jest więc administratorem tych danych. Turniejo przetwarza je w jego
          imieniu na podstawie umowy powierzenia zawartej w{" "}
          <Link href="/terms#powierzenie" className={linkClass}>
            §6 Regulaminu
          </Link>
          .
        </p>
        <p>
          Organizator odpowiada za posiadanie podstawy prawnej do przetwarzania danych dzieci, w
          szczególności zgody rodziców lub opiekunów na publikację wizerunku i wyników. Domyślnie
          portal pokazuje zawodników w formie „Jan K.” — pełne nazwiska organizator musi świadomie
          włączyć.
        </p>
        <p>
          Rodzic, który chce poprawić lub usunąć dane dziecka, zwraca się do organizatora turnieju.
          Jeśli napiszesz do nas, przekażemy prośbę organizatorowi i pomożemy ją zrealizować.
        </p>
      </LegalSection>

      <LegalSection title="6. Jak długo przechowujemy dane">
        <ul>
          <li>Dane konta — do czasu jego usunięcia.</li>
          <li>
            Po usunięciu konta dane osobowe są natychmiast anonimizowane. W kopiach zapasowych
            znikają najpóźniej po <Fill value={LEGAL.backupRetention} />.
          </li>
          <li>
            Logi serwera — <Fill value={LEGAL.logRetention} />.
          </li>
          <li>
            Wyniki i zdarzenia meczowe zostają w historii turnieju bez danych usuniętego konta (np.
            jako „Usunięty użytkownik”).
          </li>
        </ul>
      </LegalSection>

      <LegalSection title="7. Twoje prawa">
        <p>Masz prawo do:</p>
        <ul>
          <li>dostępu do swoich danych i otrzymania ich kopii,</li>
          <li>sprostowania danych,</li>
          <li>usunięcia danych („prawo do bycia zapomnianym”),</li>
          <li>ograniczenia przetwarzania,</li>
          <li>przenoszenia danych,</li>
          <li>sprzeciwu wobec przetwarzania opartego na uzasadnionym interesie.</li>
        </ul>
        <p>
          Kopię danych w pliku JSON pobierzesz i konto usuniesz samodzielnie w zakładce „Moje konto”
          po zalogowaniu. W pozostałych sprawach napisz na <Fill value={LEGAL.contactEmail} /> —
          odpowiemy w ciągu miesiąca.
        </p>
        <p>
          Jeśli jesteś jedynym organizatorem organizacji, jej usunięcie (wraz z turniejami,
          drużynami i danymi zawodników) wymaga kontaktu mailowego — żeby nie usunąć przez pomyłkę
          danych innych osób.
        </p>
        <p>
          Przysługuje Ci też skarga do Prezesa Urzędu Ochrony Danych Osobowych (ul. Stawki 2, 00-193
          Warszawa).
        </p>
      </LegalSection>

      <LegalSection id="cookies" title="8. Pliki cookies i pamięć przeglądarki">
        <p>Turniejo zapisuje w przeglądarce tylko to, co jest niezbędne do działania:</p>
        <ul>
          <li>
            <strong>Ciasteczka sesji</strong> (<code>authjs.session-token</code>,{" "}
            <code>authjs.csrf-token</code>, <code>authjs.callback-url</code>) — utrzymują
            zalogowanie i chronią formularze. Znikają po wylogowaniu lub wygaśnięciu sesji.
          </li>
          <li>
            <strong>Pamięć lokalna</strong> (<code>turniejo-active-tournament</code>) — ostatnio
            wybrany turniej, żeby nawigacja od razu go pokazywała.
          </li>
        </ul>
        <p>
          Statystyki odwiedzin zbieramy narzędziem Umami, które nie używa ciasteczek ani pamięci
          przeglądarki, nie śledzi użytkowników między stronami i nie pozwala ich zidentyfikować.
          Adresy stron są zapisywane bez parametrów (np. bez kodów zaproszeń). Dlatego serwis nie
          wyświetla banera cookies — nie ma na co wyrażać zgody.
        </p>
      </LegalSection>

      <LegalSection title="9. Bezpieczeństwo">
        <p>
          Połączenie z serwisem jest szyfrowane (HTTPS), hasła przechowujemy wyłącznie jako skróty
          kryptograficzne, a dostęp do danych turnieju mają tylko osoby z odpowiednią rolą.
        </p>
      </LegalSection>

      <LegalSection title="10. Zmiany polityki">
        <p>
          O istotnych zmianach poinformujemy e-mailem lub komunikatem w serwisie. Aktualna wersja i
          jej data są zawsze na tej stronie.
        </p>
      </LegalSection>
    </LegalDocument>
  )
}
