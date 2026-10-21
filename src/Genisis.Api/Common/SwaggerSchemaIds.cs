namespace Genisis.Api.Common;

/// <summary>
/// Swagger schema ids that stay unique across screens: the namespace after <c>Genisis.Api.</c> plus the type name, with generic
/// arguments spelled out (for example <c>Case.Mma.MmaProcedureRequest</c>, <c>ListOfMma.MmaFee</c>). Swashbuckle's default id is the
/// bare class name, which fails the whole document as soon as two screens use the same class name.
/// </summary>
public static class SwaggerSchemaIds
{
    private const string RootNamespace = "Genisis.Api.";

    /// <summary>The schema id of <paramref name="type"/>.</summary>
    public static string For(Type type)
    {
        var name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        if (type.IsGenericType) name += "Of" + string.Join("And", type.GetGenericArguments().Select(For));
        if (type.IsNested && type.DeclaringType is { } outer) return For(outer) + "." + name;
        var ns = type.Namespace ?? "";
        return ns.StartsWith(RootNamespace, StringComparison.Ordinal) ? ns[RootNamespace.Length..] + "." + name : name;
    }
}
