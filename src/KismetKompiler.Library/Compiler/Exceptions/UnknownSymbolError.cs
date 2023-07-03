using KismetKompiler.Library.Syntax.Statements.Expressions;
using System.Runtime.Serialization;

namespace KismetKompiler.Library.Compiler.Exceptions
{
    [Serializable]
    internal class UnknownSymbolError : Exception
    {
        private Identifier identifier;

        public UnknownSymbolError()
        {
        }

        public UnknownSymbolError(Identifier identifier)
        {
            this.identifier = identifier;
        }

        public UnknownSymbolError(string? message) : base(message)
        {
        }

        public UnknownSymbolError(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected UnknownSymbolError(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}