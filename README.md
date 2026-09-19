# Portfólio GitHub — evidências e recomendações

Integração real entre React → ASP.NET Core Web API (.NET 8) → GitHub REST API. Nome e logo definitivos ainda estão em aberto. O plano mestre original foi lido integralmente e preservado.

## Escopo entregue

Consulta pública sem login: perfil, repositórios, skills com evidências explicáveis e recomendações de estudo por regras. A Home mostra perfil, cobertura, até seis skills e três recomendações. As listas completas ficam em `/skills`, `/recomendacoes` e `/repositorios`; `/perfil` também mostra o resumo. Inclui loading, mensagens de erro, validação de username e Bootstrap 5 com tema claro/escuro.

Inclui PostgreSQL com EF Core 8, contas da plataforma, cache compartilhado por seis horas e `/minhas-analises`. Uma conta autenticada pode conectar opcionalmente seu GitHub em `/github`, após habilitação segura pelo administrador: veja [configuração OAuth e proteção dos tokens](docs/github-autenticacao.md). Isso não substitui cadastro/login da plataforma. O Supabase é usado como PostgreSQL. Não há vagas, turmas ou IA.

**Comece pelo [guia de persistência e Supabase](docs/persistencia.md)** para criar/configurar o banco, aplicar a migration inicial e testar cadastro, login e análises salvas. Sem banco configurado, a consulta pública continua disponível, mas cache e contas ficam indisponíveis.

As regras, níveis, limites e a lista de arquivos desta fase estão em [docs/analise.md](docs/analise.md).

Deploy público preparado para **Vercel Hobby → Render Free (.NET 8/Docker) → Supabase existente**. Siga o [guia de deploy manual](docs/deploy.md), com variáveis, CORS, migrations e smoke test. A execução local permanece em localhost:5173/5080; nenhuma conta externa é criada automaticamente.

## Requisitos

- .NET SDK 8.0.424 (ou patch posterior da faixa 8.0.4xx). O `global.json` impede a seleção do SDK 9.
- Node.js 22.12+ (testado com 24.20.0) e npm.
- Internet para restaurar dependências e consultar o GitHub.
- PostgreSQL local ou hospedado no Supabase para cache/contas; Docker Desktop em modo Linux para os testes de integração descartáveis.

## Iniciar

### Backend pelo Visual Studio 2022

1. Abra `Portfolio.sln` no Visual Studio 2022 com a carga de trabalho **ASP.NET e desenvolvimento Web** instalada e suporte ao SDK .NET 8 usado pelo projeto.
2. No Gerenciador de Soluções, clique com o botão direito em **Portfolio.Api** e selecione **Definir como Projeto de Inicialização**.
3. Selecione o perfil **http** ao lado do botão verde e pressione **F5** (depuração) ou **Ctrl+F5** (sem depuração).

A API escuta em http://localhost:5080. O `launchSettings.json` mantém `commandName: Project`, ambiente Development e `launchBrowser: false`: o backend inicia sem abrir uma página própria. Para encerrar a depuração, use Shift+F5.

### Frontend pelo VS Code

1. Abra a pasta **ProjetoFinal** inteira no VS Code, para carregar `.vscode/tasks.json`.
2. Na primeira execução ou após mudanças nas dependências, use **Terminal → Executar Tarefa… → Frontend: instalar dependências**.
3. Use **Terminal → Executar Tarefa… → Frontend: iniciar Vite**. Também é possível abrir a paleta com Ctrl+Shift+P e buscar **Tasks: Run Task**.
4. Abra http://localhost:5173 e consulte `leomacedo2`.

A task executa `npm run dev` dentro de `frontend` no terminal integrado, sem terminal externo. Para encerrar, use **Tasks: Terminate Task** na paleta ou Ctrl+C no terminal da task. Nenhuma porta foi alterada. Encerre instâncias anteriores antes de iniciar pelo editor.

### Alternativa pelo terminal

