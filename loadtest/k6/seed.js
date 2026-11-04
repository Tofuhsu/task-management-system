// Creates the dataset through the public API (provider-agnostic).
//   k6 run -e BASE_URL=http://localhost:8080 loadtest/k6/seed.js
// The API must allow USERS register calls per minute from one IP
// (AUTH_RATE_LIMIT / RateLimiting__Auth__PermitLimit).
import http from 'k6/http';
import { check, fail } from 'k6';
import exec from 'k6/execution';
import {
  BASE_URL, USERS, TASKS_PER_USER, PASSWORD, COOKIE_NAME, WORDS, STATUSES, PRIORITIES,
  emailFor, rng, pick, jsonParams,
} from './lib.js';

export const options = {
  scenarios: {
    seed: { executor: 'shared-iterations', vus: 10, iterations: USERS, maxDuration: '30m' },
  },
  thresholds: { checks: ['rate==1'] },
};

const BASE_DATE = Date.UTC(2026, 9, 1); // 2026-10-01, fixed so due dates are reproducible

export default function () {
  const i = exec.scenario.iterationInTest;
  const random = rng(1000 + i);

  const reg = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email: emailFor(i), password: PASSWORD }),
    jsonParams(null, 'POST /api/auth/register'),
  );
  if (!check(reg, { 'register 201': (r) => r.status === 201 })) {
    fail(`register failed for user ${i}: ${reg.status} ${reg.body}`);
  }
  const token = reg.cookies[COOKIE_NAME][0].value;

  for (let n = 0; n < TASKS_PER_USER; n++) {
    const title = `${pick(random, WORDS)} ${pick(random, WORDS)} #${n}`;
    const hasDue = random() < 0.7;
    const due = hasDue
      ? new Date(BASE_DATE + Math.floor(random() * 120) * 86400000).toISOString()
      : null;
    const res = http.post(
      `${BASE_URL}/api/tasks`,
      JSON.stringify({
        title,
        description: `${pick(random, WORDS)} ${pick(random, WORDS)} ${pick(random, WORDS)} notes`,
        status: pick(random, STATUSES),
        priority: pick(random, PRIORITIES),
        dueDate: due,
      }),
      jsonParams(token, 'POST /api/tasks'),
    );
    check(res, { 'create 201': (r) => r.status === 201 });
  }
}
