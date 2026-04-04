/**
 * k6 mặc định coi nhiều 4xx là failed (http_req_failed).
 * k6 v1.6 chỉ cho setResponseCallback(http.expectedStatuses(...)).
 * 429 = rate limit; 401 = login sai (spike); 404 = purge idempotent.
 */
import http from 'k6/http';
import { safeJsonParse } from './user-lifecycle.js';

export function initHttpSuccessPolicy() {
  http.setResponseCallback(
    http.expectedStatuses(
      { min: 200, max: 399 },
      400,
      401,
      404,
      429
    )
  );
}

export function pickRandom(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

export function buildHeaders(token) {
  return {
    Authorization: 'Bearer ' + token,
    'Content-Type': 'application/json',
  };
}

/** Admin token đầu tiên (label bắt đầu bằng admin). */
export function firstAdminToken(tokens) {
  var labels = Object.keys(tokens || {});
  var i;
  for (i = 0; i < labels.length; i++) {
    if (labels[i].indexOf('admin') === 0) return tokens[labels[i]];
  }
  return null;
}

export function pickActor(tokens, adminWeight) {
  var labels = Object.keys(tokens);
  var admins = labels.filter(function (x) {
    return x.indexOf('admin') === 0;
  });
  var users = labels.filter(function (x) {
    return x.indexOf('user') === 0;
  });
  var roll = Math.random();
  if (roll < adminWeight && admins.length > 0) {
    var a = pickRandom(admins);
    return { label: a, token: tokens[a] };
  }
  if (users.length === 0) {
    var a2 = pickRandom(labels);
    return { label: a2, token: tokens[a2] };
  }
  var u = pickRandom(users);
  return { label: u, token: tokens[u] };
}

/** Lấy id phần tử đầu từ body paged ApiOk (data.items). */
export function firstItemId(body) {
  var j = safeJsonParse(body);
  if (!j || !j.data) return null;
  var items = j.data.items || j.data.Items;
  if (!Array.isArray(items) || !items.length) return null;
  var row = items[0];
  return row.id || row.Id || row.userId || row.UserId || null;
}

export function log429(counter, res) {
  if (res && res.status === 429) counter.add(1);
  return res;
}

export function okOr429(r) {
  return r.status === 200 || r.status === 429;
}

/** GET: 200, 404 (không tìm thấy / race khi đọc theo id), 429 */
export function okGetOr429(r) {
  return r.status === 200 || r.status === 404 || r.status === 429;
}

export function okCreateOr429(r) {
  return r.status === 200 || r.status === 201 || r.status === 429;
}

export function okDeleteOr429(r) {
  return r.status === 200 || r.status === 204 || r.status === 429;
}
