// Targeted Edge review for the final hero, development-status and audit-touch changes.
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
const output = path.join(root, "artifacts", "final-adjustments");
await mkdir(output, { recursive: true });
const freePort = () => new Promise((resolve, reject) => {
  const server = net.createServer(); server.on("error", reject);
  server.listen(0, "127.0.0.1", () => { const port = server.address().port; server.close(() => resolve(port)); });
});
const appPort = await freePort(), debugPort = await freePort(), base = `http://127.0.0.1:${appPort}`;
const children = [], results = [], errors = [], pending = new Map();
let ws, nextId = 0;
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
  await send("Network.clearBrowserCookies");
  await navigate("/cuenta/iniciar-sesion");
  await evaluate(`(()=>{document.querySelector("#Identifier").value=${JSON.stringify(user)};document.querySelector("#Password").value=${JSON.stringify(password)};document.querySelector("#Identifier").form.requestSubmit()})()`);
  await until(() => evaluate(`location.pathname!=="/cuenta/iniciar-sesion"`));
}
async function touchScrollOnCard(pathname, selector, width, height) {
  await viewport(width, height); await navigate(pathname);
  await evaluate(`(()=>{
    const card=document.querySelector(${JSON.stringify(selector)});
    if(!card)return false;
    document.documentElement.style.scrollBehavior="auto"; window.scrollTo(0,0);
    const top=card.getBoundingClientRect().top+window.scrollY;
    window.scrollTo(0,Math.max(0,top+card.getBoundingClientRect().height/2-innerHeight/2)); return true;
  })()`);
  await wait(250);
  const positioned = await evaluate(`(()=>{
    const card=document.querySelector(${JSON.stringify(selector)});
    if(!card)return {ok:false,why:"card absent",scrollY:window.scrollY};
    const r=card.getBoundingClientRect();
    return {ok:r.top>=0&&r.bottom<=innerHeight,x:Math.round(r.left+r.width/2),y:Math.round(r.top+r.height/2),scrollY:window.scrollY,scrollHeight:document.documentElement.scrollHeight,innerHeight,cardHeight:Math.round(r.height)};
  })()`);
  if (!positioned.ok) { record(`Touch scroll ${pathname} ${width}`, false, JSON.stringify(positioned)); return; }
  await wait(200);
  const before = await evaluate("window.scrollY");
  await send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x: positioned.x, y: positioned.y, id: 1 }] });
  for (let i=1;i<=7;i++) { await send("Input.dispatchTouchEvent", { type: "touchMove", touchPoints: [{ x: positioned.x, y: positioned.y-30*i, id: 1 }] }); await wait(35); }
  await send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] }); await wait(450);
  const after = await evaluate("window.scrollY");
  record(`Touch scroll ${pathname} ${width}`, after > before + 10, JSON.stringify({ before, after, delta: after-before, card: positioned }));
}

try {
  const app = spawn("dotnet", [path.join(root, "src/PortalProyectos.Web/bin/Debug/net10.0/PortalProyectos.Web.dll"), "--contentRoot", path.join(root, "src/PortalProyectos.Web")], {
    cwd: root, windowsHide: true,
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
    if(message.method==="Runtime.exceptionThrown") errors.push(message.params.exceptionDetails.text);
  });
  ws.addEventListener("close", event => console.log(`CDP websocket cerrado ${event.code} ${event.reason}`));
  ws.addEventListener("error", () => console.log("CDP websocket error"));
  await send("Page.enable"); await send("Runtime.enable"); await send("Network.enable");
  await send("Network.setBlockedURLs", { urls: ["https://*"] });

  for (const [w,h,name] of [[1920,1080,"home-1920x1080.png"],[1440,900,"home-1440x900.png"],[1366,768,"home-1366x768.png"],[1280,800,"home-1280x800.png"],[1024,768,"home-1024x768.png"],[768,1024,"home-768x1024.png"],[430,932,"home-430x932.png"],[390,844,"home-390x844.png"],[360,800,"home-360x800.png"]]) {
    await viewport(w,h); await navigate("/"); await evaluate("window.scrollTo(0,0)"); await wait(150);
    const layout=await evaluate(`(()=>{const header=document.querySelector(".public-header"),hero=document.querySelector(".discovery-hero"),next=document.querySelector(".discovery-section");const a=header?.getBoundingClientRect(),b=hero?.getBoundingClientRect(),c=next?.getBoundingClientRect();return {width:innerWidth,headerBottom:Math.round(a?.bottom||0),heroHeight:Math.round(b?.height||0),heroBottom:Math.round(b?.bottom||0),nextTop:Math.round(c?.top||0),nextVisible:Math.max(0,innerHeight-(c?.top||innerHeight)),scrollWidth:document.documentElement.scrollWidth}})()`);
    record(`Home hero ${w}x${h} 100% zoom`, layout.scrollWidth<=w+1 && (w!==1366 || (layout.nextVisible>0&&layout.nextTop<768)), JSON.stringify(layout));
    await screenshot(name);
  }

  await login("ADMIN001", "Functional!2026");
  for (const width of [430,390,360]) {
    const height=width===430?932:width===390?844:800;
    await touchScrollOnCard("/Admin/AuditLog", ".audit-results-scroll tbody tr", width,height);
    await navigate("/Admin/AuditLog"); await screenshot(`audit-mobile-fixed-${width}.png`);
    if(width===390){
      await navigate("/Admin/Publications"); await screenshot("audit-mobile-before-or-reference.png");
      await touchScrollOnCard("/Admin/Publications", ".table-responsive tbody tr", 390, 844);
    }
  }

  await login("OWNER001", "Functional!2026");
  for(const [w,h,name] of [[1366,768,"project-edit-development-status-1366.png"],[390,844,"project-edit-development-status-390.png"]]){
    await viewport(w,h);
    if(w===1366){
      await navigate("/Student/Projects/Details/1");
      const isApproved=await evaluate('document.body.innerText.includes("Aprobado")&&!!document.querySelector("form[action*=StartDevelopmentStatusUpdate]")');
      record(`Estado de desarrollo visible para responsable ${w}`,isApproved);
      await evaluate('document.querySelector("form[action*=StartDevelopmentStatusUpdate]").requestSubmit()');
      await until(()=>evaluate('location.pathname.toLowerCase().includes("/student/projects/edit")'));
    } else await navigate("/Student/Projects/Edit/1");
    const editor=await evaluate('!!document.querySelector("select[name=DevelopmentStatus]")&&!document.querySelector("[data-project-form]")');
    record(`Editor acotado a estado ${w}`,editor);
    await screenshot(name);
  }

  record("Errores JavaScript del navegador", errors.length===0, errors.join("; "));
} catch(error) {
  console.error(error.stack||error); results.push({name:"Ejecución Edge aislada",passed:false,detail:String(error)});
} finally {
  if(ws?.readyState===WebSocket.OPEN){try{await send("Browser.close");}catch{} ws.close();}
  for(const child of children) if(child.exitCode===null) child.kill();
  await writeFile(path.join(output,"results.json"),JSON.stringify({results,errors,productionTouched:false,scratch},null,2));
}
if(results.some(x=>!x.passed)) process.exitCode=1;
