namespace Portfolio.Api.Services;

public enum LearningStage { Fundamentals, Applications, Persistence, Quality, Architecture, Production, Deepening }
public sealed record LearningStep(string Topic, string[] Signals, string[][] Requires, string Practice, bool Milestone = true)
{
    public LearningStage Stage { get; init; }
    public bool Advanced { get; init; }
    public int Priority { get; init; }
    public string[][] BoostWhen { get; init; } = [];
    public int Boost { get; init; }
    public string Rationale { get; init; } = "Consolide a próxima etapa da trilha em um projeto já existente.";
    public string? Concept { get; init; }
}
public sealed record LearningTrack(string Name, string[][] Entry, LearningStep[] Steps)
{
    public string[][] BaseEvidence { get; init; } = [];
}

public static class LearningTrackCatalog
{
    // Commas separate alternatives; semicolons separate groups that must all be present.
    private static string[] Names(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[][] Groups(string value) => value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Names).ToArray();
    private static LearningStep S(string topic, string signals, string requires, string practice, bool milestone = true) => new(topic, Names(signals), Groups(requires), practice, milestone);
    private static LearningTrack T(string name, string entry, params LearningStep[] steps) => new(name, Groups(entry), steps);
    public static IReadOnlyList<LearningTrack> All { get; } = [
        T("Backend Python", "Python",
            S("Python", "Python", "", "Pratique funções e módulos."),
            S("POO", "POO", "Python", "Modele uma entidade com responsabilidades claras."),
            S("Flask/FastAPI", "APIs Python,Flask,FastAPI", "Python;POO,CRUD,SQL,SQLite,SQLAlchemy", "Transforme um CRUD Python em uma pequena API com Flask ou FastAPI."),
            S("API REST", "REST API,API REST", "APIs Python,Flask,FastAPI", "Documente métodos HTTP, validações e respostas de uma API."),
            S("Banco SQL", "SQL,SQLite,SQL Server,PostgreSQL,MySQL", "APIs Python,Flask,FastAPI", "Adicione persistência e consultas parametrizadas à API.", false),
            S("Testes automatizados", "Testes automatizados,pytest", "Python;CRUD,APIs Python,Flask,FastAPI,SQLAlchemy", "Teste uma regra e um caso de erro do projeto usando pytest.", false),
            S("Docker", "Docker", "APIs Python,Flask,FastAPI;Testes automatizados,pytest", "Containerize a API e documente sua execução.", false),
            S("CI/CD", "CI/CD", "Docker;Testes automatizados,pytest", "Execute build e testes da API em um workflow.", false)),
        T("Backend Java", "Java",
            S("Java", "Java", "", "Pratique classes e coleções."),
            S("POO", "POO", "Java", "Modele responsabilidades com classes e interfaces."),
            S("Maven", "Maven", "Java", "Organize dependências e build com Maven."),
            S("Spring Boot", "Spring Boot", "Java;Maven", "Crie um serviço Spring Boot com validação."),
            S("REST API", "REST API,API REST", "Spring Boot", "Implemente endpoints e tratamento consistente de erros."),
            S("JPA", "JPA,Hibernate", "Spring Boot;REST API,API REST", "Persista uma entidade com Spring Data JPA e documente o mapeamento."),
            S("SQL", "SQL,SQLite,SQL Server,PostgreSQL,MySQL", "JPA,Hibernate", "Pratique consultas, relacionamentos e transações.", false),
            S("JUnit", "JUnit", "Java;Maven,Spring Boot", "Teste uma regra de serviço com JUnit, incluindo casos de borda.", false),
            S("Docker", "Docker", "Spring Boot;REST API,API REST;Maven", "Crie uma imagem da API Spring e documente como executá-la.", false)),
        T("Frontend React", "JavaScript,TypeScript,React;HTML,CSS,React",
            S("HTML", "HTML", "", "Estruture uma página semântica."),
            S("CSS", "CSS", "HTML", "Crie um layout responsivo com estados de foco."),
            S("JavaScript", "JavaScript", "HTML;CSS", "Pratique interações com eventos e funções."),
            S("TypeScript", "TypeScript", "JavaScript", "Tipifique os dados e funções de um projeto existente."),
            S("React", "React", "JavaScript,TypeScript;HTML,CSS", "Divida uma interface em componentes com estado e propriedades."),
            S("React Router", "React Router", "React", "Adicione rotas e uma página não encontrada."),
            S("Consumo de API", "Consumo de API,Axios", "JavaScript,TypeScript;HTML,React", "Consuma uma API com loading, validação e tratamento de erros.", false),
            S("Testes automatizados", "Testes automatizados,Jest,Vitest", "React;React Router,Consumo de API,Axios", "Teste uma interação e um estado de erro de um componente.", false)),
        T("Backend .NET", "C#",
            S("C#", "C#", "", "Pratique classes e interfaces."),
            S("ASP.NET Core", "ASP.NET Core", "C#", "Crie um serviço ASP.NET Core com injeção de dependência."),
            S("Web API", "Web API,REST API,API REST", "ASP.NET Core", "Implemente endpoints com DTOs e validação."),
            S("Entity Framework Core", "Entity Framework Core", "ASP.NET Core;Web API,REST API,API REST", "Mapeie uma entidade e pratique consultas com EF Core."),
            S("SQL Server", "SQL Server", "Entity Framework Core", "Pratique consultas e relacionamentos com SQL Server.", false),
            S("Testes automatizados", "Testes automatizados,xUnit", "C#;ASP.NET Core,Entity Framework Core", "Teste um serviço .NET com xUnit, incluindo erros.", false),
            S("Docker", "Docker", "ASP.NET Core;Web API,REST API,API REST;Testes automatizados,xUnit", "Containerize a Web API e documente sua inicialização.", false)),
        T("Mobile React Native", "React Native,Expo",
            S("JavaScript/TypeScript", "JavaScript,TypeScript", "", "Pratique funções e tipos utilizados no app."),
            S("React", "React", "JavaScript,TypeScript", "Pratique componentes, propriedades e estado."),
            S("React Native", "React Native", "React", "Crie uma tela nativa com formulário e lista."),
            S("Expo", "Expo", "React Native", "Execute um app Expo e documente sua configuração."),
            S("Navegação", "Navegação,Expo Router,React Navigation", "React Native,Expo", "Conecte duas telas com parâmetros e retorno."),
            S("Consumo de API", "Consumo de API,Axios", "React Native,Expo", "Carregue dados remotos com estados de espera e erro.", false),
            S("Persistência", "Persistência,AsyncStorage,SQLite", "React Native,Expo;Navegação,Expo Router,React Navigation,Consumo de API,Axios", "Salve uma preferência local e restaure ao abrir o app.", false),
            S("Testes automatizados", "Testes automatizados,Jest", "React Native,Expo;Persistência,AsyncStorage,SQLite", "Teste uma interação e uma falha de persistência.", false))
    ];

