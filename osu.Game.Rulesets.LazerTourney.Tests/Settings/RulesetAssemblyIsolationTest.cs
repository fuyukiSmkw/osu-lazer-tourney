// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace osu.Game.Rulesets.LazerTourney.Tests.Settings
{
    /// <summary>
    /// Guards the dev-build ruleset load: the CLR resolves signature-level references
    /// (base types, interfaces, fields, properties, method signatures including compiler
    /// generated closure/iterator/async state machines, attributes) when RulesetStore
    /// scans our assembly, but method-body references only on first use. No load-triggering
    /// position may name the official ruleset assemblies, or the whole plugin fails to
    /// load when they resolve late. Official ruleset types may only appear inside method
    /// bodies, behind the AreRulesetConfigsAvailable gate.
    /// </summary>
    [TestFixture]
    public class RulesetAssemblyIsolationTest
    {
        private static readonly string[] rulesetAssemblyNames =
        {
            "osu.Game.Rulesets.Osu",
            "osu.Game.Rulesets.Taiko",
            "osu.Game.Rulesets.Mania",
        };

        private static bool isRulesetAssembly(Assembly? assembly)
            => assembly != null && rulesetAssemblyNames.Contains(assembly.GetName().Name);

        private static bool involvesRulesetAssembly(Type? type)
        {
            if (type == null)
                return false;

            if (isRulesetAssembly(type.Assembly))
                return true;

            return type.IsGenericType && type.GetGenericArguments().Any(involvesRulesetAssembly);
        }

        private static void checkAttribute(List<string> offenders, string owner, CustomAttributeData attribute)
        {
            if (involvesRulesetAssembly(attribute.AttributeType))
                offenders.Add($"{owner} has attribute {attribute.AttributeType}");

            foreach (var argument in attribute.ConstructorArguments.Concat(attribute.NamedArguments.Select(a => a.TypedValue)))
                checkAttributeValue(offenders, owner, argument);
        }

        private static void checkAttributeValue(List<string> offenders, string owner, CustomAttributeTypedArgument argument)
        {
            if (involvesRulesetAssembly(argument.ArgumentType))
                offenders.Add($"{owner} has attribute argument of type {argument.ArgumentType}");

            if (argument.Value is Type valueType && involvesRulesetAssembly(valueType))
                offenders.Add($"{owner} has attribute argument typeof({valueType})");

            if (argument.Value is System.Collections.IEnumerable values && argument.Value is not string)
            {
                foreach (object? value in values)
                {
                    if (value is Type elementType && involvesRulesetAssembly(elementType))
                        offenders.Add($"{owner} has attribute argument typeof({elementType})");
                }
            }
        }

        [Test]
        public void PluginTypesDoNotExposeRulesetAssemblyTypes()
        {
            // The test run has the ruleset assemblies present, so every reference below
            // resolves; the assertion is purely about WHERE references appear.
            Assembly plugin = typeof(Tournament.TournamentGameBase).Assembly;

            var offenders = new List<string>();

            foreach (Type type in plugin.GetTypes())
            {
                string name = type.FullName ?? type.Name;

                for (Type? baseType = type.BaseType; baseType != null && baseType != typeof(object) && baseType != typeof(ValueType); baseType = baseType.BaseType)
                {
                    if (involvesRulesetAssembly(baseType))
                        offenders.Add($"{name} derives from {baseType}");
                }

                foreach (Type iface in type.GetInterfaces())
                {
                    if (involvesRulesetAssembly(iface))
                        offenders.Add($"{name} implements {iface}");
                }

                const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

                foreach (FieldInfo field in type.GetFields(members))
                {
                    if (involvesRulesetAssembly(field.FieldType))
                        offenders.Add($"{name}.{field.Name} : {field.FieldType}");
                }

                foreach (PropertyInfo property in type.GetProperties(members))
                {
                    if (involvesRulesetAssembly(property.PropertyType))
                        offenders.Add($"{name}.{property.Name} : {property.PropertyType}");
                }

                foreach (MethodInfo method in type.GetMethods(members))
                {
                    if (involvesRulesetAssembly(method.ReturnType))
                        offenders.Add($"{name}.{method.Name}() : {method.ReturnType}");

                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        if (involvesRulesetAssembly(parameter.ParameterType))
                            offenders.Add($"{name}.{method.Name}({parameter.Name} : {parameter.ParameterType})");
                    }
                }

                foreach (CustomAttributeData attribute in type.GetCustomAttributesData())
                    checkAttribute(offenders, name, attribute);
            }

            Assert.That(offenders, Is.Empty, () => string.Join("\n", offenders));
        }
    }
}
