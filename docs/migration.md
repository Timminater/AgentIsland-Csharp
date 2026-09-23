# Migratiehandleiding

## Vanuit een oudere Windows-versie

1. Sluit AgentIsland volledig af via het systeemvak.
2. Maak desgewenst een reservekopie van `%APPDATA%\AgentIsland`.
3. Pak de nieuwe release uit in een nieuwe map.
4. Start `AgentIsland.exe` en controleer providers, taal en waarschuwingen.
5. Verwijder de oude map pas nadat de nieuwe versie goed werkt.

Gebruikersinstellingen blijven in de toepassingsgegevensmap staan en worden opnieuw gebruikt. Niet langer ondersteunde taalwaarden vallen terug op de systeemkeuze.

## Voor ontwikkelaars

- Gebruik .NET 8 op Windows x64.
- Herstel en bouw de volledige oplossing.
- Voer de volledige testset uit voordat een pakket wordt gemaakt.
- Maak een releasepakket via `build.ps1` met een expliciet versienummer.
- Publiceer zowel het zipbestand als het bijbehorende SHA-256-bestand.

## Compatibiliteit

AgentIsland leest bestaande providergegevens alleen-lezen. Verwijder of wijzig geen providerarchieven tijdens een migratie. Bij problemen kan de nieuwe map worden verwijderd en de vorige uitvoering opnieuw worden gestart.
