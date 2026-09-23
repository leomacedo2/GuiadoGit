# Análise de evidências

## Fluxo e contrato

`GET /api/analyses/{username}` → PersistentAnalysisService (cache compartilhado) → AnalysisService → GitHubService → SkillDetector → RecommendationService. Force refresh mantém o endpoint POST existente. [Refinamento atual, custos e validação](refinamento.md).

A resposta contém `profile` (perfil e lista integral original), `analyzedRepositories` (repositórios considerados, incluindo metadados), `inspectedRepositories` (árvores de arquivos obtidas), `additionalRequests`, `isPartial`, `warnings`, `skills` e `recommendations`.

Cada skill possui nome, categoria, nível, contagem de repositórios distintos e evidências com ID, nome, URL do repositório e motivo. Há até três motivos representativos por skill/repositório para evitar milhares de linhas repetidas. As evidências não interpretam o código-fonte.

## Regras centralizadas em SkillDetector

| Sinal | Evidência |
| --- | --- |
| Linguagem principal do GitHub | Linguagem correspondente |
| `.py`, `.java`, `.js`/`.jsx`, `.ts`/`.tsx`, `.cs`, `.sql` | Python, Java, JavaScript, TypeScript, C#, SQL |
| `pom.xml` | Maven e Java |
| `package.json` | Lido para dependências; não gera skill genérica visível |
| Dependência `react` | React |
| Dependência `expo` | Expo e React Native |
| Dependência `react-native` | React Native |
| Dependência Maven do grupo `org.springframework.boot` | Spring Boot |
| Spring Boot starter web/webflux | REST API como indício de infraestrutura web; não valida endpoints |
| `Dockerfile` ou `Dockerfile.*` | Docker |
| `.github/workflows/*.yml` ou `.yaml` | CI/CD |
| Arquivos em `test`, `tests`, `__tests__`; `test_*`, `.test.`, `.spec.` | Estrutura de testes automatizados |
| pytest, Jest, Vitest, Mocha, Playwright, xUnit, NUnit, MSTest, Spring starter test | Dependência de testes automatizados |
| JUnit ou JUnit Jupiter | JUnit e testes automatizados |
| FastAPI ou Flask em requirements | APIs Python |
| SQLAlchemy, psycopg2, psycopg2-binary ou sqlite3 em requirements; Entity Framework em csproj | Banco de dados |

Dependências são lidas de JSON, XML, requirements e TOML estruturado (Tomlyn): package.json, csproj, pom.xml, requirements.txt, pyproject.toml (PEP 621/Poetry/grupos), Pipfile e composer.json. Gradle/Gemfile aceitam declarações literais sem executar scripts. package-lock.json v2/v3 é fallback apenas quando falta package.json no mesmo diretório: lê só dependências diretas da raiz, nunca transitivas. Não se resolvem herança Maven, aliases/variáveis Gradle, includes externos, imports Python nem bibliotecas da linguagem padrão. Maven dependencyManagement não comprova uso.

Vite, tsconfig, soluções .NET, Docker/Compose e GitHub Actions fornecem sinais pela árvore, sem download extra. Ferramentas declaradas de estado, acessibilidade, performance, testes específicos, autenticação e integração ampliam as evidências sem comprovar execução ou qualidade. Jest/Vitest só geram evidência específica frontend/mobile quando o mesmo manifesto demonstra React/React Native/Expo; isoladamente continuam sinalizando testes genéricos.

Pastas `wwwroot/lib`, `node_modules`, `vendor`, `.git`, `bin`, `obj`, `dist`, `build`, `.venv`, `venv`, `third_party`, `third-party`, `bower_components`, `.next`, `.nuxt`, `coverage` e `__pycache__` (e arquivos `.min.js`) são excluídas da inspeção. Isso reduz falsos positivos, mas também pode omitir projetos que usam esses nomes para conteúdo próprio. Não é possível identificar automaticamente toda dependência copiada em pastas arbitrárias; linguagem principal é metadado do GitHub e continua considerada em todos os repositórios.

