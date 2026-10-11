// UI review of account status actions, using only a disposable functional-test fixture.
import { spawn } from "node:child_process";
import { cp, mkdir, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import net from "node:net";

const root = process.cwd();
const fixture = process.argv[2] ? path.resolve(process.argv[2]) : "";
const fixtureRoot = path.join(tmpdir(), "PortalProyectosFunctionalTests") + path.sep;
if (!fixture.startsWith(fixtureRoot)) throw new Error("Solo se admite el fixture funcional aislado.");
const output = path.join(root, "artifacts", "account-lifecycle-review");
await mkdir(output, { recursive: true });
const scratch = await import("node:fs/promises").then(m => m.mkdtemp(path.join(tmpdir(), "PortalAccountsUI-")));
const fixtureCopy = path.join(scratch, "fixture");
await cp(fixture, fixtureCopy, { recursive: true });
const freePort = () => new Promise((resolve, reject) => {
  const s = net.createServer(); s.on("error", reject); s.listen(0, "127.0.0.1", () => { const p = s.address().port; s.close(() => resolve(p)); });
});
const appPort = await freePort(), debugPort = await freePort(), base = `http://127.0.0.1:${appPort}`;
const children = [], checks = [], pending = new Map(); let ws, seq = 0;
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++seq, timer = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 15000);
  pending.set(id, { resolve, reject, timer }); ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expression => {
  const r = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.text); return r.result.value;
};
const wait = ms => new Promise(r => setTimeout(r, ms));
async function until(fn, timeout = 30000) { const end = Date.now() + timeout; while (Date.now() < end) { try { const v = await fn(); if (v) return v; } catch {} await wait(200); } throw new Error("Timeout al iniciar fixture/navegador."); }
function record(name, ok, detail = "") { checks.push({ name, ok: !!ok, detail }); console.log(`${ok ? "PASS" : "FAIL"} ${name}${detail ? ` ${detail}` : ""}`); }
async function navigate(route) { await send("Page.navigate", { url: base + route }); await until(() => evaluate(`document.readyState==="complete"&&location.pathname===${JSON.stringify(route.split("?")[0])}`)); await wait(180); }
async function viewport(width, height) { await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: width < 768 }); await send("Emulation.setTouchEmulationEnabled", { enabled: width < 768, configuration: "mobile" }); }
async function screenshot(name) { const r = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false }); await writeFile(path.join(output, name), Buffer.from(r.data, "base64")); }
async function login() {
  await navigate("/cuenta/iniciar-sesion");
  await evaluate(`(()=>{document.querySelector("#Identifier").value="SUPERDEV001";document.querySelector("#Password").value="Functional!2026";document.querySelector("#Identifier").form.requestSubmit()})()`);
  await until(() => evaluate(`location.pathname!=="/cuenta/iniciar-sesion"`));
}
async function checkTable(route, width, height, screenshotName, cases, statusOptions, pageName) {
  await viewport(width, height); await navigate(route);
  const state = await evaluate(`(()=>({scrollWidth:document.documentElement.scrollWidth,innerWidth,options:[...document.querySelector('select[name=status]')?.options||[]].map(x=>x.textContent.trim()),rows:[...document.querySelectorAll('tbody tr')].map(tr=>({text:tr.innerText,actions:[...tr.querySelectorAll('button')].map(b=>b.innerText.trim()),forms:[...tr.querySelectorAll('form')].map(f=>f.getAttribute('action')||f.querySelector('button')?.innerText.trim()),status:tr.querySelector('.status-badge')?.innerText.trim()}))}))()`);
  record(`${pageName} ${width}x${height}: sin overflow global`, state.scrollWidth <= width + 1, JSON.stringify({ scrollWidth: state.scrollWidth, innerWidth: state.innerWidth }));
  record(`${pageName} ${width}x${height}: filtros`, statusOptions.every(x => state.options.includes(x)), JSON.stringify(state.options));
  for (const c of cases) {
    const row = state.rows.find(r => c.match(r.text) && (!c.status || r.status === c.status));
    const actual = row?.actions ?? [];
    record(`${pageName} ${width}: acciones ${c.name}`, !!row && c.must.every(x => actual.includes(x)) && c.mustNot.every(x => !actual.includes(x)), JSON.stringify({ status: row?.status, actions: actual }));
    if (c.confirmation) {
      const has = await evaluate(`(()=>[...document.querySelectorAll('[data-confirm],[data-description]')].some(x=>(${JSON.stringify(c.confirmation)}).toLowerCase().split(/\\s+/).every(term=>(x.getAttribute('data-confirm')||x.getAttribute('data-description')||'').toLowerCase().includes(term))))()`);
      record(`${pageName} ${width}: confirmación ${c.name}`, has);
    }
  }
  await screenshot(screenshotName);
}

