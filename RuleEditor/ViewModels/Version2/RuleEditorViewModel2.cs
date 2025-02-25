using RuleEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RuleEditor.ViewModels.Version2
{
    public class RuleEditorViewModel2 : ViewModelBase
    {
        // Example of property using the new pattern
        public List<RulePropertyInfo> AvailableProperties
        {
            get => GetValue<List<RulePropertyInfo>>();
            set => SetValue(value);
        }

        public string ExpressionCode
        {
            get => GetValue<string>();
            set => SetValue(value);
        }

        public string ValidationMessage
        {
            get => GetValue<string>();
            set => SetValue(value);
        }

        public RuleEditorViewModel2()
        {
            InitializeAvailableProperties();
        }

        private void InitializeAvailableProperties()
        {
            AvailableProperties = new List<RulePropertyInfo>
            {
                new RulePropertyInfo { Name = "Name", Type = typeof(string), Description = "Person's full name" },
                new RulePropertyInfo { Name = "Age", Type = typeof(int), Description = "Age in years" },
                new RulePropertyInfo { Name = "IsActive", Type = typeof(bool), Description = "Account status" },
                new RulePropertyInfo { Name = "Balance", Type = typeof(decimal), Description = "Current balance" },
                new RulePropertyInfo { Name = "Email", Type = typeof(string), Description = "Email address" },
                new RulePropertyInfo { Name = "LastLoginDate", Type = typeof(DateTime), Description = "Last login timestamp" }
            };
        }

        public (List<string> Suggestions, int StartIndex) GetSuggestions(string textBeforeCaret)
        {
            var suggestions = new List<string>();
            int startIndex = textBeforeCaret.Length;

            // Find the start of the current word
            var lastSpace = textBeforeCaret.LastIndexOf(' ');
            var lastNewLine = textBeforeCaret.LastIndexOf('\n');
            var lastOperator = textBeforeCaret.LastIndexOfAny(new[] { '=', '>', '<', '!' });
            
            startIndex = Math.Max(Math.Max(lastSpace, lastNewLine), lastOperator);
            if (startIndex < 0) startIndex = 0;

            var currentWord = textBeforeCaret.Substring(startIndex).Trim();

            // Add property suggestions
            suggestions.AddRange(AvailableProperties
                .Where(p => p.Name.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Name));

            // Add operator suggestions if after a property
            var tokens = Tokenize(textBeforeCaret);
            if (tokens.Count > 0 && AvailableProperties.Any(p => p.Name.Equals(tokens.Last(), StringComparison.OrdinalIgnoreCase)))
            {
                var propertyType = AvailableProperties.First(p => p.Name.Equals(tokens.Last(), StringComparison.OrdinalIgnoreCase)).Type;
                suggestions.AddRange(GetValidOperators(propertyType));
            }

            return (suggestions, startIndex);
        }

        public List<string> GetUnknownProperties(string expression)
        {
            var unknownProperties = new List<string>();
            var tokens = Tokenize(expression);

            foreach (var token in tokens)
            {
                // Skip operators and values
                if (IsOperator(token) || IsValue(token))
                    continue;

                // Check if token is a known property
                if (!AvailableProperties.Any(p => p.Name.Equals(token, StringComparison.OrdinalIgnoreCase)))
                {
                    unknownProperties.Add(token);
                }
            }

            return unknownProperties;
        }

        public string FormatExpression(string expression)
        {
            var tokens = Tokenize(expression);
            var formattedExpression = "";
            var indentLevel = 0;

            foreach (var token in tokens)
            {
                if (token == "(")
                {
                    formattedExpression += token + "\n" + new string(' ', ++indentLevel * 4);
                }
                else if (token == ")")
                {
                    formattedExpression = formattedExpression.TrimEnd() + "\n" + new string(' ', --indentLevel * 4) + token;
                }
                else if (token == "AND" || token == "OR")
                {
                    formattedExpression = formattedExpression.TrimEnd() + "\n" + new string(' ', indentLevel * 4) + token + " ";
                }
                else
                {
                    formattedExpression += token + " ";
                }
            }

            return formattedExpression.Trim();
        }

        public bool ValidateExpression(string expression)
        {
            try
            {
                var tokens = Tokenize(expression);
                return ValidateTokens(tokens);
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Validation error: {ex.Message}";
                return false;
            }
        }

        private List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            var currentToken = "";

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];

                if (char.IsWhiteSpace(c))
                {
                    if (!string.IsNullOrEmpty(currentToken))
                    {
                        tokens.Add(currentToken);
                        currentToken = "";
                    }
                }
                else if (c == '(' || c == ')')
                {
                    if (!string.IsNullOrEmpty(currentToken))
                    {
                        tokens.Add(currentToken);
                        currentToken = "";
                    }
                    tokens.Add(c.ToString());
                }
                else if (IsOperatorChar(c))
                {
                    if (!string.IsNullOrEmpty(currentToken))
                    {
                        tokens.Add(currentToken);
                        currentToken = "";
                    }
                    currentToken += c;
                }
                else
                {
                    currentToken += c;
                }
            }

            if (!string.IsNullOrEmpty(currentToken))
            {
                tokens.Add(currentToken);
            }

            return tokens;
        }

        private bool ValidateTokens(List<string> tokens)
        {
            // Basic validation rules
            int parenthesesCount = 0;
            bool expectingValue = true;

            foreach (var token in tokens)
            {
                if (token == "(")
                {
                    parenthesesCount++;
                    expectingValue = true;
                }
                else if (token == ")")
                {
                    parenthesesCount--;
                    if (parenthesesCount < 0)
                        throw new Exception("Unmatched closing parenthesis");
                    expectingValue = false;
                }
                else if (IsOperator(token))
                {
                    if (expectingValue)
                        throw new Exception($"Unexpected operator: {token}");
                    expectingValue = true;
                }
                else
                {
                    if (!expectingValue)
                        throw new Exception($"Unexpected value: {token}");
                    expectingValue = false;
                }
            }

            if (parenthesesCount != 0)
                throw new Exception("Unmatched opening parenthesis");

            return true;
        }

        private bool IsOperator(string token)
        {
            return token == "==" || token == "!=" || token == ">" || token == "<" || 
                   token == ">=" || token == "<=" || token == "AND" || token == "OR" ||
                   token == "CONTAINS" || token == "STARTSWITH" || token == "ENDSWITH";
        }

        private bool IsOperatorChar(char c)
        {
            return c == '=' || c == '!' || c == '>' || c == '<';
        }

        private bool IsValue(string token)
        {
            return token.StartsWith("'") || token.StartsWith("\"") || 
                   double.TryParse(token, out _) || bool.TryParse(token, out _);
        }

        private List<string> GetValidOperators(Type propertyType)
        {
            var operators = new List<string>();

            // Common operators for all types
            operators.AddRange(new[] { "==", "!=" });

            // Numeric operators
            if (propertyType == typeof(int) || propertyType == typeof(double) || 
                propertyType == typeof(decimal))
            {
                operators.AddRange(new[] { ">", "<", ">=", "<=" });
            }

            // String operators
            if (propertyType == typeof(string))
            {
                operators.AddRange(new[] { "CONTAINS", "STARTSWITH", "ENDSWITH" });
            }

            return operators;
        }
    }
}
