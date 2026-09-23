# Refinamento do GuiaDoGit — cobertura, navegação, gráficos e trilhas

## Escopo e preservação

Esta rodada altera somente cobertura de manifestos, fluxo de páginas, gráficos de evidências, ordenação da lista pessoal e recomendações. Foram preservados .NET 8, PostgreSQL/Supabase, EF Core 8, Identity, bearer oficial, conexão OAuth opcional, tokens protegidos, cotas separadas, cache compartilhado e force refresh. Sem turmas, professores, vagas, recuperação de senha, IA ou novo sistema de autenticação.

Não houve acesso a User Secrets, consultas reais à API GitHub, alteração externa do OAuth App, migration no Supabase ou deploy. Os testes de persistência usam PostgreSQL descartável; chamadas externas são simuladas. Nenhuma migration foi criada: as trilhas adicionais são armazenadas no `MetadataJson` existente.

## Por que aparecia “leitura limitada a dois manifestos”

O `AnalysisService` selecionava os manifestos reconhecidos por profundidade do caminho e ordem alfabética, usando `Take(2)`. Se houvesse um terceiro candidato, adicionava um aviso, e qualquer aviso tornava a análise parcial. Esse teto nasceu como economia de chamadas, mas interrompia monorepos pequenos mesmo com cota e orçamento disponíveis. Dois package.json podiam ocupar as duas vagas antes do csproj, por exemplo.

### Estratégia atual

- **16 manifestos relevantes por repositório**, configurável entre 1 e 32 por `Analysis:Individual:MaxManifestsPerRepository` ou `Analysis__Individual__MaxManifestsPerRepository`.
- Seleção em `ManifestSelector`: declarações diretas antes de lockfiles; caminhos mais próximos da raiz; rodadas entre ecossistemas npm, .NET, Maven, Gradle, Python, PHP e Ruby antes de repetir arquivos da mesma família. Desempate estável pelo caminho.
- Cópias de mesmo conteúdo/formato, identificadas pelo SHA, não ocupam novas vagas. Blobs já obtidos são reutilizados na mesma análise, inclusive entre repositórios.
- `package.json`, `.csproj`, `pom.xml`, `build.gradle`, `build.gradle.kts`, `requirements.txt`, `pyproject.toml`, `Pipfile`, `composer.json` e `Gemfile` têm leitura de declarações de dependência. TOML usa Tomlyn; scripts de build nunca são executados.
- `package-lock.json` é fallback quando falta `package.json` no mesmo diretório; versões 2/3 fornecem dependências **diretas da raiz**, não pacotes transitivos. Lock antigo sem essa estrutura não permite inferência segura e gera aviso.
- `vite.config.*`, `tsconfig*.json`, `.sln`/`.slnx`, Dockerfile, Compose e arquivos de Actions fornecem evidências pela árvore já consultada. O conteúdo não é baixado quando a regra precisa apenas da presença/caminho.
- Excluem-se `node_modules`, `vendor`, `dist`, `build`, `bin`, `obj`, `.git`, `wwwroot/lib`, `.min.js`, ambientes Python, `third_party`, `third-party`, `bower_components`, `.next`, `.nuxt`, `coverage` e `__pycache__`.

O aviso de cobertura parcial agora se refere a cortes reais: mais de 16 declarações relevantes distintas, arquivos grandes/ilegíveis, árvore truncada, falha externa, prazo, teto de repositórios ou orçamento. Arquivos redundantes ou sinais obtidos somente pela árvore não tornam a cobertura parcial. “Sem cortes” significa cobertura das regras atuais, não compreensão integral do código.

### Custo das consultas

