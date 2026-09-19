# PostgreSQL, contas da plataforma e análises salvas

## Arquitetura entregue

React → ASP.NET Core 8 → EF Core 8 → PostgreSQL. O Supabase hospeda somente o banco. Não são utilizados Supabase Auth, SDK, Edge Functions, chaves anon/service_role nem conexão do React ao banco.

Pacotes fixados: `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11`, Identity/EF Design `8.0.31`. Ferramenta local `dotnet-ef 8.0.31` em `.config/dotnet-tools.json`. O SDK continua .NET 8 e as portas continuam 5080/5173.

`PersistentAnalysisService` envolve o `AnalysisService` existente. O detector, catálogo de trilhas, serviço de recomendações, OAuth App do GitHub, rate limiting e orçamento da inspeção não foram substituídos. Visitantes continuam consultando perfis e todas as páginas de resultados.

## Modelo e snapshots

Todas as tabelas ficam no schema `portfolio`, separado de `public` e do schema `auth` do Supabase. Não inclua `portfolio` nos schemas expostos pela Data API do Supabase. O acesso ocorre exclusivamente pelo backend.

| Entidade | Finalidade |
| --- | --- |
| `ApplicationUser` / `AspNetUsers` | Identity com Nome e DataCadastro; hash de senha gerenciado pelo Identity |
| `GitHubProfile` | Identidade pública compartilhada; GitHubId único e Username normalizado em minúsculas com índice único |
| `Analysis` | Snapshot imutável com data, expiração, cobertura, avisos e perfil/contadores em metadados JSONB |
| `RepositoryAnalysis` | Repositórios da coleta; chave AnalysisId + RepositoryId |
| `SkillAnalysis` → `Evidence` | Skills, níveis, recorrência e motivos encontrados nos repositórios |
| `Recommendation` | Assunto, trilha, motivo, próximo passo e referências às skills consideradas |
| `UserSavedProfile` | Relação muitos-para-muitos, chave UserId + GitHubProfileId e SavedAt |

Um perfil possui vários snapshots; cada snapshot possui suas coleções ordenadas. As recomendações reutilizam as evidências das skills do mesmo snapshot. Os metadados não duplicam as coleções. A criação do grafo é transacional. Uma falha não substitui pela metade o resultado anterior. Perfil/skills antigos não são misturados com repositórios de uma coleta nova.

A lista pessoal acompanha a **última análise compartilhada do perfil**, não congela uma versão específica para cada usuário. Remover da lista apaga somente o vínculo; snapshots, perfil e vínculos de outras contas permanecem.

## Autenticação

Cadastro e validação de senha usam `UserManager<ApplicationUser>` e `SignInManager`. O token opaco é emitido pelo handler oficial `AddBearerToken` do ASP.NET Core, protegido com Data Protection; não existe JWT artesanal. A API usa `[Authorize]` nos endpoints pessoais e obtém o UserId da identidade autenticada, nunca de parâmetros enviados pelo visitante.

O React mantém o access token **somente na memória da aba**, enviando-o como `Authorization: Bearer` para endpoints pessoais. Não há cookies de autenticação, localStorage de tokens ou credenciais GitHub no frontend. Assim, o navegador não anexa credenciais automaticamente a requisições de outro site. CORS permite apenas a origem configurada. O logout apaga a sessão local; o token emitido expira em 30 minutos. Recarregar a página exige novo login. O handler oficial emite também refresh token, mas ele não é armazenado ou utilizado, e não há endpoint de renovação nesta etapa.

Identity aplica senha de 8–128 caracteres, maiúscula, minúscula, número e símbolo. Cinco falhas de senha bloqueiam a conta por cinco minutos. Os endpoints de autenticação também têm limite de 30 requisições por minuto por IP. Não há confirmação de e-mail ou recuperação de senha nesta fase. HTTPS será obrigatório quando houver publicação; o HTTP atual é exclusivo do desenvolvimento local. A configuração e persistência das chaves Data Protection precisam ser tratadas antes de distribuir o backend entre servidores.

Referência: [Identity para SPAs no ASP.NET Core 8](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-8.0).

## Cache e consumo do GitHub

