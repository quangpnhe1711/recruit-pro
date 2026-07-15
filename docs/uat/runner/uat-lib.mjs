// RecruitPro staging UAT runner — core lib. No deps (Node 18+ global fetch).
// Executes UAT cases against the live staging API and records PASS/FAIL/BLOCKED.

export const BASE = process.env.UAT_BASE || "https://www.recruitpro.site/api";
export const PW = "Password@123";

export const ACCOUNTS = {
  admin: { portal: "internal", role: "SystemAdmin" },
  minhkhoi: { portal: "internal", role: "SystemAdmin" },
  thucuyen: { portal: "internal", role: "HR" },
  giahan: { portal: "internal", role: "HR" },
  tiendat: { portal: "internal", role: "Manager+HeadDepartment" },
  quocbao: { portal: "internal", role: "Manager" },
  nhatquang: { portal: "candidate", role: "Candidate" },
  haidang: { portal: "candidate", role: "Candidate" },
  yennhi: { portal: "candidate", role: "Candidate(Inactive)" },
};

const tokenCache = new Map(); // username -> { accessToken, refreshToken, user }

export async function api(method, path, { token, body, headers } = {}) {
  const isForm = typeof FormData !== "undefined" && body instanceof FormData;
  // For multipart, let fetch set Content-Type (with boundary); don't JSON-encode.
  const h = { ...(isForm ? {} : { "Content-Type": "application/json" }), ...(headers || {}) };
  if (token) h.Authorization = `Bearer ${token}`;
  let res, json, text;
  try {
    res = await fetch(`${BASE}${path}`, {
      method,
      headers: h,
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    });
    text = await res.text();
    try { json = text ? JSON.parse(text) : null; } catch { json = null; }
  } catch (e) {
    return { status: 0, json: null, text: String(e && e.message || e), networkError: true };
  }
  return { status: res.status, json, text, headers: res.headers };
}

export async function login(username, { portal } = {}) {
  if (tokenCache.has(username)) return tokenCache.get(username);
  const p = portal || ACCOUNTS[username]?.portal || "internal";
  const path = p === "candidate" ? "/auth/candidate/login" : "/auth/internal/login";
  const r = await api("POST", path, { body: { username, password: PW } });
  if (r.status !== 200 || !r.json?.data?.accessToken) {
    throw new Error(`login ${username} (${p}) failed: HTTP ${r.status} ${r.json?.errorCode || r.text?.slice(0,120)}`);
  }
  const tok = {
    accessToken: r.json.data.accessToken,
    refreshToken: r.json.data.refreshToken,
    user: r.json.data.user,
  };
  tokenCache.set(username, tok);
  return tok;
}

// ---- result accumulation ----
const results = []; // { id, title, priority, result, actualApi, notes }
let currentSuite = "";

export function suite(name) { currentSuite = name; }

// A case body returns { result, actualApi, actualData, notes } or throws.
export async function testCase(id, title, priority, fn) {
  const rec = { suite: currentSuite, id, title, priority, result: "FAIL", actualApi: "", actualData: "", notes: "" };
  try {
    const out = await fn();
    Object.assign(rec, out);
    if (!out.result) rec.result = "PASS";
  } catch (e) {
    rec.result = "FAIL";
    rec.notes = `exception: ${e && e.message || e}`;
  }
  results.push(rec);
  const glyph = rec.result === "PASS" ? "PASS" : rec.result === "FAIL" ? "FAIL" : rec.result;
  console.log(`  [${glyph}] ${id} [${priority}] — ${title}${rec.notes ? ` (${rec.notes})` : ""}`);
  return rec;
}

// assertion helpers that return a compact API summary string
export function expectStatus(r, want, path) {
  const got = r.status;
  const codes = Array.isArray(want) ? want : [want];
  const ok = codes.includes(got);
  const summary = `${path || ""} → ${got}${r.json?.errorCode ? ` ${r.json.errorCode}` : ""}`;
  if (!ok) throw new Error(`expected ${codes.join("/")} got ${got}${r.json?.errorCode ? ` (${r.json.errorCode})` : ""}${r.networkError ? " [network]" : ""}`);
  return summary;
}

export function expectErrorCode(r, code) {
  const got = r.json?.errorCode;
  if (got !== code) throw new Error(`expected errorCode ${code} got ${got ?? "none"} (HTTP ${r.status})`);
}

export function getResults() { return results; }

export function summarize() {
  const by = (k) => results.filter((r) => r.result === k).length;
  const total = results.length;
  console.log("\n" + "=".repeat(60));
  console.log(`TOTAL ${total} | PASS ${by("PASS")} | FAIL ${by("FAIL")} | BLOCKED ${by("BLOCKED")} | N/A ${by("N/A")}`);
  console.log("=".repeat(60));
  return { total, pass: by("PASS"), fail: by("FAIL"), blocked: by("BLOCKED"), na: by("N/A") };
}

export function toMarkdown(runMeta) {
  const by = (k) => results.filter((r) => r.result === k).length;
  let md = `# RecruitPro Staging UAT — Execution Log\n\n`;
  md += `- **Environment:** ${BASE}\n- **Run at:** ${runMeta.at}\n- **Runner:** automated (docs/uat/runner)\n\n`;
  md += `## Summary\n\n`;
  md += `| Total | PASS | FAIL | BLOCKED | N/A |\n|---|---|---|---|---|\n`;
  md += `| ${results.length} | ${by("PASS")} | ${by("FAIL")} | ${by("BLOCKED")} | ${by("N/A")} |\n\n`;
  // per-suite
  const suites = [...new Set(results.map((r) => r.suite))];
  md += `## By suite\n\n| Suite | PASS | FAIL | BLOCKED | N/A |\n|---|---|---|---|---|\n`;
  for (const su of suites) {
    const rs = results.filter((r) => r.suite === su);
    md += `| ${su} | ${rs.filter(r=>r.result==="PASS").length} | ${rs.filter(r=>r.result==="FAIL").length} | ${rs.filter(r=>r.result==="BLOCKED").length} | ${rs.filter(r=>r.result==="N/A").length} |\n`;
  }
  md += `\n## Cases\n\n| Case | Pri | Result | API | Notes |\n|---|---|---|---|---|\n`;
  for (const r of results) {
    md += `| ${r.id} | ${r.priority} | ${r.result} | ${(r.actualApi||"").replace(/\|/g,"\\|")} | ${(r.notes||"").replace(/\|/g,"\\|")} |\n`;
  }
  return md;
}
