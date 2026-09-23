import { test, expect } from '@playwright/test';

const avatar = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="56" height="56"><circle cx="28" cy="28" r="28" fill="#365bbc"/></svg>');
const now = new Date();
const before = new Date(now.getTime() - 9 * 86400000).toISOString();
const months = Array.from({ length: 12 }, (_, i) => new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 11 + i, 1)).toISOString().slice(0, 7));
const activity = { months, series: [{ name: 'React', counts: [...Array(11).fill(null), 18] }, { name: 'JavaScript', counts: [...Array(10).fill(null), 3, 6] }], eligibleRepositories: 4, inspectedRepositories: 3, completedRepositories: 3, isPartial: true, missingStudents: 1, studentCount: 2, warnings: ['Um histórico não está disponível.'] };
function studentDashboard(student) {
  return student.username === 'ana' ? {
    technologies: [{ label: 'React', count: 7 }, { label: 'JavaScript', count: 5 }],
    categories: [{ label: 'Frontend', count: 7 }, { label: 'Linguagens', count: 5 }],
    commitActivity: { ...activity, series: activity.series.map(s => ({ ...s, counts: s.counts.map(n => n ?? 0) })), isPartial: false, missingStudents: 0, studentCount: 0, warnings: [] },
  } : {
    technologies: [{ label: 'Python', count: 3 }, { label: 'React', count: 2 }],
    categories: [{ label: 'Linguagens', count: 3 }, { label: 'Frontend', count: 2 }], commitActivity: null,
  };
}
function snapshot(student, commitActivity = activity) {
  return { id: 'snapshot', analysisVersion: 2, profile: { username: student.username, name: student.nome, avatarUrl: avatar, profileUrl: 'https://github.com/example', publicRepositories: 1,
    repositories: [{ id: 1, name: 'demo', url: 'https://github.com/example/demo', updatedAt: before, pushedAt: before }] },
  analyzedRepositories: 1, inspectedRepositories: 1, completelyInspectedRepositories: 1, skills: commitActivity?.series.length > 10 ? commitActivity.series.map((item, i) => ({ name: item.name, category: 'Linguagens', evidenceLevel: 'Boa evidência', repositoryCount: 20 - i, evidence: [{ repositoryId: 1 }] })) : [{ name: 'React', category: 'Frontend', evidenceLevel: 'Pouca evidência', repositoryCount: 1, evidence: [{ repositoryId: 1 }] }],
  warnings: [], recommendations: [], learningTracks: [], analyzedAt: before, source: 'cache', isPartial: false, commitActivity };
}
async function mock(page, empty = false, commitOverride = null) {
  const calls = [];
  let students = empty ? [] : ['Ana', 'Bruno'].map((nome, index) => ({ gitHubProfileId: `student-${index}`, nome, username: nome.toLowerCase(), avatarUrl: avatar, isExpired: true,
    latest: { analyzedAt: before, isComplete: index === 0, skills: ['React', 'JavaScript'] } }));
  const classes = new Map();
  const rankedTechnologies = commitOverride?.series.map((item, i) => ({ label: item.name, count: 20 - i }));
  const summary = item => ({ ...item, memberCount: item.profileIds.length, technologies: rankedTechnologies ?? [{ label: 'React', count: item.profileIds.length }] });
  await page.route('**/*', async route => {
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname; const method = request.method();
    const headers = { 'access-control-allow-origin': '*', 'access-control-allow-headers': 'authorization,content-type', 'access-control-allow-methods': 'GET,POST,PUT,DELETE' };
    const reply = (data, status = 200) => route.fulfill({ status, json: data, headers });
    if (path.startsWith('/api/')) {
      if (method === 'OPTIONS') return route.fulfill({ status: 204, headers });
      calls.push({ path, method, authorization: request.headers().authorization });
      if (path === '/api/auth/login') return reply({ accessToken: 'platform-test-only', expiresIn: 1800 });
      if (path === '/api/auth/me') return reply({ id: 'teacher-context', nome: 'Conta de teste' });
      if (path === '/api/me/profiles') return reply(students);
      if (path.startsWith('/api/me/profiles/')) {
        const id = path.split('/')[4];
        if (path.endsWith('/analysis')) return reply(snapshot(students.find(s => s.gitHubProfileId === id), commitOverride ?? activity));
        const used = [...classes.values()].filter(c => c.profileIds.includes(id));
        if (used.length) return reply({ detail: `Remova primeiro o perfil das turmas: ${used.map(c => c.name).join(', ')}.` }, 409);
        students = students.filter(s => s.gitHubProfileId !== id); return reply({});
      }
      if (path.startsWith('/api/classes')) {
        if (!request.headers().authorization) return reply({}, 401);
        if (path === '/api/classes') {
          if (method === 'GET') return reply([...classes.values()].map(summary));
          const body = request.postDataJSON(); const id = `class-${classes.size + 1}`;
          classes.set(id, { ...body, id, createdAt: before, updatedAt: now.toISOString() }); return reply({ id }, 201);
        }
        const [, , , id, members, profileId] = path.split('/'); const item = classes.get(id);
        if (!item) return reply({}, 404);
        if (members === 'members') {
          if (method === 'POST') item.profileIds.push(...request.postDataJSON().profileIds);
          else item.profileIds = item.profileIds.filter(value => value !== profileId);
          return reply({});
        }
        if (method === 'PUT') { item.name = request.postDataJSON().name; return reply({}); }
        if (method === 'DELETE') { classes.delete(id); return reply({}); }
        return reply({ ...summary(item), categories: [{ label: 'Frontend', count: item.profileIds.length }], commitActivity: commitOverride ?? activity,
          members: students.filter(s => item.profileIds.includes(s.gitHubProfileId)).map(s => ({ ...s, name: s.nome, analyzedAt: before, isComplete: s.latest.isComplete, skills: s.latest.skills, dashboard: { ...studentDashboard(s), ...(commitOverride ? { commitActivity: commitOverride, technologies: rankedTechnologies } : {}) } })) });
      }
      return reply({ detail: 'Unexpected API call in mocked test' }, 500);
    }
    if (url.origin === 'http://127.0.0.1:4173') return route.continue();
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
  await page.goto('/'); await page.getByLabel('E-mail', { exact: true }).fill('test@example.test');
  await page.getByLabel('Senha', { exact: true }).fill('Fictional-Test123!');
  await page.getByRole('button', { name: 'Entrar', exact: true }).click();
  await expect(page).toHaveURL(/\/minhas-analises$/);
}
async function noOverflow(page) { expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); }

for (const theme of ['dark', 'light']) test(`turma completa, gráficos e perfil salvo no tema ${theme}`, async ({ page }, testInfo) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  const calls = await mock(page);
  await page.addInitScript(value => localStorage.setItem('portfolio-theme', value), theme);
  await login(page); await nav(page, 'Turmas');
  await expect(page.getByText('Você ainda não criou nenhuma turma.')).toBeVisible();
  await page.getByRole('button', { name: 'Criar minha primeira turma' }).click();
  await page.getByLabel('Nome da turma', { exact: true }).fill('Jovem Tech 2026');
  await expect(page.getByRole('button', { name: 'Criar turma', exact: true })).toBeDisabled();
  await page.getByRole('checkbox', { name: 'Selecionar @ana' }).check();
  await page.getByLabel('Buscar por nome ou username').fill('bruno');
  await page.getByRole('checkbox', { name: 'Selecionar @bruno' }).check();
  await expect(page.getByText('2 aluno(s) selecionado(s)', { exact: true })).toBeVisible();
  await noOverflow(page);
  await page.screenshot({ path: testInfo.outputPath(`selecionar-${theme}.png`), fullPage: true });
  await page.getByRole('button', { name: 'Criar turma', exact: true }).click();
  await expect(page).toHaveURL(/\/turmas\/class-1$/);
  await expect(page.getByRole('heading', { name: 'Jovem Tech 2026', exact: true })).toBeVisible();
  await expect(page.locator('canvas')).toHaveCount(3);
  const technologies = page.getByRole('region', { name: 'Tecnologias mais presentes na turma', exact: true });
  await technologies.getByText('Ver dados em tabela', { exact: true }).click();
  await expect(technologies.getByRole('row', { name: 'React 2' })).toBeVisible();
  await expect(technologies.getByRole('columnheader', { name: 'Alunos' })).toBeVisible();
  await expect(page.getByText('1 de 2 aluno(s) ainda não possuem histórico de commits atualizado.', { exact: false })).toBeVisible();
  await page.getByLabel('Período da atividade').selectOption('6');
  await page.getByText('Ver atividade em tabela').click();
  await expect(page.getByRole('region', { name: 'Commits por tecnologia', exact: true }).getByRole('row')).toHaveCount(7);
  await noOverflow(page);
  await page.screenshot({ path: testInfo.outputPath(`turma-${theme}.png`), fullPage: true });
  await page.getByRole('button', { name: 'Editar nome', exact: true }).click();
  await page.getByLabel('Nome da turma', { exact: true }).fill('React Noite');
  await page.getByRole('button', { name: 'Salvar nome' }).click();
  await expect(page.getByRole('heading', { name: 'React Noite', exact: true })).toBeVisible();
  await page.getByRole('article', { name: 'Aluno @bruno' }).getByRole('button', { name: 'Remover da turma' }).click();
  await expect(page.getByRole('article', { name: 'Aluno @bruno' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Remover da turma' })).toBeDisabled();
  await page.getByRole('button', { name: 'Adicionar alunos', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Selecionar @ana' })).toHaveCount(0);
  await page.getByRole('checkbox', { name: 'Selecionar @bruno' }).check();
  await page.getByRole('button', { name: 'Adicionar selecionados' }).click();
  await expect(page.getByRole('article', { name: 'Aluno @bruno' })).toBeVisible();
  await nav(page, 'Minhas análises');
  await page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Ana', exact: true }) }).getByRole('button', { name: 'Remover', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('React Noite');
  await nav(page, 'Turmas');
  await page.getByRole('link', { name: 'Abrir turma' }).click();
  await page.getByRole('article', { name: 'Aluno @ana' }).getByRole('button', { name: 'Ver perfil' }).click();
  await expect(page).toHaveURL(/\/perfil\/ana$/);
  await expect(page.getByRole('heading', { name: 'Ana', exact: true })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Commits por tecnologia', exact: true }).locator('canvas')).toHaveCount(1);
  await page.getByText('Ver atividade em tabela').click();
  await expect(page.getByRole('region', { name: 'Commits por tecnologia', exact: true }).getByRole('row')).toHaveCount(13);
  await page.getByLabel('Período da atividade').selectOption('6');
  await expect(page.getByRole('region', { name: 'Commits por tecnologia', exact: true }).getByRole('row')).toHaveCount(7);
  await noOverflow(page);
  await page.screenshot({ path: testInfo.outputPath(`perfil-atividade-${theme}.png`), fullPage: true });
  await nav(page, 'Turmas');
  page.once('dialog', dialog => dialog.dismiss());
  await page.getByRole('button', { name: 'Excluir', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'React Noite', exact: true })).toBeVisible();
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Excluir', exact: true }).click();
  await expect(page.getByText('Você ainda não criou nenhuma turma.')).toBeVisible();
  expect(calls.filter(c => c.path.startsWith('/api/analyses/') || c.path.startsWith('/api/github/'))).toHaveLength(0);
  expect(calls.filter(c => c.path.startsWith('/api/classes')).every(c => c.authorization === 'Bearer platform-test-only')).toBe(true);
  expect(errors).toEqual([]);
});

test('visitante não vê Turmas e rotas privadas redirecionam sem consultar API', async ({ page }) => {
  const calls = await mock(page);
  await page.goto('/analisar');
  await expect(page.getByRole('link', { name: 'Turmas', exact: true })).toHaveCount(0);
  for (const path of ['/turmas', '/turmas/arbitrary-id']) {
    await page.goto(path); await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole('button', { name: 'Entrar', exact: true })).toBeVisible();
  }
  expect(calls.filter(c => c.path.startsWith('/api/classes'))).toHaveLength(0);
});
test('sem análises salvas orienta salvar perfil e não permite criar turma vazia', async ({ page }) => {
  const calls = await mock(page, true);
  await login(page); await nav(page, 'Turmas');
  await page.getByRole('button', { name: 'Nova turma', exact: true }).click();
  await expect(page.getByText('Você precisa salvar pelo menos uma análise antes de criar uma turma.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Criar turma', exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: 'Analisar um perfil', exact: true }).click();
  await expect(page).toHaveURL(/\/analisar$/);
  expect(calls.filter(c => c.path === '/api/classes' && c.method === 'POST')).toHaveLength(0);
});

for (const theme of ['dark', 'light']) test(`segmentação local do dashboard por aluno no tema ${theme}`, async ({ page }, testInfo) => {
  const calls = await mock(page);
  await page.addInitScript(value => localStorage.setItem('portfolio-theme', value), theme);
  await login(page); await nav(page, 'Turmas');
  await page.getByRole('button', { name: 'Nova turma', exact: true }).click();
  await page.getByLabel('Nome da turma', { exact: true }).fill('Turma com filtro');
  await page.getByRole('checkbox', { name: 'Selecionar @ana' }).check();
  await page.getByRole('checkbox', { name: 'Selecionar @bruno' }).check();
  await page.getByRole('button', { name: 'Criar turma', exact: true }).click();
  await expect(page).toHaveURL(/\/turmas\/class-1$/);
  const filter = page.getByLabel('Visualizar:', { exact: true });
  await expect(filter).toHaveValue('');
  await expect(filter.locator('option:checked')).toHaveText('Todos os alunos');
  const aggregate = page.getByRole('region', { name: 'Tecnologias mais presentes na turma', exact: true });
  await aggregate.getByText('Ver dados em tabela', { exact: true }).click();
  await expect(aggregate.getByRole('row', { name: 'React 2', exact: true })).toBeVisible();
  const beforeFiltering = calls.length;
  await filter.selectOption('student-0');
  const summary = page.getByRole('region', { name: 'Resumo do aluno selecionado' });
  await expect(summary.getByRole('heading', { name: 'Ana', exact: true })).toBeVisible();
  await expect(summary).toContainText('@ana');
  await expect(summary).toContainText('Última análise:');
  await expect(summary).toContainText('Sem cortes nas regras de inspeção.');
  const technologies = page.getByRole('region', { name: 'Tecnologias do aluno', exact: true });
  await expect(technologies.getByRole('row', { name: 'React 7', exact: true })).toBeVisible();
  await expect(technologies.getByRole('columnheader', { name: 'Repositórios', exact: true })).toBeVisible();
  const categories = page.getByRole('region', { name: 'Categorias do aluno', exact: true });
  await categories.getByText('Ver dados em tabela', { exact: true }).click();
  await expect(categories.getByRole('row', { name: 'Frontend 7', exact: true })).toBeVisible();
  const commits = page.getByRole('region', { name: 'Commits por tecnologia', exact: true });
  await commits.getByText('Ver atividade em tabela', { exact: true }).click();
  await expect(commits.getByRole('row').last()).toContainText('18');
  await expect(commits.getByText('Quantidade de commits do usuário, por mês, em repositórios onde cada tecnologia foi detectada.')).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-bs-theme', theme);
  await noOverflow(page);
  await page.screenshot({ path: testInfo.outputPath(`filtro-aluno-${theme}.png`), fullPage: true });

  await filter.selectOption('student-1');
  await expect(summary.getByRole('heading', { name: 'Bruno', exact: true })).toBeVisible();
  await expect(summary).toContainText('Cobertura parcial.');
  await expect(technologies.getByRole('row', { name: 'Python 3', exact: true })).toBeVisible();
  await expect(categories.getByRole('row', { name: 'Frontend 2', exact: true })).toBeVisible();
  await expect(commits.getByText('Este snapshot ainda não possui histórico de commits. Atualize a análise pelo GitHub para gerar esse gráfico.')).toBeVisible();
  await expect(commits.locator('canvas')).toHaveCount(0);
  await filter.selectOption('');
  await expect(aggregate.getByRole('row', { name: 'React 2', exact: true })).toBeVisible();
  await expect(aggregate.getByRole('columnheader', { name: 'Alunos', exact: true })).toBeVisible();
  await expect(summary).toHaveCount(0);
  await expect(commits.locator('canvas')).toHaveCount(1);
  expect(calls.length).toBe(beforeFiltering);

  await filter.selectOption('student-1');
  await page.getByRole('article', { name: 'Aluno @bruno', exact: true }).getByRole('button', { name: 'Remover da turma', exact: true }).click();
  await expect(filter).toHaveValue('');
  await expect(filter.locator('option:checked')).toHaveText('Todos os alunos');
  await expect(aggregate.getByRole('row', { name: 'React 1', exact: true })).toBeVisible();
  // Re-adding the same student must not silently restore the previous selection.
  await page.getByRole('button', { name: 'Adicionar alunos', exact: true }).click();
  await page.getByRole('checkbox', { name: 'Selecionar @bruno' }).check();
  await page.getByRole('button', { name: 'Adicionar selecionados' }).click();
  await expect(filter.locator('option', { hasText: '@bruno' })).toHaveCount(1);
  await expect(filter).toHaveValue('');
  await filter.selectOption('student-0');
  await summary.getByRole('button', { name: 'Ver perfil completo', exact: true }).click();
  await expect(page).toHaveURL(/\/perfil\/ana$/);
  expect(calls.filter(c => c.path.endsWith('/analysis'))).toHaveLength(1);
  expect(calls.filter(c => c.path.startsWith('/api/analyses/') || c.path.startsWith('/api/github/') || c.path.includes('/commits'))).toHaveLength(0);
});


test('Top 5, Top 10 e Todas em perfil, Skills e turma sem chamadas adicionais', async ({ page }, testInfo) => {
  const history = { ...activity, series: Array.from({ length: 12 }, (_, i) => ({
    name: i === 8 ? 'TSQL' : `Tech${i}`, counts: [200 - i * 2, ...Array(10).fill(0), i],
  })) };
  const calls = await mock(page, false, history);
  await login(page); await nav(page, 'Turmas');
  await page.getByRole('button', { name: 'Nova turma', exact: true }).click();
  await page.getByLabel('Nome da turma', { exact: true }).fill('Top tecnologias');
  await page.getByRole('checkbox', { name: 'Selecionar @ana' }).check();
  await page.getByRole('button', { name: 'Criar turma', exact: true }).click();
  await expect(page).toHaveURL(/\/turmas\/class-1$/);
  const panel = page.getByRole('region', { name: 'Commits por tecnologia', exact: true });
  const quantity = panel.getByLabel('Tecnologias exibidas:', { exact: true });
  await expect(quantity).toHaveValue('5');
  await panel.getByText('Ver atividade em tabela', { exact: true }).click();
  await expect(panel.getByRole('columnheader')).toHaveCount(6);
  const initialCalls = calls.length;

  async function barSelections(title) {
    const bars = page.getByRole('region', { name: title, exact: true });
    const select = bars.getByLabel('Tecnologias exibidas:', { exact: true });
    await expect(select).toHaveValue('5');
    const details = bars.locator('details');
    if (await details.getAttribute('open') === null) await bars.getByText('Ver dados em tabela', { exact: true }).click();
    await expect(bars.getByRole('row')).toHaveCount(6);
    await select.selectOption('10');
    await expect(bars.getByRole('row')).toHaveCount(11);
    await expect(bars.getByRole('rowheader', { name: 'TSQL', exact: true })).toBeVisible();
    await select.selectOption('all');
    await expect(bars.getByRole('row')).toHaveCount(13);
    await expect(bars.getByRole('rowheader', { name: 'Tech11', exact: true })).toBeVisible();
    await noOverflow(page);
    await select.selectOption('5');
    await expect(bars.getByRole('row')).toHaveCount(6);
  }
  async function commitSelections() {
    for (const [value, count] of [['10', 11], ['all', 13]]) {
      await quantity.selectOption(value);
      await expect(panel.getByRole('columnheader')).toHaveCount(count);
      await expect(panel.getByRole('columnheader', { name: 'TSQL', exact: true })).toBeVisible();
      const headings = await panel.getByRole('columnheader').allTextContents();
      await page.getByLabel('Período da atividade').selectOption('6');
      await expect(panel.getByRole('row')).toHaveCount(7);
      expect(await panel.getByRole('columnheader').allTextContents()).toEqual(headings);
      await page.getByLabel('Período da atividade').selectOption('12');
      await expect(panel.getByRole('row')).toHaveCount(13);
      expect(await panel.getByRole('columnheader').allTextContents()).toEqual(headings);
      await noOverflow(page);
    }
    await quantity.selectOption('5');
    await expect(panel.getByRole('columnheader')).toHaveCount(6);
  }
  await barSelections('Tecnologias mais presentes na turma');
  await expect(page.getByRole('region', { name: 'Categorias da turma', exact: true }).getByLabel('Tecnologias exibidas:')).toHaveCount(0);
  await commitSelections();
  await page.getByRole('button', { name: 'Modo escuro', exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-bs-theme', 'light');
  await quantity.selectOption('all');
  await noOverflow(page);
  await page.screenshot({ path: testInfo.outputPath('todas-tecnologias.png'), fullPage: true });
  await quantity.selectOption('5');
  await page.getByLabel('Visualizar:', { exact: true }).selectOption('student-0');
  await barSelections('Tecnologias do aluno');
  await commitSelections();
  await page.getByLabel('Visualizar:', { exact: true }).selectOption('');
  await expect(quantity).toHaveValue('5');
  expect(calls.length).toBe(initialCalls);

  await page.getByLabel('Visualizar:', { exact: true }).selectOption('student-0');
  await page.getByRole('button', { name: 'Ver perfil completo', exact: true }).click();
  await expect(page).toHaveURL(/\/perfil\/ana$/);
  await expect(quantity).toHaveValue('5');
  await panel.getByText('Ver atividade em tabela', { exact: true }).click();
  const profileCalls = calls.length;
  await barSelections('Linguagens mais evidenciadas');
  await commitSelections();
  await nav(page, 'Skills');
  await barSelections('Tecnologias com mais evidências');
  // Leaving and reopening the page resets the local choice to Top 5.
  await page.getByRole('region', { name: 'Tecnologias com mais evidências', exact: true }).getByLabel('Tecnologias exibidas:').selectOption('all');
  await nav(page, 'Recomendações'); await nav(page, 'Skills');
  await expect(page.getByRole('region', { name: 'Tecnologias com mais evidências', exact: true }).getByLabel('Tecnologias exibidas:')).toHaveValue('5');
  expect(calls.length).toBe(profileCalls);
  expect(calls.filter(c => c.path.startsWith('/api/analyses/') || c.path.startsWith('/api/github/'))).toHaveLength(0);
});
