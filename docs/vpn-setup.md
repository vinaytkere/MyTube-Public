# WireGuard VPN & Torrent Setup

MyTube includes an optional torrent downloader component (Transmission) designed with **privacy-by-default** and **fail-closed** guarantees. All torrent networking is routed through a dedicated WireGuard VPN tunnel managed by [Gluetun](https://github.com/qdm12/gluetun).

---

## 1. Quick Overview & Official WireGuard Links

- **Official WireGuard Project:** [https://www.wireguard.com/](https://www.wireguard.com/)
- **Official WireGuard Software Downloads:** [https://www.wireguard.com/install/](https://www.wireguard.com/install/)

> [!NOTE]
> **Do you need to install WireGuard on your host OS?**  
> **No.** When you enable torrents in MyTube, the setup script deploys the WireGuard client automatically inside the `mytube-vpn` (Gluetun) Docker container.  
> You only need to provide your **VPN Provider**, your **Assigned Client IP/CIDR**, and your **Private Key**.

---

## 2. Understanding the Required Settings

During setup step `[10/16] WireGuard VPN Configuration`, the installer prompts:

| Prompt | What It Means | Where to Find It | Example |
| :--- | :--- | :--- | :--- |
| **VPN Provider** | The name of your VPN service (supported natively by Gluetun) | Your VPN provider name | `mullvad`, `protonvpn`, `airvpn`, `ivpn`, or `custom` |
| **Assigned Client IP/CIDR** | The internal tunnel IP address given to your client by the VPN | `Address =` inside your WireGuard `.conf` file | `10.2.0.2/32` or `10.64.0.2/32` |
| **WireGuard Private Key** | 32-byte Base64-encoded secret key used to authenticate your device | `PrivateKey =` inside your WireGuard `.conf` file | `8LBUNcJwJcs3aXd+wuxPcEG8dMaPjatDwuZaO6dtKWY=` |

Inside any standard WireGuard configuration file (`.conf`), these map directly:

```ini
[Interface]
PrivateKey = <Your WireGuard Private Key>       <-- Paste into "WireGuard Private Key"
Address = <Your Assigned Client IP/CIDR>        <-- Paste into "WireGuard Assigned Client IP/CIDR"
DNS = 10.2.0.1

[Peer]
PublicKey = <VPN Server Public Key>             <-- Required only if VPN Provider is "custom"
Endpoint = <VPN Server IP:Port>                 <-- Required only if VPN Provider is "custom"
AllowedIPs = 0.0.0.0/0
```

---

## 3. Step-by-Step: Obtaining Credentials from Providers

### Option 1: Mullvad VPN
1. Log into your account on the [Mullvad Account Page](https://mullvad.net/account).
2. Go to **WireGuard configuration** ([mullvad.net/account/#/wireguard-config](https://mullvad.net/account/#/wireguard-config)).
3. Click **Generate a new key** (or select an existing device key).
4. Select your preferred exit country and city.
5. Click **Download file** to get the `.conf` file.
6. Open the downloaded file in a text editor:
   - Copy `Address` (e.g. `10.64.12.34/32`)
   - Copy `PrivateKey` (e.g. `yAnZ...=`)
7. In the MyTube installer:
   - **VPN Provider**: `mullvad`
   - **Assigned Client IP**: Paste `Address`
   - **Private Key**: Paste `PrivateKey`

---

### Option 2: Proton VPN (Free or Paid)
1. Log into your [Proton VPN Account Dashboard](https://account.protonvpn.com/).
2. In the left navigation bar, click **Downloads**, then scroll to **WireGuard configuration** ([account.protonvpn.com/downloads#wireguard-configuration](https://account.protonvpn.com/downloads#wireguard-configuration)).
3. Select your device/platform: Choose **GNU/Linux** or **Router**.
4. Select the server location/category you want and click **Create** / **Download configuration**.
5. Open the downloaded `.conf` file:
   - Copy `Address` (e.g. `10.2.0.2/32`)
   - Copy `PrivateKey`
6. In the MyTube installer:
   - **VPN Provider**: `protonvpn`
   - **Assigned Client IP**: Paste `Address`
   - **Private Key**: Paste `PrivateKey`

---

### Option 3: AirVPN, IVPN, Windscribe, or TorGuard
1. Log into your VPN provider's user dashboard.
2. Navigate to the **WireGuard Configuration Generator** or **Manual Setup** tab.
3. Generate a key pair and download the configuration file (`.conf`).
4. Copy `Address` and `PrivateKey` from under the `[Interface]` header.
5. In the MyTube installer, type your provider name (e.g., `airvpn`, `ivpn`, etc.).

---

### Option 4: Custom VPS / Self-Hosted WireGuard Server

If you operate your own WireGuard server (e.g. on Oracle Cloud, AWS, DigitalOcean, or Hetzner):

#### Step 1: Install WireGuard Tools on your machine (to generate keys)
- **Linux (Ubuntu/Debian):**
  ```bash
  sudo apt update && sudo apt install -y wireguard-tools
  ```
- **Linux (Fedora/RHEL):**
  ```bash
  sudo dnf install -y wireguard-tools
  ```
- **macOS (via Homebrew):**
  ```bash
  brew install wireguard-tools
  ```
- **Windows:**
  Download and install the official client from [https://www.wireguard.com/install/](https://www.wireguard.com/install/). Alternatively, use PowerShell to generate a base64 key or use WSL.

#### Step 2: Generate a WireGuard Key Pair
```bash
wg genkey | tee privatekey | wg pubkey > publickey
```
- The `privatekey` file contains your **Client Private Key**.
- The `publickey` file is your **Client Public Key**, which you register on your server (`/etc/wireguard/wg0.conf`).

#### Step 3: Configure the Client Peer on Your Server
Add your client peer to your server config:
```ini
[Peer]
PublicKey = <Contents of client publickey>
AllowedIPs = 10.213.122.4/32
```

#### Step 4: Run MyTube Setup
In the MyTube installer:
- **VPN Provider**: `custom`
- **WireGuard Assigned Client IP/CIDR**: `10.213.122.4/32`
- **WireGuard Private Key**: Paste contents of `privatekey`

Then add your server's endpoint and public key to `.env`:
```env
WIREGUARD_PUBLIC_KEY=<Server_Public_Key>
WIREGUARD_ENDPOINT_IP=<Server_Public_IP>
WIREGUARD_ENDPOINT_PORT=51820
```

---

## 4. Architecture: Fail-Closed Killswitch

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
   echo -n "aaaaaa..." > deploy/secrets/wireguard_private_key.txt
   chmod 600 deploy/secrets/wireguard_private_key.txt
   ```

3. Configure server parameters in `.env`:
   ```env
   VPN_PROVIDER=custom
   VPN_WIREGUARD_ADDRESSES=10.2.0.2/32
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

