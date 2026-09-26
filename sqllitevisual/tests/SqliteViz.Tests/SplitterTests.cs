using SqliteViz.Core.Runner;

namespace SqliteViz.Tests;

public static class SplitterTests
{
    [Test]
    public static void SemicolonsInStringsAndComments()
    {
        var pieces = SqlSplitter.Split("SELECT 'a;b';\n-- c; d\nSELECT \"x;y\" FROM t /* ; */;\nSELECT 3");
        Assert.Equal(3, pieces.Count);
        Assert.Equal("SELECT 'a;b';", pieces[0].Sql);
        Assert.Equal("SELECT \"x;y\" FROM t /* ; */;", pieces[1].Sql);
        Assert.Equal("SELECT 3", pieces[2].Sql);
    }

    [Test]
    public static void TriggerBodyIsOneStatement()
    {
        var sql = """
            CREATE TRIGGER t AFTER INSERT ON a
            BEGIN
                INSERT INTO b VALUES (1);
                UPDATE c SET x = 1;
            END;
            SELECT 1;
            """;
        var pieces = SqlSplitter.Split(sql);
        Assert.Equal(2, pieces.Count);
        Assert.True(pieces[0].Sql.EndsWith("END;"), pieces[0].Sql);
        Assert.Equal(6, pieces[1].Line);
    }

    [Test]
    public static void LineNumbersSkipLeadingComments()
    {
        var pieces = SqlSplitter.Split("-- title\n\nSELECT 1;\n  /* note */ SELECT\n 2;;\n;\n-- trailing only");
        Assert.Equal(2, pieces.Count);
        Assert.Equal((3, 1), (pieces[0].Line, pieces[0].Column));
        Assert.Equal((4, 14), (pieces[1].Line, pieces[1].Column));
        Assert.Equal(5, pieces[1].EndLine);
    }

    [Test]
    public static void BaseLineAndColumnShiftPositions()
    {
        var pieces = SqlSplitter.Split("SELECT 1; SELECT 2;", baseLine: 10, baseColumn: 5);
        Assert.Equal((10, 5), (pieces[0].Line, pieces[0].Column));
        Assert.Equal((10, 15), (pieces[1].Line, pieces[1].Column));
    }

    [Test]
    public static void StatementAtCursor()
    {
        const string script = "SELECT 1;\n\nSELECT 2\n  FROM t;\n\n";
        Assert.Equal("SELECT 1;", SqlSplitter.StatementAt(script, 1, 3)!.Sql);
        Assert.Equal("SELECT 2\n  FROM t;", SqlSplitter.StatementAt(script, 4, 3)!.Sql);
        Assert.Equal("SELECT 2\n  FROM t;", SqlSplitter.StatementAt(script, 6, 1)!.Sql); // trailing blank line -> previous
        Assert.Equal("SELECT 1;", SqlSplitter.StatementAt(script, 2, 1)!.Sql);
        Assert.Null(SqlSplitter.StatementAt("-- nothing", 1, 1));
    }
}
