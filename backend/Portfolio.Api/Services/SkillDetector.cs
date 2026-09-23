using Portfolio.Api.DTOs;
using Portfolio.Api.Models;

namespace Portfolio.Api.Services;

// Rules inspect paths and structured dependency declarations, never source code lines.
public sealed class SkillDetector
{
    public static bool IsRelevantPath(string path) => !path.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
        && !("/" + path).Contains("/wwwroot/lib/", StringComparison.OrdinalIgnoreCase)
        && !path.Split('/').Any(segment =>
        new[] { "node_modules", "vendor", ".git", "bin", "obj", "dist", "build", ".venv", "venv", "third_party", "third-party", "bower_components", ".next", ".nuxt", "coverage", "__pycache__" }.Contains(segment, StringComparer.OrdinalIgnoreCase));

    public static bool IsManifest(string path) => ManifestSelector.Family(path) is not null;

    public IReadOnlyList<(string Name, string Category, string Reason)> Detect(RepositoryDto repo, RepositoryInspection? inspection)
    {
        var found = new List<(string Name, string Category, string Reason)>();
        void Add(string name, string category, string reason) => found.Add((name, category, reason));
        if (!string.IsNullOrWhiteSpace(repo.Language)) Add(repo.Language, "Linguagens", $"Linguagem principal informada pelo GitHub: {repo.Language}.");
        if (inspection is null) return found;
        foreach (var path in inspection.Paths.Where(IsRelevantPath))
        {
            var lower = path.ToLowerInvariant();
            var file = Path.GetFileName(lower);
            var language = Path.GetExtension(lower) switch { ".html" or ".htm" => "HTML", ".css" => "CSS", ".py" => "Python", ".java" => "Java", ".js" or ".jsx" => "JavaScript", ".ts" or ".tsx" => "TypeScript", ".cs" => "C#", ".sql" => "SQL", _ => null };
            if (language is not null) Add(language, "Linguagens", $"Arquivo {path}.");
            if (file == "pom.xml") { Add("Maven", "Ferramentas", $"Manifesto {path}."); Add("Java", "Linguagens", $"Manifesto Maven {path}."); }
            if (file is "build.gradle" or "build.gradle.kts") Add("Gradle", "Ferramentas", $"Manifesto {path}; a linguagem depende dos demais arquivos.");
            if (file.StartsWith("vite.config.")) Add("Vite", "Ferramentas", $"Configuração {path}.");
            if (file.StartsWith("tsconfig") && file.EndsWith(".json")) Add("TypeScript", "Linguagens", $"Configuração TypeScript {path}.");
            if (file.EndsWith(".sln") || file.EndsWith(".slnx")) Add(".NET", "Ferramentas", $"Solução {path}; não identifica a linguagem sozinha.");
            if (file == "dockerfile" || file.StartsWith("dockerfile.")) Add("Docker", "DevOps", $"Arquivo {path}.");
            if (file is "docker-compose.yml" or "docker-compose.yaml" or "compose.yml" or "compose.yaml") Add("Docker", "DevOps", $"Configuração Compose {path}.");
            if (lower.StartsWith(".github/workflows/") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml"))) Add("CI/CD", "DevOps", $"Workflow do GitHub Actions: {path}.");
            if (lower.Split('/').Any(p => p is "test" or "tests" or "__tests__") || file.StartsWith("test_") || file.Contains(".test.") || file.Contains(".spec.")) Add("Testes automatizados", "Qualidade", $"Arquivo em estrutura de testes: {path} (presença não comprova execução).");
        }
        foreach (var (path, content) in inspection.Manifests)
        {
            if (!IsRelevantPath(path)) continue;
            var dependencies = ManifestDependencies.Read(path, content).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in dependencies)
            {
                var reason = $"Dependência {dependency} declarada em {path}.";
                switch (dependency)
                {
                    case "microsoft.net.sdk.web": Add("ASP.NET Core", "Backend", $"SDK Microsoft.NET.Sdk.Web declarado em {path}; não comprova Web API."); break;
                    case "react-router": case "react-router-dom": Add("React Router", "Frontend", reason); break;
                    case "axios": Add("Consumo de API", "Integração", reason + " Indício de cliente HTTP, não comprova chamadas executadas."); break;
                    case "expo-router": Add("Expo Router", "Mobile", reason); break;
                    case "@react-navigation/native": Add("React Navigation", "Mobile", reason); break;
                    case "@react-native-async-storage/async-storage": Add("AsyncStorage", "Dados", reason); break;
                    case "expo-sqlite": Add("SQLite", "Dados", reason); break;
                    case "org.springframework.boot:spring-boot-starter-data-jpa": case "jakarta.persistence:jakarta.persistence-api": case "javax.persistence:javax.persistence-api": Add("JPA", "Dados", reason); break;
                    case "react": Add("React", "Frontend", reason); break;
                    case "typescript": Add("TypeScript", "Linguagens", reason); break;
                    case "vite": Add("Vite", "Ferramentas", reason); break;
                    case "redux": case "@reduxjs/toolkit": case "zustand": case "mobx": case "recoil": Add("Gerenciamento de estado", "Frontend", reason); break;
                    case "jest": case "vitest":
                        if (dependencies.Contains("react-native") || dependencies.Contains("expo")) Add("Testes mobile", "Qualidade", reason);
                        else if (dependencies.Contains("react")) Add("Testes de frontend", "Qualidade", reason);
                        break;
                    case "@testing-library/react": Add("Testes de frontend", "Qualidade", reason + " Ferramenta declarada; não comprova execução."); break;
                    case "@testing-library/react-native": Add("Testes mobile", "Qualidade", reason); break;
                    case "eslint-plugin-jsx-a11y": case "@axe-core/react": case "axe-core": case "jest-axe": Add("Acessibilidade", "Qualidade", reason + " Ferramenta declarada; não certifica acessibilidade."); break;
                    case "web-vitals": case "@lhci/cli": Add("Performance web", "Qualidade", reason + " Ferramenta de medição; não comprova desempenho."); break;
                    case "microsoft.aspnetcore.openapi": case "swashbuckle.aspnetcore": Add("Web API", "Backend", reason + " Indício de documentação HTTP; não valida endpoints."); break;
                    case "microsoft.aspnetcore.identity.entityframeworkcore": case "microsoft.aspnetcore.authentication.jwtbearer": case "passport": Add("Autenticação", "Backend", reason + " Não comprova integração frontend/backend."); break;
                    case "microsoft.aspnetcore.mvc.testing": case "testcontainers": case "org.testcontainers:junit-jupiter": Add("Testes de integração", "Qualidade", reason); break;
                    case "pytest": Add("pytest", "Qualidade", reason); break;
                    case "xunit": Add("xUnit", "Qualidade", reason); break;
                    case "nunit": Add("NUnit", "Qualidade", reason); break;
                    case "pandas": Add("Pandas", "Dados", reason); break;
                    case "numpy": Add("NumPy", "Dados", reason); break;
                    case "laravel/framework": Add("Laravel", "Backend", reason); break;
                    case "rails": Add("Ruby on Rails", "Backend", reason); break;
                    case "phpunit/phpunit": case "rspec": Add("Testes automatizados", "Qualidade", reason); break;
                    case "expo": Add("Expo", "Mobile", reason); Add("React Native", "Mobile", reason); break;
                    case "react-native": Add("React Native", "Mobile", reason); break;
                    case "org.springframework.boot:spring-boot-starter-web":
                    case "org.springframework.boot:spring-boot-starter-webflux": Add("REST API", "Backend", reason + " Indício de infraestrutura web, não valida endpoints REST."); break;
                    case "fastapi": case "flask": Add("APIs Python", "Backend", reason); break;
                    case "sqlalchemy": Add("SQLAlchemy", "Dados", reason); break;
                    case "psycopg2": case "psycopg2-binary": case "npgsql": Add("PostgreSQL", "Dados", reason); break;
                    case "sqlite3": case "microsoft.data.sqlite": Add("SQLite", "Dados", reason); break;
                    case "microsoft.data.sqlclient": case "system.data.sqlclient": case "com.microsoft.sqlserver:mssql-jdbc": case "mssql": Add("SQL Server", "Dados", reason); break;
                }
                if (dependency.StartsWith("org.springframework.boot:")) Add("Spring Boot", "Backend", reason);
                if (dependency.StartsWith("testcontainers.")) Add("Testes de integração", "Qualidade", reason);
                if (dependency.StartsWith("junit:") || dependency.StartsWith("org.junit.jupiter:")) { Add("JUnit", "Qualidade", reason); Add("Testes automatizados", "Qualidade", reason); }
                if (new[] { "pytest", "jest", "vitest", "mocha", "@playwright/test", "xunit", "nunit", "mstest.testframework", "org.springframework.boot:spring-boot-starter-test" }.Contains(dependency)) Add("Testes automatizados", "Qualidade", reason);
                if (dependency == "microsoft.entityframeworkcore" || dependency.StartsWith("microsoft.entityframeworkcore.") || dependency == "npgsql.entityframeworkcore.postgresql" || dependency == "pomelo.entityframeworkcore.mysql") Add("Entity Framework Core", "Dados", reason);
                if (dependency == "microsoft.entityframeworkcore.sqlserver") Add("SQL Server", "Dados", reason);
                if (dependency == "microsoft.entityframeworkcore.sqlite") Add("SQLite", "Dados", reason);
                if (dependency == "npgsql.entityframeworkcore.postgresql") Add("PostgreSQL", "Dados", reason);
                if (dependency == "pomelo.entityframeworkcore.mysql") Add("MySQL", "Dados", reason);
            }
        }
        // Keep representative reasons, rather than thousands of identical file-extension hits.
        return found.GroupBy(x => (x.Name, x.Category)).SelectMany(group => group.Distinct().Take(3)).ToList();
    }

}
