/**
 * Spike test — burst / recovery, ≥10k requests, seed credentials JSON.
 * Coi 429 là hành vi bình thường (không fail http_req_failed). Feedback: xóa bằng admin.
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import { loadSeedCredentials } from './seed-accounts.js';
import {
  initHttpSuccessPolicy,
  pickRandom,
  pickActor,
  buildHeaders,
  firstAdminToken,
  firstItemId,
  log429,
  okOr429,
  okCreateOr429,
  okDeleteOr429,
} from './http-common.js';
import {
  buildReportOutputPath,
  formatCheckOutcome,
  formatHttpOutcome,
  formatRateLimit429Note,
  formatRequestTiming,
} from './summary-helpers.js';
import {
  registerAndPurge,
  accessTokenFromLoginBody,
  feedbackIdFromCreateBody,
  deleteFeedbackWithRetry,
  seedStyleFeedbackContent,
} from './user-lifecycle.js';
import { runK6TeardownCleanup } from './cleanup.js';

initHttpSuccessPolicy();

const http429 = new Counter('http_429_spike');
const httpFailed = new Counter('http_failed_spike');
const spikeLatency = new Trend('spike_request_duration');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const SPIKE_TEST_INDEX = __ENV.SPIKE_TEST_INDEX || '1';

var seedCred = loadSeedCredentials();
var accounts = seedCred.accounts;
var bulkSharedPassword = seedCred.bulkSharedPassword;

export const options = {
  scenarios: {
    spike_profile: {
      executor: 'ramping-arrival-rate',
      startRate: 5,
      timeUnit: '1s',
      preAllocatedVUs: 60,
      maxVUs: 200,
      stages: [
        { duration: '3m', target: 8 },
        { duration: '90s', target: 42 },
        { duration: '3m', target: 6 },
        { duration: '60s', target: 28 },
        { duration: '4m', target: 7 },
        { duration: '2m', target: 5 },
      ],
      exec: 'spikeMain',
    },
    auth_spike_lane: {
      executor: 'constant-vus',
      vus: 4,
      duration: '5m',
      startTime: '2m',
      exec: 'authSpikeLane',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<12000', 'p(99)<20000'],
    http_req_failed: ['rate<0.12'],
    http_failed_spike: ['count<800'],
  },
};

function track(res) {
  if (res && res.timings && res.timings.duration >= 0) spikeLatency.add(res.timings.duration);
  return res;
}

export function setup() {
  var tokens = {};
  for (var i = 0; i < accounts.length; i++) {
    var acc = accounts[i];
    var loginRes = http.post(
      BASE_URL + '/api/v1.0/auth/login',
      JSON.stringify({ email: acc.email, password: acc.password }),
      { headers: { 'Content-Type': 'application/json' }, tags: { name: 'SpikeLogin:' + acc.label } }
    );
    var token = accessTokenFromLoginBody(loginRes.body);
    var ok = check(loginRes, {
      ['spike setup login ' + acc.label]: function (r) {
        return r.status === 200 && !!token;
      },
    });
    if (!ok || !token) {
      throw new Error(
        'spike setup failed for ' + acc.email + ' status=' + loginRes.status + ' body=' + String(loginRes.body).slice(0, 200)
      );
    }
    tokens[acc.label] = token;
  }
  return { tokens: tokens, bulkSharedPassword: bulkSharedPassword };
}

export function teardown(data) {
  var tokMap = data && data.tokens ? data.tokens : {};
  var adminTok = firstAdminToken(tokMap);
  runK6TeardownCleanup(BASE_URL, adminTok);
}

function failIfBad(c) {
  if (!c) httpFailed.add(1);
}

export function spikeMain(data) {
  var adminTok = firstAdminToken(data.tokens);
  if (!adminTok) {
    httpFailed.add(1);
    return;
  }

  var r = Math.random();
  if (r < 0.88) {
    spikeSingleRead(data, adminTok);
  } else if (r < 0.97) {
    spikeFeedbackWrite(data, adminTok);
  } else {
    spikeMiniBrowse(data, adminTok);
  }

  sleep(0.005 + Math.random() * 0.02);
}

function spikeSingleRead(data, adminTok) {
  var actor = pickActor(data.tokens, 0.3);
  var headers = buildHeaders(actor.token);
  var admin = actor.label.indexOf('admin') === 0;
  var roll = Math.random();
  var res;

  if (roll < 0.22) {
    res = track(log429(http429, http.get(BASE_URL + '/api/v1.0/auth/me', { headers: headers, tags: { name: 'SpikeMe' } })));
    failIfBad(check(res, { 'me 200 or 429': okOr429 }));
    return;
  }
  if (roll < 0.42) {
    var url = admin
      ? BASE_URL + '/api/v1.0/apartments?pageNumber=1&pageSize=10'
      : BASE_URL + '/api/v1.0/apartments/me';
    res = track(log429(http429, http.get(url, { headers: headers, tags: { name: 'SpikeApt' } })));
    failIfBad(check(res, { 'apt 200 or 429': okOr429 }));
    return;
  }
  if (roll < 0.58) {
    res = track(
      log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/invoices/me?pageNumber=1&pageSize=8', { headers: headers, tags: { name: 'SpikeInvMe' } })
      )
    );
    failIfBad(check(res, { 'inv/me 200 or 429': okOr429 }));
    return;
  }
  if (roll < 0.72) {
    res = track(
      log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/utilityservices?pageNumber=1&pageSize=10', { headers: headers, tags: { name: 'SpikeUtil' } })
      )
    );
    failIfBad(check(res, { 'util 200 or 429': okOr429 }));
    return;
  }
  if (roll < 0.82) {
    res = track(
      log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/feedbacks/me?pageNumber=1&pageSize=10', { headers: headers, tags: { name: 'SpikeFbMe' } })
      )
    );
    failIfBad(check(res, { 'fb me 200 or 429': okOr429 }));
    return;
  }
  if (admin) {
    if (roll < 0.9) {
      res = track(
        log429(
          http429,
          http.get(BASE_URL + '/api/v1.0/invoices?pageNumber=1&pageSize=6', { headers: headers, tags: { name: 'SpikeInvAd' } })
        )
      );
      failIfBad(check(res, { 'inv ad 200 or 429': okOr429 }));
    } else if (roll < 0.95) {
      res = track(log429(http429, http.get(BASE_URL + '/api/v1.0/feedbacks/tree', { headers: headers, tags: { name: 'SpikeTree' } })));
      failIfBad(check(res, { 'tree 200 or 429': okOr429 }));
    } else {
      res = track(
        log429(http429, http.get(BASE_URL + '/api/v1.0/feedbacks/flattened', { headers: headers, tags: { name: 'SpikeFlat' } }))
      );
      failIfBad(check(res, { 'flat 200 or 429': okOr429 }));
    }
  } else {
    if (roll < 0.88) {
      res = track(
        log429(http429, http.get(BASE_URL + '/api/v1.0/residents/me', { headers: headers, tags: { name: 'SpikeResMe' } }))
      );
      failIfBad(check(res, { 'res me 200 or 429': okOr429 }));
    } else {
      res = track(
        log429(http429, http.get(BASE_URL + '/api/v1.0/feedbacks/tree', { headers: headers, tags: { name: 'SpikeTreeU' } }))
      );
      failIfBad(check(res, { 'tree u 200 or 429': okOr429 }));
    }
  }
}

function spikeMiniBrowse(data, adminTok) {
  var actor = pickActor(data.tokens, 0.35);
  var headers = buildHeaders(actor.token);
  var admin = actor.label.indexOf('admin') === 0;

  var a = track(log429(http429, http.get(BASE_URL + '/api/v1.0/auth/me', { headers: headers, tags: { name: 'SpikeMini1' } })));
  failIfBad(check(a, { 'm1 200 or 429': okOr429 }));

  var u2 = admin
    ? BASE_URL + '/api/v1.0/users?pageNumber=1&pageSize=5'
    : BASE_URL + '/api/v1.0/apartments/me';
  var b = track(log429(http429, http.get(u2, { headers: headers, tags: { name: 'SpikeMini2' } })));
  failIfBad(check(b, { 'm2 200 or 429': okOr429 }));

  var list = track(
    log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/invoices/me?pageNumber=1&pageSize=5', { headers: headers, tags: { name: 'SpikeMini3' } })
    )
  );
  failIfBad(check(list, { 'm3 200 or 429': okOr429 }));

  if (admin) {
    var invId = list.status === 200 ? firstItemId(list.body) : null;
    if (invId && Math.random() < 0.6) {
      var one = track(
        log429(
          http429,
          http.get(BASE_URL + '/api/v1.0/invoices/' + invId, { headers: headers, tags: { name: 'SpikeMini4' } })
        )
      );
      failIfBad(check(one, { 'm4 200 or 429': okOr429 }));
    }
  }

  if (adminTok && Math.random() < 0.4) {
    var perf = track(
      log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/performance', {
          headers: { Authorization: 'Bearer ' + adminTok },
          tags: { name: 'SpikePerf' },
        })
      )
    );
    failIfBad(check(perf, { 'perf 200 or 429': okOr429 }));
  }
}

function spikeFeedbackWrite(data, adminTok) {
  var users = Object.keys(data.tokens).filter(function (l) {
    return l.indexOf('user') === 0;
  });
  var pool = users.length ? users : Object.keys(data.tokens);
  var label = pickRandom(pool);
  var token = data.tokens[label];
  var headers = buildHeaders(token);
  var res = track(
    log429(
      http429,
      http.post(
        BASE_URL + '/api/v1.0/feedbacks',
        JSON.stringify({ content: seedStyleFeedbackContent(label, __VU, __ITER) }),
        { headers: headers, tags: { name: 'SpikeFbCreate' } }
      )
    )
  );
  failIfBad(check(res, { 'fb create 200/201/429': okCreateOr429 }));

  if (res.status === 200 || res.status === 201) {
    var id = feedbackIdFromCreateBody(res.body);
    if (id) {
      var del = track(log429(http429, deleteFeedbackWithRetry(BASE_URL, adminTok, id, 'SpikeFbDel')));
      failIfBad(check(del, { 'fb del 200/204/429': okDeleteOr429 }));
    } else httpFailed.add(1);
  }
}

function pickFailLoginEmail() {
  var i;
  for (i = 0; i < accounts.length; i++) {
    if (accounts[i].label === 'user1') return accounts[i].email;
  }
  for (i = 0; i < accounts.length; i++) {
    if (accounts[i].label.indexOf('user') === 0) return accounts[i].email;
  }
  return accounts.length ? accounts[0].email : null;
}

export function authSpikeLane(data) {
  var adminTok = firstAdminToken(data.tokens);
  if (adminTok) {
    var bundle = registerAndPurge(BASE_URL, adminTok, __VU, __ITER, data.bulkSharedPassword);
    failIfBad(
      check(bundle.reg, {
        'reg 200/201/400/429': function (x) {
          return [200, 201, 400, 429].indexOf(x.status) >= 0;
        },
      })
    );
    if (bundle.purge) {
      failIfBad(
        check(bundle.purge, {
          'purge 200/404/429': function (x) {
            return [200, 404, 429].indexOf(x.status) >= 0;
          },
        })
      );
    }
  }

  var email = pickFailLoginEmail();
  if (!email) return;
  var res = track(
    log429(
      http429,
      http.post(
        BASE_URL + '/api/v1.0/auth/login',
        JSON.stringify({ email: email, password: 'WrongPassword!!!' }),
        { headers: { 'Content-Type': 'application/json' }, tags: { name: 'SpikeLoginFail' } }
      )
    )
  );
  failIfBad(
    check(res, {
      'fail login 401 or 429': function (r) {
        return r.status === 401 || r.status === 429;
      },
    })
  );
  sleep(0.05 + Math.random() * 0.08);
}

export function handleSummary(data) {
  var now = new Date();
  var iso = now.toISOString().replace(/[:.]/g, '-');
  var fileName = buildReportOutputPath('spiketest', 'SpikeTest(' + SPIKE_TEST_INDEX + ')-(' + iso + ').txt');

  var httpVals = {};
  if (data.metrics.http_reqs && data.metrics.http_reqs.values) httpVals = data.metrics.http_reqs.values;
  var r429 = undefined;
  if (data.metrics.http_429_spike && data.metrics.http_429_spike.values && data.metrics.http_429_spike.values.count !== undefined) {
    r429 = data.metrics.http_429_spike.values.count;
  } else if (data.metrics.http_429_spike && data.metrics.http_429_spike.count !== undefined) {
    r429 = data.metrics.http_429_spike.count;
  }
  var rateStr = httpVals.rate !== undefined ? String(httpVals.rate) : 'n/a';
  var countStr = httpVals.count !== undefined ? String(httpVals.count) : 'n/a';

  var summary = ['SpikeTest(' + SPIKE_TEST_INDEX + ') - ' + iso]
    .concat(formatHttpOutcome(data.metrics))
    .concat(formatCheckOutcome(data.root_group))
    .concat(formatRequestTiming(data.metrics))
    .concat(['HTTP req/s: ' + rateStr, 'HTTP requests total: ' + countStr])
    .concat(formatRateLimit429Note(r429, 'http_429_spike'))
    .concat([
      'Report path (relative to k6 cwd): ' + fileName,
      'spike_profile: ramping-arrival-rate (burst ~42/s + mini 28/s), 1–5 HTTP/iteration tùy nhánh.',
      'auth_spike_lane: register+purge + login sai mật khẩu (401/429).',
    ])
    .join('\n');

  console.log(summary);
  var out = {};
  out[fileName] = summary;
  return out;
}
