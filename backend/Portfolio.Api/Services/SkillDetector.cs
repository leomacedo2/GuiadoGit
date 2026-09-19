using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Portfolio.Api.DTOs;
using Portfolio.Api.Models;

namespace Portfolio.Api.Services;

// Rules inspect paths and structured dependency declarations, never source code lines.
public sealed class SkillDetector
{
    public static bool IsRelevantPath(string path) => !path.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
        && !("/" + path).Contains("/wwwroot/lib/", StringComparison.OrdinalIgnoreCase)
        && !path.Split('/').Any(segment =>
        new[] { "node_modules", "vendor", ".git", "bin", "obj", "dist", "build", ".venv", "venv" }.Contains(segment, StringComparer.OrdinalIgnoreCase));

    public static bool IsManifest(string path) => Path.GetFileName(path) is "package.json" or "pom.xml" or "requirements.txt" || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

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
            if (file == "dockerfile" || file.StartsWith("dockerfile.")) Add("Docker", "DevOps", $"Arquivo {path}.");
            if (lower.StartsWith(".github/workflows/") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml"))) Add("CI/CD", "DevOps", $"Workflow do GitHub Actions: {path}.");
            if (lower.Split('/').Any(p => p is "test" or "tests" or "__tests__") || file.StartsWith("test_") || file.Contains(".test.") || file.Contains(".spec.")) Add("Testes automatizados", "Qualidade", $"Arquivo em estrutura de testes: {path} (presença não comprova execução).");
        }
        foreach (var (path, content) in inspection.Manifests)
        {
            if (!IsRelevantPath(path)) continue;
            foreach (var dependency in Dependencies(path, content))
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

    private static IEnumerable<string> Dependencies(string path, string content)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (path.EndsWith("package.json"))
        {
            using var json = JsonDocument.Parse(content);
            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies", "optionalDependencies" })
                if (json.RootElement.TryGetProperty(section, out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
                    foreach (var entry in dependencies.EnumerateObject()) result.Add(entry.Name.ToLowerInvariant());
        }
        else if (path.EndsWith("pom.xml") || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var xml = XDocument.Load(reader);
            if (xml.Root?.Attribute("Sdk")?.Value == "Microsoft.NET.Sdk.Web") result.Add("microsoft.net.sdk.web");
            foreach (var dependency in xml.Descendants().Where(x => x.Name.LocalName == "dependency" && !x.Ancestors().Any(a => a.Name.LocalName == "dependencyManagement")))
            {
                string? Value(string name) => dependency.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim();
                result.Add($"{Value("groupId")}:{Value("artifactId")}".ToLowerInvariant());
            }
            foreach (var reference in xml.Descendants().Where(x => x.Name.LocalName == "PackageReference"))
                if (reference.Attribute("Include")?.Value is { } name) result.Add(name.ToLowerInvariant());
        }
        else if (path.EndsWith("requirements.txt"))
        {
            foreach (var line in content.Split('\n'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line.Trim(), @"^([A-Za-z0-9][A-Za-z0-9_.-]*)(?:\[.*?\])?\s*(?:[<>=!~;#]|$)");
                if (match.Success) result.Add(match.Groups[1].Value.ToLowerInvariant());
            }
        }
        return result;
    }
}
