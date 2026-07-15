import { api, login, suite, testCase, expectStatus, expectErrorCode } from "./uat-lib.mjs";

// Shared ids for negative lookups.
const UNKNOWN_GUID = "11111111-1111-4111-8111-111111111111";

// Assert the error envelope contract: flat {success:false, statusCode, errorCode, message, data:null}
// plus the nested self-describing `error{type,code,fieldErrors,globalErrors,traceId}` block.
function assertErrEnvelope(r) {
  const j = r.json || {};
  if (j.success !== false) throw new Error(`success not false (got ${JSON.stringify(j.success)})`);
  if (typeof j.statusCode !== "number") throw new Error("missing numeric statusCode");
  if (!("errorCode" in j)) throw new Error("missing errorCode");
  if (!("message" in j)) throw new Error("missing message");
  if (j.data !== null && j.data !== undefined) throw new Error(`data not null: ${JSON.stringify(j.data)}`);
  if (!j.error || typeof j.error !== "object") throw new Error("missing nested error block");
  if (!j.error.type || !j.error.code) throw new Error("nested error missing type/code");
  return `code=${j.errorCode} type=${j.error.type} trace=${j.error.traceId ? "y" : "n"}`;
}

export async function runErr() {
  suite("ERR");

  await testCase("UAT-ERR-001", "Field-level validation → 400 VALIDATION_FAILED with camelCase fieldErrors", "P0", async () => {
    // register is multipart-only: JSON body → 415; multipart missing fields → raw ASP.NET ProblemDetails
    // (PascalCase `errors`, no `errorCode`/`error.type`), NOT the app VALIDATION_FAILED envelope.
    const rjson = await api("POST", "/candidates/register", { body: {} });
    const rmp = await api("POST", "/candidates/register", { headers: { "Content-Type": "multipart/form-data; boundary=X" }, body: undefined });
    const hasCustom = rjson.json?.errorCode === "VALIDATION_FAILED" || rmp.json?.errorCode === "VALIDATION_FAILED";
    if (hasCustom) return { actualApi: "register → VALIDATION_FAILED envelope" };
    return {
      result: "FAIL",
      actualApi: `register JSON→${rjson.status}, multipart-missing→400 ProblemDetails`,
      notes: "FINDING-CONTRACT-01 (Low): model-binding validation returns ASP.NET ProblemDetails (PascalCase errors, no errorCode), not the app VALIDATION_FAILED+camelCase envelope. FE apiError.ts:405 falls back to data.errors so errors DO surface, but PascalCase field names don't match camelCase form fields → inline field error degrades to a toast; no stable errorCode",
    };
  });

  await testCase("UAT-ERR-002", "Backend message present for debug; code+nested error also present", "P1", async () => {
    const r = await api("GET", `/jobs/${UNKNOWN_GUID}`);
    expectStatus(r, [404, 400], "GET /jobs/{unknown}");
    const j = r.json || {};
    if (!j.message) throw new Error("no debug message present");
    if (!j.errorCode || !j.error?.code) throw new Error("no machine code present");
    return { actualApi: `errorCode=${j.errorCode}`, notes: "message(debug) + code both present; FE i18n mapping is UI-side" };
  });

  await testCase("UAT-ERR-003", "error.type derived from HTTP status (400/401/403/404)", "P2", async () => {
    const cand = (await login("nhatquang")).accessToken;
    // 400 has two code paths: app-validation (custom envelope) vs model-binding (raw ProblemDetails, no
    // error.type). forgot-password reaches the app validator and yields the custom envelope.
    const r400 = await api("POST", "/auth/candidate/forgot-password", { body: {} });
    const r401 = await api("GET", "/hr/applications");
    const r403 = await api("GET", "/hr/applications", { token: cand });
    const r404 = await api("GET", `/jobs/${UNKNOWN_GUID}`);
    const got = {
      "400": r400.json?.error?.type ?? "(problemDetails)",
      "401": r401.json?.error?.type,
      "403": r403.json?.error?.type,
      "404": r404.json?.error?.type,
    };
    // 401/403/404 mapping is the stable contract; assert those. 400.type is path-dependent — record it.
    const ok = got["401"] === "AUTH_ERROR" && got["403"] === "FORBIDDEN" && got["404"] === "NOT_FOUND";
    if (!ok) throw new Error(`type mapping mismatch: ${JSON.stringify(got)}`);
    return { actualApi: JSON.stringify(got), notes: `400.type path-dependent (app-validation=${got["400"]}); model-binding 400s emit raw ProblemDetails — see FINDING-CONTRACT-01` };
  });

  await testCase("UAT-ERR-004", "Multiple field errors incl. whitespace-only + nested camelCase mapping", "P2", async () => {
    // Same contract gap as ERR-001: register JSON → 415 (multipart-only); no camelCase fieldErrors array.
    const r = await api("POST", "/candidates/register", {
      body: { username: "ab", fullName: "   ", email: "notanemail", password: "Pw@1", phone: "123" },
    });
    if (r.json?.errorCode === "VALIDATION_FAILED" && (r.json?.error?.fieldErrors || []).length >= 2) {
      return { actualApi: "register → VALIDATION_FAILED + fieldErrors" };
    }
    return { result: "FAIL", actualApi: `register JSON→${r.status}`, notes: "FINDING-CONTRACT-01 (Low): register is multipart-only (JSON→415) + model-binding validation → ProblemDetails, not VALIDATION_FAILED camelCase envelope" };
  });

  await testCase("UAT-ERR-005", "500 carries traceId, no stack/secret", "P0", async () => {
    return { result: "N/A", notes: "deferred: 500 not reproducible on prod (no 500s across business flows); traceId presence covered by UAT-ERR-013" };
  });

  await testCase("UAT-ERR-006", "Network error → toast (not inline)", "P1", async () => {
    return { result: "N/A", notes: "UI-only: NETWORK_ERROR is generated client-side; no server response to assert" };
  });

  await testCase("UAT-ERR-007", "Timeout → toast", "P2", async () => {
    return { result: "N/A", notes: "UI-only: TIMEOUT_ERROR (client abort); not server-observable" };
  });

  await testCase("UAT-ERR-008", "Unknown error code falls back to UNEXPECTED_ERROR", "P3", async () => {
    return { result: "N/A", notes: "UI-only: FE getErrorMessage fallback; server never emits an off-contract code" };
  });

  await testCase("UAT-ERR-009", "i18n error copy exists in both vi and en", "P3", async () => {
    return { result: "N/A", notes: "UI-only: FE i18n dictionaries (errors.*); backend returns codes only" };
  });

  await testCase("UAT-ERR-010", "Server validation reachable via API independent of UI submit-disable", "P3", async () => {
    // Server validation IS reachable API-direct (the point of the case): forgot-password reaches the app
    // validator and rejects an empty body with the custom envelope. (register path → FINDING-CONTRACT-01.)
    const r = await api("POST", "/auth/candidate/forgot-password", { body: {} });
    const s = expectStatus(r, 400, "POST /auth/candidate/forgot-password (direct)");
    if (!r.json?.errorCode) throw new Error("no errorCode on server validation response");
    return { actualApi: `${s}`, notes: `server validation reachable API-direct (errorCode=${r.json.errorCode}); double-submit guard is UI-only` };
  });

  await testCase("UAT-ERR-011", "409 vs 422 semantics correct", "P1", async () => {
    return { result: "N/A", notes: "deferred: stateful (write-phase) — needs a live apply to force 409 duplicate-active / 422 precondition" };
  });

  await testCase("UAT-ERR-012", "401 vs 403 semantics correct", "P1", async () => {
    const cand = (await login("nhatquang")).accessToken;
    const r401 = await api("GET", "/hr/applications");
    expectStatus(r401, 401, "GET /hr/applications (no token)");
    expectErrorCode(r401, "UNAUTHENTICATED");
    const r403 = await api("GET", "/hr/applications", { token: cand });
    expectStatus(r403, 403, "GET /hr/applications (candidate)");
    expectErrorCode(r403, "FORBIDDEN");
    return { actualApi: "401 UNAUTHENTICATED; 403 FORBIDDEN" };
  });

  await testCase("UAT-ERR-013", "traceId present and unique per error", "P3", async () => {
    const a = await api("GET", `/jobs/${UNKNOWN_GUID}`);
    const b = await api("GET", `/jobs/${UNKNOWN_GUID}`);
    const ta = a.json?.error?.traceId, tb = b.json?.error?.traceId;
    if (!ta || !tb) throw new Error(`missing traceId (${ta} / ${tb})`);
    if (ta === tb) throw new Error(`traceIds not unique: ${ta}`);
    return { actualApi: `traceA≠traceB`, actualData: `${ta} | ${tb}` };
  });

  await testCase("UAT-ERR-014", "FE and BE validation agree (salary max<min rejected by BE)", "P2", async () => {
    const hr = (await login("thucuyen")).accessToken;
    // Salary-invalid body fails validation → nothing created (non-destructive).
    const r = await api("POST", "/hr/jobs", { token: hr, body: { title: "[UAT] err-014", salaryMin: 40000000, salaryMax: 20000000 } });
    const s = expectStatus(r, [400, 422], "POST /hr/jobs (salary max<min)");
    const blob = JSON.stringify(r.json?.error || {}).toLowerCase() + (r.json?.errorCode || "").toLowerCase();
    if (!/salary/.test(blob)) throw new Error(`no salary-range signal in error: ${r.json?.errorCode}`);
    return { actualApi: s, notes: "BE rejects salary max<min; matches FE salary_check rule" };
  });
}
