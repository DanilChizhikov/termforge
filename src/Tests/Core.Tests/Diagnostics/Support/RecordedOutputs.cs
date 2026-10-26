namespace DTech.Core.Tests.Diagnostics.Support;

internal static class RecordedOutputs
{
    public const string UbuntuSystem =
        "Linux x86_64\n@@@@\nPRETTY_NAME=\"Ubuntu 24.04.1 LTS\"\nNAME=\"Ubuntu\"\nVERSION_ID=\"24.04\"\nID=ubuntu\nID_LIKE=debian\n@@@@\n/bin/bash\n";

    public const string AlpineSystem =
        "Linux aarch64\n@@@@\nNAME=\"Alpine Linux\"\nID=alpine\nVERSION_ID=3.22.1\nPRETTY_NAME=\"Alpine Linux v3.22\"\nHOME_URL=\"https://alpinelinux.org/\"\n@@@@\n/bin/sh\n";

    public const string AlpineWithoutPrettyName =
        "Linux x86_64\n@@@@\nNAME=\"Alpine Linux\"\nVERSION_ID=3.19.0\n@@@@\n/bin/ash\n";

    public const string DarwinWithoutOsRelease = "Darwin arm64\n@@@@\n@@@@\n/bin/zsh\n";

    public const string MissingEverything = "@@@@\n@@@@\n\n";

    public const string DockerPermissionDenied =
        "permission denied while trying to connect to the Docker daemon socket at unix:///var/run/docker.sock: Get \"http://%2Fvar%2Frun%2Fdocker.sock/v1.47/info\": dial unix /var/run/docker.sock: connect: permission denied\n";

    public const string DockerDaemonDown =
        "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?\n";

    public const string SudoPasswordRequired = "sudo: a password is required\n";

    public const string SudoTerminalRequired = "sudo: a terminal is required to read the password; either use the -S option to read from standard input or configure an askpass helper\n";

    public const string SudoNotInSudoers = "tester is not in the sudoers file.\n";

    public const string BusyBoxUtilities = "stat=yes\ndf=yes\nfree=yes\nuptime=yes\n";

    public const string MinimalContainerUtilities = "stat=yes\ndf=yes\nfree=no\nuptime=no\n";
}
