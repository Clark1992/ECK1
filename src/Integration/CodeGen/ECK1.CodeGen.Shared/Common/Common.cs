using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ECK1.CodeGen.Shared.Common;

internal static class Common
{
    internal static readonly string InterfaceName = "ECK1.IntegrationContracts.Abstractions.IIntegrationMessage";

    internal static string MakeSafeFileName(string prefix, INamedTypeSymbol symbol) => $"{prefix}_{MakeSafeIdentifier(symbol)}.g.cs";

    internal static bool IsSystemLike(ITypeSymbol s) 
    {
        if (s == null) return true; 
        if (s.SpecialType != SpecialType.None) return true; 
        var ns = s.ContainingNamespace?.ToDisplayString() ?? ""; 
    
        if (ns.StartsWith("System", StringComparison.Ordinal)) return true; 
        return false;
    }

    internal static bool IsPrimitiveLike(ITypeSymbol s)
    {
        if (s == null) return true;
        if (s.SpecialType != SpecialType.None) return true;
        if (s.TypeKind == TypeKind.Enum) return true;
        var ns = s.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System", StringComparison.Ordinal) && !ns.StartsWith("System.Collections")) return true;
        return false;
    }

    internal static bool IsListType(ITypeSymbol type, out ITypeSymbol elementType)
    {
        elementType = null;
        if (type is INamedTypeSymbol named && named.IsGenericType && named.Name == "List" && named.TypeArguments.Length == 1)
        {
            elementType = named.TypeArguments[0];
            return true;
        }
        return false;
    }

    internal static string MakeSafeIdentifier(ITypeSymbol s)
    {
        var txt = s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return txt.Replace("global::", "")
                  .Replace("<", "_")
                  .Replace(">", "_")
                  .Replace(".", "_")
                  .Replace(",", "_")
                  .Replace(" ", "")
                  .Replace(":", "_");
    }

    internal static void CollectAllTypesRecursive(INamedTypeSymbol type, HashSet<INamedTypeSymbol> acc)
    {
        if (type == null || acc.Contains(type) || IsSystemLike(type)) return;
        acc.Add(type);

        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
        {
            var t = prop.Type;

            if (t is IArrayTypeSymbol arr)
            {
                if (arr.ElementType is INamedTypeSymbol e) CollectAllTypesRecursive(e, acc);
                continue;
            }

            if (t is INamedTypeSymbol named)
            {
                if (named.IsGenericType)
                {
                    foreach (var ta in named.TypeArguments.OfType<INamedTypeSymbol>())
                    {
                        if (!IsSystemLike(ta)) CollectAllTypesRecursive(ta, acc);
                    }
                }
                if (!IsSystemLike(named)) CollectAllTypesRecursive(named, acc);
                continue;
            }

            if (t is INamedTypeSymbol pNamed && !IsSystemLike(pNamed))
                CollectAllTypesRecursive(pNamed, acc);
        }
    }

    internal static (string PropName, ITypeSymbol Type, bool IsPrimitive, bool IsList) GetPropertyData(IPropertySymbol property)
    {
        var type = property.Type;
        var isList = IsListType(type, out var elementType);
        if (isList) type = elementType;

        var isPrimitive = IsPrimitiveLike(type);

        return (property.Name, type, isPrimitive, isList);
    }