## Níveis

- 1 repositório: Pouca evidência.
- 2 repositórios: Em desenvolvimento.
- 3–4 repositórios: Boa evidência.
- 5 ou mais: Forte evidência.

Vários arquivos da mesma skill em um repositório não elevam o nível. Estes nomes medem frequência de sinais na amostra, não classificam a pessoa.

## Recomendações

O backend retorna `learningTracks` com até três trilhas relevantes, até quatro próximos passos por trilha e `recommendations` com até três prioridades resumidas. Consulte [trilhas.md](trilhas.md) para o algoritmo, exemplos sintéticos e limitações.

## Controle de chamadas e limitações

- Todos os repositórios usam a linguagem principal já retornada pelo GitHubService.
- Árvores recursivas da branch padrão (`HEAD`) são consultadas em todos os repositórios disponíveis, até o teto configurável de segurança (200 por padrão), por ordem de atualização.
- Até 16 manifestos relevantes por repositório, configurável (1–32), escolhidos com diversidade de ecossistemas, preferência por declarações diretas e caminhos menos profundos; apenas arquivos com tamanho conhecido de até 64 KiB. Cópias de mesmo SHA/formato não ocupam vagas extras.
- No máximo 600 chamadas adicionais e 180 segundos para a inspeção por padrão, configuráveis; sem concorrência ou repetição automática. Respostas externas são limitadas a 3 MiB.
- Manifestos são buscados por SHA de blob presente na árvore; não são executados e não se acessam URLs arbitrárias do conteúdo.
- Falhas, limites reais, árvores truncadas e declarações relevantes ilegíveis geram avisos. Não há aviso só por haver mais de dois manifestos, arquivos redundantes ou configurações já identificáveis pela árvore. As evidências já obtidas são preservadas.
- Projetos antigos também são inspecionados. Somente quando um teto ou falha interrompe a consulta a cobertura favorece os mais recentes; a interface informa a limitação.
- Cache persistente fresco continua tendo prioridade e consome zero chamadas GitHub. Navegar entre resultados já carregados reutiliza memória limitada a cinco perfis públicos. Reload de `/perfil/{username}` ou páginas com `?perfil=username` consulta o backend, que decide pelo cache; abrir pela lista pessoal usa o snapshot persistido mesmo expirado. Force refresh é explícito. A preferência de tema continua persistida.
- Não há prova de autoria, testes efetivamente executados, qualidade, domínio nem semântica REST. Dependências podem estar declaradas e não utilizadas.

## Registro histórico da primeira fase (não representa os limites atuais)

Consulta real de `leomacedo2`: 25 repositórios considerados, 12 árvores consultadas, 22 chamadas adicionais e 12 skills. Foram geradas sugestões de testes, APIs Python e CI/CD. Esses números podem mudar; a análise informa cobertura parcial.

Testes automatizados cobrem detecção positiva/negativa, manifestos, exclusões, contagem por repositório, níveis, recomendações, orçamento, falhas parciais, perfil vazio e cancelamento, além dos testes existentes de integração.

Validação: builds do backend .NET 8 e frontend concluídos; 27 testes aprovados. Navegação real entre Home, Skills, Recomendações e Repositórios verificada no navegador, com 25 repositórios e ambos os temas. Uma segunda consulta teve cobertura de 11 árvores; o resultado parcial continuou disponível.

Criados:

- `backend/Portfolio.Api/DTOs/AnalysisDto.cs`
- `backend/Portfolio.Api/Models/RepositoryInspection.cs`
- `backend/Portfolio.Api/Services/AnalysisService.cs`
- `backend/Portfolio.Api/Services/SkillDetector.cs`
- `backend/Portfolio.Api/Services/RecommendationService.cs`
- `backend/Portfolio.Api/Controllers/AnalysisController.cs`
- `backend/Portfolio.Api.Tests/AnalysisTests.cs`
- `frontend/src/components/ProfileCard.jsx`
- `frontend/src/components/SkillCard.jsx`
- `frontend/src/components/RecommendationCard.jsx`
- `frontend/src/pages/HomePage.jsx`
- `frontend/src/pages/SkillsPage.jsx`
- `frontend/src/pages/RecommendationsPage.jsx`
- `frontend/src/pages/RepositoriesPage.jsx`
- `docs/analise.md`

