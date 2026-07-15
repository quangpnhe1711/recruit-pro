import { writeFileSync } from "node:fs";
import { BASE, summarize, toMarkdown, getResults } from "./uat-lib.mjs";
import { runAuth } from "./suite-auth.mjs";
import { runPub } from "./suite-pub.mjs";
import { runCjob } from "./suite-cjob.mjs";
import { runDash } from "./suite-dash.mjs";
import { runLog } from "./suite-log.mjs";
import { runMdata } from "./suite-mdata.mjs";
import { runNoti } from "./suite-noti.mjs";
import { runRbac } from "./suite-rbac.mjs";
import { runErr } from "./suite-err.mjs";
import { runApi } from "./suite-api.mjs";
import { runSec } from "./suite-sec.mjs";
import { runUi } from "./suite-ui.mjs";
import { runWrite } from "./suite-write.mjs";
import { runWrite2 } from "./suite-write2.mjs";
import { runWrite3 } from "./suite-write3.mjs";
import { runAi } from "./suite-ai.mjs";

// Order: public/reads first, then auth, then reads/negatives, then cross-cutting, then stateful writes.
const SUITES = {
  pub: runPub, cjob: runCjob, auth: runAuth,
  dash: runDash, log: runLog, mdata: runMdata, noti: runNoti, rbac: runRbac,
  err: runErr, api: runApi, sec: runSec, ui: runUi,
  ai: runAi,
  write: runWrite, write2: runWrite2, write3: runWrite3,
};

const only = process.argv.slice(2); // e.g. `node run-all.mjs auth`
const toRun = only.length ? only : Object.keys(SUITES);

console.log(`RecruitPro staging UAT — ${BASE}`);
console.log(`Suites: ${toRun.join(", ")}\n`);

for (const key of toRun) {
  const fn = SUITES[key];
  if (!fn) { console.log(`(skip unknown suite: ${key})`); continue; }
  console.log(`\n### Suite: ${key}`);
  await fn();
}

const sum = summarize();
const at = new Date().toISOString();
const md = toMarkdown({ at });
const outPath = new URL("./RESULTS.md", import.meta.url);
writeFileSync(outPath, md);
writeFileSync(new URL("./RESULTS.json", import.meta.url), JSON.stringify({ at, base: BASE, sum, cases: getResults() }, null, 2));
console.log(`\nWrote ${outPath.pathname}`);
