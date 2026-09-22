// Copyright (c) 2025 MATRIX-feather. Licensed under the MIT Licence.
// Copyright (c) 2025 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader.Utils;

public static class HandlerExtension
{
    public const BindingFlags INSTANCE_FLAG = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.GetField;

    private static FieldInfo? findFieldInstanceInBaseType(Type baseType, Type type)
    {
        var field = baseType.GetFields() // INSTANCE_FLAG
                            .FirstOrDefault(f => f.FieldType == type);

        if (field == null && baseType.BaseType != null)
            field = findFieldInstanceInBaseType(baseType.BaseType, type);

        return field;
    }

    public static FieldInfo? FindFieldInstance(this object obj, Type type)
    {
        return findFieldInstanceInBaseType(obj.GetType(), type);
    }

    public static object? FindInstance(this object obj, Type type)
    {
        var field = obj.FindFieldInstance(type);
        return field?.GetValue(obj);
    }

    private static FieldInfo? findFieldInstanceByName(Type baseType, string name)
    {
        var field = baseType.GetField(name, INSTANCE_FLAG); // INSTANCE_FLAG

        if (field == null && baseType.BaseType != null)
            field = findFieldInstanceByName(baseType.BaseType, name);

        return field;
    }

    public static FieldInfo? FindFieldInstance(this object obj, string name)
    {
        return findFieldInstanceByName(obj.GetType(), name);
    }

    public static object? FindInstance(this object obj, string name)
    {
        var field = obj.FindFieldInstance(name);
        return field?.GetValue(obj);
    }

    private static MethodInfo? findMethodByName(Type baseType, string name, Type returnType)
    {
        var method = baseType.GetMethod(name, INSTANCE_FLAG);
        if (method == null && baseType.BaseType != null)
            method = findMethodByName(baseType.BaseType, name, returnType);
        return method?.IsGenericMethodDefinition ?? false ? method?.MakeGenericMethod(returnType) : method;
    }

    public static Func<object?[], object?> FindMethod(this object obj, string name, Type returnType)
    {
        return parameters => findMethodByName(obj.GetType(), name, returnType)?.Invoke(obj, parameters);
    }

    private static Type? findTypeByName(Type baseType, string typeName)
    {
        if (baseType.Name == typeName || baseType.Name.Split('`')[0] == typeName)
            return baseType;

        var nestedTypes = baseType.GetNestedTypes(INSTANCE_FLAG);
        foreach (var nested in nestedTypes)
        {
            var ret = findTypeByName(nested, typeName);
            if (ret is not null)
                return ret;
        }
        return null;
    }

    public static Type? FindType(this object obj, string typeName)
    {
        return findTypeByName(obj.GetType(), typeName);
    }

    public static Type? FindType(this Type baseType, string typeName)
    {
        return findTypeByName(baseType, typeName);
    }

    public const BindingFlags PROPERTY_FLAG = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.DeclaredOnly;

    private static PropertyInfo? findPropertyByName(Type? type, string name)
    {
        var prop = type?.GetProperty(name, PROPERTY_FLAG);
        if (prop != null)
            return prop;
        return findPropertyByName(type?.BaseType, name);
    }

    public static object? GetPropertyValue(this object obj, string propertyName)
    {
        var prop = findPropertyByName(obj.GetType(), propertyName) ?? throw new MissingMemberException(propertyName);
        var getter = prop.GetGetMethod(nonPublic: true) ?? throw new InvalidOperationException($"{propertyName} has no getter");
        return getter.Invoke(obj, []);
    }

    public static void SetPropertyValue(this object obj, string propertyName, object? value)
    {
        var prop = findPropertyByName(obj.GetType(), propertyName) ?? throw new MissingMemberException(propertyName);
        var setter = prop.GetSetMethod(nonPublic: true) ?? throw new InvalidOperationException($"{propertyName} has no setter");
        setter.Invoke(obj, [value]);
    }
}
