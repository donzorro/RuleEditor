using RuleEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace RuleEditor.ViewModels.Version3
{
    public class RuleEditorViewModel3 : INotifyPropertyChanged
    {
        private ExpressionParser _parser;
        private ExpressionCompiler _compiler;
        private string _expressionText = "";
        private List<Token> _tokens = new List<Token>();
        private List<string> _suggestions = new List<string>();
        private bool _isSyntaxValid;
        private List<string> _syntaxErrors = new List<string>();
        private List<Token> _tokensWithErrors = new List<Token>();
        private string _statusMessage = "Ready";
        private string _testResultMessage = "";
        private int _caretPosition;
        private Token _currentToken;
        
        // Sample friend data for demonstration purposes
        private readonly List<(string Id, string FullName)> _sampleFriends = new List<(string Id, string FullName)>
        {
            ("user123", "John Smith"),
            ("user456", "Emma Johnson"),
            ("user789", "Michael Brown"),
            ("user321", "Sarah Davis"),
            ("user654", "David Wilson"),
            ("user987", "Jennifer Taylor")
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public string ExpressionText
        {
            get => _expressionText;
            set
            {
                if (_expressionText != value)
                {
                    _expressionText = value;
                    OnPropertyChanged();
                    UpdateTokens();
                }
            }
        }

        public List<Token> Tokens
        {
            get => _tokens;
            private set
            {
                _tokens = value;
                OnPropertyChanged();
            }
        }

        public List<string> Suggestions
        {
            get => _suggestions;
            private set
            {
                _suggestions = value;
                OnPropertyChanged();
            }
        }

        public bool IsSyntaxValid
        {
            get => _isSyntaxValid;
            private set
            {
                if (_isSyntaxValid != value)
                {
                    _isSyntaxValid = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string> SyntaxErrors
        {
            get => _syntaxErrors;
            private set
            {
                _syntaxErrors = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSyntaxErrors));
            }
        }

        public List<Token> SyntaxErrorObjects
        {
            get => _tokensWithErrors;
            private set
            {
                _tokensWithErrors = value;
                OnPropertyChanged();
            }
        }

        public bool HasSyntaxErrors => SyntaxErrors.Count > 0;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TestResultMessage
        {
            get => _testResultMessage;
            set
            {
                if (_testResultMessage != value)
                {
                    _testResultMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CaretPosition
        {
            get => _caretPosition;
            set
            {
                if (_caretPosition != value)
                {
                    _caretPosition = value;
                    OnPropertyChanged();
                    UpdateCurrentToken();
                }
            }
        }

        public Token CurrentToken
        {
            get => _currentToken;
            private set
            {
                _currentToken = value;
                OnPropertyChanged();
                UpdateSuggestions();
            }
        }

        public List<RulePropertyInfo> AvailableProperties { get; private set; }

        public ICommand ValidateCommand { get; private set; }
        public ICommand TestExpressionCommand { get; private set; }

        public RuleEditorViewModel3()
        {
            InitializeAvailableProperties();
            _parser = new ExpressionParser(AvailableProperties);
            _compiler = new ExpressionCompiler(AvailableProperties);

            ValidateCommand = new RelayCommand(ValidateExpression);
            TestExpressionCommand = new RelayCommand(TestExpression);
        }

        private void InitializeAvailableProperties()
        {

            AvailableProperties = new List<RulePropertyInfo>
            {
                new RulePropertyInfo { Name = "Name", Type = typeof(string), Description = "Person's full name" },
                new RulePropertyInfo { Name = "Age", Type = typeof(int), Description = "Age in years" },
                new RulePropertyInfo { Name = "IsActive", Type = typeof(bool), Description = "Account status" },
                new RulePropertyInfo { Name = "Price", Type = typeof(decimal), Description = "Item price" },
                new RulePropertyInfo { Name = "Email", Type = typeof(string), Description = "Email address" },
                new RulePropertyInfo { Name = "Emoji", Type = typeof(string), Description = "Emoji address" },
                new RulePropertyInfo { Name = "LastLoginDate", Type = typeof(DateTime), Description = "Last login timestamp" },
                new RulePropertyInfo { 
                    Name = "Friends", 
                    Type = typeof(string), 
                    Description = "Comma-separated list of friend user IDs",
                    AllowedValues = _sampleFriends.Select(f => $"'{f.FullName} ({f.Id})'")
                }
            };
        }

        public void UpdateTokens()
        {
            Tokens = _parser.Tokenize(ExpressionText);
            UpdateCurrentToken();
            ValidateExpressionSyntax();
        }

        public void UpdateCurrentToken()
            // Find the token at the current caret position
        {
            var tokenAtCaret = Tokens.FirstOrDefault(t => 
                t.Position <= CaretPosition && 
                t.Position + t.Length >= CaretPosition);
            
            // If no token is found at the caret position, we're in between tokens or at the end
            if (tokenAtCaret == null)
            {
                // Clear the current token but still update suggestions
                _currentToken = null;
                OnPropertyChanged(nameof(CurrentToken));
                UpdateSuggestions();
            }
            else if (_currentToken != tokenAtCaret)
            {
                CurrentToken = tokenAtCaret;
                UpdateSuggestions();
            }
        }

        private void UpdateSuggestions()
        {
            List<string> newSuggestions = new List<string>();
            
            if (CurrentToken == null)
            {
                // If we're not on a token, determine what kind of token would be expected next
                newSuggestions = GetExpectedNextTokenSuggestions();
            }
            else
            {
                // Generate suggestions based on the possible token types
                if (CurrentToken.PossibleTypes.Contains(TokenType.Property))
                {
                    // Suggest properties
                    var matchingProperties = AvailableProperties
                        .Select(p => p.Name)
                        .Where(name => name.StartsWith(CurrentToken.Value, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Add all matching properties to suggestions
                    newSuggestions.AddRange(matchingProperties);

                    // If there are no matching properties, mark the token as an error
                    // but still show all available properties in the dropdown
                    if (matchingProperties.Count == 0 && !string.IsNullOrEmpty(CurrentToken.Value))
                    {
                        // Mark the current token as an error
                        CurrentToken.HasError = true;
                        CurrentToken.ErrorMessage = $"No property found that starts with '{CurrentToken.Value}'";
                        
                        // Find the corresponding token in the Tokens collection and mark it as an error too
                        var tokenInCollection = Tokens.FirstOrDefault(t => 
                            t.Position == CurrentToken.Position && 
                            t.Length == CurrentToken.Length);
                            
                        if (tokenInCollection != null && tokenInCollection != CurrentToken)
                        {
                            tokenInCollection.HasError = true;
                            tokenInCollection.ErrorMessage = CurrentToken.ErrorMessage;
                        }
                        
                        // Show all available properties in the dropdown
                        newSuggestions.AddRange(AvailableProperties.Select(p => p.Name));
                        
                        // Update SyntaxErrorObjects collection with the error token
                        var updatedErrors = new List<Token>(SyntaxErrorObjects);
                        if (!updatedErrors.Any(t => t.Position == CurrentToken.Position && t.Length == CurrentToken.Length))
                        {
                            updatedErrors.Add(CurrentToken);
                            SyntaxErrorObjects = updatedErrors;
                            OnPropertyChanged(nameof(SyntaxErrorObjects));
                        }
                    }
                    else
                    {
                        // Clear any error from the token if we now have matches
                        if (CurrentToken.HasError)
                        {
                            CurrentToken.HasError = false;
                            CurrentToken.ErrorMessage = null;
                            
                            // Also clear the error from the token in the collection
                            var tokenInCollection = Tokens.FirstOrDefault(t => 
                                t.Position == CurrentToken.Position && 
                                t.Length == CurrentToken.Length);
                                
                            if (tokenInCollection != null && tokenInCollection != CurrentToken)
                            {
                                tokenInCollection.HasError = false;
                                tokenInCollection.ErrorMessage = null;
                            }
                            
                            // Remove this token from SyntaxErrorObjects if it exists
                            var tokenPositionAndLength = (CurrentToken.Position, CurrentToken.Length);
                            var updatedErrors = SyntaxErrorObjects
                                .Where(t => t.Position != CurrentToken.Position || t.Length != CurrentToken.Length)
                                .ToList();
                                
                            if (updatedErrors.Count != SyntaxErrorObjects.Count)
                            {
                                SyntaxErrorObjects = updatedErrors;
                                OnPropertyChanged(nameof(SyntaxErrorObjects));
                            }
                        }
                    }
                }

                if (CurrentToken.PossibleTypes.Contains(TokenType.Operator))
                {
                    // Get the previous token to determine what operators are valid
                    var prevToken = Tokens
                        .LastOrDefault(t => t.Position < CurrentToken.Position);
                    
                    if (prevToken != null && prevToken.Type == TokenType.Property)
                    {
                        var propInfo = AvailableProperties
                            .FirstOrDefault(p => p.Name.Equals(prevToken.Value, StringComparison.OrdinalIgnoreCase));
                        
                        if (propInfo != null)
                        {
                            newSuggestions.AddRange(GetValidOperatorsForType(propInfo.Type)
                                .Where(op => op.StartsWith(CurrentToken.Value, StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                }

                if (CurrentToken.PossibleTypes.Contains(TokenType.LogicalOperator))
                {
                    // Suggest logical operators
                    newSuggestions.AddRange(new List<string> { "AND", "OR", "NOT" }
                        .Where(op => op.StartsWith(CurrentToken.Value, StringComparison.OrdinalIgnoreCase)));
                }

                if (CurrentToken.PossibleTypes.Contains(TokenType.Value))
                {
                    // Get the previous tokens to determine what values are valid
                    var operatorToken = Tokens
                        .LastOrDefault(t => t.Position < CurrentToken.Position && t.Type == TokenType.Operator);
                    var propertyToken = Tokens
                        .LastOrDefault(t => t.Position < (operatorToken?.Position ?? 0) && t.Type == TokenType.Property);
                    
                    if (propertyToken != null)
                    {
                        var propInfo = AvailableProperties
                            .FirstOrDefault(p => p.Name.Equals(propertyToken.Value, StringComparison.OrdinalIgnoreCase));
                        
                        if (propInfo != null)
                        {
                            // Special case for Friends property
                            if (propInfo.AllowedValues != null)
                            {
                                // Return friend suggestions in "Full Name (user_id)" format
                                var prefix = CurrentToken.Value;
                                var friendSuggestions = propInfo.AllowedValues
                                    .Where(s => string.IsNullOrEmpty(prefix) || 
                                               s.Contains(prefix.TrimStart('\''), StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                    
                                newSuggestions.AddRange(friendSuggestions);
                            }
                            else
                            {
                                // Normal property value suggestions
                                newSuggestions.AddRange(GetCommonValuesForType(propInfo.Type, CurrentToken.Value)
                                    .Where(v => v.StartsWith(CurrentToken.Value, StringComparison.OrdinalIgnoreCase)));
                            }
                        }
                    }
                }
            }
            
            // Always update suggestions to ensure the popup shows the latest suggestions
            Suggestions = newSuggestions.Distinct().ToList();
        }

        private List<string> GetExpectedNextTokenSuggestions()
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

            // Suggest based on the last token type
            switch (lastToken.Type)
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
                        .LastOrDefault(t => t.Position < lastToken.Position && t.Type == TokenType.Property);
                    
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
                            ? GetCommonValuesForType(prop.Type, "") 
                            : new List<string>();
                    }
                    return new List<string>();

                case TokenType.Value:
                case TokenType.CloseParenthesis:
                    // After a value or closing parenthesis, suggest logical operators
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

        private List<string> GetCommonValuesForType(Type type, string prefix = "")
        {
            // Get the property info by checking the token values
            if (Tokens.Count >= 2)
            {
                var lastOperatorToken = Tokens.LastOrDefault(t => t.Type == TokenType.Operator);
                var propertyToken = Tokens.LastOrDefault(t => 
                    t.Position < (lastOperatorToken?.Position ?? 0) && 
                    t.Type == TokenType.Property);
                
                if (propertyToken != null)
                {
                    // Check if this property has restricted values
                    var propInfo = AvailableProperties
                        .FirstOrDefault(p => p.Name.Equals(propertyToken.Value, StringComparison.OrdinalIgnoreCase));
                    
                    if (propInfo != null && propInfo.AllowedValues != null)
                    {
                        // Return the allowed values for this property
                        return propInfo.AllowedValues.ToList();
                    }
                }
            }
            
            // If no specific allowed values, return default values for the type
            if (type == typeof(bool))
            {
                return new List<string> { "true", "false" }
                    .Where(v => string.IsNullOrEmpty(prefix) || v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            
            if (type == typeof(int))
            {
                return new List<string> { "0", "1", "10", "100" }
                    .Where(v => string.IsNullOrEmpty(prefix) || v.StartsWith(prefix))
                    .ToList();
            }
            
            if (type == typeof(string))
            {
                // For strings, suggest using quotes
                if (!prefix.StartsWith("'") && !prefix.StartsWith("\""))
                {
                    return new List<string> { "'" };
                }
            }
            
            return new List<string>();
        }

        public void ValidateExpressionSyntax()
        {
            if (string.IsNullOrWhiteSpace(ExpressionText))
            {
                IsSyntaxValid = false;
                SyntaxErrors = new List<string> { "Expression is empty" };
                SyntaxErrorObjects = new List<Token>();
                StatusMessage = "Empty expression";
                return;
            }

            SyntaxErrors = _parser.GetSyntaxErrors(Tokens);
            // After validation, collect tokens with errors
            SyntaxErrorObjects = Tokens.Where(t => t.HasError).ToList();
            IsSyntaxValid = SyntaxErrors.Count == 0;
            StatusMessage = IsSyntaxValid ? "Expression is valid" : "Expression has syntax errors";
        }

        private void ValidateExpression()
        {
            ValidateExpressionSyntax();
            if (IsSyntaxValid)
            {
                StatusMessage = "Expression is valid and ready to use";
            }
        }

        private void TestExpression()
        {
            if (!IsSyntaxValid)
            {
                TestResultMessage = "Cannot test: Expression has syntax errors";
                return;
            }

            try
            {
                // Create a test object
                var testObject = new TestObject
                {
                    Name = "John Doe",
                    Age = 25,
                    IsActive = true,
                    Price = 99.99m
                };

                // Compile and evaluate the expression
                var func = _compiler.CompileExpression<TestObject>(ExpressionText);
                bool result = func(testObject);

                TestResultMessage = $"Result: {result} (evaluated against test object)";
            }
            catch (Exception ex)
            {
                TestResultMessage = $"Error: {ex.Message}";
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple relay command implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}