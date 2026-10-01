/* ============================================================
   make-cert.js -- local HTTPS certificate for the LAN install route.

   Why this exists (CLAUDE.md §13.1/§14): Chrome only treats `https`,
   `localhost` and `127.0.0.1` as secure origins. A LAN IP is none of those,
   so over plain http the service worker never registers and "Install app"
   degrades to a bookmark shortcut that opens in a Chrome tab -- address bar
   and all. Marking the origin trusted with chrome://flags patches the
   *service worker* half of that, but the install still isn't a real one.

   Serving the same files over https with a certificate the phone actually
   trusts fixes both halves at the source: a genuinely secure origin, a real
   install, and no address bar in the launched app.

   It produces a two-certificate chain, the way mkcert does:

     tools/certs/rootCA.pem   the CA -- install THIS on the phone, once
     tools/certs/rootCA.crt   byte-identical copy, .crt so Android's file
                              picker offers it as a CA certificate
     tools/certs/rootCA.key   the CA private key -- never leaves this machine
     tools/certs/server.crt   leaf served by tools/serve.js --https
     tools/certs/server.key   its private key

   The CA is generated once and then reused: re-running this after your
   router hands the PC a different IP re-issues only the leaf, so the phone
   does NOT have to trust anything again. Delete tools/certs/ to start over.

       node tools/make-cert.js                 localhost + every LAN IPv4
       node tools/make-cert.js 192.168.1.50    plus extra hosts/IPs

   Requires the `openssl` binary. It does not have to be on PATH -- Git for
   Windows ships one, and this script finds it there (PowerShell's PATH
   normally has no openssl at all, which is what "openssl not found on PATH"
   used to mean). Set OPENSSL to an explicit path to override the search.
   Nothing here touches the network.

   SECURITY: tools/certs/ holds a private CA key. Anyone who steals it can
   impersonate any site to a device that trusts this CA. It is gitignored;
   keep it on this machine, and if it ever leaks, delete tools/certs/, run
   this again, and remove the old CA from the phone's user credentials.
   ============================================================ */
'use strict';

const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const dir = path.join(__dirname, 'certs');
const caKey = path.join(dir, 'rootCA.key');
const caCert = path.join(dir, 'rootCA.pem');
const caCopy = path.join(dir, 'rootCA.crt');
const srvKey = path.join(dir, 'server.key');
const srvCert = path.join(dir, 'server.crt');
const srvCsr = path.join(dir, 'server.csr');
const extFile = path.join(dir, 'server.ext');

/* Find an openssl to drive. On Windows it is almost never on PATH -- but
   Git for Windows ships two copies of it, and this project already assumes
   git. Looking there beats telling the user to go install OpenSSL. */
