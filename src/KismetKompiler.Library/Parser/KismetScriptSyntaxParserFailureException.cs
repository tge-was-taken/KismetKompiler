using System.Runtime.Serialization;

namespace KismetKompiler.Library.Parser;

[Serializable]
internal class KismetScriptSyntaxParserFailureException : Exception
{
    public KismetScriptSyntaxParserFailureException()
    {
    }

    public KismetScriptSyntaxParserFailureException(string? message) : base(message)
    {
    }

    public KismetScriptSyntaxParserFailureException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected KismetScriptSyntaxParserFailureException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}