Abra dois terminais na pasta `ProjetoFinal`.

**Terminal 1 — backend:**

```powershell
dotnet run --project backend/Portfolio.Api
```

API em http://localhost:5080. O perfil de execução configura o ambiente Development.

**Terminal 2 — frontend:**

```powershell
cd frontend
npm ci
npm run dev
```

Abra http://localhost:5173. Nas próximas execuções, basta `npm run dev` dentro de `frontend`. Mantenha ambos os terminais abertos. Use Ctrl+C para encerrar.

Não são necessárias credenciais nem arquivo `.env` para o primeiro teste. Se as portas estiverem ocupadas, encerre a instância anterior. O Vite não muda silenciosamente para outra porta.

## Tema claro e escuro

O padrão inicial é escuro. O botão discreto no topo alterna entre **Tema claro** e **Tema escuro**. A preferência é salva na chave `portfolio-theme` do `localStorage` deste navegador/origem e aplicada antes da renderização ao recarregar a página.

Os componentes usam `data-bs-theme` e variáveis/classes nativas do Bootstrap, sem duplicação de folhas de estilo. Se o navegador bloquear armazenamento local, a alternância funciona durante a sessão, mas a preferência não poderá ser persistida. Sem preferência válida, o padrão continua escuro.

Para conferir: abra a página sem preferência salva, alterne para claro, recarregue e confirme que o claro permanece; repita para escuro e consulte um perfil para verificar os cards nos dois temas.

## Testar o marco

1. Abra a página inicial.
2. Digite `leomacedo2` e clique em **Analisar Portfólio**.
3. Confira o loading, o perfil, a cobertura e os resumos de skills e recomendações.
4. Navegue pelos três botões da Home; em Skills, confira os repositórios e motivos. Em Repositórios, a lista completa continua disponível, com os links originais.
5. Para testar erro 404, consulte `portfolio-missing-7d8931b9` (inexistente na validação).
6. Para testar validação, digite `test--name`.
7. Para testar falha de conexão, pare o backend e faça uma nova consulta; depois reinicie-o.

Consulta direta pelo PowerShell:

```powershell
Invoke-RestMethod http://localhost:5080/api/github/profile/leomacedo2
```

## Build e testes

Na raiz:

```powershell
dotnet build Portfolio.sln
dotnet test Portfolio.sln
```

No frontend:

```powershell
npm run build
```

Validação histórica do primeiro marco em 17/09/2026 (resultados das fases posteriores estão nos guias de persistência, deploy e autenticação):

- Backend compilado em .NET 8, sem avisos ou erros.
- Frontend compilado para produção.
- 15 testes aprovados: paginação de 101 repositórios, campos nulos, 404, 429, 401/403/500 externos, limite via header, JSON inválido, conexão, timeout e usernames inválidos.
- Consulta real retornou Leonardo Moraes Macedo (`leomacedo2`), 25 repositórios públicos e 25 itens na lista.
- Perfil e repositórios verificados no navegador, incluindo avatar, loading e erro de usuário inexistente.
- Layout verificado em desktop e viewport de celular, sem transbordamento horizontal.
- CORS validado: origem local permitida e origem não autorizada sem header de permissão.

Os testes automatizados usam respostas simuladas para não consumir a cota do GitHub. A validação manual acima usou dados reais. A quantidade de repositórios pode mudar com o tempo. A bio estava ausente; a interface informa essa ausência.

## Arquitetura e arquivos criados

```text
.gitignore
.vscode/tasks.json
global.json
Portfolio.sln
README.md
backend/
  Portfolio.Api/
    Portfolio.Api.csproj
    Program.cs
    appsettings.json
    Properties/launchSettings.json
    Controllers/GitHubController.cs
    DTOs/PortfolioDto.cs
    Models/GitHubModels.cs
    Services/IGitHubService.cs
    Services/GitHubService.cs
    Services/GitHubApiException.cs
  Portfolio.Api.Tests/
    Portfolio.Api.Tests.csproj
    GitHubServiceTests.cs
frontend/
  .env.example
  package.json
  package-lock.json
  vite.config.js
  index.html
  src/
    main.jsx
    App.jsx
    styles.css
    services/api.js
```

