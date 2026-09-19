# Trilhas de aprendizagem

O RecommendationService usa o catálogo LearningTrackCatalog, com cinco trilhas ordenadas: Backend Python, Backend Java, Frontend React, Backend .NET e Mobile React Native. Cada etapa declara assunto, sinais equivalentes, grupos de pré-requisitos e atividade prática. No catálogo, vírgulas representam alternativas e ponto e vírgula separa requisitos obrigatórios.

## Seleção e progressão

1. Uma trilha só participa quando há sinais de entrada: Python, Java ou C# nas trilhas backend; sinais web combinados no frontend; React Native/Expo no mobile. React presente em um app mobile não ativa automaticamente a trilha web sem HTML, CSS ou React Router.
2. Cada etapa evidenciada soma 10 pontos, mais a recorrência máxima entre seus sinais, limitada a 5 repositórios. Frequência reforça a afinidade, não mede proficiência. Etapas e nomes repetidos não multiplicam os pontos.
3. A etapa central mais avançada observada orienta a progressão. Banco, testes, Docker e CI/CD não avançam sozinhos essa posição. Sinais REST compartilhados só avançam a trilha quando há evidência de seu framework.
4. Entre etapas posteriores, são elegíveis apenas assuntos não evidenciados com pré-requisitos realmente presentes. Recomendações geradas não contam como pré-requisitos para outras recomendações da mesma execução. Etapas anteriores não são declaradas conhecidas: são apenas deixadas de fora da prioridade de próximos passos. CRUD em Python prioriza o framework em vez de retornar automaticamente a POO.
5. As trilhas elegíveis são ordenadas pela pontuação, com desempate pelo nome. Trilhas com menos da metade da pontuação da mais forte ficam fora das recomendações principais. Há uma rodada por trilha antes de selecionar outros passos, evitando que uma única trilha ocupe tudo quando há evidências comparáveis.
6. São retornadas até três sugestões sem assuntos repetidos. Não é obrigatório preencher três vagas. Uma única ocorrência já conta como evidência da etapa; não se recomenda novamente uma tecnologia só por ter baixa recorrência.

## Exemplos cobertos por testes

| Skills sintéticas | Recomendações esperadas |
| --- | --- |
| HTML, CSS, JavaScript | TypeScript; React; Consumo de API |
| Java, Maven, Spring Boot, REST API | JPA; JUnit; Docker |
| Python, SQLite, CRUD | Flask/FastAPI; Testes automatizados |
| C#, ASP.NET Core, Web API | Entity Framework Core; Testes automatizados |
| JavaScript, React, React Native, Expo | Navegação; Consumo de API |
| Python apenas | POO |

No terceiro exemplo, API REST ainda não é recomendada: Flask/FastAPI foi sugerido, mas não detectado. Docker/CI/CD exigem contexto adicional. Essas escolhas são heurísticas educacionais explícitas, não uma sequência universal obrigatória.

## Evidências e interface

Cada recomendação contém `topic`, `track`, `reason`, `consideredEvidence` e `nextStep`. As evidências consideradas incluem nome da skill, recorrência e os motivos/repositórios de origem. A interface mostra trilha, motivo e atividade, com detalhes recolhíveis. O máximo de três é aplicado no backend, inclusive na página completa.

A detecção foi ampliada, sem novas chamadas externas, para HTML/CSS, React Router, cliente HTTP Axios, Expo Router, React Navigation, AsyncStorage, Expo SQLite, dependências JPA e SDK Microsoft.NET.Sdk.Web. O SDK demonstra ASP.NET Core, mas não comprova Web API. Axios é indício de consumo de API, não comprovação de execução de requisições.

POO e CRUD não são inferidos automaticamente a partir da linguagem. Podem ser entradas de futuros detectores; os testes sintéticos usam esses sinais explicitamente. O catálogo aceita alguns aliases que o detector atual ainda não emite. A falta de sinal não significa desconhecimento. Sinais de repositórios diferentes são agregados por perfil; a recomendação não afirma que todas as tecnologias convivem no mesmo projeto. A cobertura parcial da coleta continua limitando as conclusões.

## Validação e arquivos

60 testes aprovados; build .NET 8 sem avisos/erros e build Vite aprovado. Nenhuma chamada real ao GitHub foi necessária nesta etapa. Os testes verificam perfis diferentes, pré-requisitos, recorrência, limite de três, sinais já observados, perfil vazio, contexto mobile, evidências rastreáveis e detecção dos novos sinais.

Criados: `backend/Portfolio.Api/Services/LearningTrackCatalog.cs`, `backend/Portfolio.Api.Tests/LearningTrackTests.cs`, este documento.

Modificados: `RecommendationService.cs`, `SkillDetector.cs`, `DTOs/AnalysisDto.cs`, `AnalysisTests.cs`, `frontend/src/components/RecommendationCard.jsx`, `frontend/src/pages/RecommendationsPage.jsx`, `README.md` e `docs/analise.md`. Autenticação, rate limiting e orçamento de inspeção não foram alterados.
