# GitHub: autenticação da aplicação e conexão opcional de usuário

A identidade principal continua sendo `ApplicationUser` (ASP.NET Core Identity). Cadastro por nome/e-mail/senha, bearer oficial, visitantes, análises e cache compartilhado continuam funcionando. **Conectar GitHub não cria uma conta da plataforma e não é login com GitHub.**

A conexão vem desabilitada (`GitHub:OAuth:Enabled=false`) para permitir primeiro revisar/aplicar a migration e configurar a proteção das chaves. Os User Secrets existentes não foram lidos nem modificados durante a implementação.

## Dois modos de consulta

1. Visitante ou conta sem conexão utilizável: `GitHubService` mantém `ClientId + ClientSecret` via HTTP Basic exclusivamente no backend. O modo anônimo e o token de aplicação legado continuam disponíveis quando OAuth de usuário está desabilitado; nunca configure o token legado junto com o par do OAuth App.
2. Conta autenticada com conexão válida: a chamada GitHub usa `Authorization: Bearer` com o access token daquela conexão. O frontend envia apenas o bearer **da plataforma** nas consultas; nunca recebe o token GitHub.

Antes de escolher credenciais, `PersistentAnalysisService` consulta o cache global. Um snapshot fresco atende visitantes e contas conectadas sem consulta externa nem leitura/descriptografia da conexão. Force refresh mantém o comportamento atual. Seis horas são frescor, não exclusão: snapshots permanecem no PostgreSQL. Tetos de 200 repositórios, 600 chamadas adicionais, 180 segundos e dois manifestos por repositório não mudaram.

## Fluxo e endpoints

| Endpoint | Autenticação | Comportamento |
| --- | --- | --- |
| `POST /api/github/connect` | Bearer oficial da plataforma | Inicia a tentativa e redireciona ao GitHub. Aceita Authorization ou, somente neste endpoint, o bearer no campo de formulário `platformToken`. |
| `GET /api/github/callback` | State + cookie de correlação + PKCE | Consome a tentativa, troca code pelo token, consulta `GET https://api.github.com/user` e associa ao usuário iniciador. Não depende de bearer no callback. |
| `GET /api/github/connection` | Bearer da plataforma | Retorna estado, username/avatar, datas e quota observada. Nunca retorna token, ciphertext, state ou chave. |
| `DELETE /api/github/connection` | Bearer da plataforma | Remove a conexão local e tentativas pendentes; preserva conta, análises, cache e perfis salvos. |

A interface `/github` abre uma janela separada por um formulário POST. Isso mantém a sessão em memória na aba original e permite criar o cookie de correlação em navegação principal no domínio Render, sem depender de cookies de terceiros via AJAX. O bearer da plataforma não entra em URL ou armazenamento do navegador. O callback só comunica sucesso/erro ao domínio frontend configurado; a aba valida origem **e janela remetente**. Se navegador/GitHub isolarem `window.opener`, feche a janela e use **Atualizar estado**. Permita pop-ups para esse site. Não atualize a aba original durante o fluxo: a sessão da plataforma continua sendo apenas em memória.

`state` possui 32 bytes criptograficamente aleatórios, validade de dez minutos, hash SHA-256 no banco, vínculo com `ApplicationUserId` e consumo atômico de uso único. O cookie contém outro valor aleatório, é HttpOnly, SameSite=Lax e Secure em HTTPS; o hash correspondente é validado em tempo constante. O verificador PKCE S256 fica protegido no banco. Uma nova tentativa invalida a anterior da mesma conta. `userId` ou username fornecidos pelo navegador nunca determinam a associação.

## Permissões mínimas

A autorização envia **`scope=` vazio**. Não pede `repo`, `public_repo`, `user:email`, escrita, repositórios privados ou `offline_access`. A identidade pública vem de `/user` autenticado, e as análises continuam lendo apenas repositórios públicos. PKCE S256 é usado junto com Client Secret.

Se o GitHub devolver permissões adicionais de uma autorização antiga, a conexão é recusada. Revogue a autorização antiga em **GitHub → Settings → Applications → Authorized OAuth Apps → seu app → Revoke**, depois conecte novamente. Não ative Device Flow nem expiração com refresh/offline para este MVP. Se o token retornado incluir expiração, ela é respeitada e a interface pede reconexão; não há renovação automática.