O plano mestre foi preservado. Pastas geradas por instalação/build (`node_modules`, `dist`, `bin`, `obj`) estão no `.gitignore`. A lista acima registra a estrutura inicial; os arquivos desta fase estão relacionados abaixo.

O endpoint original `GET /api/github/profile/{username}` continua disponível com o mesmo contrato. O frontend agora usa `GET /api/analyses/{username}`, que reutiliza a integração e acrescenta skills, recomendações e cobertura. O controller valida a entrada e converte erros em Problem Details.

`AddHttpClient` registra um cliente tipado com gerenciamento de conexões pelo ASP.NET Core. As chamadas usam User-Agent, Accept e versão da API explícitos. A lista é consultada em páginas de até 100 itens, ordenada pela atualização mais recente. Inclui forks e repositórios arquivados públicos do proprietário.

Erros retornados: 400 para username inválido; 404 para usuário inexistente; 429 para limite identificado; 502 para conexão, resposta inválida ou outras falhas do GitHub; 504 para timeout. O conteúdo bruto de erros externos não é exposto.

## Configuração e segredos

O frontend conhece somente a URL pública do backend. Para alterá-la, copie `frontend/.env.example` para `frontend/.env.local`, ajuste `VITE_API_BASE_URL` e reinicie o Vite. Nunca coloque credenciais em variáveis `VITE_*`, pois seus valores são incorporados ao JavaScript entregue ao navegador.

CORS permite `http://localhost:5173`. Se alterar a origem do frontend, ajuste `Cors:AllowedOrigins` no backend. CORS não substitui autenticação ou proteção contra abuso.

O backend aceita Client ID + Client Secret de OAuth App por User Secrets (desenvolvimento) ou variáveis de ambiente (produção). O modo anônimo e o token opcional anterior continuam disponíveis, mas não podem ser misturados com o par OAuth. Consulte o [guia de configuração segura e verificação da cota](docs/github-autenticacao.md) para o cadastro do aplicativo e os comandos exatos. Nenhum login GitHub é solicitado aos visitantes.

## Limitações deste marco

- Coletas novas dependem da conexão e da disponibilidade/cota da API pública do GitHub. Com PostgreSQL configurado, resultados recentes são reutilizados do cache; cada coleta nova faz uma chamada de perfil e uma por página de repositórios, podendo fazer uma página vazia final quando o total é múltiplo de 100.
- A obtenção do perfil/repositórios tem prazo de 45 segundos; a inspeção adicional, até 180 segundos por padrão. O Axios aguarda até 245 segundos. Se a obtenção do perfil falhar, é retornado erro; esgotamento da cota durante a paginação preserva perfil e páginas já recebidas; falhas na inspeção adicional preservam o perfil e as evidências coletadas.
- A paginação não é uma fotografia atômica: mudanças no perfil durante a consulta podem afetar a contagem. IDs repetidos são removidos.
- Datas usam `updated_at` do GitHub e o fuso horário do navegador; não representam necessariamente a data do último commit.
- Linguagens também são detectadas por extensões na amostra de arquivos. As regras não comprovam autoria, execução de testes, funcionamento dos projetos ou proficiência; forks e exemplos também podem produzir evidências.
- HTTP e origens locais foram configurados para desenvolvimento. Publicação e HTTPS ficam para um marco posterior.

Referências da integração: [GitHub REST — início](https://docs.github.com/en/rest/using-the-rest-api/getting-started-with-the-rest-api) e [repositórios de usuários](https://docs.github.com/en/rest/repos/repos#list-repositories-for-a-user).

As recomendações agora seguem [trilhas de aprendizagem](docs/trilhas.md), com até três sugestões, pré-requisitos e evidências consideradas.
