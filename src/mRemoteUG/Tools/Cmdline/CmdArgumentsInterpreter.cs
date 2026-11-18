using System.Buffers;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using mRemoteUG.App;
// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteUG.Tools.Cmdline;

//
//* Arguments class: application arguments interpreter
//*
//* Authors:		R. LOPES
//* Contributors:	R. LOPES
//* Created:		25 October 2002
//* Modified:		28 October 2002
//*
//* Version:		1.0
//
public class CmdArgumentsInterpreter
{
    private readonly StringDictionary _parameters;

    private static readonly SearchValues<char> s_separators = SearchValues.Create(":=");

    // Retrieve a parameter value if it exists
    public string? this[string param]
    {
        get { return (_parameters[param]); }
    }

    public CmdArgumentsInterpreter(IEnumerable<string> args)
    {
        _parameters = new StringDictionary();
        var remover = new Regex("^[\'\"]?(.*?)[\'\"]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        string? parameter = null;

        // Valid parameters forms:
        // {-,/,--}param{ ,=,:}((",')value(",'))
        // Examples: -param1 value1 --param2 /param3:"Test-:-work" /param4=happy -param5 '--=nice=--'

        try
        {
            foreach (string txt in args)
            {
                // Only an argument that starts with -, -- or / is a switch, and only a switch is split, at
                // its first : or =. Anything else is a value and stays whole (C:\x\y.xml, a=b).
                string[] parts = SplitArgument(txt);
                switch (parts.Length)
                {
                    case 1:
                        // Found a value (for the last parameter found (space separator))
                        if (parameter != null)
                        {
                            if (!_parameters.ContainsKey(parameter))
                            {
                                parts[0] = remover.Replace(parts[0], "$1");
                                _parameters.Add(parameter, parts[0]);
                            }
                            parameter = null;
                        }
                        // else Error: no parameter waiting for a value (skipped)
                        break;
                    case 2:
                        // Found just a parameter
                        // The last parameter is still waiting. With no value, set it to true.
                        if (parameter != null)
                        {
                            if (!_parameters.ContainsKey(parameter))
                            {
                                _parameters.Add(parameter, "true");
                            }
                        }
                        parameter = parts[1];
                        break;
                    case 3:
                        // Parameter with enclosed value
                        // The last parameter is still waiting. With no value, set it to true.
                        if (parameter != null)
                        {
                            if (!_parameters.ContainsKey(parameter))
                            {
                                _parameters.Add(parameter, "true");
                            }
                        }
                        parameter = parts[1];
                        // Remove possible enclosing characters (",')
                        if (!_parameters.ContainsKey(parameter))
                        {
                            parts[2] = remover.Replace(parts[2], "$1");
                            _parameters.Add(parameter, parts[2]);
                        }
                        parameter = null;
                        break;
                }
            }
            // In case a parameter is still waiting
            if (parameter == null)
            {
                return;
            }

            if (!_parameters.ContainsKey(parameter))
            {
                _parameters.Add(parameter, "true");
            }
        }
        catch (Exception ex)
        {
            Runtime.MessageCollector.AddExceptionMessage("Creating new Args failed", ex);
        }
    }

    // A value comes back as one part. A switch comes back as an empty part, its name, and, when it
    // has a : or = separator, its value.
    private static string[] SplitArgument(string argument)
    {
        if (!(argument.StartsWith('-') || argument.StartsWith('/')))
        {
            return new[] { argument };
        }

        string switchText = argument.StartsWith("--", StringComparison.Ordinal)
            ? argument.Substring(2)
            : argument.Substring(1);
        int separator = switchText.AsSpan().IndexOfAny(s_separators);
        return separator < 0
            ? new[] { string.Empty, switchText }
            : new[] { string.Empty, switchText.Substring(0, separator), switchText.Substring(separator + 1) };
    }
}
