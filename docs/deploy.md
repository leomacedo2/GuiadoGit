# Deploy manual gratuito: Vercel → Render → Supabase

Este roteiro usa o repositório atual, sem trocar stack e sem criar outro banco. O Supabase existente já possui a migration inicial. A conexão opcional de usuário GitHub acrescenta uma migration **manual**, descrita abaixo. Nenhum deploy, conexão de conta ou alteração no banco real foi feito automaticamente.

## Custos e limites

Escolha **Vercel Hobby**, **Render Free Web Service** e mantenha **Supabase Free**. Use os domínios gratuitos `.vercel.app` e `.onrender.com`. Não adicione discos, banco Render, serviços extras ou trials pagos. Vercel Hobby é destinado a uso pessoal não comercial: [condições do plano](https://vercel.com/docs/plans/hobby).

Render Free dorme após 15 minutos sem tráfego; acordar pode levar cerca de um minuto. Há limites mensais de horas, tráfego e builds. Excedê-los pode suspender o serviço; com forma de pagamento cadastrada, excedentes de tráfego/build podem gerar cobrança. Para custo zero, não habilite excedentes pagos, não cadastre pagamento para esse fim e confira Billing/limites. Tráfego externo muito alto também pode causar suspensão. Não há garantia de disponibilidade ilimitada gratuita. [Limitações oficiais](https://render.com/docs/free).

O frontend aguarda até 330 segundos em produção, informa que o servidor pode estar iniciando e não repete automaticamente cadastro, refresh ou outras operações. Localmente mantém 245 segundos. Não há ping periódico ou mecanismo para impedir sleep. Se o proxy responder antes com erro, aguarde e tente manualmente; não há garantia de espera por todo esse prazo na infraestrutura externa.

## Antes de começar

1. Publique os arquivos do projeto em um repositório GitHub que você controla. Preserve `.gitignore`; confira que não incluiu `.env`, User Secrets, connection strings ou credenciais. Não copie secrets para arquivos do repositório.
2. O repositório precisa conter `backend/Portfolio.Api`, `frontend`, `.dockerignore` e os arquivos de deploy desta etapa.
3. Tenha as credenciais do **Supabase existente** e do **OAuth App GitHub já utilizado** disponíveis somente no seu gerenciador seguro. Não envie valores ao chat.
4. Selecione uma branch estável. O projeto continua .NET/EF Core 8. O Docker usa SDK/runtime 8; Node 22.x ou 24.x atende ao frontend.

## Parte A — Backend no Render

1. Entre/crie uma conta no [Render](https://dashboard.render.com/).
2. Selecione **New → Web Service**. Conecte seu GitHub e autorize apenas o repositório necessário; selecione o repositório e a branch.
3. Escolha um nome para o backend e uma região, preferencialmente próxima do Supabase.
4. Selecione **Language/Runtime: Docker**.
5. Configure os caminhos exatamente assim:

   | Campo | Valor |
   | --- | --- |
   | Root Directory | deixe vazio: raiz do monorepo |
   | Dockerfile Path | `backend/Portfolio.Api/Dockerfile` |
   | Docker Build Context Directory, se exibido | `.` |
   | Instance Type | **Free** |
   | Docker Command / Start Command | deixe vazio; usa ENTRYPOINT |
   | Pre-deploy Command | deixe vazio |
   | Health Check Path | `/health` |

6. Não informe `dotnet run` nem build command separado. O Dockerfile restaura e publica em Release com SDK 8, depois executa `dotnet Portfolio.Api.dll` no runtime ASP.NET 8 como usuário `app`, sem privilégios de root. O contexto inclui somente o backend, excluindo arquivos locais/gerados/secrets conhecidos. Nunca acrescente credenciais como `ARG` ou `ENV` no Dockerfile.
7. Em **Environment**, adicione as variáveis da tabela abaixo. Inicialmente pode omitir `Frontend__BaseUrl`, pois a URL Vercel ainda não existe. Sem ela, o backend funciona, mas não libera CORS para navegadores externos.
8. Clique em **Deploy Web Service**. Acompanhe Events/Logs até o serviço ficar disponível. Verifique que o plano continua **Free**.
9. Copie a URL HTTPS exibida, por exemplo `https://portfolio-example.onrender.com`.
10. Abra `https://portfolio-example.onrender.com/health`: deve retornar HTTP 200 e `{"status":"ok"}`. Essa rota não acessa GitHub nem banco.
11. Abra `https://portfolio-example.onrender.com/api/database/status`: deve retornar HTTP 200 e `{"connected":true,"migrationsApplied":true}`. Em falha retorna 503 com os dois indicadores; não revela configuração. O diagnóstico tem limite global de 10 consultas/minuto e prazo de 10 segundos; HTTP 429 significa aguardar. Não o use como health check nem monitore em loop.

Render fornece `PORT` (normalmente 10000). Fora de Development, o backend usa `http://0.0.0.0:PORT`; aceita portas não privilegiadas entre 1024 e 65535. Sem PORT, o container usa 8080. `EXPOSE 8080` é metadado, não fixa a porta Render. Em Development, PORT é ignorada e o perfil Visual Studio continua em localhost:5080.

Referências: [Docker no Render](https://render.com/docs/docker), [Web Services e portas](https://render.com/docs/web-services), [health checks](https://render.com/docs/health-checks).

### Variáveis do backend Render

Todos os exemplos são fictícios. Insira valores reais somente no painel Environment do Render.

| Nome | Obrigatória? | Exemplo fictício | Onde obter | É segredo? |
| --- | --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Sim, já padrão da imagem | `Production` | valor fixo | Não |
| `ConnectionStrings__Portfolio` | Sim para contas/cache | `Host=HOST_DO_POOLER;Port=5432;Database=postgres;Username=postgres.REFERENCIA;Password="SENHA_FICTICIA";SSL Mode=VerifyFull;Maximum Pool Size=10;Timeout=15;Command Timeout=30` | Supabase existente → Connect; senha do banco | **Sim** |
| `GitHub__ClientId` | Sim no modo OAuth App atual | `ID_FICTICIO` | GitHub Settings → Developer settings → OAuth Apps → seu app | Identificador público, manter no backend |
| `GitHub__ClientSecret` | Sim junto com ClientId | `SEGREDO_FICTICIO` | mesmo OAuth App; segredo guardado por você | **Sim** |
| `GitHub__OAuth__Enabled` | Opcional, padrão false; true após preparar OAuth de usuário | `true` | habilitação manual | Não |
| `GitHub__OAuth__CallbackUrl` | Sim quando OAuth de usuário habilitado | `https://guiadogit-api.onrender.com/api/github/callback` | callback do backend | Não |
| `DataProtection__CertificateBase64` | Sim quando OAuth de usuário habilitado | `PFX_BASE64_FICTICIO` | arquivo privado gerado conforme guia OAuth | **Sim, chave privada** |
| `DataProtection__CertificatePassword` | Sim para o PFX gerado | `SENHA_FICTICIA` | mesmo arquivo privado | **Sim** |
| `Frontend__BaseUrl` | Sim para integrar Vercel; pode omitir no primeiro deploy | `https://portfolio-example.vercel.app` | domínio de produção do frontend Vercel | Não |
| `GitHubAnalysisCacheHours` | Opcional, padrão 6 | `6` | configuração do projeto | Não |
| `GitHub__UserAgent` | Opcional, padrão existente | `PortfolioCourseProject/1.0` | configuração do projeto | Não |
| `GitHub__ApiVersion` | Opcional, padrão existente | `2022-11-28` | configuração do projeto | Não |
| `Analysis__Individual__MaxRepositories` | Opcional, não aumentar nesta etapa | `200` | configuração atual | Não |
| `Analysis__Individual__MaxRequests` | Opcional, não aumentar nesta etapa | `600` | configuração atual | Não |
| `Analysis__Individual__TimeoutSeconds` | Opcional, padrão existente | `180` | configuração atual | Não |
| `PORT` | Fornecida pelo Render; não precisa adicionar | `10000` | ambiente Render | Não |
| `GitHub__Token` | Não usar com ClientId/ClientSecret | `TOKEN_FICTICIO` | alternativa já suportada; não necessária neste roteiro | **Sim** |

O ASP.NET converte `__` para `:` automaticamente. `ConnectionStrings__Portfolio` é lida pelo mesmo `GetConnectionString("Portfolio")` usado localmente. Não há mudança nos User Secrets.

### Supabase existente

Use a conexão Npgsql que já funciona, com host acessível do Render. Para IPv4, prefira **Connect → Session pooler**, porta 5432, copiando host e usuário exatos. Não suponha que o endereço direto IPv6 será acessível. Não use URI `postgresql://...`: use os pares `Host=...;Database=...;...` acima, com senha literal (não URL-encoded). Não crie outro projeto/banco.

Mantenha SSL com validação (`VerifyFull`). Se a configuração local usa um caminho Windows para CA, ele não existe no Linux do container: use a cadeia confiável do runtime ou disponibilize a CA pública como arquivo de configuração no ambiente e ajuste o caminho; não desative a validação para contornar erro. Confira restrições de rede do projeto se estiverem ativadas. Nenhuma chave Supabase anon/service_role é necessária. [Conexões Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres).

## Parte B — Frontend no Vercel

1. Entre no [Vercel](https://vercel.com/) e selecione o plano pessoal **Hobby**, sem Pro/trial.
2. **Add New → Project → Import Git Repository**. Conecte GitHub, se necessário, e importe o mesmo repositório.
3. Em **Root Directory**, selecione **frontend**.
4. **Framework Preset: Vite**. Se precisar escolher Node, use **22.x** ou **24.x**.
5. Em Environment Variables, adicione **`VITE_API_BASE_URL`** com a URL HTTPS Render, sem barra final e sem `/api`, por exemplo `https://portfolio-example.onrender.com`. Selecione Production. Para Preview, veja a limitação CORS abaixo.
6. Confira **Install Command: npm ci**, **Build Command: npm run build**, **Output Directory: dist**. Essas opções também estão em `frontend/vercel.json`.
7. Clique em **Deploy** e espere o build terminar.
8. Copie o domínio de produção estável, por exemplo `https://portfolio-example.vercel.app`.

`VITE_API_BASE_URL` é pública e incorporada ao JavaScript durante o build. Alterá-la exige **redeploy do frontend**. Nunca cadastre connection string, Client Secret ou token GitHub em variáveis `VITE_*`. Axios continua centralizado em `frontend/src/services/api.js`, enviando requisições diretamente ao Render, sem proxy Vercel para a API. Sem variável, o fallback continua localhost:5080 para uso local; portanto não omita a variável no deploy.

`frontend/vercel.json` reescreve navegações SPA para `/index.html`. Acesso direto/reload de `/login`, `/cadastro`, `/analisar`, `/perfil/{username}`, `/skills`, `/recomendacoes`, `/repositorios`, `/perfil` e `/minhas-analises` carrega o React em vez de 404. A rota pessoal pede login após recarregar, conforme a sessão em memória já existente. As páginas de resultado usam `?perfil=username` para recuperar o contexto após reload. [Vite e SPA no Vercel](https://vercel.com/docs/frameworks/frontend/vite).

## Parte C — CORS e HTTPS

1. Volte ao serviço Render → **Environment**.
2. Adicione/atualize **`Frontend__BaseUrl`** com a origem Vercel estável: `https://portfolio-example.vercel.app`.
3. Use **Save, rebuild and deploy / Save and deploy**, conforme o painel, para reiniciar o backend com o novo ambiente.
4. Espere `/health` responder. Abra o Vercel e faça uma consulta.

Em Development continua `Cors:AllowedOrigins`, incluindo localhost:5173. Em Production somente `Frontend__BaseUrl` é permitida; localhost não é herdado. A URL deve ser HTTPS sem caminho, query, credenciais ou wildcard. Uma barra final é normalizada. Domínios temporários de Preview não são automaticamente liberados; teste pelo domínio estável. Se trocar para um Preview para testes, atualize a origem exata e depois restaure a de produção.

O preflight permite GET/POST/PUT/DELETE e headers, incluindo Authorization. Não há `AllowAnyOrigin`, cookies de autenticação da plataforma ou `AllowCredentials`. O bearer oficial continua em memória, expira em 30 minutos e acompanha endpoints pessoais e consultas de análise/GitHub quando há sessão. Respostas 401 encerram a sessão. Logout local e proteção `[Authorize]` permanecem. O OAuth usa somente um cookie temporário de correlação HttpOnly/Secure/SameSite=Lax, estabelecido pela navegação da janela de conexão, sem alterar CORS. Render termina TLS na borda e encaminha HTTP ao container; não foi adicionado redirecionamento HTTPS interno que causaria loops. Use sempre as URLs públicas HTTPS.

Não foi habilitada confiança irrestrita em `X-Forwarded-For`. O limitador de login pode agrupar visitantes pelo IP do proxy Render (30 requisições/minuto); a trava por conta do Identity continua ativa. Se houver 429 em uma apresentação com várias pessoas, espere um minuto. Configurar proxies confiáveis e limites para tráfego maior fica fora deste MVP.

## Migrations: manuais

A migration inicial já está aplicada no Supabase. A nova `20260919170906_AddGitHubOAuthConnections` cria somente `portfolio.GitHubConnections`, `GitHubOAuthAttempts` e `DataProtectionKeys`, índices e relacionamentos. Ela não foi aplicada ao banco real. Revise e aplique manualmente **antes de habilitar OAuth**. O startup apenas consulta disponibilidade e migrations pendentes, sem `Migrate`, `EnsureCreated`, reset ou exclusão.

Na raiz, gere o SQL sem carregar User Secrets ou conectar ao banco:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:GitHub__OAuth__Enabled = 'false'
dotnet tool restore
New-Item -ItemType Directory -Force .artifacts | Out-Null
dotnet ef migrations script 20260918122632_InitialPostgreSql 20260919170906_AddGitHubOAuthConnections --idempotent --project backend/Portfolio.Api --output .artifacts/github-oauth-migration.sql
```

Após revisar o SQL e preparar backup, aplique usando sua conexão existente nos User Secrets:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:GitHub__OAuth__Enabled = 'false'
dotnet ef database update 20260919170906_AddGitHubOAuthConnections --project backend/Portfolio.Api
Remove-Item Env:GitHub__OAuth__Enabled
Remove-Item Env:DOTNET_ENVIRONMENT
Remove-Item Env:ASPNETCORE_ENVIRONMENT
```

Execute esse update enquanto esta for a migration mais recente. Não use `database drop`, `database update 0`, `EnsureDeleted`, downgrade nem alteração manual do histórico. Não configure migrations no build/start do Render, nem dependa de shell/jobs pagos. O roteiro detalhado está no [guia OAuth](github-autenticacao.md).

## Habilitar conexão GitHub no deploy existente

1. Revise/aplique a migration acima manualmente. É aditiva e compatível com o backend anterior.
2. Gere **uma única vez** o certificado privado seguindo [geração e backup](github-autenticacao.md#gerar-a-proteção-uma-única-vez-manualmente). Não copie secrets para o repositório.
3. Em GitHub → Settings → Developer settings → OAuth Apps → app existente: Homepage `https://guiadogit.vercel.app`; Authorization callback URL **`https://guiadogit-api.onrender.com/api/github/callback`**. Salve. Device Flow fica desabilitado; nenhum scope adicional é solicitado.
4. No Render → serviço `guiadogit-api` → Environment, mantenha ClientId/ClientSecret/conexão Supabase. Adicione as quatro variáveis novas da tabela e confirme `Frontend__BaseUrl=https://guiadogit.vercel.app`.
5. Publique o código e faça deploy manual do backend com as variáveis. Não execute migrations pelo Render. Confirme `/health` e `/api/database/status`.
6. No Vercel, publique o frontend atualizado, preservando `VITE_API_BASE_URL=https://guiadogit-api.onrender.com`. Não adicione secret. O fallback SPA existente também cobre `/github`.
7. Entre novamente na plataforma se necessário; a primeira ativação muda o keyring anterior. Abra **GitHub → Conectar GitHub**, autorize na janela e confira estado. Se necessário, use **Atualizar estado** ao retornar.
8. Teste cache, uma atualização explícita, desconexão e persistência após restart conforme [checklist OAuth](github-autenticacao.md#verificação-manual-localdeploy). Não faça pings para evitar sleep.

Para publicar o código antes de preparar a configuração, mantenha `GitHub__OAuth__Enabled=false`; login/visitantes continuam usando o comportamento anterior. Mesmo assim, aplique a migration antes de exigir `migrationsApplied=true` no diagnóstico desta versão.

## Cache, logs e sessões

Seis horas definem apenas o prazo de frescor. **Nada é apagado por expiração**: snapshots antigos continuam reconstruíveis no PostgreSQL. Abrir pela lista pessoal não chama GitHub. Force refresh e orçamento permanecem iguais; o segredo GitHub existe apenas no backend.

Os logs registram ambiente/inicialização, banco disponível/indisponível, quantidade de migrations pendentes, status de falhas GitHub e tipos de falhas de integração. Os logs próprios não registram bodies, headers, parâmetros, URL de conexão ou mensagens brutas de exceção. Em Production, logs brutos do EF/Npgsql/HttpClient e do middleware de exceções são desativados; falhas HTTP 5xx geram status e TraceId. Não habilite SensitiveDataLogging, HTTP body logging ou dumps de ambiente para diagnosticar secrets.

O filesystem gratuito Render é efêmero. Com OAuth habilitado, Data Protection persiste o keyring no PostgreSQL **criptografado pelo certificado privado do Environment**, protegendo também tokens GitHub. Preserve certificado/senha e keyring entre deploys; mantenha backup privado de ambos. Ambientes que usam o mesmo banco/keyring precisam do mesmo certificado. Com o recurso desabilitado, continua o comportamento anterior de chaves locais efêmeras. O bearer da plataforma ainda dura 30 minutos e permanece somente na memória da aba, portanto recarregar a página exige login independentemente da persistência das chaves. Não há disco pago nem JWT próprio.

## Parte D — Smoke test após deploy

- [ ] Home abre pelo domínio Vercel HTTPS.
- [ ] Backend responde HTTP 200 em `/health`.
- [ ] Banco responde connected=true e migrationsApplied=true em `/api/database/status`.
- [ ] Visitante analisa `leomacedo2`.
- [ ] GitHub responde numa coleta nova; se houver cache, uma atualização explícita pode verificar a integração (não repetir desnecessariamente).
- [ ] Segunda análise utiliza cache, mesma data/id e zero chamadas GitHub atuais.
- [ ] Cadastro funciona (use uma conta de teste sua).
- [ ] Login funciona; senha errada é recusada.
- [ ] Salvar perfil funciona.
- [ ] Minhas análises lista e abre o perfil sem nova coleta; remover apaga só o vínculo.
- [ ] Logout funciona e a área pessoal pede login.
- [ ] Refresh direto de todas as rotas React não dá 404.
- [ ] Celular abre a aplicação.
- [ ] Layout básico e tema claro/escuro continuam funcionais.
- [ ] Conta autenticada conecta GitHub; o estado mostra o usuário autorizado, sem tokens.
- [ ] Uma análise fresca continua vindo do cache após conectar.
- [ ] Uma atualização explícita utiliza a conexão; a quota exibida vem dos headers observados.
- [ ] Desconectar preserva conta, análises salvas e cache.
- [ ] Reiniciar o backend não inutiliza a conexão GitHub; certificado e keyring permanecem iguais.

Se aparecer CORS: confira a origem exata no Render e aguarde o redeploy. Se o navegador tentar localhost: confira `VITE_API_BASE_URL` e refaça o build Vercel. Se banco der 503: confira string Npgsql, SSL, pooler/IPv4 e projeto Supabase ativo. Se login der 401 após restart: entre novamente. Se GitHub retornar 429: aguarde o reset informado, use análises salvas e não force refresh repetidamente.

## Execução e validação local

Visual Studio 2022: `Portfolio.Api` como inicialização, perfil **http**, F5/Ctrl+F5 → localhost:5080. VS Code: task **Frontend: iniciar Vite** → localhost:5173. User Secrets, launchSettings e vite.config foram preservados. O frontend local não precisa de uma URL Render.

```powershell
dotnet test Portfolio.sln
dotnet build Portfolio.sln
npm --prefix frontend ci
npm --prefix frontend run build
docker build -f backend/Portfolio.Api/Dockerfile -t portfolio-deploy-check .
```

Os testes completos exigem Docker Desktop (PostgreSQL descartável), sem usar Supabase ou GitHub reais. A etapa de deploy tinha **84 testes**; a etapa OAuth preserva esses testes e acrescenta validações de conexão, segurança, cache e persistência das chaves. Durante a implementação OAuth não houve acesso a User Secrets, consultas GitHub reais, migration no Supabase ou deploy. O fluxo real de autorização e as políticas de janela/cookie do navegador devem ser confirmados por você após a configuração.

## Arquivos desta etapa

Criados: `.dockerignore`, `backend/Portfolio.Api/Dockerfile`, `backend/Portfolio.Api/appsettings.Production.json`, `backend/Portfolio.Api/Deployment/DeploymentConfiguration.cs`, `DatabaseDiagnostics.cs`, `GitHubDiagnosticsHandler.cs`, `backend/Portfolio.Api.Tests/DeploymentTests.cs`, `frontend/vercel.json`, `docs/deploy.md`.

Modificados: `backend/Portfolio.Api/Program.cs`, `backend/Portfolio.Api.Tests/PersistenceTests.cs`, `frontend/src/services/api.js`, `frontend/src/pages/AuthPage.jsx`, `frontend/src/App.jsx`, `README.md`, `docs/persistencia.md` (referência ao diagnóstico agora disponível em produção).

## Atualização de cobertura, páginas e trilhas

A rodada de [refinamento](refinamento.md) não exige migration, novo secret, callback OAuth ou variável obrigatória. Dockerfile, portas, CORS, conexão PostgreSQL e configuração Vercel foram preservados. Com auto-deploy habilitado na branch atual, commit/push aciona os builds normais de ambos os serviços; aguarde os dois terminarem antes do smoke test.

Configuração opcional somente no backend: `Analysis__Individual__MaxManifestsPerRepository=16` (padrão; permitido 1–32). O orçamento permanece em 600 chamadas adicionais. Não é preciso editar variáveis para adotar o padrão. As novas dependências são restauradas pelos builds normais; Playwright/Chromium são usados somente em testes locais, não no serviço publicado.

Após deploy, confira `/` como entrada, login → `/minhas-analises`, visitante → `/analisar` → `/perfil/{username}`, gráficos nos dois temas e refresh de rotas. Snapshots antigos continuam válidos e podem mostrar avisos antigos; **Atualizar pelo GitHub** aplica as novas regras em uma coleta explícita. Não limpe o banco nem o cache para atualizar a interface.
