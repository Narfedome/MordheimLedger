// One-off analysis: cross-reference Tools/output/all.json (mordheimer.net scrape) against
// MordheimLedgerApp.Core/Data/SeedData/Equipment.json (common pool) and every band file's own
// Equipment[] array, to see which scraped items already exist in our catalog (just need
// description+specialRules merged in) vs. which are genuinely new. Read-only, writes a report to
// Tools/output/match-report.json - does not touch any seed file.

import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const SEED_DIR = 'MordheimLedgerApp.Core/Data/SeedData';
const BAND_FILES = [
  'Averlanders.json', 'BeastmenRaiders.json', 'CarnivalOfChaos.json', 'CultOfThePossessed.json',
  'DwarfTreasureHunters.json', 'Kislevites.json', 'Marienburgers.json', 'Middenheimers.json',
  'OrcMob.json', 'Ostlanders.json', 'Reiklanders.json', 'SistersOfSigmar.json',
  'SkavenOfClanEshin.json', 'Undead.json', 'WitchHunters.json',
];

function normalize(name) {
  return name
    .toLowerCase()
    .replace(/&/g, 'and')
    .replace(/[^a-z0-9]+/g, ' ')
    .trim();
}

async function main() {
  const scraped = JSON.parse(await readFile('Tools/output/all.json', 'utf8'));
  const commonPool = JSON.parse(await readFile(path.join(SEED_DIR, 'Equipment.json'), 'utf8'));

  const bandIndex = []; // { file, bandNameEn, itemNameEn, restricted }
  for (const file of BAND_FILES) {
    const data = JSON.parse(await readFile(path.join(SEED_DIR, file), 'utf8'));
    for (const eq of data.equipment ?? []) {
      bandIndex.push({ file, bandNameEn: data.name.en, itemNameEn: eq.name.en, restricted: !!eq.restrictedToThisWarband });
    }
  }

  const commonByNorm = new Map(commonPool.map((e) => [normalize(e.name.en), e]));
  const bandByNorm = new Map();
  for (const b of bandIndex) {
    const key = normalize(b.itemNameEn);
    if (!bandByNorm.has(key)) bandByNorm.set(key, []);
    bandByNorm.get(key).push(b);
  }

  const matchedCommon = [];
  const matchedBand = [];
  const unmatched = [];

  for (const item of scraped) {
    const key = normalize(item.name);
    if (commonByNorm.has(key)) {
      matchedCommon.push({ scraped: item, existing: commonByNorm.get(key) });
    } else if (bandByNorm.has(key)) {
      matchedBand.push({ scraped: item, existing: bandByNorm.get(key) });
    } else {
      unmatched.push(item);
    }
  }

  console.log(`Scraped items: ${scraped.length}`);
  console.log(`Matched to Equipment.json (common pool): ${matchedCommon.length}`);
  console.log(`Matched to a band file's own Equipment[]: ${matchedBand.length}`);
  console.log(`No match at all (new to our catalog): ${unmatched.length}`);

  console.log('\n--- Unmatched (name | availability | group) ---');
  for (const u of unmatched) {
    console.log(`- ${u.name} | ${u.availability} | group=${u.group ?? '-'}`);
  }

  await writeFile(
    'Tools/output/match-report.json',
    JSON.stringify({ matchedCommon, matchedBand, unmatched }, null, 2),
    'utf8'
  );
  console.log('\nWrote Tools/output/match-report.json');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
