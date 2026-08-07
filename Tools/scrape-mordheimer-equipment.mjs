// Scrape the per-item "Weapons & Armour" reference pages on mordheimer.net into structured JSON,
// so Claude can cross-check/import Cost+Description+Special Rules for the Trading Post catalog
// instead of parsing raw HTML by hand. Run with: node Tools/scrape-mordheimer-equipment.mjs
//
// Output: Tools/output/<slug>.json (one file per source page) + Tools/output/all.json (combined).
//
// No npm dependencies (native fetch, hand-rolled HTML slicing) - the page markup is a static
// Docusaurus build with a very regular shape (see div.equipment blocks), so a tag-tree scanner is
// enough; no need to pull in a full HTML parser for this one-off.
//
// Gotcha found while validating this script: some entries are grouped under a wrapper
// div.equipment with no Cost/Availability of its own (e.g. "Poisons and Drugs", "Vehicles",
// "Claimed Gnoblars" on the miscellaneous-equipment page) - the real items are NESTED
// div.equipment blocks inside it, using <h4> instead of <h3>. A naive "find balanced top-level
// div.equipment" pass silently swallows those nested items into the parent's text. This scraper
// builds a proper tree so nested items are extracted as their own entries, tagged with `group`.

import { writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const PAGES = [
  { url: 'https://mordheimer.net/docs/weapons-armour/close-combat', category: 'MeleeWeapon' },
  { url: 'https://mordheimer.net/docs/weapons-armour/missile', category: 'MissileWeapon' },
  { url: 'https://mordheimer.net/docs/weapons-armour/blackpowder', category: 'BlackPowderWeapon' },
  { url: 'https://mordheimer.net/docs/weapons-armour/armour', category: 'Armour' },
  { url: 'https://mordheimer.net/docs/weapons-armour/miscellaneous-equipment', category: 'MiscellaneousEquipment' },
    { url: 'https://mordheimer.net/docs/weapons-armour/animal-bestiary', category: 'AnimalBestiary' },
];

const OUT_DIR = path.join(path.dirname(fileURLToPath(import.meta.url)), 'output');

// Source grade tiers seen on the site: core, 1a, 1b, 1c (and presumably 2a+ elsewhere). Only
// core/1a for now - 1b/1c are later errata/annual content, out of scope until we widen import
// coverage. Override with --grades=core,1a,1b,1c (comma-separated, no spaces).
const DEFAULT_GRADES = ['core', '1a'];
const gradesArg = process.argv.find((a) => a.startsWith('--grades='));
const ALLOWED_GRADES = gradesArg ? gradesArg.slice('--grades='.length).split(',') : DEFAULT_GRADES;

/** Pulls the trailing "(core)"/"(1a)"/... tag off a source line like "Mordheim Rulebook (core)". */
function extractGrade(source) {
  const m = (source || '').match(/\(([^)]+)\)\s*$/);
  return m ? m[1] : null;
}

// --- HTML helpers -----------------------------------------------------------------------------

/** Decode the handful of HTML entities/comments that actually show up on this site, plus the
 * zero-width spaces Docusaurus injects after headings (invisible "copy anchor" link target) and
 * the empty <!-- --> React hydration comments it splices into text nodes. */
