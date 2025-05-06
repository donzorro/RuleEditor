using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RuleEditor.Models;

namespace RuleEditor.ViewModels.Version3
{
    public class ExpressionCompiler
    {
        private readonly ExpressionParser _parser;
        private readonly List<RulePropertyInfo> _availableProperties;

        public ExpressionCompiler(List<RulePropertyInfo> availableProperties)
        {
            _availableProperties = availableProperties ?? new List<RulePropertyInfo>();
            _parser = new ExpressionParser(_availableProperties);
        }

        public Func<T, bool> CompileExpression<T>(string expressionText) where T : class
        {
            if (string.IsNullOrWhiteSpace(expressionText))
                return _ => true; // Empty expression always returns true

            var tokens = _parser.Tokenize(expressionText);
            if (!_parser.ValidateSyntax(tokens))
                throw new ArgumentException("Invalid expression syntax");

            // Create parameter expression for the input object
            var parameter = Expression.Parameter(typeof(T), "obj");
            
            // Build the expression tree
            var expression = BuildExpression(tokens, parameter);
            
            // Compile the expression to a delegate
            return Expression.Lambda<Func<T, bool>>(expression, parameter).Compile();
        }

        private Expression BuildExpression(List<Token> tokens, ParameterExpression parameter)
        {
            if (tokens.Count == 0)
                return Expression.Constant(true);

            // Handle parenthesized expressions
            return ParseLogicalExpression(tokens, 0, tokens.Count - 1, parameter);
        }

        private Expression ParseLogicalExpression(List<Token> tokens, int startIndex, int endIndex, ParameterExpression parameter)
        {
            if (startIndex > endIndex)
                return Expression.Constant(true);

            // Find logical operators at the current level (not inside parentheses)
            var logicalOperatorIndices = FindLogicalOperatorsAtCurrentLevel(tokens, startIndex, endIndex);
            
            if (logicalOperatorIndices.Count == 0)
            {
                // No logical operators at this level, check for parentheses
                if (tokens[startIndex].TokenType == TokenType.OpenParenthesis && tokens[endIndex].TokenType == TokenType.CloseParenthesis)
                {
                    // Remove outer parentheses and parse the inner expression
                    return ParseLogicalExpression(tokens, startIndex + 1, endIndex - 1, parameter);
                }
                
                // This should be a simple condition (Property Operator Value)
                return ParseCondition(tokens, startIndex, endIndex, parameter);
            }

            // Process logical operators in the correct order (AND before OR)
            var andOperators = logicalOperatorIndices.Where(i => tokens[i].Value.Equals("AND", StringComparison.OrdinalIgnoreCase)).ToList();
            var orOperators = logicalOperatorIndices.Where(i => tokens[i].Value.Equals("OR", StringComparison.OrdinalIgnoreCase)).ToList();
            
            // Process all AND operators first
            Expression result = null;
            int currentStart = startIndex;
            
            foreach (var andIndex in andOperators.OrderBy(i => i))
            {
                var leftExpression = ParseLogicalExpression(tokens, currentStart, andIndex - 1, parameter);
                
                if (result == null)
                    result = leftExpression;
                else
                    result = Expression.AndAlso(result, leftExpression);
                
                currentStart = andIndex + 1;
            }
            
            // Process the last segment after the last AND
            if (andOperators.Count > 0)
            {
                var lastAndIndex = andOperators.Max();
                var rightExpression = ParseLogicalExpression(tokens, lastAndIndex + 1, endIndex, parameter);
                result = result == null ? rightExpression : Expression.AndAlso(result, rightExpression);
            }
            
            // Process all OR operators
            currentStart = startIndex;
            Expression orResult = null;
            
            foreach (var orIndex in orOperators.OrderBy(i => i))
            {
                var leftExpression = ParseLogicalExpression(tokens, currentStart, orIndex - 1, parameter);
                
                if (orResult == null)
                    orResult = leftExpression;
                else
                    orResult = Expression.OrElse(orResult, leftExpression);
                
                currentStart = orIndex + 1;
            }
            
            // Process the last segment after the last OR
            if (orOperators.Count > 0)
            {
                var lastOrIndex = orOperators.Max();
                var rightExpression = ParseLogicalExpression(tokens, lastOrIndex + 1, endIndex, parameter);
                orResult = orResult == null ? rightExpression : Expression.OrElse(orResult, rightExpression);
            }
            
            // Combine AND and OR results
            if (result != null && orResult != null)
                return Expression.OrElse(result, orResult);
            
            return result ?? orResult ?? Expression.Constant(true);
        }