function findOpenssl() {
  const tried = [];
  const candidates = [];

  if (process.env.OPENSSL) candidates.push(process.env.OPENSSL);
  candidates.push('openssl'); // already on PATH (Git Bash, macOS, Linux)

  if (process.platform === 'win32') {
    // Ask git where it lives, then look inside that installation.
    let gitRoot = '';
    try {
      const out = execFileSync('git', ['--exec-path'], { encoding: 'utf8' }).trim();
      // ...\Git\mingw64\libexec\git-core -> ...\Git
      const marker = out.replace(/\//g, path.sep).split(path.sep + 'mingw64' + path.sep)[0];
      if (marker && marker !== out) gitRoot = marker;
    } catch {
      /* git not on PATH either; fall through to the fixed guesses */
    }

    const roots = [gitRoot, process.env.ProgramFiles, process.env['ProgramFiles(x86)'], process.env.LOCALAPPDATA]
      .filter(Boolean)
      .flatMap((r) => (r === gitRoot ? [r] : [path.join(r, 'Git'), path.join(r, 'Programs', 'Git')]));

    for (const r of roots) {
      candidates.push(path.join(r, 'mingw64', 'bin', 'openssl.exe'));
      candidates.push(path.join(r, 'usr', 'bin', 'openssl.exe'));
    }
    for (const r of [process.env.ProgramFiles, process.env['ProgramFiles(x86)']].filter(Boolean)) {
      candidates.push(path.join(r, 'OpenSSL-Win64', 'bin', 'openssl.exe'));
      candidates.push(path.join(r, 'OpenSSL-Win32', 'bin', 'openssl.exe'));
    }
  }

  for (const c of candidates) {
    if (tried.includes(c)) continue;
    tried.push(c);
    try {
      execFileSync(c, ['version'], { stdio: ['ignore', 'pipe', 'pipe'] });
      return c;
    } catch {
      /* next candidate */
    }
  }

  console.error('Could not find an openssl binary. Looked for:\n  ' + tried.join('\n  '));
  console.error('\nFixes, easiest first:');
  console.error('  - Run this from Git Bash instead of PowerShell (Git ships openssl).');
  console.error('  - Or point at it explicitly, e.g. in PowerShell:');
  console.error('      $env:OPENSSL = "C:\\Program Files\\Git\\mingw64\\bin\\openssl.exe"');
  console.error('      node tools/make-cert.js');
  process.exit(1);
}

const OPENSSL = findOpenssl();
if (OPENSSL !== 'openssl') console.log('Using openssl at ' + OPENSSL);

function openssl(args) {
  return execFileSync(OPENSSL, args, { stdio: ['ignore', 'pipe', 'pipe'] });
}

fs.mkdirSync(dir, { recursive: true });

/* Every address the phone might type. The leaf must name them all up front:
   a certificate that doesn't list the IP you browse to is a certificate
   error, and a certificate error is not a secure context. */
const hosts = ['localhost', '127.0.0.1'];
for (const ifaces of Object.values(os.networkInterfaces())) {
  for (const i of ifaces || []) {
    if (i.family === 'IPv4' && !i.internal) hosts.push(i.address);
  }
}
for (const extra of process.argv.slice(2)) if (!hosts.includes(extra)) hosts.push(extra);

const isIp = (h) => /^\d{1,3}(\.\d{1,3}){3}$/.test(h);
const san = hosts.map((h) => `${isIp(h) ? 'IP' : 'DNS'}:${h}`).join(',');

// --- the CA: generated once, reused forever -------------------------------
const reusedCa = fs.existsSync(caCert) && fs.existsSync(caKey);
if (reusedCa) {
  console.log('Reusing existing CA (tools/certs/rootCA.pem) -- no need to re-trust the phone.');
} else {
  openssl([
    'req', '-x509', '-newkey', 'rsa:2048', '-nodes',
    '-keyout', caKey, '-out', caCert,
    '-days', '3650', '-sha256',
    '-subj', '/CN=Task Manager Local CA/O=Task Manager',
    '-addext', 'basicConstraints=critical,CA:TRUE,pathlen:0',
    '-addext', 'keyUsage=critical,keyCertSign,cRLSign',
  ]);
  console.log('Created CA: tools/certs/rootCA.pem (valid 10 years).');
}
fs.copyFileSync(caCert, caCopy);

// --- the leaf: re-issued whenever the set of addresses changes -------------
fs.writeFileSync(
  extFile,
  [
    'basicConstraints=CA:FALSE',
    'keyUsage=critical,digitalSignature,keyEncipherment',
    'extendedKeyUsage=serverAuth',
    `subjectAltName=${san}`,
    '',
  ].join('\n')
);

openssl([
  'req', '-newkey', 'rsa:2048', '-nodes',
  '-keyout', srvKey, '-out', srvCsr,
  '-subj', '/CN=Task Manager Local',
]);

/* 397 days: Chrome rejects longer leaf certificates. That limit is only
   enforced for publicly-trusted roots today, but staying under it costs
   nothing and removes one way for this to mysteriously stop working. */
openssl([
  'x509', '-req', '-in', srvCsr,
  '-CA', caCert, '-CAkey', caKey, '-CAcreateserial',
  '-out', srvCert, '-days', '397', '-sha256',
  '-extfile', extFile,
]);

fs.rmSync(srvCsr, { force: true });
fs.rmSync(extFile, { force: true });

console.log('Created leaf: tools/certs/server.crt, valid for 397 days, covering:');
for (const h of hosts) console.log(`  ${h}`);
console.log('\nNext:');
console.log('  1. node tools/serve.js --https      (starts https on 8443)');
console.log('  2. On the phone, install tools/certs/rootCA.crt as a CA certificate.');
console.log('     The server prints a download link and the exact Android steps.');
if (reusedCa) console.log('     (Already done on this phone? Skip it -- the CA did not change.)');
