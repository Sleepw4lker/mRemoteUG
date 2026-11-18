using System.Text.RegularExpressions;

namespace mRemoteUG.Tools.Cmdline;

// Adapted from http://qntm.org/cmd
public class CommandLineArguments
{
    private readonly List<Argument> _arguments = new List<Argument>();


    #region Public Methods
    public void Add(string argument, bool forceQuotes = false)
    {
        _arguments.Add(new Argument(argument, forceQuotes));
    }

    public void Add(params string[] argumentArray)
    {
        foreach (string argument in argumentArray)
        {
            Add(argument);
        }
    }


    public override string ToString()
    {
        IEnumerable<string> argList = _arguments.Select(ProcessArgument);
        return string.Join(" ", argList.ToArray());
    }


    public static string EscapeBackslashes(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return argument;
        }

        // Sequence of backslashes followed by a double quote:
        //     double up all the backslashes and escape the double quote
        return Regex.Replace(argument, "(\\\\*)\"", "$1$1\\\"");
    }

    public static string EscapeBackslashesForTrailingQuote(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return argument;
        }

        // Sequence of backslashes followed by the end of the string
        // (which will become a double quote):
        //     double up all the backslashes
        return Regex.Replace(argument, "(\\\\*)$", "$1$1");
    }

    public static string QuoteArgument(string argument, bool forceQuotes = false)
    {
        if (!forceQuotes && !string.IsNullOrEmpty(argument) && !argument.Contains(' '))
        {
            return argument;
        }

        return "\"" + EscapeBackslashesForTrailingQuote(argument) + "\"";
    }


    #endregion

    #region Protected Methods
    protected static string ProcessArgument(Argument argument)
    {
        string text = argument.Text;

        text = EscapeBackslashes(text);
        text = QuoteArgument(text, argument.ForceQuotes);

        return text;
    }
    #endregion

    #region Protected Classes
    protected class Argument
    {
        public Argument(string text, bool forceQuotes = false)
        {
            Text = text;
            ForceQuotes = forceQuotes;
        }

        public string Text { get; set; }
        public bool ForceQuotes { get; set; }
    }
    #endregion
}
