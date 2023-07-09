using Antlr4.Runtime;

namespace KismetKompiler.Library.Parser;

public static class KismetScriptParserHelper
{
    public static KismetScriptParser.CompilationUnitContext ParseCompilationUnit(string input, IAntlrErrorListener<IToken> errorListener = null)
    {
        var inputStream = new AntlrInputStream(input);
        return ParseCompilationUnit(inputStream, errorListener);
    }

    public static KismetScriptParser.CompilationUnitContext ParseCompilationUnit(TextReader input, IAntlrErrorListener<IToken> errorListener = null)
    {
        var inputStream = new AntlrInputStream(input);
        return ParseCompilationUnit(inputStream, errorListener);
    }

    public static KismetScriptParser.CompilationUnitContext ParseCompilationUnit(Stream input, IAntlrErrorListener<IToken> errorListener = null)
    {
        var inputStream = new AntlrInputStream(input);
        return ParseCompilationUnit(inputStream, errorListener);
    }

    private static KismetScriptParser.CompilationUnitContext ParseCompilationUnit(AntlrInputStream inputStream, IAntlrErrorListener<IToken> errorListener = null)
    {
        var lexer = new KismetScriptLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);

        // parser
        var parser = new KismetScriptParser(tokenStream);
        parser.BuildParseTree = true;
        parser.ErrorHandler = new BailErrorStrategy();
        //parser.ErrorHandler = new DefaultErrorStrategy();

        //errorListener = new CustomErrorListener(parser.Vocabulary);

        if (errorListener != null)
        {
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
        }

        var cst = parser.compilationUnit();

        return cst;
    }
}