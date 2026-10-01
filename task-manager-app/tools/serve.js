/* ============================================================
   serve.js — static file server for the built Angular app (CLAUDE.md §13.6).

   It serves pwa/dist/tasks/browser, i.e. the *output* of `npm run build`
   inside /pwa -- not the source. A dev server (`npm start` in /pwa) is for
   editing; this one is for installing, because the service worker only
   exists in a production build.

   §13.6 suggests `python3 -m http.server 8000`. Any static server works;
   this is the same thing in Node, for machines without Python installed.
   Zero dependencies, no network access of its own, nothing persisted.

       node tools/serve.js            -> http://localhost:8000  (loopback only)
       node tools/serve.js 8080       -> another port
       node tools/serve.js --https    -> https://<lan-ip>:8443 with the local
                                         certificate from tools/make-cert.js
                                         -- a REAL secure origin, so Chrome
                                         does a real install with no address
                                         bar (§14.4). Run make-cert.js first.
       node tools/serve.js --adb      -> also forward the port to an Android
                                         phone over USB, so the phone reaches
                                         it as localhost (§14.1)
       node tools/serve.js --lan      -> plain http, reachable from the LAN.
                                         NOTE: a LAN IP over http is not a
                                         secure origin, so Chrome will not
                                         register the service worker and
                                         "Install app" degrades to a bookmark
                                         shortcut that opens in a browser tab.
                                         Prefer --https.

   Only needed for the *first* load on a device. Once the service worker
   has cached the shell (§13.4), the installed app runs with this stopped.
   ============================================================ */
'use strict';

const http = require('node:http');
const https = require('node:https');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { execFile } = require('node:child_process');

const args = process.argv.slice(2);
const secure = args.includes('--https');
const adb = args.includes('--adb');
// --https is only useful from another device, so it implies LAN binding.
const lan = args.includes('--lan') || secure;
const port = Number(args.find((a) => /^\d+$/.test(a))) || (secure ? 8443 : 8000);
const root = path.join(__dirname, '..', 'pwa', 'dist', 'tasks', 'browser');
const certDir = path.join(__dirname, 'certs');
const host = lan ? '0.0.0.0' : '127.0.0.1';
const scheme = secure ? 'https' : 'http';

if (!fs.existsSync(path.join(root, 'index.html'))) {
  console.error('No build found at ' + path.relative(path.join(__dirname, '..'), root) + '.');
  console.error('Build it first:  cd pwa  &&  npm install  &&  npm run build');
  process.exit(1);
}

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.css': 'text/css; charset=utf-8',
};

function lanIps() {
  const ips = [];
  for (const ifaces of Object.values(os.networkInterfaces())) {
    for (const i of ifaces || []) {
      if (i.family === 'IPv4' && !i.internal) ips.push(i.address);
    }
  }
  return ips;
}

function serveStatic(req, res) {
  let pathname;
  try {
    pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
  } catch {
    res.writeHead(400).end('Bad request');
    return;
  }
  if (pathname.endsWith('/')) pathname += 'index.html';

  // Resolve inside root and reject anything that escapes it.
  const file = path.join(root, pathname);
  if (file !== root && !file.startsWith(root + path.sep)) {
    res.writeHead(403).end('Forbidden');
    return;
  }

  fs.readFile(file, (err, data) => {
    if (err) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }).end('Not found');
      return;
    }
    const ext = path.extname(file).toLowerCase();
    res
      .writeHead(200, {
        'Content-Type': TYPES[ext] || 'application/octet-stream',
        // Never let the browser's HTTP cache mask an updated shell; the
        // service worker is what provides offline, not this header.
        'Cache-Control': 'no-cache',
        'X-Content-Type-Options': 'nosniff',
        // ngsw-worker.js must be allowed to control the whole origin.
        ...(path.basename(file) === 'ngsw-worker.js' ? { 'Service-Worker-Allowed': '/' } : {}),
      })
      .end(data);
  });
}

