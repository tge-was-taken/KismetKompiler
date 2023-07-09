using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Sharpen;

class ConsoleAntlrErrorListener : IAntlrErrorListener<IToken>
{
    public ConsoleAntlrErrorListener() { }

    public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Console.WriteLine($"Syntax error: {msg} ({offendingSymbol.Line}:{offendingSymbol.Column})");
    }
}

public class CustomErrorListener : BaseErrorListener
{
    private readonly IVocabulary vocabulary;

    public CustomErrorListener(IVocabulary vocabulary)
    {
        this.vocabulary = vocabulary;
    }

    public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        if (e is NoViableAltException exception)
        {
            // Extract relevant information from the exception
            int offendingTokenIndex = exception.OffendingToken.TokenIndex;
            string decisionDescription = GetDecisionDescription(exception.Context);
            List<string> expectedTokens = GetExpectedTokenNames(exception.GetExpectedTokens());

            // Build a meaningful error message
            var errorMessage = $"Syntax error at line {line}, position {charPositionInLine}: {msg}\n";

            // Add more details about the decision point
            errorMessage += $"Decision: {decisionDescription}\n";

            // Add suggestions based on the expected tokens
            errorMessage += $"Expected tokens: {string.Join(", ", expectedTokens)}\n";

            Console.WriteLine(errorMessage);
        }
        else
        {
            // Handle other types of errors
            // ...
        }
    }

    private string GetDecisionDescription(RuleContext context)
    {
        // Retrieve the description of the current parser rule
        return context.ToString();
    }

    private List<string> GetExpectedTokenNames(IntervalSet expectedTokens)
    {
        // Convert the expected token IntervalSet to a list of symbolic names
        List<string> tokenNames = expectedTokens.ToList()
            .Select(tokenId => vocabulary.GetSymbolicName(tokenId))
            .ToList();
        return tokenNames;
    }
}