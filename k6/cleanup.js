/**
 * Teardown: remove k6-created feedbacks (content prefix) and orphan k6reg-/k6adm- users (admin purge).
 * Uses only APIs; safe to run multiple times.
 */
import http from 'k6/http';
import { sleep } from 'k6';
import { safeJsonParse, K6_FEEDBACK_SEARCH } from './user-lifecycle.js';

function unwrapData(body) {
  var j = safeJsonParse(body);
  if (!j || !j.data) return null;
  return j.data;
}

function getItems(body) {
  var d = unwrapData(body);
  if (!d) return [];
  return d.items || d.Items || [];
}

/**
 * @param {string} baseUrl
 * @param {string} adminToken
 */
export function runK6TeardownCleanup(baseUrl, adminToken) {
  if (!baseUrl || !adminToken) return;
  var headers = { Authorization: 'Bearer ' + adminToken };

  var search = encodeURIComponent(K6_FEEDBACK_SEARCH);
  var page = 1;
  var maxPages = 30;
  while (page <= maxPages) {
    var listUrl =
      baseUrl +
      '/api/v1.0/feedbacks?pageNumber=' +
      page +
      '&pageSize=100&search=' +
      search;
    var res = http.get(listUrl, { headers: headers, tags: { name: 'TeardownFeedbackList' } });
    if (res.status !== 200) break;
    var items = getItems(res.body);
    if (!items.length) break;
    for (var i = 0; i < items.length; i++) {
      var id = items[i].id || items[i].Id;
      if (!id) continue;
      var del = http.del(baseUrl + '/api/v1.0/feedbacks/' + id, null, {
        headers: headers,
        tags: { name: 'TeardownFeedbackDel' },
      });
      if (del.status === 429) sleep(0.4);
    }
    if (items.length < 100) break;
    page++;
  }

  for (var p = 1; p <= 50; p++) {
    var uUrl = baseUrl + '/api/v1.0/users?pageNumber=' + p + '&pageSize=100';
    var ur = http.get(uUrl, { headers: headers, tags: { name: 'TeardownUserList' } });
    if (ur.status !== 200) break;
    var users = getItems(ur.body);
    if (!users.length) break;
    for (var u = 0; u < users.length; u++) {
      var row = users[u];
      var email = row.email || row.Email || '';
      var uid = row.userId || row.UserId;
      if (!uid || !email) continue;
      if (email.indexOf('k6reg-') >= 0 || email.indexOf('k6adm-') >= 0) {
        var pr = http.del(baseUrl + '/api/v1.0/Users/' + uid + '/purge', null, {
          headers: headers,
          tags: { name: 'TeardownPurgeK6User' },
        });
        if (pr.status === 429) sleep(0.4);
      }
    }
  }
}
