# Autenticação da aplicação no GitHub

O backend pode consultar dados públicos usando um OAuth App da sua conta, sem login ou autorização dos visitantes. O Client ID e o Client Secret são enviados via HTTP Basic, no header Authorization, somente para `https://api.github.com/`. Não são parâmetros de URL nem valores enviados ao React.

## Criar o OAuth App

1. Entre na sua conta atual do GitHub e abra **Settings → Developer settings → OAuth Apps → New OAuth App** (ou **Register a new application**).
2. Preencha:
   - **Application name:** `Portfolio Curso Desenvolvimento` (nome provisório, pode ser alterado).
   - **Homepage URL:** `http://localhost:5173`.
   - **Application description:** `Consulta educacional de perfis públicos do GitHub` (opcional).
   - **Authorization callback URL:** `http://127.0.0.1:5080/github/callback`.
3. Não habilite Device Flow. Não é necessário alterar opções de tokens de usuários, pois não usamos esse fluxo.
4. Clique em **Register application**.
5. Na página do aplicativo, copie o **Client ID**.
6. Clique em **Generate a new client secret**, confirme sua identidade se solicitado e copie o **Client Secret** gerado.

A callback é um campo do cadastro, reservado e **não implementado nem utilizado nesta fase**. Não há redirecionamento, autorização de visitante ou endpoint de login. Quando a aplicação tiver hospedagem própria, ajuste as URLs do cadastro. Não use sua senha do GitHub, App ID, código temporário ou um token como Client Secret.

## Configurar no desenvolvimento

Pare o backend. Execute os comandos abaixo na pasta `ProjetoFinal`, substituindo **localmente** os dois valores fictícios pelos valores copiados. Não envie as credenciais ao chat e não salve os comandos preenchidos em arquivos do projeto.

```powershell
dotnet user-secrets set "GitHub:ClientId" "CLIENT_ID_FICTICIO" --project backend/Portfolio.Api
dotnet user-secrets set "GitHub:ClientSecret" "CLIENT_SECRET_FICTICIO" --project backend/Portfolio.Api
```

O projeto já possui UserSecretsId; não é necessário executar `user-secrets init`. Esses comandos gravam fora do repositório. User Secrets é armazenamento local de desenvolvimento, não um cofre criptografado. Evite executar `user-secrets list` em telas compartilhadas, pois ele mostra valores.

Se havia um token configurado, remova-o antes de usar o par OAuth:

```powershell
dotnet user-secrets remove "GitHub:Token" --project backend/Portfolio.Api
Remove-Item Env:GitHub__Token -ErrorAction SilentlyContinue
```

O segundo comando remove a variável apenas do terminal atual. Se havia uma variável persistente do Windows ou do ambiente de hospedagem, remova-a na configuração correspondente e reinicie o Visual Studio. Variáveis de ambiente têm precedência sobre User Secrets; confira se não há configurações antigas sobrescrevendo o par.

Inicie o backend pelo perfil **http** no Visual Studio 2022 (F5), ou:

```powershell
dotnet run --project backend/Portfolio.Api
```

O perfil atual usa `ASPNETCORE_ENVIRONMENT=Development`, portanto o ASP.NET Core carrega User Secrets automaticamente. Sem credenciais, o modo público anônimo continua disponível. Um par incompleto ou o uso simultâneo de par OAuth e Token impede a inicialização com mensagem sem valores secretos. Credenciais inválidas não fazem fallback silencioso para modo anônimo.

## Produção por variáveis de ambiente

Cadastre no ambiente privado do processo/serviço de hospedagem:

```text
GitHub__ClientId=CLIENT_ID_FICTICIO
GitHub__ClientSecret=CLIENT_SECRET_FICTICIO
ASPNETCORE_ENVIRONMENT=Production
```

Substitua os valores apenas no painel seguro da hospedagem, nunca em `appsettings.json`, `launchSettings.json`, React, arquivos versionados ou documentação. Remova `GitHub__Token` se existir. Reinicie o processo depois da alteração. Não use o perfil local de Development para publicar. HTTPS deve ser configurado na hospedagem quando essa fase for autorizada.

## Verificar o limite depois de configurar