- `GitHubAnalysisCacheHours` vale `6` inicialmente; aceita valores maiores que zero até 168 horas. Pode ser sobrescrito por configuração de ambiente.
- `GET /api/analyses/{username}` normaliza o username e usa o snapshot mais recente válido. O retorno traz `source: "cache"` e `currentGitHubRequests: 0`.
- Sem cache válido, a coleta existente é executada e persistida, retornando `source: "github"`.
- `POST /api/analyses/{username}/refresh` ignora o cache válido por solicitação explícita. O botão **Atualizar pelo GitHub** usa esse endpoint.
- Consultas simultâneas ao mesmo username são coordenadas dentro desta instância da API; uma atualização concluída enquanto outra aguardava é reutilizada. Não há retentativas em loop.
- Snapshots parciais também são persistidos com seus avisos e a mesma validade. A interface distingue cobertura parcial de validade temporal; o botão de atualização permite tentar novamente depois.
- Falha GitHub antes de obter um novo resultado: se houver snapshot anterior, ele é retornado com um aviso da falha e sua data original. Sem snapshot anterior, o erro original permanece.
- `totalGitHubRequests` registra a coleta original; `currentGitHubRequests` registra o consumo desta requisição. A interface mostra ambos, sem fingir que o cache repetiu as chamadas originais.
- **Abrir análise** na lista pessoal sempre lê o banco, mesmo se expirado. **Atualizar** solicita uma coleta nova.
- Sem connection string, o modo visitante continua funcionando em memória, com aviso de que cache/salvamento estão indisponíveis. Cadastro/login retornam 503 orientando configurar o banco. Com string configurada e banco indisponível, a API retorna 503 em vez de fazer chamadas GitHub que não poderia persistir.

## Criar seu projeto Supabase Free