function decodeEntities(text) {
  return text
    .replace(/<!--\s*-->/g, '')
    .replace(/​/g, '')
    .replace(/&amp;/g, '&')
    .replace(/&quot;/g, '"')
    .replace(/&#x27;|&#39;|&rsquo;|&apos;/g, "'")
    .replace(/&lsquo;/g, '‘')
    .replace(/&ndash;/g, '–')
    .replace(/&mdash;/g, '—')
    .replace(/&hellip;/g, '…')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>');
}

/** Strip all tags, collapse <br> to newlines, keep link/span inner text - good enough for this
 * site's flat markup (no nested block tags inside a paragraph/rule div). */
function stripTags(html) {
  return decodeEntities(
    html
      .replace(/<br\s*\/?>/gi, '\n')
      .replace(/<[^>]+>/g, '')
  )
    .replace(/[ \t]+/g, ' ')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

/** Single linear pass over the whole document building a tree of every <div class="equipment">,
 * nested or not, using a generic div-depth stack so <div class="equipment"> blocks nested inside
 * another one (see gotcha above) become child nodes instead of being swallowed as raw text. */
function buildEquipmentTree(html) {
  const anyDivRe = /<div\b[^>]*>|<\/div>/g;
  const genericStack = [];
  const equipStack = [];
  const roots = [];
  let m;
  while ((m = anyDivRe.exec(html))) {
    if (m[0].startsWith('</div')) {
      const opened = genericStack.pop();
      if (opened?.isEquipment) {
        opened.node.end = anyDivRe.lastIndex;
        equipStack.pop();
      }
    } else if (/class="equipment"/.test(m[0])) {
      const node = { start: m.index, end: null, children: [] };
      if (equipStack.length) equipStack[equipStack.length - 1].children.push(node);
      else roots.push(node);
      equipStack.push(node);
      genericStack.push({ isEquipment: true, node });
    } else {
      genericStack.push({ isEquipment: false });
    }
  }
  return roots;
}

/** The node's own markup with every nested child's span cut out, so parsing a group wrapper
 * (e.g. "Vehicles") never accidentally picks up its first child's Cost/Availability/rules. */
function ownHtml(html, node) {
  let out = '';
  let cursor = node.start;
  for (const child of node.children) {
    out += html.slice(cursor, child.start);
    cursor = child.end;
  }
  out += html.slice(cursor, node.end);
  return out;
}

// --- Item parsing -------------------------------------------------------------------------------

function parseOwnFields(html) {
  const nameMatch = html.match(/data-toc-value="([^"]+)"/);
  const name = nameMatch ? decodeEntities(nameMatch[1]) : null;

  const paragraphs = [...html.matchAll(/<p>([\s\S]*?)<\/p>/g)].map((m) => m[1]);

  let source = null;
  let cost = null;
  let availability = null;
  let flavorText = null;
  const stats = {};

  for (const p of paragraphs) {
    const plain = stripTags(p);
    if (!plain) continue;

    const isPureItalic = /^<em>[\s\S]*<\/em>$/.test(p.trim());
    const costMatch = p.match(/<strong>Cost:<\/strong>([\s\S]*?)(?:<br>|$)/);
    const availMatch = p.match(/<strong>Availability:<\/strong>([\s\S]*?)(?:<br>|$)/);

    if (costMatch || availMatch) {
      if (costMatch) cost = stripTags(costMatch[1]);
      if (availMatch) availability = stripTags(availMatch[1]);
      continue;
    }

    if (isPureItalic && source === null) {
      source = plain;
      continue;
    }

    if (isPureItalic) {
      flavorText = flavorText ? `${flavorText}\n\n${plain}` : plain;
      continue;
    }

    const statRe = /<strong>([^<]+):<\/strong>\s*([\s\S]*?)(?=<strong>|$)/g;
    let statMatch;
    let matchedAny = false;
    while ((statMatch = statRe.exec(p))) {
      matchedAny = true;
      const label = stripTags(statMatch[1]).trim();
      const value = stripTags(statMatch[2]).trim();
      if (label) stats[label] = value;
    }
    if (!matchedAny && plain) {
      flavorText = flavorText ? `${flavorText}\n\n${plain}` : plain;
    }
  }

  const h5Match = html.match(/<h5[^>]*>Special Rules<\/h5>([\s\S]*)$/);
  const specialRules = [];
  if (h5Match) {
    // At this point child nodes have already been cut out of `html`, so every remaining
    // top-level <div>...</div> after the h5 is a rule block for THIS item.
    const ruleDivRe = /<div>([\s\S]*?)<\/div>/g;
    let ruleMatch;
    while ((ruleMatch = ruleDivRe.exec(h5Match[1]))) {
      const inner = ruleMatch[1];
      const ruleNameMatch = inner.match(/^<strong>([^<]+):\s*<\/strong>/);
      if (ruleNameMatch) {
        const description = stripTags(inner.slice(ruleNameMatch[0].length));
        if (description) specialRules.push({ name: stripTags(ruleNameMatch[1]), description });
      } else {
        const description = stripTags(inner);
        if (description) specialRules.push({ name: null, description });
      }
    }
  }

  return { name, source, cost, availability, flavorText, stats, specialRules };
}

function flattenNode(html, node, category, sourceUrl, group, out) {
  const own = ownHtml(html, node);
  const fields = parseOwnFields(own);
  const isGroupWrapper = fields.cost === null && node.children.length > 0;

  if (!isGroupWrapper) {
    const grade = extractGrade(fields.source);
    if (ALLOWED_GRADES.includes(grade)) {
      out.push({ ...fields, grade, category, sourceUrl, group });
    }
  }

  const childGroup = isGroupWrapper ? fields.name : group;
  for (const child of node.children) {
    flattenNode(html, child, category, sourceUrl, childGroup, out);
  }
}

// --- Main -----------------------------------------------------------------------------------

async function scrapePage({ url, category }) {
  const res = await fetch(url, {
    headers: {
      'User-Agent':
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36',
    },
  });
  if (!res.ok) throw new Error(`${url} -> HTTP ${res.status}`);
  // Strip React hydration comments up front - they can land *inside* a tag (e.g.
  // "<strong>Pair<!-- -->: </strong>"), which breaks tag-content regexes downstream if left in.
  const html = (await res.text()).replace(/<!--[\s\S]*?-->/g, '');
  const roots = buildEquipmentTree(html);
  const items = [];
  for (const root of roots) flattenNode(html, root, category, url, null, items);
  return items;
}

async function main() {
  await mkdir(OUT_DIR, { recursive: true });
  console.log(`Grades kept: ${ALLOWED_GRADES.join(', ')}\n`);
  const all = [];

  for (const page of PAGES) {
    const slug = page.url.split('/').pop();
    process.stdout.write(`Scraping ${page.url} ... `);
    try {
      const items = await scrapePage(page);
      console.log(`${items.length} items`);
      await writeFile(path.join(OUT_DIR, `${slug}.json`), JSON.stringify(items, null, 2), 'utf8');
      all.push(...items);
    } catch (err) {
      console.log(`FAILED (${err.message})`);
    }
    await new Promise((r) => setTimeout(r, 500));
  }

  await writeFile(path.join(OUT_DIR, 'all.json'), JSON.stringify(all, null, 2), 'utf8');
  console.log(`\nWrote ${all.length} items total to ${path.join(OUT_DIR, 'all.json')}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