Modificados: `backend/Portfolio.Api/Program.cs`, `Services/IGitHubService.cs`, `Services/GitHubService.cs`; `frontend/package.json`, `package-lock.json`, `src/App.jsx`, `src/main.jsx`, `src/services/api.js`; `README.md`.

A autenticação de aplicação e o controle de cota estão descritos em [github-autenticacao.md](github-autenticacao.md). Esgotamento de cota na paginação agora preserva o perfil e a lista parcial, com avisos propagados à análise.

Portas, perfil do Visual Studio e tasks do VS Code foram mantidos. Antes de rodar o frontend, execute a task de instalação de dependências para obter o React Router.

Referências: [árvores Git do GitHub](https://docs.github.com/en/rest/git/trees), [React Router](https://reactrouter.com/start/declarative/routing).

## Registro histórico da ampliação individual

Validação real em 17/09/2026 com `leomacedo2`: 25/25 árvores inspecionadas, 24 inspeções sem cortes e 41 chamadas GitHub (2 para perfil/listagem + 39 adicionais). Único corte: `AprendendoReact`, com mais de dois manifestos. Foram identificadas 18 skills, incluindo Spring Boot, Docker, SQL Server e Entity Framework Core. A cota observada foi 5000, com 4933 restantes após a consulta. Os 50 testes passaram e o build do frontend foi concluído.

Configuração atual em `Analysis:Individual`: `MaxRepositories=200`, `MaxRequests=600`, `TimeoutSeconds=180`, `MaxManifestsPerRepository=16`. Variáveis equivalentes usam `Analysis__Individual__` como prefixo. Valores válidos: 1–1000 repositórios, 1–3000 chamadas adicionais, 1–180 segundos, 1–32 manifestos. São tetos de segurança, não metas de consumo. O prazo do frontend permanece 245 segundos local e 330 segundos em produção para acomodar o início do Render Free. Não há estratégia de turma implementada.

Árvores são obtidas uma vez por repositório distinto; conteúdo de manifestos é reutilizado por SHA dentro da mesma análise (inclusive entre repositórios). A persistência/cache foi acrescentada posteriormente; nesta rodada o teto de dois foi substituído por 16. Continuam o limite de 64 KiB, autenticação no backend e bloqueio por rate limit.

`inspectedRepositories` conta árvores obtidas; `completelyInspectedRepositories` conta inspeções sem cortes/erros nas regras atuais. A diferença pode ocorrer por árvore truncada, limite de manifestos, tamanho, erro ou orçamento. Nenhuma dessas contagens certifica entendimento integral do código. `totalGitHubRequests` conta chamadas efetivamente enviadas pelo cliente, incluindo perfil, paginação e inspeção; chamadas impedidas pelo rate limiter não entram. `additionalRequests` é o orçamento de tentativas adicionais, podendo incluir uma tentativa bloqueada localmente.

Entity Framework Core é identificado pelos pacotes do ORM; SQL Server pelo provider EF SqlServer ou drivers SQL Server explícitos. SQLite, PostgreSQL, MySQL e SQLAlchemy também possuem detecção específica. Arquivo SQL sozinho não identifica o fabricante do banco. Evidências na página de Skills são recolhíveis, fechadas inicialmente.

Arquivos desta etapa: novo `Services/IndividualAnalysisOptions.cs` e `Portfolio.Api.Tests/IndividualCoverageTests.cs`; alterações em AnalysisService, SkillDetector, RecommendationService, GitHubService, IGitHubService, GitHubRateLimitHandler (somente marcação de contagem), AnalysisDto, Program, appsettings, AnalysisTests, App.jsx, SkillCard.jsx, services/api.js e documentação. A autenticação e as decisões de bloqueio por rate limit foram preservadas.