/* `adb reverse` makes the phone's own localhost:PORT tunnel back to this
   server over USB. That is what turns the page into a secure origin on the
   phone -- without it Chrome refuses to register sw.js and the app never
   caches, which is the whole reason offline was failing over --lan. */
function adbReverse() {
  execFile('adb', ['reverse', `tcp:${port}`, `tcp:${port}`], (err, stdout, stderr) => {
    if (err) {
      const missing = err.code === 'ENOENT';
      console.log('\n  adb port-forward FAILED.');
      console.log(
        missing
          ? '  adb is not on PATH. Install "Android SDK Platform Tools" (a standalone\n' +
              '  zip, no Android Studio needed) and add its folder to PATH.'
          : `  ${String(stderr || err.message).trim()}`
      );
      if (!missing) {
        console.log('  Check: phone plugged in, USB debugging on, and the');
        console.log('  "Allow USB debugging?" prompt accepted on the phone.');
      }
      return;
    }
    console.log(`\n  adb reverse OK -- open http://localhost:${port} in Chrome ON THE PHONE.`);
    console.log('  That origin is secure, so the service worker registers and caches the shell.');
    console.log('  Check Settings -> "Offline install" in the app; it must say "Offline ready"');
    console.log('  BEFORE you unplug. After that the phone works with this server stopped.');
  });
}

/* The phone has to get the CA onto itself somehow, and it cannot fetch it
   over the very connection the CA is meant to validate -- that would mean
   clicking through a certificate warning to obtain the file that removes
   the warning. So --https also opens a tiny plain-http listener whose only
   job is handing out the CA certificate and the steps to install it. It
   serves no app files. */
function startCaHelper(caPath, httpsUrls) {
  const helperPort = port === 8000 ? 8080 : 8000;
  const ca = fs.readFileSync(caPath);
  const links = httpsUrls.map((u) => `<li><a href="${u}">${u}</a></li>`).join('');
  const page =
    '<!doctype html><meta charset="utf-8">' +
    '<meta name="viewport" content="width=device-width,initial-scale=1">' +
    '<title>Task Manager - certificate setup</title>' +
    '<style>body{font:16px/1.5 -apple-system,"Segoe UI",Roboto,sans-serif;background:#0e1014;' +
    'color:#e6e8ee;margin:0;padding:24px;max-width:40rem}a{color:#7aa2ff}' +
    'ol,ul{padding-left:1.25rem}li{margin:.4rem 0}' +
    '.btn{display:inline-block;margin:1rem 0;padding:.8rem 1.2rem;border-radius:12px;' +
    'background:#7aa2ff;color:#0e1014;font-weight:600;text-decoration:none}</style>' +
    '<h1>Install the local certificate</h1>' +
    '<p>Do this once, on this phone. It lets Chrome trust this PC over HTTPS, ' +
    'which is what makes the app install as a real app instead of a browser shortcut.</p>' +
    '<p><a class="btn" href="/rootCA.crt" download="rootCA.crt">1. Download rootCA.crt</a></p>' +
    '<ol start="2">' +
    '<li>Android: <b>Settings &rarr; Security &rarr; More security settings &rarr; ' +
    'Encryption &amp; credentials &rarr; Install a certificate &rarr; CA certificate</b>, ' +
    'tap <b>Install anyway</b>, then pick the downloaded <b>rootCA.crt</b>.<br>' +
    '(Wording varies by phone; searching Settings for <i>certificate</i> also finds it.)</li>' +
    '<li>Fully close Chrome and reopen it.</li>' +
    '<li>Open the app over HTTPS:<ul>' + links + '</ul></li>' +
    '<li>Check <b>Settings &rarr; Offline install</b> in the app. It must say ' +
    '<b>Offline ready</b>.</li>' +
    '<li>Chrome menu &rarr; <b>Install app</b>.</li>' +
    '</ol>' +
    '<p><b>Note:</b> the HTTPS address is a different origin from the old ' +
    'http:// one, so it starts with an empty task list. Export a backup from ' +
    'the old app first, then restore it here.</p>';

  const helper = http.createServer((req, res) => {
    const p = (req.url || '/').split('?')[0];
    if (p === '/rootCA.crt' || p === '/ca') {
      res
        .writeHead(200, {
          'Content-Type': 'application/x-x509-ca-cert',
          'Content-Disposition': 'attachment; filename="rootCA.crt"',
          'Cache-Control': 'no-store',
        })
        .end(ca);
      return;
    }
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' }).end(page);
  });

  helper.on('error', (err) => {
    console.log(`\n  (certificate helper could not start on port ${helperPort}: ${err.code})`);
    console.log(`  Copy tools/certs/rootCA.crt to the phone by hand instead.`);
  });

  helper.listen(helperPort, host, () => {
    console.log('\n  Certificate setup page (plain http, phone-friendly):');
    for (const ip of lanIps()) console.log(`    http://${ip}:${helperPort}`);
  });
}

