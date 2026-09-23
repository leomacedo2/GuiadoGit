# Persistência da sessão da plataforma

## Funcionamento

- O login continua validando senha e lockout pelo ASP.NET Core Identity. Emite access token de **10 minutos**, no mesmo formato/protetor oficial de `BearerTokenHandler`, e uma sessão de refresh de **30 dias absolutos** desde o login.
- O access token permanece apenas na memória do React. O endpoint não retorna o refresh oficial que o `SignIn` normalmente serializaria: retorna somente `tokenType`, `accessToken` e `expiresIn`.
- O refresh é um segredo aleatório de 256 bits (32 bytes), enviado exclusivamente no cookie `guidogit-refresh`. No PostgreSQL fica **somente SHA-256** do segredo, junto com usuário, security stamp do Identity, criação, expiração e revogação. SHA-256 é adequado a esse segredo aleatório de alta entropia; não é usado para senhas.
- `POST /api/auth/refresh` valida sessão, expiração, revogação, security stamp e bloqueio da conta. Rotaciona o hash com uma atualização condicional atômica: apenas uma requisição pode consumir o token antigo. Não prolonga os 30 dias.
- `POST /api/auth/logout` revoga a sessão, expira o cookie e permite limpar o estado local. É idempotente. Também utiliza a identificação da sessão contida no bearer, quando disponível, para revogar corretamente durante uma rotação concorrente.
- O startup React faz uma tentativa silenciosa de refresh, compartilhada entre efeitos do StrictMode, seguida de `/api/auth/me`. As rotas aguardam a tentativa antes de decidir se existe conta. Sem sessão válida, o fluxo normal de visitante/login permanece.
- Um 401 de uma chamada autenticada pode gerar um único refresh e uma única repetição, compartilhando refreshes simultâneos. Outro 401 limpa a sessão local, sem loop. Falhas de negócio ou 5xx após a repetição não removem uma sessão válida. Requisições de uma conta anterior não são repetidas depois de login/logout mudar a sessão.
- Logout com erro de rede/servidor informa que o encerramento não foi confirmado e permite tentar novamente. Não anuncia saída bem-sucedida enquanto a revogação falhou.

## Cookies, CORS e CSRF

| Ambiente | Cookie |
| --- | --- |
| Development/Testing | HttpOnly, SameSite=Lax, Path=/api/auth, expiração persistente, sem Secure para HTTP local |
| Produção | HttpOnly, Secure, SameSite=None, Path=/api/auth, expiração persistente |

Não há Domain no cookie (restrito ao host do backend). Use **localhost** tanto em 5173 quanto em 5080; não misture localhost com 127.0.0.1. Em produção, continue usando HTTPS.

Axios usa `withCredentials` somente em login, refresh e logout. CORS mantém as origens explícitas existentes, agora com `AllowCredentials`: `Cors:AllowedOrigins` em desenvolvimento e `Frontend:BaseUrl` em produção. Sem wildcard. Refresh/logout exigem o header não simples `X-Session-Request: 1`, e a origem, quando enviada, precisa estar na configuração permitida. Isso bloqueia CSRF via formulário/no-cors e exige preflight para pedidos cross-origin. Login também rejeita origens não permitidas e exige o corpo JSON existente. Clientes fora do navegador podem omitir Origin, mas refresh/logout continuam exigindo o header.

**Limitação de Vercel + Render:** os domínios atuais são cross-site. Navegadores que bloqueiam cookies de terceiros podem bloquear armazenamento/envio do refresh mesmo com SameSite=None, Secure e CORS corretos. Nesse caso, login com access token funciona, mas restauração não é garantida. Não há fallback para localStorage, proxy novo ou alteração de domínio nesta etapa. Verifique no navegador real que o cookie foi aceito. Para compatibilidade irrestrita entre navegadores seria necessário avaliar uma implantação same-site, fora deste escopo. [Referência MDN](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CORS).

## Persistência e limites

A nova tabela `portfolio.RefreshSessions` tem PK Id, FK ApplicationUserId → AspNetUsers com cascade, hash único, índice por usuário e índice de expiração. Revogar uma sessão não apaga dados, análises ou conexão GitHub. Outros dispositivos podem manter suas próprias sessões; um novo login substitui a sessão anterior do mesmo cookie.

