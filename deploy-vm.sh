#!/usr/bin/env bash
set -euo pipefail

echo "=========================================================="
echo "  Resin Art by Dhara — Production VM Setup & Deploy"
echo "  (Supports Oracle Cloud & Google Cloud Ubuntu VMs)"
echo "=========================================================="

# 1. Add 2GB Swap if total RAM < 3GB (prevents OOM during Docker build on 1GB e2-micro VMs)
TOTAL_MEM_MB=$(awk '/MemTotal/ {print int($2/1024)}' /proc/meminfo)
if [ "$TOTAL_MEM_MB" -lt 3000 ] && [ ! -f /swapfile ]; then
    echo "[1/5] Creating 2GB swapfile for low-memory VM (${TOTAL_MEM_MB}MB RAM)..."
    sudo fallocate -l 2G /swapfile
    sudo chmod 600 /swapfile
    sudo mkswap /swapfile
    sudo swapon /swapfile
    echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
else
    echo "[1/5] Memory/Swap check OK (${TOTAL_MEM_MB}MB RAM)."
fi

# 2. Install Docker & Docker Compose plugin if missing
if ! command -v docker >/dev/null 2>&1; then
    echo "[2/5] Installing Docker Engine & Compose plugin..."
    curl -fsSL https://get.docker.com | sudo sh
    sudo usermod -aG docker "$USER" || true
else
    echo "[2/5] Docker is already installed."
fi

# 3. Open OS firewall ports 80 and 443 (especially important on Oracle Cloud Ubuntu images)
echo "[3/5] Opening HTTP (80) and HTTPS (443) in OS firewall..."
if command -v ufw >/dev/null 2>&1 && sudo ufw status | grep -q "Status: active"; then
    sudo ufw allow 80/tcp
    sudo ufw allow 443/tcp
    sudo ufw allow 443/udp
fi
if sudo iptables -L INPUT -n | grep -q "REJECT"; then
    sudo iptables -I INPUT 5 -p tcp --dport 80 -m state --state NEW -j ACCEPT
    sudo iptables -I INPUT 5 -p tcp --dport 443 -m state --state NEW -j ACCEPT
    sudo iptables -I INPUT 5 -p udp --dport 443 -j ACCEPT
    if command -v netfilter-persistent >/dev/null 2>&1; then
        sudo netfilter-persistent save
    fi
fi

# 4. Create .env file if it doesn't exist
if [ ! -f .env ]; then
    echo "[4/5] Configuring domain & initial admin credentials..."
    read -rp "Enter your domain name (e.g. resinartbydhara.in): " INPUT_DOMAIN
    INPUT_DOMAIN=${INPUT_DOMAIN:-localhost}
    read -rsp "Enter a strong password for admin user 'dhara': " INPUT_PASS
    echo ""
    INPUT_PASS=${INPUT_PASS:-ResinArt@2026}

    cat > .env <<EOF
DOMAIN_NAME=${INPUT_DOMAIN}
ADMIN_INITIAL_USERNAME=dhara
ADMIN_INITIAL_PASSWORD=${INPUT_PASS}
EOF
    chmod 600 .env
    echo "Saved configuration to .env"
else
    echo "[4/5] Using existing .env file."
fi

# 5. Build and launch containers
echo "[5/5] Building and starting Resin Art by Dhara + Caddy HTTPS proxy..."
sudo docker compose up -d --build

echo ""
echo "=========================================================="
echo "  Deployment Complete!"
echo "  Storefront : https://$(grep '^DOMAIN_NAME=' .env | cut -d= -f2)"
echo "  Admin      : https://$(grep '^DOMAIN_NAME=' .env | cut -d= -f2)/admin"
echo "=========================================================="
