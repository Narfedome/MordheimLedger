# Relais « Nous écrire »

Les joueurs de Mordheim Ledger (et de Resonance) écrivent depuis l'appli, **sans compte**. Le message suit ce chemin :

```
Appli (Paramètres › Nous écrire)
  └─ POST /report ──► Worker Cloudflare (ce dossier)
                        └─ ticket dans le dépôt privé de support (+ captures dans attachments/)
Dépôt de support : workflow « Tri IA » ─► type, priorité ia:P0-P3, zone, doublons, commentaire technique
Correction mergée : vous posez l'étiquette « corrigé » sur le ticket
Tag de release vX.Y.Z ─► .github/workflows/notify-release.yml ─► POST /released
                        └─ étiquette « livré:X.Y.Z », ticket fermé
```

Aucun e-mail : l'appli ne demande pas d'adresse au joueur (décidé le 2026-11-27 - envoyer des e-mails exige de
vérifier un domaine d'expédition chez un service comme Resend, pas jugé utile). Les corrections sont annoncées dans les
nouveautés de chaque version.

## Mise en place (une fois)

### 1. Dépôt de support
1. Créer un dépôt **privé**, par exemple `Narfedome/support`, avec un README (il faut une branche `main` pour les
   captures).
2. Y copier le contenu de `support-repo/` (`.github/workflows/triage.yml` et `TRIAGE.md`).
3. Secrets du dépôt (Settings › Secrets and variables › Actions) :
   - `CLAUDE_CODE_OAUTH_TOKEN` : utilise l'abonnement Claude Pro/Max (pas de facturation à part, mais compte dans ses
     limites d'usage). S'obtient sur un PC où Claude Code est installé : `claude setup-token`. À défaut,
     `ANTHROPIC_API_KEY` (console.anthropic.com, facturée à l'usage) en changeant la ligne commentée de `triage.yml`.
     Les dépôts MordheimLedger et Resonance étant publics, le tri IA lit leur code sans autre jeton.
4. Notifications : par défaut, GitHub envoie un e-mail à l'auteur d'un ticket quand quelqu'un le commente. Les
   tickets étant créés avec votre jeton, vous êtes « auteur » : vous recevez un e-mail à chaque commentaire du tri IA.

### 2. Jeton GitHub du relais
GitHub › Settings › Developer settings › Fine-grained tokens › nouveau jeton :
- Repository access : **seulement** le dépôt de support ;
- Permissions : **Issues** lecture/écriture, **Contents** lecture/écriture.

### 3. Worker Cloudflare

**Sans ligne de commande** (tout dans dash.cloudflare.com) :
1. Storage & Databases › KV › Create : nom `FEEDBACK_KV`.
2. Compute › Workers & Pages › Create › Start with Hello World : nom `feedback-relay`, Deploy.
3. Edit code : remplacer tout le contenu par `dashboard/worker.js`, puis Deploy.
4. Settings › Bindings › Add › KV namespace : variable `FEEDBACK_KV`, namespace `FEEDBACK_KV`.
5. Settings › Variables and Secrets › Add :
   - Text : `SUPPORT_REPO` = `Narfedome/support`, `APP_KEYS` = `mordheim-ledger=mordheim,resonance=resonance`,
     `RATE_LIMIT_PER_HOUR` = `5` ;
   - Secret : `GITHUB_TOKEN` (étape 2), `RELEASE_TOKEN` (un mot de passe inventé, voir étape 4).
6. L'adresse du Worker est dans Settings › Domains & Routes (`https://feedback-relay.<compte>.workers.dev`).

Après une modification de `src/index.ts` : `npm run build:dashboard`, puis recoller `dashboard/worker.js`. Dans ce
mode, la configuration vit dans le tableau de bord : ne pas mélanger avec `wrangler deploy`, qui la remplacerait par
celle de `wrangler.toml`.

**Avec la ligne de commande**, depuis ce dossier :

```bash
npm install
npx wrangler login
npx wrangler kv namespace create FEEDBACK_KV      # recopier l'id dans wrangler.toml
npx wrangler secret put GITHUB_TOKEN              # le jeton de l'étape 2
npx wrangler secret put RELEASE_TOKEN             # une longue valeur aléatoire, gardée pour l'étape 4
npx wrangler deploy                               # affiche l'adresse : https://feedback-relay.<compte>.workers.dev
```

Vérifier `SUPPORT_REPO` dans `wrangler.toml`.

### 4. Appli et release
1. `MordheimLedgerApp/Services/FeedbackService.cs` et, dans Resonance, `Resonance/Services/FeedbackService.cs` :
   renseigner `Endpoint` avec `https://…workers.dev/report`. Tant qu'il est nul, Paramètres ouvre toujours l'ancien
   formulaire Google.
2. Secrets des **deux** dépôts (MordheimLedger et Resonance) : `FEEDBACK_RELAY_URL` (`https://…workers.dev`, sans
   `/report`) et `FEEDBACK_RELEASE_TOKEN` (même valeur que `RELEASE_TOKEN`). Chaque dépôt a son
   `.github/workflows/notify-release.yml`, qui n'annonce que les tickets de son appli.
3. Avant la sortie : remplir de nouveau la section « Sécurité des données » de la fiche Google Play (messages, captures,
   informations de l'appareil, envoyés à la demande de l'utilisateur) et l'équivalent du Microsoft Store.

## Au quotidien

- Un ticket arrive avec `app:mordheim`, `type:…` et `à-trier` ; le tri IA remplace `à-trier` par `ia:P0`-`ia:P3`,
  `zone:…` et commente. Les étiquettes `ia:` sont des propositions : la priorité définitive reste la vôtre.
- Une fois la correction mergée sur `master` : étiquette **`corrigé`** sur le ticket.
- Au tag de release (`Deploy/Release-*.ps1`) : les tickets `corrigé` deviennent `livré:X.Y.Z`
  et sont fermés.
- Retrier un ticket : Actions › Tri IA › Run workflow, avec son numéro.

## Limites

- 5 messages par heure et par adresse IP (`RATE_LIMIT_PER_HOUR`), 3 captures de 1,5 Mo au plus, mêmes limites de
  texte que `MordheimLedgerApp.Core/Rules/FeedbackRules.cs` - les garder alignées.
- L'identifiant d'appli (`X-Feedback-Key`, `APP_KEYS`) est public : il écarte les robots, pas un attaquant
  déterminé. La limite d'envois et la validation font le reste.
- Un message déjà reçu (même `id`, renvoyé par la file hors connexion de l'appli) répond 409 et n'est pas dupliqué.

## Développement

```bash
npm run check          # vérification des types
npx wrangler dev       # local, avec un fichier .dev.vars (GITHUB_TOKEN=..., RELEASE_TOKEN=...)
```
