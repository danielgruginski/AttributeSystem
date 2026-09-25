using System;

namespace ReactiveSolutions.AttributeSystem.Core.Data
{
    /// <summary>
    /// Thrown when JSON can't be read as a StatBlock or an entity profile. The message says what is wrong and
    /// where, e.g. <c>modifiers[1].linear.coefficient: expected a number ... (line 7, column 45)</c>.
    /// </summary>
    public class JsonFormatException : FormatException
    {
        /// <summary>The line (1-based) of the problem, or 0 if unknown.</summary>
        public int Line { get; }

        /// <summary>The column (1-based) of the problem, or 0 if unknown.</summary>
        public int Column { get; }

        public JsonFormatException(string message, int line = 0, int column = 0)
            : base(line > 0 ? $"{message} (line {line}, column {column})" : message)
        {
            Line = line;
            Column = column;
        }
    }
}
