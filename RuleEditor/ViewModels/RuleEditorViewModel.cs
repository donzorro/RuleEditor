using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Xaml.Behaviors.Core;
using RuleEditor.Models;
using RulePropertyInfo = RuleEditor.Models.RulePropertyInfo;

namespace RuleEditor.ViewModels
{   

    public class RuleEditorViewModel : ViewModelBase
    {
        private Rule _currentRule;
        private string _expression;
        private string _validationMessage;
        private bool _isValid;
        private ObservableCollection<RulePropertyInfo> _availableProperties;
        private ObservableCollection<string> _unknownProperties;
        private TextDocument _expressionDocument;
        private Type _targetType;
        private readonly HashSet<string> _validOperators = new HashSet<string> 
        { 
            ">", "<", ">=", "<=", "==", "!=", 
            "CONTAINS", "STARTSWITH", "ENDSWITH",
            "AND", "OR", "NOT"
        };

        public RuleEditorViewModel()
        {
            CurrentRule = new Rule();
            AvailableProperties = new ObservableCollection<RulePropertyInfo>();
            UnknownProperties = new ObservableCollection<string>();
            IsValid = true;
            Expression = string.Empty;
            ExpressionDocument = new TextDocument();
            FormatExpressionCommand = new ActionCommand(ExecuteFormatExpression);
        }

        public Rule CurrentRule
        {
            get => _currentRule;
            set => SetProperty(ref _currentRule, value);
        }

        public string Expression
        {
            get => _expression;
            set
            {
                if (SetProperty(ref _expression, value))
                {
                    ValidateExpression();
                }
            }
        }

        public TextDocument ExpressionDocument
        {
            get => _expressionDocument;
            set => SetProperty(ref _expressionDocument, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }

        public bool IsValid
        {
            get => _isValid;
            set => SetProperty(ref _isValid, value);
        }

        public ObservableCollection<RulePropertyInfo> AvailableProperties
        {
            get => _availableProperties;
            set => SetProperty(ref _availableProperties, value);
        }

        public ObservableCollection<string> UnknownProperties
        {
            get => _unknownProperties;
            private set => SetProperty(ref _unknownProperties, value);
        }

        public ICommand FormatExpressionCommand { get; }

        private void ExecuteFormatExpression()
        {
            if (string.IsNullOrWhiteSpace(Expression)) return;

            try
            {
                var tokens = TokenizeExpression(Expression);
                var formattedExpression = FormatTokens(tokens);
                Expression = formattedExpression;
                ExpressionDocument.Text = formattedExpression;
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Could not format expression: {ex.Message}";
            }
        }

        private string FormatTokens(List<string> tokens)
        {
            var indent = 0;
            var result = new System.Text.StringBuilder();
            var needsSpace = false;

            foreach (var token in tokens)
            {
                if (token == "(")
                {
                    if (needsSpace) result.Append(' ');
                    result.Append(token);
                    result.AppendLine();
                    indent++;
                    result.Append(new string(' ', indent * 4));
                    needsSpace = false;
                }
                else if (token == ")")
                {
                    result.AppendLine();
                    indent--;
                    result.Append(new string(' ', indent * 4));
                    result.Append(token);
                    needsSpace = true;
                }
                else if (IsOperator(token))
                {
                    result.AppendLine();
                    result.Append(new string(' ', indent * 4));
                    result.Append(token);
                    result.AppendLine();
                    result.Append(new string(' ', indent * 4));
                    needsSpace = false;
                }
                else
                {
                    if (needsSpace) result.Append(' ');
                    result.Append(token);
                    needsSpace = true;
                }
            }

            return result.ToString();
        }

        public void ValidateExpression()
        {
            if (string.IsNullOrWhiteSpace(Expression))
            {
                IsValid = false;
                ValidationMessage = "Expression cannot be empty.";
                UnknownProperties.Clear();
                return;
            }

            try
            {
                // Normalize the expression, including multi-line handling
                var normalizedExpression = NormalizeExpression(Expression);
                
                // Validate parentheses balance
                if (!AreParenthesesBalanced(normalizedExpression))
                {
                    IsValid = false;
                    ValidationMessage = "Unbalanced parentheses in the expression.";
                    UnknownProperties.Clear();
                    return;
                }

                // Tokenize the expression
                var tokens = Tokenize(normalizedExpression);
                
                // Update unknown properties
                UpdateUnknownProperties(tokens);
                
                // Validate token sequence
                var validationResult = ValidateTokenSequence(tokens);
                
                if (!validationResult.IsValid)
                {
                    IsValid = false;
                    ValidationMessage = validationResult.ErrorMessage;
                    return;
                }

                // If we get here, the expression looks syntactically valid
                IsValid = UnknownProperties.Count == 0;
                ValidationMessage = UnknownProperties.Count > 0 
                    ? $"Unknown properties: {string.Join(", ", UnknownProperties)}" 
                    : "Expression is valid.";
            }
            catch (Exception ex)
            {
                IsValid = false;
                ValidationMessage = $"Error validating expression: {ex.Message}";
                UnknownProperties.Clear();
            }
        }

        private string NormalizeExpression(string expression)
        {
            // Remove extra whitespaces, normalize operators
            var normalized = expression
                .Trim()
                .Replace("  ", " ")
                .Replace(" == ", "==")
                .Replace(" != ", "!=")
                .Replace(" >= ", ">=")
                .Replace(" <= ", "<=")
                .Replace(" > ", ">")
                .Replace(" < ", "<")
                .Replace(" AND ", " AND ")
                .Replace(" OR ", " OR ");

            // Handle multi-line expressions by adding implicit AND
            var lines = normalized.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length > 1)
            {
                // Join lines with explicit AND
                normalized = string.Join(" AND ", lines.Select(line => 
                    line.Trim().StartsWith("(") ? line.Trim() : $"({line.Trim()})"));
            }

            return normalized;
        }

