# Prestaties en resourcegebruik

AgentIsland is bedoeld als continu actieve desktopapp. Prestatieclaims moeten daarom met de meegeleverde meetmiddelen op een echte Windows-computer worden gecontroleerd.

## Ontwerpkeuzes

- Logbestanden worden incrementeel en waar mogelijk vanaf het einde gelezen.
- Grote tijdelijke buffers komen uit gedeelde geheugenpools.
- Bestandsvingerafdrukken voorkomen herverwerking van ongewijzigde gegevens.
- Providerscans hebben een beperkte gelijktijdigheid om schijfpieken te voorkomen.
- Achtergrondtaken gebruiken timers buiten de interface-thread.
- Niet langer benodigde caches kunnen gericht worden leeggemaakt.

## Reproduceerbare controle

Gebruik `scripts/Measure-ProcessResources.ps1` voor CPU- en werkgeheugenmetingen. Gebruik `Launch-StressUI.bat` voor de geïsoleerde stressweergave met een groot historisch gegevensbestand.

Meet minimaal:

1. koude start;
2. stabiele inactieve toestand;
3. actieve providerscan;
4. uitgebreid rapportvenster;
5. geheugen na het uitschakelen van een zware provider.

Werkgeheugen verschilt per provider, sessievolume, schermschaal, animatiestatus en grafische driver. Historische momentopnamen zijn daarom geen vaste bovengrens.