## Modelo e proteção persistente

A migration `20260919170906_AddGitHubOAuthConnections` acrescenta no schema privado `portfolio`:

- `GitHubConnections`: Id, ApplicationUserId, GitHubUserId, GitHubUsername, AvatarUrl, AccessTokenEncrypted, ConnectedAt, UpdatedAt, ExpiresAt e InvalidatedAt. Índices únicos em ApplicationUserId e GitHubUserId: uma conexão por conta e o mesmo GitHub não pode pertencer a duas contas da plataforma.
- `GitHubOAuthAttempts`: tentativa temporária com usuário, hashes de state/correlação, verificador PKCE protegido e validade. Tentativas vencidas são limpas ao iniciar novas conexões.
- `DataProtectionKeys`: keyring oficial do ASP.NET Core Data Protection.

Tokens e verificadores são protegidos por Data Protection com propósito separado por usuário e por tipo de segredo. **As chaves do keyring são persistidas no PostgreSQL e criptografadas com certificado RSA privado**, usando `PersistKeysToDbContext` + `ProtectKeysWithCertificate`. O PFX e sua senha ficam apenas nos User Secrets/Environment do backend, fora do banco e da imagem Docker. O schema `portfolio` não deve ser exposto pela Data API do Supabase.

A aplicação usa o nome estável `GuiaDoGit` para Data Protection. O mesmo keyring protege os bearer tokens oficiais. Um restart do Render carrega as chaves do PostgreSQL com o mesmo certificado, mantendo tokens GitHub utilizáveis. Na primeira ativação, sessões emitidas pelo keyring efêmero anterior podem precisar de novo login. A expiração de 30 minutos e o armazenamento da sessão somente na memória da aba continuam iguais.

**Guarde backup privado do PFX/senha e do banco com o keyring.** Quem tiver acesso a ambos pode descriptografar os tokens. Não regenere o certificado a cada deploy nem remova chaves antigas. Ambientes que compartilham o mesmo banco/keyring precisam usar **o mesmo certificado**; não configure outro PFX no desenvolvimento se ele aponta ao mesmo Supabase. Rotação do certificado exige planejar leitura das chaves antigas e não foi automatizada. Perder o certificado/keyring exige novas autorizações dos usuários; trocar apenas as variáveis por outro certificado não recupera tokens existentes.

## Gerar a proteção uma única vez, manualmente

Na raiz do projeto, com PowerShell 7:

```powershell
pwsh -File scripts/New-DataProtectionCertificate.ps1
```

O script usa APIs oficiais .NET para gerar RSA 3072/PFX e senha aleatória. Salva em `%LOCALAPPDATA%\GuiaDoGit\dataprotection-secrets.json`, fora do repositório, não imprime conteúdo e recusa sobrescrever arquivo existente. Não altera User Secrets, banco ou Render. Trate o arquivo inteiro como segredo, restrinja o acesso ao seu usuário e guarde backup privado. Não o envie ao chat nem o abra em apresentação/compartilhamento de tela.

Para importar somente essas duas chaves nos User Secrets locais, preservando as demais:

```powershell
Get-Content -Raw "$env:LOCALAPPDATA\GuiaDoGit\dataprotection-secrets.json" | dotnet user-secrets set --project backend/Portfolio.Api
```

User Secrets é armazenamento local fora do repositório, não cofre criptografado. Nunca use `user-secrets list` em tela compartilhada. O script acima **não foi executado pelo agente**; nenhum certificado real foi criado nesta implementação.

## Revisar e aplicar a migration manualmente

Apenas novas tabelas/índices/FKs são criados pelo `Up`. Nenhuma tabela existente é recriada, nenhum snapshot é removido, não há migration automática na inicialização. **Não apliquei esta migration ao Supabase real.** Os testes a aplicam somente em PostgreSQL descartável no Docker.

