namespace DTech.Core.CommandHistory;

public static class ShellIntegrationSnippets
{
    private const string EscapeFunction =
        " __tf_e(){ local c=$1; c=${c//\\\\/\\\\\\\\}; c=${c//;/\\\\x3b}; c=${c//$'\\n'/\\\\x0a}; c=${c//$'\\a'/\\\\x07}; c=${c//$'\\e'/\\\\x1b}; printf '\\e]633;E;%s\\a' \"$c\"; }";

    private const string BashBody =
        "; __tf_n(){ local c; c=$(HISTTIMEFORMAT= history 1); c=${c#\"${c%%[0-9]*}\"}; printf '%s' \"${c%%[!0-9]*}\"; }"
        + "; __tf_h=$(__tf_n)"
        + "; __tf_p(){ local x=$? c n; c=$(HISTTIMEFORMAT= history 1); c=${c#\"${c%%[0-9]*}\"}; n=${c%%[!0-9]*}; c=${c#\"$n\"}; c=${c#  }; if [ \"$n\" != \"$__tf_h\" ]; then __tf_h=$n; __tf_e \"$c\"; fi; printf '\\e]133;D;%s\\a' \"$x\"; }"
        + "; PROMPT_COMMAND=\"__tf_p${PROMPT_COMMAND:+;$PROMPT_COMMAND}\"";

    private const string ZshBody =
        "; __tf_pre(){ __tf_e \"$1\"; }"
        + "; __tf_post(){ printf '\\e]133;D;%s\\a' \"$?\"; }"
        + "; autoload -Uz add-zsh-hook; add-zsh-hook preexec __tf_pre; add-zsh-hook precmd __tf_post";

    public static string For(ShellKind shell)
    {
        return shell == ShellKind.Zsh ? EscapeFunction + ZshBody : EscapeFunction + BashBody;
    }
}
