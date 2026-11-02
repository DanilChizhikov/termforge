namespace DTech.Core.CommandHistory;

public static class ShellIntegrationSnippets
{
    private const string EscapeFunction =
        " __tf_e(){ local c=$1; c=${c//\\\\/\\\\\\\\}; c=${c//;/\\\\x3b}; c=${c//$'\\n'/\\\\x0a}; c=${c//$'\\a'/\\\\x07}; c=${c//$'\\e'/\\\\x1b}; printf '\\e]633;E;%s;%s\\a' \"$c\" \"$__tf_nonce\"; }";

    private const string BashBody =
        "; __tf_n(){ local c; c=$(HISTTIMEFORMAT= history 1); c=${c#\"${c%%[0-9]*}\"}; printf '%s' \"${c%%[!0-9]*}\"; }"
        + "; __tf_h=$(__tf_n)"
        + "; __tf_p(){ local x=$? c n; c=$(HISTTIMEFORMAT= history 1); c=${c#\"${c%%[0-9]*}\"}; n=${c%%[!0-9]*}; c=${c#\"$n\"}; c=${c#  }; if [ \"$n\" != \"$__tf_h\" ]; then __tf_h=$n; __tf_e \"$c\"; fi; printf '\\e]133;D;%s;%s\\a' \"$x\" \"$__tf_nonce\"; }"
        + "; PROMPT_COMMAND=\"__tf_p${PROMPT_COMMAND:+;$PROMPT_COMMAND}\"";

    private const string ZshBody =
        "; __tf_pre(){ __tf_e \"$1\"; }"
        + "; __tf_post(){ printf '\\e]133;D;%s;%s\\a' \"$?\" \"$__tf_nonce\"; }"
        + "; autoload -Uz add-zsh-hook; add-zsh-hook preexec __tf_pre; add-zsh-hook precmd __tf_post";

    public static string For(ShellKind shell, string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        for (var i = 0; i < nonce.Length; i++)
        {
            if (!char.IsAsciiHexDigit(nonce[i]))
            {
                throw new ArgumentException("A shell integration nonce must contain only hexadecimal characters.", nameof(nonce));
            }
        }

        var body = shell == ShellKind.Zsh ? ZshBody : BashBody;
        return $" __tf_nonce='{nonce}';" + EscapeFunction + body;
    }
}