Na raiz, primeiro gere o SQL sem carregar User Secrets nem abrir conexão:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:GitHub__OAuth__Enabled = 'false'
dotnet tool restore
New-Item -ItemType Directory -Force .artifacts | Out-Null
dotnet ef migrations script 20260918122632_InitialPostgreSql 20260919170906_AddGitHubOAuthConnections --idempotent --project backend/Portfolio.Api --output .artifacts/github-oauth-migration.sql
```

Leia a migration C# e `.artifacts/github-oauth-migration.sql`. Faça backup adequado antes de alterar o banco. Quando decidir aplicar ao Supabase já configurado nos seus User Secrets, **você** executa:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
# Mantém OAuth desabilitado durante a operação; não modifica User Secrets.
$env:GitHub__OAuth__Enabled = 'false'
dotnet ef database update 20260919170906_AddGitHubOAuthConnections --project backend/Portfolio.Api
Remove-Item Env:GitHub__OAuth__Enabled
Remove-Item Env:DOTNET_ENVIRONMENT
Remove-Item Env:ASPNETCORE_ENVIRONMENT
```

Execute o comando de atualização enquanto esta é a migration mais recente. Ele usa sua conexão existente; confirme o ambiente de destino privadamente. Não use `database drop`, `EnsureDeleted`, `database update 0` nem rollback desta migration com tokens em uso (o Down removeria essas três tabelas). Não configure update no startup/build Render.

## GitHub Developer Settings: produção

1. Entre na sua conta GitHub → **Settings → Developer settings → OAuth Apps → seu OAuth App existente**.
2. Em **Homepage URL**, configure `https://guiadogit.vercel.app`.
3. Em **Authorization callback URL**, substitua o callback antigo por **`https://guiadogit-api.onrender.com/api/github/callback`** e salve em **Update application**.
4. Mantenha Device Flow desabilitado. Não solicite scopes extras.
5. Reutilize o Client ID e Client Secret já configurados no Render. Não é necessário regenerar um secret válido. Se não possui mais o secret, gere outro e atualize somente o backend.

O callback deve corresponder ao `GitHub__OAuth__CallbackUrl`. Um OAuth App possui uma configuração de callback, portanto prefira **outro OAuth App para desenvolvimento local**, com seu par nos User Secrets. Isso não muda o OAuth App de produção nem exige nova conta GitHub. Alternativamente, altere temporariamente o callback do app existente para local, sabendo que a conexão em produção ficará indisponível nesse período. As consultas públicas por credenciais da aplicação não usam callback.

## Variáveis do Render

| Variável | Necessidade | Valor/exemplo | Segredo? |
| --- | --- | --- | --- |
| `GitHub__OAuth__Enabled` | Nova, habilita o recurso após migration/configuração | `true` | Não |
| `GitHub__OAuth__CallbackUrl` | Nova, quando habilitado | `https://guiadogit-api.onrender.com/api/github/callback` | Não |
| `DataProtection__CertificateBase64` | Nova, quando habilitado | conteúdo de `DataProtection:CertificateBase64` do arquivo privado; exemplo `PFX_BASE64_FICTICIO` | **Sim, contém chave privada** |
| `DataProtection__CertificatePassword` | Nova, quando habilitado | conteúdo de `DataProtection:CertificatePassword`; exemplo `SENHA_FICTICIA` | **Sim** |
| `GitHub__ClientId` | Existente, OAuth App | `CLIENT_ID_FICTICIO` | Identificador público, backend |
| `GitHub__ClientSecret` | Existente, OAuth App | `CLIENT_SECRET_FICTICIO` | **Sim** |
| `Frontend__BaseUrl` | Existente, agora também destino fixo de postMessage | `https://guiadogit.vercel.app` | Não |
| `ConnectionStrings__Portfolio` | Existente, mesmo Supabase | connection string Npgsql já configurada | **Sim** |
| `ASPNETCORE_ENVIRONMENT` | Existente | `Production` | Não |

Abra privadamente o arquivo gerado para transferir os dois valores ao **Render → seu serviço → Environment**; não cole o JSON inteiro no campo de uma variável. Salve e faça deploy manual após revisar/aplicar a migration. Não há variável secreta nova no Vercel: continua apenas `VITE_API_BASE_URL=https://guiadogit-api.onrender.com`.