        private bool AreParenthesesBalanced(string expression)
        {
            int openCount = 0;
            foreach (char c in expression)
            {
                if (c == '(') openCount++;
                if (c == ')') openCount--;
                
                if (openCount < 0) return false; // More closing than opening
            }
            return openCount == 0;
        }

        private List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            var currentToken = new StringBuilder();
            bool inQuotes = false;
            char? quoteChar = null;
            int i = 0;

            while (i < expression.Length)
            {
                char c = expression[i];

                if (inQuotes)
                {
                    currentToken.Append(c);
                    if (c == quoteChar)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                        inQuotes = false;
                        quoteChar = null;
                    }
                    i++;
                    continue;
                }

                // Handle multi-character operators
                if (c == '=' && i + 1 < expression.Length && expression[i + 1] == '=')
                {
                    // Flush any current token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    
                    // Add the == operator
                    tokens.Add("==");
                    i += 2;
                    continue;
                }

                // Handle other multi-character operators
                if ((c == '>' || c == '<') && i + 1 < expression.Length && expression[i + 1] == '=')
                {
                    // Flush any current token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    
                    // Add the >= or <= operator
                    tokens.Add(c == '>' ? ">=" : "<=");
                    i += 2;
                    continue;
                }

                // Handle single-character operators and parentheses
                if ("()><!".Contains(c))
                {
                    // Flush any current token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    
                    // Add the single-character operator or parenthesis
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                // Handle quotes
                if (c == '\'' || c == '"')
                {
                    // Flush any current token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    
                    // Start a quoted string
                    currentToken.Append(c);
                    inQuotes = true;
                    quoteChar = c;
                    i++;
                    continue;
                }

                // Handle whitespace
                if (char.IsWhiteSpace(c))
                {
                    // Flush any current token
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    
                    i++;
                    continue;
                }

                // Accumulate characters for the current token
                currentToken.Append(c);
                i++;
            }

            // Add the last token if any
            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens;
        }

        private (bool IsValid, string ErrorMessage) ValidateTokenSequence(List<string> tokens)
        {
            if (tokens.Count == 0)
                return (false, "Expression is empty.");

            var operators = new HashSet<string> 
            { 
                "==", "!=", ">", "<", ">=", "<=", 
                "CONTAINS", "STARTSWITH", "ENDSWITH", 
                "AND", "OR" 
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                // Check for unknown properties or values
                if (!IsValidToken(token))
                {
                    return (false, $"Unknown token: {token}");
                }

                // Validate token sequence
                if (i > 0)
                {
                    var prevToken = tokens[i - 1];

                    // Properties and values cannot follow each other
                    if (IsProperty(token) && IsProperty(prevToken))
                    {
                        return (false, $"Invalid sequence: {prevToken} {token}");
                    }

                    // Operators cannot follow each other, except for unary operators (future enhancement)
                    if (operators.Contains(token) && operators.Contains(prevToken))
                    {
                        // Special case for == to allow chained comparisons
                        if (!(prevToken == "==" && token == "=="))
                        {
                            return (false, $"Invalid sequence: {prevToken} {token}");
                        }
                    }
                }
            }

            return (true, "Expression is valid.");
        }

        private bool IsValidToken(string token)
        {
            // Check if token is a valid property, value, operator, or parenthesis
            return IsProperty(token) || 
                   IsValue(token) || 
                   IsOperator(token) || 
                   IsParenthesis(token);
        }

