namespace Portfolio.Api.Services;

// Prerequisites, not catalog position, open deeper challenges. Suggestions never become evidence.
public static class LearningTrackProgression
{
    private const string Sql = "SQL,SQL Server,PostgreSQL,SQLite,MySQL,TSQL";
    private const string Integration = "Testes de integração,WebApplicationFactory,Testcontainers";
    private const string Auth = "Autenticação,Autorização,Autenticação integrada,ASP.NET Core Identity,Identity,JWT,OAuth,Spring Security";
    private const string Logging = "Logging estruturado,Serilog,NLog,Logback,Logging,Observabilidade,OpenTelemetry";
    private const string Observability = "Observabilidade,OpenTelemetry,Métricas,Tracing";
    private const string Cache = "Cache,Caching,Redis,IMemoryCache,MemoryCache";
    private const string Architecture = "Arquitetura em camadas,Clean Architecture,Arquitetura modular,Arquitetura hexagonal";
    private const string Api = "Web API,REST API,API REST";
    private const string Backend = "ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask";
    private const string FrontTests = "Testes de frontend,Jest,Vitest";
    private static string[] Names(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[][] Groups(string value) => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Names).ToArray();
    private static LearningStep A(string topic, LearningStage stage, string signals, string requires, int priority,
        string rationale, string practice, string boostWhen = "", int boost = 0, string? concept = null) =>
        new(topic, Names(signals).Append(topic).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), Groups(requires), practice, false)
        { Advanced = true, Stage = stage, Priority = priority, Rationale = rationale, BoostWhen = Groups(boostWhen), Boost = boost, Concept = concept };

    public static LearningTrack Expand(LearningTrack track)
    {
        var steps = track.Steps.Select(s => s with { Stage = BaseStage(s.Topic) }).ToArray();
        if (track.Name == "Backend .NET")
            steps = steps.Select(s => s.Topic == "Docker" ? s with { Requires = Groups($"ASP.NET Core;{Api};xUnit,NUnit,MSTest,Testes .NET,Testes automatizados") }
                : s.Topic == "Testes .NET" ? s with { Signals = ["xUnit", "NUnit", "MSTest", "Testes .NET"] } : s).ToArray();
        track = track with { Steps = steps };
        return track.Name switch
        {
            "Backend .NET" => BackendTrack(track, "ASP.NET Core", Api, "xUnit,NUnit,MSTest,Testes .NET", "Entity Framework Core",
                "Crie testes de integração para dois endpoints usando WebApplicationFactory e um banco de teste; valide sucesso e acesso negado."),
            "Backend Java" => BackendTrack(track, "Spring Boot", Api, "JUnit,Testes Java", "JPA,Hibernate",
                "Teste dois endpoints Spring Boot com SpringBootTest e Testcontainers, incluindo persistência e uma falha; não use banco de produção."),
            "Backend Python" => BackendTrack(track, "APIs Python,FastAPI,Flask", "APIs Python,FastAPI,Flask,REST API,API REST", "pytest,Testes Python", $"SQLAlchemy,{Sql}",
                "Teste dois endpoints com pytest e o cliente de testes de FastAPI/Flask, usando fixtures e um banco isolado."),
            "Frontend React" => Frontend(track),
            "Full Stack" => FullStack(track),
            _ => track
        };
    }

    private static LearningTrack BackendTrack(LearningTrack track, string framework, string api, string tests, string persistence, string integrationPractice)
    {
        var quality = $"{framework};{api};{Sql};{tests}";
        var production = $"{framework};{api};Docker;{tests}";
        var architecture = $"{framework};{api};{Sql};{Integration}";
        var securityTopic = track.Name == "Backend Java" ? "Spring Security" : "Autenticação e autorização";
        var advanced = new[]
        {
            A("Testes de integração", LearningStage.Quality, Integration, $"{quality};{persistence}", 100,
                "Validar API e persistência em conjunto complementa os testes unitários já evidenciados.", integrationPractice, quality, 30, "integration-tests"),
            A(securityTopic, LearningStage.Persistence, Auth, $"{framework};{api};{Sql}", 90,
                "Uma API com persistência oferece contexto para demonstrar proteção de acesso.",
                "Proteja dois endpoints com o mecanismo oficial da stack; teste autorização, expiração e acesso negado. Use JWT/OAuth somente se o fluxo escolhido exigir.", $"{framework};{Sql}", 20, "authentication"),
            A("Logging estruturado", LearningStage.Production, Logging, production, 75,
                "Uma API testada e containerizada pode tornar falhas de execução mais fáceis de investigar.",
                "Registre logs estruturados e um correlation ID por requisição; demonstre uma falha sem registrar tokens ou dados sensíveis.", "Docker", 20, "logging"),
            A("Tratamento global de erros", LearningStage.Quality, "Tratamento global de erros,ProblemDetails,ExceptionHandler", quality, 65,
                "Padronizar falhas ajuda clientes da API a lidar com validação e indisponibilidade.",
                "Centralize o tratamento de exceções, padronize respostas de erro e teste dois casos sem expor stack traces."),
            A("Validação avançada", LearningStage.Quality, "Validação avançada,FluentValidation,Bean Validation,Pydantic", quality, 60,
                "Contratos de entrada podem ser exercitados com regras e casos de borda.",
                "Valide uma operação com regras cruzadas e teste mensagens, campos inválidos e limites."),
            A("Documentação e versionamento de API", LearningStage.Persistence, "Versionamento de API,API Versioning,Documentação e versionamento de API", quality, 55,
                "Uma API persistida e testada permite avaliar evolução de contratos com compatibilidade.",
                "Documente erros e autenticação no OpenAPI e simule uma evolução de contrato, registrando sua estratégia de compatibilidade."),
            A("Arquitetura em camadas", LearningStage.Architecture, Architecture, architecture, 85,
                "Testes de integração ajudam a preservar comportamento ao separar responsabilidades.",
                "Separe uma funcionalidade em aplicação, domínio e infraestrutura; documente dependências e preserve os testes.", concept: "architecture"),
            A("SOLID aplicado", LearningStage.Architecture, "SOLID,SOLID aplicado", $"{framework};{Architecture};{tests}", 80,
                "Uma estrutura explícita permite avaliar acoplamento em um caso concreto.",
                "Refatore uma regra com duas implementações intercambiáveis; explique o princípio aplicado e valide com testes."),
            A("Arquitetura modular", LearningStage.Architecture, "Arquitetura modular,Clean Architecture,Arquitetura hexagonal", $"{framework};Arquitetura em camadas;{Integration}", 75,
                "Limites de módulos podem aprofundar uma arquitetura em camadas já evidenciada.",
                "Isole um módulo funcional, defina suas interfaces e teste os limites; compare o custo com a organização atual.", concept: "architecture"),
            A("Cache", LearningStage.Deepening, Cache, architecture, 70,
                "Endpoints com persistência e testes permitem verificar acertos, expiração e invalidação.",
                "Adicione cache a um endpoint de leitura e teste sua invalidação após escrita; registre quando a solução compensa.", concept: "cache"),
            A("Redis", LearningStage.Deepening, "Redis", $"{framework};{api};Cache,Caching,IMemoryCache,MemoryCache;{Logging}", 60,
                "Um cache local demonstrado permite comparar necessidades de compartilhamento entre instâncias.",
                "Compare cache local e Redis em ambiente de teste, medindo latência e comportamento quando o cache falha.", concept: "cache"),
            A("Background services/jobs", LearningStage.Deepening, "Background services/jobs,BackgroundService,Worker Service,Hangfire,Quartz,Spring Batch,Celery,RQ", architecture, 65,
                "Uma operação testada pode ser separada do ciclo de uma requisição quando houver trabalho demorado.",
                "Extraia uma tarefa para um job com cancelamento, idempotência e teste de falha; documente por que ela deve ser assíncrona.", concept: "background-jobs"),
            A("Mensageria", LearningStage.Deepening, "Mensageria,RabbitMQ,Kafka,Filas/Eventos", $"{framework};{Integration};Docker;Background services/jobs,BackgroundService,Hangfire,Quartz,Spring Batch,Celery,RQ", 55,
                "Jobs e testes oferecem uma base para explorar entrega e reprocessamento de mensagens.",
                "Modele um evento, produtor e consumidor local; teste duplicatas, falhas e reprocessamento antes de adotar uma fila."),
            A("Observabilidade", LearningStage.Production, Observability, $"{framework};{api};{Logging};CI/CD", 80,
                "Logs e uma pipeline permitem aprofundar o diagnóstico com métricas e rastreamento.",
                "Instrumente latência e erros com métricas e tracing; correlacione uma requisição lenta com seus logs."),
            A("Health checks", LearningStage.Production, "Health checks,HealthChecks,Spring Boot Actuator", $"{framework};{api};Docker;CI/CD", 75,
                "Uma execução automatizada em containers pode distinguir disponibilidade do processo e de dependências.",
                "Implemente checks de liveness e readiness e teste indisponibilidade de uma dependência sem consultar serviços pagos."),
            A("Segurança de API", LearningStage.Quality, "Segurança de API,API Security", $"{framework};{api};{Auth};{Integration}", 80,
                "Autenticação e testes permitem aprofundar limites de acesso além do login.",
                "Teste autorização por recurso, entradas maliciosas e limites de requisição; registre ameaças e controles adotados."),
            A("Performance/profiling", LearningStage.Deepening, "Performance/profiling,Profiling,BenchmarkDotNet,JMH", $"{quality};{Observability}", 70,
                "Métricas observáveis ajudam a escolher uma otimização apoiada em medidas.",
                "Meça um endpoint e suas consultas, identifique um gargalo e compare antes/depois com carga reproduzível."),
            A("Resiliência", LearningStage.Deepening, "Resiliência,Polly,Resilience4j,Tenacity", $"{framework};{api};{Integration};{Observability}", 65,
                "Testes e observabilidade ajudam a avaliar falhas de dependências sem esconder erros.",
                "Simule uma dependência lenta e teste timeout, circuit breaker e retries limitados somente em operações seguras."),
            A("Deploy/cloud", LearningStage.Production, "Deploy/cloud,Deploy integrado,Deploy", $"{framework};Docker;CI/CD;Health checks,HealthChecks,Spring Boot Actuator", 60,
                "Pipeline e checks permitem ensaiar publicação e recuperação de uma versão.",
                "Documente deploy com HTTPS, configuração por ambiente e rollback em ambiente gratuito ou local reproduzível.", concept: "deploy")
        };
        // Python already declares this production step; do not add an equivalent duplicate.
        if (!track.Steps.Any(s => s.Topic == "CI/CD")) advanced = advanced.Append(
            A("CI/CD", LearningStage.Production, "CI/CD", production, 80,
                "Docker e testes permitem automatizar a validação de cada alteração.",
                "Crie uma pipeline que execute testes e build da imagem, com secrets fora do repositório.", $"Docker;{tests}", 40)).ToArray();
        return track with { Steps = track.Steps.Concat(advanced).ToArray(), BaseEvidence = Groups($"{framework};{api};{persistence};{Sql};{tests};Docker") };
    }

    private static LearningTrack Frontend(LearningTrack track) => track with
    {
        BaseEvidence = Groups($"React;TypeScript;React Router;Consumo de API,Axios;{FrontTests}"),
        Steps = track.Steps.Concat(new[]
        {
            A("Arquitetura de componentes", LearningStage.Architecture, "Arquitetura de componentes", $"React;React Router;Consumo de API,Axios;{FrontTests}", 90,
                "Rotas, APIs e testes oferecem contexto para organizar responsabilidades da interface.",
                "Separe apresentação, acesso a dados e regras de uma funcionalidade; mantenha os testes e documente as dependências."),
            A("Formulários e validação", LearningStage.Applications, "Formulários e validação,React Hook Form,Formik,Zod", $"React;Consumo de API,Axios;{FrontTests}", 85,
                "Fluxos com API e testes permitem validar entradas e respostas de erro de forma consistente.",
                "Construa um formulário com validação acessível, erros vindos da API e testes de submissão inválida."),
            A("Design system", LearningStage.Architecture, "Design system,Storybook", "React;Arquitetura de componentes;Acessibilidade", 80,
                "Componentes organizados e acessibilidade oferecem base para padrões reutilizáveis.",
                "Documente três componentes com variantes, foco e estados de erro; teste consistência e acessibilidade."),
            A("Segurança frontend", LearningStage.Quality, "Segurança frontend", $"React;Consumo de API,Axios;{FrontTests}", 75,
                "Uma interface conectada pode explicitar fronteiras de confiança e tratamento seguro de dados.",
                "Revise XSS, dados sensíveis no navegador e respostas de acesso negado; teste renderização de entradas não confiáveis."),
            A("SSR/SSG", LearningStage.Deepening, "SSR/SSG,Next.js,Remix,Astro", "React;Performance web;Arquitetura de componentes", 60,
                "Medidas de performance e organização da interface permitem avaliar se renderização no servidor atende ao projeto.",
                "Compare uma página indexável com renderização cliente e SSR/SSG; documente SEO, custo e quando manter a solução atual."),
            A("CI/CD frontend", LearningStage.Production, "CI/CD,CI/CD frontend", $"React;{FrontTests};Performance web,Acessibilidade", 85,
                "Testes e verificações de qualidade podem ser automatizados para cada alteração.",
                "Execute build, testes e uma verificação de acessibilidade na pipeline, sem publicar secrets.", "Testes de frontend;Acessibilidade", 15, "ci-cd"),
            A("Observabilidade frontend", LearningStage.Production, "Observabilidade frontend,Sentry", "React;Consumo de API,Axios;Performance web;CI/CD,CI/CD frontend", 70,
                "Performance medida e pipeline permitem acompanhar erros de execução com contexto.",
                "Registre erros e métricas de navegação sem dados pessoais; reproduza uma falha e documente como investigá-la.")
        }).ToArray()
    };

    private static LearningTrack FullStack(LearningTrack track) => track with
    {
        BaseEvidence = Groups($"React,HTML;Consumo de API,Axios;{Backend};{Sql};{Integration};Docker"),
        Steps = track.Steps.Select(step => step.Topic switch
        {
            "Autenticação integrada" => step with { Signals = Names(Auth).Append("Autenticação ponta a ponta").ToArray(), Concept = "authentication" },
            "Testes de integração" => step with { Signals = Names(Integration), Concept = "integration-tests" },
            "Integração frontend/API" => step with { Signals = ["Consumo de API", "Axios"] },
            "Deploy integrado" => step with { Concept = "deploy" },
            _ => step
        }).Concat(new[]
        {
            A("Contratos API", LearningStage.Persistence, "Contratos API,Testes de contrato,Pact", $"React,HTML;Consumo de API,Axios;{Backend};{Sql}", 100,
                "Interface e API com persistência podem verificar mudanças de contrato em conjunto.",
                "Defina o contrato de uma operação real e teste compatibilidade entre cliente e API, incluindo uma mudança incompatível."),
            A("Erros entre camadas", LearningStage.Quality, "Erros entre camadas", $"React,HTML;Consumo de API,Axios;{Backend};{Integration}", 90,
                "Testes de integração permitem verificar como falhas da API chegam à interface.",
                "Padronize códigos de erro e correlation ID entre interface e API; teste loading, falha de validação e indisponibilidade.", concept: "error-handling"),
            A("Docker Compose", LearningStage.Production, "Docker Compose,Compose", $"React,HTML;{Backend};{Sql};Docker", 85,
                "Containers já evidenciados permitem exercitar a inicialização coordenada das partes.",
                "Suba interface, API e banco de teste com Compose; documente readiness, configuração e limpeza dos dados locais.", concept: "containers"),
            A("Observabilidade ponta a ponta", LearningStage.Production, "Observabilidade ponta a ponta,Observabilidade integrada", $"Consumo de API,Axios;{Backend};CI/CD;{Logging}", 80,
                "Interface conectada, logs e pipeline permitem rastrear uma operação entre camadas.",
                "Propague um identificador da interface à API e correlacione logs, métricas e uma falha reproduzível.", concept: "observability"),
            A("Cache integrado", LearningStage.Deepening, "Cache integrado", $"Consumo de API,Axios;{Backend};{Sql};{Integration}", 75,
                "Uma operação integrada e testada permite validar frescor e invalidação entre cliente e servidor.",
                "Implemente cache em uma leitura e teste atualização após escrita, evitando divergência entre interface e API.", concept: "cache"),
            A("Arquitetura da integração", LearningStage.Architecture, "Arquitetura da integração", $"Consumo de API,Axios;{Backend};Contratos API,Testes de contrato,Pact;{Integration}", 70,
                "Contratos e testes permitem revisar fronteiras e acoplamento entre as partes.",
                "Documente os limites de uma funcionalidade ponta a ponta e demonstre uma mudança interna sem quebrar o contrato.", concept: "architecture")
        }).ToArray()
    };

    private static LearningStage BaseStage(string topic) => topic switch
    {
        "Python" or "Java" or "C#" or "POO" or "HTML" or "CSS" or "JavaScript" or "JavaScript/TypeScript" or "TypeScript" => LearningStage.Fundamentals,
        "SQL" or "SQL Server" or "Banco SQL" or "Persistência SQL" or "Entity Framework Core" or "JPA" or "Persistência" or "Consumo de API" or "Integração frontend/API" or "Autenticação integrada" => LearningStage.Persistence,
        "Testes .NET" or "Testes com pytest" or "JUnit" or "Testes de frontend" or "Testes mobile" or "Testes automatizados" or "Testes de integração" or "Testes de dados" or "Acessibilidade" or "Performance web" => LearningStage.Quality,
        "Docker" or "CI/CD" or "Deploy integrado" => LearningStage.Production,
        _ => LearningStage.Applications
    };

    public static string StageName(LearningStage stage) => stage switch
    {
        LearningStage.Fundamentals => "Fundamentos", LearningStage.Applications => "Construção de aplicações",
        LearningStage.Persistence => "Persistência e integração", LearningStage.Quality => "Qualidade",
        LearningStage.Architecture => "Arquitetura", LearningStage.Production => "Produção", _ => "Aprofundamento"
    };
}
