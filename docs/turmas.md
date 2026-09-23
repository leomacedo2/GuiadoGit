# Turmas e atividade recente — GuiaDoGit

## Escopo entregue

Qualquer conta autenticada pode criar várias turmas com perfis já salvos em **Minhas análises**. Professor é um contexto de uso, não uma role. Não foram adicionados cadastro de alunos, convites, e-mails, importação, permissões avançadas, vagas, recuperação de senha ou IA.

Identidade da plataforma, bearer, OAuth GitHub opcional, proteção de tokens/chaves, cache compartilhado, snapshots, force refresh e limites de API foram preservados. Não houve acesso a User Secrets, consultas reais ao GitHub, alteração externa, aplicação de migration no Supabase ou deploy nesta tarefa.

## Modelo, relacionamentos e integridade

| Entidade | Campos e finalidade |
| --- | --- |
| `Classroom` | Id, ApplicationUserId, Name, CreatedAt, UpdatedAt. Uma conta → várias turmas |
| `ClassroomMember` | ClassroomId, GitHubProfileId, ApplicationUserId, AddedAt. Referencia turma e perfil salvo da mesma conta |
| `RepositoryAnalysis` | Nova coluna opcional PushedAt; demais campos preservados |

Um GitHubProfile pode estar em várias turmas e ser salvo por várias contas. Não são criados perfis duplicados ou contas de alunos. O usuário autenticado é sempre obtido das claims do bearer oficial; campos de proprietário enviados no JSON não têm autoridade.

### Constraints e índices

- PK de Classrooms: `Id`. Chave alternativa única `(Id, ApplicationUserId)` para validar o proprietário da associação.
- FK `Classrooms.ApplicationUserId` → `AspNetUsers.Id`, cascade do dependente; sem alteração nas tabelas Identity.
- PK de ClassroomMembers: `(ClassroomId, GitHubProfileId)`, impedindo aluno duplicado na turma.
- FK composta `(ClassroomId, ApplicationUserId)` → `Classrooms(Id, ApplicationUserId)`, com **cascade**. Excluir turma apaga somente seus membros.
- FK composta `(ApplicationUserId, GitHubProfileId)` → `UserSavedProfiles(UserId, GitHubProfileId)`, com **NO ACTION**. Ela comprova que o perfil está salvo pela conta dona da turma e impede remover esse vínculo enquanto houver membros dependentes.
- Índices: `Classrooms(ApplicationUserId, UpdatedAt)`, `ClassroomMembers(ApplicationUserId, GitHubProfileId)` e `ClassroomMembers(ClassroomId, ApplicationUserId)`, além das PKs/chave alternativa.
- Nome obrigatório, aparado, de 2 a 100 caracteres, validado na API; check constraint do banco também verifica o tamanho do nome aparado. Caracteres de controle não são aceitos pela API.

### Regras de edição

- Criação é atômica e exige **1 a 200 perfis salvos**. O teto protege o MVP no Render Free. IDs repetidos no mesmo pedido de criação são deduplicados.
- Adicionar alunos valida a turma, os perfis salvos e ausência de vínculo. Adição de membro já existente retorna 409, sem inserção parcial.
- A turma mantém pelo menos um aluno; remover o último retorna 409 orientando adicionar outro ou excluir a turma.
- Edições de uma mesma turma usam transação e bloqueio de linha no PostgreSQL. Duas remoções simultâneas não deixam a turma vazia. FKs garantem integridade mesmo se houver remoção simultânea de Minhas análises.
- Turmas são ordenadas por **UpdatedAt decrescente**, desempate por Id. UpdatedAt muda na criação, renomeação e alteração de membros; não muda só porque um snapshot global foi atualizado.
- Alunos são ordenados por nome (ou username na ausência), ignorando maiúsculas/minúsculas; desempate pelo username.

### Remoções

Excluir turma exige confirmação no frontend e remove apenas Classroom/ClassroomMember. Remover aluno exclui apenas ClassroomMember. Nenhuma dessas ações apaga ApplicationUser, UserSavedProfile, GitHubProfile, Analysis, cache ou GitHubConnection.

Ao remover de Minhas análises, a API consulta turmas **da própria conta**. Se estiver em uso, retorna **409** com os nomes das turmas e preserva o vínculo. Turmas de outra conta não são reveladas nem impedem que o usuário remova seu próprio vínculo quando ele não estiver em uso localmente. A FK cobre também a corrida entre adição à turma e remoção da lista pessoal.

