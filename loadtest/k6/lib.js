import http from 'k6/http';
import { check, fail } from 'k6';

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

// Named dataset: USERS x TASKS_PER_USER. Defaults are the "ds-100x500" dataset
// (100 users x 500 tasks = 50,000 tasks) used in docs/PERFORMANCE.md.
export const USERS = Number(__ENV.USERS || 100);
export const TASKS_PER_USER = Number(__ENV.TASKS_PER_USER || 500);

// Test-only credentials for a throwaway local database.
export const PASSWORD = 'Loadtest-Password-123!';
export const emailFor = (i) => `loadtest-user-${i}@example.test`;

export const COOKIE_NAME = 'task_manager_access_token';

export const WORDS = [
  'backend', 'frontend', 'invoice', 'review', 'deploy', 'meeting', 'report',
  'refactor', 'database', 'design', 'budget', 'hiring', 'release', 'bugfix',
  'roadmap', 'security', 'customer', 'migration', 'testing', 'docs',
];
export const STATUSES = ['Todo', 'InProgress', 'Done', 'Archived'];
export const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

// Small deterministic PRNG (mulberry32) so the same seed yields the same dataset.
export function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export const pick = (random, items) => items[Math.floor(random() * items.length)];

export const jsonParams = (token, name) => ({
  headers: {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  },
  tags: { name },
});

// Returns the JWT from the auth cookie so later requests can use a Bearer header
// (the cookie is Secure, which k6 would not resend over plain HTTP).
export function login(i) {
  const res = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: emailFor(i), password: PASSWORD }),
    jsonParams(null, 'POST /api/auth/login'),
  );
  if (!check(res, { 'login 200': (r) => r.status === 200 })) {
    fail(`login failed for user ${i}: ${res.status} ${res.body}`);
  }
  return res.cookies[COOKIE_NAME][0].value;
}
