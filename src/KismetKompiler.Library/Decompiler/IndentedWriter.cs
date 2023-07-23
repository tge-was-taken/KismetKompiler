using Newtonsoft.Json.Linq;
using System.Text;

namespace KismetKompiler.Library.Decompiler;

class IndentedWriter : TextWriter
{
    private readonly TextWriter _writer;
    private int _indent;
    private bool _startOfLine = true;

    public IndentedWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public void Push() => _indent++;
    public void Pop() => _indent--;

    public override Encoding Encoding => _writer.Encoding;

    private void WriteIndent()
    {
        if (_startOfLine)
        {
            _writer.Write(new string(' ', _indent * 4));
            _startOfLine = false;
        }
    }

    public override void Write(string? value)
    {
        WriteIndent();
        _writer.Write(value);
    }

    public override void WriteLine()
    {
        _writer.WriteLine();
        _startOfLine = true;
    }

    public override void WriteLine(string? value)
    {
        WriteIndent();
        _writer.WriteLine(value);
        _startOfLine = true;
    }
}