1. Acesse o [Dashboard Supabase](https://supabase.com/dashboard), crie sua conta ou entre.
2. Crie/selecione uma organização no plano **Free** e clique em **New project**.
3. Informe um nome, escolha a região e defina uma senha forte para o **banco PostgreSQL**. Guarde-a em seu gerenciador de senhas; ela não é a senha da conta Supabase nem uma API key.
4. Confirme a criação no plano gratuito e aguarde o banco ficar pronto. Nenhum projeto é criado automaticamente por este repositório.
5. No projeto, clique em **Connect**. Para este backend local em rede IPv4, selecione **Session pooler**. Copie os campos **host, port, database e user** exatamente como mostrados; use a senha de banco definida anteriormente. O usuário do pooler costuma ser `postgres.REFERENCIA_DO_PROJETO`, e a porta é normalmente `5432`. Não deduza o host a partir da região.
6. Se sua rede suporta IPv6, pode usar **Direct connection**, normalmente usuário `postgres` e porta `5432`. A conexão direta é a recomendação do Supabase para migrations. Session pooler é a alternativa para redes IPv4 que precisam de semântica de sessão. Não use Transaction pooler/porta 6543 para este roteiro.

Fontes: [criação de projeto](https://supabase.com/docs/guides/getting-started/quickstarts/reactjs) (somente a etapa de criar o banco; não siga a integração direta React desse tutorial), [conexões PostgreSQL](https://supabase.com/docs/guides/database/connecting-to-postgres), [pooling e limites](https://supabase.com/docs/guides/database/connecting-to-postgres/pooling-and-limits).

## Connection string e User Secrets

O Npgsql espera pares `Chave=Valor;`, **não** a URI `postgresql://...` copiada do painel. Converta os campos exibidos em Connect para o formato abaixo. Todos os exemplos são fictícios:

```text
Host=HOST_EXATO_DO_POOLER;Port=5432;Database=postgres;Username=postgres.REFERENCIA_FICTICIA;Password="SENHA_FICTICIA";SSL Mode=VerifyFull;Pooling=true;Maximum Pool Size=10;Timeout=15;Command Timeout=30
```

Use os campos reais somente na sua máquina. A senha deve ser o valor literal, não URL-encoded. Valores com ponto e vírgula precisam estar entre aspas duplas; aspas internas são duplicadas segundo o formato de connection string. `SSL Mode=VerifyFull` verifica certificado e host. Se seu ambiente não confiar na CA do servidor, instale/use o certificado CA indicado pelo Supabase com `Root Certificate=C:/caminho/ca.crt`; não desative a validação.

Na raiz `ProjetoFinal`, este é o comando com valores fictícios:

```powershell
dotnet user-secrets set "ConnectionStrings:Portfolio" 'Host=HOST_EXATO_DO_POOLER;Port=5432;Database=postgres;Username=postgres.REFERENCIA_FICTICIA;Password="SENHA_FICTICIA";SSL Mode=VerifyFull;Maximum Pool Size=10' --project backend/Portfolio.Api
```

O projeto já possui UserSecretsId; não precisa executar `init`. Para evitar deixar o valor real no histórico do terminal, prefira a entrada interativa e o stdin do User Secrets:

```powershell
$dbSecret = Read-Host 'Cole sua connection string Npgsql somente aqui' -AsSecureString
$dbCredential = [System.Net.NetworkCredential]::new('', $dbSecret)
@{ 'ConnectionStrings:Portfolio' = $dbCredential.Password } | ConvertTo-Json -Compress | dotnet user-secrets set --project backend/Portfolio.Api
Remove-Variable dbSecret, dbCredential
```

Não envie a string/senha ao chat e não execute `user-secrets list` em saídas compartilhadas. User Secrets mantém o valor fora do projeto, mas não é um cofre criptografado. Variáveis de ambiente são a alternativa no servidor: configure **`ConnectionStrings__Portfolio`** pelo gerenciador de segredos do ambiente. Exemplo fictício local:

```powershell
$env:ConnectionStrings__Portfolio = 'Host=localhost;Port=5432;Database=portfolio;Username=portfolio;Password=SENHA_FICTICIA'
```

Variáveis de ambiente têm precedência sobre User Secrets. Não coloque a string em appsettings versionado, tasks.json, launchSettings.json, `.env` do React ou variáveis `VITE_*`. A configuração GitHub anterior continua independente e não precisa ser alterada.

## PostgreSQL local como alternativa

Em uma instalação PostgreSQL local, crie um banco `portfolio` e um usuário proprietário com permissão para criar o schema/tabelas. Use host `localhost`, porta da sua instalação, database `portfolio`, usuário e senha correspondentes no mesmo formato Npgsql. Não precisa instalar Supabase local nem SDK. Aplique a mesma migration descrita abaixo. Os testes automatizados usam um banco descartável PostgreSQL 17 via Docker, sem modificar seu banco local ou Supabase.

## Aplicar migrations e testar conexão

Abra um terminal integrado na raiz do projeto. Para ler User Secrets também nos comandos EF, defina Development explicitamente. `launchSettings.json` não é aplicado aos comandos da ferramenta EF:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore
dotnet ef migrations list --project backend/Portfolio.Api
dotnet ef database update --project backend/Portfolio.Api
dotnet ef migrations list --project backend/Portfolio.Api
```

A migration inicial **InitialPostgreSql já está criada**; basta aplicá-la. Para uma futura alteração intencional do modelo:

```powershell
dotnet ef migrations add NomeDaProximaAlteracao --project backend/Portfolio.Api --output-dir Data/Migrations
dotnet ef database update --project backend/Portfolio.Api
```

Não gere outra migration inicial. A ferramenta é local e fixada em EF 8. O aplicativo não executa migrations automaticamente ao iniciar. Se o build estiver bloqueado pelo Visual Studio, pare a depuração antes dos comandos EF.

No Supabase SQL Editor, execute somente estas consultas de verificação:

```sql
select table_name from information_schema.tables
where table_schema = 'portfolio' order by table_name;

select "MigrationId", "ProductVersion"
from portfolio."__EFMigrationsHistory";
```

Devem aparecer tabelas Identity e as tabelas de perfis/análises/vínculos. No Table Editor selecione o schema `portfolio`, se disponível. Isso não exige expor o schema pela Data API.

Inicie a API e consulte o diagnóstico (também disponível em produção após a preparação descrita no [guia de deploy](deploy.md), limitado a 10 consultas/minuto):

```powershell
Invoke-RestMethod http://localhost:5080/api/database/status
```

Sucesso: `connected: true`, `migrationsApplied: true`. Esse endpoint não mostra configuração nem chama o GitHub. Retorna 503 para ausência de configuração, falha de conexão ou migrations pendentes. Verifique senha do banco, host/usuário exatos, SSL, acessibilidade IPv4/IPv6 e se o projeto está ativo no painel caso falhe.

## Executar e validar o fluxo

Backend: abra `Portfolio.sln` no Visual Studio 2022, defina `Portfolio.Api` como inicialização, selecione perfil **http** e F5/Ctrl+F5. Porta 5080, Development. Reinicie após alterar secrets. Frontend: abra `ProjetoFinal` no VS Code → Terminal → Executar Tarefa → **Frontend: instalar dependências** na primeira vez → **Frontend: iniciar Vite**. Abra http://localhost:5173.

1. Como visitante, analise `leomacedo2`; confira Perfil, Skills, Recomendações e Repositórios.
2. Repita com o mesmo username. A tela deve mostrar **Análise recuperada do cache**, a mesma data e **0 chamadas GitHub nesta consulta**. `LEOMACEDO2` reutiliza o mesmo cache.
3. Clique em Salvar como visitante: verá orientação e links Entrar/Criar conta.
4. Abra `/cadastro`, preencha nome/e-mail/senha/confirmação e crie a conta. Senhas diferentes ou fracas devem falhar.
5. Abra `/login`, teste primeiro uma senha errada e depois a correta. O topo deve mostrar seu nome, Minhas análises e Sair.
6. Volte a Perfil, consulte o perfil novamente se recarregou a página e salve. Repita com outro perfil público para conferir vários cards em `/minhas-analises`.
7. Abra um card: os dados devem vir do banco. **Atualizar** faz coleta nova; respeita orçamento e rate limit. Evite atualizações repetidas desnecessárias na apresentação.
8. Remova um card e consulte novamente pela Home: o cache continua disponível. Outra conta que salvou o mesmo perfil mantém seu vínculo.
9. Saia: a área pessoal passa a oferecer login. Recarregar também encerra a sessão local, mas novo login restaura a lista persistente.

Para conferir o cache sem imprimir todo o conteúdo, com a API ligada:

```powershell
$first = Invoke-RestMethod http://localhost:5080/api/analyses/leomacedo2
$second = Invoke-RestMethod http://localhost:5080/api/analyses/LEOMACEDO2
$first, $second | Select-Object id, source, analyzedAt, currentGitHubRequests
```

A primeira chamada também pode retornar cache se já existir uma coleta recente. A atualização manual é `POST /api/analyses/leomacedo2/refresh` e consome GitHub; não é necessária para provar a leitura do cache.

## Testes e limites conhecidos

```powershell
dotnet build Portfolio.sln
dotnet test Portfolio.sln
npm --prefix frontend run build
```

Para a suíte completa, Docker Desktop precisa estar ligado em modo Linux containers. Testcontainers inicia PostgreSQL 17 descartável, aplica a migration real e remove o ambiente ao terminar. Os testes substituem o GitHub por respostas sintéticas. Para rodar apenas a suíte anterior sem Docker: `dotnet test Portfolio.sln --filter "FullyQualifiedName!~PersistenceTests"`.

Validado nesta etapa: 73 testes aprovados, migrations aplicadas em PostgreSQL 17 local, builds backend/frontend e ausência de alterações pendentes no modelo EF. Cadastro, login, cache, salvar/abrir e logout também foram conferidos no navegador com dados fictícios, nos temas claro e escuro. Nenhuma chamada real ao GitHub foi necessária. A conexão ao seu Supabase ainda depende da criação/configuração do projeto por você.

Limites: coordenação de coletas apenas dentro de uma instância da API; snapshots antigos ainda não têm política de descarte; lista pessoal ainda não tem paginação; tokens não têm revogação imediata no logout (expiram em 30 minutos); não há recuperação/confirmação de e-mail. Não há deploy, vagas, turmas, IA, login GitHub de visitante ou repositórios privados.

## Principais arquivos desta etapa

- `.config/dotnet-tools.json`
- `backend/Portfolio.Api/Portfolio.Api.csproj`, `Program.cs`, `appsettings.json`
- `backend/Portfolio.Api/Data/Entities.cs`, `PortfolioDbContext.cs`, `AnalysisSnapshot.cs`, `DatabaseExceptionHandler.cs`, `Migrations/*`
- `backend/Portfolio.Api/Controllers/AuthController.cs`, `SavedProfilesController.cs`, `AnalysisController.cs`
- `backend/Portfolio.Api/Services/PersistentAnalysisService.cs`; pequenos acréscimos em `AnalysisService.cs` e `GitHubService.cs` para contagem/ID
- `backend/Portfolio.Api/Models/GitHubModels.cs`, `DTOs/PortfolioDto.cs`, `DTOs/AnalysisDto.cs`
- `backend/Portfolio.Api.Tests/Portfolio.Api.Tests.csproj`, `PersistenceTests.cs`, `VisitorWithoutDatabaseTests.cs`
- `frontend/src/auth/AuthContext.jsx`, `pages/AuthPage.jsx`, `pages/SavedAnalysesPage.jsx`, `services/api.js`, `App.jsx`, `main.jsx`
- `README.md`, `docs/persistencia.md`
