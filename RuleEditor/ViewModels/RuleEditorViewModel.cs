using System;
using System.Collections.Concurrent;
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
        private string _expressionCode;
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
            ExpressionCode = string.Empty;
            ExpressionDocument = new TextDocument();
            FormatExpressionCommand = new ActionCommand(ExecuteFormatExpression);
        }

        public Rule CurrentRule
        {
            get => _currentRule;
            set => SetProperty(ref _currentRule, value);
        }

        public Type TargetType => _targetType;

        public string ExpressionCode
        {
            get => _expressionCode;
            set
            {
                if (SetProperty(ref _expressionCode, value))
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
            if (string.IsNullOrWhiteSpace(ExpressionCode)) return;

            try
            {
                var tokens = TokenizeExpression(ExpressionCode);
                var formattedExpression = FormatTokens(tokens);
                ExpressionCode = formattedExpression;
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
            if (string.IsNullOrWhiteSpace(ExpressionCode))
            {
                IsValid = false;
                ValidationMessage = "Expression cannot be empty.";
                UnknownProperties.Clear();
                return;
            }

            try
            {
                // Normalize the expression, including multi-line handling
                var normalizedExpression = NormalizeExpression(ExpressionCode);
                
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

        private static readonly ConcurrentDictionary<string, Func<object, bool>> _expressionCache 
            = new ConcurrentDictionary<string, Func<object, bool>>();

        private const int MAX_EXPRESSION_CACHE_SIZE = 1000;

        public Func<object, bool> CompileExpression()
        {
            if (string.IsNullOrWhiteSpace(ExpressionCode))
            {
                return _ => false;
            }

            // Normalize and cache the expression
            var normalizedExpression = NormalizeExpression(ExpressionCode);
            
            return _expressionCache.GetOrAdd(
                normalizedExpression, 
                CreateHighPerformanceExpressionFunc
            );
        }

        private Func<object, bool> CreateHighPerformanceExpressionFunc(string normalizedExpression)
        {
            try
            {
                // Limit cache size
                if (_expressionCache.Count > MAX_EXPRESSION_CACHE_SIZE)
                {
                    // Remove oldest entries
                    while (_expressionCache.Count > MAX_EXPRESSION_CACHE_SIZE)
                    {
                        _expressionCache.TryRemove(_expressionCache.Keys.First(), out _);
                    }
                }

                // Tokenize the normalized expression
                var tokens = Tokenize(normalizedExpression);

                
                // Create parameter for input object
                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(object), "input");
                
                // Convert parameter to the target type
                var convertedParam = System.Linq.Expressions.Expression.Convert(parameter, TargetType);

                // Build the full expression tree
                var expressionTree = BuildOptimizedExpressionTree(tokens, convertedParam);

                // Compile to a lambda function
                return System.Linq.Expressions.Expression.Lambda<Func<object, bool>>(expressionTree, parameter).Compile();
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error compiling expression: {ex.Message}";
                return _ => false;
            }
        }

        private System.Linq.Expressions.Expression BuildOptimizedExpressionTree(
            List<string> tokens, 
            System.Linq.Expressions.Expression parameter)
        {
            // Build the expression tree
            System.Linq.Expressions.Expression currentExpression = 
                System.Linq.Expressions.Expression.Constant(true);

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] == "AND" || tokens[i] == "OR")
                {
                    continue;
                }

                // Parse the current condition
                var condition = ParseOptimizedCondition(tokens, ref i, parameter);

                // Combine with previous expression using logical operators
                currentExpression = i > 0 && tokens[i - 1] == "AND"
                    ? System.Linq.Expressions.Expression.AndAlso(currentExpression, condition)
                    : System.Linq.Expressions.Expression.OrElse(currentExpression, condition);
            }

            return currentExpression;
        }

        private System.Linq.Expressions.Expression ParseOptimizedCondition(
            List<string> tokens, 
            ref int index, 
            System.Linq.Expressions.Expression parameter)
        {
            // Handle parenthesized expressions
            if (tokens[index] == "(")
            {
                // Find the matching closing parenthesis
                int closingIndex = FindClosingParenthesis(tokens, index);
                var innerTokens = tokens.GetRange(index + 1, closingIndex - index - 1);
                
                index = closingIndex;
                return BuildOptimizedExpressionTree(innerTokens, 
                    (System.Linq.Expressions.ParameterExpression)parameter);
            }

            // Ensure we have enough tokens for a condition
            if (index + 2 >= tokens.Count)
                throw new InvalidOperationException("Incomplete condition");

            var property = tokens[index];
            var op = tokens[index + 1];
            var value = tokens[index + 2];

            index += 2;
            return BuildOptimizedComparisonExpression(property, op, value, parameter);
        }

        private System.Linq.Expressions.Expression BuildOptimizedComparisonExpression(
            string propertyName, 
            string op, 
            string value, 
            System.Linq.Expressions.Expression parameter)
        {
            // Get property access expression
            var propertyExpression = CreatePropertyAccessExpression(parameter, propertyName);
        
            // Convert value
            var convertedValue = ConvertValueExpression(value, propertyExpression.Type);

            // Build comparison expression
            return op.ToUpper() switch
            {
                "==" => System.Linq.Expressions.Expression.Equal(propertyExpression, convertedValue),
                "!=" => System.Linq.Expressions.Expression.NotEqual(propertyExpression, convertedValue),
                ">" => System.Linq.Expressions.Expression.GreaterThan(propertyExpression, convertedValue),
                "<" => System.Linq.Expressions.Expression.LessThan(propertyExpression, convertedValue),
                ">=" => System.Linq.Expressions.Expression.GreaterThanOrEqual(propertyExpression, convertedValue),
                "<=" => System.Linq.Expressions.Expression.LessThanOrEqual(propertyExpression, convertedValue),
                "CONTAINS" => BuildStringMethodExpression(propertyExpression, "Contains", convertedValue),
                "STARTSWITH" => BuildStringMethodExpression(propertyExpression, "StartsWith", convertedValue),
                "ENDSWITH" => BuildStringMethodExpression(propertyExpression, "EndsWith", convertedValue),
                _ => throw new InvalidOperationException($"Unsupported operator: {op}")
            };
        }

        private System.Linq.Expressions.Expression CreatePropertyAccessExpression(
            System.Linq.Expressions.Expression parameter, 
            string propertyName)
        {
            // Handle nested properties
            var parts = propertyName.Split('.');
            System.Linq.Expressions.Expression currentExpression = parameter;

            foreach (var part in parts)
            {
                var property = currentExpression.Type.GetProperty(part, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property == null)
                    throw new ArgumentException($"Property '{part}' not found on type {currentExpression.Type.Name}");

                currentExpression = System.Linq.Expressions.Expression.Property(currentExpression, property);
            }

            return currentExpression;
        }

        private System.Linq.Expressions.Expression ConvertValueExpression(
            string value, 
            Type targetType)
        {
            // Remove quotes
            value = value.Trim('\'', '"');

            // Convert to constant expression of the correct type
            object convertedValue = ConvertValue(value, targetType);
            return System.Linq.Expressions.Expression.Constant(convertedValue, targetType);
        }

        private System.Linq.Expressions.Expression BuildStringMethodExpression(
            System.Linq.Expressions.Expression propertyExpression, 
            string methodName, 
            System.Linq.Expressions.Expression valueExpression)
        {
            // Ensure property is a string
            if (propertyExpression.Type != typeof(string))
                throw new InvalidOperationException($"Cannot use {methodName} on non-string property");

            // Call string method
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });
            return System.Linq.Expressions.Expression.Call(propertyExpression, method, valueExpression);
        }

        private object ConvertValue(string value, Type targetType)
        {
            // Handle null or empty string
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            // Convert to target type
            try
            {
                // Handle nullable types
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                // Special handling for different types
                if (underlyingType == typeof(string))
                    return value;

                if (underlyingType == typeof(int) || underlyingType == typeof(Int32))
                    return Convert.ToInt32(value);

                if (underlyingType == typeof(double) || underlyingType == typeof(Double))
                    return Convert.ToDouble(value);

                if (underlyingType == typeof(bool) || underlyingType == typeof(Boolean))
                    return Convert.ToBoolean(value);

                if (underlyingType == typeof(decimal))
                    return Convert.ToDecimal(value);

                if (underlyingType == typeof(DateTime))
                    return Convert.ToDateTime(value);

                if (underlyingType.IsEnum)
                    return Enum.Parse(underlyingType, value, true);

                // Fallback to default conversion
                return Convert.ChangeType(value, underlyingType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot convert '{value}' to {targetType.Name}: {ex.Message}", ex);
            }
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

        private int FindClosingParenthesis(List<string> tokens, int startIndex)
        {
            int depth = 0;
            for (int i = startIndex; i < tokens.Count; i++)
            {
                if (tokens[i] == "(") depth++;
                if (tokens[i] == ")") depth--;
                
                if (depth == 0) return i;
            }
            throw new InvalidOperationException("Unbalanced parentheses in expression");
        }
    }
}