try {
  const app = spawn("dotnet", [path.join(root, "src/PortalProyectos.Web/bin/Debug/net10.0/PortalProyectos.Web.dll"), "--contentRoot", path.join(root, "src/PortalProyectos.Web")], {
    cwd: root, windowsHide: true, stdio: "ignore",
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development", ASPNETCORE_URLS: base, Server__Urls: base,
      Data__DatabasePath: path.join(fixtureCopy, "functional-tests.db"), Storage__RootPath: path.join(fixtureCopy, "storage"),
      DataProtection__KeysPath: path.join(scratch, "keys"), Backups__RootPath: path.join(scratch, "backups"), Portal__UseHttpsRedirection: "false" }
  });
  children.push(app); await until(async () => (await fetch(base)).ok);
  const edge = spawn("C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", ["--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", `--remote-debugging-port=${debugPort}`, "--remote-allow-origins=*", `--user-data-dir=${path.join(scratch,"edge")}`, "--disable-background-networking", "about:blank"], { windowsHide: true, stdio: "ignore" });
  children.push(edge);
  const page = await until(async () => (await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json()).find(x => x.type === "page"));
  console.log(`EDGE_CDP ${page.webSocketDebuggerUrl}`);
  ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { ws.addEventListener("open", resolve, { once: true }); ws.addEventListener("error", reject, { once: true }); });
  ws.addEventListener("message", async event => { try { const raw = event.data instanceof Blob ? await event.data.text() : event.data.toString(); const m = JSON.parse(raw); if (m.id && pending.has(m.id)) { const p = pending.get(m.id); clearTimeout(p.timer); pending.delete(m.id); m.error ? p.reject(new Error(JSON.stringify(m.error))) : p.resolve(m.result); } } catch (e) { console.error("CDP message parse", e); } });
  ws.addEventListener("close", e => console.log(`CDP close ${e.code} ${e.reason}`));
  ws.addEventListener("error", e => console.log("CDP socket error", e.message || ""));
  await send("Page.enable"); await send("Runtime.enable"); await send("Network.enable"); await send("Network.setBlockedURLs", { urls: ["https://*"] });
  await login();

  const userOptions = ["Todos los estados", "Pendiente", "Aprobada", "Bloqueada", "Eliminada"];
  const adminOptions = ["Todos", "Activo", "Bloqueado", "Eliminado"];
  for (const [width,height] of [[1366,768],[390,844]]) {
    await checkTable("/Admin/Users?search=FLOW", width,height,`users-${width}x${height}.png`, [
      { name:"Pendiente", match:t=>t.includes("Flujo alumno FLOWSTUDENT"), status:"Pendiente", must:["Aprobar","Rechazar"], mustNot:["Bloquear","Desbloquear","Eliminar","Restablecer contraseña"], confirmation:"rechazar esta solicitud" },
      { name:"Aprobada", match:t=>t.includes("Flujo alumno FLOWUIAPPROVED"), status:"Aprobada", must:["Restablecer contraseña","Bloquear","Eliminar"], mustNot:["Aprobar","Rechazar","Desbloquear"], confirmation:"bloquear esta cuenta" },
      { name:"Bloqueada", match:t=>t.includes("Flujo alumno FLOWUIBLOCK"), status:"Bloqueada", must:["Desbloquear","Eliminar"], mustNot:["Aprobar","Rechazar","Bloquear","Restablecer contraseña"], confirmation:"desbloquear esta cuenta" }
    ],userOptions,"Usuarios");
    await checkTable("/Admin/Users?status=Deactivated&search=FLOWSTUDENT",width,height,`users-deleted-${width}x${height}.png`,[
      { name:"Eliminada", match:t=>t.includes("Flujo alumno FLOWSTUDENT"), status:"Eliminada", must:[], mustNot:["Aprobar","Rechazar","Bloquear","Desbloquear","Eliminar","Restablecer contraseña"] }
    ],userOptions,"Usuarios eliminados");
    await checkTable("/Admin/Administrators?search=FLOWUI",width,height,`administrators-${width}x${height}.png`,[
      { name:"Activa",match:t=>t.includes("Flujo administrador FLOWUIACTIVE"),status:"Activo",must:["Bloquear","Eliminar","Restablecer contraseña"],mustNot:["Aprobar","Rechazar","Desbloquear"],confirmation:"bloquear esta cuenta" },
      { name:"Bloqueada",match:t=>t.includes("Flujo administrador FLOWUIBLOCKED"),status:"Bloqueado",must:["Desbloquear","Eliminar","Restablecer contraseña"],mustNot:["Aprobar","Rechazar","Bloquear"],confirmation:"desbloquear esta cuenta" }
    ],adminOptions,"Administradores");
    await checkTable("/Admin/Administrators?status=Deactivated&search=FLOWADMIN",width,height,`administrators-deleted-${width}x${height}.png`,[
      { name:"Eliminada",match:t=>t.includes("Flujo administrador FLOWADMIN"),status:"Eliminado",must:[],mustNot:["Aprobar","Rechazar","Bloquear","Desbloquear","Eliminar","Restablecer contraseña"] }
    ],adminOptions,"Administradores eliminados");
  }
  const failures = checks.filter(x => !x.ok);
  console.log(`EDGE_RESULT PASS=${checks.length-failures.length} FAIL=${failures.length}`);
  if (failures.length) process.exitCode = 1;
} finally {
  try { ws?.close(); } catch {}
  for (const child of children.reverse()) { try { child.kill(); } catch {} }
}
