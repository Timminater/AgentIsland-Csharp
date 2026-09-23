# Architectuur

## Afhankelijkheidsrichting

```text
AgentIsland.Core
    ↑
AgentIsland.Providers      AgentIsland.Windows
    ↑                         ↑
             AgentIsland (WPF)
```

`AgentIsland.Core` bevat platformonafhankelijke modellen en contracten. `AgentIsland.Providers` verwerkt providergegevens. `AgentIsland.Windows` bevat Windows-paden, processen, opslag en systeemintegratie. Het WPF-project vormt de toepassingshost en de zichtbare interface.

## Verantwoordelijkheden

- Kern: statussen, gebruiksgegevens, kostenmodellen, instellingencontracten en algemene rekenregels.
- Providers: sessie-, quota- en kostenadapters voor de ingebouwde providers.
- Windows: lokale gegevenspaden, veilige bestandsopslag, procesnavigatie en Windows-specifieke diensten.
- WPF: vensters, bedieningselementen, animaties, weergavemodellen en toepassingscompositie.

## Provideruitbreiding

Een provider verklaart alleen de mogelijkheden die werkelijk beschikbaar zijn: activiteit, gebruik, kosten en sessienavigatie. Nieuwe providers worden in broncode geregistreerd. Dynamisch laden van willekeurige DLL's maakt geen deel uit van het huidige ontwerp.

Een volledig nieuwe providersleutel vereist ook aanpassing van de vaste interfacekoppelingen, zichtbaarheid, selectie, activiteitstypen, logo's en tests. Alleen registratie in dependency injection is niet voldoende.

## Achtergrondverwerking

De toepassing gebruikt `Microsoft.Extensions.Hosting`. Periodieke taken draaien buiten de WPF-interface en sturen alleen zichtbare wijzigingen terug naar de interface-thread.

## Opslag en veiligheid

Instellingen en interactieberichten worden atomair geschreven. Lokale logbestanden worden alleen-lezen verwerkt. Goedkeuringen vervallen veilig en vallen bij afwezigheid van de app terug op het oorspronkelijke providerproces.
