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
        public TokenType TokenType { get; set; }
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
            TokenType = type;
            Position = position;
            Length = value?.Length ?? 0;
            PossibleTypes = possibleTypes ?? new[] { type };
            PossibleValues = possibleValues;
        }

        public override string ToString() => Value;
    }

    public class ExpressionParser
    {
        private static readonly List<string> _logicalOperators = new List<string> { "AND", "OR", "NOT" };

        // Expose logical operators as a public property
        public static IReadOnlyList<string> LogicalOperators => _logicalOperators;

        private List<RulePropertyInfo> AvailableProperties 
        {
            get; set; 
        
        }
        private static readonly List<string> ComparisonOperators = new List<string> { "==", "!=", ">", "<", ">=", "<=", "CONTAINS", "STARTSWITH", "ENDSWITH" };

        private TokenType _expectedNextTokenType;

        public ExpressionParser(List<RulePropertyInfo> availableProperties)
        {
            AvailableProperties = availableProperties ?? new List<RulePropertyInfo>();
        }

        public List<string> GetExpectedNextTokenSuggestions(List<Token> Tokens, int CaretPosition)
        {
            // If there are no tokens yet or we're at the end, suggest properties or logical operators
            if (Tokens.Count == 0)
            {
                return AvailableProperties.Select(p => p.Name).ToList();
            }

            // Find the last token before the caret
            var lastToken = Tokens.LastOrDefault(t => t.Position < CaretPosition);
            if (lastToken == null)
            {
                return AvailableProperties.Select(p => p.Name).ToList();
            }

            // Special case for numeric values - if we're at the end of a number with no space,
            // don't show any suggestions (user might still be typing the number)
            if (lastToken.TokenType == TokenType.Value &&
                decimal.TryParse(lastToken.Value, out _) &&
                CaretPosition == lastToken.Position + lastToken.Length)
            {
                return new List<string>(); // Return empty list to indicate no suggestions
            }

            // --- FIX: After a value or close parenthesis, only suggest logical operators ---
            if ((lastToken.TokenType == TokenType.Value || lastToken.TokenType == TokenType.CloseParenthesis)
                && CaretPosition >= lastToken.Position + lastToken.Length)
            {
                return new List<string> { "AND", "OR" };
            }

            // Suggest based on the last token type
            switch (lastToken.TokenType)
            {
                case TokenType.Property:
                    // After a property, suggest operators
                    var propInfo = AvailableProperties
                        .FirstOrDefault(p => p.Name.Equals(lastToken.Value, StringComparison.OrdinalIgnoreCase));

                    return propInfo != null
                        ? GetValidOperatorsForType(propInfo.Type)
                        : new List<string>();

                case TokenType.Operator:
                    // After an operator, suggest values
                    var prevPropToken = Tokens
                        .LastOrDefault(t => t.Position < lastToken.Position && t.TokenType == TokenType.Property);

                    if (prevPropToken != null)
                    {
                        var prop = AvailableProperties
                            .FirstOrDefault(p => p.Name.Equals(prevPropToken.Value, StringComparison.OrdinalIgnoreCase));

                        // Special case for Friends property
                        if (prop != null && prop.AllowedValues != null)
                        {
                            // Just return the full list of formatted friend suggestions
                            return prop.AllowedValues.ToList();
                        }

                       return prop != null
                            ? GetCommonValuesForType(Tokens, "")
                            : new List<string>();
                    }
                    return new List<string>();

                case TokenType.Value:
                case TokenType.CloseParenthesis:
                    // Already handled above, but keep for completeness
                    return new List<string> { "AND", "OR" };

                case TokenType.LogicalOperator:
                    // After a logical operator, suggest properties or opening parenthesis
                    return AvailableProperties.Select(p => p.Name)
                        .Concat(new[] { "(" })
                        .ToList();

                case TokenType.OpenParenthesis:
                    // After an opening parenthesis, suggest properties
                    return AvailableProperties.Select(p => p.Name).ToList();

                default:
                    return new List<string>();
            }
        }

        private List<string> GetValidOperatorsForType(Type type)
        {
            var operators = new List<string> { "==", "!=" };

            if (type == typeof(string))
            {
                operators.AddRange(new[] { "CONTAINS", "STARTSWITH", "ENDSWITH" });
            }

            if (type == typeof(int) || type == typeof(decimal) || type == typeof(DateTime))
            {
                operators.AddRange(new[] { ">", "<", ">=", "<=" });
            }

            return operators;
        }

        private List<string> GetCommonValuesForType(List<Token> Tokens, string prefix = "")
        {
            // Get the property info by checking the token values
            if (Tokens.Count >= 2)
            {
                var lastOperatorToken = Tokens.LastOrDefault(t => t.TokenType == TokenType.Operator);
                var propertyToken = Tokens.LastOrDefault(t =>
                    t.Position < (lastOperatorToken?.Position ?? 0) &&
                    t.TokenType == TokenType.Property);

                if (propertyToken != null)
                {
                    // Check if this property has restricted values
                    var propInfo = AvailableProperties
                        .FirstOrDefault(p => p.Name.Equals(propertyToken.Value, StringComparison.OrdinalIgnoreCase));

                    if (propInfo != null)
                        if (propInfo.AllowedValues != null)
                        {
                            // Return the allowed values for this property
                            return propInfo.AllowedValues.ToList();
                        }
                        else
                        {
                            if (propInfo.Type == typeof(string))
                            {
                                // For strings, suggest using quotes
                                if (!prefix.StartsWith("'") && !prefix.StartsWith("\""))
                                {
                                    return new List<string> { "'" };
                                }
                            }
                            else if (propInfo.Type == typeof(int))
                            {
                                // For integers, suggest numeric values
                                return new List<string> { "0", "1", "10", "100" }
                                    .Where(v => string.IsNullOrEmpty(prefix) || v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }
                            else if (propInfo.Type == typeof(bool))
                            {
                                // For booleans, always return both true/false (no prefix filtering for complete values)
                                return new List<string> { "true", "false" };
                            }

                        }
                }
            }

            // If no specific allowed values, return default values for the type
           

            //if (type == typeof(int))
            //{
            //    return new List<string> { "0", "1", "10", "100" }
            //        .Where(v => string.IsNullOrEmpty(prefix) || v.StartsWith(prefix))
            //        .ToList();
            //}

            //if (type == typeof(string))
            //{
            //    // For strings, suggest using quotes
            //    if (!prefix.StartsWith("'") && !prefix.StartsWith("\""))
            //    {
            //        return new List<string> { "'" };
            //    }
            //}

            return new List<string>();
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

                    var newValueToken = new Token(quotedValue, TokenType.Value, position);

                    var property = tokens.Where(q => q.TokenType == TokenType.Property).LastOrDefault();
                    newValueToken.PossibleValues = GetCommonValuesForType(tokens, quotedValue);

                    tokens.Add(newValueToken);
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

                    var newToken = new Token(part, type, position);

                    switch (type)
                    {
                        case TokenType.Property:
                            newToken.PossibleValues = AvailableProperties
                                .Select(p => p.Name)
                                .ToList();
                            break;

                        case TokenType.LogicalOperator:
                            newToken.PossibleValues = LogicalOperators;
                            break;

                        case TokenType.Operator:
                            var property = tokens.Where(q => q.TokenType == TokenType.Property).LastOrDefault();
                            var propInfo = AvailableProperties.FirstOrDefault(p => p.Name.Equals(property.Value, StringComparison.OrdinalIgnoreCase));
                            newToken.PossibleValues = GetValidOperatorsForType(propInfo.Type);
                            break;

                        case TokenType.Value:
                            // Set PossibleValues for value tokens based on the property type
                            newToken.PossibleValues = GetCommonValuesForType(tokens, part);
                            break;
                    }

                    tokens.Add(newToken);
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
                if (prev.TokenType == TokenType.Value || prev.TokenType == TokenType.CloseParenthesis)
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
            if (AvailableProperties.Any(p => p.Name.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
                return TokenType.Property;

            // If the previous token was an operator, this is likely a value
            if (existingTokens.Count > 0 &&
                (existingTokens.Last().TokenType == TokenType.Operator ||
                 existingTokens.Last().TokenType == TokenType.LogicalOperator))
                return TokenType.Value;

            // Check if it's a full comparison operator
            if (ComparisonOperators.Contains(word, StringComparer.OrdinalIgnoreCase))
                return TokenType.Operator;

            // NEW: Check if it's a prefix of any comparison operator
            if (ComparisonOperators.Any(op => op.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
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
                if (token.TokenType == TokenType.OpenParenthesis)
                    parenthesisCount++;
                else if (token.TokenType == TokenType.CloseParenthesis)
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
                switch (current.TokenType)
                {
                    case TokenType.Property:
                        if (next == null || next.TokenType != TokenType.Operator)
                            return false;
                        break;

                    case TokenType.Operator:
                        if (next == null || next.TokenType != TokenType.Value)
                            return false;
                        break;

                    case TokenType.Value:
                        if (next != null && next.TokenType != TokenType.LogicalOperator && next.TokenType != TokenType.CloseParenthesis)
                            return false;
                        break;

                    case TokenType.LogicalOperator:
                        if (next == null || (next.TokenType != TokenType.Property && next.TokenType != TokenType.OpenParenthesis))
                            return false;
                        break;

                    case TokenType.OpenParenthesis:
                        if (next == null || (next.TokenType != TokenType.Property && next.TokenType != TokenType.OpenParenthesis))
                            return false;
                        break;

                    case TokenType.CloseParenthesis:
                        if (next != null && next.TokenType   != TokenType.LogicalOperator && next.TokenType != TokenType.CloseParenthesis)
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
            int openParenCount = tokens.Count(t => t.TokenType == TokenType.OpenParenthesis);
            int closeParenCount = tokens.Count(t => t.TokenType == TokenType.CloseParenthesis);

            if (openParenCount != closeParenCount)
            {
                errors.Add($"Unbalanced parentheses: {openParenCount} opening and {closeParenCount} closing");

                // Mark all parentheses as having errors
                foreach (var token in tokens.Where(t => t.TokenType == TokenType.OpenParenthesis || t.TokenType == TokenType.CloseParenthesis))
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

                switch (current.TokenType)
                {
                    case TokenType.Property:
                        if (next == null)
                        {
                            errors.Add($"Property '{current.Value}' must be followed by an operator");
                            current.HasError = true;
                            current.ErrorMessage = "Property must be followed by an operator";
                        }
                        else if (next.TokenType != TokenType.Operator)
                        {
                            errors.Add($"Property '{current.Value}' must be followed by an operator, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Property must be followed by an operator, not '{next.Value}'";
                        }

                        if (prev != null && prev.TokenType != TokenType.LogicalOperator && prev.TokenType != TokenType.OpenParenthesis)
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
                        else if (next.TokenType != TokenType.Value && next.TokenType != TokenType.RestrictedValue)
                        {
                            errors.Add($"Operator '{current.Value}' must be followed by a value, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Operator must be followed by a value, not '{next.Value}'";
                        }

                        if (prev == null || prev.TokenType != TokenType.Property)
                        {
                            errors.Add($"Operator '{current.Value}' must be preceded by a property");
                            current.HasError = true;
                            current.ErrorMessage = "Operator must be preceded by a property";
                        }
                        else
                        {
                            // Check if the operator is valid for the property type
                            var propertyToken = prev;
                            var propInfo = AvailableProperties
                                .FirstOrDefault(p => p.Name.Equals(propertyToken.Value, StringComparison.OrdinalIgnoreCase));

                            if (propInfo != null)
                            {
                                var validOperatorsForType = GetValidOperatorsForType(propInfo.Type);
                                if (!validOperatorsForType.Contains(current.Value, StringComparer.OrdinalIgnoreCase))
                                {
                                    errors.Add($"Operator '{current.Value}' is not valid for property '{propertyToken.Value}' of type {propInfo.Type.Name}");
                                    current.HasError = true;
                                    current.ErrorMessage = $"Operator '{current.Value}' is not valid for {propInfo.Type.Name} type";
                                }
                            }
                        }

                        // Check if the operator is a valid, full operator
                        bool isValidOperator = ComparisonOperators.Contains(current.Value, StringComparer.OrdinalIgnoreCase);

                        if (!isValidOperator)
                        {
                            errors.Add($"Invalid operator '{current.Value}'.");
                            current.HasError = true;
                            current.ErrorMessage = $"Invalid operator '{current.Value}'.";
                        }
                        break;

                    case TokenType.Value:
                        if (next != null && next.TokenType != TokenType.LogicalOperator && next.TokenType != TokenType.CloseParenthesis)
                        {
                            errors.Add($"Value '{current.Value}' must be followed by a logical operator or closing parenthesis, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Value must be followed by a logical operator or closing parenthesis";
                        }

                        if (prev == null || prev.TokenType != TokenType.Operator)
                        {
                            errors.Add($"Value '{current.Value}' must be preceded by an operator");
                            current.HasError = true;
                            current.ErrorMessage = "Value must be preceded by an operator";
                        }
                        else
                        {
                            // Check if the value type matches the property type
                            var operatorToken = prev;
                            var propertyToken = i >= 2 ? tokens[i - 2] : null;

                            if (propertyToken != null && propertyToken.TokenType == TokenType.Property)
                            {
                                var propInfo = AvailableProperties
                                    .FirstOrDefault(p => p.Name.Equals(propertyToken.Value, StringComparison.OrdinalIgnoreCase));

                                if (propInfo != null)
                                {
                                    // Check if the value is valid for this property type
                                    if (!IsValueValidForPropertyType(current.Value, propInfo.Type))
                                    {
                                        errors.Add($"Value '{current.Value}' is not valid for property '{propertyToken.Value}' of type {propInfo.Type.Name}");
                                        current.HasError = true;
                                        current.ErrorMessage = $"Value '{current.Value}' is not valid for {propInfo.Type.Name} type";
                                    }
                                }
                            }
                        }
                        break;

                    case TokenType.LogicalOperator:
                        if (next == null)
                        {
                            errors.Add($"Logical operator '{current.Value}' must be followed by a property or opening parenthesis");
                            current.HasError = true;
                            current.ErrorMessage = "Logical operator must be followed by a property or opening parenthesis";
                        }
                        else if (next.TokenType != TokenType.Property && next.TokenType != TokenType.OpenParenthesis)
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
                        else if (next.TokenType != TokenType.Property && next.TokenType != TokenType.OpenParenthesis)
                        {
                            errors.Add($"Opening parenthesis must be followed by a property or another opening parenthesis, not '{next.Value}'");
                            current.HasError = true;
                            current.ErrorMessage = $"Opening parenthesis must be followed by a property or another opening parenthesis";
                        }
                        break;

                    case TokenType.CloseParenthesis:
                        if (next != null && next.TokenType != TokenType.LogicalOperator && next.TokenType != TokenType.CloseParenthesis)
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

        private bool IsValueValidForPropertyType(string value, Type propertyType)
        {
            // Remove quotes if present (for string values)
            string unquotedValue = value;
            if ((value.StartsWith("'") && value.EndsWith("'")) ||
                (value.StartsWith("\"") && value.EndsWith("\"")))
            {
                unquotedValue = value.Substring(1, value.Length - 2);
            }

            // Check if value is a boolean literal
            bool isBoolLiteral = value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                value.Equals("false", StringComparison.OrdinalIgnoreCase);

            // Check if value is a numeric literal
            bool isNumericLiteral = decimal.TryParse(value, out _);

            // Check if value is a quoted string
            bool isQuotedString = (value.StartsWith("'") && value.EndsWith("'")) ||
                                  (value.StartsWith("\"") && value.EndsWith("\""));

            // Validate based on property type
            if (propertyType == typeof(bool))
            {
                // Boolean properties can only accept boolean literals
                return isBoolLiteral;
            }
            else if (propertyType == typeof(int) || propertyType == typeof(decimal))
            {
                // Numeric properties can only accept numeric literals
                return isNumericLiteral;
            }
            else if (propertyType == typeof(string))
            {
                // String properties must have quoted strings (not boolean or numeric)
                return isQuotedString;
            }
            else if (propertyType == typeof(DateTime))
            {
                // DateTime can accept quoted strings (in ISO format) or datetime literals
                return isQuotedString || DateTime.TryParse(unquotedValue, out _);
            }

            // Default: accept the value
            return true;
        }
    }
}