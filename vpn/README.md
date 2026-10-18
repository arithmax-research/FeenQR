# VPN configuration

Place your OpenVPN client profile in this directory:

```
vpn/
├── feenqr.ovpn   # your provider's profile (or any single *.ovpn)
├── auth.txt      # optional: line 1 = username, line 2 = password
└── README.md
```

`feenqr.ovpn` is preferred; if it is absent, the launcher uses the first
`*.ovpn` in this directory. If the profile contains a bare `auth-user-pass`
line (most ProtonVPN, NordVPN, etc. profiles do), create `auth.txt`:

```
your-openvpn-username
your-openvpn-password
```

The username/password are usually **not** your normal provider login. For
ProtonVPN, use the OpenVPN/IKEv2 credentials shown in the ProtonVPN dashboard
under *Account → OpenVPN / IKEv2 username*. If OpenVPN still returns
`AUTH_FAILED`, reset/regenerate those credentials, download a fresh profile,
and make sure no other device is using the account when the plan allows only
one simultaneous connection.

Files in this directory other than this README are gitignored and excluded
from the Docker build context. Never commit a profile or credentials.

For a VPN scoped to the web application only, use the launcher or the Docker
override directly:

```bash
./launch.sh web --vpn

# Equivalent:
docker compose -f docker-compose.yml -f docker-compose.vpn.yml up --build
```

That starts OpenVPN inside `feenqr-web`; the host and other containers are
not routed through the tunnel. Open **http://localhost:8080** after the
container starts. `./launch.sh web` (without `--vpn`) does not start OpenVPN on
the host.

See the "VPN (OpenVPN)" section in the root `README.md` for all launch commands.
