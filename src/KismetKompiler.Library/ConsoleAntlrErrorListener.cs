using Antlr4.Runtime;

class ConsoleAntlrErrorListener : IAntlrErrorListener<IToken>
{
    public ConsoleAntlrErrorListener() { }

    public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Console.WriteLine($"Syntax error: {msg} ({offendingSymbol.Line}:{offendingSymbol.Column})");
    }
}