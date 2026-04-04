/**
 * k6-only users: register or admin-create, then purge. Payload shapes match SeedData patterns.
 * Password: bulkSharedPassword from seed-credentials.generated.json.
 */
import http from 'k6/http';
import { sleep } from 'k6';

export function safeJsonParse(text) {
  if (!text || typeof text !== 'string') return null;
  try {
    return JSON.parse(text);
  } catch (e) {
    return null;
  }
}

export function accessTokenFromLoginBody(body) {
  var b = safeJsonParse(body);
  if (!b || !b.data || !b.data.tokens) return null;
  var t = b.data.tokens;
  return t.accessToken || t.AccessToken || null;
}

export function feedbackIdFromCreateBody(body) {
  var j = safeJsonParse(body);
  if (!j || !j.data) return null;
  return j.data.id || j.data.Id || null;
}

/** Prefix for k6-created feedback (admin search cleanup + teardown). */
export var K6_FEEDBACK_SEARCH = 'Building notice thread k6';

export function seedStyleFeedbackContent(label, vu, iter) {
  return K6_FEEDBACK_SEARCH + ' ' + label + ' vu' + vu + ' iter' + iter + ' ' + Date.now();
}

function k6PhoneResident(vu, iter) {
  var n = 300000 + ((vu * 997 + iter * 13) % 69999);
  return '+84901' + String(n).padStart(6, '0');
}

function k6PhoneAdminCreate(vu, iter) {
  var n = 310000 + ((vu * 991 + iter * 17) % 59999);
  return '+84901' + String(n).padStart(6, '0');
}

function httpDelWithRetry(url, bearerToken, name, retries) {
  if (retries === undefined) retries = 3;
  var headers = {};
  if (bearerToken) headers.Authorization = 'Bearer ' + bearerToken;
  var res = null;
  for (var a = 0; a < retries; a++) {
    res = http.del(url, null, { headers: headers, tags: { name: name } });
    if (res.status !== 429) return res;
    sleep(0.3 + a * 0.2);
  }
  return res;
}

export function registerAndPurge(baseUrl, adminToken, vu, iter, bulkSharedPassword, nowMs) {
  if (nowMs === undefined || nowMs === null) nowMs = Date.now();
  var email = 'k6reg-v' + vu + '-i' + iter + '-' + nowMs + '@seed.local';
  var reg = httpPostJson(
    baseUrl + '/api/v1.0/Auth/register',
    {
      email: email,
      password: bulkSharedPassword,
      fullName: 'Guest resident k6-' + vu + '-' + iter,
      phoneNumber: k6PhoneResident(vu, iter),
    },
    null,
    'K6Register'
  );

  var regJson = safeJsonParse(reg.body);
  var uid = regJson && regJson.data && (regJson.data.userId || regJson.data.UserId);
  var purge = null;
  if ((reg.status === 200 || reg.status === 201) && uid && adminToken) {
    purge = httpDelWithRetry(baseUrl + '/api/v1.0/Users/' + uid + '/purge', adminToken, 'K6PurgeUser', 4);
  }
  return { reg: reg, purge: purge, email: email, uid: uid };
}

export function adminCreateAndPurge(baseUrl, adminToken, vu, iter, bulkSharedPassword, nowMs) {
  if (nowMs === undefined || nowMs === null) nowMs = Date.now();
  var email = 'k6adm-v' + vu + '-i' + iter + '-' + nowMs + '@seed.local';
  var create = httpPostJson(
    baseUrl + '/api/v1.0/Users',
    {
      email: email,
      password: bulkSharedPassword,
      fullName: 'Resident Demo k6-' + vu + '-' + iter,
      phoneNumber: k6PhoneAdminCreate(vu, iter),
      roles: ['User'],
    },
    adminToken,
    'K6AdminCreateUser'
  );

  var cj = safeJsonParse(create.body);
  var uid = cj && cj.data && (cj.data.userId || cj.data.UserId);
  var purge = null;
  if ((create.status === 200 || create.status === 201) && uid && adminToken) {
    purge = httpDelWithRetry(baseUrl + '/api/v1.0/Users/' + uid + '/purge', adminToken, 'K6PurgeAfterAdminCreate', 4);
  }
  return { create: create, purge: purge, email: email, uid: uid };
}

export function deleteFeedbackWithRetry(baseUrl, token, feedbackId, tagName) {
  if (!feedbackId) return null;
  var headers = {
    Authorization: 'Bearer ' + token,
  };
  var res = null;
  for (var i = 0; i < 4; i++) {
    res = http.del(baseUrl + '/api/v1.0/feedbacks/' + feedbackId, null, {
      headers: headers,
      tags: { name: tagName || 'K6FeedbackDel' },
    });
    if (res.status === 200 || res.status === 204) return res;
    if (res.status !== 429) return res;
    sleep(0.25 + i * 0.15);
  }
  return res;
}

function httpPostJson(url, body, bearerToken, name) {
  var headers = { 'Content-Type': 'application/json' };
  if (bearerToken) headers.Authorization = 'Bearer ' + bearerToken;
  return http.post(url, JSON.stringify(body), { headers: headers, tags: { name: name } });
}
