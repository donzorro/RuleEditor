using System;
using System.Collections.Generic;

namespace RuleEditor.Models
{
    public class RulePropertyInfo
    {
        public string Name { get; set; }

        public string Description { get; set; }
        public Type Type { get; set; }

        /// <summary>
        /// Collection of allowed values for this property.
        /// If not null or empty, only these values should be presented as suggestions.
        /// For example, can be used for Friends list or any other property with restricted values.
        /// </summary>
        public IEnumerable<string> AllowedValues { get; set; }

        public bool SupportsOperator(string op)
        {
            return op switch
            {
                // Operators supported by all types
                "==" => true,
                "!=" => true,

                // Numeric comparisons
                ">" => IsNumericType(),
                "<" => IsNumericType(),
                ">=" => IsNumericType(),
                "<=" => IsNumericType(),

                // String operations
                "CONTAINS" => Type == typeof(string),
                "STARTSWITH" => Type == typeof(string),
                "ENDSWITH" => Type == typeof(string),

                // Logical operators
                "AND" => true,
                "OR" => true,

                _ => false
            };
        }

        private bool IsNumericType()
        {
            return Type == typeof(int) ||
                   Type == typeof(double) ||
                   Type == typeof(float) ||
                   Type == typeof(decimal) ||
                   Type == typeof(long) ||
                   Type == typeof(short);
        }
    }
}