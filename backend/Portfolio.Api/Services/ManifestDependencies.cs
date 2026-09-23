using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Tomlyn;
using Tomlyn.Model;

namespace Portfolio.Api.Services;

// Only dependency declarations; never execute build scripts or infer packages from descriptions.
public static class ManifestDependencies
{
    public static IEnumerable<string> Read(string path, string content)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string name) => result.Add(name.ToLowerInvariant());
        var file = Path.GetFileName(path).ToLowerInvariant();
        if (file is "package.json" or "package-lock.json" or "composer.json")
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            if (file == "package-lock.json")
            {
                // Lock v2/v3: only the root declaration, never packages/node_modules or transitive deps.
                if (!root.TryGetProperty("packages", out var packages) || !packages.TryGetProperty("", out root))
                    throw new FormatException("Lockfile sem declaração de dependências diretas na raiz.");
            }
            foreach (var section in file == "composer.json" ? new[] { "require", "require-dev" } : new[] { "dependencies", "devDependencies", "peerDependencies", "optionalDependencies" })
                if (root.TryGetProperty(section, out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
                    foreach (var entry in dependencies.EnumerateObject()) Add(entry.Name);
        }
        else if (file == "pom.xml" || file.EndsWith(".csproj"))
        {
            using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var xml = XDocument.Load(reader);
            if (xml.Root?.Attribute("Sdk")?.Value == "Microsoft.NET.Sdk.Web") Add("microsoft.net.sdk.web");
            foreach (var dependency in xml.Descendants().Where(x => x.Name.LocalName == "dependency" && !x.Ancestors().Any(a => a.Name.LocalName == "dependencyManagement")))
            {
                string? Value(string name) => dependency.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim();
                Add($"{Value("groupId")}:{Value("artifactId")}");
            }
            foreach (var reference in xml.Descendants().Where(x => x.Name.LocalName == "PackageReference"))
                if (reference.Attribute("Include")?.Value is { } name) Add(name);
        }
        else if (file == "requirements.txt")
        {
            foreach (var line in content.Split('\n')) AddRequirement(line.Trim(), Add);
        }
        else if (file is "pyproject.toml" or "pipfile")
        {
            TomlTable root;
            try { root = TomlSerializer.Deserialize<TomlTable>(content, new TomlSerializerOptions { MaxDepth = 32 })!; }
            catch (TomlException ex) { throw new FormatException("Manifesto TOML inválido.", ex); }
            if (file == "pipfile")
            {
                foreach (var section in new[] { "packages", "dev-packages" })
                    if (At(root, section) is TomlTable table) foreach (var name in table.Keys) Add(name);
            }
            else
            {
                void Requirements(object? values)
                {
                    if (values is TomlArray array) foreach (var value in array.OfType<string>()) AddRequirement(value, Add);
                }
                Requirements(At(root, "project", "dependencies"));
                if (At(root, "project", "optional-dependencies") is TomlTable extras)
                    foreach (var values in extras.Values) Requirements(values);
                foreach (var section in new[] { "dependencies", "dev-dependencies" })
                    if (At(root, "tool", "poetry", section) is TomlTable table)
                        foreach (var name in table.Keys.Where(n => n != "python")) Add(name);
                if (At(root, "tool", "poetry", "group") is TomlTable groups)
                    foreach (var group in groups.Values.OfType<TomlTable>())
                        if (At(group, "dependencies") is TomlTable table) foreach (var name in table.Keys) Add(name);
                if (At(root, "dependency-groups") is TomlTable dependencyGroups)
                    foreach (var values in dependencyGroups.Values) Requirements(values);
            }
        }
        else if (file is "build.gradle" or "build.gradle.kts")
        {
            var declarations = Regex.Replace(content, @"/\*[\s\S]*?\*/|//[^\r\n]*", "", RegexOptions.None, TimeSpan.FromSeconds(1));
            foreach (Match match in Regex.Matches(declarations,
                "(?m)^\\s*(?:implementation|api|compileOnly|runtimeOnly|testImplementation|testRuntimeOnly|annotationProcessor)\\s*(?:\\(\\s*)?[\"']([A-Za-z0-9_.-]+:[A-Za-z0-9_.-]+)(?::[^\"']*)?[\"']", RegexOptions.None, TimeSpan.FromSeconds(1)))
                Add(match.Groups[1].Value);
        }
        else if (file == "gemfile")
        {
            foreach (Match match in Regex.Matches(content, "(?m)^\\s*gem\\s+[\"']([A-Za-z0-9_.-]+)[\"']", RegexOptions.None, TimeSpan.FromSeconds(1)))
                Add(match.Groups[1].Value);
        }
        return result;
    }

    private static object? At(TomlTable root, params string[] keys)
    {
        object? value = root;
        foreach (var key in keys)
        {
            if (value is not TomlTable table || !table.TryGetValue(key, out value)) return null;
        }
        return value;
    }
    private static void AddRequirement(string declaration, Action<string> add)
    {
        var match = Regex.Match(declaration, @"^([A-Za-z0-9][A-Za-z0-9_.-]*)(?:\[[^\]]*\])?\s*(?:[<>=!~;#@]|$)", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (match.Success) add(match.Groups[1].Value);
    }
}
