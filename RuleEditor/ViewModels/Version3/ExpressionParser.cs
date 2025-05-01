using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RuleEditor.Models;

namespace RuleEditor.ViewModels.Version3
{
    public enum TokenType
    {
        Property,
        Operator,
        Value,
        LogicalOperator,
        OpenParenthesis,
        CloseParenthesis,
        RestrictedValue,  // For restricted values like Friend names that must come from a predefined list
        Unknown
    }

    public class Token
    {
        public string Value { get; set; }
        public TokenType Type { get; set; }
        public IEnumerable<TokenType> PossibleTypes { get; set; }
        public int Position { get; set; }
        public int Length { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
        public IEnumerable<string> PossibleValues { get; set; }
        // New property
        public bool RequiresParameter { get; set; }



        public Token(string value, TokenType type, int position, IEnumerable<TokenType> possibleTypes = null, IEnumerable<string> possibleValues = null)
        {
            Value = value;
            Type = type;
            Position = position;
            Length = value?.Length ?? 0;
            PossibleTypes = possibleTypes ?? new[] { type };
            PossibleValues = possibleValues;
        }

        public override string ToString() => Value;
    }

    public class ExpressionParser
    {
        private readonly List<RulePropertyInfo> _availableProperties;
        private static readonly List<string> _comparisonOperators = new List<string> { "==", "!=", ">", "<", ">=", "<=", "CONTAINS", "STARTSWITH", "ENDSWITH" };
        private static readonly List<string> _logicalOperators = new List<string> { "AND", "OR", "NOT" };
        private TokenType _expectedNextTokenType;

        public ExpressionParser(List<RulePropertyInfo> availableProperties)
        {
            _availableProperties = availableProperties ?? new List<RulePropertyInfo>();
        }

        public List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrWhiteSpace(expression))
                return tokens;

            int position = 0;
            int index = 0;
            int length = expression.Length;

            while (index < length)
            {
                // Skip whitespace
                while (index < length && char.IsWhiteSpace(expression[index]))
                {
                    index++;
                    position++;
                }

                if (index >= length)
                    break;

                // Handle quoted values (single or double quotes)
                if (expression[index] == '\'' || expression[index] == '\"')
                {
                    char quote = expression[index];
                    int start = index;
                    index++; // Skip opening quote

                    while (index < length && expression[index] != quote)
                        index++;

                    // Include the closing quote if present
                    if (index < length && expression[index] == quote)
                        index++;

                    string quotedValue = expression.Substring(start, index - start);
                    tokens.Add(new Token(quotedValue, TokenType.Value, position));
                    position += quotedValue.Length;
                }
                else
                {
                    // Read until next whitespace
                    int start = index;
                    while (index < length && !char.IsWhiteSpace(expression[index]))
                        index++;

                    string part = expression.Substring(start, index - start);
                    TokenType type = DetermineTokenType(part, tokens);
                    tokens.Add(new Token(part, type, position));
                    position += part.Length;
                }
            }

            return tokens;
        }

