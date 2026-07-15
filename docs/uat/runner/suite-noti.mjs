import { api, login, suite, testCase, expectStatus, expectErrorCode } from "./uat-lib.mjs";

// Suite NOTI — Notifications (UAT_TEST_CASES.md §10, cases 001–018).
// Notification endpoints are [Authorize]-only (any logged-in user); nhatquang has seed notifications.
// SEEN (bell open) != READ (item click). Stateful writes (seen/read/read-all) are deferred to the
// write-phase per encoding rules — this suite only exercises reads + auth/scoping negatives.
export async function runNoti() {
  suite("NOTI");

  await testCase("UAT-NOTI-001", "List notifications (paged) with isSeen/isRead/data", "P1", async () => {
    const t = await login("nhatquang");
    const r = await api("GET", "/notifications?page=1&pageSize=20", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /notifications?page=1&pageSize=20");
    const items = r.json?.data?.items;
    if (!Array.isArray(items)) throw new Error(`expected data.items array, got ${JSON.stringify(r.json?.data)?.slice(0,120)}`);
    if (!r.json?.data?.meta) throw new Error("missing pagination meta");
    // seed row shape carries isSeen/isRead/data/createdAt
    if (items.length) {
      const k = items[0];
      for (const f of ["isSeen", "isRead", "createdAt"]) if (!(f in k)) throw new Error(`item missing ${f}`);
    }
    return { actualApi: s, actualData: `items=${items.length}, total=${r.json?.data?.meta?.totalItems}` };
  });

  await testCase("UAT-NOTI-002", "Counts: unseen vs unread", "P1", async () => {
    const t = await login("nhatquang");
    const r = await api("GET", "/notifications/counts", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /notifications/counts");
    const d = r.json?.data || {};
    if (typeof d.unseen !== "number" || typeof d.unread !== "number") throw new Error(`expected {unseen,unread}, got ${JSON.stringify(d)}`);
    return { actualApi: s, actualData: `unseen=${d.unseen}, unread=${d.unread}` };
  });

  await testCase("UAT-NOTI-003", "Opening bell marks all SEEN (not read)", "P0", async () => {
    // POST /notifications/seen mutates is_seen/seen_at for the caller's rows.
    return { result: "N/A", actualApi: "POST /notifications/seen", notes: "deferred: stateful write (write-phase)" };
  });

  await testCase("UAT-NOTI-004", "Clicking an item marks READ + navigates to deep link", "P0", async () => {
    // POST/PATCH /notifications/{id}/read mutates is_read/is_seen on a real notification; nav is UI.
    return { result: "N/A", actualApi: "PATCH /notifications/{id}/read", notes: "deferred: stateful write (write-phase)" };
  });

  await testCase("UAT-NOTI-005", "Mark all as read", "P2", async () => {
    return { result: "N/A", actualApi: "POST /notifications/read-all", notes: "deferred: stateful write (write-phase)" };
  });

  await testCase("UAT-NOTI-006", "Legacy unread-count endpoint", "P3", async () => {
    const t = await login("nhatquang");
    const r = await api("GET", "/notifications/unread-count", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /notifications/unread-count");
    if (typeof r.json?.data?.unreadCount !== "number") throw new Error(`expected {unreadCount}, got ${JSON.stringify(r.json?.data)}`);
    return { actualApi: s, actualData: `unreadCount=${r.json.data.unreadCount}` };
  });

  await testCase("UAT-NOTI-007", "SSE realtime delivery (new notification appears without refresh)", "P1", async () => {
    // GET /notifications/stream is a long-lived text/event-stream; needs an SSE client + a triggered
    // event — not exercisable by the fetch-read runner.
    return { result: "N/A", actualApi: "GET /notifications/stream", notes: "deferred: SSE stream + event trigger (write-phase / manual)" };
  });

  await testCase("UAT-NOTI-008", "Deep links are role-aware and click-ready (no /api/ urls)", "P1", async () => {
    const t = await login("nhatquang");
    const r = await api("GET", "/notifications?page=1&pageSize=50", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /notifications");
    const items = r.json?.data?.items || [];
    const blob = JSON.stringify(items);
    // no deep-link url may point at the API surface (must be a frontend route)
    if (/"url"\s*:\s*"\/api\//.test(blob) || /"url"\s*:\s*"https?:\/\/[^"]*\/api\//.test(blob)) {
      throw new Error("a notification data.url points at /api/ (not a frontend route)");
    }
    return { actualApi: s, actualData: `inspected ${items.length} items; no /api/ deep links` };
  });

  await testCase("UAT-NOTI-009", "SSE is user-scoped (no cross-user leakage)", "P0", async () => {
    return { result: "N/A", actualApi: "GET /notifications/stream", notes: "deferred: two concurrent SSE clients (write-phase / manual)" };
  });

  await testCase("UAT-NOTI-010", "Re-sync on reconnect recovers missed events", "P2", async () => {
    return { result: "N/A", actualApi: "GET /notifications/stream", notes: "deferred: SSE disconnect/reconnect recovery (manual)" };
  });

  await testCase("UAT-NOTI-011", "Notification content carries no sensitive data", "P2", async () => {
    const t = await login("nhatquang");
    const r = await api("GET", "/notifications?page=1&pageSize=50", { token: t.accessToken });
    const s = expectStatus(r, 200, "GET /notifications");
    const blob = JSON.stringify(r.json?.data?.items || []).toLowerCase();
    const leaks = ["password", "apikey", "api_key", "secret", "bearer ", "accesstoken", "refreshtoken", "gen_salt"];
    const hit = leaks.find((k) => blob.includes(k));
    if (hit) throw new Error(`notification payload contains sensitive token: "${hit}"`);
    return { actualApi: s, notes: "read-only heuristic scan (partial per spec)" };
  });

  await testCase("UAT-NOTI-012", "Require auth; other user's notifications inaccessible", "P1", async () => {
    // (a) guest → 401
    const guest = await api("GET", "/notifications");
    const s1 = expectStatus(guest, 401, "GET /notifications (guest)");
    // (b) cross-user: haidang tries to mark one of nhatquang's notification ids read → 404 (scoped, no mutation).
    const nq = await login("nhatquang");
    const list = await api("GET", "/notifications?page=1&pageSize=1", { token: nq.accessToken });
    const otherId = list.json?.data?.items?.[0]?.id || "11111111-1111-4111-8111-111111111111";
    const hd = await login("haidang");
    const cross = await api("POST", `/notifications/${otherId}/read`, { token: hd.accessToken });
    const s2 = expectStatus(cross, [404, 403], `POST /notifications/{nq-id}/read (as haidang)`);
    if (cross.status === 404) expectErrorCode(cross, "NOTIFICATION_NOT_FOUND");
    return { actualApi: `${s1}; ${s2}`, notes: "no mutation: cross-user read rejected by scoping" };
  });

  await testCase("UAT-NOTI-013", "Notification publish failure never breaks business action", "P1", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "requires forcing publish failure during a status change (local fault-injection)" };
  });

  await testCase("UAT-NOTI-014", "Race on unread count (multi-tab)", "P3", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "concurrency/UI: two tabs, manual" };
  });

  await testCase("UAT-NOTI-015", "Empty & error states", "P3", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "UI-only (empty/error rendering)" };
  });

  await testCase("UAT-NOTI-016", "Event-per-transition correctness", "P1", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "deferred: large integration sweep driving every state transition (write-phase)" };
  });

  await testCase("UAT-NOTI-017", "Legacy notification_events vs emitted codes (data gap)", "P3", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "data-gap analysis (Q-NOTI-01); no read API for the lookup table" };
  });

  await testCase("UAT-NOTI-018", "System toast card (bottom-right, VI, not default)", "P3", async () => {
    return { result: "N/A", actualApi: "n/a", notes: "UI-only (toast card rendering)" };
  });
}
