import { test, expect } from '@playwright/test';

const avatar = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" rx="50" fill="#365bbc"/><text x="50" y="65" text-anchor="middle" fill="white" font-size="40">GD</text></svg>');
function snapshot(username = 'perfil-teste') {
  const names = [['JavaScript', 'Linguagens', 3], ['TypeScript', 'Linguagens', 2], ['React', 'Frontend', 2], ['C#', 'Linguagens', 1], ['ASP.NET Core', 'Backend', 1], ['PostgreSQL', 'Dados', 1]];
  const skills = names.map(([name, category, count]) => ({ name, category, repositoryCount: count, evidenceLevel: count === 3 ? 'Boa evidência' : count === 2 ? 'Em desenvolvimento' : 'Pouca evidência',
    evidence: Array.from({ length: count }, (_, i) => ({ repositoryId: i + 1, repositoryName: `projeto-${i + 1}`, repositoryUrl: 'https://github.com/example/demo', reason: 'Dependência declarada no manifesto.' })) }));
  return {
    id: 'analysis-1', analysisVersion: 2, profile: { username, name: 'Perfil de Demonstração', avatarUrl: avatar, profileUrl: 'https://github.com/example', bio: 'Projetos públicos de desenvolvimento web.', publicRepositories: 3,
      repositories: Array.from({ length: 3 }, (_, i) => ({ id: i + 1, name: `projeto-${i + 1}`, language: 'JavaScript', description: 'Projeto de teste com dados simulados.', url: 'https://github.com/example/demo', updatedAt: '2026-09-23T12:00:00Z' })) },
    analyzedRepositories: 3, inspectedRepositories: 3, completelyInspectedRepositories: 3, isPartial: false, warnings: [], source: 'cache', currentGitHubRequests: 0, totalGitHubRequests: 9,
    analyzedAt: '2026-09-23T12:00:00Z', expiresAt: '2026-09-23T18:00:00Z', skills, recommendations: [],
    learningTracks: [{ name: 'Frontend React', area: 'Frontend', demonstratedSkills: ['JavaScript', 'TypeScript', 'React'], nextSteps: [{ topic: 'Testes de frontend', reason: 'React aparece em dois repositórios e ainda não há ferramentas de testes.', nextStep: 'Teste uma interação com loading e erro.', consideredSkills: ['React'] }] },
      { name: 'Backend .NET', area: 'Backend', demonstratedSkills: ['C#', 'ASP.NET Core'], nextSteps: [{ topic: 'Web API', reason: 'Há infraestrutura ASP.NET Core.', nextStep: 'Documente endpoints e validações.', consideredSkills: ['ASP.NET Core'] }] },
      { name: 'Full Stack', area: 'Full Stack', demonstratedSkills: ['React', 'ASP.NET Core', 'PostgreSQL'], nextSteps: [{ topic: 'Testes de integração', reason: 'Há sinais de interface e backend.', nextStep: 'Teste uma operação entre interface e API.', consideredSkills: ['React', 'ASP.NET Core'] }] }]
  };
}
async function mockApi(page, saved = false) {
  const calls = [];
  let profiles = saved ? [{ gitHubProfileId: 'saved-1', username: 'perfil-teste', nome: 'Perfil de Demonstração', avatarUrl: avatar, latest: { analyzedAt: '2026-09-23T12:00:00Z', isComplete: true, skills: ['React'] }, isExpired: false }] : [];
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (path.startsWith('/api/')) {
      calls.push({ path, method: route.request().method(), authorization: route.request().headers().authorization });
      const reply = data => route.fulfill({ json: data, headers: { 'access-control-allow-origin': '*', 'access-control-allow-headers': 'authorization,content-type' } });
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: { 'access-control-allow-origin': '*', 'access-control-allow-headers': 'authorization,content-type', 'access-control-allow-methods': 'GET,POST,PUT,DELETE' } });
      if (path === '/api/auth/login') return reply({ accessToken: 'platform-test-only', expiresIn: 1800 });
      if (path === '/api/auth/me') return reply({ id: 'account-1', nome: 'Conta de teste', email: 'test@example.test' });
      if (path === '/api/auth/register') return reply({ id: 'account-1' });
      if (path === '/api/me/profiles') return reply(profiles);
      if (path === '/api/github/connection') return reply({ enabled: true, connected: false });
      if (path === '/api/me/profiles/saved-1/analysis') return reply(snapshot());
      if (path === '/api/me/profiles/saved-1' && route.request().method() === 'DELETE') { profiles = []; return reply({}); }
      if (path.startsWith('/api/analyses/')) {
        const result = snapshot(decodeURIComponent(path.split('/')[3]));
        if (path.endsWith('/refresh')) { result.source = 'github'; result.currentGitHubRequests = 9; }
        return reply(result);
      }
      return route.fulfill({ status: 404, json: { detail: 'Endpoint não simulado' } });
    }
    if (url.origin === 'http://127.0.0.1:4173') return route.continue();
    // Test browser never reaches GitHub, Supabase, Render or any other external host.
    return route.abort();
  });
  return calls;
}
async function nav(page, label) {
  const menu = page.getByRole('button', { name: 'Menu', exact: true });
  if (await menu.isVisible() && await menu.getAttribute('aria-expanded') === 'false') await menu.click();
  await page.getByRole('navigation').getByRole('link', { name: label, exact: true }).click();
}
async function login(page) {
  await page.goto('/');
  await page.getByLabel('E-mail', { exact: true }).fill('test@example.test');
  await page.getByLabel('Senha', { exact: true }).fill('Fictional-Test123!');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/minhas-analises$/);
}
async function noOverflow(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
}
test('entrada limpa e visitante segue analisar → perfil, sem chamadas duplicadas ao navegar', async ({ page }) => {
  const calls = await mockApi(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'GuiaDoGit', exact: true })).toBeVisible();
  await expect(page.getByRole('navigation')).toHaveCount(0);
  await page.getByRole('link', { name: 'Continuar como visitante' }).click();
  await expect(page).toHaveURL(/\/analisar$/);
  const input = page.getByLabel('Username do GitHub');
  await expect(input).toHaveValue('');
  await expect(input).toHaveAttribute('placeholder', 'Digite um usuário do GitHub');
  await input.fill('perfil-teste');
  await page.getByRole('button', { name: 'Analisar', exact: true }).click();
  await expect(page).toHaveURL(/\/perfil\/perfil-teste$/);
  await expect(page.getByRole('heading', { name: 'Perfil de Demonstração' })).toBeVisible();
  await expect(page.getByLabel('Username do GitHub')).toHaveCount(0);
  await nav(page, 'Skills'); await expect(page.getByRole('heading', { name: 'Visão geral das Skills' })).toBeVisible();
  await nav(page, 'Recomendações'); await expect(page.getByRole('heading', { name: 'Trilhas e recomendações' })).toBeVisible();
  await nav(page, 'Repositórios'); await expect(page.getByRole('heading', { name: 'Repositórios', exact: true })).toBeVisible();
  expect(calls.filter(c => c.path.startsWith('/api/analyses/'))).toHaveLength(1);
  expect(calls.find(c => c.path.startsWith('/api/analyses/')).authorization).toBeUndefined();
});
test('login abre lista vazia, primeira análise e logout preservam fluxo existente', async ({ page }) => {
  await mockApi(page);
  await login(page);
  await expect(page.getByText('Você ainda não possui análises salvas.')).toBeVisible();
  await page.getByRole('link', { name: 'Fazer minha primeira análise' }).click();
  await expect(page).toHaveURL(/\/analisar$/);
  await page.getByRole('button', { name: 'Sair', exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
  await page.goto('/minhas-analises');
  await expect(page.getByText('Entre para ver os perfis salvos', { exact: false })).toBeVisible();
});
test('lista abre snapshot sem coleta, atualiza por POST e remove somente vínculo', async ({ page }) => {
  const calls = await mockApi(page, true);
  await login(page);
  await page.getByRole('button', { name: 'Ver perfil', exact: true }).click();
  await expect(page).toHaveURL(/\/perfil\/perfil-teste$/);
  expect(calls.filter(c => c.path.startsWith('/api/analyses/'))).toHaveLength(0);
  await nav(page, 'Minhas análises');
  await page.getByRole('button', { name: 'Atualizar', exact: true }).click();
  await expect(page).toHaveURL(/\/perfil\/perfil-teste$/);
  expect(calls.filter(c => c.path.endsWith('/refresh') && c.method === 'POST')).toHaveLength(1);
  await nav(page, 'Minhas análises');
  await page.getByRole('button', { name: 'Remover', exact: true }).click();
  await expect(page.getByText('Você ainda não possui análises salvas.')).toBeVisible();
  expect(calls.filter(c => c.method === 'DELETE')).toEqual([expect.objectContaining({ path: '/api/me/profiles/saved-1' })]);
});
test('rota direta e reload resolvem o perfil indicado, incluindo skills com contexto na URL', async ({ page }) => {
  const calls = await mockApi(page);
  await page.goto('/perfil/segundo-perfil');
  await expect(page.getByRole('link', { name: '@segundo-perfil', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('link', { name: '@segundo-perfil', exact: true })).toBeVisible();
  await page.goto('/skills?perfil=terceiro-perfil');
  await expect(page.getByRole('link', { name: '@terceiro-perfil', exact: true })).toBeVisible();
  await page.goto('/perfil/constructor');
  await expect(page.getByRole('link', { name: '@constructor', exact: true })).toBeVisible();
  expect(calls.filter(c => c.path === '/api/analyses/segundo-perfil')).toHaveLength(2);
});
for (const theme of ['dark', 'light']) {
  test(`gráficos, filtros e layout responsivo no tema ${theme}`, async ({ page }, testInfo) => {
    await mockApi(page);
    await page.addInitScript(value => localStorage.setItem('portfolio-theme', value), theme);
    await page.goto('/');
    await noOverflow(page);
    await page.screenshot({ path: testInfo.outputPath(`entrada-${theme}.png`), fullPage: true });
    await page.goto('/perfil/perfil-teste');
    await expect(page.locator('canvas')).toHaveCount(2);
    await expect(page.locator('html')).toHaveAttribute('data-bs-theme', theme);
    await noOverflow(page);
    await page.screenshot({ path: testInfo.outputPath(`perfil-${theme}.png`), fullPage: true });
    const languages = page.getByRole('region', { name: 'Linguagens mais evidenciadas', exact: true });
    await languages.getByText('Ver dados em tabela', { exact: true }).click();
    await expect(languages.getByRole('row', { name: 'JavaScript 3' })).toBeVisible();
    await nav(page, 'Skills');
    await expect(page.locator('canvas')).toHaveCount(1);
    await page.locator('summary').filter({ hasText: /^Frontend/ }).click();
    await noOverflow(page);
    await page.screenshot({ path: testInfo.outputPath(`skills-${theme}.png`), fullPage: true });
    await nav(page, 'Recomendações');
    await page.getByRole('button', { name: 'Full Stack', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Full Stack', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('heading', { name: 'Full Stack', exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Frontend React', exact: true })).toHaveCount(0);
    await noOverflow(page);
    await page.screenshot({ path: testInfo.outputPath(`trilhas-${theme}.png`), fullPage: true });
    await page.getByRole('button', { name: 'Modo escuro', exact: true }).click();
    await expect(page.locator('html')).toHaveAttribute('data-bs-theme', theme === 'dark' ? 'light' : 'dark');
    await nav(page, 'Skills');
    await expect(page.locator('canvas')).toHaveCount(1);
    await page.getByRole('button', { name: 'Modo escuro', exact: true }).click();
    await expect(page.locator('html')).toHaveAttribute('data-bs-theme', theme);
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-bs-theme', theme);
    await expect(page.locator('canvas')).toHaveCount(1);
  });
}
test('username inválido não consulta API e página vazia oferece análise', async ({ page }) => {
  const calls = await mockApi(page);
  await page.goto('/skills');
  await expect(page.getByText('Escolha um perfil para visualizar esta página.')).toBeVisible();
  await page.goto('/perfil/test--invalid');
  await expect(page.getByText('Username inválido.', { exact: false })).toBeVisible();
  expect(calls.filter(c => c.path.startsWith('/api/analyses/'))).toHaveLength(0);
});

