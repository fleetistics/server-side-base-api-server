namespace exs.commons.Json
{
    /// <summary>
    /// Distinguishes "field omitted from a PATCH request body" (IsSet=false, entity
    /// property left untouched) from "field explicitly present" (IsSet=true, apply
    /// Value — which may itself be null for a nullable T, clearing the field).
    ///
    /// The distinction relies on System.Text.Json never invoking a property's
    /// converter/setter for a JSON key that is absent from the payload, so an
    /// unset property simply stays at its struct default (IsSet=false). See
    /// OptionalJsonConverterFactory for the read side.
    ///
    /// Note: Optional&lt;string&gt; and Optional&lt;string?&gt; are the same runtime
    /// closed generic type — C# nullable-reference-type annotations on a generic
    /// argument are compile-time only. The converter therefore accepts a JSON
    /// null uniformly for any reference T; "must not be null" is an
    /// application-level validation concern for whoever consumes IsSet/Value,
    /// not something this type or its converter enforces.
    /// </summary>
    public readonly struct Optional<T>
    {
        public bool IsSet { get; }
        public T? Value { get; }

        private Optional(bool isSet, T? value)
        {
            IsSet = isSet;
            Value = value;
        }

        public static Optional<T> Of(T? value) => new(true, value);
    }
}
