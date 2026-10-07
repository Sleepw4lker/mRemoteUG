
using System;
using System.Diagnostics.CodeAnalysis;

namespace mRemoteUG.Tools
{
    public static class Extensions
	{
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if the given value is
        /// null. Otherwise, return the value.
        /// </summary>
        /// <remarks>
        /// Both halves of the nullability here are load-bearing, and neither covers the other.
        /// The parameter is <c>T?</c> so that a caller passing a nullable argument infers
        /// <c>T</c> as the non-nullable type and gets a non-nullable value back --
        /// <c>this T value</c> would infer <c>T</c> as the nullable type and hand the caller
        /// its own maybe-null back, which is the whole point of the guard. The
        /// <see cref="NotNullAttribute"/> is what serves the call sites that discard the
        /// result and rely on the argument itself being known non-null afterwards.
        /// </remarks>
        /// <typeparam name="T">The type of the value being checked.</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="argName">
        /// The name of the argument
        /// </param>
	    public static T ThrowIfNull<T>([NotNull] this T? value, string argName)
	    {
            if (value == null)
                throw new ArgumentNullException(argName);
	        return value;
	    }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if the value
        /// is null or an empty string. Otherwise, returns the value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="argName"></param>
	    public static string ThrowIfNullOrEmpty([NotNull] this string? value, string argName)
	    {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value cannot be null or empty", argName);
	        return value;
	    }
	}
}
