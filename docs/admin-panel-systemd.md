# HM Admin Panel — server config

First-time setup on the production host. Subsequent deploys are handled
automatically by `scripts/deploy.sh`.

## systemd unit (`/etc/systemd/system/hm-admin.service`)

```ini
[Unit]
Description=HM Admin Panel
After=network.target

[Service]
WorkingDirectory=/var/www/hm-admin
ExecStart=/usr/bin/dotnet /var/www/hm-admin/HM.AdminPanel.dll --urls=http://127.0.0.1:5050
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
User=www-data
Group=www-data

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable hm-admin
sudo systemctl start hm-admin
```

## nginx (`/etc/nginx/sites-available/admin.hm.fustani.cloud`)

```nginx
server {
    listen 443 ssl http2;
    server_name admin.hm.fustani.cloud;

    ssl_certificate     /etc/letsencrypt/live/admin.hm.fustani.cloud/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/admin.hm.fustani.cloud/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5050;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name admin.hm.fustani.cloud;
    return 301 https://$host$request_uri;
}
```

Enable, certbot-issue, reload:

```bash
sudo ln -s /etc/nginx/sites-available/admin.hm.fustani.cloud /etc/nginx/sites-enabled/
sudo certbot --nginx -d admin.hm.fustani.cloud
sudo nginx -t && sudo systemctl reload nginx
```

## Production `appsettings.json`

Located at `/var/www/hm-admin/appsettings.json`. Preserved across deploys
by `scripts/deploy.sh` (rsync `--exclude='appsettings.json'`).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<prod connection string>"
  },
  "AdminPanel": {
    "WebApiBaseUrl": "https://hm.fustani.cloud",
    "SignalRHubUrl": "https://hm.fustani.cloud/hubs/shipment-tracking"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*"
}
```

Initial SuperAdmin credentials live in `/var/www/hm/appsettings.json`
(WebApi project) under `AdminPanel:SeedAdmin:{Email,Password}` — that's
where DbSeeder runs from. Change the password from inside the admin
panel after first login (Phase 2 feature) or by hashing a new one
with `dotnet user-secrets` against the WebApi.

## First-time deploy steps

1. `git pull origin main` on the prod box (or wait for next deploy run).
2. Publish admin once manually: `dotnet publish HM.AdminPanel/HM.AdminPanel.csproj -c Release -o /var/www/hm-admin`.
3. Create `/var/www/hm-admin/appsettings.json` with prod values above.
4. Install systemd unit + nginx config above.
5. Add `AdminPanel:SeedAdmin` section to `/var/www/hm/appsettings.json` (WebApi config) so the default SuperAdmin gets seeded on next WebApi restart.
6. Restart `hm` service to seed the role + SuperAdmin user.
7. Verify `https://admin.hm.fustani.cloud/Account/Login` returns the login page.

Subsequent deploys run via `scripts/deploy.sh`.