Não é necessário repetir análises inteiras para conferir a configuração. Com o backend reiniciado e as credenciais configuradas:

1. Execute uma consulta simples do perfil (para `leomacedo2`, normalmente são duas chamadas externas: perfil e uma página de repositórios):

```powershell
Invoke-RestMethod "http://localhost:5080/api/github/profile/leomacedo2" | Select-Object username, publicRepositories
```

2. Leia os últimos headers recebidos:

```powershell
Invoke-RestMethod "http://localhost:5080/api/github/rate-limit" | Format-List
```

Esse segundo endpoint **não chama o GitHub**, não expõe credenciais e só existe em Development. Também pode ser aberto no navegador local. Retorna:

- `limit`: valor de `x-ratelimit-limit`; esperado **5000** para OAuth App pessoal, em vez de **60** no modo anônimo.
- `remaining`: valor de `x-ratelimit-remaining`; após as chamadas será menor que 5000.
- `resetAt`: `x-ratelimit-reset` convertido de Unix timestamp para data/hora UTC.
- `observedAt`: momento da observação dos headers, em UTC.
- `blockedUntil`: horário até o qual novas chamadas serão bloqueadas localmente, ou null se não há bloqueio conhecido.

Antes da primeira resposta do GitHub, os campos são null. São os últimos valores observados por este processo, não uma consulta em tempo real da conta. Se `limit` continuar em 60, confira os nomes das chaves e o ambiente Development e reinicie o backend. Se o GitHub recusar as credenciais, confira o par e se o secret foi revogado; não tente repetidamente. Um valor null significa informação indisponível, não cota zero.

## Rate limit e resultados parciais

- Headers são lidos em todas as respostas, inclusive erros e a última resposta de sucesso com `remaining=0`.
- A última resposta de sucesso é aproveitada; a próxima chamada é bloqueada até o reset conhecido.
- 403 por cota e 429 geram mensagem amigável; `Retry-After` também é respeitado. Se não há reset válido, aplica-se pausa mínima de um minuto. Não há retry automático.
- O estado é compartilhado entre clientes do mesmo processo e esquecido ao reiniciar. Não coordena múltiplas instâncias de produção nem outros programas usando o mesmo OAuth App. Não reinicie para tentar contornar limites do GitHub.
- Se não foi possível obter o perfil, a API responde 429. Se já há perfil e parte da lista, essa parte é devolvida com `profile.warnings`; a análise propaga os avisos e interrompe a inspeção. Falhas posteriores mantêm evidências já obtidas.
- Na Home, o aviso de cobertura parcial e seus detalhes mostram a limitação. A quantidade pública total pode ser maior que a quantidade recebida/analisada.
- A análise individual agora usa tetos configuráveis: 200 repositórios, 600 chamadas adicionais e 180 segundos por padrão, mantendo até dois manifestos por repositório. Essa ampliação foi uma decisão explícita de produto; autenticar não altera automaticamente os tetos. Veja analise.md.
- Logs do HttpClient ocultam headers; redirecionamentos automáticos estão desativados. Não há segredos no frontend, no endpoint de diagnóstico ou nas mensagens de erro.

## Arquivos desta alteração

Validação realizada: 38 testes aprovados, incluindo todos os 27 anteriores, e builds do backend .NET 8 e frontend concluídos. O diagnóstico local foi conferido sem disparar chamadas externas. Nenhuma requisição real à GitHub REST API foi feita nesta implementação; a confirmação da cota autenticada depende do cadastro e configuração das suas credenciais.

Criados: `Services/GitHubClientConfiguration.cs`, `Services/GitHubRateLimitState.cs`, `Services/GitHubRateLimitHandler.cs` dentro de `backend/Portfolio.Api`; `backend/Portfolio.Api.Tests/GitHubAuthenticationTests.cs`; este documento.

Modificados: `backend/Portfolio.Api/Program.cs`, `Services/GitHubService.cs`, `Services/AnalysisService.cs`, `DTOs/PortfolioDto.cs`; `README.md` e `docs/analise.md`. Nenhum arquivo React ou appsettings recebeu credenciais.

Referências oficiais: [criar OAuth App](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app), [limites da REST API e credenciais de aplicação](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api).
