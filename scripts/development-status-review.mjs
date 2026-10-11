// Targeted isolated review: administrative development transitions and student permissions.
// It accepts only a disposable fixture emitted by the functional suite.
import { spawn } from "node:child_process";
import { mkdtemp, mkdir, writeFile, cp } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import net from "node:net";

const root = process.cwd();
const fixture = process.argv[2] ? path.resolve(process.argv[2]) : "";
const allowedFixtureRoot = path.join(tmpdir(), "PortalProyectosFunctionalTests") + path.sep;
if (!fixture.startsWith(allowedFixtureRoot)) throw new Error("Se requiere exclusivamente un fixture funcional desechable.");
const scratch = await mkdtemp(path.join(tmpdir(), "PortalFinalAdjustments-"));
const fixtureCopy = path.join(scratch, "fixture");
await cp(fixture, fixtureCopy, { recursive: true });
const output = path.join(root, "artifacts", "development-status-final");
await mkdir(output, { recursive: true });
const freePort = () => new Promise((resolve, reject) => {
  const server = net.createServer(); server.on("error", reject);
  server.listen(0, "127.0.0.1", () => { const port = server.address().port; server.close(() => resolve(port)); });
});
const appPort = await freePort(), debugPort = await freePort(), base = `http://127.0.0.1:${appPort}`;
const children = [], results = [], errors = [], pending = new Map();
let ws, nextId = 0, acceptDialog = true, dialogs = 0;
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++nextId, timer = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 15000);
  pending.set(id, { resolve, reject, timer }); ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expression => {
  const response = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (response.exceptionDetails) throw new Error(response.exceptionDetails.text);
  return response.result.value;
};
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(action, timeout = 30000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { try { const value = await action(); if (value) return value; } catch {} await wait(200); }
  throw new Error("Timeout esperando aplicación/navegador aislado.");
}
function record(name, passed, detail = "") {
  results.push({ name, passed: Boolean(passed), detail }); console.log(`${passed ? "PASS" : "FAIL"} ${name}${detail ? ` ${detail}` : ""}`);
}
async function navigate(route) {
  await send("Page.navigate", { url: base + route });
  await until(() => evaluate(`document.readyState==="complete"&&location.pathname===${JSON.stringify(route.split("?")[0])}`));
  await wait(250);
}
async function viewport(width, height) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: width < 768 });
  await send("Emulation.setTouchEmulationEnabled", { enabled: width < 768, configuration: "mobile" });
}
async function screenshot(name) {
  const data = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
  await writeFile(path.join(output, name), Buffer.from(data.data, "base64"));
}
async function login(user, password) {
  // Stop the previous authenticated page (including SignalR/recent requests)
  // before clearing its cookies, so late responses cannot renew that session.
  await send("Page.navigate", {url:"about:blank"});
  await until(()=>evaluate('location.href==="about:blank"&&document.readyState==="complete"'));
  await send("Network.clearBrowserCookies");
  await navigate("/cuenta/iniciar-sesion");
  await evaluate(`(()=>{document.querySelector("#Identifier").value=${JSON.stringify(user)};document.querySelector("#Password").value=${JSON.stringify(password)};document.querySelector("#Identifier").form.requestSubmit()})()`);
  await until(() => evaluate(`location.pathname!=="/cuenta/iniciar-sesion"`));
}