## Desenvolvimento local

Depois da migration e da importação do mesmo certificado do keyring, configure o OAuth App local com Homepage `http://localhost:5173` e callback **`http://localhost:5080/api/github/callback`**. Não misture `localhost` e `127.0.0.1`, porque o cookie precisa voltar ao mesmo host. Se sua política GitHub exigir HTTPS, use um callback HTTPS de desenvolvimento correspondente e ajuste a URL local da API; o projeto continua aceitando HTTP de loopback somente em Development.

```powershell
dotnet user-secrets set "GitHub:ClientId" "CLIENT_ID_LOCAL_FICTICIO" --project backend/Portfolio.Api
dotnet user-secrets set "GitHub:ClientSecret" "CLIENT_SECRET_LOCAL_FICTICIO" --project backend/Portfolio.Api
dotnet user-secrets set "GitHub:OAuth:CallbackUrl" "http://localhost:5080/api/github/callback" --project backend/Portfolio.Api
dotnet user-secrets set "GitHub:OAuth:Enabled" "true" --project backend/Portfolio.Api
```

Substitua os exemplos apenas localmente. Remova `GitHub:Token`/`GitHub__Token` se houver configuração legada; não misture com o par OAuth. Se `Frontend:BaseUrl` já estiver configurada para produção nos User Secrets, ajuste para `http://localhost:5173` no ambiente local. Sem essa chave, Development usa localhost:5173. Variáveis de ambiente sobrescrevem User Secrets.

Inicie backend pelo Visual Studio 2022, perfil **http** (5080), e frontend pela task VS Code **Frontend: iniciar Vite** (5173). Entre normalmente, abra **GitHub → Conectar GitHub**, autorize na janela e volte. A conta conectada deve corresponder à identidade autenticada no GitHub, independentemente do username analisado.

## Verificação manual local/deploy

1. Antes de habilitar, visitante, cadastro/login e análise continuam funcionando. Após habilitar, `/health` e `/api/database/status` devem responder 200.
2. Entre na conta da plataforma, abra `/github`, conecte e confira avatar/@username. Nenhum token GitHub aparece em respostas da API ou armazenamento do React.
3. Consulte um perfil já fresco: fonte cache, zero chamadas. Conectar não dispara análise.
4. Para verificar coleta autenticada, faça **uma** atualização explícita de `leomacedo2`; não repita para contornar rate limit. Em `/github`, **Atualizar estado** mostra os headers observados daquele usuário.
5. Reinicie manualmente o backend (sem trocar certificado nem keyring), entre novamente se a aba foi recarregada e confirme conexão utilizável. O restart pode limpar apenas as observações de quota em memória.
6. Desconecte. Confirme que conta, análises salvas e cache permanecem. Uma nova coleta usa as credenciais da aplicação.
7. Confira que uma segunda conta da plataforma não consegue associar o mesmo GitHub e que duas contas com GitHubs distintos funcionam independentemente.
8. Teste cancelar autorização e abrir callback sem parâmetros: erro amigável, sem associação. Confira celular/pop-up, tema claro/escuro e rota `/github` aberta diretamente.

## Rate limit, fallback e desconexão

O estado da aplicação continua separado. Para usuários, há estado por GitHubUserId público, com headers reais `x-ratelimit-limit`, `remaining` e `reset`. O `/user` de conexão também registra headers; suas duas chamadas de autorização/identidade não entram na contagem da análise. Quotas ficam somente em memória, limitadas a 10.000 entradas e expiração por inatividade de duas horas, e podem ser compartilhadas com outros apps/tokens GitHub daquele usuário. A interface não promete requests exclusivos. `/api/github/rate-limit` continua diagnóstico **da aplicação** somente em Development; `/api/github/connection` retorna a quota observada do usuário autenticado, sem chamada externa.

Cota esgotada bloqueia novas chamadas daquele contexto até reset/cooldown, sem retry e sem trocar de credencial para contornar quota. Resultados parciais/fallback para snapshots anteriores continuam disponíveis. O controle é por processo, adequado à instância única; não coordena outras aplicações ou múltiplas instâncias.