    // Extended views retain the original priority contract while adding practical, contextual steps.
    public static IReadOnlyList<LearningTrack> Expanded { get; } = All.Select(track => track with
    {
        Steps = track.Steps.Select(step => (track.Name, step.Topic) switch
        {
            ("Backend Java", "Maven") => step with { Topic = "Maven/Gradle", Signals = ["Maven", "Gradle"] },
            ("Backend Java", _) => step with { Requires = step.Requires.Select(group => group.Contains("Maven") ? group.Concat(["Gradle"]).ToArray() : group).ToArray() },
            ("Backend .NET", "SQL Server") => step with { Topic = "SQL", Signals = ["SQL", "SQL Server", "PostgreSQL", "SQLite", "MySQL"], Practice = "Pratique relacionamentos e consultas no banco SQL já utilizado pelo projeto." },
            ("Backend .NET", "Testes automatizados") => step with { Topic = "Testes .NET", Signals = ["xUnit", "NUnit", "Testes .NET"] },
            ("Backend Python", "Testes automatizados") => step with { Topic = "Testes com pytest", Signals = ["pytest", "Testes Python"] },
            ("Frontend React", "Testes automatizados") => step with { Topic = "Testes de frontend", Signals = ["Testes de frontend", "Jest", "Vitest"] },
            ("Mobile React Native", "Testes automatizados") => step with { Topic = "Testes mobile", Signals = ["Testes mobile"] },
            _ => step
        }).Concat(track.Name == "Frontend React" ? new[]
        {
            S("Acessibilidade", "Acessibilidade", "React;React Router,Consumo de API", "Revise navegação por teclado, rótulos de formulários e contraste; registre os testes e correções.", false),
            S("Gerenciamento de estado", "Gerenciamento de estado", "React;React Router,Consumo de API", "Implemente um fluxo de estado compartilhado e documente por que Context ou uma biblioteca é adequada.", false),
            S("Performance web", "Performance web", "React;Testes de frontend,Gerenciamento de estado", "Meça carregamento e renderização antes/depois de uma melhoria e registre os resultados.", false)
        } : []).ToArray()
    }).Concat([
        T("Full Stack", "HTML,React;JavaScript,TypeScript;ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask",
            S("Frontend do perfil", "HTML,CSS,JavaScript,TypeScript,React,React Router", "", "", false),
            S("Backend do perfil", "C#,ASP.NET Core,Web API,Java,Spring Boot,Python,APIs Python,FastAPI,Flask,REST API", "", "", false),
            S("Persistência SQL", "SQL,SQL Server,PostgreSQL,SQLite,MySQL,Entity Framework Core,JPA,SQLAlchemy", "ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask", "Adicione uma operação persistida em SQL à API da stack já demonstrada e exponha o resultado na interface.", false),
            S("Integração frontend/API", "Consumo de API", "HTML,React;ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask", "Conecte uma tela à API da stack já demonstrada, com DTOs, validação, loading e tratamento de erros.", false),
            S("Autenticação integrada", "Autenticação integrada", "Consumo de API;ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask", "Integre a interface a um mecanismo oficial de autenticação da sua stack; teste expiração, logout e acesso negado. Dependência de autenticação isolada não comprova essa integração.", false),
            S("Testes de integração", "Testes de integração", "HTML,React;ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask", "Teste uma operação da interface até a API e persistência de teste, incluindo uma falha; não use banco de produção.", false),
            S("Docker", "Docker", "ASP.NET Core,Spring Boot,APIs Python,FastAPI,Flask;SQL,SQL Server,PostgreSQL,SQLite,MySQL,Entity Framework Core,JPA,SQLAlchemy", "Documente a execução integrada da interface, API e banco de desenvolvimento com containers.", false),
            S("CI/CD", "CI/CD", "Docker;Testes automatizados,Testes de integração", "Execute builds e testes das duas partes em um workflow, sem versionar secrets.", false),
            S("Deploy integrado", "Deploy integrado", "CI/CD;Docker", "Publique interface e API com HTTPS, CORS e configuração por ambiente; registre health check e rollback.", false)),
        T("Dados com Python", "Python;Pandas,NumPy",
            S("Python", "Python", "", ""),
            S("Manipulação de dados", "Pandas,NumPy", "Python", ""),
            S("SQL", "SQL,PostgreSQL,SQLite,MySQL,SQL Server", "Pandas,NumPy", "Carregue um conjunto de dados em SQL e documente consultas de validação.", false),
            S("Testes de dados", "Testes de dados,pytest", "Pandas,NumPy", "Teste valores ausentes, duplicatas e limites em uma transformação de dados.", false),
            S("Pipeline de dados", "Pipeline de dados", "Pandas,NumPy;SQL,PostgreSQL,SQLite", "Organize extração, validação e carga reproduzíveis, registrando entradas e saídas.", false))
    ]).Select(LearningTrackProgression.Expand).ToArray();
}
