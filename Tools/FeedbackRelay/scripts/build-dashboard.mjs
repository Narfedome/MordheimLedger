// Produit dashboard/worker.js (JavaScript simple) depuis src/index.ts, pour un déploiement par copier-coller dans
// l'éditeur du Worker sur dash.cloudflare.com, qui ne compile pas le TypeScript.
import { execSync } from "node:child_process";
import { readFileSync, writeFileSync, unlinkSync } from "node:fs";

execSync("npx tsc --noEmit false --outDir dashboard --removeComments false", { stdio: "inherit" });
const header =
  "// FICHIER GÉNÉRÉ par « npm run build:dashboard » depuis src/index.ts - ne pas modifier à la main.\n" +
  "// À coller tel quel dans l'éditeur du Worker sur dash.cloudflare.com (voir README.md).\n\n";
writeFileSync("dashboard/worker.js", header + readFileSync("dashboard/index.js", "utf8"));
unlinkSync("dashboard/index.js");