Conexão expirada/inválida/indecifrável usa autenticação da aplicação. Se GitHub responder 401 a um token até então válido, ele é marcado para reconexão; a chamada não é repetida automaticamente. Uma próxima consulta pode usar a aplicação. Não há refresh token automático.

**Desconectar revoga apenas a associação local.** Não executa revogação remota: uma autorização/token do mesmo OAuth App pode estar sendo utilizada em outro contexto e não deve ser invalidada inesperadamente. Para revogar no GitHub, use Settings → Applications → Authorized OAuth Apps. Uma chamada já em andamento pode terminar usando o token que havia carregado. Contas/análises não são apagadas.

## Limitações e validação

Validação automatizada: **105 testes aprovados (84 anteriores + 21 novos casos)**, build .NET 8 Release sem avisos/erros, build Vite e imagem Docker de produção aprovados. A migration foi aplicada nos bancos descartáveis dos testes; o SQL aditivo foi gerado para revisão e `has-pending-model-changes` não encontrou divergências. O script de certificado teve sua sintaxe verificada sem ser executado.

- Suporte de popup/cookie de correlação depende do navegador. Há atualização manual de estado se a comunicação entre janelas for bloqueada.
- OAuth requer banco e keyring disponíveis. A feature permanece desabilitada até ser configurada; certificados inválidos impedem inicialização com mensagem sem valores secretos.
- Os limites de login/OAuth podem agrupar usuários pelo proxy Render; não houve relaxamento de confiança em proxies.
- O código da autorização chega na query do callback por exigência do protocolo; é curto, de uso único, atrelado a PKCE e removido do histórico da janela após processamento. Tokens nunca são colocados em query. Não habilite logs de request bodies/headers/query strings nem capture a troca OAuth em apresentações; o proxy da hospedagem pode ter seus próprios logs de acesso.
- Os testes usam GitHub simulado e PostgreSQL descartável. Nenhuma chamada GitHub real, acesso a User Secrets, migration no Supabase ou deploy foi executado nesta implementação. Autorização real deve ser validada manualmente depois da configuração.

Referências oficiais: [Authorization Code Flow, state, PKCE e identidade](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps), [scopes OAuth](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/scopes-for-oauth-apps), [Data Protection: persistência de chaves](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-8.0), [proteção do keyring por certificado](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-8.0), [rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api).

## Arquivos da implementação OAuth

Criados:

- `backend/Portfolio.Api/Controllers/GitHubConnectionController.cs`
- `backend/Portfolio.Api/Data/GitHubConnection.cs`
- `backend/Portfolio.Api/OAuth/GitHubOAuthConfiguration.cs`
- `backend/Portfolio.Api/OAuth/GitHubOAuthClient.cs`
- `backend/Portfolio.Api/OAuth/GitHubTokenProtection.cs`
- `backend/Portfolio.Api/OAuth/GitHubRequestContext.cs`
- `backend/Portfolio.Api/Data/Migrations/20260919170906_AddGitHubOAuthConnections.cs` e `.Designer.cs`
- `backend/Portfolio.Api.Tests/GitHubConnectionTests.cs`
- `backend/Portfolio.Api.Tests/GitHubOAuthConfigurationTests.cs`
- `frontend/src/pages/GitHubConnectionPage.jsx`
- `scripts/New-DataProtectionCertificate.ps1`

Modificados: `.gitignore`, `README.md`, `docs/deploy.md`, este guia, `backend/Portfolio.Api/Program.cs`, `Portfolio.Api.csproj`, `appsettings.json`, `Data/PortfolioDbContext.cs`, `Data/Migrations/PortfolioDbContextModelSnapshot.cs`, `Services/GitHubService.cs`, `Services/GitHubRateLimitHandler.cs`, `frontend/src/App.jsx` e `frontend/src/services/api.js`.

`ApplicationUser`, `PersistentAnalysisService`, detector/recomendações, orçamento, portas e Dockerfile foram preservados; o relacionamento novo é configurado no DbContext. O SQL revisável também está em `.artifacts/github-oauth-migration.sql` (gerado, ignorado pelo Git).
