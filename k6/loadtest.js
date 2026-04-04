/**
 * Load test — benchmark ≥10k HTTP requests, đa route / đa tài khoản seed (seed-credentials.generated.json).
 * Giới hạn API: global authenticated 1200 req/phút; user ~150/phút; feedback user delete 10/giờ → xóa feedback bằng admin.
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { loadSeedCredentials } from './seed-accounts.js';
import {
  initHttpSuccessPolicy,
  pickActor,
  buildHeaders,
  firstAdminToken,
  firstItemId,
  log429,
  okGetOr429,
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
  adminCreateAndPurge,
  seedStyleFeedbackContent,
  accessTokenFromLoginBody,
  feedbackIdFromCreateBody,
  deleteFeedbackWithRetry,
} from './user-lifecycle.js';
import { runK6TeardownCleanup } from './cleanup.js';

initHttpSuccessPolicy();

const http429 = new Counter('http_429_total');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const LOAD_TEST_INDEX = __ENV.LOAD_TEST_INDEX || '1';

var seedCred = loadSeedCredentials();
var accounts = seedCred.accounts;
var bulkSharedPassword = seedCred.bulkSharedPassword;

/** ~2 iteration/s × 12m × ~7 req ≈ 10k+ (có thể tăng LOAD_DURATION, LOAD_RATE qua env). */
var LOAD_DURATION = __ENV.LOAD_DURATION || '12m';
var LOAD_RATE = __ENV.LOAD_RATE ? parseFloat(__ENV.LOAD_RATE) : 2;

export const options = {
  scenarios: {
    steady_mix: {
      executor: 'constant-arrival-rate',
      rate: LOAD_RATE,
      timeUnit: '1s',
      duration: LOAD_DURATION,
      preAllocatedVUs: 50,
      maxVUs: 120,
      exec: 'loadMain',
    },
    lifecycle_light: {
      executor: 'per-vu-iterations',
      exec: 'registerPurgeIteration',
      vus: 1,
      iterations: 12,
      startTime: '45s',
      maxDuration: '20m',
    },
  },
  // Không dùng counter + threshold riêng cho check: k6 đã có Check success %; ngưỡng <500 lần là quá chặt với hàng nghìn check.
  thresholds: {
    http_req_duration: ['p(95)<4000', 'p(99)<8000'],
    http_req_failed: ['rate<0.06'],
  },
};

export function setup() {
  var tokens = {};

  for (var i = 0; i < accounts.length; i++) {
    var acc = accounts[i];
    var loginRes = http.post(
      BASE_URL + '/api/v1.0/auth/login',
      JSON.stringify({ email: acc.email, password: acc.password }),
      { headers: { 'Content-Type': 'application/json' }, tags: { name: 'AuthLogin:' + acc.label } }
    );

    var token = accessTokenFromLoginBody(loginRes.body);

    var ok = check(loginRes, {
      ['login ' + acc.label + ' status 200']: function (r) {
        return r.status === 200;
      },
      ['login ' + acc.label + ' has token']: function () {
        return !!token;
      },
    });

    if (!ok || !token) {
      throw new Error(
        'setup login failed for ' + acc.email + ': status=' + loginRes.status + ' body=' + String(loginRes.body).slice(0, 300)
      );
    }

    tokens[acc.label] = token;
  }

  return { tokens: tokens, bulkSharedPassword: bulkSharedPassword };
}

export function teardown(data) {
  var adminTok = firstAdminToken(data.tokens);
  runK6TeardownCleanup(BASE_URL, adminTok);
}

export function loadMain(data) {
  var adminTok = firstAdminToken(data.tokens);
  if (!adminTok) {
    check(null, {
      'loadMain: có token admin từ setup': function () {
        return false;
      },
    });
    return;
  }

  if (Math.random() < 0.86) {
    readMixIteration(data, adminTok);
  } else {
    feedbackCreateAdminDelete(data, adminTok);
  }

  sleep(0.02 + Math.random() * 0.06);
}

