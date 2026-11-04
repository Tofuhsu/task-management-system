// Mixed read/write workload against a seeded dataset.
//   k6 run -e BASE_URL=http://localhost:8080 -e LABEL=sqlserver-baseline loadtest/k6/tasks.js
// Closed model: VUS virtual users loop with no think time for DURATION, so the
// reported req/s is the throughput the API sustains at that concurrency.
import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import { BASE_URL, USERS, WORDS, STATUSES, PRIORITIES, rng, pick, jsonParams, login } from './lib.js';

const VUS = Number(__ENV.VUS || 50);
const DURATION = __ENV.DURATION || '60s';
const LABEL = __ENV.LABEL || 'run';

export const options = {
  setupTimeout: '5m',
  scenarios: {
    mixed: { executor: 'constant-vus', vus: VUS, duration: DURATION, gracefulStop: '5s' },
  },
  summaryTrendStats: ['avg', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    // Per-endpoint trends show up in the summary because they have thresholds.
    'http_req_duration{name:GET /api/tasks}': ['p(95)<1000'],
    'http_req_duration{name:GET /api/tasks?search}': ['p(95)<1000'],
    'http_req_duration{name:GET /api/tasks/summary}': ['p(95)<1000'],
    'http_req_duration{name:POST /api/tasks}': ['p(95)<1000'],
    'http_req_duration{name:PATCH /api/tasks/{id}/status}': ['p(95)<1000'],
  },
};

export function setup() {
  const tokens = [];
  for (let i = 0; i < USERS; i++) tokens.push(login(i));
  return { tokens };
}

// Weights: 40% list, 20% search, 15% summary, 15% create, 10% status change.
export default function (data) {
  const vu = exec.vu.idInTest;
  const token = data.tokens[(vu - 1) % data.tokens.length];
  const random = rng(vu * 100000 + exec.vu.iterationInScenario);
  const roll = random();

  if (roll < 0.4) {
    const page = 1 + Math.floor(random() * 5);
    const res = http.get(
      `${BASE_URL}/api/tasks?page=${page}&pageSize=20&sortBy=CreatedAt&sortDirection=desc`,
      jsonParams(token, 'GET /api/tasks'),
    );
    check(res, { 'list 200': (r) => r.status === 200 });
  } else if (roll < 0.6) {
    const res = http.get(
      `${BASE_URL}/api/tasks?page=1&pageSize=20&search=${pick(random, WORDS)}`,
      jsonParams(token, 'GET /api/tasks?search'),
    );
    check(res, { 'search 200': (r) => r.status === 200 });
  } else if (roll < 0.75) {
    const res = http.get(`${BASE_URL}/api/tasks/summary`, jsonParams(token, 'GET /api/tasks/summary'));
    check(res, { 'summary 200': (r) => r.status === 200 });
  } else if (roll < 0.9) {
    const res = http.post(
      `${BASE_URL}/api/tasks`,
      JSON.stringify({
        title: `load ${pick(random, WORDS)}`,
        description: null,
        status: 'Todo',
        priority: pick(random, PRIORITIES),
        dueDate: null,
      }),
      jsonParams(token, 'POST /api/tasks'),
    );
    check(res, { 'create 201': (r) => r.status === 201 });
  } else {
    // Change the status of the newest task owned by this user.
    const list = http.get(
      `${BASE_URL}/api/tasks?page=1&pageSize=1`,
      jsonParams(token, 'GET /api/tasks'),
    );
    const id = list.status === 200 ? list.json('items.0.id') : null;
    if (id) {
      const res = http.patch(
        `${BASE_URL}/api/tasks/${id}/status`,
        JSON.stringify({ status: pick(random, STATUSES) }),
        jsonParams(token, 'PATCH /api/tasks/{id}/status'),
      );
      check(res, { 'status 204': (r) => r.status === 204 });
    }
  }
}

export function handleSummary(data) {
  const file = `loadtest/results/${LABEL}.json`;
  return { [file]: JSON.stringify(data, null, 2), stdout: textSummary(data) };
}

function textSummary(data) {
  const m = data.metrics;
  const fmt = (v) => (v === undefined ? '-' : v.toFixed(1));
  const lines = [
    `label=${LABEL} vus=${VUS} duration=${DURATION} base=${BASE_URL}`,
    `http_reqs: ${m.http_reqs.values.count} total, ${fmt(m.http_reqs.values.rate)} req/s`,
    `http_req_failed: ${(m.http_req_failed.values.rate * 100).toFixed(2)}%`,
    `http_req_duration ms: med=${fmt(m.http_req_duration.values.med)} p95=${fmt(m.http_req_duration.values['p(95)'])} p99=${fmt(m.http_req_duration.values['p(99)'])}`,
  ];
  for (const [key, metric] of Object.entries(m)) {
    if (key.startsWith('http_req_duration{name:')) {
      const v = metric.values;
      lines.push(`  ${key.slice(23, -1).padEnd(32)} med=${fmt(v.med)} p95=${fmt(v['p(95)'])} p99=${fmt(v['p(99)'])}`);
    }
  }
  return lines.join('\n') + '\n';
}
