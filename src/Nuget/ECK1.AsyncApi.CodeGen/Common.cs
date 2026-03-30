using Microsoft.CodeAnalysis;

namespace ECK1.AsyncApi.CodeGen;

internal static class Common
{
    internal static IncrementalValueProvider<IEnumerable<INamedTypeSymbol>> GetTypesWithAttributeWithDeps(
        IncrementalGeneratorInitializationContext context,
        string attributeMetadataName)
    {
        return context.CompilationProvider
            .Select((comp, _) =>
            {
                var attributeType = comp.GetTypeByMetadataName(attributeMetadataName);
                if (attributeType is null)
                    return [];

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

                static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType)
                {
                    foreach (var attr in type.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType))
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
                    .Where(t => t != null && HasAttribute(t, attributeType))
                    .Distinct(SymbolEqualityComparer.Default)
                    .Cast<INamedTypeSymbol>();
            });
    }
}