function readMixIteration(data, adminTok) {
  var actor = pickActor(data.tokens, 0.28);
  var headers = buildHeaders(actor.token);
  var opt = { headers: headers };
  var admin = actor.label.indexOf('admin') === 0;

  var meRes = log429(http429, http.get(BASE_URL + '/api/v1.0/auth/me', Object.assign({}, opt, { tags: { name: 'LoadAuthMe' } })));
  check(meRes, { 'auth/me 200/404/429': okGetOr429 });

  if (admin) {
    var apt = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/apartments?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'LoadAptAdmin' } }))
    );
    check(apt, { 'apartments 200/404/429': okGetOr429 });
    var aptId = apt.status === 200 ? firstItemId(apt.body) : null;

    var inv = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/invoices?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'LoadInvAdmin' } }))
    );
    check(inv, { 'invoices 200/404/429': okGetOr429 });
    var invId = inv.status === 200 ? firstItemId(inv.body) : null;

    var res = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/residents?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'LoadResAdmin' } }))
    );
    check(res, { 'residents 200/404/429': okGetOr429 });
    var resId = res.status === 200 ? firstItemId(res.body) : null;

    var usr = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/users?pageNumber=1&pageSize=10', Object.assign({}, opt, { tags: { name: 'LoadUsersAdmin' } }))
    );
    check(usr, { 'users 200/404/429': okGetOr429 });

    var util = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/utilityservices?pageNumber=1&pageSize=12', Object.assign({}, opt, { tags: { name: 'LoadUtil' } }))
    );
    check(util, { 'utility 200/404/429': okGetOr429 });
    var utilId = util.status === 200 ? firstItemId(util.body) : null;

    var fbList = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/feedbacks?pageNumber=1&pageSize=15', Object.assign({}, opt, { tags: { name: 'LoadFbPaged' } }))
    );
    check(fbList, { 'feedbacks paged 200/404/429': okGetOr429 });

    if (Math.random() < 0.35) {
      var tree = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/feedbacks/tree', Object.assign({}, opt, { tags: { name: 'LoadFbTree' } }))
      );
      check(tree, { 'tree 200/404/429': okGetOr429 });
    }
    if (Math.random() < 0.3) {
      var flat = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/feedbacks/flattened', Object.assign({}, opt, { tags: { name: 'LoadFbFlat' } }))
      );
      check(flat, { 'flat 200/404/429': okGetOr429 });
    }

    if (aptId && Math.random() < 0.4) {
      var aptOne = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/apartments/' + aptId, Object.assign({}, opt, { tags: { name: 'LoadAptById' } }))
      );
      check(aptOne, { 'apartment by id 200/404/429': okGetOr429 });
    }
    if (invId && Math.random() < 0.35) {
      var invOne = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/invoices/' + invId, Object.assign({}, opt, { tags: { name: 'LoadInvById' } }))
      );
      check(invOne, { 'invoice by id 200/404/429': okGetOr429 });
    }
    if (resId && Math.random() < 0.35) {
      var resOne = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/residents/' + resId, Object.assign({}, opt, { tags: { name: 'LoadResById' } }))
      );
      check(resOne, { 'resident by id 200/404/429': okGetOr429 });
    }
    if (utilId && Math.random() < 0.35) {
      var uOne = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/utilityservices/' + utilId, Object.assign({}, opt, { tags: { name: 'LoadUtilById' } }))
      );
      check(uOne, { 'utility by id 200/404/429': okGetOr429 });
    }
  } else {
    var am = log429(http429, http.get(BASE_URL + '/api/v1.0/apartments/me', Object.assign({}, opt, { tags: { name: 'LoadAptMe' } })));
    check(am, { 'apartments/me 200/404/429': okGetOr429 });

    var im = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/invoices/me?pageNumber=1&pageSize=8', Object.assign({}, opt, { tags: { name: 'LoadInvMe' } }))
    );
    check(im, { 'invoices/me 200/404/429': okGetOr429 });

    var rm = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/residents/me', Object.assign({}, opt, { tags: { name: 'LoadResMe' } }))
    );
    check(rm, { 'residents/me 200/404/429': okGetOr429 });

    var util2 = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/utilityservices?pageNumber=1&pageSize=12', Object.assign({}, opt, { tags: { name: 'LoadUtilUser' } }))
    );
    check(util2, { 'utility user 200/404/429': okGetOr429 });

    var fme = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/feedbacks/me?pageNumber=1&pageSize=12', Object.assign({}, opt, { tags: { name: 'LoadFbMe' } }))
    );
    check(fme, { 'feedbacks/me 200/404/429': okGetOr429 });

    if (Math.random() < 0.08) {
      var treeU = log429(
        http429,
        http.get(BASE_URL + '/api/v1.0/feedbacks/tree', Object.assign({}, opt, { tags: { name: 'LoadFbTreeUser' } }))
      );
      check(treeU, { 'tree user 200/404/429': okGetOr429 });
    }
  }

  if (adminTok && Math.random() < 0.12) {
    var perf = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/performance', {
        headers: { Authorization: 'Bearer ' + adminTok },
        tags: { name: 'LoadPerformance' },
      })
    );
    check(perf, { 'performance 200/404/429': okGetOr429 });
  }
}

