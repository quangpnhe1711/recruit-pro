import { api, login } from "./uat-lib.mjs";

// One-off: remove any leftover [UAT] jobs on staging.
const hr = (await login("thucuyen")).accessToken;
const list = await api("GET", "/hr/jobs?pageSize=200", { token: hr });
const items = list.json?.data?.items || [];
const uat = items.filter((x) => /\[UAT/.test(x.title || ""));
console.log(`total jobs: ${items.length} | [UAT] leftovers: ${uat.length}`);
for (const j of uat) {
  await api("PATCH", `/hr/jobs/${j.id}/status`, { token: hr, body: { status: "CLOSED" } });
  const del = await api("DELETE", `/hr/jobs/${j.id}`, { token: hr });
  console.log(`  ${j.title} (${j.id}) → delete ${del.status}`);
}
const after = await api("GET", "/hr/jobs?pageSize=200", { token: hr });
const left = (after.json?.data?.items || []).filter((x) => /\[UAT/.test(x.title || "")).length;
console.log(`remaining [UAT] jobs: ${left}`);
