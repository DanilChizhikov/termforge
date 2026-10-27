namespace DTech.Core.Credentials;

public readonly record struct CredentialKey
{
    public const int MaxLength = 200;

    private const string AllowedPunctuation = "._-:/@";

    private CredentialKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CredentialResult<CredentialKey> Create(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            var emptyError = CredentialErrors.InvalidKey("the key is empty.");
            return CredentialResult<CredentialKey>.Failure(emptyError);
        }

        if (value.Length > MaxLength)
        {
            var lengthError = CredentialErrors.InvalidKey($"the key is longer than {MaxLength} characters.");
            return CredentialResult<CredentialKey>.Failure(lengthError);
        }

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsAsciiLetterOrDigit(character) || AllowedPunctuation.Contains(character))
            {
                continue;
            }

            var characterError = CredentialErrors.InvalidKey($"only ASCII letters, digits and '{AllowedPunctuation}' are allowed.");
            return CredentialResult<CredentialKey>.Failure(characterError);
        }

        var key = new CredentialKey(value);
        return CredentialResult<CredentialKey>.Success(key);
    }

    public override string ToString()
    {
        return Value;
    }
}
