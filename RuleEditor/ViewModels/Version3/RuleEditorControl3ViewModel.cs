using CommunityToolkit.Mvvm.Input;
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
    public class RuleEditorControl3ViewModel : ViewModelBase
    {
        private ExpressionParser _parser;
        private ExpressionCompiler _compiler;
        private string _expressionText = "";       
        private List<string> _syntaxErrors = new List<string>();        
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

        public RuleEditorControl3ViewModel()
        {
            InitializeAvailableProperties();

            _parser = new ExpressionParser(AvailableProperties);
            _compiler = new ExpressionCompiler(AvailableProperties);

            ValidateCommand = new RelayCommand(ValidateExpression);
            TestExpressionCommand = new RelayCommand(TestExpression);
        }

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
            get => GetValue<List<Token>>();
            private set => SetValue(value);
        }

        public List<string> Suggestions
        {
            get => GetValue<List<string>>();
            private set => SetValue(value);
        }

        public bool IsSyntaxValid
        {
            get => GetValue<bool>();
            private set => SetValue(value);
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
            get => GetValue<List<Token>>();
            private set => SetValue(value);
        }

        public bool HasSyntaxErrors => SyntaxErrors.Count > 0;

        public string StatusMessage
        {
            get => GetValue<string>();
            private set => SetValue(value);
        }

        public string TestResultMessage
        {
            get => GetValue<string>();
            private set => SetValue(value);
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

        public void ApplySelectedSuggestion(string suggestion, int caretPosition)
        {
            if (string.IsNullOrEmpty(suggestion)) return;

            var currentToken = CurrentToken;

            if (currentToken != null)
            {
                int tokenStart = currentToken.Position;
                int tokenEnd = tokenStart + currentToken.Length;
                string before = ExpressionText.Substring(0, tokenStart);
                string after = ExpressionText.Substring(tokenEnd);

                if (suggestion == "'" || suggestion == "\"")
                {
                    // Insert matching quotes and place caret inside
                    string quotes = suggestion + suggestion;
                    ExpressionText = before + quotes + after;
                    CaretPosition = tokenStart + 1;
                }
                else
                {
                    // Check if a space is needed after the suggestion
                    bool needsSpace = string.IsNullOrEmpty(after) || !char.IsWhiteSpace(after[0]);
                    string suggestionWithSpace = suggestion + (needsSpace ? " " : "");
                    ExpressionText = before + suggestionWithSpace + after;
                    CaretPosition = tokenStart + suggestion.Length + (needsSpace ? 1 : 0);
                }
            }
            else
            {
                // Fallback: just insert at caret
                string before = ExpressionText.Substring(0, caretPosition);
                string after = ExpressionText.Substring(caretPosition);

                if (suggestion == "'" || suggestion == "\"")
                {
                    string quotes = suggestion + suggestion;
                    ExpressionText = before + quotes + after;
                    CaretPosition = caretPosition + 1;
                }
                else
                {
                    bool needsSpace = string.IsNullOrEmpty(after) || !char.IsWhiteSpace(after[0]);
                    string suggestionWithSpace = suggestion + (needsSpace ? " " : "");
                    ExpressionText = before + suggestionWithSpace + after;
                    CaretPosition = caretPosition + suggestion.Length + (needsSpace ? 1 : 0);
                }
            }
        }

        public void UpdateCurrentToken()
        {
            // Find the token at the current caret position
            var tokenAtCaret = Tokens.FirstOrDefault(t =>
                t.Position <= CaretPosition &&
                t.Position + t.Length >= CaretPosition);

            // Always update suggestions, even if token didn't change
            if (tokenAtCaret == null)
            {
                _currentToken = null;
                OnPropertyChanged(nameof(CurrentToken));
                UpdateSuggestions();
            }
            else
            {
                if (_currentToken != tokenAtCaret)
                {
                    CurrentToken = tokenAtCaret;
                }
                // Always update suggestions when caret moves within the token
                UpdateSuggestions();
            }
        }

        private void UpdateSuggestions()
        {
            List<string> newSuggestions = new List<string>();

            var tokenAtCaret = Tokens.FirstOrDefault(t =>
                t.Position <= CaretPosition &&
                t.Position + t.Length >= CaretPosition);

            if (tokenAtCaret == null)
            {
                // If no token, suggest properties or logical operators
                newSuggestions.AddRange(_parser.GetExpectedNextTokenSuggestions(Tokens, CaretPosition));
            }
            else
            {
                if(tokenAtCaret.PossibleValues != null)
                    newSuggestions.AddRange(CurrentToken.PossibleValues);
             
            }

            Suggestions = newSuggestions.Distinct().ToList();
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
    }
}