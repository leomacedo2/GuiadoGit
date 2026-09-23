# Trilhas de aprendizagem

O RecommendationService usa o catálogo LearningTrackCatalog, com Backend Python, Backend Java, Frontend React, Backend .NET, Mobile React Native, Full Stack e Dados com Python. Cada etapa declara assunto, sinais equivalentes, grupos de pré-requisitos e atividade prática. No catálogo, vírgulas representam alternativas e ponto e vírgula separa requisitos obrigatórios. O catálogo original e `Recommend` permanecem para compatibilidade; a análise atual usa `BuildTracks` e `Priorities`, com o catálogo ampliado.

## Seleção e progressão

1. Uma trilha só participa quando há sinais de entrada: Python, Java ou C# nas trilhas backend; sinais web combinados no frontend; React Native/Expo no mobile. React presente em um app mobile não ativa automaticamente a trilha web sem HTML, CSS ou React Router.
2. Cada etapa evidenciada soma 10 pontos, mais a recorrência máxima entre seus sinais, limitada a 5 repositórios. Frequência reforça a afinidade, não mede proficiência. Etapas e nomes repetidos não multiplicam os pontos.
3. A etapa central mais avançada observada orienta a progressão. Banco, testes, Docker e CI/CD não avançam sozinhos essa posição. Sinais REST compartilhados só avançam a trilha quando há evidência de seu framework.
4. Entre etapas posteriores, são elegíveis apenas assuntos não evidenciados com pré-requisitos realmente presentes. Recomendações geradas não contam como pré-requisitos para outras recomendações da mesma execução. Etapas anteriores não são declaradas conhecidas: são apenas deixadas de fora da prioridade de próximos passos. CRUD em Python prioriza o framework em vez de retornar automaticamente a POO.
5. As trilhas elegíveis são ordenadas pela pontuação, com desempate pelo nome. Seleciona-se somente a stack backend mais evidenciada, não uma trilha obrigatória para cada linguagem. Full Stack exige sinais web e framework backend; quando elegível, compõe a visão Frontend + Backend + Full Stack. Sem Full Stack, áreas com menos da metade da pontuação da mais forte ficam fora; no máximo três áreas são exibidas. Dados exige Python e Pandas/NumPy; Mobile exige React Native/Expo.
6. Cada trilha recebe até quatro próximos passos com atividade concreta, motivo e skills consideradas. Assuntos repetidos entre trilhas são removidos, incluindo aliases de SQL; passos compartilhados como Docker ficam preferencialmente em Full Stack. As prioridades resumidas alternam entre trilhas, até três. Não há obrigação de preencher vagas se faltam pré-requisitos. Uma única ocorrência já conta como evidência da etapa; não se recomenda novamente uma tecnologia só por ter baixa recorrência.

Frontend acrescenta testes específicos, acessibilidade, estado e performance conforme pré-requisitos. Backend Java aceita Maven ou Gradle; .NET respeita o banco SQL demonstrado, sem obrigar SQL Server. Full Stack usa as stacks reais e sugere integração, persistência, autenticação integrada, testes de integração, containers, CI/CD e deploy apenas quando houver contexto. Declarar Identity sozinho não comprova autenticação integrada entre interface e API.

## Exemplos cobertos por testes

| Skills sintéticas | Recomendações esperadas |
| --- | --- |
| HTML, CSS, JavaScript | TypeScript; React; Consumo de API |
| Java, Maven, Spring Boot, REST API | JPA; JUnit; Docker |
| Python, SQLite, CRUD | Flask/FastAPI; Testes com pytest |
| C#, ASP.NET Core, Web API | Entity Framework Core; Testes .NET |
| JavaScript, React, React Native, Expo | Navegação; Consumo de API |
| Python apenas | POO |
| HTML, JavaScript, TypeScript, React, React Router, Consumo de API | Testes de frontend; Acessibilidade; Gerenciamento de estado |
| HTML, React, TypeScript, JavaScript, React Router, Consumo de API, C#, ASP.NET Core, Web API, EF Core, PostgreSQL, xUnit | Frontend: testes/acessibilidade/estado; Full Stack: autenticação integrada/testes de integração/Docker; Backend .NET sem tecnologias já demonstradas reapresentadas como novas |

No terceiro exemplo, API REST ainda não é recomendada: Flask/FastAPI foi sugerido, mas não detectado. Docker/CI/CD exigem contexto adicional. Essas escolhas são heurísticas educacionais explícitas, não uma sequência universal obrigatória.

## Evidências e interface

As prioridades `recommendations` mantêm `topic`, `track`, `reason`, `consideredEvidence` e `nextStep`, até três. O novo `learningTracks` contém `name`, `area`, `demonstratedSkills` e `nextSteps` (assunto, motivo, atividade e nomes das skills consideradas). Recorrência e evidências completas continuam em `skills`, sem multiplicá-las em cada etapa do snapshot. A página permite filtrar áreas e recolher os motivos; prioridades ficam em uma seção opcional. Até três trilhas e quatro próximos passos por trilha, sem pontuação de proficiência.

Snapshots antigos continuam mostrando as recomendações originais. Não há recálculo silencioso nem chamada GitHub adicional quando o cache está fresco: uma atualização explícita obtém as novas regras. Os campos adicionais ficam no JSON de metadados existente, sem migration.

A detecção foi ampliada, sem novas chamadas externas, para HTML/CSS, React Router, cliente HTTP Axios, Expo Router, React Navigation, AsyncStorage, Expo SQLite, dependências JPA e SDK Microsoft.NET.Sdk.Web. O SDK demonstra ASP.NET Core, mas não comprova Web API. Axios é indício de consumo de API, não comprovação de execução de requisições.

POO e CRUD não são inferidos automaticamente a partir da linguagem. Podem ser entradas de futuros detectores; os testes sintéticos usam esses sinais explicitamente. O catálogo aceita alguns aliases que o detector atual ainda não emite. A falta de sinal não significa desconhecimento. Sinais de repositórios diferentes são agregados por perfil; a recomendação não afirma que todas as tecnologias convivem no mesmo projeto. A cobertura parcial da coleta continua limitando as conclusões.

## Validação histórica da primeira versão de trilhas

60 testes aprovados; build .NET 8 sem avisos/erros e build Vite aprovado. Nenhuma chamada real ao GitHub foi necessária nesta etapa. Os testes verificam perfis diferentes, pré-requisitos, recorrência, limite de três, sinais já observados, perfil vazio, contexto mobile, evidências rastreáveis e detecção dos novos sinais.

Criados: `backend/Portfolio.Api/Services/LearningTrackCatalog.cs`, `backend/Portfolio.Api.Tests/LearningTrackTests.cs`, este documento.

Modificados: `RecommendationService.cs`, `SkillDetector.cs`, `DTOs/AnalysisDto.cs`, `AnalysisTests.cs`, `frontend/src/components/RecommendationCard.jsx`, `frontend/src/pages/RecommendationsPage.jsx`, `README.md` e `docs/analise.md`. Autenticação, rate limiting e orçamento de inspeção não foram alterados.

Validação atual e inventário completo: [refinamento.md](refinamento.md).
