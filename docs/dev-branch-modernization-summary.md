# Samenvatting van de modernisering

Dit is een historisch overzicht van de modernisering van de ontwikkeltak. De actuele testbasis op `main` is 91 tests: 90 reguliere tests en 1 stress- en resourcetest.

## Bereikte verbeteringen

- Bedrijfslogica is van globale statische instanties naar dependency injection verplaatst.
- `Microsoft.Extensions.Hosting` beheert de levensduur en periodieke achtergrondtaken.
- Providerfuncties zijn opgesplitst in expliciete contracten voor activiteit, gebruik, kosten en navigatie.
- Instellingen gebruiken getypeerde opties en atomaire opslag.
- Netwerkverzoeken gebruiken een centrale clientfabriek, time-outs en herstelbeleid.
- Venster- en dialoogdiensten houden weergavemodellen los van concrete WPF-vensters.
- Tests gebruiken geïsoleerde opslag en kunnen grotendeels gelijktijdig draaien.

## Belangrijke reparaties

Een dubbele constructor in de alarmketen veroorzaakte onduidelijke dependency-injectionselectie en is verwijderd. De toepassingsinstantie wordt veilig benaderd. Achtergrondwerk stuurt alleen noodzakelijke wijzigingen naar de interface-thread.

## Architectuurregels

1. `AgentIsland.Core` verwijst niet terug naar platform- of interfacelagen.
2. Diensten ontvangen hun afhankelijkheden via constructors.
3. Externe gegevens worden begrensd, incrementeel en defensief verwerkt.
4. Nieuwe providers krijgen end-to-endtests voor selectie, zichtbaarheid en gegevensstromen.
5. Historische meetresultaten gelden niet automatisch als actuele prestatiegarantie.
