/**
 * Shared k6 handleSummary helpers (ES syntax compatible with older k6/goja).
 */

/**
 * Đường dẫn output cho handleSummary (relative tới thư mục làm việc khi chạy k6).
 * Luôn dùng thư mục con reports: mặc định root = "reports" → chạy từ trong k6/ sẽ ghi vào k6/reports/...
 * Nếu chạy từ repo root: k6 run -e K6_REPORT_ROOT=k6/reports k6/loadtest.js
 */
export function buildReportOutputPath(subdir, fileName) {
  var root = __ENV.K6_REPORT_ROOT;
  if (root === undefined || root === null || String(root).trim() === '') {
    root = 'reports';
  }
  root = String(root).replace(/\\/g, '/').replace(/\/+$/, '');
  subdir = String(subdir || '')
    .replace(/\\/g, '/')
    .replace(/^\/+|\/+$/g, '');
  fileName = String(fileName || 'report.txt').replace(/^\/+/, '');
  return root + '/' + subdir + '/' + fileName;
}

export function formatHttpOutcome(metrics) {
  var httpReqCount =
    metrics.http_reqs && metrics.http_reqs.values ? metrics.http_reqs.values.count : undefined;
  var fv = metrics.http_req_failed ? metrics.http_req_failed.values : undefined;
  var lines = [];

  if (fv && typeof fv.passes === 'number' && typeof fv.fails === 'number') {
    var failedReq = fv.passes;
    var okReq = fv.fails;
    var total = failedReq + okReq;
    if (total > 0) {
      var okPct = (okReq / total) * 100;
      var failPct = (failedReq / total) * 100;
      lines.push('HTTP success (2xx/3xx, k6 default): ' + okPct.toFixed(2) + '% (' + okReq + '/' + total + ' requests)');
      lines.push('HTTP failure (0 or 4xx–5xx): ' + failPct.toFixed(2) + '% (' + failedReq + '/' + total + ' requests)');
    }
  } else if (fv && typeof fv.rate === 'number' && typeof httpReqCount === 'number' && httpReqCount > 0) {
    var failRate = fv.rate;
    var failN = Math.round(failRate * httpReqCount);
    var okN = Math.max(0, httpReqCount - failN);
    lines.push('HTTP success (from fail rate): ' + ((1 - failRate) * 100).toFixed(2) + '% (~' + okN + '/' + httpReqCount + ' requests)');
    lines.push('HTTP failure (from fail rate): ' + (failRate * 100).toFixed(2) + '% (~' + failN + '/' + httpReqCount + ' requests)');
  } else {
    lines.push('HTTP success/failure %: n/a (missing http_req_failed or http_reqs metrics)');
  }

  if (typeof httpReqCount === 'number') {
    lines.push('HTTP requests total (http_reqs): ' + httpReqCount);
  }

  return lines;
}

function sumChecksFromGroup(group) {
  var passes = 0;
  var fails = 0;
  if (!group) return { passes: passes, fails: fails };

  if (Array.isArray(group.checks)) {
    for (var i = 0; i < group.checks.length; i++) {
      var c = group.checks[i];
      passes += typeof c.passes === 'number' ? c.passes : 0;
      fails += typeof c.fails === 'number' ? c.fails : 0;
    }
  }
  if (Array.isArray(group.groups)) {
    for (var g = 0; g < group.groups.length; g++) {
      var s = sumChecksFromGroup(group.groups[g]);
      passes += s.passes;
      fails += s.fails;
    }
  }
  return { passes: passes, fails: fails };
}

export function formatCheckOutcome(rootGroup) {
  var s = sumChecksFromGroup(rootGroup);
  var passes = s.passes;
  var fails = s.fails;
  var total = passes + fails;
  if (total === 0) {
    return ['Checks: no results'];
  }
  var okPct = (passes / total) * 100;
  var badPct = (fails / total) * 100;
  return [
    'Check success (script expectations): ' + okPct.toFixed(2) + '% (' + passes + '/' + total + ')',
    'Check failure: ' + badPct.toFixed(2) + '% (' + fails + '/' + total + ')',
  ];
}

export function formatRequestTiming(metrics) {
  var dur = metrics.http_req_duration && metrics.http_req_duration.values ? metrics.http_req_duration.values : {};
  var wait = metrics.http_req_waiting && metrics.http_req_waiting.values ? metrics.http_req_waiting.values : {};
  var send = metrics.http_req_sending && metrics.http_req_sending.values ? metrics.http_req_sending.values : {};
  var recv = metrics.http_req_receiving && metrics.http_req_receiving.values ? metrics.http_req_receiving.values : {};
  var lines = [];
  var fmt = function (label, v) {
    var a = v.avg;
    var p95 = v['p(95)'];
    var p99 = v['p(99)'];
    var aStr = a !== undefined && a && typeof a.toFixed === 'function' ? a.toFixed(2) + ' ms' : a !== undefined ? String(a) : 'n/a';
    var p95Str =
      p95 !== undefined && p95 && typeof p95.toFixed === 'function' ? p95.toFixed(2) + ' ms' : p95 !== undefined ? String(p95) : 'n/a';
    var p99Str =
      p99 !== undefined && p99 && typeof p99.toFixed === 'function' ? p99.toFixed(2) + ' ms' : p99 !== undefined ? String(p99) : 'n/a';
    lines.push(label + ' — avg: ' + aStr + ', p95: ' + p95Str + ', p99: ' + p99Str);
  };
  if (Object.keys(dur).length) fmt('Round-trip (http_req_duration)', dur);
  if (Object.keys(wait).length) fmt('Waiting / TTFB (http_req_waiting)', wait);
  if (Object.keys(send).length) fmt('Sending (http_req_sending)', send);
  if (Object.keys(recv).length) fmt('Receiving (http_req_receiving)', recv);
  return lines.length ? lines : ['Request timing breakdown: n/a'];
}

export function formatRateLimit429Note(count429, counterName) {
  if (counterName === undefined || counterName === null) counterName = '429 counter';
  var n = count429 !== undefined && count429 !== null ? count429 : 'n/a';
  return [
    'HTTP 429 (' + counterName + '): ' + n,
    'Rate limiting: 429 responses indicate the API quota/rate limit is working as designed — not a random server fault. Scripts treat 429 as success where limits are intentionally exercised.',
  ];
}