    internal static IncrementalValueProvider<IEnumerable<INamedTypeSymbol>> GetTypesForCodeGeneration(
        IncrementalGeneratorInitializationContext context,
        string attribute) => context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is PropertyDeclarationSyntax prop &&
                    prop.AttributeLists.Count > 0,
                transform: (ctx, _) =>
                {
                    var prop = (PropertyDeclarationSyntax)ctx.Node;

                    foreach (var attrList in prop.AttributeLists)
                        foreach (var attr in attrList.Attributes)
                        {
                            var name = attr.Name.ToString();
                            if (name.Equals(attribute) || name.Equals($"{attribute}Attribute"))
                            {
                                // read typeof(...)
                                if (attr.ArgumentList?.Arguments.Count == 1)
                                {
                                    var arg = attr.ArgumentList.Arguments[0];

                                    if (arg.Expression is TypeOfExpressionSyntax typeofExpr)
                                    {
                                        var typeSyntax = typeofExpr.Type;
                                        var semanticType = ctx.SemanticModel.GetTypeInfo(typeSyntax).Type;

                                        if (semanticType is INamedTypeSymbol namedType)
                                            return namedType;
                                    }
                                }
                            }
                        }

                    return null;
                })
            .Where(t => t is not null)
            .Collect()
            .Select((arr, _) => arr.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>().ToList().AsEnumerable());

    internal static IncrementalValueProvider<IEnumerable<INamedTypeSymbol>> GetTypesForCodeGenerationByInterface(
        IncrementalGeneratorInitializationContext context,
        string interfaceName)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax cls && cls.BaseList != null,
                transform: (ctx, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)ctx.Node;

                    var model = ctx.SemanticModel;
                    var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (classSymbol is null)
                        return null;

                    foreach (var iface in classSymbol.AllInterfaces)
                    {
                        if (iface.Name.Equals(interfaceName, StringComparison.Ordinal) ||
                            iface.ToDisplayString().Equals(interfaceName, StringComparison.Ordinal))
                        {
                            return classSymbol;
                        }
                    }

                    return null;
                })
            .Where(t => t is not null)
            .Collect()
            .Select((arr, _) =>
                arr.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>().ToList().AsEnumerable()
            );
    }

    // Scans current compilation and referenced assemblies for types implementing the given interface.
    internal static IncrementalValueProvider<IEnumerable<INamedTypeSymbol>> GetTypesForCodeGenWithDepsByInterface(
        IncrementalGeneratorInitializationContext context,
        string interfaceName)
    {
        return context.CompilationProvider
            .Select((comp, _) =>
            {
                var collected = new List<INamedTypeSymbol>();

                void AddNestedTypes(INamedTypeSymbol t)
                {
                    foreach (var nested in t.GetTypeMembers())
                    {
                        collected.Add(nested);
                        AddNestedTypes(nested);
                    }
                }

                void WalkNamespace(INamespaceSymbol ns)
                {
                    if (ns == null) return;
                    foreach (var t in ns.GetTypeMembers())
                    {
                        collected.Add(t);
                        AddNestedTypes(t);
                    }

                    foreach (var child in ns.GetNamespaceMembers())
                        WalkNamespace(child);
                }

                // current assembly
                WalkNamespace(comp.Assembly.GlobalNamespace);

                // referenced assemblies (iterate metadata references and resolve to assembly symbols)
                foreach (var mr in comp.References)
                {
                    var asm = comp.GetAssemblyOrModuleSymbol(mr) as IAssemblySymbol;
                    if (asm != null)
                        WalkNamespace(asm.GlobalNamespace);
                }

                var result = collected
                    .Where(s => s != null && s.AllInterfaces.Any(i =>
                        i.ToDisplayString().Equals(interfaceName, StringComparison.Ordinal)))
                    .Distinct(SymbolEqualityComparer.Default)
                    .Cast<INamedTypeSymbol>();

                return result;
            });
    }

    // Scans current compilation and referenced assemblies for types deriving from the given base class.
    internal static IncrementalValueProvider<IEnumerable<INamedTypeSymbol>> GetTypesForCodeGenWithDepsByBaseType(
        IncrementalGeneratorInitializationContext context,
        string baseTypeMetadataName)
    {
        return context.CompilationProvider
            .Select((comp, _) =>
            {
                var baseType = comp.GetTypeByMetadataName(baseTypeMetadataName);
                if (baseType is null)
                    return Enumerable.Empty<INamedTypeSymbol>();

                var collected = new List<INamedTypeSymbol>();

                void AddNestedTypes(INamedTypeSymbol t)
                {
                    foreach (var nested in t.GetTypeMembers())
                    {
                        collected.Add(nested);
                        AddNestedTypes(nested);
                    }
                }

                void WalkNamespace(INamespaceSymbol ns)
                {
                    if (ns == null) return;
                    foreach (var t in ns.GetTypeMembers())
                    {
                        collected.Add(t);
                        AddNestedTypes(t);
                    }

                    foreach (var child in ns.GetNamespaceMembers())
                        WalkNamespace(child);
                }

                static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
                {
                    for (INamedTypeSymbol cur = type; cur != null; cur = cur.BaseType)
                    {
                        if (SymbolEqualityComparer.Default.Equals(cur, baseType))
                            return true;
                    }
                    return false;
                }

                WalkNamespace(comp.Assembly.GlobalNamespace);

                foreach (var mr in comp.References)
                {
                    var asm = comp.GetAssemblyOrModuleSymbol(mr) as IAssemblySymbol;
                    if (asm != null)
                        WalkNamespace(asm.GlobalNamespace);
                }

                return collected
                    .Where(s => s != null && IsDerivedFrom(s, baseType))
                    .Distinct(SymbolEqualityComparer.Default)
                    .Cast<INamedTypeSymbol>();
            });
    }

    internal static IncrementalValueProvider<IEnumerable<string>> GetAll(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax cls,
                transform: (ctx, _) =>
                {
                    var classDecl = (ClassDeclarationSyntax)ctx.Node;

                    var model = ctx.SemanticModel;
                    var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                    if (classSymbol is null)
                        return null;


                    return classSymbol.Name;
                })
            .Where(t => t is not null)
            .Collect()
            .Select((arr, _) =>
                arr.Distinct().OfType<string>()
            );
    }
}
