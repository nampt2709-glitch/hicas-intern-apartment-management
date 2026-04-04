/**
 * Load k6 login accounts + bulk password from SeedData output only (no env-injected credentials).
 *
 * After SeedData menu 1, copy seed-credentials.generated.json next to this file, repo root, or set SEED_CREDENTIALS_PATH
 * to the file the tool wrote (often under bin/Debug/net8.0 or similar).
 *
 * JSON must include:
 * - k6Users: [{ label, email, password }, ...]
 * - bulkSharedPassword: shared password for demoblk bulk users at seed.local (and k6 temp registrations)
 */
function stripBom(s) {
  if (!s || s.length === 0) return s;
  if (s.charCodeAt(0) === 0xfeff) return s.slice(1);
  return s;
}

export function normalizeSeedUsers(parsed) {
  var users = Array.isArray(parsed) ? parsed : null;
  if (!users && parsed && parsed.k6Users) users = parsed.k6Users;
  if (!users && parsed && parsed.users) users = parsed.users;
  if (!Array.isArray(users)) {
    throw new Error('Invalid seed credentials: expected k6Users/users array or top-level JSON array');
  }
  return users.map(function (u) {
    return {
      label: u.label,
      email: u.email,
      password: u.password,
    };
  });
}

function parseSeedFileContent(raw, sourceLabel) {
  var text = stripBom(String(raw).trim());
  if (!text.length) return { ok: false, err: 'empty file: ' + sourceLabel };
  try {
    return { ok: true, data: JSON.parse(text) };
  } catch (e) {
    return { ok: false, err: 'JSON.parse failed for ' + sourceLabel + ': ' + (e && e.message ? e.message : String(e)) };
  }
}

function tryOpenJsonPaths(paths) {
  var lastErr = null;
  for (var i = 0; i < paths.length; i++) {
    var p = paths[i];
    try {
      var raw = open(p);
      if (!raw || String(raw).trim().length === 0) {
        lastErr = 'empty or missing: ' + p;
        continue;
      }
      var pr = parseSeedFileContent(raw, p);
      if (pr.ok) return pr.data;
      lastErr = pr.err;
    } catch (e) {
      lastErr = 'open failed for ' + p + ': ' + (e && e.message ? e.message : String(e));
    }
  }
  return { error: lastErr || 'no paths tried' };
}

function readSeedJsonObject() {
  var filePath = __ENV.SEED_CREDENTIALS_PATH;
  if (filePath && String(filePath).trim().length > 0) {
    var rawEnv = open(filePath);
    if (!rawEnv || String(rawEnv).trim().length === 0) {
      throw new Error('SEED_CREDENTIALS_PATH set but file empty or unreadable: ' + filePath);
    }
    var pe = parseSeedFileContent(rawEnv, filePath);
    if (!pe.ok) throw new Error(pe.err);
    return pe.data;
  }

  // k6 open() resolves relative to the process working directory (where you ran `k6 run`).
  // Cover: cwd = repo root, cwd = k6/, and SeedData output still only under bin/Debug.
  var fileCandidates = [
    'seed-credentials.generated.json',
    './seed-credentials.generated.json',
    '.\\seed-credentials.generated.json',
    './k6/seed-credentials.generated.json',
    '.\\k6\\seed-credentials.generated.json',
    'k6/seed-credentials.generated.json',
    'k6\\seed-credentials.generated.json',
    '../seed-credentials.generated.json',
    '..\\seed-credentials.generated.json',
    '../k6/seed-credentials.generated.json',
    '..\\k6\\seed-credentials.generated.json',
    '../src/ApartmentManagement.SeedData/bin/Debug/net8.0/seed-credentials.generated.json',
    '..\\src\\ApartmentManagement.SeedData\\bin\\Debug\\net8.0\\seed-credentials.generated.json',
    '../src/ApartmentManagement.SeedData/bin/Debug/net9.0/seed-credentials.generated.json',
    '..\\src\\ApartmentManagement.SeedData\\bin\\Debug\\net9.0\\seed-credentials.generated.json',
  ];

  var parsed = tryOpenJsonPaths(fileCandidates);
  if (!parsed || typeof parsed !== 'object' || parsed.error) {
    throw new Error(
      'Missing or invalid seed-credentials.generated.json. Run SeedData menu 1, copy the file into k6/, or set SEED_CREDENTIALS_PATH.\n' +
        'Tried: ' +
        fileCandidates.join(', ') +
        (parsed && parsed.error ? '\nLast error: ' + parsed.error : '')
    );
  }
  return parsed;
}

export function loadSeedCredentials() {
  var parsed = readSeedJsonObject();
  var accounts = normalizeSeedUsers(parsed);
  var bulk = parsed.bulkSharedPassword;
  if (!bulk || typeof bulk !== 'string' || bulk.length < 8) {
    throw new Error(
      'seed-credentials.generated.json must include string bulkSharedPassword (8+ chars). Re-run SeedData insert with the current tool.'
    );
  }
  return { accounts: accounts, bulkSharedPassword: bulk };
}

export function loadSeedAccounts() {
  return loadSeedCredentials().accounts;
}
