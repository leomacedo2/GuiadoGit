import { test, expect } from '@playwright/test';

test('turma agrupa evidências por aluno usando somente endpoints de snapshots', async ({ page }) => {
  const calls = [];
  const members = ['ana', 'bruno', 'carla'].map((username, index) => ({ gitHubProfileId: `id-${index}`, username, name: username,
    analyzedAt: '2026-09-01T00:00:00Z', skills: [index < 2 ? 'React' : 'Python'],
    dashboard: { technologies: [{ label: index < 2 ? 'React' : 'Python', count: 1 }], categories: [], commitActivity: null } }));
  const classroom = { id: 'class-1', name: 'Turma teste', members, createdAt: '2026-09-01', updatedAt: '2026-09-01', technologies: [], categories: [],
    commitActivity: { months: ['2026-09'], series: [{ name: 'React', counts: [10] }], warnings: [] } };
  await page.route('**/*', async route => {
    const request = route.request(); const url = new URL(request.url());
    if (url.origin === 'http://127.0.0.1:4173') return route.continue();
    if (url.origin !== 'http://api.example.test') return route.abort();
    const headers = { 'access-control-allow-origin': 'http://127.0.0.1:4173', 'access-control-allow-credentials': 'true',
      'access-control-allow-headers': 'authorization,content-type,x-session-request', 'access-control-allow-methods': 'GET,POST' };
    if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers });
    const path = url.pathname; calls.push(path);
    const reply = data => route.fulfill({ json: data, headers });
    if (path === '/api/auth/refresh') return reply({ accessToken: 'mock', expiresIn: 600 });
    if (path === '/api/auth/me') return reply({ id: 'account', nome: 'Teste' });
    if (path === '/api/github/connection') return reply({ connected: false });
    if (path === '/api/classes/class-1') return reply(classroom);
    const student = members.find(m => path === `/api/me/profiles/${m.gitHubProfileId}/analysis`);
    if (student) return reply({ profile: { username: student.username, repositories: [{ id: 1, name: `${student.username}-react`, description: 'Descrição persistida', url: 'https://github.com/example/demo' }, { id: 2, name: 'fora-da-tecnologia' }] },
      skills: [{ name: student.skills[0], evidence: [{ repositoryId: 1, reason: 'package.json: react' }] }] });
    return route.fulfill({ status: 500, headers, json: { detail: `Chamada inesperada: ${path}` } });
  });
  await page.goto('/turmas/class-1');
  await page.getByRole('link', { name: 'React — Ver repositórios', exact: true }).click();
  await expect(page).toHaveURL(/\/turmas\/class-1\/tecnologia\/React$/);
  await expect(page.getByRole('heading', { name: 'ana (@ana)', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'bruno (@bruno)', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'ana-react', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'bruno-react', exact: true })).toBeVisible();
  await expect(page.getByText('carla (@carla)', { exact: true })).toHaveCount(0);
  await expect(page.getByText('fora-da-tecnologia', { exact: true })).toHaveCount(0);
  await expect(page.getByText('2 repositório(s) encontrado(s)', { exact: false })).toBeVisible();
  expect(calls.filter(p => p.endsWith('/analysis'))).toEqual(['/api/me/profiles/id-0/analysis', '/api/me/profiles/id-1/analysis']);
  expect(calls.every(p => p.startsWith('/api/auth/') || p === '/api/github/connection' || p === '/api/classes/class-1' || p.startsWith('/api/me/profiles/'))).toBe(true);
  await expect(page.getByRole('navigation').getByRole('link', { name: /Ver repositórios/ })).toHaveCount(0);
});