        private TokenType DetermineTokenType(string word, List<Token> existingTokens)
        {
            // If the previous token was a value or close parenthesis, only logical operators are valid
            if (existingTokens.Count > 0)
            {
                var prev = existingTokens.Last();
                if (prev.Type == TokenType.Value || prev.Type == TokenType.CloseParenthesis)
                {
                    // Only allow logical operators
                    if (new[] { "AND", "OR", "NOT" }.Any(op => op.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
                        return TokenType.LogicalOperator;
                    return TokenType.Unknown; // Prevent property or value tokens here
                }
            }

            // Check if it's a boolean literal
            if (word.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                word.Equals("false", StringComparison.OrdinalIgnoreCase))
                return TokenType.Value;

            // Check if it's a numeric literal
            if (decimal.TryParse(word, out _))
                return TokenType.Value;

            // Check if it's a property name
            if (_availableProperties.Any(p => p.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
                return TokenType.Property;

            // If the previous token was an operator, this is likely a value
            if (existingTokens.Count > 0 &&
                (existingTokens.Last().Type == TokenType.Operator ||
                 existingTokens.Last().Type == TokenType.LogicalOperator))
                return TokenType.Value;

            // Check if it's a full comparison operator
            if (_comparisonOperators.Contains(word, StringComparer.OrdinalIgnoreCase))
                return TokenType.Operator;

            // NEW: Check if it's a prefix of any comparison operator
            if (_comparisonOperators.Any(op => op.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
                return TokenType.Operator;

            // Default to unknown
            return TokenType.Unknown;
        }

        public void SetExpectedNextTokenType(TokenType expectedType)
        {
            _expectedNextTokenType = expectedType;
        }

        public bool ValidateSyntax(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return false;

            // Check for balanced parentheses
            int parenthesisCount = 0;
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.OpenParenthesis)
                    parenthesisCount++;
                else if (token.Type == TokenType.CloseParenthesis)
                    parenthesisCount--;

                if (parenthesisCount < 0) // More closing than opening parentheses
                    return false;
            }

            if (parenthesisCount != 0) // Unbalanced parentheses
                return false;

            // Check for valid token sequences
            for (int i = 0; i < tokens.Count; i++)
            {
                var current = tokens[i];
                var next = i < tokens.Count - 1 ? tokens[i + 1] : null;

                // Rules for valid sequences
                switch (current.Type)
                {
                    case TokenType.Property:
                        if (next == null || next.Type != TokenType.Operator)
                            return false;
                        break;

                    case TokenType.Operator:
                        if (next == null || next.Type != TokenType.Value)
                            return false;
                        break;

                    case TokenType.Value:
                        if (next != null && next.Type != TokenType.LogicalOperator && next.Type != TokenType.CloseParenthesis)
                            return false;
                        break;

                    case TokenType.LogicalOperator:
                        if (next == null || (next.Type != TokenType.Property && next.Type != TokenType.OpenParenthesis))
                            return false;
                        break;

                    case TokenType.OpenParenthesis:
                        if (next == null || (next.Type != TokenType.Property && next.Type != TokenType.OpenParenthesis))
                            return false;
                        break;

                    case TokenType.CloseParenthesis:
                        if (next != null && next.Type != TokenType.LogicalOperator && next.Type != TokenType.CloseParenthesis)
                            return false;
                        break;

                    case TokenType.Unknown:
                        return false;
                }
            }

            return true;
        }

        public List<string> GetSyntaxErrors(List<Token> tokens)
        {
            var errors = new List<string>();

            // Reset all error flags first
            foreach (var token in tokens)
            {
                token.HasError = false;
                token.ErrorMessage = null;
            }

            if (tokens == null || tokens.Count == 0)
            {
                return new List<string> { "Expression is empty" };
            }

            // Check for balanced parentheses
            int openParenCount = tokens.Count(t => t.Type == TokenType.OpenParenthesis);
            int closeParenCount = tokens.Count(t => t.Type == TokenType.CloseParenthesis);

            if (openParenCount != closeParenCount)
            {
                errors.Add($"Unbalanced parentheses: {openParenCount} opening and {closeParenCount} closing");

                // Mark all parentheses as having errors
                foreach (var token in tokens.Where(t => t.Type == TokenType.OpenParenthesis || t.Type == TokenType.CloseParenthesis))
                {
                    token.HasError = true;
                    token.ErrorMessage = "Unbalanced parentheses";
                }
            }

            // Check token sequence
            for (int i = 0; i < tokens.Count; i++)
            {
                var current = tokens[i];
                var next = i < tokens.Count - 1 ? tokens[i + 1] : null;
                var prev = i > 0 ? tokens[i - 1] : null;

                switch (current.Type)
                {
                    case TokenType.Property:
                        if (next == null)
                        {
                            errors.Add($"Property '{current.Value}' must be followed by an operator");
                            current.HasError = true;
                            current.ErrorMessage = "Property must be followed by an operator";
                        }
                        else if (next.Type != TokenType.Operator)
                        {
                            errors.Add($"Property '{current.Value}' must be followed by an operator, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Property must be followed by an operator, not '{next.Value}'";
                        }

                        if (prev != null && prev.Type != TokenType.LogicalOperator && prev.Type != TokenType.OpenParenthesis)
                        {
                            errors.Add($"Property '{current.Value}' must be preceded by a logical operator or opening parenthesis, not '{prev.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Property must be preceded by a logical operator or opening parenthesis";
                        }
                        break;

                    case TokenType.Operator:
                        if (next == null)
                        {
                            errors.Add($"Operator '{current.Value}' must be followed by a value");
                            current.HasError = true;
                            current.ErrorMessage = "Operator must be followed by a value";
                        }
                        else if (next.Type != TokenType.Value && next.Type != TokenType.RestrictedValue)
                        {
                            errors.Add($"Operator '{current.Value}' must be followed by a value, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Operator must be followed by a value, not '{next.Value}'";
                        }

                        if (prev == null || prev.Type != TokenType.Property)
                        {
                            errors.Add($"Operator '{current.Value}' must be preceded by a property");
                            current.HasError = true;
                            current.ErrorMessage = "Operator must be preceded by a property";
                        }


                        // Check if the operator is a valid, full operator
                        bool isValidOperator = _comparisonOperators.Contains(current.Value, StringComparer.OrdinalIgnoreCase);

                        if (!isValidOperator)
                        {
                            errors.Add($"Invalid operator '{current.Value}'.");
                            current.HasError = true;
                            current.ErrorMessage = $"Invalid operator '{current.Value}'.";
                        }
                        break;

                    case TokenType.Value:
                        if (next != null && next.Type != TokenType.LogicalOperator && next.Type != TokenType.CloseParenthesis)
                        {
                            errors.Add($"Value '{current.Value}' must be followed by a logical operator or closing parenthesis, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Value must be followed by a logical operator or closing parenthesis";
                        }

                        if (prev == null || prev.Type != TokenType.Operator)
                        {
                            errors.Add($"Value '{current.Value}' must be preceded by an operator");
                            current.HasError = true;
                            current.ErrorMessage = "Value must be preceded by an operator";
                        }
                        break;

                    case TokenType.LogicalOperator:
                        if (next == null)
                        {
                            errors.Add($"Logical operator '{current.Value}' must be followed by a property or opening parenthesis");
                            current.HasError = true;
                            current.ErrorMessage = "Logical operator must be followed by a property or opening parenthesis";
                        }
                        else if (next.Type != TokenType.Property && next.Type != TokenType.OpenParenthesis)
                        {
                            errors.Add($"Logical operator '{current.Value}' must be followed by a property or opening parenthesis, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Logical operator must be followed by a property or opening parenthesis";
                        }
                        break;

                    case TokenType.OpenParenthesis:
                        if (next == null)
                        {
                            errors.Add("Opening parenthesis must be followed by a property or another opening parenthesis");
                            current.HasError = true;
                            current.ErrorMessage = "Opening parenthesis must be followed by a property or another opening parenthesis";
                        }
                        else if (next.Type != TokenType.Property && next.Type != TokenType.OpenParenthesis)
                        {
                            errors.Add($"Opening parenthesis must be followed by a property or another opening parenthesis, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Opening parenthesis must be followed by a property or another opening parenthesis";
                        }
                        break;

                    case TokenType.CloseParenthesis:
                        if (next != null && next.Type != TokenType.LogicalOperator && next.Type != TokenType.CloseParenthesis)
                        {
                            errors.Add($"Closing parenthesis must be followed by a logical operator or another closing parenthesis, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Closing parenthesis must be followed by a logical operator or another closing parenthesis";
                        }
                        break;
                }
            }

            return errors;
        }

        private bool IsValidRestrictedValue(string inputValue, IEnumerable<string> allowedValues)
        {
            if (allowedValues == null || !allowedValues.Any())
                return true; // No restrictions

            string valueToCheck = inputValue;

            // Remove quotes if present
            if ((valueToCheck.StartsWith("'") && valueToCheck.EndsWith("'")) ||
                (valueToCheck.StartsWith("\"") && valueToCheck.EndsWith("\"")))
            {
                valueToCheck = valueToCheck.Substring(1, valueToCheck.Length - 2);
            }

            // First check for exact match
            if (allowedValues.Any(v => v.Equals(inputValue, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Then check for matches where we extract just the name part
            return allowedValues.Any(v =>
            {
                string cleanValue = v;

                // Remove quotes if present
                if ((cleanValue.StartsWith("'") && cleanValue.EndsWith("'")) ||
                    (cleanValue.StartsWith("\"") && cleanValue.EndsWith("\"")))
                {
                    cleanValue = cleanValue.Substring(1, cleanValue.Length - 2);
                }

                // Extract just the name part if it's in Name (ID) format
                int parenIndex = cleanValue.IndexOf(" (");
                if (parenIndex > 0)
                {
                    cleanValue = cleanValue.Substring(0, parenIndex);
                }

                return cleanValue.StartsWith(valueToCheck, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}