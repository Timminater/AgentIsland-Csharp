# Verkenning van een mogelijke Avalonia-uitvoering

Dit document is een verkenning en geen actuele migratieopdracht. De huidige productie-app blijft .NET 8 met WPF en Windows-specifieke integraties.

## Doel

Onderzoeken welke onderdelen later platformonafhankelijk kunnen worden zonder de betrouwbare Windows-uitvoering te verstoren.

## Geschikte onderdelen

- Kernmodellen, rekenregels en providercontracten.
- Parsing van lokale JSON- en JSONL-gegevens.
- Gebruiks-, quota- en kostenaggregatie.
- Weergavemodellen zonder directe WPF-afhankelijkheid.

## Windows-specifieke onderdelen

- Systeemvak, vensterpositionering en transparant doorklikken.
- Proces- en terminalnavigatie.
- Windows-aanmeldstart, systeemgeluiden en geheugenteruggave.
- Cursor-opslag via de lokale Windows-SQLitebibliotheek.

## Voorwaarden voor een vervolg

1. De bestaande WPF-testbasis blijft groen.
2. Platformcontracten worden eerst losgekoppeld van zichtbare vensters.
3. Er komt een werkend prototype voor positionering, systeemvak en transparantie.
4. Geheugen, CPU en animatiegedrag worden op echte systemen gemeten.
5. Een migratie gaat pas door wanneer de nieuwe uitvoering aantoonbaar gelijkwaardig is.
