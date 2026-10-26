namespace DTech.Core.Tests.Docker.Support;

internal static class DockerRecordedOutputs
{
    public const string ContainerList =
        "{\"Command\":\"\\\"/docker-entrypoint.sh nginx -g 'daemon off;'\\\"\",\"CreatedAt\":\"2026-10-20 09:12:44 +0000 UTC\",\"ID\":\"3f4e1b2c9d8a7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8e7d6c5b4a3f2e\",\"Image\":\"nginx:1.27-alpine\",\"Labels\":\"maintainer=NGINX Docker Maintainers\",\"LocalVolumes\":\"0\",\"Mounts\":\"\",\"Names\":\"web\",\"Networks\":\"bridge\",\"Ports\":\"0.0.0.0:8080->80/tcp, [::]:8080->80/tcp\",\"RunningFor\":\"6 days ago\",\"Size\":\"0B\",\"State\":\"running\",\"Status\":\"Up 6 days (healthy)\"}\n"
        + "{\"Command\":\"\\\"docker-entrypoint.s…\\\"\",\"CreatedAt\":\"2026-10-19 18:01:02 +0000 UTC\",\"ID\":\"a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90\",\"Image\":\"postgres:17\",\"Labels\":\"\",\"LocalVolumes\":\"1\",\"Mounts\":\"pgdata\",\"Names\":\"db\",\"Networks\":\"bridge\",\"Ports\":\"\",\"RunningFor\":\"7 days ago\",\"Size\":\"0B\",\"State\":\"exited\",\"Status\":\"Exited (0) 2 hours ago\"}\n"
        + "{\"Command\":\"\\\"sleep infinity\\\"\",\"CreatedAt\":\"2026-10-25 07:30:00 +0000 UTC\",\"ID\":\"0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0\",\"Image\":\"alpine:3.22\",\"Labels\":\"\",\"LocalVolumes\":\"0\",\"Mounts\":\"\",\"Names\":\"worker.paused\",\"Networks\":\"bridge\",\"Ports\":\"\",\"RunningFor\":\"25 hours ago\",\"Size\":\"0B\",\"State\":\"paused\",\"Status\":\"Up 25 hours (Paused)\"}\n";

    public const string LegacyContainerWithoutState =
        "{\"ID\":\"0123456789ab\",\"Image\":\"redis:7\",\"Names\":\"/cache,/app/cache\",\"Ports\":\"6379/tcp\",\"Status\":\"Up 3 minutes (health: starting)\",\"CreatedAt\":\"2026-10-26 10:00:00 +0000 UTC\"}\n";

    public const string MalformedLines =
        "WARNING: Error loading config file: /home/deploy/.docker/config.json: permission denied\n"
        + "{\"ID\":\"broken\n"
        + "{\"ID\":\"bad id;rm -rf /\",\"Names\":\"evil\",\"Image\":\"x\",\"State\":\"running\",\"Status\":\"Up\"}\n"
        + "[1,2,3]\n"
        + "{\"ID\":\"cafebabe0001\",\"Names\":\"api\",\"Image\":\"api:latest\",\"State\":\"created\",\"Status\":\"Created\"}\n";

    public const string PermissionDenied =
        "permission denied while trying to connect to the Docker daemon socket at unix:///var/run/docker.sock: Get \"http://%2Fvar%2Frun%2Fdocker.sock/v1.47/containers/json?all=1\": dial unix /var/run/docker.sock: connect: permission denied\n";

    public const string DaemonDown = "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?\n";

    public const string CommandNotFound = "sh: 1: docker: not found\n";

    public const string NoSuchContainer = "Error response from daemon: No such container: ghost\n";

    public const string StopTimeoutFailure = "Error response from daemon: cannot stop container: web: tried to kill container, but did not receive an exit event\n";
}
