using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Xaml.Behaviors.Core;
using RuleEditor.Models;

namespace RuleEditor.ViewModels
{   

    public class RuleEditorViewModel : ViewModelBase
    {
        private Rule _currentRule;
        private string _expression;
        private string _validationMessage;
        private bool _isValid;
        private ObservableCollection<PropertyInfo> _availableProperties;
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
            AvailableProperties = new ObservableCollection<PropertyInfo>();
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

        public ObservableCollection<PropertyInfo> AvailableProperties
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
                // Remove extra whitespaces and normalize the expression
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
            return expression
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
                if (count < 0) return false;
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
            if (!IsValid)
                throw new InvalidOperationException("Cannot compile invalid expression");

            if (_targetType == null)
                throw new InvalidOperationException("Target type not set. Call SetTargetObject first.");

            var tokens = TokenizeExpression(Expression);
            var parameter = System.Linq.Expressions.Expression.Parameter(_targetType, "item");
            var expr = BuildExpression(tokens, parameter);

            // Create a generic delegate type
            var delegateType = typeof(Func<,>).MakeGenericType(_targetType, typeof(bool));
            
            // Create the lambda expression with the correct delegate type
            var lambda = System.Linq.Expressions.Expression.Lambda(delegateType, expr, parameter);
            
            // Compile and return as Func<object, bool>
            var compiled = lambda.Compile();
            
            // Create a wrapper that accepts object and casts it to the correct type
            return obj =>
            {
                if (obj == null) return false;
                if (!_targetType.IsInstanceOfType(obj))
                    throw new ArgumentException($"Object must be of type {_targetType.Name}");
                
                return (bool)compiled.DynamicInvoke(obj);
            };
        }

        private Expression BuildExpression(List<string> tokens, ParameterExpression parameter)
        {
            var stack = new Stack<Expression>();
            var operators = new Stack<string>();

            foreach (var token in tokens)
            {
                if (token == "(")
                {
                    operators.Push(token);
                }
                else if (token == ")")
                {
                    while (operators.Count > 0 && operators.Peek() != "(")
                    {
                        ApplyOperator(stack, operators.Pop());
                    }
                    operators.Pop(); // Remove "("
                }
                else if (IsOperator(token))
                {
                    while (operators.Count > 0 && GetPrecedence(operators.Peek()) >= GetPrecedence(token))
                    {
                        ApplyOperator(stack, operators.Pop());
                    }
                    operators.Push(token);
                }
                else
                {
                    stack.Push(CreatePropertyOrConstantExpression(token, parameter));
                }
            }

            while (operators.Count > 0)
            {
                ApplyOperator(stack, operators.Pop());
            }

            return stack.Pop();
        }

        private Expression CreatePropertyOrConstantExpression(string token, ParameterExpression parameter)
        {
            // Check if it's a property
            var property = AvailableProperties.FirstOrDefault(p => p.Name == token);
            if (property != null)
            {
                return System.Linq.Expressions.Expression.Property(parameter, property.Name);
            }

            // Check if it's a string literal
            if ((token.StartsWith("'") && token.EndsWith("'")) ||
                (token.StartsWith("\"") && token.EndsWith("\"")))
            {
                var stringValue = token.Substring(1, token.Length - 2);
                return System.Linq.Expressions.Expression.Constant(stringValue);
            }

            // Check if it's a number
            if (decimal.TryParse(token, out decimal number))
            {
                return System.Linq.Expressions.Expression.Constant(number);
            }

            // Check if it's a boolean
            if (bool.TryParse(token, out bool boolean))
            {
                return System.Linq.Expressions.Expression.Constant(boolean);
            }

            throw new InvalidOperationException($"Invalid token: {token}");
        }

        private void ApplyOperator(Stack<Expression> stack, string op)
        {
            op = op.ToUpperInvariant();
            Expression right = stack.Pop();
            Expression left = stack.Pop();

            Expression result = op switch
            {
                ">" => System.Linq.Expressions.Expression.GreaterThan(left, right),
                "<" => System.Linq.Expressions.Expression.LessThan(left, right),
                ">=" => System.Linq.Expressions.Expression.GreaterThanOrEqual(left, right),
                "<=" => System.Linq.Expressions.Expression.LessThanOrEqual(left, right),
                "==" => System.Linq.Expressions.Expression.Equal(left, right),
                "!=" => System.Linq.Expressions.Expression.NotEqual(left, right),
                "AND" => System.Linq.Expressions.Expression.AndAlso(left, right),
                "OR" => System.Linq.Expressions.Expression.OrElse(left, right),
                "CONTAINS" => CreateStringMethodCall(left, right, "Contains"),
                "STARTSWITH" => CreateStringMethodCall(left, right, "StartsWith"),
                "ENDSWITH" => CreateStringMethodCall(left, right, "EndsWith"),
                _ => throw new InvalidOperationException($"Unknown operator: {op}")
            };

            stack.Push(result);
        }

        private Expression CreateStringMethodCall(Expression left, Expression right, string methodName)
        {
            // Convert the right expression to string if needed
            if (right.Type != typeof(string))
            {
                right = System.Linq.Expressions.Expression.Call(right, right.Type.GetMethod("ToString", Type.EmptyTypes));
            }

            // Handle string method calls
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) });
            if (method == null)
            {
                throw new InvalidOperationException($"String method not found: {methodName}");
            }

            // If left is not a string, call ToString()
            if (left.Type != typeof(string))
            {
                left = System.Linq.Expressions.Expression.Call(left, left.Type.GetMethod("ToString", Type.EmptyTypes));
            }

            return System.Linq.Expressions.Expression.Call(left, method, right);
        }

        public void SetTargetObject<T>(T example)
        {
            AvailableProperties.Clear();
            _targetType = typeof(T);

            foreach (var prop in _targetType.GetProperties())
            {
                AvailableProperties.Add(new PropertyInfo 
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