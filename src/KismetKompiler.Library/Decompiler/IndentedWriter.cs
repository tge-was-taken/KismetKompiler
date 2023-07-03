using System.Text;

namespace KismetKompiler.Library.Decompiler;

class IndentedWriter : TextWriter
{
    private readonly TextWriter _writer;
    private int _indent;

    public IndentedWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public void Push() => _indent++;
    public void Pop() => _indent--;

    public override Encoding Encoding => _writer.Encoding;

    public override void Write(string? value)
    {
        throw new NotSupportedException();
    }

    public override void WriteLine()
    {
        _writer.WriteLine();
    }

    public override void WriteLine(string? value)
    {
        _writer.WriteLine(new string(' ', _indent * 4) + value);
    }
}
