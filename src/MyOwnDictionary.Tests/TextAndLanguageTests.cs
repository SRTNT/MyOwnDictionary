using MyOwnDictionary.Core;

namespace MyOwnDictionary.Tests;

public class TextNormalizerTests
{
    [Fact]
    public void English_IsLowercased_AndSpacesCollapse()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("  Hello   WORLD  ", Language.En));
    }

    [Fact]
    public void English_TrimsEdgePunctuation()
    {
        Assert.Equal("hello", TextNormalizer.Normalize("\"Hello!\"", Language.En));
    }

    [Fact]
    public void Persian_NormalizesLetters_Diacritics_Tatweel_AndZwnj()
    {
        var input = "كِتاب\u0640ي\u200Cخانه";
        Assert.Equal("کتابیخانه", TextNormalizer.Normalize(input, Language.Fa));
    }

    [Fact]
    public void Persian_MapsDigits()
    {
        Assert.Equal("12", TextNormalizer.Normalize("۱۲", Language.Fa));
    }

    [Theory]
    [InlineData("hello", 1)]
    [InlineData("one two three four five", 5)]
    [InlineData("یک\u200Cدو سه", 3)]
    [InlineData("   ", 0)]
    public void CountWords_SplitsOnSpaceAndZwnj(string text, int expected)
    {
        Assert.Equal(expected, TextNormalizer.CountWords(text));
    }
}

public class LanguageDetectorTests
{
    [Theory]
    [InlineData("hello", Language.En)]
    [InlineData("سلام", Language.Fa)]
    [InlineData("hello سلام", Language.Fa)]
    [InlineData("", Language.En)]
    [InlineData("123", Language.En)]
    public void DetectsScript(string text, Language expected)
    {
        Assert.Equal(expected, LanguageDetector.Detect(text));
    }

    [Fact]
    public void Opposite_FlipsLanguage()
    {
        Assert.Equal(Language.Fa, LanguageDetector.Opposite(Language.En));
        Assert.Equal(Language.En, LanguageDetector.Opposite(Language.Fa));
    }
}

public class GoogleTranslateResponseParserTests
{
    [Fact]
    public void Parse_JoinsSegments()
    {
        const string json = """[[["سلام. ","Hello. ",null,null,1],["حالت چطور است؟","How are you?",null,null,1]],null,"en"]""";
        Assert.Equal("سلام. حالت چطور است؟", GoogleTranslateResponseParser.Parse(json));
    }

    [Fact]
    public void Parse_ThrowsWhenEmpty()
    {
        Assert.Throws<FormatException>(() => GoogleTranslateResponseParser.Parse("[]"));
    }
}

public class CsvPairReaderTests
{
    [Fact]
    public void Read_SkipsComments_AndUnquotesCommas()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            # comment
            hello,سلام
            "yes, please",بله لطفا

            """));
        var pairs = CsvPairReader.Read(stream);
        Assert.Equal(2, pairs.Count);
        Assert.Equal(("hello", "سلام"), pairs[0]);
        Assert.Equal(("yes, please", "بله لطفا"), pairs[1]);
    }
}
