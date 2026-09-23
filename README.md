# AgentIsland voor Windows

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?style=flat&logo=windows)](https://www.microsoft.com/windows)
[![Tests](https://img.shields.io/badge/Tests-91%20geslaagd-brightgreen?style=flat&logo=githubactions)](tests/AgentIsland.Tests)
[![Licentie](https://img.shields.io/badge/Licentie-MIT-blue.svg)](LICENSE)

AgentIsland is een compacte WPF-app voor Windows die de activiteit, gebruikslimieten, kosten en beurten van AI-codeassistenten in een dynamisch eiland bovenaan het scherm toont.

De app ondersteunt Claude Code, OpenAI Codex, DeepSeek Harness, Google Antigravity, xAI Grok en Cursor.

## Belangrijkste mogelijkheden

- Actuele sessiestatus: bezig, wacht op jou, inactief of vereist aandacht.
- Overzicht van verbruik, resterend tegoed, resetmomenten en lokale kostenhistorie.
- Instelbare voorspelling van de resterende gebruiksduur in de bovenbalk.
- Goedkeuringen, vragen en plancontroles van Claude Code rechtstreeks op het eiland.
- Dag-, week- en maandrapporten die lokaal als afbeelding kunnen worden opgeslagen.
- Snel wisselen tussen alle ingeschakelde providers.
- Nederlandse en Engelse interface.

## Privacy

AgentIsland werkt lokaal. Broncode, prompts en gespreksinhoud worden niet door AgentIsland naar een eigen server verzonden. Voor quota en saldo gebruikt de app uitsluitend de officiële providerinterfaces en reeds aanwezige lokale aanmeldgegevens.

## Ondersteunde providers

| Provider | Activiteit | Tokens | Quota of saldo | Kostenhistorie |
| --- | :---: | :---: | :---: | :---: |
| Claude Code | Ja | Ja | Officiële interface | Ja |
| OpenAI Codex | Ja | Ja | Sessiemetadata | Ja |
| DeepSeek Harness | Ja | Ja | Officiële saldo-interface | Ja |
| Google Antigravity | Ja | Ja | Eigen uitlezer | Ja |
| xAI Grok | Ja | Ja | Gebruiksinterface | Ja |
| Cursor | Ja | Ja | Quotastatus | Ja |

## Installeren

1. Open [Releases](https://github.com/Timminater/AgentIsland-Csharp/releases).
2. Download `AgentIsland-2.6.1-win-x64.zip`.
3. Pak het bestand uit.
4. Start `AgentIsland.exe`.

De download is zelfstandig en vereist geen afzonderlijke installatie van de .NET-SDK.

## Zelf bouwen

Vereisten:

- Windows 10 of Windows 11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet build AgentIsland.sln
dotnet test AgentIsland.sln
.\build.ps1 -Runtime win-x64 -Version 2.6.1
```

De pakketbuild verschijnt als `dist/AgentIsland-2.6.1-win-x64.zip`.

## Projectstructuur

```text
src/AgentIsland.Core       domeinmodellen en algemene logica
src/AgentIsland.Providers  provideruitlezers en adapters
src/AgentIsland.Windows    Windows-integratie
src/AgentIsland            WPF-interface en toepassingshost
tests/AgentIsland.Tests    geautomatiseerde tests
docs/releases              Nederlandstalige release-informatie
```

## Ontwerp en kwaliteit

- .NET 8, WPF, dependency injection en beheerde achtergrondtaken.
- Begrensde en incrementele verwerking van lokale logbestanden.
- Veilige, atomaire opslag van instellingen en goedkeuringsberichten.
- 91 geslaagde tests: 90 reguliere tests en 1 stress- en resourcetest.
- Een zelfvoorzienend Windows x64-releasepakket met SHA-256-controlebestand.

## Herkomst en licentie

Deze zelfstandige Windows-uitvoering bouwt voort op het oorspronkelijke open-sourceproject [Agent Island](https://github.com/agent-island/agent-island). Zie [LICENSE](LICENSE) voor de licentievoorwaarden.
