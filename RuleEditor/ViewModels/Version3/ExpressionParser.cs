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
        Unknown
    }

    public class Token
    {
        public string Value { get; set; }
        public TokenType Type { get; set; }
        public int Position { get; set; }
        public int Length { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }

        public Token(string value, TokenType type, int position)
        {
            Value = value;
            Type = type;
            Position = position;
            Length = value?.Length ?? 0;
        }

        public override string ToString() => Value;
    }

    public class ExpressionParser
    {
        private readonly List<RulePropertyInfo> _availableProperties;
        private static readonly List<string> _comparisonOperators = new List<string> { "==", "!=", ">", "<", ">=", "<=", "CONTAINS", "STARTSWITH", "ENDSWITH" };
        private static readonly List<string> _logicalOperators = new List<string> { "AND", "OR", "NOT" };

        public ExpressionParser(List<RulePropertyInfo> availableProperties)
        {
            _availableProperties = availableProperties ?? new List<RulePropertyInfo>();
        }

        public List<Token> Tokenize(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return new List<Token>();

            var tokens = new List<Token>();
            var currentPosition = 0;
            var expressionLength = expression.Length;
            var currentState = TokenType.Property; // Start expecting a property

            while (currentPosition < expressionLength)
            {
                // Skip whitespace
                while (currentPosition < expressionLength && char.IsWhiteSpace(expression[currentPosition]))
                    currentPosition++;

                if (currentPosition >= expressionLength)
                    break;

                // Check for parentheses
                if (expression[currentPosition] == '(')
                {
                    tokens.Add(new Token("(", TokenType.OpenParenthesis, currentPosition));
                    currentPosition++;
                    continue;
                }
                else if (expression[currentPosition] == ')')
                {
                    tokens.Add(new Token(")", TokenType.CloseParenthesis, currentPosition));
                    currentPosition++;
                    continue;
                }

                // Check for string literals
                if (expression[currentPosition] == '\'' || expression[currentPosition] == '"')
                {
                    var quoteChar = expression[currentPosition];
                    var startPos = currentPosition;
                    currentPosition++; // Skip the opening quote

                    // Find the closing quote
                    while (currentPosition < expressionLength && expression[currentPosition] != quoteChar)
                        currentPosition++;

                    if (currentPosition < expressionLength) // Found closing quote
                    {
                        currentPosition++; // Include the closing quote
                        var value = expression.Substring(startPos, currentPosition - startPos);
                        tokens.Add(new Token(value, TokenType.Value, startPos));
                    }
                    else // No closing quote found
                    {
                        var value = expression.Substring(startPos);
                        tokens.Add(new Token(value, TokenType.Unknown, startPos));
                    }
                    continue;
                }

                // Try to match operators
                var remainingText = expression.Substring(currentPosition);
                var operatorMatch = _comparisonOperators
                    .Concat(_logicalOperators)
                    .OrderByDescending(op => op.Length) // Try longer operators first
                    .FirstOrDefault(op => 
                        remainingText.StartsWith(op, StringComparison.OrdinalIgnoreCase) && 
                        (remainingText.Length == op.Length || !char.IsLetterOrDigit(remainingText[op.Length])));

                if (operatorMatch != null)
                {
                    var tokenType = _logicalOperators.Contains(operatorMatch, StringComparer.OrdinalIgnoreCase) 
                        ? TokenType.LogicalOperator 
                        : TokenType.Operator;
                    
                    tokens.Add(new Token(operatorMatch, tokenType, currentPosition));
                    currentPosition += operatorMatch.Length;
                    continue;
                }

                // Try to match a word (property name, value, etc.)
                var wordMatch = Regex.Match(remainingText, @"^\w+");
                if (wordMatch.Success)
                {
                    var word = wordMatch.Value;
                    var tokenType = DetermineTokenType(word, tokens);
                    
                    tokens.Add(new Token(word, tokenType, currentPosition));
                    currentPosition += word.Length;
                    continue;
                }

                // If we get here, we couldn't match anything recognizable
                tokens.Add(new Token(expression[currentPosition].ToString(), TokenType.Unknown, currentPosition));
                currentPosition++;
            }

            return tokens;
        }

        private TokenType DetermineTokenType(string word, List<Token> existingTokens)
        {
            // Check if it's a boolean literal
            if (word.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                word.Equals("false", StringComparison.OrdinalIgnoreCase))
                return TokenType.Value;

            // Check if it's a numeric literal
            if (decimal.TryParse(word, out _))
                return TokenType.Value;

            // Check if it's a property name
            if (_availableProperties.Any(p => p.Name.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return TokenType.Property;

            // If the previous token was an operator, this is likely a value
            if (existingTokens.Count > 0 && 
                (existingTokens.Last().Type == TokenType.Operator || 
                 existingTokens.Last().Type == TokenType.LogicalOperator))
                return TokenType.Value;

            // Default to unknown
            return TokenType.Unknown;
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
                        else if (next.Type != TokenType.Value)
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
    }
}