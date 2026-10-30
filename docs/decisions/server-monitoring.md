# Server monitoring

## Decision

Basic metrics (connection state and latency, uptime, load average, CPU and CPU count, memory, free disk space) are collected on demand over the existing `IRemoteCommandRunner`. The backend is stateless: one `GET /api/connections/{id}/metrics` opens a connection, runs one shell script and closes the connection. Polling lives in the UI.

- One script (`MetricsScript`) prints `@@name` sections. It picks the strategy from `uname -s` in the same run, so collection does not depend on a cached capability report.
- Parsing is pure (`Core/Monitoring/Parsing`) and covered by recorded outputs.
- Every metric is a `MetricReading<T>`: either a value or an unavailable reason. The API returns `{ available, value, reason }`; there is no default value, so a missing tool never shows a misleading number.

## Collection by OS

| Metric | Linux (glibc, BusyBox) | macOS | Other BSD |
|---|---|---|---|
| CPU | two `/proc/stat` samples one second apart, busy = 1 - idle delta / total delta (idle includes iowait) | `top -l 1 -n 0` idle percentage | unavailable (reason names the OS) |
| Memory | `/proc/meminfo`: `MemTotal - MemAvailable`, falling back to `MemFree + Buffers + Cached` | `hw.memsize` and `vm_stat` (free + inactive + speculative pages count as available) | unavailable |
| Uptime | `/proc/uptime` | `kern.boottime` and `date +%s` | `kern.boottime` and `date +%s` |
| Disk | `df -Pk /` | `df -Pk /` | `df -Pk /` |
| Load average | `/proc/loadavg` | `sysctl -n vm.loadavg` | `sysctl -n vm.loadavg` |
| CPU count | `getconf _NPROCESSORS_ONLN`, then `nproc` | `getconf`, then `sysctl -n hw.ncpu` | `getconf`, then `sysctl -n hw.ncpu` |

Latency is the time the backend spends opening the SSH connection for the poll, so it includes the handshake and authentication.

Only the root filesystem is measured. A tool that is missing or denied produces an empty section and therefore an unavailable reason; the other metrics are still reported.

## Polling

- The UI polls while a view that shows metrics is mounted (sidebar metrics of the selected server, Diagnostics tab). Views for the same server share one poller.
- The interval is 10, 30 or 60 seconds (default 30), stored in the browser.
- Polling stops when the last view closes, when the server reports a state other than `Connected` or when a request fails. "Measure now" resumes it.
- A collection takes about one second on Linux because of the CPU sampling interval.
- The poller keeps the CPU samples of the last 15 minutes for the chart in the CPU card, the time since the connection was last established and the number of lost connections since local midnight. This history lives only in the browser and starts over when the last view of the server closes.

## Limits

- No shared connection pool: every poll opens a new SSH connection (documented follow-up for all features).
- No stored history or alerts; the CPU chart covers only the polls of the open view.
- CPU and memory are not collected on BSD systems other than macOS.
- Only the root filesystem is measured.
