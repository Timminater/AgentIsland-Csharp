# Productieplan voor rapportillustraties

Dit document beschrijft de naamgeving en plaatsing van de illustraties in rapportkaarten.

## Mappen

- `assets/Report/portretten`: losse providerportretten.
- `assets/Report/duels`: samengestelde duelillustraties.
- Dezelfde mappen onder `src/AgentIsland/Assets/Report` worden in de WPF-app opgenomen.

## Naamgeving

- `duel-{links}-win-{rechts}.png`: de linker provider wint.
- `duel-{links}-lose-{rechts}.png`: de rechter provider wint.
- `duel-{links}-draw-{rechts}.png`: gelijkspel.

De volgorde van de providernamen bepaalt de positie in de afbeelding. Bestanden gebruiken kleine letters, streepjes en transparante PNG-achtergronden.

## Visuele eisen

- Vierkant bronbestand met transparante achtergrond.
- Duidelijke silhouetten op kleine rapportkaarten.
- Geen tekst in de illustratie; labels worden door de app getekend.
- Logo's en kleuren moeten ook bij verkleining herkenbaar blijven.
