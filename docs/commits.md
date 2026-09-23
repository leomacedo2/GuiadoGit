# Commits por tecnologia

Esta alteração substitui o gráfico de último push por commits reais retornados pelo GitHub. Não altera cadastro/login, OAuth, ownership de Turmas, cache compartilhado, banco ou deploy. Não exige nova migration.

## Coleta e significado

O GitHubService usa `GET /repos/{owner}/{repo}/commits`, com:

- `author`: username canônico do perfil analisado, nunca a conta da plataforma ou o usuário que autorizou o token.
- `since`: primeiro dia UTC do mês atual menos onze meses, às 00:00:00.
- `until`: instante UTC fixado no início da análise; todas as páginas usam os mesmos limites.
- `per_page=100` e `page=1,2,...`. A existência de próxima página vem do header Link. URLs do header não são seguidas; incrementamos a página no endpoint fixo.
- Sem `sha`: usa o branch padrão do repositório. Não consulta commits individuais, arquivos modificados ou GraphQL.

Fonte: [GitHub REST — List commits](https://docs.github.com/en/rest/commits/commits#list-commits).

Cada SHA é contado uma única vez **por repositório**, mesmo se repetido em páginas. A data de agrupamento é `commit.committer.date`, convertida para UTC: é a data de registro do commit, que pode diferir da data original do autor em rebases/cherry-picks. O filtro de autoria continua sendo `author`. Datas fora da janela são descartadas. Entradas inválidas preservam as contagens válidas e tornam a cobertura parcial.

O commit é associado a todas as tecnologias já detectadas naquele repositório. Dez commits em um repositório com React e JavaScript somam dez em cada tecnologia. Não se inspecionam arquivos dos commits e não se atribui proficiência. Somar séries de tecnologias não produz o total de commits únicos.

## Seleção, limites e custo

Somente repositórios públicos da listagem atual, não arquivados, **não forks**, com tecnologias detectadas e `PushedAt` dentro da janela. Ordenação por PushedAt decrescente, desempate pelo ID. PushedAt permanece como metadado e critério de seleção, mas não gera barras nem contagens.

| Configuração em Analysis:Individual | Padrão | Faixa validada |
| --- | ---: | ---: |
| MaxCommitHistoryRepositories | 25 | 1–30 |
| MaxCommitPagesPerRepository | 10 | 1–10 |
| CommitHistoryRequestBudget | 100 | 1–300 |

Variáveis opcionais equivalentes: `Analysis__Individual__MaxCommitHistoryRepositories`, `Analysis__Individual__MaxCommitPagesPerRepository`, `Analysis__Individual__CommitHistoryRequestBudget`. Não há secret nem variável obrigatória nova.

- Sem o orçamento global, 25 × 10 permitiria **250 chamadas** de commits.
- Com os padrões, o teto é **min(250, 100) = 100 chamadas adicionais**, até 10.000 registros retornados no conjunto e até 1.000 por repositório. Repetições e datas descartadas reduzem a contagem útil.
- O orçamento de inspeção permanece **600 chamadas**, separado. O teto de inspeção + histórico é 700, além da chamada de perfil e da paginação de repositórios já existente. Essa listagem continua considerando todos os repositórios: não há promessa de teto absoluto de 700 para a análise inteira.
- Um perfil com 25 repositórios elegíveis e até uma página em cada um custa até 25 chamadas adicionais; repositórios sem commits também exigem uma página para confirmar zero.
- O histórico usa o **tempo restante do prazo de inspeção existente de 180 segundos**, sem aumentar timeout do frontend. Coletas grandes podem terminar parciais antes do teto de chamadas.
- As skills são coletadas primeiro. `AdditionalRequests` mantém o significado de inspeção; `commitActivity.requests` registra chamadas de histórico efetivamente enviadas; `TotalGitHubRequests` inclui ambas, perfil e listagem. Tentativas bloqueadas localmente pela quota não contam como envio, mas o orçamento de tentativas continua conservador.

## Autenticação, erros e rate limit

O novo endpoint passa pelo mesmo GetAsync, HttpClient, GitHubRequestContext e GitHubRateLimitHandler. Credenciais da aplicação, token OAuth opcional, quotas separadas, headers observados e fallback existente foram preservados.

Não há retries automáticos. Falha em um repositório preserva suas páginas obtidas e permite tentar outros. Rate limit interrompe o restante da coleta. Timeout de uma chamada permite continuar quando ainda há prazo; esgotamento do prazo global encerra o histórico. Cancelamento explícito do cliente continua propagando normalmente.

A cobertura do histórico é independente de `AnalysisDto.IsPartial`, que continua descrevendo a inspeção de skills. A interface exibe aviso específico de commits. Status por repositório: complete, repository-limit, request-budget, page-limit, rate-limit, deadline, timeout, unavailable ou invalid-data. Logs usam o handler existente, sem tokens nem conteúdo dos commits.

## Persistência e cache

`AnalysisDto.CommitActivity` é serializado automaticamente pelo mecanismo existente de `AnalysisSnapshot.Create` em `Analysis.MetadataJson` (jsonb). Não são persistidos SHAs, mensagens, e-mails ou commits individuais.

Estrutura resumida, com valores fictícios:

```json
{
  "commitActivity": {
    "months": ["2025-10", "2025-11", "...", "2026-09"],
    "series": [
      { "name": "React", "counts": [0, 2, "...", 18] },
      { "name": "JavaScript", "counts": [0, 2, "...", 18] }
    ],
    "periodStart": "2025-10-01T00:00:00+00:00",
    "periodEnd": "2026-10-01T00:00:00+00:00",
    "collectedAt": "2026-09-23T12:00:00+00:00",
    "isPartial": false,
    "eligibleRepositories": 2,
    "inspectedRepositories": 2,
    "completedRepositories": 2,
    "requests": 2,
    "requestBudget": 100,
    "repositoryLimit": 25,
    "pageLimit": 10,
    "warnings": [],
    "repositories": [
      { "repositoryId": 123, "name": "exemplo", "pages": 1, "commits": 20, "status": "complete" }
    ]
  }
}
```

As reticências acima apenas abreviam o exemplo: os dados reais possuem doze meses e doze números/nulls por série. PeriodEnd é o limite exclusivo do calendário, enquanto CollectedAt registra o corte real da consulta no mês atual.

Todas as séries são persistidas para permitir que a turma escolha seu próprio top 5 após somar os alunos. O frontend seleciona as cinco maiores contagens na janela completa e mantém exatamente essas séries no filtro visual de seis meses, inclusive quando seu recorte contém apenas zeros.

- `0`: nenhum commit encontrado para a tecnologia/mês na coleta completa dos repositórios elegíveis.
- `null` / `—`: não há contagem positiva e uma limitação impede confirmar zero para aquela tecnologia.
- Contagem positiva com aviso parcial: mínimo observado, não garantia de total completo.

Cache fresco tem prioridade mesmo se não possuir commitActivity. Nova análise, cache vencido consultado normalmente ou force refresh explícito coleta o histórico. Abrir um snapshot salvo ou turma não atualiza nada. Nenhum snapshot é apagado/invalido por não ter esse campo.

Snapshots antigos exibem: “Este snapshot ainda não possui histórico de commits. Atualize a análise pelo GitHub para gerar esse gráfico.” Não existe fallback para pushed_at. O helper legado RecentActivity e seus testes permanecem para compatibilidade histórica, mas não são utilizados nos endpoints/gráficos atuais.

## Individual e Turmas

Perfil: gráfico **Commits por tecnologia**, meses UTC, eixo Y em commits, tooltip com tecnologia/mês/contagem, filtro 6/12 e tabela acessível. A janela individual reflete o momento da coleta, exibido na tela; abrir um snapshot antigo não atualiza suas datas.

Turma: usa somente o snapshot mais recente persistido de cada GitHubProfile; deduplica alunos por ID e soma suas séries por mês/tecnologia. Não deduplica por repositório entre alunos: são contagens de autores diferentes. O top 5 é selecionado depois dessa soma, usando todas as séries persistidas.

A janela da turma usa os doze meses atuais e alinha as chaves YYYY-MM. Meses fora da janela coletada de um aluno são desconhecidos, não zero. Alunos sem campo commitActivity são contados em MissingStudents/StudentCount e explicitamente informados. Históricos parciais e janelas antigas geram aviso de cobertura. Datas individuais continuam nos cards; o painel também informa a coleta mais antiga entre os históricos disponíveis.

Os gráficos gerais de tecnologias/categorias continuam contando **alunos**, sem alterações. Rotas, entidades, permissões, validações e operações de Turmas foram preservadas. Chart.js/react-chartjs-2 e os temas existentes são reutilizados, sem nova dependência.

## Limitações

- E-mail/identidade não associados ao GitHub podem impedir que o filtro author encontre commits.
- O endpoint percorre o branch padrão; commits não alcançáveis por esse branch podem ficar ausentes. Merge commits podem entrar conforme o retorno da API.
- Apenas repositórios próprios da listagem pública atual, não forks e não arquivados; não é um inventário de todas as contribuições externas do usuário.
- As tecnologias refletem a análise atual do repositório, não necessariamente as tecnologias presentes historicamente em cada commit.
- Commits posteriores à coleta não aparecem até nova análise. Paginação não oferece snapshot transacional do GitHub; SHA deduplica repetições, mas mudanças concorrentes podem afetar cobertura.
- Limites, indisponibilidade e o prazo restante podem produzir histórico parcial. PushedAt sem data não é substituído por updated_at.

## Validação e passos manuais

Testes exclusivamente com GitHub mockado; testes de persistência usam PostgreSQL descartável local, nunca Supabase. Não foram acessados User Secrets nem serviços externos de produção.

1. Finalizar a execução local antiga e iniciar o backend atualizado quando for testar. A validação foi feita em Release porque o executável Debug estava em uso.
2. Abrir um perfil já salvo: um snapshot legado deve mostrar a orientação, sem consulta de commits.
3. Solicitar uma atualização explícita de um perfil e verificar gráfico, tabela, 6/12 meses e cobertura. Essa ação manual consome a quota real disponível.
4. Abrir a turma que contém esse perfil: o agregado deve refletir o snapshot atualizado sem consulta GitHub. Outros alunos legados continuam sinalizados; não atualizar todos automaticamente.
5. Commit/push e aguardar os auto-deploys existentes, **desde que a migration anterior de Turmas já esteja aplicada**. Esta alteração não cria nem exige outra migration. Nenhuma configuração OAuth, Supabase, Vercel ou Render precisa mudar.

Comandos de validação: `dotnet test Portfolio.sln -c Release`, `dotnet build Portfolio.sln -c Release`, e no frontend `npm test`, `npm run test:e2e`, `npm run build`.

## Arquivos desta alteração

Novos:
- backend/Portfolio.Api/DTOs/CommitActivityDto.cs
- backend/Portfolio.Api/Models/GitHubCommit.cs
- backend/Portfolio.Api/Services/CommitHistoryService.cs
- backend/Portfolio.Api/Services/CommitActivityAggregation.cs
- backend/Portfolio.Api.Tests/CommitHistoryTests.cs
- docs/commits.md

Modificados:
- Backend: AnalysisController.cs, SavedProfilesController.cs; DTOs/AnalysisDto.cs, ClassroomDto.cs, PortfolioDto.cs; Models/GitHubModels.cs; Services/AnalysisService.cs, GitHubService.cs, IGitHubService.cs, IndividualAnalysisOptions.cs, ClassroomDashboardService.cs; appsettings.json.
- Testes backend: AnalysisTests.cs, IndividualCoverageTests.cs, ManifestCoverageTests.cs (implementação explícita do novo método nos mocks); PersistenceTests.cs, ClassroomTests.cs.
- Frontend: pages/ProfilePage.jsx, ClassroomPage.jsx; components/ActivityPanel.jsx, ActivityChart.jsx; analysis/activityData.js.
- Testes frontend: tests/activity-data.test.js; tests/e2e/classrooms.spec.js, flows.spec.js.
- Documentação: README.md, docs/turmas.md, docs/deploy.md, docs/persistencia.md.

Os demais arquivos já modificados no diretório pertencem à rodada anterior de Turmas. Nenhuma migration foi criada ou alterada nesta rodada.

Resultado da validação desta alteração: **184 testes backend + 7 testes de dados frontend + 24 testes E2E = 215 passando**, nenhum ignorado. Build Release sem avisos/erros e build Vite concluído. E2E executado em desktop/mobile e dark/light mode, com integrações mockadas. A compilação Debug inicial encontrou o executável local em uso; a validação final foi feita em Release, preservando a execução local.
