/**
 * Stress test — tải tăng dần / cao, ≥10k request, đa route & tài khoản seed.
 * 429 không fail http_req_failed. Ghi feedback → xóa bằng admin (tránh quota xóa 10/h của user).
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { loadSeedCredentials } from './seed-accounts.js';
import {
  initHttpSuccessPolicy,
  pickActor,
  pickRandom,
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

const http429 = new Counter('http_429_total');
const httpFailed = new Counter('http_failed_total');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const STRESS_TEST_INDEX = __ENV.STRESS_TEST_INDEX || '1';

var seedCred = loadSeedCredentials();
var accounts = seedCred.accounts;
var bulkSharedPassword = seedCred.bulkSharedPassword;

export const options = {
  scenarios: {
    stress_ramp: {
      executor: 'ramping-arrival-rate',
      startRate: 4,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { duration: '90s', target: 8 },
        { duration: '3m', target: 16 },
        { duration: '4m', target: 24 },
        { duration: '2m', target: 10 },
        { duration: '2m', target: 6 },
      ],
      exec: 'stressMain',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<8000', 'p(99)<15000'],
    http_req_failed: ['rate<0.15'],
    http_failed_total: ['count<1200'],
  },
};

export function setup() {
  var tokens = {};
  for (var i = 0; i < accounts.length; i++) {
    var acc = accounts[i];
    var loginRes = http.post(
      BASE_URL + '/api/v1.0/auth/login',
      JSON.stringify({ email: acc.email, password: acc.password }),
      { headers: { 'Content-Type': 'application/json' }, tags: { name: 'StressAuth:' + acc.label } }
    );
    var token = accessTokenFromLoginBody(loginRes.body);
    var ok = check(loginRes, {
      ['stress login ' + acc.label]: function (r) {
        return r.status === 200 && !!token;
      },
    });
    if (!ok || !token) {
      throw new Error('stress setup login failed: ' + acc.email + ' status=' + loginRes.status);
    }
    tokens[acc.label] = token;
  }
  return { tokens: tokens, bulkSharedPassword: bulkSharedPassword };
}

export function teardown(data) {
  var adminTok = firstAdminToken(data.tokens);
  runK6TeardownCleanup(BASE_URL, adminTok);
}

function failIfBad(c) {
  if (!c) httpFailed.add(1);
}

export function stressMain(data) {
  var adminTok = firstAdminToken(data.tokens);
  if (!adminTok) {
    httpFailed.add(1);
    return;
  }

  var r = Math.random();
  if (r < 0.78) {
    stressReadHeavy(data, adminTok);
  } else if (r < 0.93) {
    stressFeedback(data, adminTok);
  } else {
    stressRegisterPurge(data);
  }

  sleep(0.01 + Math.random() * 0.04);
}

function stressReadHeavy(data, adminTok) {
  var actor = pickActor(data.tokens, 0.32);
  var headers = buildHeaders(actor.token);
  var opt = { headers: headers };
  var admin = actor.label.indexOf('admin') === 0;

  var meRes = log429(http429, http.get(BASE_URL + '/api/v1.0/auth/me', Object.assign({}, opt, { tags: { name: 'StressMe' } })));
  failIfBad(check(meRes, { 'me 200 or 429': okOr429 }));

  var util = log429(
    http429,
    http.get(BASE_URL + '/api/v1.0/utilityservices?pageNumber=1&pageSize=10', Object.assign({}, opt, { tags: { name: 'StressUtil' } }))
  );
  failIfBad(check(util, { 'util 200 or 429': okOr429 }));

  if (admin) {
    var apt = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/apartments?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'StressApt' } }))
    );
    failIfBad(check(apt, { 'apt 200 or 429': okOr429 }));
    var inv = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/invoices?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'StressInv' } }))
    );
    failIfBad(check(inv, { 'inv 200 or 429': okOr429 }));
    var res = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/residents?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'StressRes' } }))
    );
    failIfBad(check(res, { 'res 200 or 429': okOr429 }));
    var fb = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/feedbacks?pageNumber=1&pageSize=12', Object.assign({}, opt, { tags: { name: 'StressFb' } }))
    );
    failIfBad(check(fb, { 'fb 200 or 429': okOr429 }));
    if (Math.random() < 0.45) {
      var tr = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/feedbacks/tree', Object.assign({}, opt, { tags: { name: 'StressTree' } }))
      );
      failIfBad(check(tr, { 'tree 200 or 429': okOr429 }));
    }
  } else {
    var am = log429(http429, http.get(BASE_URL + '/api/v1.0/apartments/me', Object.assign({}, opt, { tags: { name: 'StressAptMe' } })));
    failIfBad(check(am, { 'apt me 200 or 429': okOr429 }));
    var im = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/invoices/me?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'StressInvMe' } }))
    );
    failIfBad(check(im, { 'inv me 200 or 429': okOr429 }));
    var rm = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/residents/me', Object.assign({}, opt, { tags: { name: 'StressResMe' } }))
    );
    failIfBad(check(rm, { 'res me 200 or 429': okOr429 }));
    var fm = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/feedbacks/me?pageNumber=1&pageSize=12', Object.assign({}, opt, { tags: { name: 'StressFbMe' } }))
    );
    failIfBad(check(fm, { 'fb me 200 or 429': okOr429 }));
  }

  if (adminTok && Math.random() < 0.2) {
    var perf = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/performance', {
        headers: { Authorization: 'Bearer ' + adminTok },
        tags: { name: 'StressPerf' },
      })
    );
    failIfBad(check(perf, { 'perf 200 or 429': okOr429 }));
  }
}

function stressFeedback(data, adminTok) {
  var labels = Object.keys(data.tokens).filter(function (l) {
    return l.indexOf('user') === 0;
  });
  if (!labels.length) labels = Object.keys(data.tokens);
  var label = pickRandom(labels);
  var token = data.tokens[label];
  var headers = buildHeaders(token);

  var createRes = log429(
    http429,
    http.post(
      BASE_URL + '/api/v1.0/feedbacks',
      JSON.stringify({ content: seedStyleFeedbackContent(label, __VU, __ITER) }),
      { headers: headers, tags: { name: 'StressFbCreate' } }
    )
  );
  failIfBad(check(createRes, { 'create 200/201/429': okCreateOr429 }));

  if (createRes.status !== 200 && createRes.status !== 201) return;

  var feedbackId = feedbackIdFromCreateBody(createRes.body);
  if (!feedbackId) {
    httpFailed.add(1);
    return;
  }

  var byId = log429(
    http429,
    http.get(BASE_URL + '/api/v1.0/feedbacks/' + feedbackId, {
      headers: { Authorization: 'Bearer ' + adminTok },
      tags: { name: 'StressFbById' },
    })
  );
  failIfBad(check(byId, { 'byId 200 or 429': okOr429 }));

  var delRes = log429(http429, deleteFeedbackWithRetry(BASE_URL, adminTok, feedbackId, 'StressFbDel'));
  failIfBad(check(delRes, { 'del 200/204/429': okDeleteOr429 }));
}

function stressRegisterPurge(data) {
  var adminTok = firstAdminToken(data.tokens);
  if (!adminTok) return;
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

export function handleSummary(data) {
  var now = new Date();
  var iso = now.toISOString().replace(/[:.]/g, '-');
  var fileName = buildReportOutputPath('stresstest', 'StressTest(' + STRESS_TEST_INDEX + ')-(' + iso + ').txt');

  var httpVals = {};
  if (data.metrics.http_reqs && data.metrics.http_reqs.values) httpVals = data.metrics.http_reqs.values;
  var r429 = undefined;
  if (data.metrics.http_429_total && data.metrics.http_429_total.values && data.metrics.http_429_total.values.count !== undefined) {
    r429 = data.metrics.http_429_total.values.count;
  } else if (data.metrics.http_429_total && data.metrics.http_429_total.count !== undefined) {
    r429 = data.metrics.http_429_total.count;
  }
  var rateStr = httpVals.rate !== undefined ? String(httpVals.rate) : 'n/a';
  var countStr = httpVals.count !== undefined ? String(httpVals.count) : 'n/a';

  var summary = ['StressTest(' + STRESS_TEST_INDEX + ') - ' + iso]
    .concat(formatHttpOutcome(data.metrics))
    .concat(formatCheckOutcome(data.root_group))
    .concat(formatRequestTiming(data.metrics))
    .concat(['Throughput (req/s): ' + rateStr, 'HTTP requests total: ' + countStr])
    .concat(formatRateLimit429Note(r429, 'http_429_total'))
    .concat([
      'Report path (relative to k6 cwd): ' + fileName,
      'stress_ramp: ramping-arrival-rate ~78% read-heavy, ~15% feedback+admin delete, ~7% register+purge.',
      'Teardown: quét feedback prefix k6 + user k6reg/k6adm.',
    ])
    .join('\n');

  console.log(summary);
  var out = {};
  out[fileName] = summary;
  return out;
}
