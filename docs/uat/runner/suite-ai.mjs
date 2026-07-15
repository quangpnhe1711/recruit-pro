import { api, login, suite, testCase, expectStatus } from "./uat-lib.mjs";

// PHASE 2d — AI copilot: ONLY the cheap, non-generative cases (permission gates, IDOR, unknown-id
// negatives, no-secret-leak). Generative cases (ranking/fit/questions/email) consume AI-provider
// credits + need a Screening pool → see MANUAL-CHECKLIST.md, not automated here.

const UNKNOWN_GUID = "00000000-0000-0000-0000-000000000000";
const SECRET_KEYS = /passwordHash|"password"|apiKey|api_key|secret|connectionString|systemPrompt|bearer\s|BEGIN (RSA|PRIVATE)/i;

export async function runAi() {
  suite("AI");

  await testCase("UAT-AI-001", "Copilot job picker is role-gated (HR 200, candidate 403, guest 401)", "P2", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const cand = (await login("nhatquang")).accessToken;
    const rHr = await api("GET", "/copilot/jobs", { token: hr });
    const rCand = await api("GET", "/copilot/jobs", { token: cand });
    const rGuest = await api("GET", "/copilot/jobs");
    expectStatus(rHr, 200, "HR GET /copilot/jobs");
    expectStatus(rCand, 403, "candidate GET /copilot/jobs");
    expectStatus(rGuest, 401, "guest GET /copilot/jobs");
    return { actualApi: `HR→200, candidate→403, guest→401` };
  });

  await testCase("UAT-AI-006", "Unknown ranking session → 404/400 (no leak)", "P3", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const r = await api("GET", `/copilot/ranking-sessions/${UNKNOWN_GUID}`, { token: hr });
    expectStatus(r, [404, 400], "GET ranking-sessions/{unknown}");
    return { actualApi: `unknown ranking session → ${r.status} ${r.json?.errorCode || ""}` };
  });

  await testCase("UAT-AI-016", "Copilot conversation isolation/IDOR — unknown/other id → 403/404", "P0", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const r = await api("GET", `/copilot/conversations/${UNKNOWN_GUID}`, { token: hr });
    expectStatus(r, [403, 404, 400], "GET conversations/{unknown}");
    return { actualApi: `foreign/unknown conversation → ${r.status} ${r.json?.errorCode || ""}`, notes: "cross-HR real-session IDOR needs two live sessions — see MANUAL-CHECKLIST" };
  });

  await testCase("UAT-AI-017", "Candidate pool for unknown job → 404/400 (negative)", "P2", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const r = await api("GET", `/copilot/jobs/${UNKNOWN_GUID}/candidates`, { token: hr });
    expectStatus(r, [404, 400, 403], "GET copilot/jobs/{unknown}/candidates");
    return { actualApi: `unknown-job pool → ${r.status} ${r.json?.errorCode || ""}` };
  });

  await testCase("UAT-AI-015", "No secret leakage in copilot responses", "P1", async () => {
    const hr = (await login("thucuyen")).accessToken;
    const jobs = await api("GET", "/copilot/jobs", { token: hr });
    if (SECRET_KEYS.test(jobs.text || "")) throw new Error("copilot/jobs leaks a secret/system-prompt field");
    return { actualApi: "copilot/jobs: no apiKey/systemPrompt/secret fields" };
  });
}