        private bool IsProperty(string token)
        {
            // Check if the token is in the list of available properties
            return AvailableProperties
                .Any(p => p.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsValue(string token)
        {
            // Check for string, number, boolean, null values
            return token.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                   IsNumeric(token) ||
                   IsQuotedString(token);
        }

        private bool IsOperator(string token)
        {
            var operators = new[] 
            { 
                "==", "!=", ">", "<", ">=", "<=", 
                "CONTAINS", "STARTSWITH", "ENDSWITH", 
                "AND", "OR" 
            };
            return operators.Contains(token.ToUpper());
        }

        private bool IsParenthesis(string token)
        {
            return token == "(" || token == ")";
        }

        private bool IsNumeric(string token)
        {
            return double.TryParse(token, out _);
        }

        private bool IsQuotedString(string token)
        {
            return (token.StartsWith("'") && token.EndsWith("'")) ||
                   (token.StartsWith("\"") && token.EndsWith("\""));
        }

        private List<string> TokenizeExpression(string expression)
        {
            var tokens = new List<string>();
            var pattern = @"[\w\.]+|>=|<=|==|!=|>|<|\(|\)|'[^']*'|\""[^\""]*\""";
            var matches = Regex.Matches(expression, pattern);
            
            foreach (Match match in matches)
            {
                var token = match.Value.Trim();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    tokens.Add(token);
                }
            }
            
            return tokens;
        }

        private bool ValidateParentheses(string expression)
        {
            int count = 0;
            foreach (char c in expression)
            {
                if (c == '(') count++;
                else if (c == ')') count--;
                
                if (count < 0) return false; // More closing than opening
            }
            return count == 0;
        }

        private bool ValidateValue(string value)
        {
            // Check if it's a quoted string
            if ((value.StartsWith("'") && value.EndsWith("'")) ||
                (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                return true;
            }

            // Check if it's a number
            if (decimal.TryParse(value, out _))
            {
                return true;
            }

            // Check if it's a boolean
            if (bool.TryParse(value, out _))
            {
                return true;
            }

            return false;
        }

        private bool ValidateLogicalOperators(List<string> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i].ToUpperInvariant();
                if (token == "AND" || token == "OR")
                {
                    // Check if it's not at the start or end
                    if (i == 0 || i == tokens.Count - 1)
                    {
                        return false;
                    }

                    // Check if it has expressions on both sides
                    var prevToken = tokens[i - 1].ToUpperInvariant();
                    var nextToken = tokens[i + 1].ToUpperInvariant();

                    if (_validOperators.Contains(prevToken) || _validOperators.Contains(nextToken))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void UpdateUnknownProperties(List<string> tokens)
        {
            // Clear previous unknown properties
            UnknownProperties.Clear();

            // Check each token that might be a property
            foreach (var token in tokens)
            {
                // Only check tokens that look like properties (not operators, values, or parentheses)
                if (IsLikelyPropertyName(token) && !IsProperty(token))
                {
                    // Add to unknown properties if not already present
                    if (!UnknownProperties.Contains(token))
                    {
                        UnknownProperties.Add(token);
                    }
                }
            }
        }

        private bool IsLikelyPropertyName(string token)
        {
            // Exclude known non-property tokens
            if (IsOperator(token) || 
                IsValue(token) || 
                IsParenthesis(token))
            {
                return false;
            }

            // Check if the token looks like a valid property name
            return System.Text.RegularExpressions.Regex.IsMatch(token, @"^[a-zA-Z][a-zA-Z0-9]*$");
        }

        public Func<object, bool> CompileExpression()
        {
            if (string.IsNullOrWhiteSpace(Expression))
            {
                return _ => false;
            }

            try
            {
                // Normalize the expression
                var normalizedExpression = NormalizeExpression(Expression);

                // Create a lambda that uses dynamic property access
                return CreateDynamicExpressionFunc(normalizedExpression);
            }
            catch (Exception ex)
            {
                // Log or handle compilation error
                ValidationMessage = $"Error compiling expression: {ex.Message}";
                return _ => false;
            }
        }

        private Func<object, bool> CreateDynamicExpressionFunc(string normalizedExpression)
        {
            // Tokenize the normalized expression
            var tokens = Tokenize(normalizedExpression);

            // Return a function that evaluates the expression
            return (input) => EvaluateExpression(tokens, input);
        }

        private bool EvaluateExpression(List<string> tokens, object input)
        {
            bool currentResult = true;
            string currentLogicalOperator = "AND";

            for (int i = 0; i < tokens.Count; i++)
            {
                // Skip logical operators
                if (tokens[i] == "AND" || tokens[i] == "OR")
                {
                    currentLogicalOperator = tokens[i];
                    continue;
                }

                // Parse and evaluate the condition
                bool conditionResult = EvaluateCondition(tokens, ref i, input);

                // Apply logical operator
                if (currentLogicalOperator == "AND")
                {
                    currentResult &= conditionResult;
                }
                else // OR
                {
                    currentResult |= conditionResult;
                }
            }

            return currentResult;
        }

        private bool EvaluateCondition(List<string> tokens, ref int index, object input)
        {
            // Handle parenthesized expressions
            if (tokens[index] == "(")
            {
                // Find matching closing parenthesis
                int closingIndex = FindClosingParenthesis(tokens, index);
                var innerTokens = tokens.GetRange(index + 1, closingIndex - index - 1);
                
                index = closingIndex;
                return EvaluateExpression(innerTokens, input);
            }

            // Ensure we have enough tokens for a condition
            if (index + 2 >= tokens.Count)
                throw new InvalidOperationException("Incomplete condition");

            string propertyName = tokens[index];
            string op = tokens[index + 1];
            string value = tokens[index + 2];

            index += 2;
            return EvaluateComparison(input, propertyName, op, value);
        }

        private bool EvaluateComparison(object input, string propertyName, string op, string value)
        {
            // Get the property value dynamically
            object propertyValue = GetPropertyValue(input, propertyName);

            // Remove quotes from string values
            value = value.Trim('\'', '"');

            // Convert value to appropriate type
            object convertedValue = ConvertValue(value, propertyValue?.GetType() ?? typeof(string));

            // Perform comparison
            return op.ToUpper() switch
            {
                "==" => Equals(propertyValue, convertedValue),
                "!=" => !Equals(propertyValue, convertedValue),
                ">" => CompareValues(propertyValue, convertedValue) > 0,
                "<" => CompareValues(propertyValue, convertedValue) < 0,
                ">=" => CompareValues(propertyValue, convertedValue) >= 0,
                "<=" => CompareValues(propertyValue, convertedValue) <= 0,
                "CONTAINS" => propertyValue?.ToString().Contains(convertedValue?.ToString() ?? "") ?? false,
                "STARTSWITH" => propertyValue?.ToString().StartsWith(convertedValue?.ToString() ?? "") ?? false,
                "ENDSWITH" => propertyValue?.ToString().EndsWith(convertedValue?.ToString() ?? "") ?? false,
                _ => throw new InvalidOperationException($"Unsupported operator: {op}")
            };
        }

        private object GetPropertyValue(object input, string propertyName)
        {
            if (input == null)
                return null;

            // Handle nested properties (e.g., "Address.City")
            var parts = propertyName.Split('.');
            object currentValue = input;

            foreach (var part in parts)
            {
                if (currentValue == null)
                    return null;

                var type = currentValue.GetType();
                var property = type.GetProperty(part, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property == null)
                    throw new ArgumentException($"Property '{part}' not found on type {type.Name}");

                currentValue = property.GetValue(currentValue);
            }

            return currentValue;
        }

        private int CompareValues(object a, object b)
        {
            // Handle null cases
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            // Use IComparable if available
            if (a is IComparable comparable)
            {
                return comparable.CompareTo(b);
            }

            // Fallback to string comparison
            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        private object ConvertValue(string value, Type targetType)
        {
            // Remove quotes
            value = value.Trim('\'', '"');

            // Convert to target type
            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(int))
                return int.Parse(value);

            if (targetType == typeof(double))
                return double.Parse(value);

            if (targetType == typeof(bool))
                return bool.Parse(value);

            if (targetType == typeof(decimal))
                return decimal.Parse(value);

            // Add more type conversions as needed
            return value;
        }

        private int FindClosingParenthesis(List<string> tokens, int startIndex)
        {
            int depth = 0;
            for (int i = startIndex; i < tokens.Count; i++)
            {
                if (tokens[i] == "(") depth++;
                if (tokens[i] == ")") depth--;
                
                if (depth == 0) return i;
            }
            throw new InvalidOperationException("Unbalanced parentheses");
        }

        public void SetTargetObject<T>(T example)
        {
            AvailableProperties.Clear();
            _targetType = typeof(T);

            foreach (var prop in _targetType.GetProperties())
            {
                AvailableProperties.Add(new Models.RulePropertyInfo 
                { 
                    Name = prop.Name,
                    Type = prop.PropertyType,                    
                });
            }
        }

        private int GetPrecedence(string op)
        {
            return op.ToUpperInvariant() switch
            {
                "OR" => 1,
                "AND" => 2,
                "==" or "!=" => 3,
                ">" or "<" or ">=" or "<=" => 4,
                "CONTAINS" or "STARTSWITH" or "ENDSWITH" => 5,
                _ => 0
            };
        }

        private bool IsComparisonOperator(string token)
        {
            var comparisonOperators = new[] 
            { 
                "==", "!=", ">", "<", ">=", "<=", 
                "CONTAINS", "STARTSWITH", "ENDSWITH" 
            };
            return comparisonOperators.Contains(token.ToUpper());
        }

        private bool IsLogicalOperator(string token)
        {
            var logicalOperators = new[] { "AND", "OR" };
            return logicalOperators.Contains(token.ToUpper());
        }
    }
}