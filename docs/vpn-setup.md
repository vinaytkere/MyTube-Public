# WireGuard VPN & Torrent Setup

MyTube includes an optional torrent downloader component (Transmission) designed with **privacy-by-default** and **fail-closed** guarantees. All torrent networking is routed through a dedicated WireGuard VPN tunnel managed by [Gluetun](https://github.com/qdm12/gluetun).

---

## Architecture: Fail-Closed Killswitch

```
+-------------------------------------------------------------+
|                      Host Machine                           |
|                                                             |
|   +-----------------------------------------------------+   |
|   |                  Gluetun Container                  |   |
|   |  - WireGuard Client (wg0 interface)                 |   |
|   |  - Strict iptables Firewall (Killswitch)            |   |
|   |  - Custom DNS & Port Forwarding                     |   |
|   +--------------------------^--------------------------+   |
|                              | Network Namespace            |
|   +--------------------------+--------------------------+   |
|   |                Transmission Container               |   |
|   |  - network_mode: service:gluetun                    |   |
|   |  - Has NO independent network interface             |   |
|   +-----------------------------------------------------+   |
+-------------------------------------------------------------+
```

### Why this design?
1. **Shared Network Namespace:** Transmission does not have an independent virtual network interface or gateway. It attaches directly to Gluetun's network stack.
2. **Hardware/Kernel Killswitch:** Gluetun configures container firewall rules (`iptables`) so that only traffic passing through the `wg0` tunnel interface is allowed out.
3. **Fail-Closed Guarantee:** If the VPN tunnel drops or the handshake times out, the firewall immediately drops all packets. Transmission can never leak your host IP address.

---

## Configuration Options

### Option A: Custom WireGuard Profile (Recommended for any provider)

If you have a `.conf` file from Mullvad, IVPN, TorGuard, ProtonVPN, or a self-hosted WireGuard peer:

1. Open your WireGuard configuration file and locate:
   ```ini
   [Interface]
   PrivateKey = aaaaaa...
   Address = 10.2.0.2/32
   DNS = 10.2.0.1

   [Peer]
   PublicKey = bbbbbb...
   Endpoint = 198.51.100.1:51820
   ```

2. Store the sensitive credentials in secret files:
   ```bash
   echo -n "aaaaaa..." > secrets/wireguard_private_key.secret
   echo -n "10.2.0.2/32" > secrets/wireguard_addresses.secret
   chmod 600 secrets/wireguard_*.secret
   ```

3. Configure server parameters in `.env`:
   ```env
   VPN_SERVICE_PROVIDER=custom
   VPN_TYPE=wireguard
   WIREGUARD_PUBLIC_KEY=bbbbbb...
   WIREGUARD_ENDPOINT_IP=198.51.100.1
   WIREGUARD_ENDPOINT_PORT=51820
   ```

---

## Verification & Leak Testing

Before downloading torrents, verify that the VPN tunnel and killswitch are functioning as expected:

### 1. Verify Public IP inside the Container
Run a query through the VPN container to ensure it reports your VPN provider's exit IP, not your home IP:

```bash
docker exec gluetun wget -qO- https://ipinfo.io
```

### 2. Verify DNS Leak Protection
Confirm that DNS queries are handled exclusively by Gluetun's internal secure DNS resolver:

```bash
docker exec gluetun nslookup whoami.akamai.net
```

### 3. Verify Fail-Closed Killswitch
Simulate a tunnel disconnect by temporarily bringing down the `wg0` interface inside the container:

```bash
# Bring down the VPN interface
docker exec gluetun ip link set wg0 down

# Attempt an external network connection (MUST fail or time out)
docker exec gluetun wget -T 5 -qO- https://ipinfo.io
```

Expected result: The command times out with no connection. No packets escape to your ISP.

To restore the tunnel:
```bash
docker compose -f deploy/docker-compose.torrent.yml restart gluetun
```

