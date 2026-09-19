# Deploy manual gratuito: Vercel → Render → Supabase

Este roteiro usa o repositório atual, sem trocar stack e sem criar outro banco. O Supabase existente já tem a migration aplicada. Nenhum deploy, conexão de conta ou alteração no banco foi feito automaticamente.

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

`frontend/vercel.json` reescreve navegações SPA para `/index.html`. Acesso direto/reload de `/login`, `/cadastro`, `/skills`, `/recomendacoes`, `/repositorios`, `/perfil` e `/minhas-analises` carrega o React em vez de 404. A rota pessoal pede login após recarregar, conforme a sessão em memória já existente. [Vite e SPA no Vercel](https://vercel.com/docs/frameworks/frontend/vite).

## Parte C — CORS e HTTPS

1. Volte ao serviço Render → **Environment**.
2. Adicione/atualize **`Frontend__BaseUrl`** com a origem Vercel estável: `https://portfolio-example.vercel.app`.
3. Use **Save, rebuild and deploy / Save and deploy**, conforme o painel, para reiniciar o backend com o novo ambiente.
4. Espere `/health` responder. Abra o Vercel e faça uma consulta.

Em Development continua `Cors:AllowedOrigins`, incluindo localhost:5173. Em Production somente `Frontend__BaseUrl` é permitida; localhost não é herdado. A URL deve ser HTTPS sem caminho, query, credenciais ou wildcard. Uma barra final é normalizada. Domínios temporários de Preview não são automaticamente liberados; teste pelo domínio estável. Se trocar para um Preview para testes, atualize a origem exata e depois restaure a de produção.

O preflight permite GET/POST/PUT/DELETE e headers, incluindo Authorization. Não há `AllowAnyOrigin`, cookies de autenticação ou `AllowCredentials`. O bearer oficial continua em memória, expira em 30 minutos, é enviado apenas para endpoints pessoais e respostas 401 encerram a sessão. Logout local e proteção `[Authorize]` permanecem. Render termina TLS na borda e encaminha HTTP ao container; não foi adicionado redirecionamento HTTPS interno que causaria loops. Use sempre as URLs públicas HTTPS.

Não foi habilitada confiança irrestrita em `X-Forwarded-For`. O limitador de login pode agrupar visitantes pelo IP do proxy Render (30 requisições/minuto); a trava por conta do Identity continua ativa. Se houver 429 em uma apresentação com várias pessoas, espere um minuto. Configurar proxies confiáveis e limites para tráfego maior fica fora deste MVP.

## Migrations: manuais, sem alteração nesta etapa

A migration inicial já está aplicada no Supabase: **não precisa reaplicá-la para este deploy**. O startup apenas consulta disponibilidade e migrations pendentes, sem `Migrate`, `EnsureCreated`, reset ou exclusão. Nenhuma migration nova foi criada nesta etapa.

Para uma mudança de modelo futura, revise a migration e seu SQL, faça backup adequado antes de alterações de dados e execute da sua máquina (raiz do repositório), usando os User Secrets existentes que apontam ao mesmo Supabase:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore
dotnet ef migrations list --project backend/Portfolio.Api
dotnet ef migrations script --idempotent --project backend/Portfolio.Api --output migration-review.sql
# Após revisar o SQL, aplique somente as pendentes:
dotnet ef database update --project backend/Portfolio.Api
```

`database update` sem destino aplica migrations pendentes, não apaga o banco automaticamente; uma migration escrita com operações destrutivas ainda pode apagar dados, por isso a revisão é necessária. Não use `database drop`, `database update 0`, `EnsureDeleted` ou alteração manual do histórico. Não configure migrations no build/start do Render, nem dependa de shell/jobs pagos. Se não há mudança de modelo, pule estes comandos.

## Cache, logs e sessões

Seis horas definem apenas o prazo de frescor. **Nada é apagado por expiração**: snapshots antigos continuam reconstruíveis no PostgreSQL. Abrir pela lista pessoal não chama GitHub. Force refresh e orçamento permanecem iguais; o segredo GitHub existe apenas no backend.

Os logs registram ambiente/inicialização, banco disponível/indisponível, quantidade de migrations pendentes, status de falhas GitHub e tipos de falhas de integração. Os logs próprios não registram bodies, headers, parâmetros, URL de conexão ou mensagens brutas de exceção. Em Production, logs brutos do EF/Npgsql/HttpClient e do middleware de exceções são desativados; falhas HTTP 5xx geram status e TraceId. Não habilite SensitiveDataLogging, HTTP body logging ou dumps de ambiente para diagnosticar secrets.

O filesystem gratuito Render é efêmero. As chaves Data Protection dos bearer tokens não são persistidas externamente nesta fase: após restart/redeploy/sleep, pode ser necessário entrar novamente antes dos 30 minutos. Nenhuma conta ou análise é perdida, pois esses dados estão no Supabase. Não há disco pago ou solução própria de token. Esta limitação é aceitável para a sessão transitória do MVP; não promete sessões duráveis entre reinícios.

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

Os testes completos exigem Docker Desktop (PostgreSQL descartável), sem usar o Supabase real ou GitHub real. Foram preservados os 73 testes anteriores e adicionados 11 testes/casos de porta, CORS, health e status do banco: **84 aprovados**. Builds .NET/Vite e Docker aprovados. O banco real foi consultado apenas pelo diagnóstico de leitura, sem migrations ou dados de teste. O deploy remoto e o comportamento real do domínio Vercel/Render só podem ser confirmados por você após publicar.

## Arquivos desta etapa

Criados: `.dockerignore`, `backend/Portfolio.Api/Dockerfile`, `backend/Portfolio.Api/appsettings.Production.json`, `backend/Portfolio.Api/Deployment/DeploymentConfiguration.cs`, `DatabaseDiagnostics.cs`, `GitHubDiagnosticsHandler.cs`, `backend/Portfolio.Api.Tests/DeploymentTests.cs`, `frontend/vercel.json`, `docs/deploy.md`.

Modificados: `backend/Portfolio.Api/Program.cs`, `backend/Portfolio.Api.Tests/PersistenceTests.cs`, `frontend/src/services/api.js`, `frontend/src/pages/AuthPage.jsx`, `frontend/src/App.jsx`, `README.md`, `docs/persistencia.md` (referência ao diagnóstico agora disponível em produção).
