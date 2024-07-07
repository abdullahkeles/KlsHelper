using System.Reflection;
using System.Text;

namespace Net2Mermaid;

public static class Class2ClassDiagram
{
    /// <summary>
    /// bu method projede var olan veya referans verilen namespace ler için çalışır.
    /// enttiti lerin yolunu alarak referand edilen namespace de arar. bulduğu class ları ve ilişkilerini mermaid diagrama dönüştürür.
    /// </summary>
    /// <param name="namespaceName">kök namespace</param>
    /// <param name="namespaceEntities">namespace in alt path ini bekler. default value si '.Database.Entities'</param>
    public static string GenerateString(string namespaceName, string namespaceEntities = ".Database.Entities")
    {
        var assembly = Assembly.Load(namespaceName);
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && t.Namespace.StartsWith(namespaceName + ".Database.Entities"))
                .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("---");
        sb.AppendLine($"title : {namespaceName}.Database.Entities UML Diagrams");
        sb.AppendLine("---");
        sb.AppendLine("classDiagram");
        sb.AppendLine("direction RL");

        foreach (var type in types)
        {
            if (type.IsEnum)
            {
                GenerateEnumDiagram(sb, type);
            }
            else
            {
                GenerateClassDiagram(sb, type, types);
            }

            GenerateInterfaceRelations(sb, type);
        }

        GenerateRelationships(sb, types);

        sb.AppendLine("```");
        return sb.ToString();
        //File.WriteAllText(outputPath, sb.ToString());
    }
    /// <summary>
    /// GenerateString metodu ile oluştulan diagram ı .md uzantılı dosya dönüştürür.
    /// dosayını isim fotmatı [{DateTime Long}-{namespaceName}-{namespaceEntities}.md]
    /// </summary>
    /// <param name="outputPath">dosyanın kök dizinini takip eden dosya yolu bekler. null olursa kök dizine kaydeder dosyayı</param>
    /// <param name="namespaceName">kök namespace</param>
    /// <param name="namespaceEntities">namespace in alt path ini bekler. default value si '.Database.Entities'</param>
    public static void GenerateMdFile(string? outputPath, string namespaceName, string namespaceEntities = ".Database.Entities")
    {
        outputPath = outputPath is null ? Path.Combine(Directory.GetCurrentDirectory(), $"{namespaceName}{namespaceEntities}.md") : Path.Combine(Directory.GetCurrentDirectory(), outputPath, $"{namespaceName}{namespaceEntities}.md");
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), outputPath), GenerateString(namespaceName, namespaceEntities));
    }
    private static void GenerateEnumDiagram(StringBuilder sb, Type type)
    {
        sb.AppendLine($"class {type.Name} {{");
        sb.AppendLine("<<enumeration>>");
        foreach (var value in Enum.GetNames(type))
        {
            sb.AppendLine($"  {value}");
        }
        sb.AppendLine("}");
    }
    private static void GenerateInterfaceRelations(StringBuilder sb, Type type)
    {
        if (type.GetInterfaces().Any())
        {
            foreach (var interf in type.GetInterfaces())
            {
                sb.AppendLine($"{interf.Name} <|-- {type.Name} : implements");
            }
        }
    }
    private static void GenerateRelationships(StringBuilder sb, List<Type> types)
    {
        foreach (var type in types)
        {
            foreach (var prop in type.GetProperties())
            {
                if (types.Contains(prop.PropertyType))
                {
                    sb.AppendLine($"{type.Name} --> {prop.PropertyType.Name} : has");
                }
                else if (prop.PropertyType.IsGenericType &&
                         prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    var elementType = prop.PropertyType.GetGenericArguments()[0];
                    if (types.Contains(elementType))
                    {
                        sb.AppendLine($"{type.Name} --> \"*\" {elementType.Name} : has many");
                    }
                }
            }
        }
    }
    private static IEnumerable<MethodInfo> GetAllMethods(Type type)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var methods = new List<MethodInfo>();

        while (type != null && type != typeof(object))
        {
            methods.AddRange(type.GetMethods(flags));
            type = type.BaseType;
        }

        return methods.Distinct();
    }
    private static void GenerateClassDiagram(StringBuilder sb, Type type, List<Type> namespaceTypes)
    {
        sb.AppendLine($"class {type.Name} {{");

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        foreach (var prop in properties)
        {
            string visibility = prop.GetGetMethod(true)?.IsPublic == true ? "+" : "-";
            string propertyType = GetFriendlyTypeName(prop.PropertyType);
            sb.AppendLine($"  {visibility}{propertyType} {prop.Name}");
        }

        var methods = GetAllMethods(type);
        foreach (var method in methods)
        {
            if (!method.IsSpecialName) // Exclude property accessor methods
            {
                string visibility = method.IsPublic ? "+" : "-";
                string returnType = GetFriendlyTypeName(method.ReturnType);
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{GetFriendlyTypeName(p.ParameterType)} {p.Name}"));
                sb.AppendLine($"  {visibility}{method.Name}({parameters}) {returnType}");
            }
        }
        sb.AppendLine("}");

        // Add inheritance relationship
        if (type.BaseType != null && type.BaseType != typeof(object) && namespaceTypes.Contains(type.BaseType))
        {
            sb.AppendLine($"{type.BaseType.Name} <|.. {type.Name} : inherits");
        }
    }
    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments()
                .Select(GetFriendlyTypeName)
                .ToArray();

            var typeName = type.Name.Split('`')[0];
            return $"{typeName}~{string.Join(", ", genericArguments)}~";
        }

        return type.Name;
    }
}