        private List<int> FindLogicalOperatorsAtCurrentLevel(List<Token> tokens, int startIndex, int endIndex)
        {
            var result = new List<int>();
            int parenthesisLevel = 0;
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (tokens[i].TokenType == TokenType.OpenParenthesis)
                    parenthesisLevel++;
                else if (tokens[i].TokenType == TokenType.CloseParenthesis)
                    parenthesisLevel--;
                else if (tokens[i].TokenType == TokenType.LogicalOperator && parenthesisLevel == 0)
                    result.Add(i);
            }
            
            return result;
        }

        private Expression ParseCondition(List<Token> tokens, int startIndex, int endIndex, ParameterExpression parameter)
        {
            // Simple condition should have the form: Property Operator Value
            if (endIndex - startIndex < 2)
                throw new ArgumentException($"Invalid condition at position {tokens[startIndex].Position}");

            var propertyToken = tokens[startIndex];
            var operatorToken = tokens[startIndex + 1];
            var valueToken = tokens[startIndex + 2];

            if (propertyToken.TokenType != TokenType.Property || operatorToken.TokenType != TokenType.Operator || valueToken.TokenType != TokenType.Value)
                throw new ArgumentException($"Invalid condition format at position {tokens[startIndex].Position}");

            // Get property info
            var propertyInfo = _availableProperties.FirstOrDefault(p => 
                p.Name.Equals(propertyToken.Value, StringComparison.OrdinalIgnoreCase));
            
            if (propertyInfo == null)
                throw new ArgumentException($"Unknown property: {propertyToken.Value}");

            // Create property access expression
            var propertyAccess = Expression.Property(parameter, propertyInfo.Name);
            
            // Parse the value
            var parsedValue = ParseValue(valueToken.Value, propertyInfo.Type);
            var valueExpression = Expression.Constant(parsedValue, propertyInfo.Type);
            
            // Create the comparison expression based on the operator
            return CreateComparisonExpression(propertyAccess, operatorToken.Value, valueExpression, propertyInfo.Type);
        }

        private object ParseValue(string value, Type targetType)
        {
            // Remove quotes from string values
            if ((value.StartsWith("'") && value.EndsWith("'")) || 
                (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                value = value.Substring(1, value.Length - 2);
            }

            // Convert the value to the target type
            if (targetType == typeof(string))
                return value;
            else if (targetType == typeof(bool))
                return bool.Parse(value);
            else if (targetType == typeof(int))
                return int.Parse(value);
            else if (targetType == typeof(decimal))
                return decimal.Parse(value);
            else if (targetType == typeof(DateTime))
                return DateTime.Parse(value);
            else
                throw new ArgumentException($"Unsupported type: {targetType.Name}");
        }

        private Expression CreateComparisonExpression(MemberExpression propertyAccess, string operatorValue, ConstantExpression valueExpression, Type propertyType)
        {
            // Handle string-specific operators
            if (propertyType == typeof(string))
            {
                switch (operatorValue.ToUpper())
                {
                    case "CONTAINS":
                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                        return Expression.Call(propertyAccess, containsMethod, valueExpression);
                        
                    case "STARTSWITH":
                        var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                        return Expression.Call(propertyAccess, startsWithMethod, valueExpression);
                        
                    case "ENDSWITH":
                        var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
                        return Expression.Call(propertyAccess, endsWithMethod, valueExpression);
                }
            }

            // Handle standard comparison operators
            switch (operatorValue)
            {
                case "==":
                    return Expression.Equal(propertyAccess, valueExpression);
                case "!=":
                    return Expression.NotEqual(propertyAccess, valueExpression);
                case ">":
                    return Expression.GreaterThan(propertyAccess, valueExpression);
                case "<":
                    return Expression.LessThan(propertyAccess, valueExpression);
                case ">=":
                    return Expression.GreaterThanOrEqual(propertyAccess, valueExpression);
                case "<=":
                    return Expression.LessThanOrEqual(propertyAccess, valueExpression);
                default:
                    throw new ArgumentException($"Unsupported operator: {operatorValue}");
            }
        }
    }
}