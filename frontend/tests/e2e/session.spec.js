import { test, expect } from '@playwright/test';

// Browser routes are fully mocked; no GitHub, Supabase or public backend is contacted.
async function sessionApi(page, { persisted = false, expiredAccess = false, logoutFails = false, retriedStatus = 200, restoreFails = false } = {}) {
  let active = persisted;
  let access = 'access-login';
  const calls = [];
  await page.route('**/*', async route => {
    const request = route.request(); const url = new URL(request.url());
    if (url.origin === 'http://127.0.0.1:4173') return route.continue();
    if (url.origin !== 'http://api.example.test') return route.abort();
    const headers = { 'access-control-allow-origin': 'http://127.0.0.1:4173', 'access-control-allow-credentials': 'true',
      'access-control-allow-headers': 'authorization,content-type,x-session-request', 'access-control-allow-methods': 'GET,POST' };
    const reply = (data, status = 200) => route.fulfill({ status, json: data, headers });
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    const path = url.pathname;
    calls.push({ path, authorization: request.headers().authorization, sessionHeader: request.headers()['x-session-request'] });
    if (path === '/api/auth/login') { active = true; access = 'access-login'; return reply({ accessToken: access, expiresIn: 600 }); }
    if (path === '/api/auth/refresh') {
      if (!active || restoreFails) return reply({}, 401);
      access = `access-refreshed-${calls.length}`;
      return reply({ accessToken: access, expiresIn: 600 });
    }
    if (path === '/api/auth/me') return reply({ id: 'account-1', nome: 'Conta persistente' });
    if (path === '/api/auth/logout') {
      if (logoutFails) return reply({ detail: 'Falha simulada.' }, 503);
      active = false; return reply({});
    }
    if (path === '/api/me/profiles') {
      if (expiredAccess && request.headers().authorization === 'Bearer access-login') return reply({}, 401);
      return reply(retriedStatus === 200 ? [] : { detail: 'Falha simulada na requisição repetida.' }, retriedStatus);
    }
    return reply({}, 404);
  });
  return calls;
}
async function login(page) {
  await page.goto('/');
  await page.getByLabel('E-mail', { exact: true }).fill('session@example.test');
  await page.getByLabel('Senha', { exact: true }).fill('Fictional-Test123!');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/minhas-analises$/);
}

test('reload restaura a conta, não armazena bearer e logout impede nova restauração', async ({ page }) => {
  await page.addInitScript(() => {
    window.sessionRequests = [];
    const open = XMLHttpRequest.prototype.open;
    const send = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function(method, url, ...args) {
      this.sessionTestUrl = url;
      return open.call(this, method, url, ...args);
    };
    XMLHttpRequest.prototype.send = function(...args) {
      window.sessionRequests.push({ url: this.sessionTestUrl, credentials: this.withCredentials });
      return send.apply(this, args);
    };
  });
  const calls = await sessionApi(page);
  await login(page);
  await page.reload();
  await expect(page.getByText('Você ainda não possui análises salvas.')).toBeVisible();
  await expect(page.getByText('Conta persistente', { exact: true })).toBeVisible();
  expect(calls.filter(c => c.path === '/api/auth/refresh')).toHaveLength(2); // One on each page load, never a loop.
  expect(calls.filter(c => c.path === '/api/auth/refresh').every(c => c.sessionHeader === '1' && !c.authorization)).toBe(true);
  const storage = await page.evaluate(() => JSON.stringify({ local: { ...localStorage }, session: { ...sessionStorage } }));
  expect(storage).not.toContain('access-login'); expect(storage).not.toContain('access-refreshed');
  await page.getByRole('button', { name: 'Sair', exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
  const requests = await page.evaluate(() => window.sessionRequests);
  expect(requests.filter(r => /\/api\/auth\/(refresh|logout)$/.test(r.url)).every(r => r.credentials)).toBe(true);
  expect(requests.filter(r => /\/api\/(auth\/me|me\/profiles)$/.test(r.url)).every(r => !r.credentials)).toBe(true);
  await page.reload();
  await expect(page.getByRole('button', { name: 'Entrar', exact: true })).toBeVisible();
  expect(calls.filter(c => c.path === '/api/auth/logout')).toHaveLength(1);
});

test('sessão existente restaura antes de decidir o redirecionamento da rota privada', async ({ page }) => {
  const calls = await sessionApi(page, { persisted: true });
  await page.goto('/minhas-analises');
  await expect(page.getByText('Você ainda não possui análises salvas.')).toBeVisible();
  await expect(page).toHaveURL(/\/minhas-analises$/);
  expect(calls.filter(c => c.path === '/api/auth/refresh')).toHaveLength(1);
  expect(calls.filter(c => c.path === '/api/auth/me')).toHaveLength(1);
});

test('sem cookie válido mantém visitante com uma única tentativa silenciosa', async ({ page }) => {
  const calls = await sessionApi(page);
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Entrar', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Continuar como visitante' }).click();
  await expect(page).toHaveURL(/\/analisar$/);
  expect(calls.filter(c => c.path === '/api/auth/refresh')).toHaveLength(1);
  expect(calls.filter(c => c.path === '/api/auth/me')).toHaveLength(0);
});

test('401 renova uma vez e repete a requisição com novo bearer', async ({ page }) => {
  const calls = await sessionApi(page, { expiredAccess: true });
  await login(page);
  await expect(page.getByText('Você ainda não possui análises salvas.')).toBeVisible();
  expect(calls.filter(c => c.path === '/api/auth/refresh')).toHaveLength(2);
  const profiles = calls.filter(c => c.path === '/api/me/profiles');
  expect(profiles).toHaveLength(2);
  expect(profiles[0].authorization).toBe('Bearer access-login');
  expect(profiles[1].authorization).toMatch(/^Bearer access-refreshed-/);
});

test('segundo 401 encerra estado local sem refresh infinito', async ({ page }) => {
  const calls = await sessionApi(page, { expiredAccess: true, retriedStatus: 401 });
  await login(page);
  await expect(page.getByText('Entre para ver os perfis salvos', { exact: false })).toBeVisible();
  expect(calls.filter(c => c.path === '/api/auth/refresh')).toHaveLength(2);
  expect(calls.filter(c => c.path === '/api/me/profiles')).toHaveLength(2);
});

test('erro de negócio depois do refresh preserva a sessão renovada', async ({ page }) => {
  const calls = await sessionApi(page, { expiredAccess: true, retriedStatus: 503 });
  await login(page);
  await expect(page.getByText('Falha simulada na requisição repetida.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Sair', exact: true })).toBeVisible();
  expect(calls.filter(c => c.path === '/api/auth/refresh')).toHaveLength(2);
});

test('logout indisponível informa falha e não anuncia encerramento que não foi confirmado', async ({ page }) => {
  await sessionApi(page, { logoutFails: true });
  await login(page);
  await page.getByRole('button', { name: 'Sair', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Não foi possível encerrar a sessão.');
  await expect(page.getByText('Conta persistente', { exact: true })).toBeVisible();
});
