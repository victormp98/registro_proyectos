// Isolated UI smoke review using the locally installed Edge and Node's CDP client.
// Never starts the published application or opens an existing database.
import { spawn } from "node:child_process";
import { mkdtemp, mkdir, writeFile, cp } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import net from "node:net";
import { randomBytes } from "node:crypto";

const root = process.cwd();
const scratch = await mkdtemp(path.join(tmpdir(), "PortalVisualReview-"));
const fixture = process.argv[2] ? path.resolve(process.argv[2]) : null;
if (fixture && !fixture.startsWith(path.join(tmpdir(), "PortalProyectosFunctionalTests") + path.sep))
  throw new Error("Only a disposable functional-test fixture is accepted");
const fixtureCopy = path.join(scratch, "fixture");
if (fixture) await cp(fixture, fixtureCopy, { recursive: true });
const output = path.join(root, "artifacts", "ui-final-global");
await mkdir(output, { recursive: true });
const freePort = () => new Promise((resolve, reject) => {
  const server = net.createServer();
  server.on("error", reject);
  server.listen(0, "127.0.0.1", () => {
    const port = server.address().port;
    server.close(() => resolve(port));
  });
});
const appPort = await freePort();
const debugPort = await freePort();
const base = "http://127.0.0.1:" + appPort;
const password = "Ui!" + randomBytes(18).toString("hex") + "9aA";
const children = [];
let ws;
let nextId = 0;
const pending = new Map();
const results = [];
const errors = [];
const requests = [];
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++nextId;
  const timer = setTimeout(() => { pending.delete(id); reject(new Error("CDP timeout: " + method)); }, 15000);
  pending.set(id, { resolve, reject, timer });
  ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async (expression) => {
  const answer = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (answer.exceptionDetails) throw new Error(JSON.stringify(answer.exceptionDetails));
  return answer.result.value;
};
const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));
async function until(action, timeout = 25000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    try { const result = await action(); if (result) return result; } catch { /* retry during startup */ }
    await wait(200);
  }
  throw new Error("Timeout waiting for isolated app/browser");
}
function record(name, passed, detail = "") {
  results.push({ name, passed: Boolean(passed), detail });
  console.log((passed ? "PASS " : "FAIL ") + name + (detail ? " " + detail : ""));
}
async function navigate(route) {
  await send("Page.navigate", { url: base + route });
  await until(() => evaluate('document.readyState === "complete" && location.href.startsWith(' + JSON.stringify(base + route.split("?")[0]) + ')'));
  await wait(200);
}
async function login(identifier, secret) {
  await navigate("/cuenta/iniciar-sesion");
  await evaluate('document.querySelector("#Identifier").value=' + JSON.stringify(identifier) + ';document.querySelector("#Password").value=' + JSON.stringify(secret) + ';document.querySelector(".auth-form form").requestSubmit()');
  await until(() => evaluate('location.pathname!=="/cuenta/iniciar-sesion" && document.readyState==="complete"'));
}
async function logout() {
  await evaluate('(async()=>{const f=document.querySelector("form[action*=cerrar-sesion]");if(!f)throw new Error("Logout form absent");await fetch(f.action,{method:"POST",body:new FormData(f)});})()');
}
async function screenshot(name) {
  const shot = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: true });
  await writeFile(path.join(output, name + ".png"), Buffer.from(shot.data, "base64"));
}
async function screenshotViewport(name) {
  const shot = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
  await writeFile(path.join(output, name + ".png"), Buffer.from(shot.data, "base64"));
}
async function captureRoute(route, name, width, height, mobile = false) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile });
  await navigate(route);
  const metrics = await evaluate(`(()=>{
    const visible = [...document.querySelectorAll("body *")].filter(x => {
      const s=getComputedStyle(x), r=x.getBoundingClientRect();
      if(s.display==="none" || s.visibility==="hidden" || r.width===0 || r.height===0)return false;
      if(x.closest("details:not([open]) *"))return false;
      if(x.closest("thead"))return false;
      if(x.closest(".dashboard-sidebar")&&!document.body.classList.contains("sidebar-open"))return false;
      return true;
    });
    const outside=visible.filter(x=>{
      if(x.closest("[data-carousel],.project-rail,.spotlight-track"))return false;
      if(x.closest(".dashboard-sidebar")&&!document.body.classList.contains("sidebar-open"))return false;
      const r=x.getBoundingClientRect(); return r.left < -1 || r.right > innerWidth+1;
    });
    const h1=document.querySelector("h1"), h1Size=h1?parseFloat(getComputedStyle(h1).fontSize):0;
    const largeCards=visible.filter(x=>x.matches(".panel-card,.stat-card,.public-project-card,.news-card")&&x.getBoundingClientRect().height>500).length;
    const largeControls=visible.filter(x=>x.matches("input,select,button,.btn")&&x.getBoundingClientRect().height>56).length;
    const largeHeads=visible.filter(x=>x.matches(".project-search-hero,.news-list-hero,.page-head")&&x.getBoundingClientRect().height>180).length;
    return {viewport:innerWidth,scrollWidth:document.documentElement.scrollWidth,heading:h1?.textContent?.trim(),headingPx:h1Size,largeCards,largeControls,largeHeads,outside:outside.slice(0,8).map(x=>({tag:x.tagName,cls:String(x.className).slice(0,70),left:Math.round(x.getBoundingClientRect().left),right:Math.round(x.getBoundingClientRect().right),html:x.outerHTML.slice(0,180)}))};
  })()`);
  record("Overflow " + name + " " + width, metrics.scrollWidth <= width + 1 && metrics.outside.length === 0, JSON.stringify(metrics));
  await screenshotViewport(name + "-" + width);
}
try {
  const app = spawn("dotnet", [path.join(root, "src/PortalProyectos.Web/bin/Debug/net10.0/PortalProyectos.Web.dll"), "--contentRoot", path.join(root, "src/PortalProyectos.Web")], {
    cwd: root, windowsHide: true,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development", ASPNETCORE_URLS: base, Server__Urls: base,
      Data__DatabasePath: fixture ? path.join(fixtureCopy, "functional-tests.db") : path.join(scratch, "ui.db"),
      Storage__RootPath: path.join(fixture ? fixtureCopy : scratch, "storage"),
      DataProtection__KeysPath: path.join(scratch, "keys"), Backups__RootPath: path.join(scratch, "backups"),
      Portal__SeedAdmin__Email: "ui-review@example.invalid", Portal__SeedAdmin__UserName: "UIREVIEW",
      Portal__SeedAdmin__FullName: "Revisión visual aislada", Portal__SeedAdmin__Password: password,
      Portal__UseHttpsRedirection: "false" }
  });
  children.push(app);
  let appLog = "";
  app.stdout.on("data", data => { appLog += data; });
  app.stderr.on("data", data => { appLog += data; });
  app.on("error", error => errors.push(error.message));
  await until(async () => (await fetch(base)).ok);
  const browser = spawn("C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", [
    "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
    "--remote-debugging-port=" + debugPort, "--user-data-dir=" + path.join(scratch, "edge"),
    "--disable-background-networking", "about:blank"
  ], { windowsHide: true, stdio: "ignore" });
  children.push(browser);
  const pages = await until(async () => {
    const pages = await (await fetch("http://127.0.0.1:" + debugPort + "/json/list")).json();
    return pages.find(page => page.type === "page");
  });
  ws = new WebSocket(pages.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { ws.addEventListener("open", resolve, { once: true }); ws.addEventListener("error", reject, { once: true }); });
  ws.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
      const request = pending.get(message.id); clearTimeout(request.timer); pending.delete(message.id);
      if (message.error) request.reject(new Error(JSON.stringify(message.error)));
      else request.resolve(message.result);
    }
    if (message.method === "Runtime.exceptionThrown") errors.push(message.params.exceptionDetails.text);
    if (message.method === "Network.requestWillBeSent") requests.push(message.params.request.url);
  });
  await send("Page.enable"); await send("Runtime.enable"); await send("Network.enable");
  // Only local page resources are allowed throughout this review.
  await send("Network.setBlockedURLs", { urls: ["https://*"] });
  const heights = new Map([[1920,1080],[1440,900],[1366,768],[1280,800],[1024,768],[768,1024],[430,932],[390,844],[360,800]]);
  for (const width of [1920, 1440, 1366, 1280, 1024, 768, 430, 390, 360]) {
    await send("Emulation.setDeviceMetricsOverride", { width, height: heights.get(width), deviceScaleFactor: 1, mobile: width < 768 });
    await navigate("/");
    const geometry = await evaluate('({width:innerWidth,scroll:document.documentElement.scrollWidth,heading:document.querySelector("h1")?.textContent,css:!![...document.styleSheets].find(x=>x.href?.includes("identity.css"))})');
    record("Home " + width + "px sin overflow", geometry.scroll <= width + 1 && geometry.css, JSON.stringify(geometry));
    const shot = await send("Page.captureScreenshot", { format: "png", captureBeyondViewport: true });
    await writeFile(path.join(output, "home-" + width + ".png"), Buffer.from(shot.data, "base64"));
    await evaluate('document.querySelector(".header-search summary").click()');
    record("Buscador visible " + width, await evaluate('(()=>{const r=document.querySelector(".header-search form").getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&document.activeElement.name==="search"})()'));
    await send("Input.dispatchKeyEvent", { type: "keyDown", key: "Escape", code: "Escape" });
    record("Cerrar buscador con Escape " + width, await evaluate('!document.querySelector(".header-search").open'));
    if (width < 992) {
      await evaluate('document.querySelector(".portal-toggler").click()');
      const menuOpened = await until(() => evaluate('document.querySelector("#publicNavigation").classList.contains("show")'), 4000).catch(() => false);
      record("Menú móvil " + width, menuOpened);
    }
  }
  for (const route of ["/proyectos", "/noticias", "/privacidad", "/cuenta/crear", "/cuenta/iniciar-sesion"]) {
    await navigate(route);
    record("Ruta " + route, await evaluate('document.documentElement.scrollWidth<=innerWidth+1 && !document.querySelector("h1")?.textContent.includes("Error")'));
  }
  for (const [width, height, mobile] of [[1440,900,false],[1366,768,false],[1280,800,false],[1024,768,false],[768,1024,false],[430,932,true],[390,844,true],[360,800,true]]) {
    await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile });
    for (const [route, prefix, listSelector, searchSelector] of [
      ["/proyectos", "projects", ".public-project-card", ".public-filter-grid"],
      ["/noticias", "news", ".news-card", ".news-search-form"]
    ]) {
      await navigate(route);
      const layout = await evaluate('(()=>{const header=document.querySelector(".public-header")?.getBoundingClientRect();const heading=document.querySelector(".project-search-hero,.news-list-hero")?.getBoundingClientRect();const search=document.querySelector(' + JSON.stringify(searchSelector) + ')?.getBoundingClientRect();const cards=[...document.querySelectorAll(' + JSON.stringify(listSelector) + ')].map(x=>x.getBoundingClientRect());const first=cards[0];const row=cards.filter(x=>first&&Math.abs(x.top-first.top)<4);return {viewport:innerWidth,scrollWidth:document.documentElement.scrollWidth,headerBottom:header?.bottom,headingHeight:heading?.height,searchBottom:search?.bottom,firstRowBottom:row.length?Math.max(...row.map(x=>x.bottom)):null,firstRowCount:row.length,globalSearch:getComputedStyle(document.querySelector(".header-search")).display}})()');
      const noDuplicate = layout.globalSearch === "none";
      const fitsFirstRow = width !== 1366 || mobile || (layout.firstRowCount > 0 && layout.firstRowBottom <= height);
      record(prefix + " listado compacto " + width + "x" + height,
        layout.scrollWidth <= width + 1 && noDuplicate && fitsFirstRow,
        JSON.stringify(layout));
      if (width === 1366 || width === 390) await screenshotViewport(prefix + "-" + width + "x" + height);
    }
  }
  await navigate("/");
  await evaluate('document.querySelector(".header-search").open=true; document.querySelector("#headerProjectSearch").value="ingeniería"; document.querySelector(".header-search form").requestSubmit()');
  await until(() => evaluate('location.pathname==="/proyectos"'));
  record("Búsqueda existente conserva query", await evaluate('new URLSearchParams(location.search).get("search")==="ingeniería"'));
  if (fixture) {
    await navigate("/");
    const detail = await evaluate('document.querySelector(".spotlight-content h2 a")?.getAttribute("href")');
    record("Proyecto real de fixture presente", !!detail);
    if (detail) {
      await navigate(detail);
      record("Detalle público", await evaluate('!!document.querySelector(".public-detail-hero")'));
    }
    await navigate("/noticias");
    const newsDetail = await evaluate('document.querySelector(".news-card h2 a")?.getAttribute("href")');
    if (newsDetail) {
      await navigate(newsDetail);
      record("Detalle de noticia", await evaluate('!!document.querySelector("h1")'));
    }
    for (const [route,name] of [["/","home"],["/proyectos","projects"],["/noticias","news"],["/privacidad","privacy"],["/cuenta/iniciar-sesion","login"],["/cuenta/crear","register"]]) {
      for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(route,name,width,height,mobile);
    }
    if (detail) for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(detail,"project-detail",width,height,mobile);
    if (newsDetail) for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(newsDetail,"news-detail",width,height,mobile);
    // Authenticated pages use only the disposable fixture's accounts.
    await login("UIREVIEW", password);
    if (await evaluate('location.pathname==="/cuenta/cambiar-contrasena"')) {
      const nextPassword = password + "B";
      await evaluate('document.querySelector("#CurrentPassword").value=' + JSON.stringify(password) + ';document.querySelector("#NewPassword").value=' + JSON.stringify(nextPassword) + ';document.querySelector("#ConfirmPassword").value=' + JSON.stringify(nextPassword) + ';document.querySelector(".auth-form form").requestSubmit()');
      await until(() => evaluate('location.pathname.startsWith("/Admin") && document.readyState==="complete"'));
    }
    record("Login y cambio obligatorio", await evaluate('location.pathname.startsWith("/Admin")'));
    for (const [route,name] of [["/Admin/Dashboard","admin-dashboard"],["/Admin/Users","admin-users"],["/Admin/Projects","admin-projects"],["/Admin/Reviews","admin-reviews"],["/Admin/Publications","admin-publications"],["/Admin/News","admin-news"],["/Admin/AuditLog","admin-audit"],["/Admin/Careers","admin-catalogs"],["/Admin/Administrators","admin-administrators"],["/Admin/Storage","admin-storage"],["/Notifications","admin-notifications"]]) {
      for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(route,name,width,height,mobile);
    }
    for (const width of [1440,1280,1024,768,430,390,360]) {
      await send("Emulation.setDeviceMetricsOverride", { width, height: 1000, deviceScaleFactor: 1, mobile: false });
      await navigate("/Admin/Dashboard");
      record("Dashboard SuperAdmin " + width, await evaluate('document.documentElement.scrollWidth<=innerWidth+1 && !!document.querySelector(".dashboard-sidebar")'));
      await screenshot("admin-" + width);
      await evaluate('document.querySelector("[data-notification-toggle]").click()');
      await wait(400);
      record("Campana dentro del viewport " + width, await evaluate('(()=>{const x=document.querySelector("[data-notification-dropdown]");const r=x.getBoundingClientRect();return !x.hidden&&r.left>=0&&r.right<=innerWidth&&!x.textContent.includes("No fue posible")})()'));
      if (width < 992) {
        await evaluate('document.querySelector("[data-sidebar-open]").click()');
        record("Sidebar móvil " + width, await evaluate('document.body.classList.contains("sidebar-open")'));
        await evaluate('document.querySelector("[data-sidebar-close]").click()');
      }
    }
    for (const route of ["/Admin/Users","/Admin/Projects","/Admin/Reviews","/Admin/Publications","/Admin/News","/Admin/Careers","/Admin/ProjectCatalogs","/Admin/Administrators","/Admin/AuditLog"]) {
      await navigate(route);
      record("Vista privada " + route, await evaluate('!!document.querySelector(".dashboard-sidebar") && !document.querySelector("h1")?.textContent.includes("Error")'));
    }
    await navigate("/Admin/Users");
    record("Acciones alumno aprobada", await evaluate('(()=>{const row=[...document.querySelectorAll("tr")].find(x=>x.innerText.includes("OWNER001"));return !!row&&row.innerText.includes("Restablecer contraseña")&&!row.innerText.includes("Bloquear")&&!!row.querySelector("form[data-confirm]")})()'));
    if (detail) {
      const id = detail.split("/").pop();
      await navigate("/Admin/Publications/" + id);
      for (const [shape,w,h] of [["vertical",500,900],["horizontal",1000,500],["cuadrada",700,700]]) {
        // Generated geometry is test material only, uploaded exclusively to the disposable fixture.
        const uploaded = await evaluate('(async()=>{const c=document.createElement("canvas");c.width=' + w + ';c.height=' + h + ';const g=c.getContext("2d");g.fillStyle="#d9e9f2";g.fillRect(0,0,c.width,c.height);g.fillStyle="#285496";g.fillRect(c.width*.15,c.height*.12,c.width*.7,c.height*.7);g.fillStyle="#ffbd5d";g.fillRect(c.width*.32,c.height*.25,c.width*.36,c.height*.44);const f=new FormData();f.append("image",await new Promise(r=>c.toBlob(r,"image/png")),"ui-' + shape + '.png");f.append("altText","Geometría de prueba ' + shape + '");f.append("__RequestVerificationToken",document.querySelector("input[name=__RequestVerificationToken]").value);return(await fetch("/Admin/Publications/' + id + '/images",{method:"POST",body:f})).ok})()');
        record("Upload imagen fixture " + shape, uploaded);
        await navigate("/Admin/Publications/" + id);
        await evaluate('(async()=>{const f=[...document.forms].filter(x=>x.action.endsWith("/cover")).at(-1);if(f)await fetch(f.action,{method:"POST",body:new FormData(f)});})()');
      await navigate("/");
      await screenshot("hero-" + shape + "-mobile");
      record("Hero contain " + shape, await evaluate('(()=>{const x=document.querySelector(".spotlight-photo");return x?.complete&&x.naturalWidth>0&&getComputedStyle(x).objectFit==="contain"})()'));
      await send("Emulation.setDeviceMetricsOverride", { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
      await navigate("/proyectos");
      record("Tarjeta proyecto contain " + shape, await evaluate('(()=>{const x=document.querySelector(".project-cover-image");return x?.complete&&x.naturalWidth>0&&getComputedStyle(x).objectFit==="contain"&&!!x.previousElementSibling?.classList.contains("project-cover-backdrop")})()'));
      await screenshotViewport("project-cover-" + shape + "-390x844");
      await navigate("/Admin/Publications/" + id);
      }
      await navigate(detail);
      record("Galería con miniaturas (portada excluida)", await evaluate('document.querySelectorAll(".publication-gallery-trigger").length>=2'));
      await evaluate('document.querySelector(".publication-gallery-trigger").click()');
      await until(() => evaluate('document.querySelector("[data-project-lightbox]").contains(document.activeElement)'));
      record("Lightbox abre", await evaluate('document.querySelector("[data-project-lightbox]").classList.contains("show")'));
      await evaluate('document.querySelector("[data-lightbox-next]").click()');
      record("Lightbox siguiente", await evaluate('document.querySelector("[data-lightbox-position]").textContent.startsWith("2")'));
      await send("Input.dispatchKeyEvent", { type: "keyDown", key: "Escape", code: "Escape", windowsVirtualKeyCode: 27 });
      await send("Input.dispatchKeyEvent", { type: "keyUp", key: "Escape", code: "Escape", windowsVirtualKeyCode: 27 });
      await wait(600);
      record("Lightbox Escape", await evaluate('!document.querySelector("[data-project-lightbox]").classList.contains("show")'));
    }
    await logout();
    await login("ADMIN001", "Functional!2026");
    await navigate("/Admin/Dashboard");
    record("Panel Administrador", await evaluate('!!document.querySelector(".dashboard-sidebar") && ![...document.links].some(a=>a.getAttribute("href")==="/Admin/Administrators")'));
    await logout();
    await login("OWNER001", "Functional!2026");
    for (const [route,name] of [["/Student/Dashboard","student-dashboard"],["/Student/Profile","student-profile"],["/Student/Projects","student-projects"],["/Notifications","student-notifications"]]) {
      for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(route,name,width,height,mobile);
    }
    await navigate("/Student/Projects");
    const studentDetail = await evaluate('document.querySelector("a[href*=\\\"/Student/Projects/Details\\\"]")?.getAttribute("href")');
    if (studentDetail) {
      await navigate(studentDetail);
      const studentEdit = await evaluate('document.querySelector("a[href*=\\\"/Student/Projects/Edit\\\"]")?.getAttribute("href")');
      if (studentEdit) for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(studentEdit,"student-project-edit",width,height,mobile);
    }
    if (!studentDetail || !(await evaluate('!!document.querySelector("a[href*=\\\"/Student/Projects/Edit\\\"]")'))) {
      // Create a disposable draft only in the isolated copy so the editor view is also captured.
      await navigate("/Student/Projects");
      const editLocation = await evaluate('(async()=>{const form=[...document.forms].find(x=>x.action.includes("/Create"));if(!form)return null;const response=await fetch(form.action,{method:"POST",body:new FormData(form),redirect:"follow"});return new URL(response.url).pathname+new URL(response.url).search})()');
      if (editLocation?.includes("/Edit")) for (const [width,height,mobile] of [[1366,768,false],[390,844,true]]) await captureRoute(editLocation,"student-project-edit",width,height,mobile);
    }
    for (const route of ["/Student/Dashboard","/Student/Profile","/Student/Projects","/Notifications"]) {
      await navigate(route);
      record("Panel Alumno " + route, await evaluate('!!document.querySelector(".dashboard-sidebar") && document.documentElement.scrollWidth<=innerWidth+1'));
    }
    await navigate("/Student/Dashboard");
    await screenshot("student-mobile");
    await evaluate('document.querySelector("[data-notification-toggle]").click()');
    await wait(400);
    const unreadBefore = await evaluate('document.querySelectorAll("[data-notification-id]").length');
    if (await evaluate('!!document.querySelector("[data-notification-mark-read]")')) {
      await evaluate('document.querySelector("[data-notification-mark-read]").click()');
      await wait(400);
      record("Marcar leída desde campana", await evaluate('document.querySelectorAll("[data-notification-id]").length') < unreadBefore);
    }
    await navigate("/");
    // Repeated DOM cards exercise overflow without creating or altering project records.
    await evaluate('(()=>{const t=document.querySelector(".project-rail");if(t?.firstElementChild){while(t.children.length<6)t.append(t.firstElementChild.cloneNode(true));t.dispatchEvent(new Event("scroll"));}})()');
    record("Carrusel flecha derecha", await evaluate('(async()=>{const t=document.querySelector(".project-rail");document.querySelector(".project-showcase [data-rail-next]")?.click();await new Promise(r=>setTimeout(r,650));return t?.scrollLeft>0})()'));
    record("Carrusel flecha izquierda", await evaluate('(async()=>{const t=document.querySelector(".project-rail");document.querySelector(".project-showcase [data-rail-prev]")?.click();await new Promise(r=>setTimeout(r,650));return t?.scrollLeft<2})()'));
    await evaluate('document.querySelector(".project-rail").focus()');
    await send("Input.dispatchKeyEvent", { type: "keyDown", key: "ArrowRight", code: "ArrowRight" });
    await wait(650);
    record("Carrusel teclado", await evaluate('document.querySelector(".project-rail").scrollLeft>0'));
    await send("Emulation.setTouchEmulationEnabled", { enabled: true });
    await evaluate('document.querySelector(".project-rail").scrollIntoView({block:"center"});document.querySelector(".project-rail").scrollLeft=0');
    await wait(300);
    const rect = await evaluate('(()=>{const r=document.querySelector(".project-rail").getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height}})()');
    await send("Input.dispatchTouchEvent", { type:"touchStart", touchPoints:[{x:rect.x+rect.width*.85,y:rect.y+80}] });
    for (let i=1;i<=8;i++) { await send("Input.dispatchTouchEvent", {type:"touchMove",touchPoints:[{x:rect.x+rect.width*(.85-i*.08),y:rect.y+80}]}); await wait(30); }
    await send("Input.dispatchTouchEvent", { type:"touchEnd", touchPoints:[] });
    await wait(600);
    record("Carrusel swipe emulado", await evaluate('document.querySelector(".project-rail").scrollLeft>0'));
    await screenshot("carousel-mobile-fixture");
    await evaluate('document.activeElement?.blur();document.querySelector(".project-rail").scrollLeft=0;window.scrollTo(0,0)');
    await wait(250);
    for (const width of [1440,1280,1024,768,430,390,360]) {
      await send("Emulation.setDeviceMetricsOverride", { width, height: heights.get(width), deviceScaleFactor: 1, mobile: width < 768 });
      await evaluate('window.scrollTo(0,0)');
      await wait(200);
      record("Carrusel poblado sin overflow " + width, await evaluate('document.documentElement.scrollWidth<=innerWidth+1'));
      await screenshot("populated-" + width);
      if (width === 1440 || width === 390) {
        const prefix = width === 1440 ? "desktop" : "mobile";
        const height = heights.get(width);
        const maxScroll = await evaluate('Math.max(0,document.documentElement.scrollHeight-innerHeight)');
        for (const [name, y] of [["top",0],["middle",Math.round(maxScroll/2)],["bottom",maxScroll]]) {
          await evaluate('window.scrollTo(0,' + y + ')');
          await wait(250);
          await screenshotViewport(prefix + "-" + name + "-" + width);
        }
      }
    }
  }
  record("Sin excepciones JavaScript", errors.length === 0, errors.join("; "));
  record("Recursos de página locales", requests.filter(x => /^https?:/.test(x)).every(x => x.startsWith(base)), "URLs verificadas: " + requests.length);
} catch (error) {
  console.error(error.message);
  results.push({ name: "Ejecución UI", passed: false, detail: error.message });
} finally {
  if (ws?.readyState === WebSocket.OPEN) {
    try { await send("Browser.close"); } catch { /* browser closes socket */ }
    ws.close();
  }
  for (const child of children) { if (child.exitCode === null) child.kill(); }
  await writeFile(path.join(output, "results.json"), JSON.stringify({ results, errors, scratch, productionTouched: false }, null, 2));
}
if (results.some(x => !x.passed)) process.exitCode = 1;
