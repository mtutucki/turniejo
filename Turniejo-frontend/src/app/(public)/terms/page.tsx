import type { Metadata } from "next"
import Link from "next/link"

import { Fill, LegalDocument, LegalSection } from "@/components/legal/legal-document"
import { LEGAL } from "@/lib/legal"

export const metadata: Metadata = {
  title: "Regulamin",
  description: "Zasady korzystania z Turniejo i umowa powierzenia danych dla organizatorów.",
}

const linkClass = "text-primary font-medium hover:underline"

export default function TermsPage() {
  return (
    <LegalDocument title="Regulamin serwisu Turniejo">
      <LegalSection title="§1. Postanowienia ogólne">
        <ol>
          <li>
            Regulamin określa zasady korzystania z serwisu Turniejo (dalej „Serwis”) — aplikacji do
            organizowania turniejów piłkarskich, dostępnej przez przeglądarkę.
          </li>
          <li>
            Usługodawcą jest{" "}
            <strong>
              <Fill value={LEGAL.controllerName} />
            </strong>
            , <Fill value={LEGAL.controllerAddress} />, <Fill value={LEGAL.controllerRegistry} />,
            e-mail: <Fill value={LEGAL.contactEmail} /> (dalej „Usługodawca”).
          </li>
          <li>
            Pojęcia: <strong>Użytkownik</strong> — osoba z kontem w Serwisie;{" "}
            <strong>Organizator</strong> — Użytkownik prowadzący organizację i jej turnieje;{" "}
            <strong>Uczestnik</strong> — Użytkownik, który dołączył do turnieju kodem (trener,
            sędzia, rodzic, kibic); <strong>Zawodnik</strong> — osoba grająca w turnieju, której
            dane wprowadza Organizator lub trener; <strong>Portal</strong> — publiczna strona
            turnieju dostępna bez logowania.
          </li>
          <li>
            Zasady przetwarzania danych osobowych opisuje{" "}
            <Link href="/privacy" className={linkClass}>
              Polityka prywatności
            </Link>
            .
          </li>
        </ol>
      </LegalSection>

      <LegalSection title="§2. Wymagania techniczne">
        <ol>
          <li>
            Do korzystania z Serwisu potrzebne są: urządzenie z dostępem do internetu, aktualna
            przeglądarka (Chrome, Safari, Firefox lub Edge) z włączoną obsługą JavaScript i
            ciasteczek oraz adres e-mail.
          </li>
          <li>Serwis nie wymaga instalowania oprogramowania.</li>
        </ol>
      </LegalSection>

      <LegalSection title="§3. Konto">
        <ol>
          <li>
            Konto może założyć osoba, która ukończyła 16 lat. Założenie konta wymaga akceptacji
            Regulaminu i zapoznania się z Polityką prywatności.
          </li>
          <li>
            Z chwilą założenia konta zawierana jest umowa o świadczenie usług drogą elektroniczną na
            czas nieokreślony. <Fill value={LEGAL.pricing} />
          </li>
          <li>
            Użytkownik podaje prawdziwe dane, chroni hasło przed osobami trzecimi i nie udostępnia
            konta innym osobom.
          </li>
          <li>
            Użytkownik może w każdej chwili usunąć konto w zakładce „Moje konto”, co rozwiązuje
            umowę. Jedyny organizator organizacji usuwa konto przez kontakt z Usługodawcą (§10).
          </li>
        </ol>
      </LegalSection>

      <LegalSection title="§4. Zasady korzystania">
        <ol>
          <li>
            Zabronione jest wprowadzanie treści bezprawnych, obraźliwych lub naruszających prawa
            innych osób, w tym danych osobowych bez podstawy prawnej.
          </li>
          <li>
            Kody zaproszeń do turnieju są poufne — Użytkownik przekazuje je tylko osobom, które mają
            uczestniczyć w turnieju.
          </li>
          <li>
            Zabronione są działania zakłócające pracę Serwisu, w tym próby nieuprawnionego dostępu
            do danych innych Użytkowników.
          </li>
        </ol>
      </LegalSection>

      <LegalSection title="§5. Obowiązki Organizatora">
        <ol>
          <li>
            Organizator odpowiada za przebieg turnieju oraz za dane Zawodników i Uczestników, które
            wprowadza lub którym nadaje dostęp.
          </li>
          <li>
            Organizator oświadcza, że ma podstawę prawną do przetwarzania danych Zawodników, a w
            przypadku dzieci — zgodę rodziców lub opiekunów, w tym na publikację imion, nazwisk,
            zdjęć i wyników na Portalu, jeśli go włącza.
          </li>
          <li>
            Organizator decyduje, czy Portal jest publiczny i jakie dane pokazuje. Pełne nazwiska
            Zawodników są na Portalu domyślnie ukryte.
          </li>
          <li>
            Organizator realizuje prawa Zawodników i ich opiekunów (np. poprawienie lub usunięcie
            danych), a Usługodawca mu w tym pomaga.
          </li>
        </ol>
      </LegalSection>

      <LegalSection id="powierzenie" title="§6. Powierzenie przetwarzania danych osobowych">
        <ol>
          <li>
            W zakresie danych Zawodników i Uczestników turnieju Organizator jest administratorem, a
            Usługodawca podmiotem przetwarzającym. Niniejszy paragraf stanowi umowę powierzenia
            przetwarzania danych w rozumieniu art. 28 RODO, zawieraną z chwilą utworzenia
            organizacji.
          </li>
          <li>
            <strong>Przedmiot i cel:</strong> przechowywanie i udostępnianie danych w Serwisie w
            celu organizacji turnieju, prowadzenia terminarza, wyników, statystyk i Portalu — przez
            czas korzystania z Serwisu przez Organizatora.
          </li>
          <li>
            <strong>Zakres:</strong> imię, nazwisko, data urodzenia, numer, pozycja, zdjęcie i
            statystyki Zawodników; imię, nazwisko i e-mail Uczestników. Kategorie osób: Zawodnicy (w
            tym dzieci), ich opiekunowie, trenerzy i sędziowie.
          </li>
          <li>
            Usługodawca przetwarza dane wyłącznie na udokumentowane polecenie Organizatora —
            poleceniami są Regulamin oraz czynności wykonywane przez Organizatora w Serwisie.
          </li>
          <li>
            Usługodawca zapewnia poufność danych (dostęp mają tylko osoby upoważnione, zobowiązane
            do tajemnicy) i stosuje środki bezpieczeństwa z art. 32 RODO.
          </li>
          <li>
            Organizator udziela ogólnej zgody na korzystanie z dalszych podmiotów przetwarzających
            wymienionych w Polityce prywatności (hosting, wysyłka e-maili). O planowanej zmianie
            Usługodawca informuje z wyprzedzeniem, a Organizator może się jej sprzeciwić.
          </li>
          <li>
            Usługodawca pomaga Organizatorowi w realizacji praw osób, których dane dotyczą, oraz
            obowiązków z art. 32–36 RODO, a o naruszeniu ochrony danych zawiadamia go bez zbędnej
            zwłoki, nie później niż w ciągu 48 godzin od jego wykrycia.
          </li>
          <li>
            Po zakończeniu korzystania z Serwisu przez Organizatora Usługodawca usuwa powierzone
            dane, chyba że przepisy nakazują ich dalsze przechowywanie.
          </li>
          <li>
            Usługodawca udostępnia Organizatorowi informacje potrzebne do wykazania spełnienia tych
            obowiązków i umożliwia audyt po wcześniejszym uzgodnieniu terminu.
          </li>
        </ol>
      </LegalSection>

      <LegalSection title="§7. Odpowiedzialność">
        <ol>
          <li>
            Usługodawca dokłada starań, aby Serwis działał bez przerw, ale zastrzega sobie prawo do
            przerw technicznych, o dłuższych informując z wyprzedzeniem.
          </li>
          <li>
            Usługodawca nie odpowiada za treści wprowadzane przez Użytkowników ani za decyzje
            organizacyjne Organizatora (np. wyniki, terminy meczów).
          </li>
        </ol>
      </LegalSection>

      <LegalSection title="§8. Reklamacje">
        <ol>
          <li>
            Reklamacje dotyczące Serwisu można składać na adres <Fill value={LEGAL.contactEmail} />,
            opisując problem i podając adres e-mail konta.
          </li>
          <li>Usługodawca odpowiada na reklamację w ciągu 14 dni.</li>
        </ol>
      </LegalSection>

      <LegalSection title="§9. Blokada konta">
        <p>
          W razie rażącego naruszenia Regulaminu Usługodawca może zablokować konto po bezskutecznym
          wezwaniu do zaprzestania naruszeń, a w przypadkach niecierpiących zwłoki (np. zagrożenie
          bezpieczeństwa danych) — od razu, informując o przyczynie.
        </p>
      </LegalSection>

      <LegalSection title="§10. Usunięcie organizacji">
        <p>
          Organizacja z turniejami, drużynami i danymi Zawodników jest usuwana na prośbę jej
          jedynego Organizatora wysłaną na <Fill value={LEGAL.contactEmail} /> — w ciągu 30 dni,
          razem z kontem Organizatora.
        </p>
      </LegalSection>

      <LegalSection title="§11. Zmiany Regulaminu i postanowienia końcowe">
        <ol>
          <li>
            O zmianach Regulaminu Usługodawca informuje e-mailem co najmniej 14 dni przed ich
            wejściem w życie. Użytkownik, który nie akceptuje zmian, może usunąć konto.
          </li>
          <li>
            Regulamin podlega prawu polskiemu. Nie ogranicza praw konsumentów wynikających z
            bezwzględnie obowiązujących przepisów.
          </li>
        </ol>
      </LegalSection>
    </LegalDocument>
  )
}
