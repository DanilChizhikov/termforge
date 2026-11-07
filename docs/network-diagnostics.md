# Network diagnostics

Diagnostics run manually from the local machine and selected saved Linux SSH points. No checks start automatically after a connection failure.

## Interface

Open the separate Network diagnostics workspace tab to choose a saved target or custom address, ports and ranges, protocols and up to eight measurement points. Server capabilities and monitoring remain in Diagnostics. DNS domain and HTTP URL are available under Additional parameters. Choose measurement points opens a searchable dialog that matches names, hosts and groups; Apply selection saves the temporary selection, while Cancel or Escape discards it. The local machine counts toward the eight-point limit. Selected points stay visible with explicit Russia / Outside Russia categories, stored as `rf` / `outsideRf`. Connection test failures and disconnected terminals offer a Network diagnostics button that fills the saved target without starting checks.

The form, progress and current results remain available when switching tabs or settings until another test starts or the application closes. Settings and results have separate scroll areas; results use a sticky table header and pages of 100. Result labels remain on one line. Details show a shortened preview with View details opening a scrollable, monospaced view of the received text. Progress uses realtime events with periodic refresh and reconnect recovery. Cancel stops pending probes and retains collected results. Full scans require a separate confirmation, including when the complete range was typed manually.

Optional remote actions are prepared in a separate disclosure section, initially expanded when an SSH point is already selected. Select an SSH measurement point and review the exact server, packages, command, privileges and warning before confirming installation or a privileged UDP check. Read-only servers can run ordinary checks but cannot perform these actions. A sudo password stays only in the current confirmation input and is cleared on submission or cancellation. Copying and exporting reports are not provided.

## Requests and results

`POST /api/network-diagnostics` accepts a saved `targetConnectionId` or an arbitrary `host`. Optional `domain` selects the DNS/TLS name; `url` selects an HTTP(S) URL without embedded credentials. `protocols` selects `dns`, `tcp`, `udp`, `ssh`, `tls`, `http`, `route`. `ports` accepts comma-separated numbers and inclusive ranges; `allPorts` selects 1–65535. Defaults are 22, 80 and 443, replacing 22 with the saved connection's port when targeting a saved connection.

`points` contains 1–8 distinct `{ connectionId, category }` objects. A null connection identifies the local machine. Categories are explicitly supplied as `rf` or `outsideRf`; they are not inferred from IP addresses.

```json
{
  "host": "example.com",
  "domain": "example.com",
  "url": "https://example.com/",
  "protocols": ["dns", "tcp", "tls", "http"],
  "ports": "22,80,443,8000-8002",
  "points": [{ "connectionId": null, "category": "rf" }]
}
```

DNS and route run once per point. HTTP with an explicit URL checks that URL once; otherwise HTTP, TCP, UDP, SSH and TLS check the selected ports. HTTP redirects are not followed. TLS validates certificates. SSH uses installed `ssh-keyscan` for key exchange without login; collected keys do not establish host trust and do not change saved host keys.

The response is 202 with the run ID and progress snapshot. `GET /api/network-diagnostics` lists runs; `GET /api/network-diagnostics/{id}` reads progress. `GET /api/network-diagnostics/{id}/results?offset=0&limit=100` reads results, with a maximum page size of 1000. `POST /api/network-diagnostics/{id}/cancel` cancels pending work. Results remain in memory until application shutdown. Snapshots are broadcast as `network-diagnostics.changed` on `/api/ws` every 250 ms while running and on completion. Clients fetch new result pages using the completed count.

Results identify the point, its category, protocol, port, duration, and an outcome: `responded`, `refused`, `noResponse`, `error`, `unavailable`. A failed or unavailable check does not stop other checks. Silence from UDP is inconclusive. Neither a timeout nor a regional difference establishes filtering by a particular organization.

Two runs execute at a time, with up to eight waiting. Each point shares a limit of 16 concurrent probes and 50 new probes per second across runs. Remote points use at most four SSH exec channels per run. Socket checks time out after three seconds; HTTP and local route checks after ten seconds. SSH key scanning uses a three-second network timeout with two additional seconds for process completion. Remote command cleanup also allows a small additional timeout. Cancelling a run stops its work; it does not delete collected results.

## Full port scans

A full scan requires `POST /api/network-diagnostics/prepare` with the same request. Review the returned normalized plan, then submit its `confirmation.token` as `confirmationToken` to the start endpoint. The token expires after two minutes, is single-use, and is bound to the host, domain, URL, ports, protocols and points. Writing `1-65535` manually also requires confirmation. Normal scans require no token.

## Remote tools and privileged checks

Ordinary remote checks use installed `dig`, `nc`, `ssh-keyscan`, `openssl`, `curl`, `traceroute` and, for TLS, `timeout`. Unsupported tool variants are reported as unavailable. Read-only servers may run these checks. The local machine uses .NET sockets/DNS/TLS/HTTP and installed SSH/route tools; no local software is installed.

`POST /api/network-diagnostics/remote-actions/prepare` prepares an action:

- `{ connectionId, kind: "install", protocols: ["dns", "tcp"], usePassword: false }` selects allowlisted packages for apt-get, dnf, apk, pacman or zypper. Use protocol `udpDetails` to install nmap. Unknown package managers require manual installation.
- `{ connectionId, kind: "udpDetails", host, port, usePassword: false }` prepares a privileged nmap UDP check for one port. Its `open|filtered` result remains inconclusive.

Review the returned server, packages, exact command, sudo requirement and warning. Execute with `POST /api/network-diagnostics/remote-actions/execute` and `{ planId, confirmationToken, password? }`. Plans expire after two minutes and execute only once. A changed saved connection or read-only setting invalidates execution. Commands cannot be supplied or edited by the client; changing an action requires a new plan and confirmation.

The sudo password is sent through SSH stdin, never through command arguments, and is not stored in result or journal data. No full system upgrade, untrusted download or automatic privileged action is performed. Package operations may change dependencies and run distribution hooks. Cancellation cannot roll back packages already changed; available utilities are refreshed after an execution attempt when the connection is still available. Installers use configured system repositories and retain their signature checks.

## Limits

Remote command errors and timeouts may reflect utility, DNS, routing, firewall or permission differences. UDP sends a single zero byte and waits for a reply; protocol-specific UDP payloads are outside this implementation. Route tools may require permissions or remain unavailable. Results and remote action plans do not survive application restart. Export, VPN and subscriptions are outside this feature.