Data Protection, certificado e OAuth GitHub existentes permanecem inalterados. Refresh depende do hash no banco, não de memória do processo, e pode emitir um novo bearer depois de reinicialização. Os tokens de acesso ainda válidos têm a limitação natural do bearer: podem ser aceitos até os **10 minutos** de validade, mesmo após logout. Logout invalida imediatamente o refresh; não foi acrescentada consulta de revogação em toda requisição da aplicação. [Formato oficial do bearer ASP.NET Core 8](https://github.com/dotnet/aspnetcore/blob/v8.0.31/src/Security/Authentication/BearerToken/src/BearerTokenHandler.cs).

A rotação não mantém lista de tokens antigos nem faz revogação de família por replay. Tokens já consumidos falham. Abas diferentes que tentem rotacionar exatamente ao mesmo tempo podem fazer uma delas receber 401; a coordenação é por aba, e a outra pode continuar usando o cookie renovado. Sessões expiradas/revogadas permanecem como registros pequenos; não foi adicionado job de limpeza. Nenhum token é persistido no JavaScript, logado ou enviado em URL.

## Migration manual — não aplicada ao Supabase

Nome: `20260923222738_AddRefreshSessions`. Cria somente a tabela e seus índices/FK; não altera migrations anteriores nem tabelas funcionais.

Na raiz, revise o SQL, sem conectar ao banco ou carregar User Secrets:

```powershell
dotnet tool restore
New-Item -ItemType Directory -Force .artifacts | Out-Null
dotnet ef migrations script 20260923151210_AddClassroomsAndRepositoryPushActivity 20260923222738_AddRefreshSessions --idempotent --project backend/Portfolio.Api --configuration Release --output .artifacts/refresh-session-migration.sql -- --environment Testing
```

Depois de conferir o SQL, com sua configuração local existente e banco correto, aplique manualmente:

```powershell
dotnet ef database update 20260923222738_AddRefreshSessions --project backend/Portfolio.Api --configuration Release -- --environment Development
```

Esse último comando usa sua configuração de desenvolvimento existente, incluindo User Secrets. O agente não o executou nem leu esses secrets. Execute somente enquanto essa for a migration pendente mais recente; não faça downgrade de um banco com migrations posteriores. Se a migration anterior de Turmas já estiver aplicada, somente AddRefreshSessions ficará pendente. Nenhuma migration é aplicada no startup ou deploy.

## Publicação e verificação manual

1. Revise e aplique a migration antes do auto-deploy; a tabela nova é compatível com o backend anterior.
2. Mantenha `Frontend__BaseUrl=https://guiadogit.vercel.app` no Render, `VITE_API_BASE_URL=https://guiadogit-api.onrender.com` no Vercel e as configurações atuais. **Nenhuma variável nova.**
3. Faça commit/push; os auto-deploys atuais são suficientes após a migration. Nenhum deploy foi feito pelo agente.
4. Faça login novamente uma vez para criar o cookie persistente (sessões da versão antiga não possuem esse cookie).
5. Recarregue `/minhas-analises`, feche/reabra o navegador, verifique a conta restaurada e a conexão GitHub preservada. DevTools deve mostrar refresh HttpOnly no host do backend, sem refresh no JSON/localStorage/sessionStorage.
6. Clique Sair, recarregue e confirme que permanece deslogado. Verifique também visitante e localhost:5173/5080. Se cookies forem bloqueados, consulte a limitação cross-site acima.

## Arquivos desta etapa

Criados:
- backend/Portfolio.Api/Auth/RefreshSessionService.cs
- backend/Portfolio.Api/Data/RefreshSession.cs
- backend/Portfolio.Api/Data/Migrations/20260923222738_AddRefreshSessions.cs
- backend/Portfolio.Api/Data/Migrations/20260923222738_AddRefreshSessions.Designer.cs
- backend/Portfolio.Api.Tests/RefreshSessionTests.cs
- frontend/tests/e2e/session.spec.js
- docs/sessao.md

Modificados:
- backend/Portfolio.Api/Controllers/AuthController.cs
- backend/Portfolio.Api/Data/PortfolioDbContext.cs
- backend/Portfolio.Api/Data/Migrations/PortfolioDbContextModelSnapshot.cs
- backend/Portfolio.Api/Program.cs
- backend/Portfolio.Api.Tests/DeploymentTests.cs
- frontend/src/App.jsx
- frontend/src/auth/AuthContext.jsx
- frontend/src/pages/AuthPage.jsx
- frontend/src/services/api.js
- frontend/tests/e2e/classrooms.spec.js (somente mocks de autenticação/CORS)
- frontend/tests/e2e/flows.spec.js (mocks de autenticação/CORS e distinção entre refresh de sessão e análise)
- docs/deploy.md

## Validação executada

217 testes backend, 10 unitários frontend e 46 cenários E2E desktop/mobile aprovados (273 ao todo). Foram adicionados 15 casos backend e 7 cenários de sessão, executados em desktop e mobile. Os testes de rotação usam PostgreSQL descartável de testes; não o Supabase. O build .NET 8 Release passou sem avisos/erros, e o build Vite passou. Não houve chamadas externas reais nos testes. O SQL da migration foi gerado e revisado, sem aplicação ao banco real.

O limitador de autenticação existente também cobre refresh/logout. Em caso de muitas inicializações simultâneas, pode responder 429; no Render, o proxy pode agrupar IPs. Seus limites não foram alterados nesta etapa.
