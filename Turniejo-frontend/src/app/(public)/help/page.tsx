import { ChevronDown } from "lucide-react"
import type { Metadata } from "next"
import Link from "next/link"
import type { ReactNode } from "react"

import { PASSWORD_HINT } from "@/lib/auth/password"

export const metadata: Metadata = {
  title: "Pomoc",
  description:
    "Czym jest Turniejo, dla kogo jest i jak zacząć - organizator, trener, sędzia, rodzic.",
}

type Faq = { question: string; answer: ReactNode }
type FaqSection = { title: string; items: Faq[] }

const SECTIONS: FaqSection[] = [
  {
    title: "O Turniejo",
    items: [
      {
        question: "Czym jest Turniejo?",
        answer: (
          <p>
            Aplikacją do prowadzenia turniejów piłkarskich od zapisów do finału: drużyny i
            kategorie, boiska, automatyczny terminarz, wyniki na żywo, tabele, drabinka pucharowa i
            statystyki zawodników. Wszyscy uczestnicy - organizator, trenerzy, sędziowie, rodzice i
            kibice - widzą te same, aktualne dane.
          </p>
        ),
      },
      {
        question: "Dla kogo jest Turniejo?",
        answer: (
          <ul className="list-disc space-y-1 pl-5">
            <li>
              <strong>Organizator</strong> - tworzy turniej, przyjmuje drużyny, generuje terminarz,
              przydziela sędziów i boiska.
            </li>
            <li>
              <strong>Trener</strong> - zarządza składem swojej drużyny, widzi jej mecze i
              statystyki.
            </li>
            <li>
              <strong>Sędzia</strong> - prowadzi przydzielone mecze na żywo: gole, asysty, kartki.
            </li>
            <li>
              <strong>Rodzic</strong> - śledzi terminarz, wyniki i statystyki swojego dziecka.
            </li>
            <li>
              <strong>Kibic</strong> - ogląda publiczny portal turnieju, także bez konta.
            </li>
          </ul>
        ),
      },
      {
        question: "Czy jedno konto może mieć kilka ról?",
        answer: (
          <p>
            Tak. Role są przypisane do konkretnego turnieju, drużyny czy zawodnika, więc możesz
            organizować jeden turniej, sędziować w innym i jednocześnie śledzić mecze dziecka. Menu
            pokazuje tylko sekcje, do których masz dostęp.
          </p>
        ),
      },
    ],
  },
  {
    title: "Jak zacząć",
    items: [
      {
        question: "Organizuję turniej - od czego zacząć?",
        answer: (
          <ol className="list-decimal space-y-1 pl-5">
            <li>Załóż konto i wybierz „Organizuję turnieje”.</li>
            <li>Utwórz turniej: nazwa, termin, miejsce.</li>
            <li>Dodaj kategorie (np. roczniki), boiska i drużyny.</li>
            <li>Wybierz format - grupowy, pucharowy albo grupy + puchar - i zamknij zapisy.</li>
            <li>Wygeneruj terminarz i przydziel sędziów do meczów.</li>
            <li>
              Roześlij kody zaproszeń trenerom, sędziom i rodzicom, a kibicom - link do portalu
              turnieju.
            </li>
          </ol>
        ),
      },
      {
        question: "Dostałem kod od organizatora - co dalej?",
        answer: (
          <>
            <p>
              Załóż konto jako „Dołączam do turnieju” (albo zaloguj się), wejdź w „Dołącz kodem” i
              wpisz 6-znakowy kod. Jeśli dostałeś link z kodem, wystarczy go otworzyć - po
              zalogowaniu dołączysz automatycznie. Turniej pojawi się w menu „Dołączone turnieje”.
            </p>
            <p>
              Sędzia od razu widzi swoje mecze. Trenera organizator przypisuje jeszcze do drużyny, a
              rodzica do dziecka - wtedy w menu pojawi się Twój panel.
            </p>
          </>
        ),
      },
      {
        question: "Czy mogę oglądać turniej bez konta?",
        answer: (
          <p>
            Tak, jeśli organizator udostępnił publiczny portal turnieju. Pod linkiem od organizatora
            znajdziesz terminarz, wyniki na żywo, tabele, drabinkę i statystyki - bez logowania.
          </p>
        ),
      },
      {
        question: "Założyłem konto uczestnika, a chcę zorganizować turniej.",
        answer: (
          <p>
            Nie musisz zakładać nowego konta. Na stronie startowej wybierz „Zostań organizatorem” i
            podaj nazwę organizacji - od razu możesz tworzyć turnieje.
          </p>
        ),
      },
    ],
  },
  {
    title: "W trakcie turnieju",
    items: [
      {
        question: "Jak sędzia prowadzi mecz?",
        answer: (
          <p>
            W zakładce „Sędziowanie” wybierz mecz i otwórz panel na żywo. Rozpoczynasz mecz,
            dodajesz gole, asysty i kartki, a na koniec kończysz spotkanie. Wynik, tabela i drabinka
            aktualizują się od razu u wszystkich.
          </p>
        ),
      },
      {
        question: "Skąd wiem, że coś się zmieniło?",
        answer: (
          <p>
            Ogłoszenia organizatora i zmiany w meczach (np. nowa godzina czy boisko) trafiają do
            zakładki „Powiadomienia”. Wyniki na żywo odświeżają się same - nie trzeba przeładowywać
            strony.
          </p>
        ),
      },
    ],
  },
  {
    title: "Wymagania i konto",
    items: [
      {
        question: "Czego potrzebuję, żeby korzystać z Turniejo?",
        answer: (
          <ul className="list-disc space-y-1 pl-5">
            <li>Aktualnej przeglądarki - Chrome, Safari, Firefox lub Edge.</li>
            <li>Telefonu, tabletu albo komputera - aplikacja działa na każdym ekranie.</li>
            <li>Połączenia z internetem (szczególnie sędzia podczas meczu).</li>
            <li>Adresu email do założenia konta. Nic nie trzeba instalować.</li>
          </ul>
        ),
      },
      {
        question: "Jakie są wymagania dotyczące hasła?",
        answer: <p>{PASSWORD_HINT} Numer telefonu jest opcjonalny.</p>,
      },
      {
        question: "Jak pobrać swoje dane albo usunąć konto?",
        answer: (
          <p>
            Po zalogowaniu wejdź w menu konta → „Moje konto”. Możesz tam pobrać swoje dane w pliku
            JSON i usunąć konto. Szczegóły znajdziesz w{" "}
            <Link href="/privacy" className="text-primary font-medium hover:underline">
              polityce prywatności
            </Link>
            .
          </p>
        ),
      },
      {
        question: "Czy portal pokazuje pełne nazwiska dzieci?",
        answer: (
          <p>
            Domyślnie nie - kibice widzą zawodników jako „Jan K.”. Organizator może to zmienić w
            ustawieniach portalu, jeśli ma na to zgodę rodziców.
          </p>
        ),
      },
      {
        question: "Nie pamiętam hasła.",
        answer: (
          <p>
            Na stronie logowania wybierz{" "}
            <Link href="/password-reset" className="text-primary font-medium hover:underline">
              reset hasła
            </Link>{" "}
            - wyślemy Ci email z linkiem do ustawienia nowego.
          </p>
        ),
      },
    ],
  },
]

function FaqItem({ question, answer }: Faq) {
  return (
    <details className="group bg-card rounded-lg border">
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 p-4 font-medium [&::-webkit-details-marker]:hidden">
        {question}
        <ChevronDown className="text-muted-foreground size-4 shrink-0 transition-transform group-open:rotate-180" />
      </summary>
      <div className="text-muted-foreground space-y-2 px-4 pb-4 text-sm leading-relaxed">
        {answer}
      </div>
    </details>
  )
}

export default function HelpPage() {
  return (
    <div className="space-y-8">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">Pomoc i najczęstsze pytania</h1>
        <p className="text-muted-foreground">
          Czym jest Turniejo, dla kogo jest i jak zacząć - niezależnie od tego, czy organizujesz
          turniej, prowadzisz drużynę, sędziujesz czy kibicujesz.
        </p>
      </div>
      {SECTIONS.map((section) => (
        <section key={section.title} className="space-y-3">
          <h2 className="text-lg font-semibold">{section.title}</h2>
          <div className="space-y-2">
            {section.items.map((item) => (
              <FaqItem key={item.question} {...item} />
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
