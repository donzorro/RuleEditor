using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RuleEditor.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> _propertyValues = new Dictionary<string, object>();
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        /// <summary>
        /// Gets a property value from the internal dictionary.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="propertyName">Name of the property (auto-detected from caller).</param>
        /// <returns>The property value, or default(T) if not set.</returns>
        protected T GetValue<T>([CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));
                
            if (_propertyValues.TryGetValue(propertyName, out var value))
            {
                return (T)value;
            }
            
            return default;
        }
        
        /// <summary>
        /// Sets a property value in the internal dictionary and raises PropertyChanged if the value changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">Name of the property (auto-detected from caller).</param>
        /// <returns>True if the value changed, false otherwise.</returns>
        protected bool SetValue<T>(T value, [CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));
                
            if (_propertyValues.TryGetValue(propertyName, out var currentValue))
            {
                if (Equals(currentValue, value))
                    return false;
            }
            
            _propertyValues[propertyName] = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}