| Situação | Chamadas esperadas |
| --- | --- |
| Snapshot fresco no cache | **0 ao GitHub**, inclusive com GitHub conectado |
| Navegar pelas páginas de um resultado carregado | 0 novas consultas de análise |
| Coleta inicial | 1 perfil + paginação de repositórios já existente |
| Inspeção antiga por repositório | 1 árvore + até 2 blobs |
| Inspeção atual por repositório | 1 árvore + até 16 blobs únicos necessários |
| Configuração reconhecível pela árvore | 0 downloads adicionais |
| Mesmo blob já lido na análise | 0 downloads adicionais |

Para um repositório que antes alcançava o teto, o máximo teórico sobe de 3 para 17 chamadas adicionais (**até 14 a mais**). Um repositório com quatro declarações distintas usa cinco chamadas de inspeção, em vez de parar artificialmente em três. Não se trata de reservar 17 chamadas para todo repositório.

Continuam os tetos padrão globais de **600 chamadas adicionais, 200 repositórios e 180 segundos**, além de 64 KiB por manifesto, limite de resposta HTTP e bloqueio por rate limit. O limite de 600 não inclui perfil/paginação inicial. Não foi acrescentado paralelismo, retry automático ou aumento de orçamento conforme autenticação. Repositórios posteriores ainda podem ficar somente com metadados se o orçamento terminar. A tela informa árvores, inspeções sem cortes e chamadas reais da consulta/coleta original.

Todos os repositórios disponíveis continuam contribuindo com a linguagem principal. A exclusão de pastas filtra a inspeção de arquivos; a linguagem principal é um metadado recebido do GitHub, não uma conclusão de autoria.

## Rotas e navegação

| Rota | Comportamento |
| --- | --- |
| `/` | Entrada limpa: GuiaDoGit, texto explicativo, e-mail/senha, Entrar, Criar conta e Continuar como visitante |
| `/login` | Mesmo formulário de entrada, preservando links existentes |
| `/cadastro` | Cadastro atual, sem login com GitHub ou recuperação de senha |
| `/minhas-analises` | Destino após login; requer conta para consultar a lista pessoal |
| `/analisar` | Formulário independente, vazio, placeholder “Digite um usuário do GitHub” |
| `/perfil/{username}` | Resultado do perfil indicado: perfil, data, origem, cobertura, skills, gráficos e resumo das trilhas |
| `/perfil` | Compatibilidade: abre perfil ativo, ou `/analisar` quando não há resultado |
| `/skills?perfil=username` | Gráfico das tecnologias, categorias e evidências recolhíveis |
| `/recomendacoes?perfil=username` | Trilhas relevantes, filtros por área, atividades e motivos recolhíveis |
| `/repositorios?perfil=username` | Lista completa já existente |
| `/github` | Conexão opcional existente, disponível após login |

Login bem-sucedido abre `/minhas-analises`. Uma conta sem perfis vê “Você ainda não possui análises salvas.” e **Fazer minha primeira análise**, que abre `/analisar`. Se o usuário já está autenticado, `/` também o encaminha à lista.

Visitante escolhe **Continuar como visitante**, analisa e navega normalmente. Salvar continua exigindo conta. Não há username público predefinido; `leomacedo2` permanece apenas como exemplo de teste na documentação histórica.

Após sucesso em `/analisar`, a aplicação abre `/perfil/{username}` automaticamente. Não mistura o resultado com o formulário. O contexto de análise reutiliza até cinco resultados públicos em memória e evita requisições concorrentes repetidas para a mesma consulta. Não grava tokens ou snapshots no localStorage.

Uma URL direta de resultado recupera aquele username pelo backend; se o cache expirou, a regra atual pode iniciar nova coleta. **Ver perfil** da lista pessoal continua abrindo o snapshot persistido, mesmo expirado, sem coleta. Refresh do navegador mantém o tema, mas encerra a sessão da plataforma em memória, como antes.

### Minhas análises

O backend ordena pela data da **última análise compartilhada, mais recente primeiro**. Desempates usam data de salvamento e username. Perfis sem snapshot ficam ao final. Cards exibem avatar, nome, username, principais skills e última atualização. **Atualizar** mantém o POST de force refresh; **Remover** apaga somente o vínculo pessoal e preserva análises/cache. A atualização de um snapshot compartilhado pode mudar sua posição na lista de quem o salvou.

