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