## API

Todos os endpoints abaixo exigem autenticação, usam proprietário das claims e enviam `Cache-Control: no-store`. Turma inexistente ou de outra conta retorna 404, sem revelar sua existência.

| Método e rota | Corpo / resultado |
| --- | --- |
| GET `/api/classes` | Turmas da conta, quantidade de alunos e quatro tecnologias principais |
| POST `/api/classes` | `{ "name": "Python Manhã", "profileIds": ["GUID_DE_PERFIL_SALVO"] }`; 201 com id e Location |
| GET `/api/classes/{id}` | Nome/datas, membros, tecnologias, categorias e atividade dos snapshots |
| PUT `/api/classes/{id}` | `{ "name": "Novo nome" }`; 204 |
| DELETE `/api/classes/{id}` | Exclui turma e associações; 204 |
| POST `/api/classes/{id}/members` | `{ "profileIds": ["GUID_DE_PERFIL_SALVO"] }`; 204 |
| DELETE `/api/classes/{id}/members/{profileId}` | Remove uma associação; 204, ou 409 se for o último aluno |

`profileIds` recebe o **GitHubProfileId UUID de Minhas análises**, não o id numérico do GitHub, nem o id de um snapshot. A validação ocorre no backend e no banco, independentemente dos checkboxes.

O endpoint existente `GET /api/me/profiles/{profileId}/analysis` continua sendo utilizado por **Ver perfil**. Ele reconstrói o último snapshot salvo mesmo expirado; não chama a coleta individual automaticamente.

## Fluxo de telas

1. Entre normalmente na plataforma, analise perfis em `/analisar` e salve-os em Minhas análises.
2. Abra **Turmas** no menu autenticado: `/turmas`.
3. Clique em **Nova turma** ou **Criar minha primeira turma**, informe nome e selecione alunos. A lista mostra avatar, nome, username e principais skills, com busca. Seleções continuam marcadas ao filtrar.
4. Clique em **Criar turma**: a aplicação abre `/turmas/{id}`. Sem perfis salvos, o formulário orienta analisar e salvar um perfil; não permite turma vazia.
5. Use **Editar nome** ou **Adicionar alunos**. A seleção para adicionar exclui membros já presentes. Nos cards, **Remover da turma** mantém perfil e análises.
6. **Ver perfil** carrega o snapshot salvo e abre `/perfil/{username}`. Não há análise nova dentro de Turmas, nem Atualizar todos.
7. Exclua a turma somente após confirmar o diálogo. Sua lista pessoal permanece intacta.

Visitante não vê o menu Turmas. `/turmas` e `/turmas/{id}` redirecionam à entrada se não houver sessão. O backend também retorna 401 sem bearer válido. A sessão continua em memória da aba; reload exige novo login, como antes.

## Dashboard persistido

`ClassroomDashboardService` depende apenas de PortfolioDbContext e TimeProvider. Não recebe GitHubService, AnalysisService ou serviços OAuth. Consulta o snapshot **mais recente disponível** de cada GitHubProfile, mesmo expirado, usando projeções sem texto de evidências. O detalhe carrega MetadataJson para ler somente o agregado commitActivity; a listagem não precisa dele.

- **Tecnologias mais presentes:** até dez tecnologias no gráfico; número de alunos com a skill. React em sete repositórios do mesmo aluno contribui **+1 aluno**.
- **Categorias:** número de alunos com ao menos uma skill positiva na categoria real. Várias skills da mesma categoria no aluno contam uma vez.
- **Alunos:** avatar, nome, username, principais skills, idade/data da análise, cobertura e acesso ao perfil. Falta de snapshot ou cobertura parcial é sinalizada; o restante da turma continua disponível.
- **Commits por tecnologia:** soma dos agregados mensais persistidos, descrita abaixo.

As consultas não dependem da disponibilidade do GitHub. O PostgreSQL precisa estar disponível; não foi criada cópia offline privada no navegador.

## Commits por tecnologia

O gráfico temporal foi substituído por commits reais filtrados pelo autor, com coleta limitada e agregados persistidos no MetadataJson. PushedAt continua como metadado e critério de seleção de repositórios, sem gerar barras. Consulte [commits.md](commits.md) para endpoint, limites, persistência, zeros/cobertura parcial, limitações e validação.