try {
  const appRoot = path.join(root, "artifacts/publish-development-final/PortalProyectos-win-x64/app");
  const app = spawn(path.join(appRoot, "PortalProyectos.Web.exe"), ["--contentRoot", appRoot], {
    cwd: scratch, windowsHide: true,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development", ASPNETCORE_URLS: base, Server__Urls: base,
      Data__DatabasePath: path.join(fixtureCopy, "functional-tests.db"), Storage__RootPath: path.join(fixtureCopy, "storage"),
      DataProtection__KeysPath: path.join(scratch, "keys"), Backups__RootPath: path.join(scratch, "backups"), Portal__UseHttpsRedirection: "false" }
  });
  children.push(app);
  await until(async () => (await fetch(base)).ok);
  const browser = spawn("C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", [
    "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
    `--remote-debugging-port=${debugPort}`, "--remote-allow-origins=*", `--user-data-dir=${path.join(scratch,"edge")}`, "--disable-background-networking", "about:blank"
  ], { windowsHide: true, stdio: "ignore" });
  children.push(browser);
  const page = await until(async () => (await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json()).find(x => x.type === "page"));
  ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{ ws.addEventListener("open",resolve,{once:true}); ws.addEventListener("error",reject,{once:true}); });
  ws.addEventListener("message", async event => {
    const raw = event.data instanceof Blob ? await event.data.text() : event.data.toString();
    const message=JSON.parse(raw);
    if(message.id&&pending.has(message.id)){const p=pending.get(message.id);clearTimeout(p.timer);pending.delete(message.id);message.error?p.reject(new Error(JSON.stringify(message.error))):p.resolve(message.result);}
    if(message.method==="Page.javascriptDialogOpening"){dialogs++; await send("Page.handleJavaScriptDialog",{accept:acceptDialog});}
    if(message.method==="Runtime.exceptionThrown") errors.push(message.params.exceptionDetails.text);
  });
  ws.addEventListener("close", event => console.log(`CDP websocket cerrado ${event.code} ${event.reason}`));
  ws.addEventListener("error", () => console.log("CDP websocket error"));
  await send("Page.enable"); await send("Runtime.enable"); await send("Network.enable");
  await send("Network.setBlockedURLs", { urls: ["https://*"] });


  const overflow = () => evaluate("document.documentElement.scrollWidth<=innerWidth");
  for(const [w,h] of [[1366,768],[390,844]]){
    // The real authentication limiter permits ten requests/minute. Keep both
    // viewport rounds independent without disabling production security rules.
    if(w===390){console.log("Esperando ventana del limitador de autenticación aislado..."); await wait(35000); await wait(30000);}
    await viewport(w,h);
    await login("ADMIN001","Functional!2026");
    await navigate("/Admin/Projects/Details/1");
    record(`Admin Terminar visible ${w}`,await evaluate('!!document.querySelector("form[action*=Finish]")&&!document.querySelector("form[action*=Reopen]")') && await overflow());
    await screenshot(`admin-project-in-progress-${w}.png`);
    if(w===390){
      await evaluate('document.documentElement.style.scrollBehavior="auto";document.querySelector("form[action*=Finish]").scrollIntoView({block:"center"})');
      await wait(200); await screenshot("admin-finish-action-390.png");
    }
    acceptDialog=false;
    await evaluate('document.querySelector("form[action*=Finish]").requestSubmit()');
    await wait(300);
    record(`Cancelar confirmación ${w}`,await evaluate('!!document.querySelector("form[action*=Finish]")&&!document.querySelector("form[action*=Finish] button").disabled'));
    acceptDialog=true;
    await evaluate('document.querySelector("form[action*=Finish]").requestSubmit()');
    await until(()=>evaluate('!!document.querySelector("form[action*=Reopen]")'));
    record(`Admin Reabrir visible ${w}`,await evaluate('document.body.innerText.includes("Terminado")&&document.body.innerText.includes("Aprobado")&&!document.querySelector("form[action*=Finish]")') && await overflow());
    await screenshot(`admin-project-completed-${w}.png`);
    if(w===390){
      await evaluate('document.documentElement.style.scrollBehavior="auto";document.querySelector("form[action*=Reopen]").scrollIntoView({block:"center"})');
      await wait(200); await screenshot("admin-reopen-action-390.png");
    }
    await evaluate('document.querySelector("form[action*=Reopen]").requestSubmit()');
    await until(()=>evaluate('!!document.querySelector("form[action*=Finish]")'));

    await login("OWNER001","Functional!2026");
    await navigate("/Student/Projects/Details/1");
    record(`Alumno aprobado sin edición académica ${w}`,await evaluate('!document.querySelector("[name=DevelopmentStatus]")&&!document.querySelector("form[action*=StartDevelopmentStatusUpdate]")&&!document.querySelector("a[href*=Edit]")&&!!document.querySelector("[data-participant-search]")')&&await overflow());
    await screenshot(`student-approved-detail-${w}.png`);
    await evaluate('document.getElementById("participantes").querySelector("button").click()');
    await wait(400);
    record(`Equipo aprobado modal accesible ${w}`,await evaluate('document.querySelector("#participantModal").classList.contains("show")')&&await overflow());
    await screenshot(`student-approved-team-${w}.png`);
    await evaluate('document.querySelector("#participantModal [data-bs-dismiss]").click()');
    await wait(400);
    await navigate("/Student/Projects");
    await evaluate('document.querySelector("form[action*=Create]").requestSubmit()');
    await until(()=>evaluate('location.pathname.toLowerCase().includes("/student/projects/edit/")'));
    const editPath=await evaluate("location.pathname");
    record(`Crear sin selector desarrollo ${w}`,await evaluate('!!document.querySelector("[data-project-form]")&&!document.querySelector("[name=DevelopmentStatus]")')&&await overflow());
    await screenshot(`student-create-${w}.png`);
    const posted=await evaluate(`(async()=>{
      const form=document.querySelector("[data-project-form]"),data=new FormData(form);
      data.set("DevelopmentStatus","Completed");
      const response=await fetch(form.action,{method:"POST",body:data});
      return {ok:response.ok,url:response.url};
    })()`);
    record(`POST manipulado no habilita selector ${w}`,posted.ok);
    await navigate(editPath);
    const id=editPath.split("/").pop();
    await navigate("/Student/Projects/Details/"+id);
    record(`Borrador no presenta Terminado ${w}`,await evaluate('document.body.innerText.includes("Disponible al aprobar")&&!document.querySelector("[name=DevelopmentStatus]")'));
    await navigate("/Student/Projects");
    await evaluate(`(async()=>{const form=[...document.querySelectorAll('form[action*="Delete"]')].find(f=>f.action.endsWith("/"+${JSON.stringify(id)}));if(form)await fetch(form.action,{method:"POST",body:new FormData(form)});})()`);
    await login("LINK001","Functional!2026");
    await navigate("/Student/Projects/Details/1");
    record(`Participante solo consulta ${w}`,await evaluate('!document.querySelector("[data-participant-search]")&&!document.querySelector("[name=DevelopmentStatus]")&&!document.querySelector("form[action*=UploadDocument]")')&&await overflow());
    const denied=await evaluate(`(async()=>{
      const token=document.querySelector('input[name="__RequestVerificationToken"]')?.value;
      const data=new URLSearchParams({id:"1",__RequestVerificationToken:token||""});
      const response=await fetch("/Admin/Projects/Finish",{method:"POST",body:data});
      return response.status===403||response.status===400||response.url.includes("acceso-denegado");
    })()`);
    record(`Participante no accede a acción administrativa ${w}`,denied);
  }
  record("Confirmaciones reales de navegador",dialogs===6,String(dialogs));
  record("Errores JavaScript del navegador", errors.length===0, errors.join("; "));
} catch(error) {
  try { console.error(await evaluate('JSON.stringify({url:location.href,text:document.body.innerText.slice(0,1000)})')); await screenshot("failure.png"); } catch {}
  console.error(error.stack||error); results.push({name:"Ejecución Edge aislada",passed:false,detail:String(error)});
} finally {
  if(ws?.readyState===WebSocket.OPEN){try{await send("Browser.close");}catch{} ws.close();}
  for(const child of children) if(child.exitCode===null) child.kill();
  await writeFile(path.join(output,"results.json"),JSON.stringify({results,errors,productionTouched:false,scratch},null,2));
}
if(results.some(x=>!x.passed)) process.exitCode=1;
