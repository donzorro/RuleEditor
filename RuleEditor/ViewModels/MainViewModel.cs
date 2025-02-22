using System;
using System.Collections.ObjectModel;
using RuleEditor.Models;

namespace RuleEditor.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private RuleEditorViewModel _ruleEditorViewModel;

        public MainViewModel()
        {
            RuleEditorViewModel = new RuleEditorViewModel();
            
            // Example: Initialize with a test object
            // You can change this to your actual object type
            var testObject = new TestObject 
            { 
                Name = "Test",
                Age = 25,
                IsActive = true,
                Price = 99.99m
            };
            
            RuleEditorViewModel.SetTargetObject(testObject);


            //// User enters expression
            //viewModel.Expression = "Age > 18 AND Name.Contains('John')";

            //// Compile the expression
            //var rule = RuleEditorViewModel.CompileExpression();

            //// Use the rule
            //var person = new Person { Name = "John Doe", Age = 25 };
            //bool matches = rule(person); // Returns true
        }

        public RuleEditorViewModel RuleEditorViewModel
        {
            get => _ruleEditorViewModel;
            set => SetProperty(ref _ruleEditorViewModel, value);
        }
    }

    // Example test class - replace with your actual object type
    public class TestObject
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public decimal Price { get; set; }
    }
}