Abrir turma ou snapshot salvo continua sem chamar GitHub. A turma soma os commits de cada aluno uma única vez, usando seu snapshot mais recente. Alunos sem commitActivity são sinalizados, sem coleta automática. Os gráficos de tecnologias/categorias continuam usando a unidade Alunos. O temporal usa Commits, top 5 e filtro local 6/12 meses.

## Migration — revisar e aplicar manualmente

Migration: **`20260923151210_AddClassroomsAndRepositoryPushActivity`**.

Cria Classrooms e ClassroomMembers no schema `portfolio` e adiciona uma coluna nullable em RepositoryAnalysis. Não modifica migrations anteriores, não reseta histórico, não recria tabelas existentes e não apaga dados no caminho Up. A aplicação continua sem migration automática. O Down gerado reverte apenas esta alteração e apagaria dados de turmas; não o execute no roteiro de atualização.

Na raiz do repositório, pare a depuração do Visual Studio antes dos comandos EF. A ferramenta local continua EF Core 8.

### Conferir arquivos/SQL sem conectar ao banco

```powershell
dotnet tool restore
dotnet ef migrations list --no-connect --project backend/Portfolio.Api -- --environment Testing --GitHub:OAuth:Enabled false
dotnet ef migrations script 20260919170906_AddGitHubOAuthConnections 20260923151210_AddClassroomsAndRepositoryPushActivity --project backend/Portfolio.Api --output turmas-review.sql -- --environment Testing --GitHub:OAuth:Enabled false
```

O SQL de revisão deve conter somente a coluna nullable, duas tabelas, constraints/índices e o registro desta migration no histórico. Não contém credenciais. O agente gerou/revisou uma cópia em `.artifacts/turmas-migration.sql`; a migration só foi aplicada nos PostgreSQL descartáveis dos testes.

### Aplicar no banco já configurado — ação sua, não executada pelo agente

