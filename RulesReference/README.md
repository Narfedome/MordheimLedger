# RulesReference

Notes de règles condensées (français, paraphrasées — pas de copie verbatim) extraites de
[La Grande Librairie de Mordheim](https://sites.google.com/view/grande-librairie-de-mordheim)
(site fan FR), constituées le 2026-08-04 pour servir de référence directe pendant le dev de
l'appli plutôt que de re-parcourir le site à chaque session. Le livre de règles PDF officiel reste
la source ultime mais est trop volumineux pour l'outil de lecture — voir CLAUDE.md § Sources.

Chaque fichier commence par une note de couverture (pages visitées en profondeur vs. index
seulement) — à compléter dans une passe suivante si besoin de plus de détail sur un point précis.

- [Regles.md](Regles.md) — Règles core : caractéristiques, tour de jeu, phases, Blessures **en
  combat** (Sonné/À terre/Hors de combat — différent des Blessures Graves post-bataille),
  Commandement/Psychologie, Règles maison.
- [Campagne.md](Campagne.md) — Séquence d'après-bataille, **Blessures Graves (Héros D66 complet +
  Hommes de main D6 — la distinction qui bloquait le End of Game)**, Expérience, Mutations,
  Revenus, Commerce. Le plus complet et le plus directement utilisé par le code actuel.
- [MarcheEtMagie.md](MarcheEtMagie.md) — Place du Marché (armes/armures/objets/rareté) et Magie
  (apprentissage/lancement de sorts, listes par tradition).
- [Bandes.md](Bandes.md) — Catalogue des 52 bandes (7 détaillées, le reste index + thème inféré à
  vérifier).
- [FrancsTireurs.md](FrancsTireurs.md) — Mécanique générale des Francs-Tireurs + catalogue des 59
  entrées (8 détaillées).
- [ScenariosEtDivers.md](ScenariosEtDivers.md) — Scénarios (index des ~50 titres), Dramatis
  Personae (13 persos), Settings, Publications.

## Écarts trouvés avec le code existant (à corriger séparément si pas déjà fait)

- `MordheimLedgerApp/Services/HenchmanInjuryTable.cs` : le placeholder (1D6, 1-2 mort, 3-6
  rétablissement) était en fait **correct** — confirmé par Campagne.md, plus besoin de le flaguer
  comme non vérifié.
- `MordheimLedgerApp/Services/SeriousInjuryTable.cs` : la structure D66 (36 codes, 11-15 = Mort)
  était correcte mais le **texte associé à chaque résultat était faux/mélangé** — corrigé dans
  `AppStrings.resx`/`AppStrings.en.resx` (clés `InjurySerious11`..`66`) pour matcher la vraie table
  p.118-119.
- `MordheimLedgerApp.Core/Data/OfficialContentSeed.cs` (Reiklander Mercenaries) : les 4 profils de
  base concordent avec Bandes.md, mais la bande a aussi deux sous-types d'Hommes de main
  (Tireurs/Spadassins) absents du seed — pas un bug, juste une extension possible plus tard.