function feedbackCreateAdminDelete(data, adminTok) {
  var actor = pickActor(data.tokens, 0.15);
  var headers = buildHeaders(actor.token);
  var opt = { headers: headers };
  var content = seedStyleFeedbackContent(actor.label, __VU, __ITER);
  var createRes = log429(
    http429,
    http.post(BASE_URL + '/api/v1.0/feedbacks', JSON.stringify({ content: content }), Object.assign({}, opt, { tags: { name: 'LoadFbCreate' } }))
  );
  check(createRes, { 'feedback create ok or 429': okCreateOr429 });

  if (createRes.status !== 200 && createRes.status !== 201) return;

  var feedbackId = feedbackIdFromCreateBody(createRes.body);
  if (!feedbackId) {
    check(createRes.body, {
      'feedback create body có id (200/201)': function () {
        return false;
      },
    });
    return;
  }

  if (Math.random() < 0.5) {
    var byId = log429(
      http429,
      http.get(BASE_URL + '/api/v1.0/feedbacks/' + feedbackId, {
        headers: { Authorization: 'Bearer ' + adminTok },
        tags: { name: 'LoadFbGetByIdAdmin' },
      })
    );
    check(byId, { 'feedback getById 200/404/429': okGetOr429 });
  }

  var delRes = log429(http429, deleteFeedbackWithRetry(BASE_URL, adminTok, feedbackId, 'LoadFbDelAdmin'));
  check(delRes, { 'feedback del 200/204 or 429': okDeleteOr429 });
}

export function registerPurgeIteration(data) {
  var adminTok = firstAdminToken(data.tokens);
  if (!adminTok) return;

  var useRegister = __ITER % 2 === 0;
  var bundle = useRegister
    ? registerAndPurge(BASE_URL, adminTok, __VU, __ITER, data.bulkSharedPassword)
    : adminCreateAndPurge(BASE_URL, adminTok, __VU, __ITER, data.bulkSharedPassword);
  var createRes = useRegister ? bundle.reg : bundle.create;

  check(createRes, {
    'register/create 200/201/400/429': function (x) {
      return [200, 201, 400, 429].indexOf(x.status) >= 0;
    },
  });

  if (bundle.purge) {
    check(bundle.purge, {
      'purge 200/404/429': function (x) {
        return [200, 404, 429].indexOf(x.status) >= 0;
      },
    });
  }

  sleep(0.15 + Math.random() * 0.2);
}

export function handleSummary(data) {
  var now = new Date();
  var iso = now.toISOString().replace(/[:.]/g, '-');
  var fileName = buildReportOutputPath('loadtest', 'LoadTest(' + LOAD_TEST_INDEX + ')-(' + iso + ').txt');

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

  var summary = ['LoadTest(' + LOAD_TEST_INDEX + ') - ' + iso]
    .concat(formatHttpOutcome(data.metrics))
    .concat(formatCheckOutcome(data.root_group))
    .concat(formatRequestTiming(data.metrics))
    .concat(['Throughput (http_reqs rate, req/s): ' + rateStr, 'HTTP requests total: ' + countStr])
    .concat(formatRateLimit429Note(r429, 'http_429_total'))
    .concat([
      'Report path (relative to k6 cwd): ' + fileName,
      'steady_mix: constant-arrival-rate (LOAD_RATE=' + LOAD_RATE + '/s, LOAD_DURATION=' + LOAD_DURATION + '), đọc đa route + ~14% feedback create + admin delete.',
      'lifecycle_light: register|admin-create + purge (không để lại user tạm).',
      '429 không tính failed trong http_req_failed (initHttpSuccessPolicy).',
    ])
    .join('\n');

  console.log(summary);
  var out = {};
  out[fileName] = summary;
  return out;
}