## Gráficos e interface

Biblioteca única: **Chart.js 4.5.1**, com integração React por **react-chartjs-2 5.3.1**. Componentes do gráfico são carregados sob demanda, separados do JavaScript inicial. Não há pacote de gráficos duplicado.

1. Perfil: barras horizontais de até oito linguagens, usando `repositoryCount` das skills da categoria Linguagens.
2. Perfil: barras de repositórios distintos por categoria real. IDs são deduplicados entre skills da mesma categoria.
3. Skills: barras das dez tecnologias com maior contagem de repositórios, seguidas por categorias recolhíveis e detalhes de evidência.

Uma barra maior significa mais repositórios com sinais, **não proficiência, porcentagem de código ou certificação**. Um repositório pode pertencer a várias linguagens/categorias. Não se inventam categorias ou dados para perfis vazios.

Gráficos acompanham `data-bs-theme`, são responsivos e incluem tabela acessível com os mesmos números. Navbar tem menu recolhível no celular; botões e cards usam Bootstrap e variáveis do tema. O dark mode continua padrão e a preferência permanece no localStorage.

Referências: [Chart.js — integração](https://www.chartjs.org/docs/latest/getting-started/usage), [responsividade](https://www.chartjs.org/docs/latest/configuration/responsive.html), [acessibilidade](https://www.chartjs.org/docs/latest/general/accessibility.html), [integração React](https://react-chartjs-2.js.org/).

## Trilhas e recomendações

Catálogo estruturado com sinais, pré-requisitos, progressão e atividade prática. A recorrência por repositórios ajuda a selecionar afinidade; não é exibida como nota. O sistema escolhe **até três áreas**, apenas uma stack backend prioritária e **até quatro próximos passos por trilha**. Pode retornar menos quando não houver contexto. Há ainda até três prioridades resumidas.

Frontend ganhou testes contextualizados, acessibilidade, estado e performance; Java aceita Maven/Gradle; .NET respeita SQL demonstrado sem impor SQL Server. Full Stack requer frontend e framework backend e combina as evidências reais do perfil. Mobile e Dados só aparecem com sinais específicos. Assuntos repetidos e aliases de SQL são deduplicados entre trilhas. Tecnologias observadas não são sugeridas novamente como novidade.

Exemplos sintéticos:

- **HTML + CSS + JavaScript**: Frontend com TypeScript, React e Consumo de API; sem Docker, Spring ou CI/CD automático.
- **Java + Gradle + Spring Boot + REST API**: Backend Java com JPA, JUnit e Docker; sem obrigação de adotar Maven ou outra linguagem backend.
- **React + TypeScript + React Router + Consumo de API + C# + ASP.NET Core + Web API + EF Core + PostgreSQL + xUnit**, com sinais web: Frontend com testes/acessibilidade/estado; Backend .NET conserva o que já foi observado; Full Stack sugere autenticação integrada, testes de integração e Docker. Não recomenda PostgreSQL/SQL Server como novidade.

Motivos indicam as skills consideradas e a recorrência; cada passo tem atividade prática. Ausência de evidência não significa desconhecimento. Sinais agregados podem vir de repositórios diferentes; Full Stack não afirma que já existe integração entre eles. Detecção de ferramentas de autenticação não comprova autenticação integrada. Veja [trilhas.md](trilhas.md).

## Cache e compatibilidade

Snapshots anteriores continuam armazenados e legíveis. Os novos campos `analysisVersion=2`, `manifestSafetyLimit` e `learningTracks` entram no JSON de metadados de coletas novas. Ausência desses campos identifica resultados antigos; a interface explica que **Atualizar pelo GitHub** aplica as regras atuais.

**Um aviso antigo de “dois manifestos” pode continuar aparecendo em snapshot antigo.** Não se apaga o aviso sem refazer a coleta nem se ignora cache fresco automaticamente. Atualize explicitamente um perfil quando desejar testar as novas regras. O prazo de seis horas continua significando apenas frescor, não exclusão.

## Arquivos

### Backend — criados

- `backend/Portfolio.Api/Services/ManifestSelector.cs`
- `backend/Portfolio.Api/Services/ManifestDependencies.cs`
- `backend/Portfolio.Api/Services/LearningTrackRecommendations.cs`
- `backend/Portfolio.Api.Tests/ManifestCoverageTests.cs`
- `backend/Portfolio.Api.Tests/ExpandedTrackTests.cs`

### Backend — modificados

- `backend/Portfolio.Api/Controllers/SavedProfilesController.cs`
- `backend/Portfolio.Api/DTOs/AnalysisDto.cs`
- `backend/Portfolio.Api/Services/AnalysisService.cs`
- `backend/Portfolio.Api/Services/IndividualAnalysisOptions.cs`
- `backend/Portfolio.Api/Services/SkillDetector.cs`
- `backend/Portfolio.Api/Services/LearningTrackCatalog.cs`
- `backend/Portfolio.Api/Services/RecommendationService.cs`
- `backend/Portfolio.Api/Portfolio.Api.csproj` (Tomlyn para TOML)
- `backend/Portfolio.Api/appsettings.json` (limite não secreto de manifestos)
- `backend/Portfolio.Api.Tests/PersistenceTests.cs`

### Frontend — criados

- `frontend/src/analysis/AnalysisContext.jsx`
- `frontend/src/analysis/chartData.js`
- `frontend/src/components/ChartPanel.jsx`
- `frontend/src/components/EvidenceChart.jsx`
- `frontend/src/pages/AnalyzePage.jsx`
- `frontend/src/pages/AnalysisResultPage.jsx`
- `frontend/src/pages/ProfilePage.jsx`
- `frontend/playwright.config.js`
- `frontend/tests/chart-data.test.js`
- `frontend/tests/e2e/flows.spec.js`

### Frontend — modificados

- `frontend/src/App.jsx`
- `frontend/src/pages/AuthPage.jsx`
- `frontend/src/pages/SavedAnalysesPage.jsx`
- `frontend/src/pages/SkillsPage.jsx`
- `frontend/src/pages/RecommendationsPage.jsx`
- `frontend/src/components/ProfileCard.jsx`
- `frontend/src/styles.css`
- `frontend/index.html`
- `frontend/package.json`
- `frontend/package-lock.json`

`frontend/src/pages/HomePage.jsx` foi substituído pela separação entre `AnalyzePage` e `ProfilePage`. API Axios, AuthContext e GitHubConnectionPage foram preservados. Dockerfile, Vercel, CORS, portas e implementação OAuth não foram alterados.

Documentação: este arquivo foi criado; README, docs/analise.md, docs/trilhas.md, docs/deploy.md e docs/github-autenticacao.md foram atualizados.

## Validação

- **143 testes backend aprovados**, nenhum ignorado: 105 anteriores + 38 casos novos. Cobrem mais de dois manifestos, diversidade/relevância, exclusões, deduplicação, limites reais, formatos, conteúdo inválido, orçamento, trilhas distintas/Full Stack, recorrência, assuntos equivalentes, ordenação e reconstrução de snapshots novos/antigos.
- **4 testes dos dados dos gráficos aprovados**: contagens reais, categorias com IDs únicos, ordenação/limite e dados vazios.
- **14 testes de navegador aprovados**: sete cenários em desktop (1280×900) e celular (390×844), incluindo visitante, login, empty state, logout, snapshot salvo, force refresh, remoção, rotas diretas/reload, username inválido, temas, gráficos e filtros. Nenhum overflow horizontal nas páginas verificadas.
- **161 casos aprovados no total** nas três suítes. Backend/GitHub/Supabase são simulados nos testes de navegador; a suíte backend usa PostgreSQL descartável onde necessário.
- `dotnet build Portfolio.sln -c Release`: **sucesso, zero avisos e zero erros**.
- `npm run build`: **sucesso**, gráficos em bundle separado (~50,8 kB gzip).
- Capturas de tela verificadas em desktop e celular, claro/escuro, disponíveis localmente em `.artifacts/frontend-e2e` (ignoradas pelo Git).

Para reproduzir, na raiz, com Docker Desktop disponível para os testes PostgreSQL:

```powershell
dotnet test Portfolio.sln
dotnet build Portfolio.sln -c Release
cd frontend
npm ci
npm test
npx playwright install chromium
npm run test:e2e
npm run build
```

O navegador de testes usa **4173 somente durante os testes**, com API interceptada e qualquer host externo bloqueado. A aplicação local continua **5173/5080**. Playwright é dependência de desenvolvimento e não é necessário instalar Chromium no Render/Vercel para o build.

## Passos manuais e deploy

1. Atualize dependências locais com `npm ci` em `frontend`. O build .NET restaura Tomlyn automaticamente. Inicie backend no Visual Studio 2022, perfil `http`, e frontend pela task habitual do VS Code.
2. Teste a entrada como visitante, analise um username e confira `/analisar` → `/perfil/{username}`. Em resultado antigo, use **Atualizar pelo GitHub** uma vez para obter as regras novas; isso consome a cota normal.
3. Entre na plataforma: confirme `/minhas-analises`, data/ordem, Ver perfil, Atualizar e Remover. Confira que a conexão OAuth continua disponível sem mudanças.
4. Valide Skills, filtros de trilhas, tabelas dos gráficos, temas e reload de rota. Observe cobertura e número de chamadas no perfil.
5. Revise as alterações, faça commit e push na branch já conectada aos serviços. **Com os auto-deploys atuais habilitados**, isso basta para os builds/deploys normais do Vercel e Render. Esta rodada não exige migration, secret, callback OAuth ou variável obrigatória nova.
6. A única configuração adicional é opcional: `Analysis__Individual__MaxManifestsPerRepository=16` no backend, caso queira sobrescrever o padrão versionado. Não aumente `MaxRequests` para habilitar esta rodada.
7. Após ambos os deploys, teste os domínios existentes, `/health`, entrada, análise, salvar/abrir perfil e acesso direto a uma rota React. Configurações externas e resultado do auto-deploy precisam ser conferidos por você; nenhum deploy foi executado nesta tarefa.

## Limitações conhecidas

- Mais manifestos podem aumentar duração/custo de uma coleta sem cache. Os limites globais, cota e timeout ainda podem produzir cobertura parcial real.
- Dependências copiadas para pastas arbitrárias nem sempre são distinguíveis de arquivos próprios; regras não provam autoria, execução ou qualidade. Gradle dinâmico, heranças, aliases e dependências transitivas não são resolvidos.
- `Autenticação integrada`, `Deploy integrado` e alguns passos conceituais não são comprováveis só por presença de pacotes. Recomendações podem pedir que o usuário documente uma integração que já existe; não afirmam que ele desconhece o assunto.
- Snapshots antigos mantêm suas regras até nova coleta. Sem sinais relevantes, trilhas/gráficos podem ter menos itens ou permanecer vazios.
- Testes visuais foram feitos em Chromium com respostas simuladas; não houve teste real nesta rodada contra produção, GitHub ou Supabase. Render Free continua podendo demorar na primeira chamada, sem ping para impedir sleep.
- Sessão da plataforma continua em memória da aba; recarregar requer novo login para ações pessoais. Os resultados públicos podem ser recuperados pela URL.

A rodada termina aqui. Não foi iniciado o módulo de professores/turmas.