Os comandos seguintes reutilizam sua configuração local existente em Development (inclusive User Secrets, lidos **pelo comando que você executar**, sem alterar seus valores). Não cole credenciais no código/chat. Variáveis de ambiente existentes continuam tendo precedência; confirme que a configuração aponta para o banco pretendido.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef migrations list --project backend/Portfolio.Api
```

Confira que `InitialPostgreSql` e `AddGitHubOAuthConnections` já estão aplicadas e que somente `AddClassroomsAndRepositoryPushActivity` está pendente. Com essa condição, o comando abaixo aplica **somente a nova migration pendente**, usando explicitamente o alvo:

```powershell
dotnet ef database update 20260923151210_AddClassroomsAndRepositoryPushActivity --project backend/Portfolio.Api
dotnet ef migrations list --project backend/Portfolio.Api
```

Não use alvo `0`, EnsureDeleted, database drop ou recriação de migrations. O ambiente local pode ser iniciado novamente nos ports habituais 5080/5173 após a aplicação. Verifique `/api/database/status`: connected e migrationsApplied devem ser true.

Referência oficial: [gerenciamento de migrations EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing).

## Deploy e sequência manual

1. Revise a migration e aplique-a manualmente antes de disparar o auto-deploy. A alteração é aditiva e compatível com o backend anterior, mas a versão nova exige as novas estruturas.
2. Teste localmente: entrar, criar turma com dois perfis salvos, editar nome, adicionar/remover aluno, conferir bloqueio de remoção de Minhas análises, abrir perfil e excluir turma. Confirme preservação da lista pessoal/cache.
3. Faça commit/push na branch já vinculada aos serviços. **Após a migration, os auto-deploys existentes de Render/Vercel são suficientes**, se estiverem ativos. Aguarde os dois builds terminarem.
4. Teste os domínios existentes, `/health`, `/api/database/status`, Turmas e o perfil individual. Não é necessário mudar OAuth App, callback, certificado Data Protection, connection string, CORS, Frontend__BaseUrl ou VITE_API_BASE_URL.
5. Não force refresh de todos os alunos para preencher o gráfico. Snapshots antigos continuam úteis; atualize individualmente somente quando necessário e observando a cota.

O agente não executou nenhum desses passos contra Supabase/Render/Vercel. Render Free mantém sleep/cold start, sem ping automático para impedir suspensão.

## Validação automatizada e visual

- **165 testes backend aprovados**: 143 anteriores + 17 casos de Turmas + 5 de atividade temporal.
- **6 testes de dados frontend aprovados**: 4 anteriores + 2 de janela temporal/dados ausentes.
- **22 testes de navegador aprovados**: 14 anteriores + 8 de Turmas (quatro cenários em desktop e celular). Criação, seleção/busca, edição, associação/desassociação, bloqueio da lista pessoal, confirmação/cancelamento de exclusão, visitante, empty state, gráficos, temas e abertura de snapshot foram verificados.
- **193 casos aprovados ao todo**, sem testes ignorados no backend.
- `dotnet build Portfolio.sln -c Release`: sucesso, sem avisos/erros.
- `npm run build`: sucesso. Nenhum pacote novo foi necessário.
- EF `has-pending-model-changes`: sem divergência após a migration. SQL Up revisado: nenhuma remoção de dados/tabelas.
- Chromium em 1280×900 e 390×844; capturas revisadas nos temas dark/light, sem overflow horizontal nas páginas verificadas. Os testes E2E agora usam o bundle de produção via Vite preview na porta de teste 4173, com API simulada e hosts externos bloqueados. Isso também verifica o fallback SPA.

Comandos na raiz (Docker Desktop deve estar disponível para os testes PostgreSQL descartáveis):

```powershell
dotnet test Portfolio.sln
dotnet build Portfolio.sln -c Release
npm test --prefix frontend
npm run test:e2e --prefix frontend
npm run build --prefix frontend
```

Na primeira execução de testes de navegador, se necessário: `npm exec --prefix frontend -- playwright install chromium`. Isso instala o navegador somente para testes locais; não é requisito de deploy.

## Arquivos desta rodada

Novos arquivos backend:

- `Data/Classroom.cs`, `DTOs/ClassroomDto.cs`
- `Controllers/ClassroomsController.cs`
- `Services/ClassroomDashboardService.cs`, `Services/RecentActivity.cs`
- migration `20260923151210_AddClassroomsAndRepositoryPushActivity.cs` e respectivo Designer
- testes `ClassroomTests.cs`, `RecentActivityTests.cs`

Arquivos backend modificados:

- `Data/Entities.cs`, `Data/PortfolioDbContext.cs`, `Data/AnalysisSnapshot.cs`, snapshot de modelo EF
- `Models/GitHubModels.cs`, `DTOs/PortfolioDto.cs`, `DTOs/AnalysisDto.cs`
- `Services/GitHubService.cs` (somente mapeamento do campo de push)
- `Controllers/AnalysisController.cs`, `Controllers/SavedProfilesController.cs`, `Program.cs` (registro do dashboard)

Novos arquivos frontend:

- `pages/ClassroomsPage.jsx`, `pages/ClassroomPage.jsx`
- `components/ClassroomMembersForm.jsx`, `components/ClassroomNameForm.jsx`
- `components/ActivityPanel.jsx`, `components/ActivityChart.jsx`, `components/useChartTheme.js`
- `analysis/activityData.js`
- `tests/activity-data.test.js`, `tests/e2e/classrooms.spec.js`

Arquivos frontend modificados:

- `App.jsx`, `services/api.js`, `pages/ProfilePage.jsx`, `styles.css`
- `components/ChartPanel.jsx`, `components/EvidenceChart.jsx` (unidade configurável e tema compartilhado)
- `playwright.config.js` (testar o bundle de produção)

Documentação: README, docs/deploy.md, docs/persistencia.md e este novo guia. As migrations anteriores, implementação OAuth, AuthContext, análise de skills/recomendações, orçamento, configuração Docker/Vercel e variáveis sensíveis permaneceram sem alterações.

## Limitações

- Dashboard representa os snapshots mais recentes disponíveis, com idades/coberturas diferentes; não sincroniza coletas de toda a turma.
- Não há histórico completo de pushes/commits. Snapshots antigos não ganham retroativamente PushedAt.
- Filtros temporais usam os cinco destaques da janela de 12 meses; outras tecnologias continuam nos gráficos gerais.
- Máximo de 200 membros por turma; listagem de turmas ainda sem paginação. Grandes conjuntos podem aumentar o trabalho do banco, embora projeções evitem carregar textos de evidências e histórico completo.
- O mínimo de um membro é garantido pelas operações da API, transações e bloqueio de linha; não há trigger para impedir que um administrador do banco remova diretamente todos os membros.
- Sessão em memória continua encerrando no reload. Não há roles, compartilhamento de turmas entre professores ou contas de alunos.
- Validação automatizada usa PostgreSQL descartável e integrações simuladas; o deploy real depende de sua aplicação manual da migration e smoke test.

A rodada termina em Turmas. Nenhuma funcionalidade posterior foi iniciada.