let server;
if (secure) {
  const key = path.join(certDir, 'server.key');
  const cert = path.join(certDir, 'server.crt');
  if (!fs.existsSync(key) || !fs.existsSync(cert)) {
    console.error('No certificate found in tools/certs/.');
    console.error('Generate one first:  node tools/make-cert.js');
    process.exit(1);
  }
  server = https.createServer({ key: fs.readFileSync(key), cert: fs.readFileSync(cert) }, serveStatic);
} else {
  server = http.createServer(serveStatic);
}

server.listen(port, host, () => {
  console.log(`Serving ${path.relative(process.cwd(), root) || root}`);
  console.log(`  ${scheme}://localhost:${port}`);

  const ips = lanIps();
  if (lan) for (const ip of ips) console.log(`  ${scheme}://${ip}:${port}  (LAN)`);

  if (secure) {
    console.log('\n  HTTPS mode -- this is a real secure origin, so on the phone you get a');
    console.log('  real "Install app" (WebAPK) that launches full-screen with no address bar,');
    console.log('  and the service worker registers without any chrome://flags override.');
    console.log('\n  The phone must trust the CA once: tools/certs/rootCA.crt');
    startCaHelper(path.join(certDir, 'rootCA.crt'), ips.map((ip) => `https://${ip}:${port}`));
    console.log('\n  Reminder: https://<ip>:PORT is a different origin from http://<ip>:PORT,');
    console.log('  so localStorage starts empty. Export from the old app, restore into this one.');
  } else if (lan) {
    console.log('\n  LAN mode: this folder is reachable by other devices on your network.');
    console.log('\n  WARNING -- a LAN IP over http is NOT a secure origin. Chrome will not');
    console.log('  register the service worker there, so "Install app" silently degrades to a');
    console.log('  plain "Add to Home screen" shortcut that opens in a Chrome tab -- address');
    console.log('  bar and all -- and stops working the moment this server does. Options:');
    console.log('\n    A) node tools/make-cert.js  then  node tools/serve.js --https');
    console.log('       (real secure origin, real install, no address bar)');
    console.log('    B) chrome://flags/#unsafely-treat-insecure-origin-as-secure on the phone,');
    console.log('       set to Enabled for the origin below, relaunch Chrome. This fixes the');
    console.log('       service worker, but Chrome still shows the URL in the installed app.');
    for (const ip of ips) console.log(`         http://${ip}:${port}`);
    console.log('    C) Plug in over USB and run with --adb instead.');
  }

  if (adb) adbReverse();
  console.log('\nCtrl+C to stop.');
});

server.on('error', (err) => {
  console.error(
    err.code === 'EADDRINUSE'
      ? `Port ${port} is already in use. Pass another port, e.g. node tools/serve.js ${port + 1}`
      : String(err.message || err)
  );
  process.exit(1);
});
