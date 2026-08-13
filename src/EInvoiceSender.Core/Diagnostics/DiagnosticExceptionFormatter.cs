using System.Diagnostics;
using System.Reflection;

namespace EInvoiceSender.Core.Diagnostics;

/// <summary>
/// Formatiert ausschließlich technische Exception-Metadaten. Insbesondere
/// werden keine Message, Data, Argumentwerte oder Quelldateiinformationen
/// gelesen.
/// </summary>
public static class DiagnosticExceptionFormatter
{
    private const int MaxInnerExceptions = 4;
    private const int MaxStackFrames = 40;

    /// <summary>Erzeugt die datensparsame Ein-Zeilen-Darstellung.</summary>
    public static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            string types = FormatTypeChain(exception);
            string methods = FormatMethodStack(exception);

            return $"exception-types={types}; methods={methods}";
        }
        catch (Exception formatterException) when (formatterException is not OutOfMemoryException
                                                    and not StackOverflowException)
        {
            // Selbst ein ungewöhnlicher Reflection-/StackTrace-Fehler darf
            // nicht in den normalen Anwendungspfad zurücklaufen. Der Typ der
            // ursprünglichen Ausnahme bleibt als kleinster sicherer Befund.
            return $"exception-types={exception.GetType().Name}; methods=unavailable";
        }
    }

    private static string FormatTypeChain(Exception exception)
    {
        var result = new List<string>(MaxInnerExceptions);
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        Exception? current = exception;

        while (current is not null
               && result.Count < MaxInnerExceptions
               && visited.Add(current))
        {
            Type type = current.GetType();
            result.Add(type.FullName ?? type.Name);
            current = current.InnerException;
        }

        return string.Join(">", result);
    }

    private static string FormatMethodStack(Exception exception)
    {
        // false ist die entscheidende Datenschutzgrenze: keine Dateinamen und
        // keine Zeilennummern aus ausgelieferten PDB-Dateien aufnehmen.
        StackFrame[]? frames = new StackTrace(exception, false).GetFrames();

        if (frames is null || frames.Length == 0)
        {
            return "unavailable";
        }

        return string.Join(">", frames
            .Take(MaxStackFrames)
            .Select(frame => FormatMethod(frame.GetMethod())));
    }

    private static string FormatMethod(MethodBase? method)
    {
        if (method is null)
        {
            return "unknown";
        }

        string declaringType = method.DeclaringType?.FullName ?? "unknown";
        return $"{declaringType}.{method.Name}";
    }
}
