// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier
{
    /// <summary>
    /// Thrown when a ModMult expression fails at evaluation time (e.g. a type mismatch).
    /// Evaluation entry points catch this and fall back to x1, so it should never reach gameplay code.
    /// </summary>
    public class ModMultRuntimeException : Exception
    {
        public ModMultRuntimeException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// A compiled ModMult expression node.
    /// </summary>
    public abstract class ModMultExpression
    {
        public abstract ModMultValue Evaluate(ModMultContext context);

        /// <summary>
        /// Collects referenced mod acronyms (upper-case) and (mod, setting) pairs for validation.
        /// </summary>
        public virtual void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
        {
        }

        protected static double requireNumber(ModMultValue value, string what)
        {
            if (value.IsNull)
                throw new ModMultRuntimeException($"{what} is null (the mod or setting is not active). Use \"??\" to provide a fallback.");
            if (!value.IsNumber)
                throw new ModMultRuntimeException($"{what} is {describe(value)}, but a number is required here.");
            return value.Number;
        }

        protected static string describe(ModMultValue value) => value.Type switch
        {
            ModMultValue.ValueType.Number => "a number",
            ModMultValue.ValueType.Boolean => "a boolean",
            ModMultValue.ValueType.Text => "a text",
            _ => "null",
        };
    }

    public sealed class ModMultLiteral : ModMultExpression
    {
        private readonly ModMultValue value;

        public ModMultLiteral(ModMultValue value)
        {
            this.value = value;
        }

        public override ModMultValue Evaluate(ModMultContext context) => value;
    }

    /// <summary>
    /// A bare mod acronym, evaluating to whether the mod is enabled (e.g. <c>EZ</c>).
    /// </summary>
    public sealed class ModMultModPresent : ModMultExpression
    {
        public string Acronym { get; }

        public ModMultModPresent(string acronym)
        {
            Acronym = acronym;
        }

        public override ModMultValue Evaluate(ModMultContext context) =>
            ModMultValue.FromBoolean(context.IsModEnabled(Acronym));

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
            => mods.Add(Acronym);
    }

    /// <summary>
    /// A mod setting access (e.g. <c>DT.speed_change</c>). Null when the mod is absent or the setting is unset.
    /// </summary>
    public sealed class ModMultModSetting : ModMultExpression
    {
        public string Acronym { get; }
        public string Setting { get; }

        public ModMultModSetting(string acronym, string setting)
        {
            Acronym = acronym;
            Setting = setting;
        }

        public override ModMultValue Evaluate(ModMultContext context) =>
            context.GetSetting(Acronym, Setting);

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
        {
            mods.Add(Acronym);
            settings.Add((Acronym, Setting));
        }
    }

    public sealed class ModMultUnary : ModMultExpression
    {
        public enum Operator
        {
            Not,
            Negate,
        }

        private readonly Operator op;
        private readonly ModMultExpression operand;

        public ModMultUnary(Operator op, ModMultExpression operand)
        {
            this.op = op;
            this.operand = operand;
        }

        public override ModMultValue Evaluate(ModMultContext context)
        {
            var value = operand.Evaluate(context);

            return op switch
            {
                Operator.Not => ModMultValue.FromBoolean(!value.AsCondition()),
                Operator.Negate => value.IsNull
                    ? ModMultValue.Null
                    : ModMultValue.FromNumber(-requireNumber(value, "Operand of \"-\"")),
                _ => throw new ModMultRuntimeException($"Unknown unary operator {op}."),
            };
        }

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
            => operand.CollectReferences(mods, settings);
    }

    public sealed class ModMultBinary : ModMultExpression
    {
        public enum Operator
        {
            Or,
            And,
            Equal,
            NotEqual,
            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual,
            Add,
            Subtract,
            Multiply,
            Divide,
            Modulo,
            Coalesce,
        }

        private readonly Operator op;
        private readonly ModMultExpression left;
        private readonly ModMultExpression right;

        public ModMultBinary(Operator op, ModMultExpression left, ModMultExpression right)
        {
            this.op = op;
            this.left = left;
            this.right = right;
        }

        public override ModMultValue Evaluate(ModMultContext context)
        {
            // Logical operators short-circuit; coalesce only evaluates the right side when needed.
            if (op == Operator.Or)
                return ModMultValue.FromBoolean(left.Evaluate(context).AsCondition() || right.Evaluate(context).AsCondition());
            if (op == Operator.And)
                return ModMultValue.FromBoolean(left.Evaluate(context).AsCondition() && right.Evaluate(context).AsCondition());
            if (op == Operator.Coalesce)
            {
                var value = left.Evaluate(context);
                return value.IsNull ? right.Evaluate(context) : value;
            }

            var leftValue = left.Evaluate(context);
            var rightValue = right.Evaluate(context);

            switch (op)
            {
                case Operator.Equal:
                    return ModMultValue.FromBoolean(equals(leftValue, rightValue));

                case Operator.NotEqual:
                    return ModMultValue.FromBoolean(!equals(leftValue, rightValue));

                case Operator.LessThan:
                case Operator.LessThanOrEqual:
                case Operator.GreaterThan:
                case Operator.GreaterThanOrEqual:
                    // A comparison involving null is false (use "== null" to test for null explicitly).
                    if (leftValue.IsNull || rightValue.IsNull)
                        return ModMultValue.FromBoolean(false);

                    int comparison = compare(leftValue, rightValue, "comparison");

                    return ModMultValue.FromBoolean(op switch
                    {
                        Operator.LessThan => comparison < 0,
                        Operator.LessThanOrEqual => comparison <= 0,
                        Operator.GreaterThan => comparison > 0,
                        _ => comparison >= 0,
                    });

                case Operator.Add:
                case Operator.Subtract:
                case Operator.Multiply:
                case Operator.Divide:
                case Operator.Modulo:
                    // Null propagates so that "??" can catch it further out.
                    if (leftValue.IsNull || rightValue.IsNull)
                        return ModMultValue.Null;

                    double a = requireNumber(leftValue, "Left operand");
                    double b = requireNumber(rightValue, "Right operand");

                    return ModMultValue.FromNumber(op switch
                    {
                        Operator.Add => a + b,
                        Operator.Subtract => a - b,
                        Operator.Multiply => a * b,
                        Operator.Divide => a / b,
                        _ => a % b,
                    });

                default:
                    throw new ModMultRuntimeException($"Unknown binary operator {op}.");
            }
        }

        private static bool equals(ModMultValue a, ModMultValue b)
        {
            if (a.IsNull || b.IsNull)
                return a.IsNull && b.IsNull;
            if (a.Type != b.Type)
                return false;

            return a.Type switch
            {
                ModMultValue.ValueType.Number => a.Number == b.Number,
                ModMultValue.ValueType.Boolean => a.Boolean == b.Boolean,
                ModMultValue.ValueType.Text => string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        private static int compare(ModMultValue a, ModMultValue b, string what)
        {
            if (a.IsNumber && b.IsNumber)
                return a.Number.CompareTo(b.Number);
            if (a.Type == ModMultValue.ValueType.Text && b.Type == ModMultValue.ValueType.Text)
                return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);

            throw new ModMultRuntimeException($"{what} requires two numbers or two texts, but got {describe(a)} and {describe(b)}.");
        }

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
        {
            left.CollectReferences(mods, settings);
            right.CollectReferences(mods, settings);
        }
    }

    public sealed class ModMultTernary : ModMultExpression
    {
        private readonly ModMultExpression condition;
        private readonly ModMultExpression whenTrue;
        private readonly ModMultExpression whenFalse;

        public ModMultTernary(ModMultExpression condition, ModMultExpression whenTrue, ModMultExpression whenFalse)
        {
            this.condition = condition;
            this.whenTrue = whenTrue;
            this.whenFalse = whenFalse;
        }

        public override ModMultValue Evaluate(ModMultContext context) =>
            (condition.Evaluate(context).AsCondition() ? whenTrue : whenFalse).Evaluate(context);

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
        {
            condition.CollectReferences(mods, settings);
            whenTrue.CollectReferences(mods, settings);
            whenFalse.CollectReferences(mods, settings);
        }
    }

    /// <summary>
    /// A call to <c>has</c>, <c>all</c> or <c>only</c>, taking mod acronyms (not expressions) as arguments.
    /// </summary>
    public sealed class ModMultModNameCall : ModMultExpression
    {
        public string Function { get; }
        public IReadOnlyList<string> Acronyms { get; }

        public ModMultModNameCall(string function, IReadOnlyList<string> acronyms)
        {
            Function = function;
            Acronyms = acronyms;
        }

        public override ModMultValue Evaluate(ModMultContext context) => ModMultValue.FromBoolean(Function switch
        {
            "has" => context.HasAny(Acronyms),
            "all" => context.HasAll(Acronyms),
            "only" => context.HasOnly(Acronyms),
            _ => throw new ModMultRuntimeException($"Unknown mod function \"{Function}\"."),
        });

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
        {
            foreach (string acronym in Acronyms)
                mods.Add(acronym);
        }
    }

    /// <summary>
    /// A call to a numeric function (<c>min</c>, <c>max</c>, <c>clamp</c>, <c>abs</c>, <c>floor</c>, <c>ceil</c>, <c>round</c>, <c>count</c>).
    /// </summary>
    public sealed class ModMultFunctionCall : ModMultExpression
    {
        public string Function { get; }
        public IReadOnlyList<ModMultExpression> Arguments { get; }

        public ModMultFunctionCall(string function, IReadOnlyList<ModMultExpression> arguments)
        {
            Function = function;
            Arguments = arguments;
        }

        public override ModMultValue Evaluate(ModMultContext context)
        {
            switch (Function)
            {
                case "count":
                    return ModMultValue.FromNumber(context.ModCount);

                case "min":
                case "max":
                {
                    var numbers = evaluateNumbers(context);
                    return ModMultValue.FromNumber(Function == "min" ? numbers.Min() : numbers.Max());
                }

                case "clamp":
                {
                    var numbers = evaluateNumbers(context);
                    return ModMultValue.FromNumber(Math.Min(Math.Max(numbers[0], numbers[1]), numbers[2]));
                }

                case "abs":
                    return ModMultValue.FromNumber(Math.Abs(singleNumber(context)));

                case "floor":
                    return ModMultValue.FromNumber(Math.Floor(singleNumber(context)));

                case "ceil":
                    return ModMultValue.FromNumber(Math.Ceiling(singleNumber(context)));

                case "round":
                {
                    var numbers = evaluateNumbers(context);

                    if (numbers.Count == 1)
                        return ModMultValue.FromNumber(Math.Round(numbers[0], MidpointRounding.AwayFromZero));

                    int digits = (int)numbers[1];
                    if (digits < 0 || digits > 15)
                        throw new ModMultRuntimeException("round() precision must be between 0 and 15.");

                    return ModMultValue.FromNumber(Math.Round(numbers[0], digits, MidpointRounding.AwayFromZero));
                }

                default:
                    throw new ModMultRuntimeException($"Unknown function \"{Function}\".");
            }
        }

        private double singleNumber(ModMultContext context)
        {
            var numbers = evaluateNumbers(context);
            return numbers[0];
        }

        public override void CollectReferences(HashSet<string> mods, List<(string mod, string setting)> settings)
        {
            foreach (var argument in Arguments)
                argument.CollectReferences(mods, settings);
        }

        private List<double> evaluateNumbers(ModMultContext context)
        {
            var numbers = new List<double>(Arguments.Count);

            foreach (var argument in Arguments)
            {
                var value = argument.Evaluate(context);

                if (value.IsNull)
                    throw new ModMultRuntimeException($"\"{Function}\" received null. Use \"??\" to provide a fallback.");

                numbers.Add(requireNumber(value, $"Argument of \"{Function}\""));
            }

            return numbers;
        }
    }

    /// <summary>
    /// Recursive-descent parser for the ModMult rule language.
    /// Grammar (loosely C/JavaScript-like):
    /// <code>
    /// rule     := expr
    /// expr     := or ("?" expr (":" expr)?)?      // ternary with optional else (defaults to 1)
    /// or       := and ("||" and)*
    /// and      := equality ("&amp;&amp;" equality)*
    /// equality := comparison (("==" | "!=") comparison)*
    /// comparison := additive (("&lt;=" | "&gt;=" | "&lt;" | "&gt;") additive)*
    /// additive := multiplicative (("+" | "-") multiplicative)*
    /// multiplicative := unary (("*" | "/" | "%") unary)*
    /// unary    := ("!" | "-")* coalesce
    /// coalesce := primary ("??" coalesce)?
    /// primary  := number | string | true | false | null
    ///           | ident "(" args? ")"              // function call
    ///           | ident "." ident                  // MOD.setting
    ///           | ident                            // bare mod acronym = presence test
    ///           | "(" expr ")"
    /// </code>
    /// </summary>
    public sealed class ModMultParser
    {
        public const string KNOWN_FUNCTIONS = "has, all, only, count, min, max, clamp, abs, floor, ceil, round";

        private readonly List<ModMultToken> tokens;
        private int position;

        private ModMultParser(List<ModMultToken> tokens)
        {
            this.tokens = tokens;
        }

        public static ModMultExpression Parse(string source)
        {
            var parser = new ModMultParser(new ModMultLexer(source).Tokenise());
            var expression = parser.parseExpression();

            parser.expect(ModMultTokenType.End);
            return expression;
        }

        private ModMultExpression parseExpression()
        {
            var condition = parseOr();

            if (!match(ModMultTokenType.Question))
                return condition;

            var whenTrue = parseExpression();

            // The else branch is optional and defaults to 1, so that "EZ ? 2" reads as "x2 with EZ, x1 otherwise".
            ModMultExpression whenFalse = match(ModMultTokenType.Colon)
                ? parseExpression()
                : new ModMultLiteral(ModMultValue.FromNumber(1));

            return new ModMultTernary(condition, whenTrue, whenFalse);
        }

        private ModMultExpression parseOr()
        {
            var left = parseAnd();

            while (match(ModMultTokenType.Or))
                left = new ModMultBinary(ModMultBinary.Operator.Or, left, parseAnd());

            return left;
        }

        private ModMultExpression parseAnd()
        {
            var left = parseEquality();

            while (match(ModMultTokenType.And))
                left = new ModMultBinary(ModMultBinary.Operator.And, left, parseEquality());

            return left;
        }

        private ModMultExpression parseEquality()
        {
            var left = parseComparison();

            while (true)
            {
                if (match(ModMultTokenType.Equals))
                    left = new ModMultBinary(ModMultBinary.Operator.Equal, left, parseComparison());
                else if (match(ModMultTokenType.NotEquals))
                    left = new ModMultBinary(ModMultBinary.Operator.NotEqual, left, parseComparison());
                else
                    return left;
            }
        }

        private ModMultExpression parseComparison()
        {
            var left = parseAdditive();

            while (true)
            {
                if (match(ModMultTokenType.LessThan))
                    left = new ModMultBinary(ModMultBinary.Operator.LessThan, left, parseAdditive());
                else if (match(ModMultTokenType.LessThanOrEqual))
                    left = new ModMultBinary(ModMultBinary.Operator.LessThanOrEqual, left, parseAdditive());
                else if (match(ModMultTokenType.GreaterThan))
                    left = new ModMultBinary(ModMultBinary.Operator.GreaterThan, left, parseAdditive());
                else if (match(ModMultTokenType.GreaterThanOrEqual))
                    left = new ModMultBinary(ModMultBinary.Operator.GreaterThanOrEqual, left, parseAdditive());
                else
                    return left;
            }
        }

        private ModMultExpression parseAdditive()
        {
            var left = parseMultiplicative();

            while (true)
            {
                if (match(ModMultTokenType.Plus))
                    left = new ModMultBinary(ModMultBinary.Operator.Add, left, parseMultiplicative());
                else if (match(ModMultTokenType.Minus))
                    left = new ModMultBinary(ModMultBinary.Operator.Subtract, left, parseMultiplicative());
                else
                    return left;
            }
        }

        private ModMultExpression parseMultiplicative()
        {
            var left = parseUnary();

            while (true)
            {
                if (match(ModMultTokenType.Asterisk))
                    left = new ModMultBinary(ModMultBinary.Operator.Multiply, left, parseUnary());
                else if (match(ModMultTokenType.Slash))
                    left = new ModMultBinary(ModMultBinary.Operator.Divide, left, parseUnary());
                else if (match(ModMultTokenType.Percent))
                    left = new ModMultBinary(ModMultBinary.Operator.Modulo, left, parseUnary());
                else
                    return left;
            }
        }

        private ModMultExpression parseUnary()
        {
            if (match(ModMultTokenType.Not))
                return new ModMultUnary(ModMultUnary.Operator.Not, parseUnary());
            if (match(ModMultTokenType.Minus))
                return new ModMultUnary(ModMultUnary.Operator.Negate, parseUnary());

            return parseCoalesce();
        }

        private ModMultExpression parseCoalesce()
        {
            var left = parsePrimary();

            if (match(ModMultTokenType.Coalesce))
                return new ModMultBinary(ModMultBinary.Operator.Coalesce, left, parseCoalesce());

            return left;
        }

        private ModMultExpression parsePrimary()
        {
            var token = peek();

            switch (token.Type)
            {
                case ModMultTokenType.Number:
                    advance();
                    return new ModMultLiteral(ModMultValue.FromNumber(token.Number));

                case ModMultTokenType.String:
                    advance();
                    return new ModMultLiteral(ModMultValue.FromText(token.Text));

                case ModMultTokenType.Identifier:
                    return parseIdentifierOrCall();

                case ModMultTokenType.OpenParen:
                {
                    advance();
                    var inner = parseExpression();
                    expect(ModMultTokenType.CloseParen);
                    return inner;
                }

                default:
                    throw new ModMultParseException($"Unexpected \"{token.Text}\"", token.Position);
            }
        }

        private ModMultExpression parseIdentifierOrCall()
        {
            var nameToken = expect(ModMultTokenType.Identifier);
            string name = nameToken.Text;

            if (peek().Type == ModMultTokenType.Dot)
            {
                advance();
                var settingToken = expect(ModMultTokenType.Identifier);
                return new ModMultModSetting(name.ToUpperInvariant(), settingToken.Text);
            }

            if (peek().Type == ModMultTokenType.OpenParen)
                return parseCall(name, nameToken.Position);

            if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase))
                return new ModMultLiteral(ModMultValue.FromBoolean(true));
            if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase))
                return new ModMultLiteral(ModMultValue.FromBoolean(false));
            if (string.Equals(name, "null", StringComparison.OrdinalIgnoreCase))
                return new ModMultLiteral(ModMultValue.Null);

            return new ModMultModPresent(name.ToUpperInvariant());
        }

        private ModMultExpression parseCall(string name, int position)
        {
            string function = name.ToLowerInvariant();

            expect(ModMultTokenType.OpenParen);

            switch (function)
            {
                case "has":
                case "all":
                case "only":
                {
                    var acronyms = new List<string>();

                    if (peek().Type != ModMultTokenType.CloseParen)
                    {
                        acronyms.Add(expect(ModMultTokenType.Identifier).Text.ToUpperInvariant());

                        while (match(ModMultTokenType.Comma))
                            acronyms.Add(expect(ModMultTokenType.Identifier).Text.ToUpperInvariant());
                    }

                    expect(ModMultTokenType.CloseParen);

                    if (acronyms.Count == 0)
                        throw new ModMultParseException($"\"{function}\" requires at least one mod acronym", position);

                    return new ModMultModNameCall(function, acronyms);
                }

                case "count":
                    expect(ModMultTokenType.CloseParen);
                    return new ModMultFunctionCall("count", Array.Empty<ModMultExpression>());

                case "min":
                case "max":
                case "clamp":
                case "abs":
                case "floor":
                case "ceil":
                case "round":
                {
                    var arguments = new List<ModMultExpression>();

                    if (peek().Type != ModMultTokenType.CloseParen)
                    {
                        arguments.Add(parseExpression());

                        while (match(ModMultTokenType.Comma))
                            arguments.Add(parseExpression());
                    }

                    expect(ModMultTokenType.CloseParen);
                    checkArity(function, arguments.Count, position);
                    return new ModMultFunctionCall(function, arguments);
                }

                default:
                    throw new ModMultParseException($"Unknown function \"{name}\". Known functions: {KNOWN_FUNCTIONS}", position);
            }
        }

        private static void checkArity(string function, int count, int position)
        {
            bool valid = function switch
            {
                "min" or "max" => count >= 1,
                "clamp" => count == 3,
                "abs" or "floor" or "ceil" => count == 1,
                "round" => count is 1 or 2,
                _ => true,
            };

            if (!valid)
                throw new ModMultParseException($"\"{function}\" received {count} argument(s)", position);
        }

        private ModMultToken peek() => tokens[position];

        private void advance() => position++;

        private bool match(ModMultTokenType type)
        {
            if (peek().Type != type)
                return false;

            advance();
            return true;
        }

        private ModMultToken expect(ModMultTokenType type)
        {
            var token = peek();

            if (type == ModMultTokenType.End)
            {
                if (token.Type != ModMultTokenType.End)
                    throw new ModMultParseException($"Expected end of rule but found \"{token.Text}\"", token.Position);

                return token;
            }

            if (token.Type == ModMultTokenType.End)
                throw new ModMultParseException("Unexpected end of rule", token.Position);

            if (token.Type != type)
            {
                string expected = type switch
                {
                    ModMultTokenType.Identifier => "a name",
                    ModMultTokenType.CloseParen => "\")\"",
                    ModMultTokenType.End => "end of rule",
                    _ => $"\"{token.Text}\"",
                };

                throw new ModMultParseException($"Expected {expected} but found \"{token.Text}\"", token.Position);
            }

            advance();
            return token;
        }
    }
}
