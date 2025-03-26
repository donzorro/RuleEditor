using System;

namespace RuleEditor.Models
{
    public class RulePropertyInfo
    {
        public string Name { get; set; }

        public string Description { get; set; }
        public Type Type { get; set; }

        /// <summary>
        /// Indicates if this property represents a comma-separated list of friends
        /// that should display special suggestions with user names and IDs
        /// </summary>
        public bool IsFriendsList { get; set; }

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